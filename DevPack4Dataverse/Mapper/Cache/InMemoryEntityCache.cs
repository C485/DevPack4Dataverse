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
public sealed class InMemoryEntityCache : IEntityCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries =
        new(StringComparer.Ordinal);

    public Task<CachedEntity> GetAsync(EntityKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string cacheKey = key.ToString();
        if (_entries.TryGetValue(cacheKey, out CacheEntry entry))
        {
            if (entry.ExpiresUtc > DateTime.UtcNow)
            {
                return Task.FromResult(entry.Value);
            }

            _entries.TryRemove(cacheKey, out _);
        }

        return Task.FromResult<CachedEntity>(null);
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

        DateTime expiresUtc =
            timeToLive > TimeSpan.Zero ? DateTime.UtcNow.Add(timeToLive) : DateTime.MaxValue;
        _entries[key.ToString()] = new CacheEntry(entry, expiresUtc);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(EntityKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries.TryRemove(key.ToString(), out _);
        return Task.CompletedTask;
    }

    public Task ClearAsync(string logicalName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string prefix = logicalName.ToLowerInvariant() + "|";
        foreach (string key in _entries.Keys.Where(p => p.StartsWith(prefix, StringComparison.Ordinal)))
        {
            _entries.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    private sealed class CacheEntry(CachedEntity value, DateTime expiresUtc)
    {
        public CachedEntity Value { get; } = value;
        public DateTime ExpiresUtc { get; } = expiresUtc;
    }
}
