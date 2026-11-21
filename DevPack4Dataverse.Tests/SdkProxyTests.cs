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
