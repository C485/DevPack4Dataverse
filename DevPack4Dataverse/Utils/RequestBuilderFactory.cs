using Ardalis.GuardClauses;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace DevPack4Dataverse.Utils;

public static class RequestBuilderFactory
{
    public static RequestBuilder<T> Create<T>(Func<T> creator)
        where T : OrganizationRequest
    {
        return new RequestBuilder<T>(Guard.Against.Null(creator)());
    }

    public static RequestBuilder<CreateMultipleRequest> CreateCreateMultipleRequest(IList<Entity> records)
    {
        CreateMultipleRequest upsertRequest =
            new()
            {
                Targets = new EntityCollection(
                    Guard
                        .Against
                        .NullOrInvalidInput(
                            records,
                            nameof(records),
                            p => p.Count > 1 && p.All(u => u is not null && !string.IsNullOrEmpty(u.LogicalName)),
                            "List cannot be null or be empty."
                        )
                )
            };

        return new RequestBuilder<CreateMultipleRequest>(upsertRequest);
    }

    public static RequestBuilder<CreateRequest> CreateCreateRequest(Entity record)
    {
        CreateRequest upsertRequest =
            new()
            {
                Target = Guard
                    .Against
                    .NullOrInvalidInput(
                        record,
                        nameof(record),
                        p => !string.IsNullOrEmpty(p.LogicalName),
                        $"{nameof(Entity)} has to have {nameof(Entity.LogicalName)} and not be null."
                    )
            };

        return new RequestBuilder<CreateRequest>(upsertRequest);
    }

    public static RequestBuilder<DeleteRequest> CreateDeleteRequest(Entity record)
    {
        return CreateDeleteRequest(
            Guard
                .Against
                .NullOrInvalidInput(
                    record,
                    nameof(record),
                    p => !string.IsNullOrEmpty(p.LogicalName) && p.Id != Guid.Empty,
                    $"{nameof(Entity)} has to have {nameof(Entity.Id)}, {nameof(Entity.LogicalName)} and not be null."
                )
                .ToEntityReference()
        );
    }

    public static RequestBuilder<DeleteRequest> CreateDeleteRequest(string logicalName, Guid id)
    {
        return CreateDeleteRequest(
            EntityReferenceUtils.CreateEntityReference(
                Guard.Against.Default(id),
                Guard.Against.NullOrEmpty(logicalName)
            )
        );
    }

    public static RequestBuilder<DeleteRequest> CreateDeleteRequest(EntityReference entityReference)
    {
        DeleteRequest deleteRequest =
            new()
            {
                Target = Guard
                    .Against
                    .NullOrInvalidInput(
                        entityReference,
                        nameof(entityReference),
                        p => !string.IsNullOrEmpty(p.LogicalName) && p.Id != Guid.Empty,
                        $"{nameof(EntityReference)} has to have {nameof(EntityReference.Id)}, {nameof(EntityReference.LogicalName)} and not be null."
                    )
            };

        return new RequestBuilder<DeleteRequest>(deleteRequest);
    }

    public static RequestBuilder<UpdateMultipleRequest> CreateUpdateMultipleRequest(IList<Entity> records)
    {
        UpdateMultipleRequest upsertRequest =
            new()
            {
                Targets = new EntityCollection(
                    Guard
                        .Against
                        .NullOrInvalidInput(
                            records,
                            nameof(records),
                            p =>
                                p.Count > 1
                                && p.All(
                                    u => u is not null && !string.IsNullOrEmpty(u.LogicalName) && u.Id != Guid.Empty
                                ),
                            "List cannot be null or be empty."
                        )
                )
            };

        return new RequestBuilder<UpdateMultipleRequest>(upsertRequest);
    }

    public static RequestBuilder<UpdateRequest> CreateUpdateRequest(Entity record)
    {
        UpdateRequest updateRequest =
            new()
            {
                Target = Guard
                    .Against
                    .NullOrInvalidInput(
                        record,
                        nameof(record),
                        p => !string.IsNullOrEmpty(p.LogicalName) && p.Id != Guid.Empty,
                        $"{nameof(Entity)} has to have {nameof(Entity.Id)}, {nameof(Entity.LogicalName)} and not be null."
                    )
            };

        return new RequestBuilder<UpdateRequest>(updateRequest);
    }

    public static RequestBuilder<UpsertMultipleRequest> CreateUpsertMultipleRequest(IList<Entity> records)
    {
        UpsertMultipleRequest upsertRequest =
            new()
            {
                Targets = new EntityCollection(
                    Guard
                        .Against
                        .NullOrInvalidInput(
                            records,
                            nameof(records),
                            p =>
                                p.Count > 1
                                && p.All(
                                    u => u is not null && !string.IsNullOrEmpty(u.LogicalName) && u.Id != Guid.Empty
                                ),
                            "List cannot be null or be empty."
                        )
                )
            };

        return new RequestBuilder<UpsertMultipleRequest>(upsertRequest);
    }

    public static RequestBuilder<UpsertRequest> CreateUpsertRequest(Entity record)
    {
        UpsertRequest upsertRequest =
            new()
            {
                Target = Guard
                    .Against
                    .NullOrInvalidInput(
                        record,
                        nameof(record),
                        p => !string.IsNullOrEmpty(p.LogicalName),
                        $"{nameof(Entity)} has to have {nameof(Entity.LogicalName)} and not be null."
                    )
            };

        return new RequestBuilder<UpsertRequest>(upsertRequest);
    }
}
