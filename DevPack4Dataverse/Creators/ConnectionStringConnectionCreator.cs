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

using System.Data.Common;
using Ardalis.GuardClauses;
using DevPack4Dataverse.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace DevPack4Dataverse.Creators;

public sealed class ConnectionStringConnectionCreator : IConnectionCreator
{
    private const string ConnectionStringInvalidError =
        "Unable to append RequireNewInstance because other option was provided. 'RequireNewInstance=True' is required to be properly detected.";

    private readonly string _connectionString;
    private readonly int _connectionCount;
    private bool _isCreated;
    private bool _isError;

    public ConnectionStringConnectionCreator(string connectionString, int connectionCount = 1)
    {
        _connectionString = Guard.Against.NullOrWhiteSpace(connectionString);
        _connectionCount = Guard.Against.NegativeOrZero(connectionCount);

        DbConnectionStringBuilder builder = new();
        builder.ConnectionString = _connectionString;
        if (builder.TryGetValue("RequireNewInstance", out object? requireNewInstance))
        {
            if (
                !bool.TryParse(requireNewInstance?.ToString(), out bool required)
                || !required
            )
            {
                throw new InvalidProgramException(ConnectionStringInvalidError);
            }
        }
        else
        {
            builder["RequireNewInstance"] = true;
        }

        _connectionString = builder.ConnectionString;
    }

    public bool IsCreated => _isCreated;
    public bool IsError => _isError;
    public bool IsValid => _isCreated && !_isError;
    public int ConnectionCount => _connectionCount;

    internal string ConnectionString => _connectionString;

    public IReadOnlyList<IConnection> Create(ILogger logger)
    {

        Guard.Against.Null(logger);

        ServiceClient? crmServiceClient = null;
        bool ownershipTransferred = false;
        try
        {
            crmServiceClient = new(_connectionString, logger);
            Guard.Against.NullOrInvalidInput(
                crmServiceClient,
                nameof(crmServiceClient),
                p => p.IsReady,
                $"{nameof(ClientSecretConnectionCreator)} - failed to make connection to connection string, LatestError: {crmServiceClient.LastError}"
            );

            ownershipTransferred = true;
            IReadOnlyList<IConnection> connections = ConnectionFactory.Create(
                crmServiceClient,
                _connectionCount,
                logger
            );

            _isCreated = true;
            return connections;
        }
        catch (Exception e)
        {
            if (!ownershipTransferred)
            {
                crmServiceClient?.Dispose();
            }

            logger.LogError(e, "Unexpected error in {NameOfClass}", nameof(ConnectionStringConnectionCreator));
            _isError = true;
            throw;
        }
    }
}
