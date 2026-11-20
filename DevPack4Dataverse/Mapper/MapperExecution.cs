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
/// One mapper call inside a wrapper batch.
/// </summary>
public sealed class MapperExecution
{
    public MapperExecution(string name, object source)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Mapper name is required.", nameof(name));
        }

        Name = name;
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public string Name { get; }

    public object Source { get; }

    public MappingRunOptions? RunOptions { get; set; }
}
