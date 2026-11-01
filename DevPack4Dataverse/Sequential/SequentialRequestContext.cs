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

using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.Sequential;

/// <summary>
/// Immutable snapshot of the steps completed so far in a sequential request.
/// </summary>
/// <remarks>
/// Step indexes are zero-based and follow the order in which steps were added.
/// The same context type is passed to step factories (containing only previous steps)
/// and returned by execution (containing all steps).
/// </remarks>
public sealed class SequentialRequestContext
{
    internal SequentialRequestContext(IReadOnlyList<SequentialStepResult> results)
    {
        Results = Array.AsReadOnly(results.ToArray());
    }

    /// <summary>Completed steps in execution order.</summary>
    public IReadOnlyList<SequentialStepResult> Results { get; }

    /// <summary>Number of completed steps.</summary>
    public int Count => Results.Count;

    /// <summary>Returns the completed step at <paramref name="stepIndex"/>.</summary>
    /// <param name="stepIndex">Zero-based step index.</param>
    public SequentialStepResult this[int stepIndex] => Results[stepIndex];

    /// <summary>Last completed step.</summary>
    /// <exception cref="InvalidOperationException">No steps have completed.</exception>
    public SequentialStepResult Last =>
        Count > 0
            ? Results[Count - 1]
            : throw new InvalidOperationException("The sequential request has no completed steps.");

    /// <summary>
    /// Returns the record ID created by the step at <paramref name="stepIndex"/>.
    /// </summary>
    /// <param name="stepIndex">Zero-based step index.</param>
    /// <exception cref="InvalidOperationException">The step did not create a record.</exception>
    public Guid CreatedId(int stepIndex)
    {
        Guid? createdId = this[stepIndex].CreatedRecordId;
        return createdId
            ?? throw new InvalidOperationException($"Step {stepIndex} did not create a record.");
    }

    /// <summary>
    /// Returns the entity retrieved by the step at <paramref name="stepIndex"/>.
    /// </summary>
    /// <param name="stepIndex">Zero-based step index.</param>
    /// <exception cref="InvalidOperationException">The step did not retrieve a record.</exception>
    public Entity RetrievedEntity(int stepIndex)
    {
        Entity entity = this[stepIndex].RetrievedEntity;
        return entity
            ?? throw new InvalidOperationException($"Step {stepIndex} did not retrieve a record.");
    }

    /// <summary>
    /// Returns the response of the step at <paramref name="stepIndex"/> cast to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Expected response type.</typeparam>
    /// <param name="stepIndex">Zero-based step index.</param>
    /// <exception cref="InvalidOperationException">The response is not of the requested type.</exception>
    public T Response<T>(int stepIndex) where T : OrganizationResponse =>
        this[stepIndex].ResponseAs<T>();
}
