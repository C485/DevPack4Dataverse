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
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security;

namespace DevPack4Dataverse;

public sealed class SdkProxy : IDataverseConnectionLayer, IDisposable
{
    private readonly bool _applyConnectionOptimalization;
    private readonly RepeatedTask _connectionCreator;
    private readonly ConcurrentBag<IConnectionCreator> _connectionCreators;
    private readonly ConcurrentBag<IConnection> _connections = new();
    private readonly ILogger _logger;
    private readonly TimeSpan _sleepTimeForConnectionCreator = TimeSpan.FromMilliseconds(100);
    private readonly TimeSpan _sleepTimeForConnectionGetter = TimeSpan.FromMilliseconds(10);

    public SdkProxy(ILogger logger, bool applyConnectionOptimalization = true, params IConnectionCreator[] connectionCreators)
    {
        using EntryExitLogger logGuard = new(logger);

        if (applyConnectionOptimalization)
        {
            OptimalizeConnections();
        }

        _logger = Guard.Against.Null(logger);
        _applyConnectionOptimalization = applyConnectionOptimalization;
        _connectionCreators = new ConcurrentBag<IConnectionCreator>(connectionCreators);
        _connectionCreator = new RepeatedTask(_sleepTimeForConnectionCreator, () =>
        {
            IConnectionCreator connectionToCreate = _connectionCreators
                .FirstOrDefault(p => !p.IsCreated && !p.IsError);
            if (connectionToCreate == null)
            {
                return;
            }
            IConnection createdConnection = connectionToCreate.Create();
            _connections.Add(createdConnection);
        }, _logger);
        _connectionCreator.Start();
    }

    public int ConnectionCount
    {
        get
        {
            using EntryExitLogger logGuard = new(_logger);
            return _connectionCreators.Count;
        }
    }

    public static SecureString StringToSecureString(string plainString)
    {
        if (plainString == null)
        {
            return null;
        }

        SecureString secureString = new();
        foreach (char singleChar in plainString)
        {
            secureString.AppendChar(singleChar);
        }
        secureString.MakeReadOnly();
        return secureString;
    }

    public void AddNewConnection(IConnectionCreator connectionCreator)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .Null(connectionCreator);

        _connectionCreators
           .Add(connectionCreator);
    }

    public Guid CreateRecord(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .CreateRecord(record, requestSettings);
    }

    public async Task<Guid> CreateRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .CreateRecordAsync(record, requestSettings);
    }

    public void DeleteRecord(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = GetConnection();

        connectionLease
           .Connection
           .DeleteRecord(logicalName, id, requestSettings);
    }

    public void DeleteRecord(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = GetConnection();

        connectionLease
           .Connection
           .DeleteRecord(entityReference, requestSettings);
    }

    public async Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = await GetConnectionAsync();

        await connectionLease
           .Connection
           .DeleteRecordAsync(logicalName, id, requestSettings);
    }

    public async Task DeleteRecordAsync(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = await GetConnectionAsync();

        await connectionLease
           .Connection
           .DeleteRecordAsync(entityReference, requestSettings);
    }

    public void Dispose()
    {
        _connectionCreator
            .StopAsync()
            .RunSynchronously();
    }

    public T Execute<T>(OrganizationRequest request, RequestSettings requestSettings = null) where T : OrganizationResponse
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .Execute<T>(request, requestSettings);
    }

    public ExecuteMultipleResponse Execute(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .Execute(executeMultipleRequestBuilder, requestSettings);
    }

    public async Task<ExecuteMultipleResponse> ExecuteAsync(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .ExecuteAsync(executeMultipleRequestBuilder, requestSettings);
    }

    public async Task<T> ExecuteAsync<T>(OrganizationRequest request, RequestSettings requestSettings = null) where T : OrganizationResponse
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .ExecuteAsync<T>(request, requestSettings);
    }

    [SuppressMessage("Minor Code Smell",
                    "S3267:Loops should be simplified with \"LINQ\" expressions",
        Justification = "No locking in LINQ")]
    public ConnectionLease GetConnection()
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.InvalidInput(_connectionCreators, nameof(_connectionCreators), p => !p.IsEmpty, "Please add at least one connection.");

        while (true)
        {
            foreach (IConnection connection in _connections)
            {
                if (connection.TryLock())
                {
                    if (_applyConnectionOptimalization)
                    {
                        connection.ApplyConnectionOptimalization();
                    }
                    return new ConnectionLease(connection);
                }
            }

            Thread
               .Sleep(_sleepTimeForConnectionGetter);
        }
    }

    public async Task<ConnectionLease> GetConnectionAsync()
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.InvalidInput(_connectionCreators, nameof(_connectionCreators), p => !p.IsEmpty, "Please add at least one connection.");

        while (true)
        {
            foreach (IConnection connection in _connections)
            {
                if (await connection.TryLockAsync())
                {
                    if (_applyConnectionOptimalization)
                    {
                        connection.ApplyConnectionOptimalization();
                    }
                    return new ConnectionLease(connection);
                }
            }

            await Task
               .Delay(_sleepTimeForConnectionGetter);
        }
    }

    public Entity RefreshRecord(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .RefreshRecord(record, requestSettings);
    }

    public async Task<Entity> RefreshRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .RefreshRecordAsync(record, requestSettings);
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .Retrieve(entityName, id, columnSet, requestSettings);
    }

    public async Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .RetrieveAsync(entityName, id, columnSet, requestSettings);
    }

    public Entity[] RetrieveMultiple(QueryExpression queryExpression, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .RetrieveMultiple(queryExpression, requestSettings);
    }

    public async Task<Entity[]> RetrieveMultipleAsync(QueryExpression queryExpression, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .RetrieveMultipleAsync(queryExpression, requestSettings);
    }

    public void UpdateRecord(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = GetConnection();

        connectionLease
           .Connection
           .UpdateRecord(record, requestSettings);
    }

    public async Task UpdateRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = await GetConnectionAsync();

        await connectionLease
           .Connection
           .UpdateRecordAsync(record, requestSettings);
    }

    public EntityReference UpsertRecord(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .UpsertRecord(record, requestSettings);
    }

    public async Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .UpsertRecordAsync(record, requestSettings);
    }

    private static void OptimalizeConnections()
    {
        ServicePointManager.DefaultConnectionLimit = 65000;
        ThreadPool.SetMinThreads(100, 100);
        ServicePointManager.Expect100Continue = false;
        ServicePointManager.UseNagleAlgorithm = false;
    }
}