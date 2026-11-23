/*
Copyright 2022 Kamil Skoracki / C485@GitHub

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using Ardalis.GuardClauses;
using DevPack4Dataverse.BulkOperations;
using DevPack4Dataverse.ExecuteMultiple;
using DevPack4Dataverse.Interfaces;
using DevPack4Dataverse.Models;
using DevPack4Dataverse.Utils;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse;

public sealed class Connection : IConnection, IConnectionIdleWaiter
{
    private readonly SemaphoreSlim _semaphoreSlim;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _callGate;
    private readonly Statistics _usageStatistics = new();

    public Connection(ServiceClient connection, ILogger logger)
    {

        _logger = Guard.Against.Null(logger);

        _semaphoreSlim = new SemaphoreSlim(1, 1);
        // shortcut: CallerId is client-wide, so calls on one client are serialized.
        // Use more connections when parallel work is needed.
        _callGate = new SemaphoreSlim(1, 1);

        PureServiceClient = Guard.Against.Null(connection);

        PureServiceClient.DisableCrossThreadSafeties = true;

        PureServiceClient.MaxRetryCount = 10;

        PureServiceClient.RetryPauseTime = TimeSpan.FromSeconds(2);
    }

    public ServiceClient PureServiceClient { get; }
    public IStatistics Statistics => _usageStatistics;

    public void ApplyConnectionOptimalization() => PureServiceClient.EnableAffinityCookie = false;

    public Guid CreateRecord(Entity record, RequestSettings? requestSettings = null)
    {

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id == Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        CreateRequest createRequest = new() { Target = record };

        return Execute<CreateResponse>(createRequest, requestSettings)!.id;
    }

    public async Task<Entity> CreateRecordAndGetAsync(Entity record, RequestSettings? requestSettings = null)
    {

        Guid createdRecordId = await CreateRecordAsync(record, requestSettings).ConfigureAwait(false);

        ColumnSet columns = CreateColumnSet(record.Attributes.Keys);

        return await RetrieveAsync(record.LogicalName, createdRecordId, columns, requestSettings)
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateRecordAsync(Entity record, RequestSettings? requestSettings = null)
    {

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id == Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        CreateRequest createRequest = new() { Target = record };

        CreateResponse createResponse = (await ExecuteAsync<CreateResponse>(createRequest, requestSettings).ConfigureAwait(false))!;
        return Guard.Against.Null(createResponse).id;
    }

    public Guid[] CreateMultiple(
        EntityCollection targets,
        RequestSettings? requestSettings = null
    )
    {
        EntityCollection prepared = BulkOperationTargets.PrepareEntities(
            targets,
            false,
            nameof(targets)
        );
        CreateMultipleResponse response = Execute<CreateMultipleResponse>(
            new CreateMultipleRequest { Targets = prepared },
            requestSettings
        )!;

        return response.Ids ?? Array.Empty<Guid>();
    }

    public async Task<Guid[]> CreateMultipleAsync(
        EntityCollection targets,
        RequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default
    )
    {
        EntityCollection prepared = BulkOperationTargets.PrepareEntities(
            targets,
            false,
            nameof(targets)
        );
        CreateMultipleResponse response = (await ExecuteAsync<CreateMultipleResponse>(
            new CreateMultipleRequest { Targets = prepared },
            requestSettings,
            cancellationToken
        ).ConfigureAwait(false))!;

        return response.Ids ?? Array.Empty<Guid>();
    }

    public void UpdateMultiple(
        EntityCollection targets,
        RequestSettings? requestSettings = null
    )
    {
        EntityCollection prepared = BulkOperationTargets.PrepareEntities(
            targets,
            true,
            nameof(targets)
        );
        UpdateMultipleRequest request = new() { Targets = prepared };
        if (requestSettings?.ConcurrencyBehavior is ConcurrencyBehavior behavior)
        {
            request.ConcurrencyBehavior = behavior;
        }

        _ = Execute<UpdateMultipleResponse>(request, requestSettings);
    }

    public async Task UpdateMultipleAsync(
        EntityCollection targets,
        RequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default
    )
    {
        EntityCollection prepared = BulkOperationTargets.PrepareEntities(
            targets,
            true,
            nameof(targets)
        );
        UpdateMultipleRequest request = new() { Targets = prepared };
        if (requestSettings?.ConcurrencyBehavior is ConcurrencyBehavior behavior)
        {
            request.ConcurrencyBehavior = behavior;
        }

        _ = await ExecuteAsync<UpdateMultipleResponse>(
                request,
                requestSettings,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public UpsertResponse[] UpsertMultiple(
        EntityCollection targets,
        RequestSettings? requestSettings = null
    )
    {
        EntityCollection prepared = BulkOperationTargets.PrepareEntities(
            targets,
            true,
            nameof(targets)
        );
        UpsertMultipleResponse response = Execute<UpsertMultipleResponse>(
            new UpsertMultipleRequest { Targets = prepared },
            requestSettings
        )!;

        return response.Results ?? Array.Empty<UpsertResponse>();
    }

    public async Task<UpsertResponse[]> UpsertMultipleAsync(
        EntityCollection targets,
        RequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default
    )
    {
        EntityCollection prepared = BulkOperationTargets.PrepareEntities(
            targets,
            true,
            nameof(targets)
        );
        UpsertMultipleResponse response = (await ExecuteAsync<UpsertMultipleResponse>(
            new UpsertMultipleRequest { Targets = prepared },
            requestSettings,
            cancellationToken
        ).ConfigureAwait(false))!;

        return response.Results ?? Array.Empty<UpsertResponse>();
    }

    public void DeleteMultiple(
        EntityReferenceCollection targets,
        RequestSettings? requestSettings = null
    )
    {
        EntityReferenceCollection prepared = BulkOperationTargets.PrepareReferences(
            targets,
            nameof(targets)
        );
        _ = Execute<OrganizationResponse>(
            new DeleteMultipleRequest { Targets = prepared },
            requestSettings
        );
    }

    public async Task DeleteMultipleAsync(
        EntityReferenceCollection targets,
        RequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default
    )
    {
        EntityReferenceCollection prepared = BulkOperationTargets.PrepareReferences(
            targets,
            nameof(targets)
        );
        _ = await ExecuteAsync<OrganizationResponse>(
            new DeleteMultipleRequest { Targets = prepared },
            requestSettings,
            cancellationToken
        ).ConfigureAwait(false);
    }

    public void DeleteRecord(string logicalName, Guid id, RequestSettings? requestSettings = null)
    {

        DeleteRequest deleteRequest =
            new() { Target = EntityReferenceUtils.CreateEntityReference(id, logicalName, _logger) };

        _ = Execute<DeleteResponse>(deleteRequest, requestSettings);
    }

    public void DeleteRecord(EntityReference entityReference, RequestSettings? requestSettings = null)
    {

        Guard.Against.Null(entityReference);

        DeleteRecord(entityReference.LogicalName, entityReference.Id, requestSettings);
    }

    public async Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings? requestSettings = null)
    {

        DeleteRequest deleteRequest =
            new() { Target = EntityReferenceUtils.CreateEntityReference(id, logicalName, _logger) };

        _ = await ExecuteAsync<DeleteResponse>(deleteRequest, requestSettings).ConfigureAwait(false);
    }

    public async Task DeleteRecordAsync(EntityReference entityReference, RequestSettings? requestSettings = null)
    {

        Guard.Against.Null(entityReference);

        await DeleteRecordAsync(entityReference.LogicalName, entityReference.Id, requestSettings).ConfigureAwait(false);
    }

    public T? Execute<T>(OrganizationRequest request, RequestSettings? requestSettings = null)
        where T : OrganizationResponse
    {

        Guard.Against.Null(request);

        _callGate.Wait();
        try
        {
            using ReplaceAndRestoreCallerId _ = new(
                new ServiceClientCallerIdAdapter(PureServiceClient),
                _logger,
                requestSettings
            );

            ApplyBatchSettingsToInnerRequests(request, requestSettings);
            requestSettings?.AddToOrganizationRequest(request, _logger);

            Statistic statisticEntry = _usageStatistics.StartNew();
            bool succeeded = false;
            try
            {
                T? result = PureServiceClient.Execute(request) as T;
                succeeded = true;
                return result;
            }
            finally
            {
                _usageStatistics.Finish(statisticEntry, succeeded);
            }
        }
        finally
        {
            _callGate.Release();
        }
    }

    public ExecuteMultipleResponse Execute(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        RequestSettings? requestSettings = null
    )
    {

        Guard.Against.Null(executeMultipleRequestBuilder);

        return Execute<ExecuteMultipleResponse>(executeMultipleRequestBuilder.RequestWithResults, requestSettings)!;
    }

    public async Task<T?> ExecuteAsync<T>(
        OrganizationRequest request,
        RequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default
    ) where T : OrganizationResponse
    {

        Guard.Against.Null(request);

        await _callGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using ReplaceAndRestoreCallerId _ = new(
                new ServiceClientCallerIdAdapter(PureServiceClient),
                _logger,
                requestSettings
            );

            ApplyBatchSettingsToInnerRequests(request, requestSettings);
            requestSettings?.AddToOrganizationRequest(request, _logger);
            Statistic statisticEntry = _usageStatistics.StartNew();
            bool succeeded = false;
            try
            {
                T? result = (await PureServiceClient
                    .ExecuteAsync(request, cancellationToken)
                    .ConfigureAwait(false)) as T;
                succeeded = true;
                return result;
            }
            finally
            {
                _usageStatistics.Finish(statisticEntry, succeeded);
            }
        }
        finally
        {
            _callGate.Release();
        }
    }

    public async Task<ExecuteMultipleResponse> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        RequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default
    )
    {

        Guard.Against.Null(executeMultipleRequestBuilder);

        return (await ExecuteAsync<ExecuteMultipleResponse>(
            executeMultipleRequestBuilder.RequestWithResults,
            requestSettings,
            cancellationToken
        ).ConfigureAwait(false))!;
    }

    public ulong GetConnectionWeight() => _usageStatistics.UsageWeightFromLastMinutes(2);

    public Entity RefreshRecord(Entity record, RequestSettings? requestSettings = null)
    {

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        ColumnSet columns = CreateColumnSet(record.Attributes.Keys);

        return Retrieve(record.LogicalName, record.Id, columns, requestSettings);
    }

    public T RefreshRecord<T>(T record, RequestSettings? requestSettings = null) where T : Entity
    {

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        ColumnSet columns = CreateColumnSet(record.Attributes.Keys);

        return Retrieve<T>(record.LogicalName, record.Id, columns, requestSettings);
    }

    public async Task<T> RefreshRecordAsync<T>(T record, RequestSettings? requestSettings = null) where T : Entity
    {

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        ColumnSet columns = CreateColumnSet(record.Attributes.Keys);

        return await RetrieveAsync<T>(record.LogicalName, record.Id, columns, requestSettings).ConfigureAwait(false);
    }

    public async Task<Entity> RefreshRecordAsync(Entity record, RequestSettings? requestSettings = null)
    {

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        ColumnSet columns = CreateColumnSet(record.Attributes.Keys);

        return await RetrieveAsync(record.LogicalName, record.Id, columns, requestSettings).ConfigureAwait(false);
    }

    public void ReleaseLock()
    {
        _semaphoreSlim.Release();
    }

    /// <summary>
    /// Waits until every permit is free, then releases them again. Used during disposal so a
    /// client is not closed while a request is still using it.
    /// </summary>
    public async Task WaitForIdleAsync(CancellationToken cancellationToken = default)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
        _semaphoreSlim.Release();
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet, RequestSettings? requestSettings = null)
    {

        Guard.Against.Null(columnSet);

        RetrieveResponse retrieveResponse = Execute<RetrieveResponse>(
            new RetrieveRequest
            {
                ColumnSet = columnSet,
                Target = EntityReferenceUtils.CreateEntityReference(id, entityName, _logger)
            },
            requestSettings
        )!;
        return Guard.Against.Null(retrieveResponse).Entity;
    }

    public T Retrieve<T>(string entityName, Guid id, ColumnSet columnSet, RequestSettings? requestSettings = null)
        where T : Entity
    {

        return Retrieve(entityName, id, columnSet, requestSettings)?.ToEntity<T>()!;
    }

    public async Task<Entity> RetrieveAsync(
        string entityName,
        Guid id,
        ColumnSet columnSet,
        RequestSettings? requestSettings = null
    )
    {

        Guard.Against.Null(columnSet);

        RetrieveResponse retrieveResponse = (await ExecuteAsync<RetrieveResponse>(
            new RetrieveRequest
            {
                ColumnSet = columnSet,
                Target = EntityReferenceUtils.CreateEntityReference(id, entityName, _logger)
            },
            requestSettings
        ).ConfigureAwait(false))!;
        return Guard.Against.Null(retrieveResponse).Entity;
    }

    public async Task<T> RetrieveAsync<T>(
        string entityName,
        Guid id,
        ColumnSet columnSet,
        RequestSettings? requestSettings = null
    ) where T : Entity
    {

        Entity entity = await RetrieveAsync(entityName, id, columnSet, requestSettings)
            .ConfigureAwait(false);
        return entity?.ToEntity<T>()!;
    }

    public Entity[] RetrieveMultiple(QueryExpression queryExpression, RequestSettings? requestSettings = null)
    {

        Guard.Against.Null(queryExpression);

        return RetrieveAllPages(
                queryExpression,
                request => Execute<RetrieveMultipleResponse>(request, requestSettings)!
            )
            .ToArray();
    }

    internal static IEnumerable<Entity> RetrieveAllPages(
        QueryExpression queryExpression,
        Func<RetrieveMultipleRequest, RetrieveMultipleResponse> execute
    )
    {
        QueryExpression query = PrepareQuery(queryExpression, out PagingInfo? autoPaging);
        while (true)
        {
            RetrieveMultipleResponse retrieveMultipleResponse = execute(
                new RetrieveMultipleRequest { Query = query }
            );
            EntityCollection retrieveMultipleResult = Guard.Against.Null(retrieveMultipleResponse).EntityCollection;

            foreach (Entity record in retrieveMultipleResult.Entities)
            {
                yield return record;
            }

            if (!retrieveMultipleResult.MoreRecords || autoPaging == null)
            {
                break;
            }

            autoPaging.PageNumber++;
            autoPaging.PagingCookie = retrieveMultipleResult.PagingCookie;
        }
    }

    public T[] RetrieveMultiple<T>(QueryExpression queryExpression, RequestSettings? requestSettings = null)
        where T : Entity
    {

        Guard.Against.Null(queryExpression);

        return RetrieveAllPages(
                queryExpression,
                request => Execute<RetrieveMultipleResponse>(request, requestSettings)!
            )
            .Select(entity => entity.ToEntity<T>())
            .ToArray();
    }

    internal static async IAsyncEnumerable<Entity> RetrieveAllPagesAsync(
        QueryExpression queryExpression,
        Func<RetrieveMultipleRequest, Task<RetrieveMultipleResponse>> execute
    )
    {
        QueryExpression query = PrepareQuery(queryExpression, out PagingInfo? autoPaging);
        while (true)
        {
            RetrieveMultipleResponse retrieveMultipleResponse = await execute(
                    new RetrieveMultipleRequest { Query = query }
                )
                .ConfigureAwait(false);
            EntityCollection retrieveMultipleResult = Guard.Against.Null(retrieveMultipleResponse).EntityCollection;

            foreach (Entity record in retrieveMultipleResult.Entities)
            {
                yield return record;
            }

            if (!retrieveMultipleResult.MoreRecords || autoPaging == null)
            {
                break;
            }

            autoPaging.PageNumber++;
            autoPaging.PagingCookie = retrieveMultipleResult.PagingCookie;
        }
    }

    public async Task<Entity[]> RetrieveMultipleAsync(
        QueryExpression queryExpression,
        RequestSettings? requestSettings = null
    )
    {

        Guard.Against.Null(queryExpression);

        return await RetrieveAllPagesAsync(
                queryExpression,
                async request =>
                    (await ExecuteAsync<RetrieveMultipleResponse>(request, requestSettings)
                        .ConfigureAwait(false))!
            )
            .ToArrayAsync()
            .ConfigureAwait(false);
    }

    public async Task<T[]> RetrieveMultipleAsync<T>(
        QueryExpression queryExpression,
        RequestSettings? requestSettings = null
    ) where T : Entity
    {

        Guard.Against.Null(queryExpression);

        return await RetrieveAllPagesAsync(
                queryExpression,
                async request =>
                    (await ExecuteAsync<RetrieveMultipleResponse>(request, requestSettings)
                        .ConfigureAwait(false))!
            )
            .Select(entity => entity.ToEntity<T>())
            .ToArrayAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the query used for one retrieve call. A query with caller paging is used as-is.
    /// A <see cref="QueryExpression.TopCount"/> query is copied without the default empty
    /// <see cref="PagingInfo"/>. Otherwise a private copy is paged so the caller's query is
    /// never changed and concurrent calls cannot interfere.
    /// </summary>
    internal static QueryExpression PrepareQuery(
        QueryExpression queryExpression,
        out PagingInfo? autoPaging
    )
    {
        autoPaging = null;
        if (HasCallerPaging(queryExpression.PageInfo))
        {
            return queryExpression;
        }

        if (queryExpression.TopCount.HasValue)
        {
            // A new QueryExpression carries an empty PageInfo object. Dataverse rejects
            // TopCount together with any PageInfo, so send a copy without it.
            QueryExpression topQuery = CloneForPaging(queryExpression);
            topQuery.PageInfo = null;
            return topQuery;
        }

        QueryExpression query = CloneForPaging(queryExpression);
        autoPaging = new PagingInfo
        {
            Count = 5000,
            PageNumber = 1,
            PagingCookie = null
        };
        query.PageInfo = autoPaging;
        return query;
    }

    private static bool HasCallerPaging(PagingInfo? paging) =>
        paging != null
        && (
            paging.Count > 0
            || paging.PageNumber > 0
            || !string.IsNullOrEmpty(paging.PagingCookie)
        );

    private static QueryExpression CloneForPaging(QueryExpression source)
    {
        QueryExpression clone = new(source.EntityName)
        {
            ColumnSet = source.ColumnSet,
            Criteria = source.Criteria,
            Distinct = source.Distinct,
            NoLock = source.NoLock,
            TopCount = source.TopCount,
            QueryHints = source.QueryHints,
            ForceSeek = source.ForceSeek,
            DataSource = source.DataSource,
            SubQueryExpression = source.SubQueryExpression,
        };

        foreach (OrderExpression order in source.Orders)
        {
            clone.Orders.Add(order);
        }

        foreach (LinkEntity link in source.LinkEntities)
        {
            clone.LinkEntities.Add(link);
        }

        return clone;
    }

    public bool Test()
    {

        _callGate.Wait();
        try
        {
            Statistic statisticEntry = _usageStatistics.StartNew();
            bool succeeded = false;
            try
            {
                WhoAmIResponse response = (WhoAmIResponse)PureServiceClient.Execute(new WhoAmIRequest());
                succeeded = response != null && response.UserId != Guid.Empty;
                return succeeded;
            }
            finally
            {
                _usageStatistics.Finish(statisticEntry, succeeded);
            }
        }
        finally
        {
            _callGate.Release();
        }
    }

    public async Task<bool> TestAsync()
    {

        await _callGate.WaitAsync().ConfigureAwait(false);
        try
        {
            Statistic statisticEntry = _usageStatistics.StartNew();
            bool succeeded = false;
            try
            {
                WhoAmIResponse response = (WhoAmIResponse)await PureServiceClient.ExecuteAsync(new WhoAmIRequest()).ConfigureAwait(false);
                succeeded = response != null && response.UserId != Guid.Empty;
                return succeeded;
            }
            finally
            {
                _usageStatistics.Finish(statisticEntry, succeeded);
            }
        }
        finally
        {
            _callGate.Release();
        }
    }

    public bool TryLock()
    {
        return _semaphoreSlim.Wait(0);
    }

    public async Task<bool> TryLockAsync()
    {
        return await _semaphoreSlim.WaitAsync(0).ConfigureAwait(false);
    }

    public void UpdateRecord(Entity record, RequestSettings? requestSettings = null)
    {

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        UpdateRequest request = new() { Target = record };

        _ = Execute<UpdateResponse>(request, requestSettings);
    }

    public async Task UpdateRecordAsync(Entity record, RequestSettings? requestSettings = null)
    {

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        UpdateRequest request = new() { Target = record };

        _ = await ExecuteAsync<UpdateResponse>(request, requestSettings).ConfigureAwait(false);
    }

    public EntityReference UpsertRecord(Entity record, RequestSettings? requestSettings = null)
    {

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p =>
                !string.IsNullOrWhiteSpace(p.LogicalName)
                && (p.Id != Guid.Empty || (p.KeyAttributes != null && p.KeyAttributes.Count > 0))
        );

        UpsertRequest request = new() { Target = record };

        UpsertResponse executeResponse = Execute<UpsertResponse>(request, requestSettings)!;

        return Guard.Against.Null(executeResponse).Target;
    }

    public async Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings? requestSettings = null)
    {

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p =>
                !string.IsNullOrWhiteSpace(p.LogicalName)
                && (p.Id != Guid.Empty || (p.KeyAttributes != null && p.KeyAttributes.Count > 0))
        );

        UpsertRequest request = new() { Target = record };

        UpsertResponse executeResponse = (await ExecuteAsync<UpsertResponse>(request, requestSettings).ConfigureAwait(false))!;

        return Guard.Against.Null(executeResponse).Target;
    }

    private static void ApplyBatchSettingsToInnerRequests(
        OrganizationRequest request,
        RequestSettings? requestSettings
    )
    {
        if (requestSettings == null)
        {
            return;
        }

        switch (request)
        {
            case ExecuteMultipleRequest executeMultiple:
                foreach (OrganizationRequest inner in executeMultiple.Requests)
                {
                    requestSettings.ApplyToInnerRequest(inner);
                }

                break;
            case ExecuteTransactionRequest executeTransaction:
                foreach (OrganizationRequest inner in executeTransaction.Requests)
                {
                    requestSettings.ApplyToInnerRequest(inner);
                }

                break;
        }
    }

    private static ColumnSet CreateColumnSet(IEnumerable<string> columns)
    {
        ColumnSet columnSet = new();
        foreach (string column in columns)
        {
            columnSet.AddColumn(column);
        }

        return columnSet;
    }
}
