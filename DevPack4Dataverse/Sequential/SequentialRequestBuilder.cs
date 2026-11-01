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
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse.Sequential;

/// <summary>
/// Builds and executes Dataverse requests one after another.
/// </summary>
/// <remarks>
/// Each step receives an immutable <see cref="SequentialRequestContext"/> containing the results of all
/// previous steps. The execution is sequential and stops on the first exception; there is no automatic
/// rollback or transaction. Builders are mutable and not thread-safe. Create one builder per sequence and
/// execute it once per logical operation, or re-execute it to repeat all steps.
/// </remarks>
public sealed class SequentialRequestBuilder
{
    private readonly ILogger _logger;
    private readonly SdkProxy _sdkProxy;
    private readonly List<SequentialStep> _steps = new();

    /// <summary>
    /// Creates a new sequential request builder.
    /// </summary>
    /// <param name="sdkProxy">Connection layer used to execute requests. Cannot be null.</param>
    /// <param name="logger">Logger used by the builder. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public SequentialRequestBuilder(SdkProxy sdkProxy, ILogger logger)
    {
        using EntryExitLogger logGuard = new(logger);
        _sdkProxy = Guard.Against.Null(sdkProxy);
        _logger = Guard.Against.Null(logger);
    }

    /// <summary>Number of steps in the sequence.</summary>
    public int Count => _steps.Count;

    /// <summary>
    /// Adds an arbitrary request.
    /// </summary>
    /// <param name="request">Request to execute. Cannot be null.</param>
    /// <param name="requestSettings">Optional request settings for this step.</param>
    /// <returns>This builder for chaining.</returns>
    public SequentialRequestBuilder Execute(
        OrganizationRequest request,
        RequestSettings requestSettings = null
    )
    {
        Guard.Against.Null(request, nameof(request));
        return AddStep(_ => request, requestSettings);
    }

    /// <summary>
    /// Adds an arbitrary request created from the results of previous steps.
    /// </summary>
    /// <param name="requestFactory">Factory receiving previous results. Cannot be null.</param>
    /// <param name="requestSettings">Optional request settings for this step.</param>
    /// <returns>This builder for chaining.</returns>
    public SequentialRequestBuilder Execute(
        Func<SequentialRequestContext, OrganizationRequest> requestFactory,
        RequestSettings requestSettings = null
    )
    {
        Guard.Against.Null(requestFactory, nameof(requestFactory));
        return AddStep(
            context => Guard.Against.Null(requestFactory(context), nameof(requestFactory)),
            requestSettings
        );
    }

    /// <summary>
    /// Adds a create request for <paramref name="record"/>.
    /// </summary>
    /// <param name="record">Record to create. Cannot be null.</param>
    /// <param name="requestSettings">Optional request settings for this step.</param>
    /// <returns>This builder for chaining.</returns>
    public SequentialRequestBuilder Create(Entity record, RequestSettings requestSettings = null)
    {
        ValidateCreateRecord(record, nameof(record));
        return Execute(new CreateRequest { Target = record }, requestSettings);
    }

    /// <summary>
    /// Adds a create request built from the results of previous steps.
    /// </summary>
    /// <param name="recordFactory">Factory receiving previous results. Cannot be null.</param>
    /// <param name="requestSettings">Optional request settings for this step.</param>
    /// <returns>This builder for chaining.</returns>
    public SequentialRequestBuilder Create(
        Func<SequentialRequestContext, Entity> recordFactory,
        RequestSettings requestSettings = null
    )
    {
        Guard.Against.Null(recordFactory, nameof(recordFactory));
        return Execute(
            context =>
            {
                Entity record = Guard.Against.Null(recordFactory(context), nameof(recordFactory));
                ValidateCreateRecord(record, nameof(recordFactory));
                return new CreateRequest { Target = record };
            },
            requestSettings
        );
    }

    /// <summary>
    /// Adds an update request for <paramref name="record"/>.
    /// </summary>
    /// <param name="record">Record to update. Cannot be null.</param>
    /// <param name="requestSettings">Optional request settings for this step.</param>
    /// <returns>This builder for chaining.</returns>
    public SequentialRequestBuilder Update(Entity record, RequestSettings requestSettings = null)
    {
        ValidateUpdateRecord(record, nameof(record));
        return Execute(new UpdateRequest { Target = record }, requestSettings);
    }

