using Ardalis.GuardClauses;
using C485.DataverseClientProxy.Interfaces;
using C485.DataverseClientProxy.Models;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace C485.DataverseClientProxy;

public class Connection : IConnection
{
    private readonly ServiceClient _connection;
    private readonly object _lockObj;
    private readonly OrganizationServiceContext _xrmServiceContext;
    private bool _disableLockingCheck;

    public Connection(ServiceClient connection, ILogger logger)
    {
        _lockObj = new object();
        _connection = Guard
           .Against
           .Null(connection);

        _xrmServiceContext = new OrganizationServiceContext(connection);
        _connection
           .DisableCrossThreadSafeties = true;

        _connection
           .MaxRetryCount = 10;

        _connection
           .RetryPauseTime = TimeSpan.FromSeconds(5);
        Logger = Guard
            .Against
            .Null(logger);
    }

    public ILogger Logger { get; }

    public IQueryable<Entity> CreateQuery_Unsafe_Unprotected(
        string entityLogicalName,
        OrganizationServiceContextSettings organizationServiceContextSettings = default)
    {
        organizationServiceContextSettings ??= OrganizationServiceContextSettings.Default;

        if (organizationServiceContextSettings.ClearChangesEveryTime)
        {
            _xrmServiceContext
               .ClearChanges();
        }

        Guard
           .Against
           .NullOrEmpty(entityLogicalName);

        return _xrmServiceContext
           .CreateQuery(entityLogicalName);
    }

    public IQueryable<T> CreateQuery_Unsafe_Unprotected<T>(
        OrganizationServiceContextSettings organizationServiceContextSettings = default) where T : Entity
    {
        organizationServiceContextSettings ??= OrganizationServiceContextSettings.Default;

        if (organizationServiceContextSettings.ClearChangesEveryTime)
        {
            _xrmServiceContext
               .ClearChanges();
        }

        return _xrmServiceContext
           .CreateQuery<T>();
    }

    public Guid CreateRecord(Entity record, RequestSettings requestSettings = null)
    {
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
        Guid createdRecordId = await CreateRecordAsync(record, requestSettings);

        ColumnSet columns = new(record.Attributes.Keys.ToArray());

        using ReplaceAndRestoreCallerId _ = new(_connection, requestSettings);

        return await _connection
           .RetrieveAsync(record.LogicalName, createdRecordId, columns);
    }

    public async Task<Guid> CreateRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
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

