using System.Collections.Concurrent;
using DevPack4Dataverse.Utils;
using Xunit;

namespace DevPack4Dataverse.Tests;

public class AsyncParallelTests
{
    [Fact]
    public async Task Fallback_ProcessesEveryItem()
    {
        var seen = new ConcurrentBag<int>();

        await AsyncParallel.ForEachWithSemaphoreAsync(
            Enumerable.Range(0, 20),
            4,
            CancellationToken.None,
            (item, _) =>
            {
                seen.Add(item);
                return Task.CompletedTask;
            }
        );

        Assert.Equal(Enumerable.Range(0, 20), seen.OrderBy(item => item));
    }

    [Fact]
    public async Task Fallback_StopsTakingNewItemsWhenAWorkerFails()
    {
        int bodyCalls = 0;

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    AsyncParallel.ForEachWithSemaphoreAsync(
                        Enumerable.Range(0, 20),
                        2,
                        CancellationToken.None,
                        async (item, token) =>
                        {
                            Interlocked.Increment(ref bodyCalls);
                            await Task.Delay(20, token);
                            if (item == 0)
                            {
                                throw new InvalidOperationException("worker failure");
                            }
                        }
                    )
            );

        Assert.Equal("worker failure", exception.Message);
        Assert.True(bodyCalls < 20, $"The loop drained the source after a failure: {bodyCalls} calls.");
    }

    [Fact]
    public async Task Fallback_ExternalCancellationStopsTheLoop()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                AsyncParallel.ForEachWithSemaphoreAsync(
                    Enumerable.Range(0, 20),
                    2,
                    cancellation.Token,
                    (_, _) => Task.CompletedTask
                )
        );
    }
}
