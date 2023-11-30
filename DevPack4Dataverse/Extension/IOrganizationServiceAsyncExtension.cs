using DevPack4Dataverse.Models;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using Microsoft.PowerPlatform.Dataverse.Client;
using Ardalis.GuardClauses;
using DevPack4Dataverse.ExecuteMultiple;
using DevPack4Dataverse.Utils;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Messages;
using System.Data.Common;
using DevPack4Dataverse.New;

namespace DevPack4Dataverse.Extension
{
    public static   class IOrganizationServiceAsyncExtension
    {
        public static async Task DeleteRecordAsync(this IOrganizationServiceAsync organizationService, EntityReference entityReference, RequestSettings? requestSettings = null)
        {
            await Guard.Against.Null(organizationService).ExtDeleteRecordAsync(entityReference.LogicalName, entityReference.Id, requestSettings);
        }

        public static async Task<Entity> ExtCreateRecordAndGetAsync(this IOrganizationServiceAsync organizationService, Entity record, RequestSettings? requestSettings = null)
        {
            Guid createdRecordId = await Guard.Against.Null(organizationService).ExtCreateRecordAsync(record, requestSettings);

            ColumnSet columns = new([.. record.Attributes.Keys]);

            return await Guard.Against.Null(organizationService).ExtRetrieveAsync(record.LogicalName, createdRecordId, columns);
        }

        public static async Task<Guid> ExtCreateRecordAsync(this IOrganizationServiceAsync organizationService, Entity record, RequestSettings? requestSettings = null)
        {
            Guard.Against.NullOrInvalidInput(
                record,
                nameof(record),
                p => p.Id == Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
            );

            Guard.Against.Null(requestSettings);

            CreateRequest createRequest = new() { Target = record };

            CreateResponse? createResponse = await Guard.Against.Null(organizationService).ExtExecuteAsync<CreateResponse>(createRequest, requestSettings);
            return Guard.Against.Null(createResponse).id;
        }

        public static async Task ExtDeleteRecordAsync(this IOrganizationServiceAsync organizationService, string logicalName, Guid id, RequestSettings? requestSettings = null)
        {
            DeleteRequest deleteRequest =
                new() { Target = EntityReferenceUtils.CreateEntityReference(id, logicalName) };

            _ = await Guard.Against.Null(organizationService).ExtExecuteAsync<DeleteResponse>(deleteRequest, requestSettings);
        }

        public static async Task<T?> ExtDrillRetrieveAsync<T>(this IOrganizationServiceAsync organizationService, EntityReference obj, string path, bool noThrowWhenNull = false, string delimiter = ".")
        {
            string[] pathParts = path.Split(delimiter);
            return await Guard.Against.Null(organizationService).ExtDrillRetrieveAsync<T>(obj,noThrowWhenNull, pathParts);
        }

