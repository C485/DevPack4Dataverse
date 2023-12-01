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
    private readonly Guid? _guidToRestore;
    private readonly ReplaceAndRestoreCallerIdType _type;
    private bool _disposedValue;

    public ReplaceAndRestoreCallerId(
        IOrganizationService organizationService,
        RequestImpersonateSettings requestImpersonateSettings
    )
    {
        _type = Guard.Against.Null(requestImpersonateSettings).ImpersonateType;
        ServiceClient? serviceClientInstance = organizationService as ServiceClient;
        ServiceClient = new WeakReference<ServiceClient>(
            Guard.Against.Null(serviceClientInstance, message: $"Only {nameof(ServiceClient)} instance is supported.")
        );

        switch (requestImpersonateSettings.ImpersonateType)
        {
            case ReplaceAndRestoreCallerIdType.CallerId:
                _guidToRestore = serviceClientInstance.CallerId;
                serviceClientInstance.CallerId = requestImpersonateSettings.ImpersonateId;

                break;

            case ReplaceAndRestoreCallerIdType.CallerAADObjectId:
                _guidToRestore = serviceClientInstance.CallerAADObjectId;
                serviceClientInstance.CallerAADObjectId = requestImpersonateSettings.ImpersonateId;

                break;

            default:
                throw new InvalidProgramException($"{_type} is not supported");
        }
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

        if (ServiceClient.TryGetTarget(out ServiceClient? serviceClient))
        {
            switch (_type)
            {
                case ReplaceAndRestoreCallerIdType.CallerId:
                    serviceClient.CallerId = _guidToRestore ?? Guid.Empty;

                    break;

                case ReplaceAndRestoreCallerIdType.CallerAADObjectId:
                    serviceClient.CallerAADObjectId = _guidToRestore;

                    break;

                default:
                    throw new InvalidProgramException($"{_type} is not supported");
            }
        }

        _disposedValue = true;
    }
}
