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

namespace DevPack4Dataverse.Mapper.Cache;

/// <summary>
/// Default thread-safe in-memory entity cache.
/// </summary>
public sealed class InMemoryEntityCache : IEntityCache, IEntityCacheByRecordId
{
    private const int MaxEntries = 100_000;
    private static readonly Task<CachedEntity?> NullTask = Task.FromResult<CachedEntity?>(
        null
    );
    private readonly ConcurrentDictionary<string, CacheEntry> _entries =
        new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<(string Key, long Generation)> _evictionQueue = new();
    private long _nextGeneration;
    private readonly ConcurrentDictionary<
        string,
        ConcurrentDictionary<string, byte>
    > _keysByRecord = new(StringComparer.Ordinal);

    public Task<CachedEntity?> GetAsync(EntityKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string cacheKey = key.ToString();
        if (_entries.TryGetValue(cacheKey, out CacheEntry? entry))
        {
            if (entry.ExpiresUtc > DateTime.UtcNow)
            {
                return entry.ReadTask;
            }

            if (_entries.TryRemove(cacheKey, out CacheEntry? expired))
            {
                UnindexEntry(cacheKey, expired.Value);
            }
        }

        return NullTask;
    }

    public Task SetAsync(
        EntityKey key,
        CachedEntity entry,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        string cacheKey = key.ToString();
        if (timeToLive <= TimeSpan.Zero)
        {
            // A non-positive TTL means the entry must not be cached. Remove an older value so
            // changing the configured TTL at runtime cannot leave stale data behind.
            if (_entries.TryRemove(cacheKey, out CacheEntry? stale))
            {
                UnindexEntry(cacheKey, stale.Value);
            }

            return Task.CompletedTask;
        }

        DateTime expiresUtc = DateTime.UtcNow.Add(timeToLive);
        if (_entries.TryGetValue(cacheKey, out CacheEntry? previous))
        {
            UnindexEntry(cacheKey, previous.Value);
        }

        long generation = Interlocked.Increment(ref _nextGeneration);
        CacheEntry newEntry = new(entry, expiresUtc, generation);
        _entries[cacheKey] = newEntry;
        IndexEntry(cacheKey, entry);
        _evictionQueue.Enqueue((cacheKey, generation));
        TrimCacheIfNeeded();

        return Task.CompletedTask;
    }

    public Task RemoveAsync(EntityKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string cacheKey = key.ToString();
        if (_entries.TryRemove(cacheKey, out CacheEntry? entry))
        {
            UnindexEntry(cacheKey, entry.Value);
        }

        return Task.CompletedTask;
    }

    public Task RemoveByRecordIdAsync(
        string logicalName,
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        string indexKey = RecordIndexKey(logicalName, id);
        if (!_keysByRecord.TryRemove(indexKey, out ConcurrentDictionary<string, byte>? keys))
        {
            return Task.CompletedTask;
        }

        foreach (string cacheKey in keys.Keys)
        {
            _entries.TryRemove(cacheKey, out _);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(string logicalName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string prefix = logicalName.ToLowerInvariant() + "|";
        foreach (string key in _entries.Keys)
        {
            if (
                !key.StartsWith(prefix, StringComparison.Ordinal)
                || !_entries.TryRemove(key, out CacheEntry? entry)
            )
            {
                continue;
            }

            UnindexEntry(key, entry.Value);
        }

        return Task.CompletedTask;
    }

    private void TrimCacheIfNeeded()
    {
        while (_entries.Count > MaxEntries && TryEvictOldest())
        {
        }

        while (_evictionQueue.Count > MaxEntries && TryEvictOldest())
        {
        }
    }

    private bool TryEvictOldest()
    {
        if (!_evictionQueue.TryDequeue(out (string Key, long Generation) candidate))
        {
            return false;
        }

        // The same key may have been written again after this queue entry was enqueued.
        // Only the current generation may be evicted; stale entries are discarded.
        if (
            !_entries.TryGetValue(candidate.Key, out CacheEntry? entry)
            || entry.Generation != candidate.Generation
        )
        {
            return true;
        }

        if (_entries.TryRemove(candidate.Key, out CacheEntry? removed))
        {
            UnindexEntry(candidate.Key, removed.Value);
        }

        return true;
    }

    private void IndexEntry(string cacheKey, CachedEntity entry)
    {
        if (entry.Record.Id == Guid.Empty)
        {
            return;
        }

        ConcurrentDictionary<string, byte> keys = _keysByRecord.GetOrAdd(
            RecordIndexKey(entry.Record.LogicalName, entry.Record.Id),
            _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal)
        );
        keys[cacheKey] = 0;
    }

    private void UnindexEntry(string cacheKey, CachedEntity entry)
    {
        if (entry.Record.Id == Guid.Empty)
        {
            return;
        }

        string indexKey = RecordIndexKey(entry.Record.LogicalName, entry.Record.Id);
        if (!_keysByRecord.TryGetValue(indexKey, out ConcurrentDictionary<string, byte>? keys))
        {
            return;
        }

        keys.TryRemove(cacheKey, out _);
        if (keys.IsEmpty)
        {
            _keysByRecord.TryRemove(indexKey, out _);
        }
    }

    private static string RecordIndexKey(string logicalName, Guid id) =>
        logicalName.ToLowerInvariant() + "|" + id.ToString("N");

    private sealed class CacheEntry(CachedEntity value, DateTime expiresUtc, long generation)
    {
        public CachedEntity Value { get; } = value;
        public DateTime ExpiresUtc { get; } = expiresUtc;
        public long Generation { get; } = generation;
        public Task<CachedEntity?> ReadTask { get; } = Task.FromResult<CachedEntity?>(value);
    }
}
