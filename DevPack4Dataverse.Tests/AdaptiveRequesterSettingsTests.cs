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
    public void DegreeOfParallelismZero_Throws()
    {
        var settings = new AdaptiveRequesterSettings { DegreeOfParallelism = 0 };

        Assert.ThrowsAny<ArgumentException>(() => settings.Validate());
    }

    [Fact]
    public void DegreeOfParallelismPositive_IsValid()
    {
        var settings = new AdaptiveRequesterSettings { DegreeOfParallelism = 15 };

        Assert.Same(settings, settings.Validate());
    }
}
