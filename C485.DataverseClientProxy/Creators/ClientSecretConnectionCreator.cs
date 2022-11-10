using Ardalis.GuardClauses;
using C485.DataverseClientProxy.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using System.Security;

namespace C485.DataverseClientProxy.Creators;

public class ClientSecretConnectionCreator : IConnectionCreator
{
    private readonly string _appId;
    private readonly string _crmUrl;
    private readonly ILogger _logger;
    private readonly SecureString _secret;
    private bool _isCreated;
    private bool _isError;

    public ClientSecretConnectionCreator(string crmUrl, string appId, SecureString secret, ILogger logger)
    {
        _crmUrl = Guard
           .Against
           .InvalidInput(crmUrl,
                nameof(crmUrl),
                p => Uri.IsWellFormedUriString(p, UriKind.Absolute),
                $"{nameof(crmUrl)} - is null or not valid url.");

        _appId = Guard
           .Against
           .NullOrEmpty(appId);

        _secret = Guard
           .Against
           .NullOrInvalidInput(secret, nameof(secret), p => p.Length > 0);

        _secret
           .MakeReadOnly();
        _logger = Guard
            .Against
            .Null(logger);
    }

    bool IConnectionCreator.IsCreated => _isCreated;

    bool IConnectionCreator.IsValid => _isCreated && !_isError;

    public IConnection Create()
    {
        try
        {
            ServiceClient crmServiceClient = new(new Uri(_crmUrl),
                _appId,
                _secret,
                true,
                _logger);

            Guard
               .Against
               .NullOrInvalidInput(crmServiceClient,
                    nameof(crmServiceClient),
                    p => p.IsReady,
                    $"{nameof(ClientSecretConnectionCreator)} - failed to make connection to Url: {_crmUrl} as AppId: {_appId}, LatestError: {crmServiceClient.LastError}");
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