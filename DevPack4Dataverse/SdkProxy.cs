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

using System.Buffers;
using System.Collections.Concurrent;
using System.Security;
using Ardalis.GuardClauses;
using DevPack4Dataverse.ExecuteMultiple;
using DevPack4Dataverse.ExecuteTransaction;
using DevPack4Dataverse.Interfaces;
using DevPack4Dataverse.Models;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse;

/// <summary>
/// Manages a pool of Dataverse connections and executes operations against them.
/// </summary>
/// <remarks>
/// Connections are created in the background. Use <see cref="WaitForConnections"/> or
/// <see cref="WaitForConnectionsAsync"/> before operations that require a minimum pool size.
/// </remarks>
public sealed class SdkProxy : IDataverseConnectionLayer, IDisposable
{
    private readonly bool _applyConnectionOptimalization;
    private readonly RepeatedTask _connectionCreator;
    private readonly ConcurrentBag<IConnectionCreator> _connectionCreators;
    private readonly ConcurrentBag<IConnection> _connections = new();
    private int _disposed;
    private readonly ILogger _logger;
    private readonly TimeSpan _sleepTimeForConnectionCreator = TimeSpan.FromMilliseconds(100);
    private readonly TimeSpan _sleepTimeForConnectionGetter = TimeSpan.FromMilliseconds(10);

    public SdkProxy(
        ILogger logger,
        bool applyConnectionOptimalization = true,
        params IConnectionCreator[] connectionCreators
    )
    {
        using EntryExitLogger logGuard = new(logger);

        _logger = Guard.Against.Null(logger);
        _applyConnectionOptimalization = applyConnectionOptimalization;
        _connectionCreators = new ConcurrentBag<IConnectionCreator>(connectionCreators);
        _connectionCreator = new RepeatedTask(
            _sleepTimeForConnectionCreator,
            () =>
            {
                IConnectionCreator connectionToCreate = _connectionCreators.FirstOrDefault(
                    p => !p.IsCreated && !p.IsError
                );
                if (connectionToCreate == null)
                {
                    return;
                }
                IConnection createdConnection = connectionToCreate.Create(_logger);
                _connections.Add(createdConnection);
            },
            _logger
        );
        _connectionCreator.Start();
    }

    /// <summary>
    /// Number of successfully created connections.
    /// </summary>
    public int ConnectionCount
    {
        get
        {
            return _connections.Count;
        }
    }

    /// <summary>
    /// Blocks until at least <paramref name="count"/> connections have been created.
    /// </summary>
    /// <param name="count">Number of connections to wait for. Must be non-negative.</param>
    /// <exception cref="ArgumentException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">
    /// Fewer than <paramref name="count"/> connection creators are configured, or the remaining
    /// creators have failed.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The proxy has been disposed.</exception>
    /// <remarks>
    /// The method waits while connection creators are still working. Use
    /// <see cref="WaitForConnectionsAsync"/> with a cancellation token for a bounded wait.
    /// A zero count returns immediately.
    /// </remarks>
    public void WaitForConnections(int count)
    {
        Guard.Against.Negative(count, nameof(count));

        if (count == 0)
        {
            return;
        }

        while (true)
        {
            ThrowIfDisposed();
            if (_connections.Count >= count)
            {
                return;
            }

            EnsureConnectionsCanBeCreated(count);
            Thread.Sleep(_sleepTimeForConnectionGetter);
        }
    }