        public static async Task<T?> ExtDrillRetrieveAsync<T>(this IOrganizationServiceAsync organizationService, EntityReference obj, bool noThrowWhenNull = false, params string[] pathParts)
        {
            Guard.Against.InvalidInput(
                pathParts,
                nameof(pathParts),
                p => Array.TrueForAll(p, u => !string.IsNullOrEmpty(u)),
                "One of path elements is null or empty."
            );
            EntityReference drillReference = Guard.Against.NullOrInvalidInput(
                obj,
                nameof(obj),
                p => !string.IsNullOrEmpty(p.LogicalName) && p.Id != Guid.Empty,
                message: "Drilling object cannot start with reference that is null or empty."
            ); 


            if (Array.Exists(pathParts, string.IsNullOrEmpty))
            {
                throw new InvalidProgramException("One of path elements is null or empty.");
            }
            for (int i = 0; i < pathParts.Length; i++)
            {
                bool isLast = i == pathParts.Length - 1;
                string currentFieldName = pathParts[i];
                Entity ret = await organizationService.RetrieveAsync(drillReference.LogicalName,
                    drillReference.Id,
                    new ColumnSet(currentFieldName));

                if (!ret.Contains(currentFieldName))
                {
                    throw new InvalidProgramException("Retrieved record doesn't contain field in attributes collection.");
                }

                object retrievedField = ret[currentFieldName];

                if (isLast)
                {
                    return retrievedField switch
                    {
                        null => default,
                        T finalValue => finalValue,
                        _ => throw new InvalidProgramException(
                            $"Retrieved field is not same type as expected one, retrieved type is {retrievedField.GetType().Name}, expected type is {typeof(T).Name}")
                    };
                }

                if (noThrowWhenNull && retrievedField is null)
                {
                    return default;
                }

                drillReference = retrievedField switch
                {
                    EntityReference retrievedFieldEntityReference => retrievedFieldEntityReference,
                    null => throw new InvalidProgramException(
                        $"Retrieved field is null but it's not last element of path, current field name {currentFieldName}"),
                    _ => throw new InvalidProgramException(
                        $"Retrieved field is not {nameof(EntityReference)}, current field name {currentFieldName}, type of retrieved field {retrievedField.GetType().Name}")
                };
            }

            throw new InvalidProgramException("Unexpected state, probably a bug.");
        }
        public static async Task<T?> ExtExecuteAsync<T>(this IOrganizationServiceAsync organizationService, OrganizationRequest request, RequestSettings? requestSettings = null)
            where T : OrganizationResponse
        {

            Guard.Against.Null(request);
            if (
                requestSettings?.ImpersonateAsUserByAADId is not null
                || requestSettings?.ImpersonateAsUserByDataverseId is not null
            )
            {
                using ReplaceAndRestoreCallerId _ = new(Guard.Against.Null(organizationService), requestSettings);
                requestSettings.AddToOrganizationRequest(request);
                return await Guard.Against.Null(organizationService).ExecuteAsync(request) as T;
            }

            requestSettings?.AddToOrganizationRequest(request);
            return await Guard.Against.Null(organizationService).ExecuteAsync(request) as T;
        }
        public static async Task<Response?> ExtExecuteAsync<Request, Response>(
    this IOrganizationServiceAsync organizationService,
    RequestBuilder<Request> executeMultipleRequestBuilder,
    RequestSettings? requestSettings = null
) where Request : OrganizationRequest where Response : OrganizationResponse
        {
            Guard.Against.Null(executeMultipleRequestBuilder);

            return await Guard.Against.Null(organizationService).ExtExecuteAsync<Response>(executeMultipleRequestBuilder.Build(), requestSettings);
        }
        public static async Task<ExecuteMultipleResponse?> ExtExecuteAsync(this IOrganizationServiceAsync organizationService,
            ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
            RequestSettings? requestSettings = null
        )
        {
            Guard.Against.Null(executeMultipleRequestBuilder);

            return await Guard.Against.Null(organizationService).ExtExecuteAsync<ExecuteMultipleResponse>(
                executeMultipleRequestBuilder.Build(),
                requestSettings
            );
        }

        public static async Task<T?> ExtRefreshRecordAsync<T>(this IOrganizationServiceAsync organizationService, T record, RequestSettings? requestSettings = null)
            where T : Entity
        {
            Guard.Against.NullOrInvalidInput(
                record,
                nameof(record),
                p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
            );

            ColumnSet columns = new([.. record.Attributes.Keys]);

            return await Guard.Against.Null(organizationService).ExtRetrieveAsync<T>(record.LogicalName, record.Id, columns, requestSettings);
        }

        public static async Task<Entity> ExtRefreshRecordAsync(this IOrganizationServiceAsync organizationService, Entity record, RequestSettings? requestSettings = null)
        {
            Guard.Against.NullOrInvalidInput(
                record,
                nameof(record),
                p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
            );

            ColumnSet columns = new([.. record.Attributes.Keys]);

            return await Guard.Against.Null(organizationService).ExtRetrieveAsync(record.LogicalName, record.Id, columns, requestSettings);
        }

        public static async Task<Entity> ExtRetrieveAsync(this IOrganizationServiceAsync organizationService,
            string entityName,
            Guid id,
            ColumnSet columnSet,
            RequestSettings? requestSettings = null
        )
        {
            Guard.Against.Null(columnSet);

            RetrieveResponse? retrieveResponse = await Guard.Against.Null(organizationService).ExtExecuteAsync<RetrieveResponse>(
                new RetrieveRequest
                {
                    ColumnSet = columnSet,
                    Target = EntityReferenceUtils.CreateEntityReference(id, entityName)
                },
                requestSettings
            );
            return Guard.Against.Null(retrieveResponse).Entity;
        }