    /// <summary>
    /// Adds an update request built from the results of previous steps.
    /// </summary>
    /// <param name="recordFactory">Factory receiving previous results. Cannot be null.</param>
    /// <param name="requestSettings">Optional request settings for this step.</param>
    /// <returns>This builder for chaining.</returns>
    public SequentialRequestBuilder Update(
        Func<SequentialRequestContext, Entity> recordFactory,
        RequestSettings requestSettings = null
    )
    {
        Guard.Against.Null(recordFactory, nameof(recordFactory));
        return Execute(
            context =>
            {
                Entity record = Guard.Against.Null(recordFactory(context), nameof(recordFactory));
                ValidateUpdateRecord(record, nameof(recordFactory));
                return new UpdateRequest { Target = record };
            },
            requestSettings
        );
    }

    /// <summary>
    /// Adds a delete request for <paramref name="entityReference"/>.
    /// </summary>
    /// <param name="entityReference">Record to delete. Cannot be null.</param>
    /// <param name="requestSettings">Optional request settings for this step.</param>
    /// <returns>This builder for chaining.</returns>
    public SequentialRequestBuilder Delete(
        EntityReference entityReference,
        RequestSettings requestSettings = null
    )
    {
        ValidateEntityReference(entityReference, nameof(entityReference));
        return Execute(new DeleteRequest { Target = entityReference }, requestSettings);
    }

    /// <summary>
    /// Adds a delete request built from the results of previous steps.
    /// </summary>
    /// <param name="entityReferenceFactory">Factory receiving previous results. Cannot be null.</param>
    /// <param name="requestSettings">Optional request settings for this step.</param>
    /// <returns>This builder for chaining.</returns>
    public SequentialRequestBuilder Delete(
        Func<SequentialRequestContext, EntityReference> entityReferenceFactory,
        RequestSettings requestSettings = null
    )
    {
        Guard.Against.Null(entityReferenceFactory, nameof(entityReferenceFactory));
        return Execute(
            context =>
            {
                EntityReference entityReference = Guard.Against.Null(
                    entityReferenceFactory(context),
                    nameof(entityReferenceFactory)
                );
                ValidateEntityReference(entityReference, nameof(entityReferenceFactory));
                return new DeleteRequest { Target = entityReference };
            },
            requestSettings
        );
    }

    /// <summary>
    /// Adds a retrieve request for <paramref name="target"/>.
    /// </summary>
    /// <param name="target">Record to retrieve. Cannot be null.</param>
    /// <param name="columnSet">Columns to retrieve. Cannot be null.</param>
    /// <param name="requestSettings">Optional request settings for this step.</param>
    /// <returns>This builder for chaining.</returns>
    public SequentialRequestBuilder Retrieve(
        EntityReference target,
        ColumnSet columnSet,
        RequestSettings requestSettings = null
    )
    {
        ValidateEntityReference(target, nameof(target));
        Guard.Against.Null(columnSet, nameof(columnSet));
        return Execute(
            new RetrieveRequest { Target = target, ColumnSet = columnSet },
            requestSettings
        );
    }

    /// <summary>
    /// Adds a retrieve request built from the results of previous steps.
    /// </summary>
    /// <param name="targetFactory">Factory receiving previous results. Cannot be null.</param>
    /// <param name="columnSet">Columns to retrieve. Cannot be null.</param>
    /// <param name="requestSettings">Optional request settings for this step.</param>
    /// <returns>This builder for chaining.</returns>
    public SequentialRequestBuilder Retrieve(
        Func<SequentialRequestContext, EntityReference> targetFactory,
        ColumnSet columnSet,
        RequestSettings requestSettings = null
    )
    {
        Guard.Against.Null(targetFactory, nameof(targetFactory));
        Guard.Against.Null(columnSet, nameof(columnSet));
        return Execute(
            context =>
            {
                EntityReference target = Guard.Against.Null(
                    targetFactory(context),
                    nameof(targetFactory)
                );
                ValidateEntityReference(target, nameof(targetFactory));
                return new RetrieveRequest { Target = target, ColumnSet = columnSet };
            },
            requestSettings
        );
    }

