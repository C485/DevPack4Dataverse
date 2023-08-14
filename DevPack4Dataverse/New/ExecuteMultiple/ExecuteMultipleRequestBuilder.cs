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

using System.Collections.Concurrent;
using CommunityToolkit.Diagnostics;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace DevPack4Dataverse.New.ExecuteMultiple;

public sealed class ExecuteMultipleRequestBuilder
{
    private readonly bool _continueOnError;
    private readonly ConcurrentBag<OrganizationRequest> _requests;

    public ExecuteMultipleRequestBuilder(bool continueOnError = true)
    {
        _requests = new ConcurrentBag<OrganizationRequest>();
        _continueOnError = continueOnError;
    }

    public int Count => _requests.Count;

    public ExecuteMultipleRequest Build()
    {
        OrganizationRequestCollection organizationRequestCollection = new();
        organizationRequestCollection.AddRange(_requests);

        return new ExecuteMultipleRequest
        {
            Settings = new ExecuteMultipleSettings
            {
                ContinueOnError = _continueOnError,
                ReturnResponses = true
            },
            Requests = organizationRequestCollection
        };
    }

    public void AddRequest<T>(RequestBuilder<T> request) where T : OrganizationRequest
    {
        Guard.IsNotNull(request);

        _requests.Add(request.Build());
    }

    public void AddRequest(OrganizationRequest request)
    {
        Guard.IsNotNull(request);

        _requests.Add(request);
    }
}