        public static async Task<T?> ExtRetrieveAsync<T>(this IOrganizationServiceAsync organizationService,
            string entityName,
            Guid id,
            ColumnSet columnSet,
            RequestSettings? requestSettings = null
        )
            where T : Entity
        {
            return await Guard.Against.Null(organizationService).ExtRetrieveAsync(entityName, id, columnSet, requestSettings)
                .ContinueWith(p => p.Result?.ToEntity<T>());
        }

        public static async Task<Entity[]> ExtRetrieveMultipleAsync(this IOrganizationServiceAsync organizationService,
            QueryExpression queryExpression,
            RequestSettings? requestSettings = null
        )
        {
            Guard.Against.Null(queryExpression);

            return await InnerRetrieveMultiple().ToArrayAsync();

            async IAsyncEnumerable<Entity> InnerRetrieveMultiple()
            {
                queryExpression.PageInfo = new PagingInfo
                {
                    Count = 5000,
                    PageNumber = 1,
                    PagingCookie = null
                };

                while (true)
                {
                    RetrieveMultipleResponse? retrieveMultipleResponse = await Guard.Against.Null(organizationService).ExtExecuteAsync<RetrieveMultipleResponse>(
                        new RetrieveMultipleRequest { Query = queryExpression },
                        requestSettings
                    );
                    EntityCollection retrieveMultipleResult = Guard.Against.Null(retrieveMultipleResponse).EntityCollection;

                    foreach (Entity record in retrieveMultipleResult.Entities)
                    {
                        yield return record;
                    }

                    if (!retrieveMultipleResult.MoreRecords)
                    {
                        break;
                    }

                    queryExpression.PageInfo.PageNumber++;
                    queryExpression.PageInfo.PagingCookie = retrieveMultipleResult.PagingCookie;
                }
            }
        }

        public static async Task<T[]> ExtRetrieveMultipleAsync<T>(this IOrganizationServiceAsync organizationService,
            QueryExpression queryExpression,
            RequestSettings? requestSettings = null
        )
            where T : Entity
        {
            Guard.Against.Null(queryExpression);

            return await InnerRetrieveMultiple().ToArrayAsync();

            async IAsyncEnumerable<T> InnerRetrieveMultiple()
            {
                queryExpression.PageInfo = new PagingInfo
                {
                    Count = 5000,
                    PageNumber = 1,
                    PagingCookie = null
                };

                while (true)
                {
                    RetrieveMultipleResponse? retrieveMultipleResponse = await Guard.Against.Null(organizationService).ExtExecuteAsync<RetrieveMultipleResponse>(
                        new RetrieveMultipleRequest { Query = queryExpression },
                        requestSettings
                    );
                    EntityCollection retrieveMultipleResult = Guard.Against.Null(retrieveMultipleResponse).EntityCollection;

                    foreach (T record in retrieveMultipleResult.Entities.Select(p => p.ToEntity<T>()))
                    {
                        yield return record;
                    }

                    if (!retrieveMultipleResult.MoreRecords)
                    {
                        break;
                    }

                    queryExpression.PageInfo.PageNumber++;
                    queryExpression.PageInfo.PagingCookie = retrieveMultipleResult.PagingCookie;
                }
            }
        }

        public static async Task<bool> ExtTestAsync(this IOrganizationServiceAsync organizationService)
        {
            try
            {

                WhoAmIResponse response = (WhoAmIResponse)await Guard.Against.Null(organizationService).ExecuteAsync(new WhoAmIRequest());

                return response != null && response.UserId != Guid.Empty;
            }
            catch (Exception)
            {

                return false;
            }
        }

        public static async Task UpdateRecordAsync(this IOrganizationServiceAsync organizationService, Entity record, RequestSettings? requestSettings = null)
        {
            Guard.Against.NullOrInvalidInput(
                record,
                nameof(record),
                p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
            );

            UpdateRequest request = new() { Target = record };

            _ = await Guard.Against.Null(organizationService).ExtExecuteAsync<UpdateResponse>(request, requestSettings);
        }

        public static async Task<EntityReference> UpsertRecordAsync(this IOrganizationServiceAsync organizationService, Entity record, RequestSettings? requestSettings = null)
        {
            Guard.Against.NullOrInvalidInput(record, nameof(record), p => string.IsNullOrEmpty(p.LogicalName));

            UpsertRequest request = new() { Target = record };

            UpsertResponse? executeResponse = await Guard.Against.Null(organizationService).ExtExecuteAsync<UpsertResponse>(request, requestSettings);

            return Guard.Against.Null(executeResponse).Target;
        }
    }
}