    /// <summary>
    /// Adds a retrieve request for the record identified by <paramref name="entityName"/> and <paramref name="id"/>.
    /// </summary>
    /// <param name="entityName">Logical entity name. Cannot be null or whitespace.</param>
    /// <param name="id">Record ID. Cannot be empty.</param>
    /// <param name="columnSet">Columns to retrieve. Cannot be null.</param>
    /// <param name="requestSettings">Optional request settings for this step.</param>
    /// <returns>This builder for chaining.</returns>
    public SequentialRequestBuilder Retrieve(
        string entityName,
        Guid id,
        ColumnSet columnSet,
        RequestSettings requestSettings = null
    )
    {
        Guard.Against.NullOrWhiteSpace(entityName, nameof(entityName));
        Guard.Against.Default(id, nameof(id));
        return Retrieve(new EntityReference(entityName, id), columnSet, requestSettings);
    }

    /// <summary>
    /// Adds a retrieve request whose ID is produced from the results of previous steps.
    /// </summary>
    /// <param name="entityName">Logical entity name. Cannot be null or whitespace.</param>
    /// <param name="idFactory">Factory receiving previous results. Cannot be null.</param>
    /// <param name="columnSet">Columns to retrieve. Cannot be null.</param>
    /// <param name="requestSettings">Optional request settings for this step.</param>
    /// <returns>This builder for chaining.</returns>
    public SequentialRequestBuilder Retrieve(
        string entityName,
        Func<SequentialRequestContext, Guid> idFactory,
        ColumnSet columnSet,
        RequestSettings requestSettings = null
    )
    {
        Guard.Against.NullOrWhiteSpace(entityName, nameof(entityName));
        Guard.Against.Null(idFactory, nameof(idFactory));
        Guard.Against.Null(columnSet, nameof(columnSet));
        return Retrieve(
            context =>
                new EntityReference(
                    entityName,
                    Guard.Against.Default(idFactory(context), nameof(idFactory))
                ),
            columnSet,
            requestSettings
        );
    }

    /// <summary>
    /// Adds an action request for <paramref name="target"/>.
    /// </summary>
    /// <param name="actionName">Action request name. Cannot be null or whitespace.</param>
    /// <param name="target">Action target. Cannot be null.</param>
    /// <param name="configure">Optional callback used to add parameters to the request.</param>
    /// <param name="requestSettings">Optional request settings for this step.</param>
    /// <returns>This builder for chaining.</returns>
    public SequentialRequestBuilder ExecuteAction(
        string actionName,
        EntityReference target,
        Action<OrganizationRequest> configure = null,
        RequestSettings requestSettings = null
    )
    {
        Guard.Against.NullOrWhiteSpace(actionName, nameof(actionName));
        ValidateEntityReference(target, nameof(target));
        return Execute(
            _ =>
            {
                OrganizationRequest request = new(actionName);
                request["Target"] = target;
                configure?.Invoke(request);
                return request;
            },
            requestSettings
        );
    }

    /// <summary>
    /// Adds an action request whose target is built from the results of previous steps.
    /// </summary>
    /// <param name="actionName">Action request name. Cannot be null or whitespace.</param>
    /// <param name="targetFactory">Factory receiving previous results. Cannot be null.</param>
    /// <param name="configure">Optional callback used to add parameters to the request.</param>
    /// <param name="requestSettings">Optional request settings for this step.</param>
    /// <returns>This builder for chaining.</returns>
    public SequentialRequestBuilder ExecuteAction(
        string actionName,
        Func<SequentialRequestContext, EntityReference> targetFactory,
        Action<SequentialRequestContext, OrganizationRequest> configure = null,
        RequestSettings requestSettings = null
    )
    {
        Guard.Against.NullOrWhiteSpace(actionName, nameof(actionName));
        Guard.Against.Null(targetFactory, nameof(targetFactory));
        return Execute(
            context =>
            {
                EntityReference target = Guard.Against.Null(
                    targetFactory(context),
                    nameof(targetFactory)
                );
                ValidateEntityReference(target, nameof(targetFactory));
                OrganizationRequest request = new(actionName);
                request["Target"] = target;
                configure?.Invoke(context, request);
                return request;
            },
            requestSettings
        );
    }

