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
using DevPack4Dataverse.Models;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace DevPack4Dataverse.Utils;

internal interface ICallerIdClient
{
    Guid CallerId { get; set; }

    Guid? CallerAADObjectId { get; set; }

    bool SupportsCallerId { get; }
}

internal sealed class ServiceClientCallerIdAdapter(ServiceClient client) : ICallerIdClient
{
    private readonly ServiceClient _client =
        client ?? throw new ArgumentNullException(nameof(client));

    public Guid CallerId
    {
        get => _client.CallerId;
        set => _client.CallerId = value;
    }

    public Guid? CallerAADObjectId
    {
        get => _client.CallerAADObjectId;
        set => _client.CallerAADObjectId = value;
    }

    public bool SupportsCallerId => _client.ActiveAuthenticationType != AuthenticationType.AD;
}

internal sealed class ReplaceAndRestoreCallerId : IDisposable
{
    private readonly Guid? oldAADCallerId;
    private readonly Guid? oldCallerId;
    private bool _disposedValue;

    public ReplaceAndRestoreCallerId(
        ICallerIdClient serviceClient,
        ILogger logger,
        Guid? callerId = null,
        Guid? aadCallerId = null
    )
    {
        ICallerIdClient client = Guard.Against.Null(serviceClient);
        Client = client;
        EnsureCallerIdSupported(client, callerId, aadCallerId);

        oldCallerId = client.CallerId;
        oldAADCallerId = client.CallerAADObjectId;
        client.CallerId = callerId ?? Guid.Empty;
        client.CallerAADObjectId = aadCallerId;
        EnsureApplied(client, callerId ?? Guid.Empty, aadCallerId);
    }

    public ReplaceAndRestoreCallerId(
        ICallerIdClient serviceClient,
        ILogger logger,
        RequestImpersonateSettings? requestSettings = null
    )
    {
        ICallerIdClient client = Guard.Against.Null(serviceClient);
        Client = client;
        Guid? requestedCallerId = requestSettings?.ImpersonateAsUserByDataverseId;
        Guid? requestedAADCallerId = requestSettings?.ImpersonateAsUserByAADId;
        EnsureCallerIdSupported(client, requestedCallerId, requestedAADCallerId);

        oldCallerId = client.CallerId;
        oldAADCallerId = client.CallerAADObjectId;
        client.CallerAADObjectId = requestedAADCallerId;
        client.CallerId = requestedCallerId ?? Guid.Empty;
        EnsureApplied(client, requestedCallerId ?? Guid.Empty, requestedAADCallerId);
    }

    private ICallerIdClient? Client { get; set; }

    public void Dispose()
    {
        InnerDispose();
    }

    private static void EnsureCallerIdSupported(
        ICallerIdClient serviceClient,
        Guid? callerId,
        Guid? aadCallerId
    )
    {
        if (callerId.HasValue || aadCallerId.HasValue)
        {
            if (!serviceClient.SupportsCallerId)
            {
                throw new NotSupportedException(
                    "Impersonation is not supported by this connection type. "
                        + "Use an OAuth, client-secret, or certificate connection."
                );
            }
        }
    }

    private void EnsureApplied(
        ICallerIdClient serviceClient,
        Guid callerId,
        Guid? aadCallerId
    )
    {
        if (callerId == Guid.Empty && !aadCallerId.HasValue)
        {
            return;
        }

        if (serviceClient.CallerId == callerId && serviceClient.CallerAADObjectId == aadCallerId)
        {
            return;
        }

        serviceClient.CallerId = oldCallerId ?? Guid.Empty;
        serviceClient.CallerAADObjectId = oldAADCallerId;
        throw new NotSupportedException(
            "The connection did not accept the requested caller id. "
                + "The request was not executed as the impersonated user."
        );
    }

    private void InnerDispose()
    {
        if (_disposedValue)
        {
            return;
        }

        if (Client != null)
        {
            Client.CallerId = oldCallerId ?? Guid.Empty;
            Client.CallerAADObjectId = oldAADCallerId;
            Client = null;
        }

        _disposedValue = true;
    }
}
