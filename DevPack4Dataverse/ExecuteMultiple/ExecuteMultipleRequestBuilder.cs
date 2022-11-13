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
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace DevPack4Dataverse.ExecuteMultiple;

public sealed class ExecuteMultipleRequestBuilder
{
    private readonly ILogger _logger;

    public ExecuteMultipleRequestBuilder(
        ILogger logger,
        bool continueOnError = true)
    {
        using EntryExitLogger logGuard = new(logger);
        _logger = Guard.Against.Null(logger);

        RequestWithResults = new ExecuteMultipleRequest
        {
            Settings = new ExecuteMultipleSettings
            {
                ContinueOnError = continueOnError,
                ReturnResponses = true
            },
            Requests = new OrganizationRequestCollection()
        };
    }

    public int Count => RequestWithResults.Requests.Count;

    public ExecuteMultipleRequest RequestWithResults { get; }

    public void AddCreate(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .NullOrInvalidInput(record, nameof(record), p => p.Id == Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

        CreateRequest request = new()
        {
            Target = record
        };

        AddRequest(request, requestSettings);
    }

    public void AddDelete(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .NullOrInvalidInput(entityReference,
                nameof(entityReference),
                p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

        DeleteRequest request = new()
        {
            Target = entityReference
        };

        AddRequest(request, requestSettings);
    }

    public void AddDelete(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .NullOrEmpty(logicalName);

        Guard
           .Against
           .Default(id);

        AddDelete(new EntityReference(logicalName, id), requestSettings);
    }

    public void AddRequest(OrganizationRequest request, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .Null(request);

        requestSettings?.AddToOrganizationRequest(request);

        RequestWithResults
           .Requests
           .Add(request);
    }

    public void AddUpdate(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .NullOrInvalidInput(record, nameof(record), p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName));

        UpdateRequest request = new()
        {
            Target = record
        };

        AddRequest(request, requestSettings);
    }

    public void AddUpsert(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
            .Against
            .NullOrInvalidInput(record, nameof(record), p => !string.IsNullOrEmpty(p.LogicalName));

        UpsertRequest request = new()
        {
            Target = record
        };

        AddRequest(request, requestSettings);
    }
}