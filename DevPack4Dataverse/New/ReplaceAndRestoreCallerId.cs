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
using Microsoft.PowerPlatform.Dataverse.Client;

namespace DevPack4Dataverse.New;

internal sealed class ReplaceAndRestoreCallerId : IDisposable
{
    private readonly Guid? _oldAadCallerId;
    private readonly Guid? _oldCallerId;
    private bool _disposedValue;

    public ReplaceAndRestoreCallerId(
        ServiceClient serviceClient,
        Guid callerId,
        ReplaceAndRestoreCallerIdType type)
    {
        Guard.IsNotNull(serviceClient);
        ServiceClient = serviceClient;
        _oldCallerId = serviceClient.CallerId;
        _oldAadCallerId = serviceClient.CallerAADObjectId;

        switch (type)
        {
            case ReplaceAndRestoreCallerIdType.CallerId:
                serviceClient.CallerId = callerId;

                break;

            case ReplaceAndRestoreCallerIdType.CallerAADObjectId:
                serviceClient.CallerAADObjectId = callerId;

                break;

            default:
                throw new InvalidProgramException($"{type} is not supported");
        }
    }

    private ServiceClient ServiceClient { get; set; }

    public void Dispose()
    {
        InnerDispose();
        GC.SuppressFinalize(this);
    }

    ~ReplaceAndRestoreCallerId()
    {
        InnerDispose();
    }

    private void InnerDispose()
    {
        if (_disposedValue)
        {
            return;
        }

        ServiceClient.CallerId = _oldCallerId ?? Guid.Empty;
        ServiceClient.CallerAADObjectId = _oldAadCallerId;
        ServiceClient = null;
        _disposedValue = true;
    }
}
