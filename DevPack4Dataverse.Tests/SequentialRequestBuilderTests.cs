using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Xunit;
using DevPack4Dataverse.Sequential;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

/// <summary>
/// Black-box tests for sequential request execution and result access.
/// </summary>
public class SequentialRequestBuilderTests
{
    private static (SdkProxy Proxy, SequentialRequestBuilder Builder) Create(
        Func<OrganizationRequest, OrganizationResponse> execute
    )
    {
        var connection = new FakeConnection { ExecuteHandler = execute };
        var proxy = new SdkProxy(NullLogger.Instance, false, new FakeConnectionCreator(connection));
        return (proxy, new SequentialRequestBuilder(proxy, NullLogger.Instance));
    }

    private static CreateResponse Created(Guid id)
    {
        var response = new CreateResponse();
        response.Results["id"] = id;
        return response;
    }

    private static RetrieveResponse Retrieved(Entity entity)
    {
        var response = new RetrieveResponse();
        response.Results["Entity"] = entity;
        return response;
    }

    private static OrganizationResponse ActionResponse(string requestName) =>
        new() { ResponseName = requestName };

    [Fact]
    public async Task ExecuteAsync_CreateActionRetrieve_ExposesAllPreviousResults()
    {
        var createdId = Guid.NewGuid();
        var retrieved = new Entity("account") { Id = createdId, ["name"] = "Contoso" };
        var calls = new List<string>();
        var actionTargets = new List<Guid>();
        var actionParameters = new List<Guid>();
        var retrieveTargets = new List<Guid>();
        var (proxy, builder) = Create(request =>
        {
            calls.Add(request.RequestName);
            switch (request)
            {
                case CreateRequest:
                    return Created(createdId);
                case OrganizationRequest action when action.RequestName == "new_Action":
                    actionTargets.Add(((EntityReference)action["Target"]).Id);
                    actionParameters.Add((Guid)action["previousId"]);
                    return ActionResponse("new_Action");
                case RetrieveRequest retrieve:
                    retrieveTargets.Add(((EntityReference)retrieve.Target).Id);
                    return Retrieved(retrieved);
                default:
                    throw new InvalidOperationException($"Unexpected request: {request.RequestName}");
            }
        });

        using (proxy)
        {
            SequentialRequestContext results = await builder
                .Create(new Entity("account") { ["name"] = "Contoso" })
                .ExecuteAction(
                    "new_Action",
                    context => new EntityReference("account", context.CreatedId(0)),
                    (context, request) => request["previousId"] = context.CreatedId(0)
                )
                .Retrieve("account", context => context.CreatedId(0), new ColumnSet("name"))
                .ExecuteAsync();

            Assert.Equal(3, builder.Count);
            Assert.Equal(3, results.Count);
            Assert.Equal(createdId, results.CreatedId(0));
            Assert.Equal("new_Action", results.Response<OrganizationResponse>(1).ResponseName);
            Assert.Equal(createdId, results.RetrievedEntity(2).Id);
            Assert.Equal("Contoso", results.RetrievedEntity(2).GetAttributeValue<string>("name"));
            Assert.Equal(2, results.Last.Index);
            Assert.Same(results[2], results.Last);

            Assert.Equal(new[] { "Create", "new_Action", "Retrieve" }, calls);
            Assert.Equal(createdId, Assert.Single(actionTargets));
            Assert.Equal(createdId, Assert.Single(actionParameters));
            Assert.Equal(createdId, Assert.Single(retrieveTargets));

            Assert.Throws<InvalidOperationException>(() => results.CreatedId(1));
            Assert.Throws<InvalidOperationException>(() => results.RetrievedEntity(0));
            Assert.Throws<InvalidOperationException>(() => results.Response<CreateResponse>(1));
        }
    }

