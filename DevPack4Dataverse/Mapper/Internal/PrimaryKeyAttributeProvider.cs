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

using System.Collections.Concurrent;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace DevPack4Dataverse.Mapper.Internal;

/// <summary>
/// Resolves the primary id attribute per table (for example <c>activityid</c> for activity
/// tables) and caches it for the lifetime of the mapper.
/// </summary>
internal sealed class PrimaryKeyAttributeProvider(SdkProxy sdkProxy)
{
    private readonly ConcurrentDictionary<string, string> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> GetAsync(string logicalName, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(logicalName, out string? cached) && cached != null)
        {
            return cached;
        }

        RetrieveEntityResponse? response = await sdkProxy
            .ExecuteAsync<RetrieveEntityResponse>(
                new RetrieveEntityRequest
                {
                    LogicalName = logicalName,
                    EntityFilters = EntityFilters.Entity,
                    RetrieveAsIfPublished = true
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        string? primaryIdAttribute = response?.EntityMetadata?.PrimaryIdAttribute;
        if (string.IsNullOrWhiteSpace(primaryIdAttribute))
        {
            throw new MapperException(
                $"Could not determine the primary key attribute for table '{logicalName}'."
            );
        }

        string primaryId = primaryIdAttribute!;
        _cache[logicalName] = primaryId;
        return primaryId;
    }
}
