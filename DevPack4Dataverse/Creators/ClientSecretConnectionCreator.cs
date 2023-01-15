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
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using System.Security;

namespace DevPack4Dataverse.Creators;

public class ClientSecretConnectionCreator : IConnectionCreator
{
    private readonly string _appId;
    private readonly string _crmUrl;
    private readonly int _maximumConcurrentlyUsage;
    private readonly SecureString _secret;
    private bool _isCreated;
    private bool _isError;

    public ClientSecretConnectionCreator(
        string crmUrl,
        string appId,
        SecureString secret,
        int maximumConcurrentlyUsage = 1
    )
    {
        _crmUrl = Guard.Against.InvalidInput(
            crmUrl,
            nameof(crmUrl),
            p => Uri.IsWellFormedUriString(p, UriKind.Absolute),
            $"{nameof(crmUrl)} - is null or not valid URL."
        );

        _appId = Guard.Against.NullOrEmpty(appId);

        _secret = Guard.Against.NullOrInvalidInput(secret, nameof(secret), p => p.Length > 0);

        _secret.MakeReadOnly();
        _maximumConcurrentlyUsage = Guard.Against.NegativeOrZero(maximumConcurrentlyUsage);
    }

    public bool IsCreated => _isCreated;
    public bool IsError => _isError;
    public bool IsValid => _isCreated && !_isError;

    public IConnection Create(ILogger logger)
    {
        using EntryExitLogger logGuard = new(logger);
        Guard.Against.Null(logger);

        try
        {
            ServiceClient crmServiceClient = new(new Uri(_crmUrl), _appId, _secret, true, logger);

            Guard.Against.NullOrInvalidInput(
                crmServiceClient,
                nameof(crmServiceClient),
                p => p.IsReady,
                $"{nameof(ClientSecretConnectionCreator)} - failed to make connection to URL: {_crmUrl} as AppId: {_appId}, LatestError: {crmServiceClient.LastError}"
            );
            Connection connection = new(crmServiceClient, logger, _maximumConcurrentlyUsage);

            bool isConnectionValid = connection.Test();
            if (!isConnectionValid)
            {
                throw new InvalidProgramException("Test on connection failed.");
            }

            _isCreated = true;
            return connection;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unexpected error in {NameOfClass}", nameof(ClientSecretConnectionCreator));
            _isError = true;
            throw;
        }
    }
}
