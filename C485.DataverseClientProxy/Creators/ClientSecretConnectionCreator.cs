using System;
using System.Security;
using Ardalis.GuardClauses;
using C485.DataverseClientProxy.Interfaces;
using Microsoft.Xrm.Tooling.Connector;

namespace C485.DataverseClientProxy.Creators;

public class ClientSecretConnectionCreator : IConnectionCreator
{
	private readonly string _appId;

	private readonly string _crmUrl;

	private readonly SecureString _secret;

	public ClientSecretConnectionCreator(string crmUrl, string appId, SecureString secret)
	{
		_crmUrl = Guard
		   .Against
		   .InvalidInput(crmUrl,
				nameof(crmUrl),
				p => Uri.IsWellFormedUriString(p, UriKind.Absolute),
				$"{nameof(crmUrl)} - is null or not valid url.");

		_appId = Guard
		   .Against
		   .NullOrEmpty(appId, nameof(appId));

		_secret = Guard
		   .Against
		   .NullOrInvalidInput(secret, nameof(secret), p => p.Length > 0);

		_secret
		   .MakeReadOnly();
	}

	public IConnection Create()
	{
		CrmServiceClient crmServiceClient = new(new Uri(_crmUrl),
			_appId,
			_secret,
			true,
			string.Empty);

		Guard
		   .Against
		   .NullOrInvalidInput(crmServiceClient,
				nameof(crmServiceClient),
				p => p.IsReady,
				$"{nameof(ClientSecretConnectionCreator)} - failed to make connection to Url: {_crmUrl} as AppId: {_appId}, LatestError: {crmServiceClient?.LastCrmError}");

		return new Connection(crmServiceClient);
	}
}