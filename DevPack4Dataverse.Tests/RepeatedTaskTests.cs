using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DevPack4Dataverse.Utils;

namespace DevPack4Dataverse.Tests;

public class RepeatedTaskTests
{
    [Fact]
    public async Task RunsActionRepeatedly_AndStops()
    {
        var count = 0;
        var task = new RepeatedTask(
            TimeSpan.FromMilliseconds(15),
            () => Interlocked.Increment(ref count),
            NullLogger.Instance);

        task.Start();
        await Task.Delay(250);
        await task.StopAsync();

        Assert.True(count >= 1, $"Expected the action to run at least once but it ran {count} time(s).");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveInterval_Throws(int milliseconds)
    {
        Assert.Throws<ArgumentException>(
            () =>
                new RepeatedTask(
                    TimeSpan.FromMilliseconds(milliseconds),
                    () => { },
                    NullLogger.Instance
                )
        );
    }

    [Fact]
    public async Task StopWithoutStart_IsNoOp()
    {
        var task = new RepeatedTask(TimeSpan.FromMilliseconds(15), () => { }, NullLogger.Instance);

        await task.StopAsync();
    }
}