    /// <summary>
    /// Executes all steps synchronously and returns their results.
    /// </summary>
    /// <returns>Completed steps in execution order.</returns>
    /// <exception cref="InvalidOperationException">The builder has no steps.</exception>
    /// <remarks>
    /// Execution stops at the first exception and that exception propagates to the caller.
    /// </remarks>
    public SequentialRequestContext Execute()
    {
        using EntryExitLogger logGuard = new(_logger);
        SequentialStep[] steps = _steps.ToArray();
        EnsureHasSteps(steps);

        var results = new List<SequentialStepResult>(steps.Length);
        foreach (SequentialStep step in steps)
        {
            SequentialRequestContext context = new(results);
            OrganizationRequest request = Guard.Against.Null(
                step.RequestFactory(context),
                nameof(step)
            );
            OrganizationResponse response = Guard.Against.Null(
                _sdkProxy.Execute<OrganizationResponse>(request, step.RequestSettings),
                message: "The Dataverse service returned a null response."
            );
            results.Add(new SequentialStepResult(results.Count, request, response));
        }

        return new SequentialRequestContext(results);
    }

    /// <summary>
    /// Executes all steps asynchronously and returns their results.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token checked before each step. The underlying request execution does not receive the token.
    /// </param>
    /// <returns>Completed steps in execution order.</returns>
    /// <exception cref="InvalidOperationException">The builder has no steps.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was requested.</exception>
    /// <remarks>
    /// Execution stops at the first exception and that exception propagates to the caller.
    /// </remarks>
    public async Task<SequentialRequestContext> ExecuteAsync(
        CancellationToken cancellationToken = default
    )
    {
        using EntryExitLogger logGuard = new(_logger);
        SequentialStep[] steps = _steps.ToArray();
        EnsureHasSteps(steps);

        var results = new List<SequentialStepResult>(steps.Length);
        foreach (SequentialStep step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SequentialRequestContext context = new(results);
            OrganizationRequest request = Guard.Against.Null(
                step.RequestFactory(context),
                nameof(step)
            );
            OrganizationResponse response = Guard.Against.Null(
                await _sdkProxy
                    .ExecuteAsync<OrganizationResponse>(request, step.RequestSettings)
                    .ConfigureAwait(false),
                message: "The Dataverse service returned a null response."
            );
            results.Add(new SequentialStepResult(results.Count, request, response));
        }

        return new SequentialRequestContext(results);
    }

    private static void EnsureHasSteps(SequentialStep[] steps)
    {
        if (steps.Length == 0)
        {
            throw new InvalidOperationException(
                "The sequential request must contain at least one step."
            );
        }
    }

    private static void ValidateCreateRecord(Entity record, string parameterName)
    {
        Guard.Against.NullOrInvalidInput(
            record,
            parameterName,
            p => p.Id == Guid.Empty && !string.IsNullOrWhiteSpace(p.LogicalName),
            "Create record must have a logical name and an empty ID."
        );
    }

    private static void ValidateUpdateRecord(Entity record, string parameterName)
    {
        Guard.Against.NullOrInvalidInput(
            record,
            parameterName,
            p => p.Id != Guid.Empty && !string.IsNullOrWhiteSpace(p.LogicalName),
            "Update record must have a logical name and a non-empty ID."
        );
    }

    private static void ValidateEntityReference(
        EntityReference entityReference,
        string parameterName
    )
    {
        Guard.Against.NullOrInvalidInput(
            entityReference,
            parameterName,
            p => p.Id != Guid.Empty && !string.IsNullOrWhiteSpace(p.LogicalName),
            "Entity reference must have a logical name and a non-empty ID."
        );
    }

    private SequentialRequestBuilder AddStep(
        Func<SequentialRequestContext, OrganizationRequest> requestFactory,
        RequestSettings requestSettings
    )
    {
        _steps.Add(new SequentialStep(requestFactory, requestSettings));
        return this;
    }

    private sealed class SequentialStep(
        Func<SequentialRequestContext, OrganizationRequest> requestFactory,
        RequestSettings requestSettings
    )
    {
        public Func<SequentialRequestContext, OrganizationRequest> RequestFactory { get; } =
            requestFactory;
        public RequestSettings RequestSettings { get; } = requestSettings;
    }
}
