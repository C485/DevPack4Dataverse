using Xunit;
using DevPack4Dataverse.Models;

namespace DevPack4Dataverse.Tests;

public class AdaptiveRequesterSettingsTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        var settings = new AdaptiveRequesterSettings();

        Assert.Same(settings, settings.Validate());
    }

    [Fact]
    public void InitialBatchSizeAboveMaximum_Throws()
    {
        var settings = new AdaptiveRequesterSettings
        {
            MinBatchSize = 1,
            MaxBatchSize = 5,
            InitialBatchSize = 6
        };

        Assert.ThrowsAny<ArgumentException>(() => settings.Validate());
    }

    [Fact]
    public void InitialParallelismAboveMaximum_Throws()
    {
        var settings = new AdaptiveRequesterSettings
        {
            MinDegreeOfParallelism = 1,
            MaxDegreeOfParallelism = 4,
            InitialDegreeOfParallelism = 5
        };

        Assert.ThrowsAny<ArgumentException>(() => settings.Validate());
    }
}
