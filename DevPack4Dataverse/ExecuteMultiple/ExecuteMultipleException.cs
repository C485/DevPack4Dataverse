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

using DevPack4Dataverse.Models;
using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.ExecuteMultiple;

/// <summary>
/// Thrown by the simple ExecuteMultiple overload when a request faults. Carries the partial
/// result and the faulted response item so the caller can resume instead of repeating work.
/// </summary>
public sealed class ExecuteMultipleException : InvalidOperationException
{
    public ExecuteMultipleException(
        string message,
        ExecuteMultipleLogicResult result,
        ExecuteMultipleResponseItem? fault
    )
        : base(message)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        Fault = fault;
    }

    /// <summary>Responses collected before the fault, in the original request order.</summary>
    public ExecuteMultipleLogicResult Result { get; }

    /// <summary>The faulted response item, including the Dataverse fault and the chunk-local index.</summary>
    public ExecuteMultipleResponseItem? Fault { get; }
}
