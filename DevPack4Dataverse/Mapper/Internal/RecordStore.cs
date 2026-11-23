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
using DevPack4Dataverse.Mapper.Cache;
using DevPack4Dataverse.Models;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse.Mapper.Internal;

internal sealed class RecordStore
{
    private const int CacheWriteParallelism = 32;
    private readonly IEntityCache _cache;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, DateTime> _preloadedAt;
    private readonly PrimaryKeyAttributeProvider _primaryKeys;
    private readonly SdkProxy _sdkProxy;
    private readonly MapperWrapperOptions _wrapperOptions;

    public RecordStore(
        SdkProxy sdkProxy,
        ILogger logger,
        IEntityCache cache,
        MapperWrapperOptions wrapperOptions,
        PrimaryKeyAttributeProvider primaryKeys,
        ConcurrentDictionary<string, DateTime> preloadedAt
    )
    {
        _sdkProxy = sdkProxy ?? throw new ArgumentNullException(nameof(sdkProxy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _wrapperOptions = wrapperOptions ?? throw new ArgumentNullException(nameof(wrapperOptions));
        _primaryKeys = primaryKeys ?? throw new ArgumentNullException(nameof(primaryKeys));
        _preloadedAt = preloadedAt ?? throw new ArgumentNullException(nameof(preloadedAt));
    }

    internal static string BuildPreloadKey(
        string logicalName,
        IEnumerable<string>? requiredColumns,
        IEnumerable<string>? alternateKeyAttributes
    )
    {
        static string Normalize(IEnumerable<string>? values) =>
            string.Join(
                ",",
                (values ?? Enumerable.Empty<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.ToLowerInvariant())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
            );

        return logicalName.ToLowerInvariant()
            + "|"
            + Normalize(requiredColumns)
            + "|"
            + Normalize(alternateKeyAttributes);
    }

    public async Task<Entity?> GetAsync(
        EntityKey key,
        IEnumerable<string> requiredColumns,
        bool useCache,
        bool forceRefresh,
        RequestSettings? requestSettings,
        CancellationToken cancellationToken
    )
    {
        HashSet<string> columns = new(
            requiredColumns ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase
        );
        if (key.IsAlternateKey)
        {
            foreach (string attribute in key.AlternateKey.Keys)
            {
                columns.Add(attribute);
            }
        }

        if (requestSettings?.HasImpersonation == true)
        {
            // The cache key does not contain the caller identity. Do not read a record
            // cached under another user, and remove the unscoped entry so a later
            // non-impersonated read cannot see it either.
            Entity? fresh = await RetrieveAsync(key, columns, requestSettings, cancellationToken)
                .ConfigureAwait(false);
            if (fresh != null)
            {
                await RemoveRecordAsync(fresh, cancellationToken).ConfigureAwait(false);
            }

            return fresh;
        }

        if (useCache && !forceRefresh)
        {
            CachedEntity? cached = await GetCacheEntryAsync(key, cancellationToken).ConfigureAwait(false);
            if (cached != null)
            {
                string[] missing = columns
                    .Where(column => !cached.LoadedColumns.Contains(column))
                    .ToArray();

                if (missing.Length == 0)
                {
                    // Return a copy so callers cannot mutate the cached record.
                    return EntityCopy.Clone(cached.Record);
                }

                Entity? missingValues = await RetrieveAsync(
                        key,
                        missing,
                        requestSettings,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                if (missingValues == null)
                {
                    await RemoveCacheKeyAsync(key, cancellationToken).ConfigureAwait(false);
                    return null;
                }

                Entity merged = EntityCopy.Clone(cached.Record)!;
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

        Entity? record = await RetrieveAsync(key, columns, requestSettings, cancellationToken)
            .ConfigureAwait(false);
        if (record == null)
        {
            if (forceRefresh)
            {
                await RemoveCacheKeyAsync(key, cancellationToken).ConfigureAwait(false);
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
        IEnumerable<string> requiredColumns,
        IEnumerable<string> alternateKeyAttributes,
        bool forceRefresh,
        RequestSettings requestSettings,
        CancellationToken cancellationToken
    )
    {
        if (requestSettings.HasImpersonation)
        {
            // A preload under impersonation would cache another user's view of the table.
            return;
        }

        string[] alternateAttributes =
            alternateKeyAttributes?.ToArray() ?? Array.Empty<string>();
        string[] requestedColumns = requiredColumns?.ToArray() ?? Array.Empty<string>();
        string preloadKey = BuildPreloadKey(
            logicalName,
            requestedColumns,
            alternateAttributes
        );

        DateTime now = DateTime.UtcNow;
        if (
            !forceRefresh
            && _preloadedAt.TryGetValue(preloadKey, out DateTime loadedAt)
            && now - loadedAt < _wrapperOptions.GetCacheTtl(logicalName)
        )
        {
            return;
        }

        HashSet<string> columns = new(requestedColumns, StringComparer.OrdinalIgnoreCase);
        foreach (string attribute in alternateAttributes)
        {
            columns.Add(attribute);
        }

        await ClearCacheAsync(logicalName, cancellationToken).ConfigureAwait(false);

        string primaryId = await _primaryKeys
            .GetAsync(logicalName, cancellationToken)
            .ConfigureAwait(false);
        QueryExpression query = new()
        {
            EntityName = logicalName,
            ColumnSet = CreateColumnSet(columns)
        };
        query.Orders.Add(new OrderExpression(primaryId, OrderType.Ascending));

        Entity[] records = await _sdkProxy
            .RetrieveMultipleAsync(query, requestSettings)
            .ConfigureAwait(false);

        ConcurrentDictionary<string, byte> alternateKeys = new(StringComparer.Ordinal);
        await AsyncParallel
            .ForEachAsync(
                records,
                CacheWriteParallelism,
                cancellationToken,
                async (record, token) =>
                {
                    if (record.Id != Guid.Empty)
                    {
                        await CacheRecordAsync(
                                record,
                                EntityKey.ById(logicalName, record.Id),
                                columns,
                                token
                            )
                            .ConfigureAwait(false);
                    }

                    EntityKey? alternateKey = TryCreateAlternateKey(
                        logicalName,
                        record,
                        alternateAttributes
                    );
                    if (alternateKey != null)
                    {
                        if (!alternateKeys.TryAdd(alternateKey.ToString(), 0))
                        {
                            throw new MapperException(
                                $"Alternate key '{alternateKey}' matches more than one '{logicalName}' record."
                            );
                        }

                        await CacheRecordAsync(record, alternateKey, columns, token)
                            .ConfigureAwait(false);
                    }
                }
            )
            .ConfigureAwait(false);

        _preloadedAt[preloadKey] = DateTime.UtcNow;
    }

    public async Task CacheSavedAsync(
        Entity entity,
        RequestSettings? requestSettings,
        CancellationToken cancellationToken
    )
    {
        if (
            entity == null
            || requestSettings?.HasImpersonation == true
            || requestSettings?.ConcurrencyBehavior == ConcurrencyBehavior.IfRowVersionMatches
        )
        {
            return;
        }

        EntityKey? key = EntityKey.FromEntity(entity);
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

    private async Task<Entity?> RetrieveAsync(
        EntityKey key,
        IEnumerable<string> columns,
        RequestSettings? requestSettings,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        QueryExpression query = new()
        {
            EntityName = key.LogicalName,
            ColumnSet = CreateColumnSet(columns)
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
            string primaryIdAttribute = await _primaryKeys
                .GetAsync(key.LogicalName, cancellationToken)
                .ConfigureAwait(false);
            query.Criteria.AddCondition(
                primaryIdAttribute,
                ConditionOperator.Equal,
                key.Id!.Value
            );
        }

        Entity[] records = await _sdkProxy
            .RetrieveMultipleAsync(query, requestSettings)
            .ConfigureAwait(false);

        if (key.IsAlternateKey && records.Length > 1)
        {
            throw new MapperException(
                $"Alternate key '{key}' matched {records.Length} records in table '{key.LogicalName}'."
            );
        }

        return records.FirstOrDefault();
    }

    private async Task CacheRecordAsync(
        Entity record,
        EntityKey requestedKey,
        ISet<string> loadedColumns,
        CancellationToken cancellationToken
    )
    {
        CachedEntity entry = new(EntityCopy.Clone(record)!, loadedColumns);
        await CacheEntryAsync(requestedKey, entry, cancellationToken).ConfigureAwait(false);

        if (record.Id != Guid.Empty)
        {
            EntityKey idKey = EntityKey.ById(record.LogicalName, record.Id);
            if (!idKey.Equals(requestedKey))
            {
                await CacheEntryAsync(idKey, entry, cancellationToken).ConfigureAwait(false);
            }
        }

        if (record.KeyAttributes != null && record.KeyAttributes.Count > 0)
        {
            EntityKey alternateKey = EntityKey.ByAlternateKey(
                record.LogicalName,
                record.KeyAttributes
            );
            if (!alternateKey.Equals(requestedKey))
            {
                await CacheEntryAsync(alternateKey, entry, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    public async Task RemoveRecordAsync(Entity entity, CancellationToken cancellationToken)
    {
        if (entity == null)
        {
            return;
        }

        if (entity.Id != Guid.Empty)
        {
            await RemoveCacheKeysByRecordIdAsync(entity.LogicalName, entity.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        EntityKey? key = EntityKey.FromEntity(entity);
        if (key != null)
        {
            await RemoveCacheKeyAsync(key, cancellationToken).ConfigureAwait(false);
        }

        if (entity.KeyAttributes != null && entity.KeyAttributes.Count > 0)
        {
            await RemoveCacheKeyAsync(
                    EntityKey.ByAlternateKey(entity.LogicalName, entity.KeyAttributes),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    private async Task CacheEntryAsync(
        EntityKey key,
        CachedEntity entry,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await _cache
                .SetAsync(
                    key,
                    entry,
                    _wrapperOptions.GetCacheTtl(key.LogicalName),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            !(exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        )
        {
            _logger.LogWarning(
                exception,
                "Could not write mapper cache entry for {LogicalName}. Mapping continues without cache.",
                key.LogicalName
            );
        }
    }

    private async Task<CachedEntity?> GetCacheEntryAsync(
        EntityKey key,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            !(exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        )
        {
            _logger.LogWarning(
                exception,
                "Could not read mapper cache entry for {LogicalName}. Reading from Dataverse.",
                key.LogicalName
            );
            return null;
        }
    }

    private async Task RemoveCacheKeyAsync(EntityKey key, CancellationToken cancellationToken)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            !(exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        )
        {
            _logger.LogWarning(
                exception,
                "Could not remove mapper cache entry for {LogicalName}. Mapping continues.",
                key.LogicalName
            );
        }
    }

    private async Task RemoveCacheKeysByRecordIdAsync(
        string logicalName,
        Guid id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (_cache is IEntityCacheByRecordId byRecordId)
            {
                await byRecordId
                    .RemoveByRecordIdAsync(logicalName, id, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                // Older external caches cannot remove by record id; clearing the table is safe.
                await _cache.ClearAsync(logicalName, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (
            !(exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        )
        {
            _logger.LogWarning(
                exception,
                "Could not remove mapper cache entries for {LogicalName}:{Id}. Mapping continues.",
                logicalName,
                id
            );
        }
    }

    private async Task ClearCacheAsync(string logicalName, CancellationToken cancellationToken)
    {
        // A table clear also invalidates every preload marker for that table. Another mapper
        // with a fresh marker must reload instead of silently missing the cleared entries.
        string prefix = logicalName + "|";
        foreach (string key in _preloadedAt.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                _preloadedAt.TryRemove(key, out _);
            }
        }

        try
        {
            await _cache.ClearAsync(logicalName, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            !(exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        )
        {
            _logger.LogWarning(
                exception,
                "Could not clear mapper cache for {LogicalName}. Continuing without cache.",
                logicalName
            );
        }
    }

    private static EntityKey? TryCreateAlternateKey(
        string logicalName,
        Entity record,
        string[] attributes
    )
    {
        if (attributes.Length == 0)
        {
            return null;
        }

        Dictionary<string, object> values = new(
            attributes.Length,
            StringComparer.OrdinalIgnoreCase
        );
        foreach (string attribute in attributes)
        {
            if (!record.Attributes.TryGetValue(attribute, out object? value) || value == null)
            {
                return null;
            }

            values[attribute.ToLowerInvariant()] = value;
        }

        return values.Count == 0 ? null : EntityKey.FromAlternateKeyValues(logicalName, values);
    }

    private static ColumnSet CreateColumnSet(IEnumerable<string> columns)
    {
        string[] values = columns?.ToArray() ?? Array.Empty<string>();
        return values.Length == 0 ? new ColumnSet(false) : new ColumnSet(values);
    }
}
