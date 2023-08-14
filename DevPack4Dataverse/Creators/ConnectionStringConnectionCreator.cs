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

using CommunityToolkit.Diagnostics;
using DevPack4Dataverse.New;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace DevPack4Dataverse.Creators;

public class ConnectionStringConnectionCreator : IConnectionCreator
{
    private const string ConnectionStringInvalidError =
        "Unable to append RequireNewInstance because other option was provided. 'RequireNewInstance=True' is required to be properly detected.";

    private readonly string _connectionString;
    private readonly int _maximumConcurrentlyUsage;

    public ConnectionStringConnectionCreator(string connectionString, int maximumConcurrentlyUsage = 1)
    {
        Guard.IsGreaterThan(maximumConcurrentlyUsage, 0);
        _maximumConcurrentlyUsage = maximumConcurrentlyUsage;
        Guard.IsNotNullOrEmpty(connectionString);
        _connectionString = connectionString;

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

    public bool IsCreated { get; private set; }

    public bool IsError { get; private set; }

    public bool IsValid => IsCreated && !IsError;

    public IConnection Create(ILogger logger)
    {
        Guard.Against.Null(logger);

        try
        {
            ServiceClient crmServiceClient = new(_connectionString, logger);
            Guard.Against.NullOrInvalidInput(crmServiceClient,
                nameof(crmServiceClient),
                p => p.IsReady,
                $"{nameof(ClientSecretConnectionCreator)} - failed to make connection to connection string, LatestError: {crmServiceClient.LastError}");

            ConnectionOld connectionOld = new(crmServiceClient, logger, _maximumConcurrentlyUsage);
            bool isConnectionValid = connectionOld.Test();

            if (!isConnectionValid)
            {
                throw new InvalidProgramException("Test on connection failed.");
            }

            IsCreated = true;

            return connectionOld;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unexpected error in {NameOfClass}", nameof(ConnectionStringConnectionCreator));
            IsError = true;

            throw;
        }
    }
}
