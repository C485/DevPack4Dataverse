using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Xunit;
using DevPack4Dataverse.Models;

namespace DevPack4Dataverse.Tests;

public class RequestSettingsTests
{
    [Fact]
    public void EmptySettings_AddNothing()
    {
        var request = new UpdateRequest { Target = new Entity("account") { Id = Guid.NewGuid() } };

        new RequestSettings().AddToOrganizationRequest(request, NullLogger.Instance);

        Assert.False(request.Parameters.Contains("tag"));
        Assert.False(request.Parameters.Contains("PartitionId"));
        Assert.False(request.Parameters.Contains("SuppressDuplicateDetection"));
        Assert.False(request.Parameters.Contains("BypassCustomPluginExecution"));
        Assert.False(request.Parameters.Contains("ConcurrencyBehavior"));
    }

    [Fact]
    public void AddsAllKnownHeaders_WhenConfigured()
    {
        var request = new UpdateRequest { Target = new Entity("account") { Id = Guid.NewGuid() } };
        var settings = new RequestSettings
        {
            SharedVariable = "my-tag",
            PartitionId = "partition-1",
            SuppressDuplicateDetection = true,
            SkipPluginExecution = true,
            ConcurrencyBehavior = ConcurrencyBehavior.AlwaysOverwrite,
        };

        settings.AddToOrganizationRequest(request, NullLogger.Instance);

        Assert.Equal("my-tag", request["tag"]);
        Assert.Equal("partition-1", request["PartitionId"]);
        Assert.Equal(true, request["SuppressDuplicateDetection"]);
        Assert.Equal(true, request["BypassCustomPluginExecution"]);
        Assert.Equal(ConcurrencyBehavior.AlwaysOverwrite, request["ConcurrencyBehavior"]);
    }

    [Fact]
    public void DoesNotAddHeaders_WhenDefaultsAreUsed()
    {
        var request = new UpdateRequest { Target = new Entity("account") { Id = Guid.NewGuid() } };
        var settings = new RequestSettings
        {
            SharedVariable = "",
            PartitionId = "",
            SuppressDuplicateDetection = false,
            SkipPluginExecution = false,
        };

        settings.AddToOrganizationRequest(request, NullLogger.Instance);

        Assert.False(request.Parameters.Contains("tag"));
        Assert.False(request.Parameters.Contains("PartitionId"));
        Assert.False(request.Parameters.Contains("SuppressDuplicateDetection"));
        Assert.False(request.Parameters.Contains("BypassCustomPluginExecution"));
    }
}
