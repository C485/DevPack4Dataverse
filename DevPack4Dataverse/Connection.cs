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

public sealed class Connection : IConnection
{
    private readonly SemaphoreSlim _semaphoreSlim;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _callGate;
    private readonly Statistics _usageStatistics = new();

    public Connection(ServiceClient connection, ILogger logger, int maximumConcurrentlyUsage = 1)
    {
        using EntryExitLogger logGuard = new(logger);

        Guard.Against.NegativeOrZero(maximumConcurrentlyUsage);

        _logger = Guard.Against.Null(logger);

        _semaphoreSlim = new SemaphoreSlim(maximumConcurrentlyUsage, maximumConcurrentlyUsage);
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

    public Guid CreateRecord(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id == Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        CreateRequest createRequest = new() { Target = record };

        return Execute<CreateResponse>(createRequest, requestSettings).id;
    }

    public async Task<Entity> CreateRecordAndGetAsync(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guid createdRecordId = await CreateRecordAsync(record, requestSettings).ConfigureAwait(false);

        ColumnSet columns = CreateColumnSet(record.Attributes.Keys);

        return await RetrieveAsync(record.LogicalName, createdRecordId, columns, requestSettings)
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id == Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        CreateRequest createRequest = new() { Target = record };

        CreateResponse createResponse = await ExecuteAsync<CreateResponse>(createRequest, requestSettings).ConfigureAwait(false);
        return Guard.Against.Null(createResponse).id;
    }

    public void DeleteRecord(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        DeleteRequest deleteRequest =
            new() { Target = EntityReferenceUtils.CreateEntityReference(id, logicalName, _logger) };

        _ = Execute<DeleteResponse>(deleteRequest, requestSettings);
    }

    public void DeleteRecord(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(entityReference);

        DeleteRecord(entityReference.LogicalName, entityReference.Id, requestSettings);
    }

    public async Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        DeleteRequest deleteRequest =
            new() { Target = EntityReferenceUtils.CreateEntityReference(id, logicalName, _logger) };

        _ = await ExecuteAsync<DeleteResponse>(deleteRequest, requestSettings).ConfigureAwait(false);
    }

    public async Task DeleteRecordAsync(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(entityReference);

        await DeleteRecordAsync(entityReference.LogicalName, entityReference.Id, requestSettings).ConfigureAwait(false);
    }

    public T Execute<T>(OrganizationRequest request, RequestSettings requestSettings = null)
        where T : OrganizationResponse
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(request);

