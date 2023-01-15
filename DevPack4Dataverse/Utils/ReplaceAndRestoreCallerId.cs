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

internal sealed class ReplaceAndRestoreCallerId : IDisposable
{
    private readonly Guid? oldAADCallerId;
    private readonly Guid? oldCallerId;
    private bool _disposedValue;

    public ReplaceAndRestoreCallerId(
        ServiceClient serviceClient,
        ILogger logger,
        Guid? callerId = null,
        Guid? aadCallerId = null
    )
    {
        using EntryExitLogger logGuard = new(logger);
        ServiceClient = Guard.Against.Null(serviceClient);
        oldCallerId = serviceClient.CallerId;
        oldAADCallerId = serviceClient.CallerAADObjectId;
        serviceClient.CallerId = callerId ?? Guid.Empty;
        serviceClient.CallerAADObjectId = aadCallerId;
    }

    public ReplaceAndRestoreCallerId(
        ServiceClient serviceClient,
        ILogger logger,
        RequestImpersonateSettings requestSettings = null
    )
    {
        using EntryExitLogger logGuard = new(logger);
        ServiceClient = Guard.Against.Null(serviceClient);
        oldCallerId = serviceClient.CallerId;
        oldAADCallerId = serviceClient.CallerAADObjectId;
        serviceClient.CallerAADObjectId = requestSettings?.ImpersonateAsUserByAADId;
        serviceClient.CallerId = requestSettings?.ImpersonateAsUserByDataverseId ?? Guid.Empty;
    }

    ~ReplaceAndRestoreCallerId()
    {
        InnerDispose();
    }

    private ServiceClient ServiceClient { get; set; }

    public void Dispose()
    {
        InnerDispose();
        GC.SuppressFinalize(this);
    }

    private void InnerDispose()
    {
        if (_disposedValue)
        {
            return;
        }

        ServiceClient.CallerId = oldCallerId ?? Guid.Empty;
        ServiceClient.CallerAADObjectId = oldAADCallerId;
        ServiceClient = null;
        _disposedValue = true;
    }
}
