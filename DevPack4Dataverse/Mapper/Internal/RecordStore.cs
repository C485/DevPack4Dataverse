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

using DevPack4Dataverse.Mapper.Cache;
using DevPack4Dataverse.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse.Mapper.Internal;

internal sealed class RecordStore
{
    private readonly IEntityCache _cache;
    private readonly SdkProxy _sdkProxy;
    private readonly MapperWrapperOptions _wrapperOptions;

    public RecordStore(
        SdkProxy sdkProxy,
        ILogger logger,
        IEntityCache cache,
        MapperWrapperOptions wrapperOptions
    )
    {
        _sdkProxy = sdkProxy ?? throw new ArgumentNullException(nameof(sdkProxy));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _wrapperOptions = wrapperOptions ?? throw new ArgumentNullException(nameof(wrapperOptions));
    }

    public async Task<Entity> GetAsync(
        EntityKey key,
        IReadOnlyCollection<string> requiredColumns,
        bool useCache,
        bool forceRefresh,
        RequestSettings requestSettings,
        CancellationToken cancellationToken
    )
    {
        HashSet<string> columns = new(requiredColumns ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        if (key.IsAlternateKey)
        {
            foreach (string attribute in key.AlternateKey.Keys)
            {
                columns.Add(attribute);
            }
        }

        if (useCache && !forceRefresh)
        {
            CachedEntity cached = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
            if (cached != null)
            {
                string[] missing = columns
                    .Where(column => !cached.LoadedColumns.Contains(column))
                    .ToArray();

                if (missing.Length == 0)
                {
                    return cached.Record;
                }

                Entity missingValues = await RetrieveAsync(
                        key,
                        missing,
                        requestSettings,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                if (missingValues == null)
                {
                    await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
                    return null;
                }

                Entity merged = EntityCopy.Clone(cached.Record);
                EntityCopy.Merge(merged, missingValues);
                HashSet<string> loadedColumns = new(
                    cached.LoadedColumns,
                    StringComparer.OrdinalIgnoreCase
                );
                foreach (string column in missing)
                {
                    loadedColumns.Add(column);
                }

                CachedEntity updated = new(merged, loadedColumns);
                await CacheEntryAsync(key, updated, cancellationToken).ConfigureAwait(false);
                return merged;
            }
        }

        Entity record = await RetrieveAsync(key, columns, requestSettings, cancellationToken)
            .ConfigureAwait(false);
        if (record == null)
        {
            if (forceRefresh)
            {
                await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        if (useCache || forceRefresh)
        {
            await CacheRecordAsync(record, key, columns, cancellationToken).ConfigureAwait(false);
        }

        return record;
    }

    public async Task LoadAllAsync(
        string logicalName,
        IReadOnlyCollection<string> requiredColumns,
        IReadOnlyCollection<string> alternateKeyAttributes,
        RequestSettings requestSettings,
        CancellationToken cancellationToken
    )
    {
        HashSet<string> columns = new(requiredColumns ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        if (alternateKeyAttributes != null)
        {
            foreach (string attribute in alternateKeyAttributes)
            {
                columns.Add(attribute);
            }
        }

        await _cache.ClearAsync(logicalName, cancellationToken).ConfigureAwait(false);

        QueryExpression query = new()
        {
            EntityName = logicalName,
            ColumnSet = CreateColumnSet(columns)
        };

        Entity[] records = await _sdkProxy
            .RetrieveMultipleAsync(query, requestSettings)
            .ConfigureAwait(false);

        string[] attributes = alternateKeyAttributes?.ToArray() ?? Array.Empty<string>();
        foreach (Entity record in records)
        {
            if (record.Id != Guid.Empty)
            {
                await CacheRecordAsync(
                        record,
                        EntityKey.ById(logicalName, record.Id),
                        columns,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }

            EntityKey alternateKey = TryCreateAlternateKey(logicalName, record, attributes);
            if (alternateKey != null)
            {
                await CacheRecordAsync(record, alternateKey, columns, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    public async Task CacheSavedAsync(Entity entity, CancellationToken cancellationToken)
    {
        if (entity == null)
        {
            return;
        }

        EntityKey key = EntityKey.FromEntity(entity);
        if (key == null)
        {
            return;
        }

        HashSet<string> columns = new(entity.Attributes.Keys, StringComparer.OrdinalIgnoreCase);
        if (entity.KeyAttributes != null)
        {
            foreach (string attribute in entity.KeyAttributes.Keys)
            {
                columns.Add(attribute);
            }
        }

        await CacheRecordAsync(entity, key, columns, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Entity> RetrieveAsync(
        EntityKey key,
        IEnumerable<string> columns,
        RequestSettings requestSettings,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        QueryExpression query = new()
        {
            EntityName = key.LogicalName,
            ColumnSet = CreateColumnSet(columns),
            TopCount = 1
        };

        if (key.IsAlternateKey)
        {
            foreach (KeyValuePair<string, object> pair in key.AlternateKey)
            {
                query.Criteria.AddCondition(pair.Key, ConditionOperator.Equal, pair.Value);
            }
        }
        else
        {
            query.Criteria.AddCondition("entityid", ConditionOperator.Equal, key.Id.Value);
        }

        Entity[] records = await _sdkProxy
            .RetrieveMultipleAsync(query, requestSettings)
            .ConfigureAwait(false);

        return records.FirstOrDefault();
    }

    private async Task CacheRecordAsync(
        Entity record,
        EntityKey requestedKey,
        IEnumerable<string> loadedColumns,
        CancellationToken cancellationToken
    )
    {
        CachedEntity entry = new(EntityCopy.Clone(record), loadedColumns);
        await CacheEntryAsync(requestedKey, entry, cancellationToken).ConfigureAwait(false);

        if (record.Id != Guid.Empty)
        {
            await CacheEntryAsync(
                    EntityKey.ById(record.LogicalName, record.Id),
                    entry,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        if (record.KeyAttributes != null && record.KeyAttributes.Count > 0)
        {
            await CacheEntryAsync(
                    EntityKey.ByAlternateKey(record.LogicalName, record.KeyAttributes),
                    entry,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    private Task CacheEntryAsync(
        EntityKey key,
        CachedEntity entry,
        CancellationToken cancellationToken
    )
    {
        return _cache.SetAsync(
            key,
            entry,
            _wrapperOptions.GetCacheTtl(key.LogicalName),
            cancellationToken
        );
    }

    private static EntityKey TryCreateAlternateKey(
        string logicalName,
        Entity record,
        IEnumerable<string> attributes
    )
    {
        Dictionary<string, object> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string attribute in attributes)
        {
            if (!record.Attributes.TryGetValue(attribute, out object value) || value == null)
            {
                return null;
            }

            values[attribute] = value;
        }

        return values.Count == 0 ? null : EntityKey.ByAlternateKey(logicalName, values);
    }

    private static ColumnSet CreateColumnSet(IEnumerable<string> columns)
    {
        string[] values = columns?.ToArray() ?? Array.Empty<string>();
        return values.Length == 0 ? new ColumnSet(false) : new ColumnSet(values);
    }
}
