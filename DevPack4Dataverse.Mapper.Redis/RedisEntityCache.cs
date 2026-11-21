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

using System.Globalization;
using DevPack4Dataverse.Mapper.Cache;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;

namespace DevPack4Dataverse.Mapper.Redis;

/// <summary>
/// Redis-backed entity cache. Values are serialized to JSON and expire using the TTL
/// supplied by the mapper wrapper.
/// </summary>
public sealed class RedisEntityCache : IEntityCache, IEntityCacheByRecordId
{
    private readonly StackExchange.Redis.IDatabase _database;
    private readonly StackExchange.Redis.IConnectionMultiplexer _connection;
    private readonly string _prefix;

    public RedisEntityCache(
        StackExchange.Redis.IConnectionMultiplexer connection,
        string keyPrefix = "devpack:mapper:entity:"
    )
    {
        // Use a unique prefix per environment when several environments share one Redis.
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _database = connection.GetDatabase();
        _prefix = string.IsNullOrWhiteSpace(keyPrefix) ? "devpack:mapper:entity:" : keyPrefix;
    }

    public async Task<CachedEntity?> GetAsync(
        EntityKey key,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        StackExchange.Redis.RedisValue value = await _database
            .StringGetAsync(Key(key))
            .ConfigureAwait(false);
        if (value.IsNull)
        {
            return null;
        }

        return Deserialize(value.ToString()!);
    }

    public async Task SetAsync(
        EntityKey key,
        CachedEntity entry,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (timeToLive <= TimeSpan.Zero)
        {
            // A non-positive TTL means the entry must not be cached. Remove an older value so
            // changing the configured TTL at runtime cannot leave stale data behind.
            await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            return;
        }

        string json = Serialize(entry);
        TimeSpan expiry = timeToLive;
        await _database.StringSetAsync(Key(key), json, expiry).ConfigureAwait(false);

        StackExchange.Redis.RedisKey tableKey = TableKey(entry.Record.LogicalName);
        await _database.SetAddAsync(tableKey, Key(key).ToString()).ConfigureAwait(false);
        await _database.KeyExpireAsync(tableKey, expiry).ConfigureAwait(false);

        if (entry.Record.Id != Guid.Empty)
        {
            StackExchange.Redis.RedisKey indexKey = IndexKey(
                entry.Record.LogicalName,
                entry.Record.Id
            );
            await _database.SetAddAsync(indexKey, Key(key).ToString()).ConfigureAwait(false);
            await _database.SetAddAsync(tableKey, indexKey.ToString()).ConfigureAwait(false);
            await _database.KeyExpireAsync(indexKey, expiry).ConfigureAwait(false);
            await _database.KeyExpireAsync(tableKey, expiry).ConfigureAwait(false);
        }
    }

    public async Task RemoveAsync(EntityKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StackExchange.Redis.RedisKey cacheKey = Key(key);
        StackExchange.Redis.RedisValue value = await _database
            .StringGetAsync(cacheKey)
            .ConfigureAwait(false);
        if (!value.IsNull)
        {
            CachedEntityDto? dto = JsonConvert.DeserializeObject<CachedEntityDto>(
                value.ToString()
            );
            if (dto != null && dto.Id != Guid.Empty)
            {
                await _database
                    .SetRemoveAsync(IndexKey(dto.LogicalName, dto.Id), cacheKey.ToString())
                    .ConfigureAwait(false);
            }
        }

        await _database.KeyDeleteAsync(cacheKey).ConfigureAwait(false);
    }

    public async Task RemoveByRecordIdAsync(
        string logicalName,
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        StackExchange.Redis.RedisKey indexKey = IndexKey(logicalName, id);
        StackExchange.Redis.RedisValue[] indexedKeys = await _database
            .SetMembersAsync(indexKey)
            .ConfigureAwait(false);

        if (indexedKeys.Length > 0)
        {
            foreach (StackExchange.Redis.RedisValue indexedKey in indexedKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _database.KeyDeleteAsync(indexedKey.ToString()).ConfigureAwait(false);
            }

            await _database.KeyDeleteAsync(indexKey).ConfigureAwait(false);
            return;
        }

        // shortcut: entries created before the id index existed are removed by scanning.
        foreach (StackExchange.Redis.RedisKey key in EnumerateKeys(logicalName, cancellationToken))
        {
            StackExchange.Redis.RedisValue value = await _database
                .StringGetAsync(key)
                .ConfigureAwait(false);
            if (value.IsNull)
            {
                continue;
            }

            CachedEntityDto? dto = JsonConvert.DeserializeObject<CachedEntityDto>(
                value.ToString()
            );
            if (dto?.Id == id)
            {
                await _database.KeyDeleteAsync(key).ConfigureAwait(false);
            }
        }
    }

