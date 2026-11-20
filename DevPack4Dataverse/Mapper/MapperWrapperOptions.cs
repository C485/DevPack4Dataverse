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

namespace DevPack4Dataverse.Mapper;

/// <summary>
/// Global mapper wrapper options: cache TTL, lock timeout, and wrapper-level validation.
/// </summary>
public sealed class MapperWrapperOptions
{
    public bool ValidateAfterSave { get; set; }

    public TimeSpan DefaultCacheTtl { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Cache TTL overrides keyed by Dataverse logical entity name.</summary>
    public IDictionary<string, TimeSpan> CacheTtlByEntity { get; } =
        new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);

    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Settings applied to wrapper-level validation reads.</summary>
    public RequestSettings RequestSettings { get; set; } = new();

    /// <summary>
    /// Validates every saved record. When set, validation fields must be supplied either in
    /// <see cref="ValidationFields"/> or in <see cref="ValidationFieldsByEntity"/>.
    /// </summary>
    public Func<Entity, MappingValidationContext, IEnumerable<string>>? Validator { get; set; }

    /// <summary>Fallback validation fields used when no per-table fields are configured.</summary>
    public ISet<string> ValidationFields { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Validation fields keyed by Dataverse logical entity name.</summary>
    public IDictionary<string, ISet<string>> ValidationFieldsByEntity { get; } =
        new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// When true, wrapper validation runs even if the mapper already performed its own
    /// post-save validation. Defaults to false to avoid duplicate reads.
    /// </summary>
    public bool ValidateAfterMapperValidation { get; set; }

    /// <summary>Returns per-table validation fields, falling back to <see cref="ValidationFields"/>.</summary>
    public ISet<string> GetValidationFields(string logicalName)
    {
        if (
            !string.IsNullOrWhiteSpace(logicalName)
            && ValidationFieldsByEntity.TryGetValue(logicalName, out ISet<string>? fields)
            && fields != null
        )
        {
            return fields;
        }

        return ValidationFields;
    }

    public TimeSpan GetCacheTtl(string logicalName)
    {
        if (
            !string.IsNullOrWhiteSpace(logicalName)
            && CacheTtlByEntity.TryGetValue(logicalName, out TimeSpan ttl)
        )
        {
            return ttl;
        }

        return DefaultCacheTtl;
    }
}
