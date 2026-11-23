using DevPack4Dataverse.Creators;
using Xunit;

namespace DevPack4Dataverse.Tests;

public class ConnectionCreationRetryTests
{
    [Fact]
    public void RetriesBeforeGivingUp()
    {
        var retry = new ConnectionCreationRetry(maxAttempts: 3, baseDelay: TimeSpan.Zero);

        retry.RecordFailure();
        Assert.False(retry.FailedPermanently);
        Assert.True(retry.CanCreateNow);
        retry.RecordFailure();
        Assert.False(retry.FailedPermanently);
        retry.RecordFailure();

        Assert.True(retry.FailedPermanently);
    }

    [Fact]
    public void BackoffBlocksUntilTheDelayPasses()
    {
        var retry = new ConnectionCreationRetry(
            maxAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(100)
        );

        retry.RecordFailure();
        Assert.False(retry.CanCreateNow);

        Thread.Sleep(150);
        Assert.True(retry.CanCreateNow);
    }

    [Fact]
    public void SuccessResetsTheAttemptCount()
    {
        var retry = new ConnectionCreationRetry(maxAttempts: 2, baseDelay: TimeSpan.Zero);

        retry.RecordFailure();
        retry.RecordSuccess();
        retry.RecordFailure();

        Assert.False(retry.FailedPermanently);
    }
}