    public async Task ClearAsync(
        string logicalName,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        StackExchange.Redis.RedisKey tableKey = TableKey(logicalName);
        StackExchange.Redis.RedisValue[] keys = await _database
            .SetMembersAsync(tableKey)
            .ConfigureAwait(false);

        foreach (StackExchange.Redis.RedisValue key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _database.KeyDeleteAsync(key.ToString()).ConfigureAwait(false);
        }

        await _database.KeyDeleteAsync(tableKey).ConfigureAwait(false);
    }

    internal static string Serialize(CachedEntity entry) =>
        JsonConvert.SerializeObject(CachedEntityDto.From(entry));

    internal static CachedEntity? Deserialize(string json) =>
        JsonConvert.DeserializeObject<CachedEntityDto>(json)?.ToCachedEntity();

    private IEnumerable<StackExchange.Redis.RedisKey> EnumerateKeys(
        string logicalName,
        CancellationToken cancellationToken
    ) => EnumerateKeysByPattern(KeyPrefix(logicalName) + "*", cancellationToken);

    private IEnumerable<StackExchange.Redis.RedisKey> EnumerateKeysByPattern(
        string pattern,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        System.Net.EndPoint[] endpoints = _connection.GetEndPoints();
        if (endpoints.Length == 0)
        {
            throw new MapperException("Redis connection has no endpoints.");
        }

        StackExchange.Redis.IServer[] servers = endpoints
            .Select(endpoint => _connection.GetServer(endpoint))
            .Where(server => !server.IsReplica)
            .ToArray();

        foreach (StackExchange.Redis.IServer server in servers)
        {
            foreach (
                StackExchange.Redis.RedisKey key in server.Keys(pattern: pattern, pageSize: 1000)
            )
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return key;
            }
        }
    }

    private StackExchange.Redis.RedisKey Key(EntityKey key) => _prefix + key;

    private StackExchange.Redis.RedisKey IndexKey(string logicalName, Guid id) =>
        _prefix + "id:" + logicalName.ToLowerInvariant() + "|" + id.ToString("N");

    private StackExchange.Redis.RedisKey TableKey(string logicalName) =>
        _prefix + "table:" + logicalName.ToLowerInvariant();

    private string KeyPrefix(string logicalName) =>
        _prefix + logicalName.ToLowerInvariant() + "|";

    private sealed class CachedEntityDto
    {
        public string LogicalName { get; set; } = null!;
        public Guid Id { get; set; }
        public List<string> LoadedColumns { get; set; } = new();
        public string? RowVersion { get; set; }
        public List<CachedAttributeDto> Attributes { get; set; } = new();
        public List<CachedAttributeDto> KeyAttributes { get; set; } = new();
        public List<KeyValuePair<string, string>> FormattedValues { get; set; } = new();

        public static CachedEntityDto From(CachedEntity entry)
        {
            CachedEntityDto dto = new()
            {
                LogicalName = entry.Record.LogicalName,
                Id = entry.Record.Id,
                RowVersion = entry.Record.RowVersion,
                LoadedColumns = new List<string>(entry.LoadedColumns),
                Attributes = new List<CachedAttributeDto>(entry.Record.Attributes.Count),
                KeyAttributes = new List<CachedAttributeDto>(entry.Record.KeyAttributes.Count),
                FormattedValues = new List<KeyValuePair<string, string>>(
                    entry.Record.FormattedValues.Count
                )
            };

            foreach (KeyValuePair<string, object> attribute in entry.Record.Attributes)
            {
                dto.Attributes.Add(CachedAttributeDto.From(attribute.Key, attribute.Value));
            }

            foreach (KeyValuePair<string, object> attribute in entry.Record.KeyAttributes)
            {
                dto.KeyAttributes.Add(CachedAttributeDto.From(attribute.Key, attribute.Value));
            }

            foreach (KeyValuePair<string, string> formatted in entry.Record.FormattedValues)
            {
                dto.FormattedValues.Add(formatted);
            }

            return dto;
        }

        public CachedEntity ToCachedEntity()
        {
            Entity record = new(LogicalName, Id)
            {
                RowVersion = RowVersion
            };
            foreach (CachedAttributeDto attribute in Attributes)
            {
                record[attribute.Name] = attribute.ToValue();
            }

            foreach (CachedAttributeDto attribute in KeyAttributes)
            {
                record.KeyAttributes[attribute.Name] = attribute.ToValue();
            }

            foreach (KeyValuePair<string, string> formatted in FormattedValues)
            {
                record.FormattedValues[formatted.Key] = formatted.Value;
            }

            return new CachedEntity(record, LoadedColumns);
        }
    }

    private sealed class CachedAttributeDto
    {
        public string Name { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Value { get; set; } = null!;
        public string? ReferenceName { get; set; }
        public List<CachedAttributeDto> ReferenceKeyAttributes { get; set; } = new();

        public static CachedAttributeDto From(string name, object? value)
        {
            if (value == null)
            {
                return Create(name, "null", string.Empty);
            }

            switch (value)
            {
                case string text:
                    return Create(name, "string", text);
                case Guid guid:
                    return Create(name, "guid", guid.ToString("N"));
                case bool boolean:
                    return Create(name, "bool", boolean.ToString());
                case int integer:
                    return Create(name, "int", integer.ToString(CultureInfo.InvariantCulture));
                case long longValue:
                    return Create(name, "long", longValue.ToString(CultureInfo.InvariantCulture));
                case decimal decimalValue:
                    return Create(
                        name,
                        "decimal",
                        decimalValue.ToString(CultureInfo.InvariantCulture)
                    );
                case double doubleValue:
                    return Create(
                        name,
                        "double",
                        doubleValue.ToString("R", CultureInfo.InvariantCulture)
                    );
                case float floatValue:
                    return Create(
                        name,
                        "float",
                        floatValue.ToString("R", CultureInfo.InvariantCulture)
                    );
                case DateTime dateTime:
                    return Create(
                        name,
                        "datetime",
                        dateTime.ToString("O", CultureInfo.InvariantCulture)
                    );
                case Money money:
                    return Create(
                        name,
                        "money",
                        money.Value.ToString(CultureInfo.InvariantCulture)
                    );
                case OptionSetValue optionSet:
                    return Create(
                        name,
                        "optionset",
                        optionSet.Value.ToString(CultureInfo.InvariantCulture)
                    );
                case EntityReference reference:
                    CachedAttributeDto referenceDto = Create(
                        name,
                        "entityreference",
                        reference.LogicalName + "|" + reference.Id.ToString("N")
                    );
                    referenceDto.ReferenceName = reference.Name;
                    referenceDto.ReferenceKeyAttributes = new List<CachedAttributeDto>(
                        reference.KeyAttributes?.Count ?? 0
                    );
                    if (reference.KeyAttributes != null)
                    {
                        foreach (
                            KeyValuePair<string, object> keyAttribute in reference.KeyAttributes
                        )
                        {
                            referenceDto.ReferenceKeyAttributes.Add(
                                From(keyAttribute.Key, keyAttribute.Value)
                            );
                        }
                    }

                    return referenceDto;
                case byte[] bytes:
                    return Create(name, "bytes", Convert.ToBase64String(bytes));
                default:
                    throw new MapperException(
                        $"Redis mapper cache does not support attribute '{name}' of type '{value.GetType().FullName}'."
                    );
            }
        }

        public object ToValue()
        {
            switch (Type)
            {
                case "null":
                    return null!;
                case "string":
                    return Value;
                case "guid":
                    return Guid.Parse(Value);
                case "bool":
                    return bool.Parse(Value);
                case "int":
                    return int.Parse(Value, CultureInfo.InvariantCulture);
                case "long":
                    return long.Parse(Value, CultureInfo.InvariantCulture);
                case "decimal":
                    return decimal.Parse(Value, CultureInfo.InvariantCulture);
                case "double":
                    return double.Parse(Value, CultureInfo.InvariantCulture);
                case "float":
                    return float.Parse(Value, CultureInfo.InvariantCulture);
                case "datetime":
                    return DateTime.Parse(
                        Value,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind
                    );
                case "money":
                    return new Money(decimal.Parse(Value, CultureInfo.InvariantCulture));
                case "optionset":
                    return new OptionSetValue(int.Parse(Value, CultureInfo.InvariantCulture));
                case "entityreference":
                    int separator = Value.IndexOf('|');
                    if (separator < 0)
                    {
                        throw new MapperException(
                            $"Redis mapper cache contains an invalid entity reference '{Value}'."
                        );
                    }

                    string logicalName = Value.Substring(0, separator);
                    Guid id = Guid.Parse(Value.Substring(separator + 1));
                    EntityReference reference =
                        id == Guid.Empty
                            ? new EntityReference(logicalName)
                            : new EntityReference(logicalName, id);
                    reference.Name = ReferenceName;
                    foreach (CachedAttributeDto keyAttribute in ReferenceKeyAttributes)
                    {
                        reference.KeyAttributes[keyAttribute.Name] = keyAttribute.ToValue();
                    }

                    return reference;
                case "bytes":
                    return Convert.FromBase64String(Value);
                default:
                    throw new MapperException($"Redis mapper cache has unknown value type '{Type}'.");
            }
        }

        private static CachedAttributeDto Create(string name, string type, string value) =>
            new()
            {
                Name = name,
                Type = type,
                Value = value
            };
    }
}
