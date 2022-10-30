using Ardalis.GuardClauses;
using C485.DataverseClientProxy.Interfaces;
using C485.DataverseClientProxy.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace C485.DataverseClientProxy;

public sealed class DataverseClientProxy : IDataverseClientProxy
{
    private readonly ConcurrentBag<IConnection> _connections;
    private readonly TimeSpan _sleepTimeForConnectionGetter = TimeSpan.FromMilliseconds(10);

    public DataverseClientProxy()
    {
        _connections = new ConcurrentBag<IConnection>();
    }

    public async Task AddNewConnection(IConnectionCreator connectionCreator)
    {
        Guard
           .Against
           .Null(connectionCreator);

        IConnection newConnection = connectionCreator
           .Create();

        Guard
           .Against
           .Null(newConnection);

        bool isConnectionValid = await newConnection
           .TestAsync();

        if (!isConnectionValid)
        {
            throw new InvalidProgramException("Connection is not valid.");
        }

        _connections
           .Add(newConnection);
    }

    public AdvancedExecuteMultipleRequestsStatistics AdvancedExecuteMultipleRequests(
            ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken = default)
    {
        Guard
           .Against
           .Null(executeMultipleRequestSettings);

        Guard
           .Against
           .Null(executeMultipleRequestBuilder);

        if (executeMultipleRequestBuilder.Count == 0)
        {
            return new AdvancedExecuteMultipleRequestsStatistics
            {
                RecordsProcessed = 0,
                Stopwatch = new Stopwatch(),
                ThreadsUsed = 0
            };
        }

        int threadsCount = executeMultipleRequestSettings.MaxDegreeOfParallelism <= 0
            ? _connections.Count
            : executeMultipleRequestSettings.MaxDegreeOfParallelism;

        AdvancedExecuteMultipleRequestsStatistics chunksStatistics = new()
        {
            Stopwatch = Stopwatch.StartNew(),
            ThreadsUsed = threadsCount
        };

        int progress = 0;
        CancellationTokenSource cts = new();
        Thread statusReportThread = new(token =>
        {
            CancellationToken ct = (CancellationToken)token;

            while (!ct.IsCancellationRequested)
            {
                Thread
                   .Sleep(executeMultipleRequestSettings.ReportProgressInterval);

                executeMultipleRequestSettings.ReportProgress(Thread.VolatileRead(ref progress),
                    executeMultipleRequestBuilder.Count);
            }
        });

        statusReportThread
           .Start(cts.Token);

        try
        {
            OrganizationRequest[][] allRequestChunks = executeMultipleRequestBuilder
               .RequestWithResults
               .Requests
               .Select((s, i) => new
               {
                   Value = s,
                   Index = i
               })
               .GroupBy(p => p.Index / executeMultipleRequestSettings.RequestSize)
               .Select(p => p
                   .Select(x => x.Value)
                   .ToArray())
               .ToArray();

            Parallel.ForEach(allRequestChunks,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = threadsCount,
                    CancellationToken = cancellationToken
                },
                packOfRequests =>
                {
                    using ConnectionLease connectionLease = GetConnection();

                    try
                    {
                        ExecuteMultipleRequest requestWithResults = new()
                        {
                            Settings = new ExecuteMultipleSettings
                            {
                                ContinueOnError = true,
                                ReturnResponses = true
                            },
                            Requests = new OrganizationRequestCollection()
                        };

                        requestWithResults
                           .Requests
                           .AddRange(packOfRequests);

                        ExecuteMultipleResponse responseWithResults =
                            (ExecuteMultipleResponse)connectionLease
                               .Connection
                               .Execute(requestWithResults,
                                    new RequestSettings
                                    {
                                        ImpersonateAsUserByDataverseId =
                                            executeMultipleRequestBuilder.ImpersonateAsUserById,
                                        SkipPluginExecution = executeMultipleRequestBuilder.SkipPluginExecution
                                    });

                        foreach (ExecuteMultipleResponseItem responseItem in responseWithResults.Responses)
                        {
                            if (responseItem.Fault == null)
                            {
                                continue;
                            }

                            OrganizationRequest requestOrigin = requestWithResults
                               .Requests[responseItem.RequestIndex];

                            executeMultipleRequestSettings.ErrorReport(requestOrigin, responseItem.Fault.ToString());
                        }
                    }
                    catch (Exception e)
                    {
                        foreach (OrganizationRequest recordToSave in packOfRequests)
                        {
                            executeMultipleRequestSettings.ErrorReport(recordToSave, e.ToString());
                        }
                    }

                    Interlocked.Add(ref progress, packOfRequests.Length);
                });
        }
        finally
        {
            cts
               .Cancel();

            statusReportThread
               .Join();

            cts
               .Dispose();
        }

