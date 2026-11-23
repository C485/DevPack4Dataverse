using System.Diagnostics;
using DevPack4Dataverse.Interfaces;
using DevPack4Dataverse.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevPack4Dataverse.Tests;

public class SdkProxyDisposeTests
{
    [Fact]
    public async Task DisposeAsync_DoesNotWaitForeverForABusyConnection()
    {
        var fake = new FakeConnection
        {
            WaitForIdleHandler = token => Task.Delay(Timeout.InfiniteTimeSpan, token)
        };
        using var proxy = new SdkProxy(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(fake)
        );
        proxy.WaitForConnections(1);
        proxy.DisposeWaitTimeoutOverride = TimeSpan.FromMilliseconds(50);

        var stopwatch = Stopwatch.StartNew();
        await proxy.DisposeAsync();
        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Dispose waited for {stopwatch.Elapsed}."
        );
    }

    [Fact]
    public async Task DisposeAsync_DoesNotWaitForeverForABlockedCreator()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var proxy = new SdkProxy(
            NullLogger.Instance,
            false,
            new BlockingCreator(entered, release)
        );
        proxy.DisposeWaitTimeoutOverride = TimeSpan.FromMilliseconds(50);

        try
        {
            Assert.True(
                entered.Wait(TimeSpan.FromSeconds(5)),
                "The connection creator did not start."
            );
            var stopwatch = Stopwatch.StartNew();

            await proxy.DisposeAsync();

            stopwatch.Stop();
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"Dispose waited for {stopwatch.Elapsed}."
            );
        }
        finally
        {
            release.Set();
        }
    }

    private sealed class BlockingCreator(
        ManualResetEventSlim entered,
        ManualResetEventSlim release
    ) : IConnectionCreator
    {
        public bool IsCreated { get; private set; }
        public bool IsError { get; private set; }
        public bool IsValid => IsCreated && !IsError;
        public int ConnectionCount => 1;

        public IReadOnlyList<IConnection> Create(ILogger logger)
        {
            entered.Set();
            release.Wait();
            IsCreated = true;
            return new IConnection[] { new FakeConnection() };
        }
    }
}
