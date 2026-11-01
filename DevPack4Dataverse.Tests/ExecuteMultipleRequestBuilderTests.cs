using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Xunit;
using DevPack4Dataverse.ExecuteMultiple;

namespace DevPack4Dataverse.Tests;

public class ExecuteMultipleRequestBuilderTests
{
    private static ExecuteMultipleRequestBuilder Create(bool continueOnError = true) =>
        new(NullLogger.Instance, continueOnError);

    [Fact]
    public void StartsEmpty()
    {
        var builder = Create();

        Assert.Equal(0, builder.Count);
    }

    [Fact]
    public void AddCreate_IncrementsCount()
    {
        var builder = Create();

        builder.AddCreate(new Entity("account"));

        Assert.Equal(1, builder.Count);
    }

    [Fact]
    public void AddCreate_RejectsRecordWithId()
    {
        var builder = Create();

        var exception = Record.Exception(() =>
            builder.AddCreate(new Entity("account") { Id = Guid.NewGuid() }));

        Assert.NotNull(exception);
    }

    [Fact]
    public void AddCreate_RejectsRecordWithoutLogicalName()
    {
        var builder = Create();

        var exception = Record.Exception(() => builder.AddCreate(new Entity()));

        Assert.NotNull(exception);
    }

    [Fact]
    public void AddUpdate_RequiresId()
    {
        var builder = Create();

        Assert.NotNull(Record.Exception(() => builder.AddUpdate(new Entity("account"))));
        builder.AddUpdate(new Entity("account") { Id = Guid.NewGuid() });
        Assert.Equal(1, builder.Count);
    }

    [Fact]
    public void AddDelete_CountsRequest()
    {
        var builder = Create();

        builder.AddDelete(new EntityReference("account", Guid.NewGuid()));

        Assert.Equal(1, builder.Count);
    }

    [Fact]
    public void RequestWithResults_MirrorsBuilderState()
    {
        var builder = Create(continueOnError: false);
        builder.AddCreate(new Entity("account"));
        builder.AddUpdate(new Entity("account") { Id = Guid.NewGuid() });

        var request = builder.RequestWithResults;

        Assert.Equal(2, request.Requests.Count);
        Assert.False(request.Settings.ContinueOnError);
        Assert.True(request.Settings.ReturnResponses);
    }

    [Fact]
    public void RequestWithResults_PreservesInsertionOrder()
    {
        var builder = Create();
        builder.AddRequest(new OrganizationRequest("first"));
        builder.AddRequest(new OrganizationRequest("second"));
        builder.AddRequest(new OrganizationRequest("third"));

        string[] names = builder
            .RequestWithResults.Requests.Select(request => request.RequestName)
            .ToArray();

        Assert.Equal(new[] { "first", "second", "third" }, names);
    }

    [Fact]
    public void AddRequest_NullThrows()
    {
        var builder = Create();

        Assert.NotNull(Record.Exception(() => builder.AddRequest(null)));
    }
}
