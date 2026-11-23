using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Xunit;
using DevPack4Dataverse.ExecuteTransaction;
using DevPack4Dataverse.Interfaces;
using DevPack4Dataverse.Models;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

public class SdkProxyTests
{
    [Fact]
    public void GetConnection_ReturnsLeasedConnection()
    {
        var fake = new FakeConnection();
        var proxy = new SdkProxy(NullLogger.Instance, false, new FakeConnectionCreator(fake));

        using var lease = proxy.GetConnection();

        Assert.Same(fake, lease.Connection);
    }

    [Fact]
    public async Task GetConnectionAsync_WaitsForTheSingleConnectionPermit()
    {
        var fake = new FakeConnection { UseRealLock = true };
        using var proxy = new SdkProxy(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(fake)
        );
        proxy.WaitForConnections(1);
        using ConnectionLease first = proxy.GetConnection();

        Task<ConnectionLease> second = proxy.GetConnectionAsync();
        Task completed = await Task.WhenAny(second, Task.Delay(150));
        Assert.NotSame(second, completed);

        first.Dispose();
        Task completedSecond = await Task.WhenAny(
            second,
            Task.Delay(TimeSpan.FromSeconds(5))
        );
        Assert.Same(second, completedSecond);
        using ConnectionLease lease = await second;
        Assert.Same(fake, lease.Connection);
    }

    [Fact]
    public async Task WaitForConnections_DuplicateCreator_ThrowsInsteadOfHanging()
    {
        var creator = new FakeConnectionCreator(new FakeConnection());
        using var proxy = new SdkProxy(
            NullLogger.Instance,
            false,
            creator,
            creator
        );
        proxy.WaitForConnections(1);

        Task wait = Task.Run(() => proxy.WaitForConnections(2));
        Task completed = await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(wait, completed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => wait);
    }

    [Fact]
    public void GetConnection_WithoutCreators_Throws()
    {
        var proxy = new SdkProxy(NullLogger.Instance, false);

        Assert.ThrowsAny<ArgumentException>(() => proxy.GetConnection());
    }

    [Fact]
    public void ExecuteTransaction_BuilderSendsRequest()
    {
        OrganizationRequest? captured = null;
        var fake = new FakeConnection
        {
            ExecuteHandler = request =>
            {
                captured = request;
                return new ExecuteTransactionResponse();
            }
        };
        using var proxy = new SdkProxy(NullLogger.Instance, false, new FakeConnectionCreator(fake));
        proxy.WaitForConnections(1);
        var builder = new ExecuteTransactionRequestBuilder(NullLogger.Instance);
        builder.AddCreate(new Entity("account"));

        ExecuteTransactionResponse response = proxy.Execute(builder);

        Assert.NotNull(response);
        var request = Assert.IsType<ExecuteTransactionRequest>(captured);
        Assert.Single(request.Requests);
        Assert.True(request.ReturnResponses);
    }

    [Fact]
    public async Task ExecuteTransactionAsync_BuilderSendsRequest()
    {
        OrganizationRequest? captured = null;
        var fake = new FakeConnection
        {
            ExecuteHandler = request =>
            {
                captured = request;
                return new ExecuteTransactionResponse();
            }
        };
        using var proxy = new SdkProxy(NullLogger.Instance, false, new FakeConnectionCreator(fake));
        await proxy.WaitForConnectionsAsync(1);
        var builder = new ExecuteTransactionRequestBuilder(NullLogger.Instance, returnResponses: false);
        builder.AddCreate(new Entity("account"));

        ExecuteTransactionResponse response = await proxy.ExecuteAsync(builder);

        Assert.NotNull(response);
        var request = Assert.IsType<ExecuteTransactionRequest>(captured);
        Assert.Single(request.Requests);
        Assert.False(request.ReturnResponses);
    }

    [Fact]
    public void ExecuteTransaction_AppliesBatchSettingsToInnerRequests()
    {
        OrganizationRequest? captured = null;
        var fake = new FakeConnection
        {
            ExecuteHandler = request =>
            {
                captured = request;
                return new ExecuteTransactionResponse();
            }
        };
        using var proxy = new SdkProxy(NullLogger.Instance, false, new FakeConnectionCreator(fake));
        proxy.WaitForConnections(1);
        var builder = new ExecuteTransactionRequestBuilder(NullLogger.Instance);
        builder.AddUpdate(new Entity("account") { Id = Guid.NewGuid() });

        proxy.Execute(
            builder,
            new RequestSettings
            {
                ConcurrencyBehavior = ConcurrencyBehavior.IfRowVersionMatches,
                SkipPluginExecution = true,
            }
        );

        var request = Assert.IsType<ExecuteTransactionRequest>(captured);
        var update = Assert.IsType<UpdateRequest>(Assert.Single(request.Requests));
        Assert.Equal(ConcurrencyBehavior.IfRowVersionMatches, update.ConcurrencyBehavior);
        Assert.Equal(true, update["BypassCustomPluginExecution"]);
    }

    [Fact]
    public async Task GetConnection_WhenAllCreatorsFail_ThrowsInsteadOfHanging()
    {
        using var proxy = new SdkProxy(NullLogger.Instance, false, new FailingCreator());
        Task<ConnectionLease> task = Task.Run(() => proxy.GetConnection());

        Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(task, completed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => task);
    }

    [Fact]
    public async Task GetConnection_RespectsCreatorBackoff()
    {
        var creator = new BackoffCreator();
        using var proxy = new SdkProxy(NullLogger.Instance, false, creator);
        Task<ConnectionLease> leaseTask = Task.Run(() => proxy.GetConnection());

        await Task.Delay(200);
        Assert.Equal(0, creator.Attempts);

        creator.Ready = true;
        Task completed = await Task.WhenAny(
            leaseTask,
            Task.Delay(TimeSpan.FromSeconds(5))
        );
        Assert.Same(leaseTask, completed);
        using ConnectionLease lease = await leaseTask;
        Assert.Equal(1, creator.Attempts);
        Assert.Same(creator.Connection, lease.Connection);
    }

    private sealed class BackoffCreator : IConnectionCreator, IConnectionCreatorRetry
    {
        public FakeConnection Connection { get; } = new();
        public int Attempts;
        public bool Ready { get; set; }
        public bool IsCreated { get; private set; }
        public bool IsError { get; private set; }
        public bool IsValid => IsCreated && !IsError;
        public int ConnectionCount => 1;
        public bool CanCreateNow => Ready;

        public IReadOnlyList<IConnection> Create(ILogger logger)
        {
            Attempts++;
            if (!Ready)
            {
                throw new InvalidOperationException("not ready");
            }

            IsCreated = true;
            return new IConnection[] { Connection };
        }
    }

    private sealed class FailingCreator : IConnectionCreator
    {
        public bool IsCreated { get; private set; }
        public bool IsError { get; private set; }
        public bool IsValid => IsCreated && !IsError;
        public int ConnectionCount => 1;

        public IReadOnlyList<IConnection> Create(ILogger logger)
        {
            IsError = true;
            throw new InvalidOperationException("Connection creation failed.");
        }
    }
}
