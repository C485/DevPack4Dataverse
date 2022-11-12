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
using DevPack4Dataverse.Interfaces;
using DevPack4Dataverse.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security;

namespace DevPack4Dataverse;

public sealed class ConnectionManager : IDataverseConnectionLayer
{
    private readonly ConcurrentBag<IConnection> _connections;
    private readonly ConcurrentBag<IConnectionCreator> _connectionsCreators;

    private readonly TimeSpan _sleepTimeForConnectionGetter = TimeSpan.FromMilliseconds(10);

    public ConnectionManager(params IConnectionCreator[] connectionCreators)
    {
        _connections = new ConcurrentBag<IConnection>();
        _connectionsCreators = new ConcurrentBag<IConnectionCreator>(connectionCreators);
    }

    public int ConnectionCount => _connections.Count;

    public static SecureString StringToSecureString(string plainString)
    {
        if (plainString == null)
        {
            return null;
        }

        SecureString secureString = new();
        foreach (char c in plainString)
        {
            secureString.AppendChar(c);
        }
        secureString.MakeReadOnly();
        return secureString;
    }

    public void AddNewConnection(IConnectionCreator connectionCreator)
    {
        Guard
           .Against
           .Null(connectionCreator);

        _connectionsCreators
           .Add(connectionCreator);
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

    public T Execute<T>(OrganizationRequest request, RequestSettings requestSettings = null) where T : OrganizationResponse
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .Execute<T>(request, requestSettings);
    }

    public ExecuteMultipleResponse Execute(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .Execute(executeMultipleRequestBuilder, requestSettings);
    }

    public async Task<ExecuteMultipleResponse> ExecuteAsync(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .ExecuteAsync(executeMultipleRequestBuilder, requestSettings);
    }

    public async Task<T> ExecuteAsync<T>(OrganizationRequest request, RequestSettings requestSettings = null) where T : OrganizationResponse
    {
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
        Guard.Against.InvalidInput(_connections, nameof(_connections), p => p.IsEmpty, "Please add at least one connection.");
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
    public async Task<ConnectionLease> GetConnectionAsync()
    {
        Guard.Against.InvalidInput(_connections, nameof(_connections), p => p.IsEmpty, "Please add at least one connection.");

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

    public Guid UpdateRecord(Entity record, RequestSettings requestSettings = null)
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

    public EntityReference UpsertRecord(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease
           .Connection
           .UpsertRecord(record, requestSettings);
    }

    public async Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync();

        return await connectionLease
           .Connection
           .UpsertRecordAsync(record, requestSettings);
    }
}