        _callGate.Wait();
        try
        {
            using ReplaceAndRestoreCallerId _ = new(PureServiceClient, _logger, requestSettings);

            requestSettings?.AddToOrganizationRequest(request, _logger);

            Statistic statisticEntry = _usageStatistics.StartNew();
            try
            {
                return PureServiceClient.Execute(request) as T;
            }
            finally
            {
                _usageStatistics.Finish(statisticEntry);
            }
        }
        finally
        {
            _callGate.Release();
        }
    }

    public ExecuteMultipleResponse Execute(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        RequestSettings requestSettings = null
    )
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(executeMultipleRequestBuilder);

        return Execute<ExecuteMultipleResponse>(executeMultipleRequestBuilder.RequestWithResults, requestSettings);
    }

    public async Task<T> ExecuteAsync<T>(OrganizationRequest request, RequestSettings requestSettings = null)
        where T : OrganizationResponse
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(request);

        await _callGate.WaitAsync().ConfigureAwait(false);

        try
        {
            using ReplaceAndRestoreCallerId _ = new(PureServiceClient, _logger, requestSettings);

            requestSettings?.AddToOrganizationRequest(request, _logger);
            Statistic statisticEntry = _usageStatistics.StartNew();
            try
            {
                return (await PureServiceClient.ExecuteAsync(request).ConfigureAwait(false)) as T;
            }
            finally
            {
                _usageStatistics.Finish(statisticEntry);
            }
        }
        finally
        {
            _callGate.Release();
        }
    }

    public async Task<ExecuteMultipleResponse> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        RequestSettings requestSettings = null
    )
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(executeMultipleRequestBuilder);

        return await ExecuteAsync<ExecuteMultipleResponse>(
            executeMultipleRequestBuilder.RequestWithResults,
            requestSettings
        ).ConfigureAwait(false);
    }

    public ulong GetConnectionWeight() => _usageStatistics.UsageWeightFromLastMinutes(2);

    public Entity RefreshRecord(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        ColumnSet columns = CreateColumnSet(record.Attributes.Keys);

        return Retrieve(record.LogicalName, record.Id, columns, requestSettings);
    }

    public T RefreshRecord<T>(T record, RequestSettings requestSettings = null) where T : Entity
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        ColumnSet columns = CreateColumnSet(record.Attributes.Keys);

        return Retrieve<T>(record.LogicalName, record.Id, columns, requestSettings);
    }

    public async Task<T> RefreshRecordAsync<T>(T record, RequestSettings requestSettings = null) where T : Entity
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        ColumnSet columns = CreateColumnSet(record.Attributes.Keys);

        return await RetrieveAsync<T>(record.LogicalName, record.Id, columns, requestSettings).ConfigureAwait(false);
    }

    public async Task<Entity> RefreshRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

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
        using EntryExitLogger logGuard = new(_logger);
        _semaphoreSlim.Release();
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(columnSet);

        RetrieveResponse retrieveResponse = Execute<RetrieveResponse>(
            new RetrieveRequest
            {
                ColumnSet = columnSet,
                Target = EntityReferenceUtils.CreateEntityReference(id, entityName, _logger)
            },
            requestSettings
        );
        return Guard.Against.Null(retrieveResponse).Entity;
    }

    public T Retrieve<T>(string entityName, Guid id, ColumnSet columnSet, RequestSettings requestSettings = null)
        where T : Entity
    {
        using EntryExitLogger logGuard = new(_logger);

        return Retrieve(entityName, id, columnSet, requestSettings)?.ToEntity<T>();
    }

    public async Task<Entity> RetrieveAsync(
        string entityName,
        Guid id,
        ColumnSet columnSet,
        RequestSettings requestSettings = null
    )
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(columnSet);

        RetrieveResponse retrieveResponse = await ExecuteAsync<RetrieveResponse>(
            new RetrieveRequest
            {
                ColumnSet = columnSet,
                Target = EntityReferenceUtils.CreateEntityReference(id, entityName, _logger)
            },
            requestSettings
        ).ConfigureAwait(false);
        return Guard.Against.Null(retrieveResponse).Entity;
    }

    public async Task<T> RetrieveAsync<T>(
        string entityName,
        Guid id,
        ColumnSet columnSet,
        RequestSettings requestSettings = null
    ) where T : Entity
    {
        using EntryExitLogger logGuard = new(_logger);

        Entity entity = await RetrieveAsync(entityName, id, columnSet, requestSettings)
            .ConfigureAwait(false);
        return entity?.ToEntity<T>();
    }

    public Entity[] RetrieveMultiple(QueryExpression queryExpression, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

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
                RetrieveMultipleResponse retrieveMultipleResponse = Execute<RetrieveMultipleResponse>(
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

    public T[] RetrieveMultiple<T>(QueryExpression queryExpression, RequestSettings requestSettings = null)
        where T : Entity
    {
        using EntryExitLogger logGuard = new(_logger);

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
                RetrieveMultipleResponse retrieveMultipleResponse = Execute<RetrieveMultipleResponse>(
                    new RetrieveMultipleRequest { Query = queryExpression },
                    requestSettings
                );
                EntityCollection retrieveMultipleResult = Guard.Against.Null(retrieveMultipleResponse).EntityCollection;

                foreach (Entity entity in retrieveMultipleResult.Entities)
                {
                    yield return entity.ToEntity<T>();
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

    public async Task<Entity[]> RetrieveMultipleAsync(
        QueryExpression queryExpression,
        RequestSettings requestSettings = null
    )
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(queryExpression);

        return await InnerRetrieveMultiple().ToArrayAsync().ConfigureAwait(false);

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
                RetrieveMultipleResponse retrieveMultipleResponse = await ExecuteAsync<RetrieveMultipleResponse>(
                    new RetrieveMultipleRequest { Query = queryExpression },
                    requestSettings
                ).ConfigureAwait(false);
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

    public async Task<T[]> RetrieveMultipleAsync<T>(
        QueryExpression queryExpression,
        RequestSettings requestSettings = null
    ) where T : Entity
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(queryExpression);

        return await InnerRetrieveMultiple().ToArrayAsync().ConfigureAwait(false);

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
                RetrieveMultipleResponse retrieveMultipleResponse = await ExecuteAsync<RetrieveMultipleResponse>(
                    new RetrieveMultipleRequest { Query = queryExpression },
                    requestSettings
                ).ConfigureAwait(false);
                EntityCollection retrieveMultipleResult = Guard.Against.Null(retrieveMultipleResponse).EntityCollection;

                foreach (Entity entity in retrieveMultipleResult.Entities)
                {
                    yield return entity.ToEntity<T>();
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

    public bool Test()
    {
        using EntryExitLogger logGuard = new(_logger);

        Statistic statisticEntry = _usageStatistics.StartNew();
        try
        {
            WhoAmIResponse response = (WhoAmIResponse)PureServiceClient.Execute(new WhoAmIRequest());

            return response != null && response.UserId != Guid.Empty;
        }
        finally
        {
            _usageStatistics.Finish(statisticEntry);
        }
    }

    public async Task<bool> TestAsync()
    {
        using EntryExitLogger logGuard = new(_logger);

        Statistic statisticEntry = _usageStatistics.StartNew();
        try
        {
            WhoAmIResponse response = (WhoAmIResponse)await PureServiceClient.ExecuteAsync(new WhoAmIRequest()).ConfigureAwait(false);

            return response != null && response.UserId != Guid.Empty;
        }
        finally
        {
            _usageStatistics.Finish(statisticEntry);
        }
    }

    public bool TryLock()
    {
        using EntryExitLogger logGuard = new(_logger);
        return _semaphoreSlim.Wait(0);
    }

    public async Task<bool> TryLockAsync()
    {
        using EntryExitLogger logGuard = new(_logger);
        return await _semaphoreSlim.WaitAsync(0).ConfigureAwait(false);
    }

    public void UpdateRecord(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        UpdateRequest request = new() { Target = record };

        _ = Execute<UpdateResponse>(request, requestSettings);
    }

    public async Task UpdateRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        UpdateRequest request = new() { Target = record };

        _ = await ExecuteAsync<UpdateResponse>(request, requestSettings).ConfigureAwait(false);
    }

    public EntityReference UpsertRecord(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p =>
                !string.IsNullOrWhiteSpace(p.LogicalName)
                && (p.Id != Guid.Empty || (p.KeyAttributes != null && p.KeyAttributes.Count > 0))
        );

        UpsertRequest request = new() { Target = record };

        UpsertResponse executeResponse = Execute<UpsertResponse>(request, requestSettings);

        return Guard.Against.Null(executeResponse).Target;
    }

    public async Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p =>
                !string.IsNullOrWhiteSpace(p.LogicalName)
                && (p.Id != Guid.Empty || (p.KeyAttributes != null && p.KeyAttributes.Count > 0))
        );

        UpsertRequest request = new() { Target = record };

        UpsertResponse executeResponse = await ExecuteAsync<UpsertResponse>(request, requestSettings).ConfigureAwait(false);

        return Guard.Against.Null(executeResponse).Target;
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
