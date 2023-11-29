using System.Net;
using Ardalis.GuardClauses;
using DevPack4Dataverse.ExecuteMultiple;
using DevPack4Dataverse.ExpressionBuilder;
using DevPack4Dataverse.Interfaces;
using DevPack4Dataverse.Models;
using DevPack4Dataverse.Utils;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse.Extension
{
    public static class IOrganizationServiceExtension
    {

        public static ILinqExpressionBuilder<T> CreateLinqExpressionBuilder<T>(this IOrganizationService _) where T : Entity, new() => LinqExpressionBuilder.Create<T>();

        public static Guid? ExtCreateRecord(
                    this IOrganizationService organizationService,
            Entity record,
            RequestSettings? requestSettings = null
        )
        {
            Guard.Against.NullOrInvalidInput(
                record,
                nameof(record),
                p => p.Id == Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
            );

            CreateRequest createRequest = new() { Target = record };

            return Guard.Against
                .Null(organizationService)
                .ExtExecute<CreateResponse>(createRequest, requestSettings)
                ?.id;
        }

        public static void ExtDeleteRecord(
            this IOrganizationService organizationService,
            string logicalName,
            Guid id,
            RequestSettings? requestSettings = null
        )
        {
            DeleteRequest deleteRequest =
                new() { Target = EntityReferenceUtils.CreateEntityReference(id, logicalName) };

            _ = Guard.Against.Null(organizationService).ExtExecute<DeleteResponse>(deleteRequest, requestSettings);
        }

        public static void ExtDeleteRecord(
            this IOrganizationService organizationService,
            EntityReference entityReference,
            RequestSettings? requestSettings = null
        )
        {
            Guard.Against.Null(entityReference);

            Guard.Against
                .Null(organizationService)
                .ExtDeleteRecord(entityReference.LogicalName, entityReference.Id, requestSettings);
        }
        public static T? ExtDrillRetrieve<T>(this IOrganizationService organizationService, EntityReference obj, string path, string delimiter = ".")
        {
            string[] pathParts = path.Split(delimiter);
            return organizationService.ExtDrillRetrieve<T>(obj, pathParts);
        }

        public static T? ExtDrillRetrieve<T>(this IOrganizationService organizationService, EntityReference obj, params string[] pathParts)
        {
            Guard.Against.InvalidInput(
                pathParts,
                nameof(pathParts),
                p => Array.TrueForAll(p, u => !string.IsNullOrEmpty(u)),
                "One of path elements is null or empty."
            );
            EntityReference drillReference = Guard.Against.Null(
                obj,
                message: "Drilling object cannot start with reference that is null."
            );
            for (int i = 0; i < pathParts.Length; i++)
            {
                bool isLast = i == pathParts.Length - 1;
                string currentFieldName = pathParts[i];
                Entity ret = organizationService.Retrieve(
                    drillReference.LogicalName,
                    drillReference.Id,
                    new ColumnSet(currentFieldName)
                );
                Guard.Against.InvalidInput(
                    ret,
                    nameof(ret),
                    p => p.Contains(currentFieldName),
                    "Retrieved record doesn't contain field in attributes collection."
                );
                object retrievedField = ret[currentFieldName];
                if (isLast)
                {
                    if (retrievedField is T || retrievedField is null)
                    {
                        return (T?)retrievedField;
                    }
                    throw new InvalidProgramException(
                        $"Retrieved field is not same type as expected one, retrieved type is {retrievedField.GetType().Name}, expected type is {typeof(T).Name}"
                    );
                }
                if (retrievedField is EntityReference retivedFieldEntityReference)
                {
                    drillReference = retivedFieldEntityReference;
                }
                else if (retrievedField is null)
                {
                    throw new InvalidProgramException(
                        $"Retrieved field is null but it's not last element of path, current field name {currentFieldName}"
                    );
                }
                else
                {
                    throw new InvalidProgramException(
                        $"Retrieved field is not {nameof(EntityReference)}, current field name {currentFieldName}, type of retrieved field {retrievedField.GetType().Name}"
                    );
                }
            }
            throw new InvalidProgramException("Unexpected state, probably a bug.");
        }

        public static T? ExtExecute<T>(
            this IOrganizationService organizationService,
            OrganizationRequest request,
            RequestSettings? requestSettings = null
        )
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
                return Guard.Against.Null(organizationService).Execute(request) as T;
            }

            requestSettings?.AddToOrganizationRequest(request);
            return Guard.Against.Null(organizationService).Execute(request) as T;
        }

        public static ExecuteMultipleResponse? ExtExecute(
            this IOrganizationService organizationService,
            ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
            RequestSettings? requestSettings = null
        )
        {
            Guard.Against.Null(executeMultipleRequestBuilder);

            return Guard.Against
                .Null(organizationService)
                .ExtExecute<ExecuteMultipleResponse>(executeMultipleRequestBuilder.RequestWithResults, requestSettings);
        }

        public static Entity ExtRefreshRecord(
            this IOrganizationService organizationService,
            Entity record,
            RequestSettings? requestSettings = null
        )
        {
            Guard.Against.NullOrInvalidInput(
                record,
                nameof(record),
                p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
            );

            ColumnSet columns = new([.. record.Attributes.Keys]);

            return Guard.Against
                .Null(organizationService)
                .ExtRetrieve(record.LogicalName, record.Id, columns, requestSettings);
        }

        public static T? ExtRefreshRecord<T>(
            this IOrganizationService organizationService,
            T record,
            RequestSettings? requestSettings = null
        )
            where T : Entity
        {
            Guard.Against.NullOrInvalidInput(
                record,
                nameof(record),
                p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
            );

            ColumnSet columns = new([.. record.Attributes.Keys]);

            return Guard.Against
                .Null(organizationService)
                .ExtRetrieve<T>(record.LogicalName, record.Id, columns, requestSettings);
        }

        public static Entity ExtRetrieve(
            this IOrganizationService organizationService,
            string entityName,
            Guid id,
            ColumnSet columnSet,
            RequestSettings? requestSettings = null
        )
        {
            Guard.Against.Null(columnSet);

            RetrieveResponse? retrieveResponse = Guard.Against
                .Null(organizationService)
                .ExtExecute<RetrieveResponse>(
                    new RetrieveRequest
                    {
                        ColumnSet = columnSet,
                        Target = EntityReferenceUtils.CreateEntityReference(id, entityName)
                    },
                    requestSettings
                );
            return Guard.Against.Null(retrieveResponse).Entity;
        }

        public static T? ExtRetrieve<T>(
            this IOrganizationService organizationService,
            string entityName,
            Guid id,
            ColumnSet columnSet,
            RequestSettings? requestSettings = null
        )
            where T : Entity
        {
            return Guard.Against
                .Null(organizationService)
                .ExtRetrieve(entityName, id, columnSet, requestSettings)
                ?.ToEntity<T>();
        }

        public static Entity[] ExtRetrieveMultiple(
            this IOrganizationService organizationService,
            QueryExpression queryExpression,
            RequestSettings? requestSettings = null
        )
        {
            Guard.Against.Null(queryExpression);

            return InnerRetrieveMultiple().ToArray();

            IEnumerable<Entity> InnerRetrieveMultiple()
            {
                queryExpression.PageInfo = new PagingInfo
                {
                    Count = 5000,
                    PageNumber = 1,
                    PagingCookie = null
                };

                while (true)
                {
                    RetrieveMultipleResponse? retrieveMultipleResponse = Guard.Against
                        .Null(organizationService)
                        .ExtExecute<RetrieveMultipleResponse>(
                            new RetrieveMultipleRequest { Query = queryExpression },
                            requestSettings
                        );
                    EntityCollection retrieveMultipleResult = Guard.Against
                        .Null(retrieveMultipleResponse)
                        .EntityCollection;

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

        public static bool ExtTest(this IOrganizationService organizationService)
        {
            try
            {
                WhoAmIResponse response = (WhoAmIResponse)organizationService.Execute(new WhoAmIRequest());

                return response != null && response.UserId != Guid.Empty;
            }
            catch (Exception)
            {

                return false;
            }


        }

        public static void ExtUpdateRecord(
            this IOrganizationService organizationService,
            Entity record,
            RequestSettings? requestSettings = null
        )
        {
            Guard.Against.NullOrInvalidInput(
                record,
                nameof(record),
                p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
            );

            UpdateRequest request = new() { Target = record };

            _ = Guard.Against.Null(organizationService).ExtExecute<UpdateResponse>(request, requestSettings);
        }

        public static EntityReference ExtUpsertRecord(
            this IOrganizationService organizationService,
            Entity record,
            RequestSettings? requestSettings = null
        )
        {
            Guard.Against.NullOrInvalidInput(record, nameof(record), p => string.IsNullOrEmpty(p.LogicalName));

            UpsertRequest request = new() { Target = record };

            UpsertResponse? executeResponse = Guard.Against
                .Null(organizationService)
                .ExtExecute<UpsertResponse>(request, requestSettings);

            return Guard.Against.Null(executeResponse).Target;
        }

        public static OrganizationServiceContext GetOrganizationServiceContext(this IOrganizationService serviceClient) => new(serviceClient);

        public static T? GetOrganizationServiceContext<T>(this IOrganizationService serviceClient) where T : OrganizationServiceContext
        {
            return (T?)Activator.CreateInstance(typeof(T), serviceClient);
        }
        public static void OptimizeConnections(this IOrganizationService _)
        {
            ServicePointManager.DefaultConnectionLimit = 65000;
            ThreadPool.SetMinThreads(100, 100);
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;
        }
        public static T[] RetrieveMultiple<T>(
            this IOrganizationService organizationService,
            QueryExpression queryExpression,
            RequestSettings? requestSettings = null
        )
            where T : Entity
        {
            Guard.Against.Null(queryExpression);

            return InnerRetrieveMultiple().ToArray();

            IEnumerable<T> InnerRetrieveMultiple()
            {
                queryExpression.PageInfo = new PagingInfo
                {
                    Count = 5000,
                    PageNumber = 1,
                    PagingCookie = null
                };

                while (true)
                {
                    RetrieveMultipleResponse? retrieveMultipleResponse = Guard.Against
                        .Null(organizationService)
                        .ExtExecute<RetrieveMultipleResponse>(
                            new RetrieveMultipleRequest { Query = queryExpression },
                            requestSettings
                        );
                    EntityCollection retrieveMultipleResult = Guard.Against
                        .Null(retrieveMultipleResponse)
                        .EntityCollection;

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
    }
}
