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
/// Non-generic mapper view used by <see cref="MapperWrapper"/>.
/// </summary>
public interface IMapper
{
    string Name { get; }

    Type SourceType { get; }

    Type TargetType { get; }

    Task<IMappingResult> ExecuteAsync(
        object source,
        MappingRunOptions? runOptions = null,
        CancellationToken cancellationToken = default
    );
}
