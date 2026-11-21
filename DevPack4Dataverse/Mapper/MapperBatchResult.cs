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
/// Results of a wrapper batch execution.
/// </summary>
[Serializable]
public sealed class MapperBatchResult
{
    /// <summary>
    /// Key in <see cref="Exception.Data"/> with the mapper results completed before a failure.
    /// </summary>
    public const string PartialResultsDataKey =
        "DevPack4Dataverse.Mapper.BatchPartialResults";

    internal MapperBatchResult(
        IReadOnlyList<IMappingResult> mapperResults,
        IReadOnlyList<MappingError> errors
    )
    {
        MapperResults = mapperResults ?? Array.Empty<IMappingResult>();
        Errors = errors ?? Array.Empty<MappingError>();
    }

    public IReadOnlyList<IMappingResult> MapperResults { get; }

    public IReadOnlyList<MappingError> Errors { get; }

    public bool HasErrors => Errors.Count > 0;
}
