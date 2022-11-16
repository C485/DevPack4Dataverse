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
    private readonly ServiceClient _connection;
    private readonly object _lockObj;
    private readonly ILogger _logger;
    private bool _disableLockingCheck;

    public Connection(ServiceClient connection, ILogger logger)
    {
        using EntryExitLogger logGuard = new(logger);
        _logger = Guard
            .Against
            .Null(logger);

        _lockObj = new object();
        _connection = Guard
           .Against
           .Null(connection);

        _connection
           .DisableCrossThreadSafeties = true;

        _connection
           .MaxRetryCount = 10;

        _connection
           .RetryPauseTime = TimeSpan.FromSeconds(2);
    }

    public Guid CreateRecord(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .NullOrInvalidInput(record, nameof(record), p => p.Id == Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

        CreateRequest createRequest = new()
        {
            Target = record
        };

        return Execute<CreateResponse>(createRequest, requestSettings).id;
    }

    public async Task<Entity> CreateRecordAndGetAsync(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guid createdRecordId = await CreateRecordAsync(record, requestSettings);

        ColumnSet columns = new(record.Attributes.Keys.ToArray());

        using ReplaceAndRestoreCallerId _ = new(_connection, requestSettings);

        return await _connection
           .RetrieveAsync(record.LogicalName, createdRecordId, columns)
           .ConfigureAwait(true);
    }

    public async Task<Guid> CreateRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .NullOrInvalidInput(record, nameof(record), p => p.Id == Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

        Guard
           .Against
           .Null(requestSettings);

        CreateRequest createRequest = new()
        {
            Target = record
        };

        return (await ExecuteAsync<CreateResponse>(createRequest, requestSettings).ConfigureAwait(true)).id;
    }

    public void DeleteRecord(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .NullOrEmpty(logicalName);

        Guard
           .Against
           .Default(id);

        DeleteRequest deleteRequest = new()
        {
            Target = new EntityReference(logicalName, id)
        };

        _ = Execute<DeleteResponse>(deleteRequest, requestSettings);
    }

    public void DeleteRecord(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .Null(entityReference);

        DeleteRecord(entityReference.LogicalName, entityReference.Id, requestSettings);
    }

    public async Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .NullOrEmpty(logicalName);

        Guard
           .Against
           .Default(id);

        DeleteRequest deleteRequest = new()
        {
            Target = new EntityReference(logicalName, id)
        };

        _ = await ExecuteAsync<DeleteResponse>(deleteRequest, requestSettings)
            .ConfigureAwait(true);
    }

    public async Task DeleteRecordAsync(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        await DeleteRecordAsync(entityReference.LogicalName, entityReference.Id, requestSettings)
            .ConfigureAwait(true);
    }

    public IConnection DisableLockingCheck()
    {
        using EntryExitLogger logGuard = new(_logger);

        _disableLockingCheck = true;
        return this;
    }

    public T Execute<T>(OrganizationRequest request, RequestSettings requestSettings = null) where T : OrganizationResponse
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .Null(request);

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for this connection.");
        }

        using ReplaceAndRestoreCallerId _ = new(_connection, requestSettings);

        requestSettings?.AddToOrganizationRequest(request);

        return _connection
           .Execute(request) as T;
    }

    public ExecuteMultipleResponse Execute(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .Null(executeMultipleRequestBuilder);

        return Execute<ExecuteMultipleResponse>(executeMultipleRequestBuilder.RequestWithResults, requestSettings);
    }

    public async Task<T> ExecuteAsync<T>(OrganizationRequest request, RequestSettings requestSettings = null) where T : OrganizationResponse
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .Null(request);

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for this connection.");
        }

        using ReplaceAndRestoreCallerId _ = new(_connection, requestSettings);

        requestSettings?.AddToOrganizationRequest(request);

        return await _connection
           .ExecuteAsync(request)
           .ConfigureAwait(true) as T;
    }

    public async Task<ExecuteMultipleResponse> ExecuteAsync(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .Null(executeMultipleRequestBuilder);

        return await ExecuteAsync<ExecuteMultipleResponse>(executeMultipleRequestBuilder.RequestWithResults, requestSettings)
            .ConfigureAwait(true);
    }

    public bool IsLockedByThisThread()
    {
        using EntryExitLogger logGuard = new(_logger);

        return Monitor
           .IsEntered(_lockObj);
    }

    public Entity RefreshRecord(Entity record)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .NullOrInvalidInput(record, nameof(record), p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for this connection.");
        }

        ColumnSet columns = new(record.Attributes.Keys.ToArray());

        return _connection
           .Retrieve(record.LogicalName, record.Id, columns);
    }

    public async Task<Entity> RefreshRecordAsync(Entity record)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .NullOrInvalidInput(record, nameof(record), p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for this connection.");
        }

        ColumnSet columns = new(record.Attributes.Keys.ToArray());

        return await _connection
           .RetrieveAsync(record.LogicalName, record.Id, columns)
           .ConfigureAwait(true);
    }

    public void ReleaseLock()
    {
        using EntryExitLogger logGuard = new(_logger);

        Monitor
           .Exit(_lockObj);
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
    {
        using EntryExitLogger logGuard = new(_logger);

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for this connection.");
        }

        Guard
           .Against
           .NullOrEmpty(entityName);

        Guard
           .Against
           .Default(id);

        Guard
           .Against
           .Null(columnSet);

        return _connection
           .Retrieve(entityName, id, columnSet);
    }

    public async Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet)
    {
        using EntryExitLogger logGuard = new(_logger);

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for this connection.");
        }

        Guard
           .Against
           .NullOrEmpty(entityName);

        Guard
           .Against
           .Default(id);

        Guard
           .Against
           .Null(columnSet);

        return await _connection
           .RetrieveAsync(entityName, id, columnSet)
           .ConfigureAwait(true);
    }

    public Entity[] RetrieveMultiple(QueryExpression queryExpression)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .Null(queryExpression);

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for this connection.");
        }

        return InnerRetrieveMultiple()
           .ToArray();

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
                EntityCollection retrieveMultipleResult = _connection
                   .RetrieveMultiple(queryExpression);

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

    public async Task<Entity[]> RetrieveMultipleAsync(QueryExpression queryExpression)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .Null(queryExpression);

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for this connection.");
        }

        return await InnerRetrieveMultiple()
           .ToArrayAsync()
           .ConfigureAwait(true);

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
                EntityCollection retrieveMultipleResult = await _connection
                   .RetrieveMultipleAsync(queryExpression)
                   .ConfigureAwait(true);

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

    public bool Test()
    {
        using EntryExitLogger logGuard = new(_logger);

        WhoAmIResponse response = (WhoAmIResponse)_connection
           .Execute(new WhoAmIRequest());

        return response != null
            && response.UserId != Guid.Empty;
    }

    public async Task<bool> TestAsync()
    {
        using EntryExitLogger logGuard = new(_logger);

        WhoAmIResponse response = (WhoAmIResponse)await _connection
           .ExecuteAsync(new WhoAmIRequest())
           .ConfigureAwait(true);

        return response != null
            && response.UserId != Guid.Empty;
    }

    public bool TryLock()
    {
        using EntryExitLogger logGuard = new(_logger);

        return Monitor
           .TryEnter(_lockObj);
    }

    public Guid UpdateRecord(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .NullOrInvalidInput(record, nameof(record), p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for used connection.");
        }

        UpdateRequest request = new()
        {
            Target = record
        };

        _ = Execute<UpdateResponse>(request, requestSettings);

        return record.Id;
    }

    public async Task<Guid> UpdateRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
            .Against
            .NullOrInvalidInput(record, nameof(record), p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for used connection.");
        }

        UpdateRequest request = new()
        {
            Target = record
        };

        _ = await ExecuteAsync<UpdateResponse>(request, requestSettings)
            .ConfigureAwait(true);

        return record.Id;
    }

    public EntityReference UpsertRecord(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .NullOrInvalidInput(record, nameof(record), p => string.IsNullOrEmpty(p.LogicalName));

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for used connection.");
        }

        UpsertRequest request = new()
        {
            Target = record
        };

        UpsertResponse executeResponse = Execute<UpsertResponse>(request, requestSettings);

        return Guard
           .Against
           .Null(executeResponse)
           .Target;
    }

    public async Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .NullOrInvalidInput(record, nameof(record), p => string.IsNullOrEmpty(p.LogicalName));

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for used connection.");
        }

        UpsertRequest request = new()
        {
            Target = record
        };

        UpsertResponse executeResponse = await ExecuteAsync<UpsertResponse>(request, requestSettings)
            .ConfigureAwait(true);

        return Guard
           .Against
           .Null(executeResponse)
           .Target;
    }
}