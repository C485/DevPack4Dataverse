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
/// Options for one mapper.
/// </summary>
public sealed class MapperOptions<TSource, TTarget>
{
    /// <summary>
    /// Request settings applied to every read and save. Contains impersonation and
    /// <see cref="RequestSettings.SkipPluginExecution"/>.
    /// </summary>
    public RequestSettings RequestSettings { get; set; } = new();

    public bool ValidateAfterSave { get; set; }

    /// <summary>Runs after all stages and before change detection.</summary>
    public Action<MappingContext<TSource, TTarget>> PreSave { get; set; }

    /// <summary>Fields ignored when deciding whether an update is required.</summary>
    public ISet<string> SkipFieldsToChanged { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// When true, the change check may use a cached copy of the current record. Defaults to
    /// false, which always reads the record fresh from Dataverse.
    /// </summary>
    public bool UseCacheForChangeDetection { get; set; }

    /// <summary>
    /// Returns validation error messages for a freshly read record. When null and validation
    /// is enabled, the mapper only verifies that the saved record exists.
    /// </summary>
    public Func<Entity, MappingValidationContext, IEnumerable<string>> Validator { get; set; }

    /// <summary>
    /// Extra columns required by <see cref="Validator"/>. When empty, mapped attributes are used.
    /// </summary>
    public ISet<string> ValidationFields { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