        chunksStatistics
           .Stopwatch
           .Stop();

        chunksStatistics.RecordsRequested = executeMultipleRequestBuilder
           .RequestWithResults
           .Requests
           .Count;

        chunksStatistics.RecordsProcessed = progress;

        chunksStatistics.Cancelled = cancellationToken
           .IsCancellationRequested;

        return chunksStatistics;
    }

    public async Task<AdvancedExecuteMultipleRequestsStatistics> AdvancedExecuteMultipleRequestsAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken = default)
    {
        return await Task
           .Run(() => AdvancedExecuteMultipleRequests(executeMultipleRequestBuilder,
                    executeMultipleRequestSettings,
                    cancellationToken),
                cancellationToken);
    }

    public IQueryable<Entity> CreateQuery_Unsafe_Unprotected(
        string entityLogicalName,
        OrganizationServiceContextSettings organizationServiceContextSettings = default)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .CreateQuery_Unsafe_Unprotected(entityLogicalName, organizationServiceContextSettings);
    }

    public IQueryable<T> CreateQuery_Unsafe_Unprotected<T>(
        OrganizationServiceContextSettings organizationServiceContextSettings = default) where T : Entity
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .CreateQuery_Unsafe_Unprotected<T>(organizationServiceContextSettings);
    }

    public Guid CreateRecord(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .CreateRecord(record, requestSettings);
    }

    public async Task<Guid> CreateRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .CreateRecordAsync(record, requestSettings);
    }

    public void DeleteRecord(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = GetConnection();

        connectionLease
           .Connection
           .DeleteRecord(logicalName, id, requestSettings);
    }

    public void DeleteRecord(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = GetConnection();

        connectionLease
           .Connection
           .DeleteRecord(entityReference, requestSettings);
    }

    public async Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        await connectionLease
           .Connection
           .DeleteRecordAsync(logicalName, id, requestSettings);
    }

    public async Task DeleteRecordAsync(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();
        await connectionLease
           .Connection
           .DeleteRecordAsync(entityReference, requestSettings);
    }

    public OrganizationResponse Execute(OrganizationRequest request, RequestSettings requestSettings)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .Execute(request, requestSettings);
    }

    public OrganizationResponse Execute(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .Execute(executeMultipleRequestBuilder);
    }

    public async Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request, RequestSettings requestSettings)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .ExecuteAsync<OrganizationResponse>(request, requestSettings);
    }

    public async Task<OrganizationResponse> ExecuteAsync(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .ExecuteAsync(executeMultipleRequestBuilder);
    }

    public Entity[] QueryMultiple(
        string entityLogicalName,
        Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .QueryMultiple(entityLogicalName, queryBuilder, organizationServiceContextSettings);
    }

    public T[] QueryMultiple<T>(
        Func<IQueryable<T>, IQueryable<T>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default) where T : Entity
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .QueryMultiple(queryBuilder, organizationServiceContextSettings);
    }

    public async Task<Entity[]> QueryMultipleAsync(
        string entityLogicalName,
        Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .QueryMultipleAsync(entityLogicalName, queryBuilder, organizationServiceContextSettings);
    }

    public async Task<T[]> QueryMultipleAsync<T>(
        Func<IQueryable<T>, IQueryable<T>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default) where T : Entity
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .QueryMultipleAsync(queryBuilder, organizationServiceContextSettings);
    }

    public Entity QuerySingle(
        string entityLogicalName,
        Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .QuerySingle(entityLogicalName, queryBuilder, organizationServiceContextSettings);
    }

    public T QuerySingle<T>(
        Func<IQueryable<T>, IQueryable<T>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default) where T : Entity
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .QuerySingle(queryBuilder, organizationServiceContextSettings);
    }

    public async Task<Entity> QuerySingleAsync(
        string entityLogicalName,
        Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .QuerySingleAsync(entityLogicalName, queryBuilder, organizationServiceContextSettings);
    }

    public async Task<T> QuerySingleAsync<T>(
        Func<IQueryable<T>, IQueryable<T>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default) where T : Entity
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .QuerySingleAsync(queryBuilder, organizationServiceContextSettings);
    }

    public Entity QuerySingleOrDefault(
        string entityLogicalName,
        Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .QuerySingleOrDefault(entityLogicalName, queryBuilder, organizationServiceContextSettings);
    }

    public T QuerySingleOrDefault<T>(
        Func<IQueryable<T>, IQueryable<T>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default) where T : Entity
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .QuerySingleOrDefault(queryBuilder, organizationServiceContextSettings);
    }

    public async Task<T> QuerySingleOrDefaultAsync<T>(
                                                Func<IQueryable<T>, IQueryable<T>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default) where T : Entity
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .QuerySingleOrDefaultAsync(queryBuilder, organizationServiceContextSettings);
    }

    public async Task<Entity> QuerySingleOrDefaultAsync(
        string entityLogicalName,
        Func<IQueryable<Entity>, IQueryable<Entity>> queryBuilder,
        OrganizationServiceContextSettings organizationServiceContextSettings = default)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .QuerySingleOrDefaultAsync(entityLogicalName, queryBuilder, organizationServiceContextSettings);
    }

    public Entity RefreshRecord(Entity record)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .RefreshRecord(record);
    }

    public async Task<Entity> RefreshRecordAsync(Entity record)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .RefreshRecordAsync(record);
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .Retrieve(entityName, id, columnSet);
    }

    public async Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .RetrieveAsync(entityName, id, columnSet);
    }

    public Entity[] RetrieveMultiple(QueryExpression queryExpression)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .RetrieveMultiple(queryExpression);
    }

    public async Task<Entity[]> RetrieveMultipleAsync(QueryExpression queryExpression)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .RetrieveMultipleAsync(queryExpression);
    }

    public Guid UpdateRecord(Entity record, RequestSettings requestSettings)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .UpdateRecord(record, requestSettings);
    }

    public async Task<Guid> UpdateRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .UpdateRecordAsync(record, requestSettings);
    }

    public EntityReference UpsertRecord(Entity record, RequestSettings requestSettings)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .UpsertRecord(record, requestSettings);
    }

    public async Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .UpsertRecordAsync(record, requestSettings);
    }

    [SuppressMessage("Minor Code Smell",
        "S3267:Loops should be simplified with \"LINQ\" expressions",
        Justification = "No locking in LINQ")]
    protected ConnectionLease GetConnection()
    {
        while (true)
        {
            foreach (IConnection connection in _connections)
            {
                if (connection.TryLock())
                {
                    return new ConnectionLease(connection);
                }
            }

            Thread
               .Sleep(_sleepTimeForConnectionGetter);
        }
    }

    [SuppressMessage("Minor Code Smell",
        "S3267:Loops should be simplified with \"LINQ\" expressions",
        Justification = "No locking in LINQ")]
    protected async Task<ConnectionLease> GetConnectionAsync()
    {
        return await Task.Run(async () =>
        {
            while (true)
            {
                foreach (IConnection connection in _connections)
                {
                    if (connection.TryLock())
                    {
                        return new ConnectionLease(connection);
                    }
                }

                await Task
                   .Delay(_sleepTimeForConnectionGetter);
            }
        });
    }
}