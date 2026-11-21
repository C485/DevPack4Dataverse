using System.Diagnostics;
using DevPack4Dataverse.Tests.TestDoubles;
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
}
