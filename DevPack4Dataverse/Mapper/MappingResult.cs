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

namespace DevPack4Dataverse.Mapper;

/// <summary>
/// Records and validation errors produced by one mapper execution.
/// </summary>
public sealed class MappingResult<TTarget> : IMappingResult
{
    internal MappingResult(
        IReadOnlyList<MappedRecord<TTarget>> records,
        IReadOnlyList<MappingError> errors
    )
    {
        Records = records ?? Array.Empty<MappedRecord<TTarget>>();
        Errors = errors ?? Array.Empty<MappingError>();
    }

    public IReadOnlyList<MappedRecord<TTarget>> Records { get; }

    public IReadOnlyList<MappingError> Errors { get; }

    public bool HasErrors => Errors.Count > 0;

    public IReadOnlyList<EntityReference> SavedReferences =>
        Records.Where(p => p.Reference != null).Select(p => p.Reference).ToArray();
}
