﻿/*
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
using C485.DataverseClientProxy.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace C485.DataverseClientProxy.Creators;

public class ConnectionStringConnectionCreator : IConnectionCreator
{
    private const string ConnectionStringInvalidError =
        "Unable to append RequireNewInstance because other option was provided. 'RequireNewInstance=True' is requred to be properly detected.";

    private readonly string _connectionString;
    private readonly ILogger _logger;
    private bool _isCreated;
    private bool _isError;

    public ConnectionStringConnectionCreator(string connectionString, ILogger logger)
    {
        _connectionString = Guard
           .Against
           .NullOrEmpty(connectionString);
        _logger = Guard
            .Against
            .Null(logger);

        if (_connectionString.Contains("RequireNewInstance=True", StringComparison.CurrentCultureIgnoreCase))
        {
            return;
        }

        if (_connectionString.Contains("RequireNewInstance", StringComparison.CurrentCultureIgnoreCase))
        {
            throw new InvalidProgramException(ConnectionStringInvalidError);
        }

        _connectionString = _connectionString.Trim();

        if (_connectionString.Last() == ';')
        {
            _connectionString += "RequireNewInstance=True";

            return;
        }

        _connectionString += ";RequireNewInstance=True";
    }

    bool IConnectionCreator.IsCreated => _isCreated;

    bool IConnectionCreator.IsValid => _isCreated && !_isError;

    public IConnection Create()
    {
        try
        {
            ServiceClient crmServiceClient = new(_connectionString, _logger);
            Guard
               .Against
               .NullOrInvalidInput(crmServiceClient,
                    nameof(crmServiceClient),
                    p => p.IsReady,
                    $"{nameof(ClientSecretConnectionCreator)} - failed to make connection to connection string, LatestError: {crmServiceClient.LastError}");

            Connection connection = new(crmServiceClient, _logger);
            bool isConnectionValid = connection.Test();
            if (!isConnectionValid)
            {
                throw new InvalidProgramException("Test on connection failed.");
            }

            _isCreated = true;
            return connection;
        }
        catch (Exception)
        {
            _isError = true;
            throw;
        }
    }
}