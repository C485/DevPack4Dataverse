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

namespace DevPack4Dataverse.Mapper;

/// <summary>
/// Source, partially mapped target, and current stage.
/// </summary>
public sealed class MappingContext<TSource, TTarget>
{
    private List<MappingError>? _errors;

    internal MappingContext(TSource source, TTarget target)
    {
        Source = source;
        Target = target;
    }

    public TSource Source { get; }

    public TTarget Target { get; }

    /// <summary>One-based mapping stage.</summary>
    public int Stage { get; internal set; }

    public IReadOnlyList<MappingError> Errors =>
        _errors ?? (IReadOnlyList<MappingError>)Array.Empty<MappingError>();

    internal void AddError(MappingError error) => (_errors ??= new List<MappingError>()).Add(error);
}
