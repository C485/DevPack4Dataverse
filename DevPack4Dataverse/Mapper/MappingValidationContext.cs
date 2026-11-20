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
/// Context passed to validators after a record has been saved and re-read.
/// </summary>
public sealed class MappingValidationContext
{
    public MappingValidationContext(string? mapperName, object? source, object? target, Entity record)
    {
        MapperName = mapperName;
        Source = source;
        Target = target;
        Record = record;
    }

    public string? MapperName { get; }

    public object? Source { get; }

    public object? Target { get; }

    public Entity Record { get; }
}
