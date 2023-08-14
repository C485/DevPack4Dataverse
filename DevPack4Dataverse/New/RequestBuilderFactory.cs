using CommunityToolkit.Diagnostics;
using DevPack4Dataverse.Utils;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace DevPack4Dataverse.New;

public static class RequestBuilderFactory
{
    public static RequestBuilder<T> Create<T>(Func<T> creator) where T : OrganizationRequest
    {
        Guard.IsNotNull(creator);

        return new RequestBuilder<T>(creator());
    }

    public static RequestBuilder<UpdateRequest> CreateUpdateRequest(Entity record)
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

    public static RequestBuilder<UpsertRequest> CreateUpsertRequest(Entity record)
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

    public static RequestBuilder<CreateMultipleRequest> CreateCreateMultipleRequest(IList<Entity> records)
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

    public static RequestBuilder<UpdateMultipleRequest> CreateUpdateMultipleRequest(IList<Entity> records)
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

    public static RequestBuilder<CreateRequest> CreateCreateRequest(Entity record)
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

    public static RequestBuilder<DeleteRequest> CreateDeleteRequest(Entity record)
    {
        Guard.IsNotNull(record);
        Guard.IsNotNullOrEmpty(record.LogicalName);
        Guard.IsNotDefault(record.Id);

        return CreateDeleteRequest(record.ToEntityReference());
    }

    public static RequestBuilder<DeleteRequest> CreateDeleteRequest(string logicalName, Guid id)
    {
        Guard.IsNotNullOrEmpty(logicalName);
        Guard.IsNotDefault(id);

        return CreateDeleteRequest(EntityReferenceUtils.CreateEntityReference(id, logicalName));
    }

    public static RequestBuilder<DeleteRequest> CreateDeleteRequest(EntityReference entityReference)
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
