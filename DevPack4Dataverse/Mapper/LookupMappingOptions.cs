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
/// Options for one lookup mapping field.
/// </summary>
public sealed class LookupMappingOptions
{
    /// <summary>Fields required from the lookup target. Only these columns are retrieved.</summary>
    public ISet<string> Fields { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Alternate-key attribute names used to index records during
    /// <see cref="LookupRetrievalMode.RetrieveAllRecords"/>.
    /// </summary>
    public ISet<string> AlternateKeyAttributes { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool UseCache { get; set; } = true;

    public LookupRetrievalMode RetrievalMode { get; set; } = LookupRetrievalMode.OneByOne;

    public bool CreateIfNotExists { get; set; }

    public bool ThrowIfEmpty { get; set; }

    public bool ThrowIfNotFound { get; set; } = true;

    internal void Validate(string targetMember)
    {
        if (RetrievalMode == LookupRetrievalMode.RetrieveAllRecords && !UseCache)
        {
            throw new MapperConfigurationException(
                $"Lookup '{targetMember}' cannot use RetrieveAllRecords when UseCache is false."
            );
        }
    }
}