        return (await ExecuteAsync<CreateResponse>(createRequest, requestSettings)).id;
    }

    public void DeleteRecord(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
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
        Guard
           .Against
           .Null(entityReference);

        DeleteRecord(entityReference.LogicalName, entityReference.Id, requestSettings);
    }

    public async Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
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

        _ = await ExecuteAsync<DeleteResponse>(deleteRequest, requestSettings);
    }

    public async Task DeleteRecordAsync(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        await DeleteRecordAsync(entityReference.LogicalName, entityReference.Id, requestSettings);
    }

    public void DisableLockingCheck()
    {
        _disableLockingCheck = true;
    }

    public T Execute<T>(OrganizationRequest request, RequestSettings requestSettings = null) where T : OrganizationResponse
    {
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
        Guard
           .Against
           .Null(executeMultipleRequestBuilder);

        return Execute<ExecuteMultipleResponse>(executeMultipleRequestBuilder.RequestWithResults, requestSettings);
    }

    public async Task<T> ExecuteAsync<T>(OrganizationRequest request, RequestSettings requestSettings = null) where T : OrganizationResponse
    {
        Guard
           .Against
           .Null(request);

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for this connection.");
        }

        using ReplaceAndRestoreCallerId _ = new(_connection, requestSettings);

        requestSettings?.AddToOrganizationRequest(request);

        return (await _connection
           .ExecuteAsync(request)) as T;
    }

    public async Task<OrganizationResponse> ExecuteAsync(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, RequestSettings requestSettings = null)
    {
        Guard
           .Against
           .Null(executeMultipleRequestBuilder);

        return await ExecuteAsync<ExecuteMultipleResponse>(executeMultipleRequestBuilder.RequestWithResults, requestSettings);
    }

    public bool IsLockedByThisThread()
    {
        return Monitor
           .IsEntered(_lockObj);
    }

    public Entity[] QueryMultiple(
        string entityLogicalName,
        Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default)
    {
        organizationServiceContextSettings ??= OrganizationServiceContextSettings.Default;
        Guard
           .Against
           .NullOrEmpty(entityLogicalName);

        Guard
           .Against
           .Null(queryBuilder);

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for used connection.");
        }

        if (organizationServiceContextSettings.ClearChangesEveryTime)
        {
            _xrmServiceContext.ClearChanges();
        }

        IQueryable<Entity> query = _xrmServiceContext
           .CreateQuery(entityLogicalName);

        Entity[] queryResults = queryBuilder(query)
           .ToArray();

        if (!organizationServiceContextSettings.DetachRetrievedRecords)
        {
            return queryResults;
        }

        foreach (Entity entity in queryResults)
        {
            _xrmServiceContext
               .Detach(entity, true);
        }

        return queryResults;
    }

    public T[] QueryMultiple<T>(
        Func<IQueryable<T>, IQueryable<T>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default) where T : Entity
    {
        organizationServiceContextSettings ??= OrganizationServiceContextSettings.Default;

        Guard
           .Against
           .Null(queryBuilder);

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for used connection.");
        }

        if (organizationServiceContextSettings.ClearChangesEveryTime)
        {
            _xrmServiceContext.ClearChanges();
        }

        IQueryable<T> query = _xrmServiceContext
           .CreateQuery<T>();

        T[] queryResults = queryBuilder(query)
           .ToArray();

        if (!organizationServiceContextSettings.DetachRetrievedRecords)
        {
            return queryResults;
        }

        foreach (T entity in queryResults)
        {
            _xrmServiceContext
               .Detach(entity, true);
        }

        return queryResults;
    }

    public async Task<Entity[]> QueryMultipleAsync(
        string entityLogicalName,
        Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default)
    {
        return await Task
           .Run(() => QueryMultiple(entityLogicalName, queryBuilder, organizationServiceContextSettings));
    }

    public async Task<T[]> QueryMultipleAsync<T>(
        Func<IQueryable<T>, IQueryable<T>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default) where T : Entity
    {
        return await Task
           .Run(() => QueryMultiple(queryBuilder, organizationServiceContextSettings));
    }

    public Entity QuerySingle(
        string entityLogicalName,
        Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default)
    {
        organizationServiceContextSettings ??= OrganizationServiceContextSettings.Default;

        if (organizationServiceContextSettings.ClearChangesEveryTime)
        {
            _xrmServiceContext
               .ClearChanges();
        }

        Guard
           .Against
           .NullOrEmpty(entityLogicalName);

        Guard
           .Against
           .Null(queryBuilder);

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for used connection.");
        }

        IQueryable<Entity> query = _xrmServiceContext
           .CreateQuery(entityLogicalName);

        Entity[] queryResults = queryBuilder(query)
           .ToArray();

        if (!organizationServiceContextSettings.DetachRetrievedRecords)
        {
            Guard
               .Against
               .InvalidInput(queryResults,
                    nameof(queryResults),
                    p => p.Length == 1,
                    $"Expected one record, retrieved {queryResults.Length}.");

            return queryResults[0];
        }

        foreach (Entity entity in queryResults)
        {
            _xrmServiceContext
               .Detach(entity, true);
        }

        Guard
           .Against
           .InvalidInput(queryResults,
                nameof(queryResults),
                p => p.Length == 1,
                $"Expected one record, retrieved {queryResults.Length}.");

        return queryResults[0];
    }

    public T QuerySingle<T>(
        Func<IQueryable<T>, IQueryable<T>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default) where T : Entity
    {
        organizationServiceContextSettings ??= OrganizationServiceContextSettings.Default;

        if (organizationServiceContextSettings.ClearChangesEveryTime)
        {
            _xrmServiceContext
               .ClearChanges();
        }

        Guard
           .Against
           .Null(queryBuilder);

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for used connection.");
        }

        IQueryable<T> query = _xrmServiceContext
           .CreateQuery<T>();

        T[] queryResults = queryBuilder(query)
           .ToArray();

        if (!organizationServiceContextSettings.DetachRetrievedRecords)
        {
            Guard
               .Against
               .InvalidInput(queryResults,
                    nameof(queryResults),
                    p => p.Length == 1,
                    $"Expected one record, retrieved {queryResults.Length}.");

            return queryResults[0];
        }

        foreach (T entity in queryResults)
        {
            _xrmServiceContext
               .Detach(entity, true);
        }

        Guard
           .Against
           .InvalidInput(queryResults,
                nameof(queryResults),
                p => p.Length == 1,
                $"Expected one record, retrieved {queryResults.Length}.");

        return queryResults[0];
    }

    public async Task<Entity> QuerySingleAsync(
        string entityLogicalName,
        Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default)
    {
        return await Task
           .Run(() => QuerySingle(entityLogicalName, queryBuilder, organizationServiceContextSettings));
    }

    public async Task<T> QuerySingleAsync<T>(
        Func<IQueryable<T>, IQueryable<T>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default) where T : Entity
    {
        return await Task
           .Run(() => QuerySingle(queryBuilder, organizationServiceContextSettings));
    }

    public Entity QuerySingleOrDefault(
        string entityLogicalName,
        Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default)
    {
        organizationServiceContextSettings ??= OrganizationServiceContextSettings.Default;

        if (organizationServiceContextSettings.ClearChangesEveryTime)
        {
            _xrmServiceContext
               .ClearChanges();
        }

        Guard
           .Against
           .NullOrEmpty(entityLogicalName);

        Guard
           .Against
           .Null(queryBuilder);

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for used connection.");
        }

        IQueryable<Entity> query = _xrmServiceContext
           .CreateQuery(entityLogicalName);

        Entity[] queryResults = queryBuilder(query)
           .ToArray();

        if (!organizationServiceContextSettings.DetachRetrievedRecords)
        {
            Guard
               .Against
               .InvalidInput(queryResults,
                    nameof(queryResults),
                    p => p.Length <= 1,
                    $"Expected one record, retrieved {queryResults.Length}.");

            return queryResults
               .SingleOrDefault();
        }

        Guard
           .Against
           .InvalidInput(queryResults,
                nameof(queryResults),
                p => p.Length <= 1,
                $"Expected one record, retrieved {queryResults.Length}.");

        foreach (Entity entity in queryResults)
        {
            _xrmServiceContext
               .Detach(entity, true);
        }

        return queryResults
           .SingleOrDefault();
    }

    public T QuerySingleOrDefault<T>(
        Func<IQueryable<T>, IQueryable<T>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default) where T : Entity
    {
        organizationServiceContextSettings ??= OrganizationServiceContextSettings.Default;

        if (organizationServiceContextSettings.ClearChangesEveryTime)
        {
            _xrmServiceContext
               .ClearChanges();
        }

        Guard
           .Against
           .Null(queryBuilder);

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for used connection.");
        }

        IQueryable<T> query = _xrmServiceContext
           .CreateQuery<T>();

        T[] queryResults = queryBuilder(query)
           .ToArray();

        if (!organizationServiceContextSettings.DetachRetrievedRecords)
        {
            Guard
               .Against
               .InvalidInput(queryResults,
                    nameof(queryResults),
                    p => p.Length <= 1,
                    $"Expected one record, retrieved {queryResults.Length}.");

            return queryResults
               .SingleOrDefault();
        }

        Guard
           .Against
           .InvalidInput(queryResults,
                nameof(queryResults),
                p => p.Length <= 1,
                $"Expected one record, retrieved {queryResults.Length}.");

        foreach (T entity in queryResults)
        {
            _xrmServiceContext
               .Detach(entity, true);
        }

        return queryResults
           .SingleOrDefault();
    }

    public async Task<Entity> QuerySingleOrDefaultAsync(
        string entityLogicalName,
        Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default)
    {
        return await Task
           .Run(() => QuerySingleOrDefault(entityLogicalName, queryBuilder, organizationServiceContextSettings));
    }

    public async Task<T> QuerySingleOrDefaultAsync<T>(
        Func<IQueryable<T>, IQueryable<T>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default) where T : Entity
    {
        return await Task
           .Run(() => QuerySingleOrDefault(queryBuilder, organizationServiceContextSettings));
    }

    public Entity RefreshRecord(Entity record)
    {
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
        Guard
           .Against
           .NullOrInvalidInput(record, nameof(record), p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

        if (!_disableLockingCheck && !IsLockedByThisThread())
        {
            throw new ArgumentException("Lock not set for this connection.");
        }

        ColumnSet columns = new(record.Attributes.Keys.ToArray());

        return await _connection
           .RetrieveAsync(record.LogicalName, record.Id, columns);
    }

    public void ReleaseLock()
    {
        Monitor
           .Exit(_lockObj);
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
    {
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
           .RetrieveAsync(entityName, id, columnSet);
    }

    public Entity[] RetrieveMultiple(QueryExpression queryExpression)
    {
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
        return await Task
           .Run(() => RetrieveMultiple(queryExpression));
    }

    public bool Test()
    {
        WhoAmIResponse response = (WhoAmIResponse)_connection
           .Execute(new WhoAmIRequest());

        return response != null
            && response.UserId != Guid.Empty;
    }

    public async Task<bool> TestAsync()
    {
        return await Task
           .Run(Test);
    }

    public bool TryLock()
    {
        return Monitor
           .TryEnter(_lockObj);
    }

    public Guid UpdateRecord(Entity record, RequestSettings requestSettings = null)
    {
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

        _ = await ExecuteAsync<UpdateResponse>(request, requestSettings);

        return record.Id;
    }

    public EntityReference UpsertRecord(Entity record, RequestSettings requestSettings = null)
    {
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

        UpsertResponse executeResponse = await ExecuteAsync<UpsertResponse>(request, requestSettings);

        return Guard
           .Against
           .Null(executeResponse)
           .Target;
    }
}