    /// <summary>
    /// Asynchronously waits until at least <paramref name="count"/> connections have been created.
    /// </summary>
    /// <param name="count">Number of connections to wait for. Must be non-negative.</param>
    /// <param name="cancellationToken">Token used to stop waiting.</param>
    /// <exception cref="ArgumentException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">
    /// Fewer than <paramref name="count"/> connection creators are configured, or the remaining
    /// creators have failed.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The proxy has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was requested.</exception>
    /// <remarks>
    /// A zero count returns immediately. Use a cancellation token for a bounded wait.
    /// </remarks>
    public async Task WaitForConnectionsAsync(
        int count,
        CancellationToken cancellationToken = default
    )
    {
        Guard.Against.Negative(count, nameof(count));

        if (count == 0)
        {
            return;
        }

        while (true)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (_connections.Count >= count)
            {
                return;
            }

            EnsureConnectionsCanBeCreated(count);
            await Task.Delay(_sleepTimeForConnectionGetter, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Adds a connection creator. The background creator picks it up on its next cycle.
    /// </summary>
    /// <param name="connectionCreator">Creator to add. Cannot be null.</param>
    public void AddNewConnection(IConnectionCreator connectionCreator)
    {

        Guard.Against.Null(connectionCreator);

        _connectionCreators.Add(connectionCreator);
    }

    private void EnsureConnectionsCanBeCreated(int count)
    {
        int possibleConnections = _connectionCreators.Count(creator => !creator.IsError);
        if (possibleConnections < count)
        {
            throw new InvalidOperationException(
                $"Cannot wait for {count} connection(s): only {possibleConnections} connection creator(s) are available and not failed."
            );
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(SdkProxy));
        }
    }

    public Guid CreateRecord(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease.Connection.CreateRecord(record, requestSettings);
    }

    public async Task<Guid> CreateRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync().ConfigureAwait(false);

        return await connectionLease.Connection.CreateRecordAsync(record, requestSettings).ConfigureAwait(false);
    }

    public void DeleteRecord(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = GetConnection();

        connectionLease.Connection.DeleteRecord(logicalName, id, requestSettings);
    }

    public void DeleteRecord(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = GetConnection();

        connectionLease.Connection.DeleteRecord(entityReference, requestSettings);
    }

    public async Task DeleteRecordAsync(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync().ConfigureAwait(false);

        await connectionLease.Connection.DeleteRecordAsync(logicalName, id, requestSettings).ConfigureAwait(false);
    }

