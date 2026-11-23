using DevPack4Dataverse.Models;
using Xunit;

namespace DevPack4Dataverse.Tests;

public class StatisticsTests
{
    [Fact]
    public async Task UsageWeight_CountsRecentCompletedRequests()
    {
        var statistics = new Statistics();
        Statistic statistic = statistics.StartNew();
        await Task.Delay(30);
        statistics.Finish(statistic);

        Assert.True(statistics.UsageWeightFromLastMinutes(2) > 0);
        Assert.Equal(0ul, statistics.UsageWeightFromLastMinutes(0));
    }

    [Fact]
    public void Failure_GetsAPenaltyWeight()
    {
        var statistics = new Statistics();
        Statistic statistic = statistics.StartNew();

        statistics.Finish(statistic, succeeded: false);

        Assert.True(statistics.UsageWeightFromLastMinutes(2) >= 100_000);
    }
}
