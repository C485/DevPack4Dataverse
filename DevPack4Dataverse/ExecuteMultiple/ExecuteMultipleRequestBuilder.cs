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
using Ardalis.GuardClauses;
using DevPack4Dataverse.Models;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace DevPack4Dataverse.ExecuteMultiple;

/// <summary>
/// Thread-safe builder for <see cref="ExecuteMultipleRequest"/> requests.
/// </summary>
/// <remarks>
/// Requests are enumerated in insertion order. Calling <see cref="RequestWithResults"/> creates a snapshot;
/// changes made to this builder afterwards do not affect that snapshot.
/// </remarks>
public sealed class ExecuteMultipleRequestBuilder
{
    private readonly bool _continueOnError;
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<OrganizationRequest> _requests;

    /// <summary>
    /// Creates a new request builder.
    /// </summary>
    /// <param name="logger">Logger used by the builder. Cannot be null.</param>
    /// <param name="continueOnError">Value used by <see cref="RequestWithResults"/> settings.</param>
    public ExecuteMultipleRequestBuilder(ILogger logger, bool continueOnError = true)
    {
        using EntryExitLogger logGuard = new(logger);
        _requests = new();
        _logger = Guard.Against.Null(logger);
        _continueOnError = continueOnError;
    }

    /// <summary>
    /// Number of requests currently in the builder.
    /// </summary>
    public int Count => _requests.Count;

    /// <summary>
    /// Creates a snapshot request with <c>ReturnResponses = true</c> and the configured
    /// <c>ContinueOnError</c> value. Requests are in insertion order.
    /// </summary>
    public ExecuteMultipleRequest RequestWithResults =>
        new()
        {
            Settings = new ExecuteMultipleSettings { ContinueOnError = _continueOnError, ReturnResponses = true },
            Requests = GetOrganizationRequests()
        };

    public void AddCreate(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id == Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        CreateRequest request = new() { Target = record };

        AddRequest(request, requestSettings);
    }

    public void AddDelete(EntityReference entityReference, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.NullOrInvalidInput(
            entityReference,
            nameof(entityReference),
            p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        DeleteRequest request = new() { Target = entityReference };

        AddRequest(request, requestSettings);
    }

    public void AddDelete(string logicalName, Guid id, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        AddDelete(EntityReferenceUtils.CreateEntityReference(id, logicalName, _logger), requestSettings);
    }

    public void AddRequest(OrganizationRequest request, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(request);

        requestSettings?.AddToOrganizationRequest(request, _logger);

        _requests.Enqueue(request);
    }

    public void AddUpdate(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.NullOrInvalidInput(
            record,
            nameof(record),
            p => p.Id != Guid.Empty && !string.IsNullOrEmpty(p.LogicalName)
        );

        UpdateRequest request = new() { Target = record };

        AddRequest(request, requestSettings);
    }

    public void AddUpsert(Entity record, RequestSettings requestSettings = null)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.NullOrInvalidInput(record, nameof(record), p => !string.IsNullOrEmpty(p.LogicalName));

        UpsertRequest request = new() { Target = record };

        AddRequest(request, requestSettings);
    }

    private OrganizationRequestCollection GetOrganizationRequests()
    {
        using EntryExitLogger logGuard = new(_logger);

        OrganizationRequestCollection organizationRequestCollection = new();
        organizationRequestCollection.AddRange(_requests);
        return organizationRequestCollection;
    }
}
