using CommunityToolkit.Diagnostics;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace DevPack4Dataverse.New;

public class RequestBuilderFactory
{
    private readonly ILogger _logger;

    public RequestBuilderFactory(ILogger logger)
    {
        Guard.IsNotNull(logger);
        _logger = logger;
    }

    public RequestBuilder<T> Create<T>(Func<T> creator) where T : OrganizationRequest
    {
        Guard.IsNotNull(creator);

        return new RequestBuilder<T>(creator());
    }

    public RequestBuilder<UpdateRequest> CreateUpdateRequest(Entity record)
    {
        Guard.IsNotNull(record);
        Guard.IsNotNullOrEmpty(record.LogicalName);
        Guard.IsNotDefault(record.Id);
        UpdateRequest updateRequest =
            new()
            {
                Target = record
            };

        return new RequestBuilder<UpdateRequest>(updateRequest);
    }

    public RequestBuilder<UpsertRequest> CreateUpsertRequest(Entity record)
    {
        Guard.IsNotNull(record);
        Guard.IsNotNullOrEmpty(record.LogicalName);
        UpsertRequest upsertRequest =
            new()
            {
                Target = record
            };

        return new RequestBuilder<UpsertRequest>(upsertRequest);
    }

    public RequestBuilder<CreateMultipleRequest> CreateCreateMultipleRequest(IList<Entity> records)
    {
        Guard.IsNotNull(records);
        Guard.IsLessThan(records.Count, 1);
        CreateMultipleRequest upsertRequest =
            new()
            {
                Targets = new EntityCollection(records)
            };

        return new RequestBuilder<CreateMultipleRequest>(upsertRequest);
    }

    public RequestBuilder<UpdateMultipleRequest> CreateUpdateMultipleRequest(IList<Entity> records)
    {
        Guard.IsNotNull(records);
        Guard.IsLessThan(records.Count, 1);
        UpdateMultipleRequest upsertRequest =
            new()
            {
                Targets = new EntityCollection(records)
            };

        return new RequestBuilder<UpdateMultipleRequest>(upsertRequest);
    }

    public RequestBuilder<CreateRequest> CreateCreateRequest(Entity record)
    {
        Guard.IsNotNull(record);
        Guard.IsNotNullOrEmpty(record.LogicalName);
        Guard.IsNotDefault(record.Id);
        CreateRequest upsertRequest =
            new()
            {
                Target = record
            };

        return new RequestBuilder<CreateRequest>(upsertRequest);
    }

    public RequestBuilder<DeleteRequest> CreateDeleteRequest(Entity record)
    {
        Guard.IsNotNull(record);
        Guard.IsNotNullOrEmpty(record.LogicalName);
        Guard.IsNotDefault(record.Id);

        return CreateDeleteRequest(record.ToEntityReference());
    }

    public RequestBuilder<DeleteRequest> CreateDeleteRequest(string logicalName, Guid id)
    {
        Guard.IsNotNullOrEmpty(logicalName);
        Guard.IsNotDefault(id);

        return CreateDeleteRequest(EntityReferenceUtils.CreateEntityReference(id, logicalName, _logger));
    }

    public RequestBuilder<DeleteRequest> CreateDeleteRequest(EntityReference entityReference)
    {
        Guard.IsNotNull(entityReference);
        Guard.IsNotNullOrEmpty(entityReference.LogicalName);
        Guard.IsNotDefault(entityReference.Id);
        DeleteRequest deleteRequest =
            new()
            {
                Target = entityReference
            };

        return new RequestBuilder<DeleteRequest>(deleteRequest);
    }
}
