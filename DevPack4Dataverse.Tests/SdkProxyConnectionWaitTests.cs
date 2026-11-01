using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DevPack4Dataverse.Interfaces;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

/// <summary>
/// Black-box tests for waiting until a minimum number of connections has been created.
/// </summary>
public class SdkProxyConnectionWaitTests
{
    private static SdkProxy Create(params IConnectionCreator[] creators) =>
        new(NullLogger.Instance, false, creators);

    [Fact]
    public void WaitForConnections_ReturnsWhenCountIsReached()
    {
        using var proxy = Create(
            new FakeConnectionCreator(new FakeConnection()),
            new FakeConnectionCreator(new FakeConnection())
        );

        proxy.WaitForConnections(2);

        Assert.Equal(2, proxy.ConnectionCount);
    }

    [Fact]
    public void WaitForConnections_Zero_ReturnsImmediately()
    {
        using var proxy = Create();

        proxy.WaitForConnections(0);

        Assert.Equal(0, proxy.ConnectionCount);
    }

    [Fact]
    public void WaitForConnections_NegativeCount_Throws()
    {
        using var proxy = Create();

        Assert.ThrowsAny<ArgumentException>(() => proxy.WaitForConnections(-1));
    }

    [Fact]
    public void WaitForConnections_MoreThanConfiguredCreators_Throws()
    {
        using var proxy = Create(new FakeConnectionCreator(new FakeConnection()));

        Assert.Throws<InvalidOperationException>(() => proxy.WaitForConnections(2));
    }

    [Fact]
    public void WaitForConnections_FailedCreator_Throws()
    {
        using var proxy = Create(new FailingConnectionCreator());

        Assert.Throws<InvalidOperationException>(() => proxy.WaitForConnections(1));
    }

    [Fact]
    public void WaitForConnections_AfterDispose_Throws()
    {
        var proxy = Create(new FakeConnectionCreator(new FakeConnection()));
        proxy.Dispose();

        Assert.Throws<ObjectDisposedException>(() => proxy.WaitForConnections(1));
    }

    [Fact]
    public async Task WaitForConnectionsAsync_ReturnsWhenCountIsReached()
    {
        using var proxy = Create(
            new FakeConnectionCreator(new FakeConnection()),
            new FakeConnectionCreator(new FakeConnection())
        );

        await proxy.WaitForConnectionsAsync(2);

        Assert.Equal(2, proxy.ConnectionCount);
    }

    [Fact]
    public async Task WaitForConnectionsAsync_Zero_ReturnsImmediately()
    {
        using var proxy = Create();

        await proxy.WaitForConnectionsAsync(0);

        Assert.Equal(0, proxy.ConnectionCount);
    }

    [Fact]
    public async Task WaitForConnectionsAsync_NegativeCount_Throws()
    {
        using var proxy = Create();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => proxy.WaitForConnectionsAsync(-1));
    }

    [Fact]
    public async Task WaitForConnectionsAsync_MoreThanConfiguredCreators_Throws()
    {
        using var proxy = Create(new FakeConnectionCreator(new FakeConnection()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.WaitForConnectionsAsync(2));
    }

    [Fact]
    public async Task WaitForConnectionsAsync_FailedCreator_Throws()
    {
        using var proxy = Create(new FailingConnectionCreator());

        await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.WaitForConnectionsAsync(1));
    }

    [Fact]
    public async Task WaitForConnectionsAsync_CanBeCanceled()
    {
        using var release = new ManualResetEventSlim(false);
        using var proxy = Create(new BlockingConnectionCreator(release));
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => proxy.WaitForConnectionsAsync(1, cancellationTokenSource.Token)
        );

        release.Set();
    }

    [Fact]
    public async Task WaitForConnectionsAsync_AfterDispose_Throws()
    {
        var proxy = Create(new FakeConnectionCreator(new FakeConnection()));
        proxy.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => proxy.WaitForConnectionsAsync(1));
    }

    [Fact]
    public void DataverseDevPack_WaitForConnections_Delegates()
    {
        var devPack = new DataverseDevPack(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(new FakeConnection())
        );

        try
        {
            devPack.WaitForConnections(1);
            Assert.Equal(1, devPack.SdkProxy.ConnectionCount);
        }
        finally
        {
            devPack.SdkProxy.Dispose();
        }
    }

    private sealed class FailingConnectionCreator : IConnectionCreator
    {
        public bool IsCreated { get; private set; }
        public bool IsError { get; private set; }
        public bool IsValid => IsCreated && !IsError;

        public IConnection Create(ILogger logger)
        {
            IsError = true;
            throw new InvalidOperationException("Connection creation failed.");
        }
    }

    private sealed class BlockingConnectionCreator(ManualResetEventSlim release) : IConnectionCreator
    {
        public bool IsCreated { get; private set; }
        public bool IsError { get; private set; }
        public bool IsValid => IsCreated && !IsError;

        public IConnection Create(ILogger logger)
        {
            release.Wait();
            IsCreated = true;
            return new FakeConnection();
        }
    }
}
