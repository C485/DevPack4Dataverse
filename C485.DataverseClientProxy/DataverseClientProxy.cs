using Ardalis.GuardClauses;
using C485.DataverseClientProxy.Interfaces;
using C485.DataverseClientProxy.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace C485.DataverseClientProxy
{
    public class DataverseClientProxy : IConnectionManager
    {
        private readonly ConcurrentBag<IConnection> _connections;
        private readonly TimeSpan _sleepTimeForConnectionGetter = TimeSpan.FromMilliseconds(10);

        public DataverseClientProxy() => _connections = new ConcurrentBag<IConnection>();

        public async Task AddNewConnection(IConnectionCreator connectionCreator)
        {
            Guard
                .Against
                .Null(connectionCreator, nameof(connectionCreator));
            IConnection newConnection = connectionCreator
                .Create();
            Guard
                .Against
                .Null(newConnection, nameof(newConnection));
            bool isConnectionValid = await newConnection
                .TestAsync();
            if (isConnectionValid)
            {
                _connections
                    .Add(newConnection);
                return;
            }
            throw new InvalidProgramException("Connection is not valid.");
        }

        public Guid CreateRecord(Entity record, RequestSettings requestSettings)
        {
            IConnection connection = GetConnection();
            try
            {
                return connection
                    .CreateRecord(record, requestSettings);
            }
            finally
            {
                connection
                    .ReleaseLock();
            }
        }

        public async Task<Guid> CreateRecordAsync(Entity record, RequestSettings requestSettings)
        {
            IConnection connection = await GetConnectionAsync();
            try
            {
                return await connection
                    .CreateRecordAsync(record, requestSettings);
            }
            finally
            {
                connection
                    .ReleaseLock();
            }
        }

        public void DeleteRecord(string logicalName, Guid id, RequestSettings requestSettings)
        {
            IConnection connection = GetConnection();
            try
            {
                connection
                    .DeleteRecord(logicalName, id, requestSettings);
            }
            finally
            {
                connection
                    .ReleaseLock();
            }
        }

        public void DeleteRecord(EntityReference entityReference, RequestSettings requestSettings)
        {
            IConnection connection = GetConnection();
            try
            {
                connection
                    .DeleteRecord(entityReference, requestSettings);
            }
            finally
            {
                connection
                    .ReleaseLock();
            }
        }

        public async Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings requestSettings)
        {
            IConnection connection = await GetConnectionAsync();
            try
            {
                await connection
                    .DeleteRecordAsync(logicalName, id, requestSettings);
            }
            finally
            {
                connection
                    .ReleaseLock();
            }
        }

        public async Task DeleteRecordAsync(EntityReference entityReference, RequestSettings requestSettings)
        {
            IConnection connection = await GetConnectionAsync();
            try
            {
                await connection
                    .DeleteRecordAsync(entityReference, requestSettings);
            }
            finally
            {
                connection
                    .ReleaseLock();
            }
        }

        public OrganizationResponse Execute(OrganizationRequest request, RequestSettings requestSettings)
        {
            IConnection connection = GetConnection();
            try
            {
                return connection
                    .Execute(request, requestSettings);
            }
            finally
            {
                connection
                    .ReleaseLock();
            }
        }

        public OrganizationResponse Execute(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder)
        {
            IConnection connection = GetConnection();
            try
            {
                return connection
                    .Execute(executeMultipleRequestBuilder);
            }
            finally
            {
                connection
                    .ReleaseLock();
            }
        }

        public async Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request, RequestSettings requestSettings)
        {
            IConnection connection = await GetConnectionAsync();
            try
            {
                return await connection
                    .ExecuteAsync(request, requestSettings);
            }
            finally
            {
                connection
                    .ReleaseLock();
            }
        }

        public async Task<OrganizationResponse> ExecuteAsync(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder)
        {
            IConnection connection = await GetConnectionAsync();
            try
            {
                return await connection
                    .ExecuteAsync(executeMultipleRequestBuilder);
            }
            finally
            {
                connection
                    .ReleaseLock();
            }
        }

        public ChunksStatistics ExecuteMultipleAsChunks(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, ExecuteMultipleRequestSettings executeMultipleRequestSettings)
        {
            Guard
                .Against
                .Null(executeMultipleRequestSettings, nameof(executeMultipleRequestSettings));
            int threadsCount = executeMultipleRequestSettings.MaxDegreeOfParallelism <= 0
                ? _connections.Count : executeMultipleRequestSettings.MaxDegreeOfParallelism;
            ChunksStatistics chunksStatistics = new()
            {
                Stopwatch = Stopwatch.StartNew(),
                ThreadsUsed = threadsCount
            };
            int progress = 0;
            CancellationTokenSource cts = new();
            Thread statusReportThread = new(new ParameterizedThreadStart((object token) =>
            {
                CancellationToken ct = (CancellationToken)token;
                while (!ct.IsCancellationRequested)
                {
                    Thread
                        .Sleep(executeMultipleRequestSettings.ReportProgressInterval);
                    executeMultipleRequestSettings.ReportProgress(Thread.VolatileRead(ref progress),
                        executeMultipleRequestBuilder.Count);
                }
            }));
            statusReportThread.Start(cts.Token);

            OrganizationRequest[][] allRequestChunks = executeMultipleRequestBuilder
                .RequestWithResults
                .Requests
                .Select((s, i) => new { Value = s, Index = i })
                .GroupBy(p => p.Index / executeMultipleRequestSettings.RequestSize)
                .Select(p => p
                    .Select(x => x.Value)
                    .ToArray()
                )
                .ToArray();

            Parallel.ForEach(allRequestChunks,
                new ParallelOptions { MaxDegreeOfParallelism = threadsCount },
                packOfRequests =>
                {
                    IConnection connection = GetConnection();
                    try
                    {
                        ExecuteMultipleRequest requestWithResults = new()
                        {
                            Settings = new ExecuteMultipleSettings()
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
                            (ExecuteMultipleResponse)connection
                                .Execute(requestWithResults, new RequestSettings
                                {
                                    ImpersonateAsUserByDataverseId = executeMultipleRequestBuilder.ImpersonateAsUserById,
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
                    finally
                    {
                        connection.ReleaseLock();
                    }

                    Interlocked.Add(ref progress, packOfRequests.Length);
                }
            );
            cts
                .Cancel();
            statusReportThread
                .Join();
            cts
                .Dispose();
            chunksStatistics.Stopwatch.Stop();
            chunksStatistics.RecordsProcessed = executeMultipleRequestBuilder
                .RequestWithResults
                .Requests
                .Count;
            return chunksStatistics;
        }

        public Guid UpdateRecord(Entity record, RequestSettings requestSettings)
        {
            IConnection connection = GetConnection();
            try
            {
                return connection
                    .UpdateRecord(record, requestSettings);
            }
            finally
            {
                connection
                    .ReleaseLock();
            }
        }

        public async Task<Guid> UpdateRecordAsync(Entity record, RequestSettings requestSettings)
        {
            IConnection connection = await GetConnectionAsync();
            try
            {
                return await connection
                    .UpdateRecordAsync(record, requestSettings);
            }
            finally
            {
                connection
                    .ReleaseLock();
            }
        }

        public EntityReference UpsertRecord(Entity record, RequestSettings requestSettings)
        {
            IConnection connection = GetConnection();
            try
            {
                return connection
                    .UpsertRecord(record, requestSettings);
            }
            finally
            {
                connection
                    .ReleaseLock();
            }
        }

        public async Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings)
        {
            IConnection connection = await GetConnectionAsync();
            try
            {
                return await connection
                    .UpsertRecordAsync(record, requestSettings);
            }
            finally
            {
                connection
                    .ReleaseLock();
            }
        }

        private IConnection GetConnection()
        {
            while (true)
            {
                Thread
                    .Sleep(_sleepTimeForConnectionGetter);
                foreach (IConnection connection in _connections)
                {
                    if (connection.TryLock())
                    {
                        return connection;
                    }
                }
            }
        }

        private async Task<IConnection> GetConnectionAsync()
        {
            return await Task.Run(async () =>
            {
                while (true)
                {
                    await Task
                        .Delay(_sleepTimeForConnectionGetter);
                    foreach (IConnection connection in _connections)
                    {
                        if (connection.TryLock())
                        {
                            return connection;
                        }
                    }
                }
            });
        }
    }
}