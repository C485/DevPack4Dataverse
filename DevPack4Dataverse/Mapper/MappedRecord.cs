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
/// Result of mapping one source item.
/// </summary>
public sealed class MappedRecord<TTarget>
{
    internal MappedRecord(
        object source,
        TTarget target,
        EntityReference reference,
        Entity savedEntity,
        bool created,
        bool skipped,
        IReadOnlyCollection<string> changedFields
    )
    {
        Source = source;
        Target = target;
        Reference = reference;
        SavedEntity = savedEntity;
        Created = created;
        Skipped = skipped;
        ChangedFields = changedFields ?? Array.Empty<string>();
    }

    public object Source { get; }

    public TTarget Target { get; }

    public EntityReference Reference { get; }

    public Entity SavedEntity { get; }

    public bool Created { get; }

    public bool Skipped { get; }

    public IReadOnlyCollection<string> ChangedFields { get; }
}
