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
/// Result of executing a mapper through <see cref="MapperWrapper"/>.
/// </summary>
[Serializable]
public sealed class MapperWrapperResult
{
    /// <summary>
    /// Key in <see cref="Exception.Data"/> with the mapper result saved before a wrapper
    /// validation failure.
    /// </summary>
    public const string PartialResultsDataKey =
        "DevPack4Dataverse.Mapper.WrapperPartialResults";

    internal MapperWrapperResult(IMappingResult mapperResult, IReadOnlyList<MappingError> errors)
    {
        MapperResult = mapperResult;
        Errors = errors ?? Array.Empty<MappingError>();
    }

    public IMappingResult MapperResult { get; }

    public IReadOnlyList<MappingError> Errors { get; }

    public bool HasErrors => Errors.Count > 0;
}