    [Fact]
    public void Execute_CreateThenUpdate_UsesPreviousCreatedId()
    {
        var createdId = Guid.NewGuid();
        var updateTarget = Guid.Empty;
        var updatedAttribute = false;
        var (proxy, builder) = Create(request =>
        {
            switch (request)
            {
                case CreateRequest:
                    return Created(createdId);
                case UpdateRequest update:
                    Entity record = (Entity)update.Target;
                    updateTarget = record.Id;
                    updatedAttribute = record.GetAttributeValue<bool>("xxx");
                    return new UpdateResponse();
                default:
                    throw new InvalidOperationException($"Unexpected request: {request.RequestName}");
            }
        });

        using (proxy)
        {
            SequentialRequestContext results = builder
                .Create(new Entity("account") { ["name"] = "Contoso" })
                .Update(
                    context =>
                        new Entity("account")
                        {
                            Id = context.CreatedId(0),
                            ["xxx"] = true,
                        }
                )
                .Execute();

            Assert.Equal(2, results.Count);
            Assert.Equal(createdId, results.CreatedId(0));
            Assert.Equal(createdId, updateTarget);
            Assert.True(updatedAttribute);
            Assert.IsType<UpdateResponse>(results.Response<UpdateResponse>(1));
        }
    }

    [Fact]
    public async Task ExecuteAsync_DynamicCustomRequest_CanUsePreviousResults()
    {
        var createdId = Guid.NewGuid();
        Guid customRequestId = Guid.Empty;
        var (proxy, builder) = Create(request =>
        {
            if (request is CreateRequest)
            {
                return Created(createdId);
            }

            if (request.RequestName == "custom")
            {
                customRequestId = (Guid)request["id"];
                return ActionResponse("custom");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestName}");
        });

        using (proxy)
        {
            await builder
                .Create(new Entity("account") { ["name"] = "Contoso" })
                .Execute(
                    context =>
                        new OrganizationRequest("custom") { ["id"] = context.CreatedId(0) }
                )
                .ExecuteAsync();

            Assert.Equal(createdId, customRequestId);
        }
    }

    [Fact]
    public void Execute_EmptyBuilder_Throws()
    {
        var (proxy, builder) = Create(_ => new OrganizationResponse());

        using (proxy)
        {
            Assert.Throws<InvalidOperationException>(() => builder.Execute());
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithCanceledToken_DoesNotRunSteps()
    {
        var calls = 0;
        var (proxy, builder) = Create(_ =>
        {
            calls++;
            return new OrganizationResponse();
        });

        using (proxy)
        using (var cancellationTokenSource = new CancellationTokenSource())
        {
            cancellationTokenSource.Cancel();
            builder.Create(new Entity("account"));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => builder.ExecuteAsync(cancellationTokenSource.Token)
            );

            Assert.Equal(0, calls);
        }
    }

    [Fact]
    public async Task ExecuteAsync_StopsAtFirstFailingStep()
    {
        var calls = 0;
        var (proxy, builder) = Create(request =>
        {
            calls++;
            if (request is CreateRequest)
            {
                return Created(Guid.NewGuid());
            }

            throw new InvalidOperationException("Step failed.");
        });

        using (proxy)
        {
            builder
                .Create(new Entity("account"))
                .Execute(new OrganizationRequest("fails"))
                .Execute(new OrganizationRequest("must-not-run"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => builder.ExecuteAsync());

            Assert.Equal(2, calls);
        }
    }

    [Fact]
    public async Task DataverseDevPack_CreateSequentialRequestBuilder_IsUsable()
    {
        var id = Guid.NewGuid();
        var connection = new FakeConnection { ExecuteHandler = _ => Created(id) };
        var devPack = new DataverseDevPack(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(connection)
        );

        try
        {
            var builder = devPack.CreateSequentialRequestBuilder();
            SequentialRequestContext results = await builder
                .Create(new Entity("account"))
                .ExecuteAsync();

            Assert.Equal(id, results.CreatedId(0));
        }
        finally
        {
            devPack.SdkProxy.Dispose();
        }
    }
}
