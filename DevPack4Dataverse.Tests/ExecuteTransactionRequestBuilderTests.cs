using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Xunit;
using DevPack4Dataverse.ExecuteTransaction;
using DevPack4Dataverse.Models;

namespace DevPack4Dataverse.Tests;

public class ExecuteTransactionRequestBuilderTests
{
    private static ExecuteTransactionRequestBuilder Create(bool returnResponses = true) =>
        new(NullLogger.Instance, returnResponses);

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
    public void AddUpsert_AllowsRecordWithoutId()
    {
        var builder = Create();

        builder.AddUpsert(new Entity("account"));

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
    public void Request_MirrorsBuilderState()
    {
        var builder = Create(returnResponses: false);
        builder.AddCreate(new Entity("account"));
        builder.AddUpdate(new Entity("account") { Id = Guid.NewGuid() });

        var request = builder.Request;

        Assert.Equal(2, request.Requests.Count);
        Assert.False(request.ReturnResponses);
    }

    [Fact]
    public void Request_ReturnsResponsesByDefault()
    {
        Assert.True(Create().Request.ReturnResponses);
    }

    [Fact]
    public void Request_PreservesInsertionOrder()
    {
        var builder = Create();
        builder.AddRequest(new OrganizationRequest("first"));
        builder.AddRequest(new OrganizationRequest("second"));
        builder.AddRequest(new OrganizationRequest("third"));

        string[] names = builder.Request.Requests.Select(request => request.RequestName).ToArray();

        Assert.Equal(new[] { "first", "second", "third" }, names);
    }

    [Fact]
    public void Request_IsSnapshot()
    {
        var builder = Create();
        builder.AddCreate(new Entity("account"));

        var request = builder.Request;
        builder.AddCreate(new Entity("account"));

        Assert.Single(request.Requests);
        Assert.Equal(2, builder.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddRequest_RejectsBatchRequests(bool useMultiple)
    {
        var builder = Create();
        OrganizationRequest request = useMultiple ? new ExecuteMultipleRequest() : new ExecuteTransactionRequest();

        Assert.Throws<ArgumentException>(() => builder.AddRequest(request));
        Assert.Equal(0, builder.Count);
    }

    [Fact]
    public void AddRequest_NullThrows()
    {
        var builder = Create();

        Assert.NotNull(Record.Exception(() => builder.AddRequest(null!)));
    }

    [Fact]
    public void AddRequest_RejectsPerRequestImpersonation()
    {
        var builder = Create();
        RequestSettings settings = new()
        {
            ImpersonateAsUserByDataverseId = Guid.NewGuid()
        };

        Assert.Throws<ArgumentException>(
            () => builder.AddRequest(new OrganizationRequest("req"), settings)
        );
        Assert.Equal(0, builder.Count);
    }

    [Fact]
    public void AddRequest_ConcurrentAdds_DoNotExceedOneThousand()
    {
        var builder = Create();
        for (int index = 0; index < 999; index++)
        {
            builder.AddRequest(new OrganizationRequest("req" + index));
        }

        int failures = 0;
        Parallel.For(
            0,
            200,
            index =>
            {
                try
                {
                    builder.AddRequest(new OrganizationRequest("extra" + index));
                }
                catch (InvalidOperationException)
                {
                    Interlocked.Increment(ref failures);
                }
            }
        );

        Assert.Equal(1000, builder.Count);
        Assert.True(failures > 0);
    }

    [Fact]
    public void AddRequest_RejectsMoreThanOneThousandRequests()
    {
        var builder = Create();
        for (int index = 0; index < 1000; index++)
        {
            builder.AddRequest(new OrganizationRequest("req" + index));
        }

        Assert.Throws<InvalidOperationException>(
            () => builder.AddRequest(new OrganizationRequest("too-many"))
        );
    }
}
