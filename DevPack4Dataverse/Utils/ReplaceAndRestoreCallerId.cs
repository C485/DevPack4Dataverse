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
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.Utils;

internal sealed class ReplaceAndRestoreCallerId : IDisposable
{
    private readonly Guid? oldAADCallerId;
    private readonly Guid? oldCallerId;
    private bool _disposedValue;

    public ReplaceAndRestoreCallerId(
        IOrganizationService organizationService,
        Guid? callerId = null,
        Guid? aadCallerId = null
    )
    {
        ServiceClient? serviceClientInstance = organizationService as ServiceClient;
        ServiceClient = Guard.Against.Null(
            serviceClientInstance,
            message: $"Only {nameof(ServiceClient)} instance is supported."
        );
        oldCallerId = ServiceClient.CallerId;
        oldAADCallerId = ServiceClient.CallerAADObjectId;
        ServiceClient.CallerId = callerId ?? Guid.Empty;
        ServiceClient.CallerAADObjectId = aadCallerId;
    }

    public ReplaceAndRestoreCallerId(
        IOrganizationService organizationService,
        RequestImpersonateSettings? requestSettings = null
    )
    {
        ServiceClient? serviceClientInstance = organizationService as ServiceClient;
        ServiceClient = new WeakReference<ServiceClient>(
            Guard.Against.Null(serviceClientInstance, message: $"Only {nameof(ServiceClient)} instance is supported.")
        );
        oldCallerId = serviceClientInstance.CallerId;
        oldAADCallerId = serviceClientInstance.CallerAADObjectId;
        serviceClientInstance.CallerAADObjectId = requestSettings?.ImpersonateAsUserByAADId;
        serviceClientInstance.CallerId = requestSettings?.ImpersonateAsUserByDataverseId ?? Guid.Empty;
    }

    ~ReplaceAndRestoreCallerId()
    {
        InnerDispose();
    }

    private WeakReference<ServiceClient> ServiceClient { get; set; }

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
