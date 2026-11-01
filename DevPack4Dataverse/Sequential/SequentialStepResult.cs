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
using Microsoft.Xrm.Sdk.Messages;

namespace DevPack4Dataverse.Sequential;

/// <summary>
/// Result of one completed step in a sequential request.
/// </summary>
public sealed class SequentialStepResult
{
    internal SequentialStepResult(
        int index,
        OrganizationRequest request,
        OrganizationResponse response
    )
    {
        Index = index;
        Request = request;
        Response = response;
    }

    /// <summary>Zero-based index of the step.</summary>
    public int Index { get; }

    /// <summary>Request that was executed.</summary>
    public OrganizationRequest Request { get; }

    /// <summary>Response returned by Dataverse.</summary>
    public OrganizationResponse Response { get; }

    /// <summary>
    /// Record ID returned by a <see cref="CreateRequest"/>, or null when the step did not create a record.
    /// </summary>
    public Guid? CreatedRecordId =>
        Response is CreateResponse createResponse && createResponse.id != Guid.Empty
            ? createResponse.id
            : null;

    /// <summary>
    /// Entity returned by a <see cref="RetrieveRequest"/>, or null when the step did not retrieve a record.
    /// </summary>
    public Entity RetrievedEntity =>
        Response is RetrieveResponse retrieveResponse ? retrieveResponse.Entity : null;

    /// <summary>
    /// Returns the response cast to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Expected response type.</typeparam>
    /// <exception cref="InvalidOperationException">The response is not of the requested type.</exception>
    public T ResponseAs<T>() where T : OrganizationResponse
    {
        if (Response is T typedResponse)
        {
            return typedResponse;
        }

        throw new InvalidOperationException(
            $"Step {Index} returned {Response.GetType().Name}, not {typeof(T).Name}."
        );
    }
}