    public async Task DeleteRecordAsync(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync().ConfigureAwait(false);

        await connectionLease.Connection.DeleteRecordAsync(entityReference, requestSettings).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops background connection creation and disposes connection clients.
    /// Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously stops background connection creation and disposes connection clients.
    /// Safe to call multiple times.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _connectionCreator.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            foreach (IConnection connection in _connections)
            {
                try
                {
                    connection.PureServiceClient?.Dispose();
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Could not dispose a Dataverse connection while stopping the proxy."
                    );
                }
            }
        }
    }

    public T Execute<T>(OrganizationRequest request, RequestSettings requestSettings = null)
        where T : OrganizationResponse
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease.Connection.Execute<T>(request, requestSettings);
    }

    public ExecuteMultipleResponse Execute(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        RequestSettings requestSettings = null
    )
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease.Connection.Execute(executeMultipleRequestBuilder, requestSettings);
    }

    public async Task<ExecuteMultipleResponse> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        RequestSettings requestSettings = null
    )
    {
        using ConnectionLease connectionLease = await GetConnectionAsync().ConfigureAwait(false);

        return await connectionLease.Connection.ExecuteAsync(executeMultipleRequestBuilder, requestSettings).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes all requests built by <paramref name="executeTransactionRequestBuilder"/> in one transaction.
    /// </summary>
    /// <param name="executeTransactionRequestBuilder">Builder containing requests. Cannot be null.</param>
    /// <param name="requestSettings">Optional settings applied to the transaction request.</param>
    /// <returns>Responses in request order when the builder returns responses.</returns>
    /// <remarks>If any request fails, all changes are rolled back by the service.</remarks>
    public ExecuteTransactionResponse Execute(
        ExecuteTransactionRequestBuilder executeTransactionRequestBuilder,
        RequestSettings requestSettings = null
    )
    {
        Guard.Against.Null(executeTransactionRequestBuilder);
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease.Connection.Execute<ExecuteTransactionResponse>(
            executeTransactionRequestBuilder.Request,
            requestSettings
        );
    }

    /// <summary>
    /// Asynchronously executes all requests built by <paramref name="executeTransactionRequestBuilder"/>
    /// in one transaction.
    /// </summary>
    /// <param name="executeTransactionRequestBuilder">Builder containing requests. Cannot be null.</param>
    /// <param name="requestSettings">Optional settings applied to the transaction request.</param>
    /// <returns>Responses in request order when the builder returns responses.</returns>
    /// <remarks>If any request fails, all changes are rolled back by the service.</remarks>
    public async Task<ExecuteTransactionResponse> ExecuteAsync(
        ExecuteTransactionRequestBuilder executeTransactionRequestBuilder,
        RequestSettings requestSettings = null
    )
    {
        Guard.Against.Null(executeTransactionRequestBuilder);
        using ConnectionLease connectionLease = await GetConnectionAsync().ConfigureAwait(false);

        return await connectionLease
            .Connection.ExecuteAsync<ExecuteTransactionResponse>(
                executeTransactionRequestBuilder.Request,
                requestSettings
            )
            .ConfigureAwait(false);
    }

    public async Task<T> ExecuteAsync<T>(OrganizationRequest request, RequestSettings requestSettings = null)
        where T : OrganizationResponse
    {
        using ConnectionLease connectionLease = await GetConnectionAsync().ConfigureAwait(false);

        return await connectionLease.Connection.ExecuteAsync<T>(request, requestSettings).ConfigureAwait(false);
    }

    public ConnectionLease GetConnection()
    {

        Guard.Against.InvalidInput(
            _connectionCreators,
            nameof(_connectionCreators),
            p => !p.IsEmpty,
            "Please add at least one connection."
        );

        while (true)
        {
            ThrowIfDisposed();
            int connectionCount = _connections.Count;
            WeightedConnection[] buffer = ArrayPool<WeightedConnection>.Shared.Rent(
                Math.Max(1, connectionCount)
            );
            int filled = 0;
            try
            {
                foreach (IConnection connection in _connections)
                {
                    AddWeightedConnection(ref buffer, ref filled, connection);
                }

                Array.Sort(buffer, 0, filled, WeightedConnectionComparer.Instance);
                for (int index = 0; index < filled; index++)
                {
                    IConnection connection = buffer[index].Connection;
                    if (connection.TryLock())
                    {
                        if (_applyConnectionOptimalization)
                        {
                            connection.ApplyConnectionOptimalization();
                        }

                        return new ConnectionLease(connection);
                    }
                }
            }
            finally
            {
                Array.Clear(buffer, 0, filled);
                ArrayPool<WeightedConnection>.Shared.Return(buffer);
            }

            if (_connections.IsEmpty)
            {
                EnsureConnectionsCanBeCreated(1);
            }

            Thread.Sleep(_sleepTimeForConnectionGetter);
        }
    }

    public async Task<ConnectionLease> GetConnectionAsync()
    {

        Guard.Against.InvalidInput(
            _connectionCreators,
            nameof(_connectionCreators),
            p => !p.IsEmpty,
            "Please add at least one connection."
        );

        while (true)
        {
            ThrowIfDisposed();
            int connectionCount = _connections.Count;
            WeightedConnection[] buffer = ArrayPool<WeightedConnection>.Shared.Rent(
                Math.Max(1, connectionCount)
            );
            int filled = 0;
            try
            {
                foreach (IConnection connection in _connections)
                {
                    AddWeightedConnection(ref buffer, ref filled, connection);
                }

                Array.Sort(buffer, 0, filled, WeightedConnectionComparer.Instance);
                for (int index = 0; index < filled; index++)
                {
                    IConnection connection = buffer[index].Connection;
                    if (await connection.TryLockAsync().ConfigureAwait(false))
                    {
                        if (_applyConnectionOptimalization)
                        {
                            connection.ApplyConnectionOptimalization();
                        }

                        return new ConnectionLease(connection);
                    }
                }
            }
            finally
            {
                Array.Clear(buffer, 0, filled);
                ArrayPool<WeightedConnection>.Shared.Return(buffer);
            }

            if (_connections.IsEmpty)
            {
                EnsureConnectionsCanBeCreated(1);
            }

            await Task.Delay(_sleepTimeForConnectionGetter).ConfigureAwait(false);
        }
    }

    public Entity RefreshRecord(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease.Connection.RefreshRecord(record, requestSettings);
    }

    public T RefreshRecord<T>(T record, RequestSettings requestSettings = null) where T : Entity
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease.Connection.RefreshRecord(record, requestSettings);
    }

    public async Task<Entity> RefreshRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync().ConfigureAwait(false);

        return await connectionLease.Connection.RefreshRecordAsync(record, requestSettings).ConfigureAwait(false);
    }

    public async Task<T> RefreshRecordAsync<T>(T record, RequestSettings requestSettings = null) where T : Entity
    {
        using ConnectionLease connectionLease = await GetConnectionAsync().ConfigureAwait(false);

        return await connectionLease.Connection.RefreshRecordAsync(record, requestSettings).ConfigureAwait(false);
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease.Connection.Retrieve(entityName, id, columnSet, requestSettings);
    }

    public T Retrieve<T>(string entityName, Guid id, ColumnSet columnSet, RequestSettings requestSettings = null)
        where T : Entity
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease.Connection.Retrieve<T>(entityName, id, columnSet, requestSettings);
    }

    public async Task<Entity> RetrieveAsync(
        string entityName,
        Guid id,
        ColumnSet columnSet,
        RequestSettings requestSettings = null
    )
    {
        using ConnectionLease connectionLease = await GetConnectionAsync().ConfigureAwait(false);

        return await connectionLease.Connection.RetrieveAsync(entityName, id, columnSet, requestSettings).ConfigureAwait(false);
    }

    public async Task<T> RetrieveAsync<T>(
        string entityName,
        Guid id,
        ColumnSet columnSet,
        RequestSettings requestSettings = null
    ) where T : Entity
    {
        using ConnectionLease connectionLease = await GetConnectionAsync().ConfigureAwait(false);

        return await connectionLease.Connection.RetrieveAsync<T>(entityName, id, columnSet, requestSettings).ConfigureAwait(false);
    }

    public Entity[] RetrieveMultiple(QueryExpression queryExpression, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease.Connection.RetrieveMultiple(queryExpression, requestSettings);
    }

    public T[] RetrieveMultiple<T>(QueryExpression queryExpression, RequestSettings requestSettings = null)
        where T : Entity
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease.Connection.RetrieveMultiple<T>(queryExpression, requestSettings);
    }

    public async Task<Entity[]> RetrieveMultipleAsync(
        QueryExpression queryExpression,
        RequestSettings requestSettings = null
    )
    {
        using ConnectionLease connectionLease = await GetConnectionAsync().ConfigureAwait(false);

        return await connectionLease.Connection.RetrieveMultipleAsync(queryExpression, requestSettings).ConfigureAwait(false);
    }

    public async Task<T[]> RetrieveMultipleAsync<T>(
        QueryExpression queryExpression,
        RequestSettings requestSettings = null
    ) where T : Entity
    {
        using ConnectionLease connectionLease = await GetConnectionAsync().ConfigureAwait(false);

        return await connectionLease.Connection.RetrieveMultipleAsync<T>(queryExpression, requestSettings).ConfigureAwait(false);
    }

    public void UpdateRecord(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = GetConnection();

        connectionLease.Connection.UpdateRecord(record, requestSettings);
    }

    public async Task UpdateRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync().ConfigureAwait(false);

        await connectionLease.Connection.UpdateRecordAsync(record, requestSettings).ConfigureAwait(false);
    }

    public EntityReference UpsertRecord(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = GetConnection();

        return connectionLease.Connection.UpsertRecord(record, requestSettings);
    }

    public async Task<EntityReference> UpsertRecordAsync(Entity record, RequestSettings requestSettings = null)
    {
        using ConnectionLease connectionLease = await GetConnectionAsync().ConfigureAwait(false);

        return await connectionLease.Connection.UpsertRecordAsync(record, requestSettings).ConfigureAwait(false);
    }

    private readonly struct WeightedConnection(IConnection connection, ulong weight)
    {
        public IConnection Connection { get; } = connection;
        public ulong Weight { get; } = weight;
    }

    private static void AddWeightedConnection(
        ref WeightedConnection[] buffer,
        ref int filled,
        IConnection connection
    )
    {
        if (filled == buffer.Length)
        {
            WeightedConnection[] larger = ArrayPool<WeightedConnection>.Shared.Rent(
                Math.Max(1, buffer.Length * 2)
            );
            Array.Copy(buffer, larger, filled);
            Array.Clear(buffer, 0, filled);
            ArrayPool<WeightedConnection>.Shared.Return(buffer);
            buffer = larger;
        }

        buffer[filled++] = new WeightedConnection(
            connection,
            connection.GetConnectionWeight()
        );
    }

    private sealed class WeightedConnectionComparer : IComparer<WeightedConnection>
    {
        public static WeightedConnectionComparer Instance { get; } = new();

        public int Compare(WeightedConnection x, WeightedConnection y) =>
            x.Weight.CompareTo(y.Weight);
    }
}
