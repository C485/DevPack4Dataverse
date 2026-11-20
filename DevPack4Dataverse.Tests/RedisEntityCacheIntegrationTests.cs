using DevPack4Dataverse.Mapper;
using DevPack4Dataverse.Mapper.Cache;
using DevPack4Dataverse.Mapper.Redis;
using Microsoft.Xrm.Sdk;
using StackExchange.Redis;
using Xunit;

namespace DevPack4Dataverse.Tests;

public sealed class RedisEntityCacheIntegrationTests
{
    [RedisFact]
    public async Task RedisCache_RoundTrip_Expiry_RemoveByIdAndClear()
    {
        using ConnectionMultiplexer connection = await RedisIntegrationTest.ConnectAsync();
        string prefix = RedisIntegrationTest.NewPrefix();
        RedisEntityCache cache = new(connection, prefix);
        IDatabase database = connection.GetDatabase();

        Guid accountId = Guid.NewGuid();
        string accountIndexKey = prefix + "id:account|" + accountId.ToString("N");
        EntityReference parent = new("account", Guid.NewGuid()) { Name = "Parent" };
        parent.KeyAttributes["accountnumber"] = "P-1";
        Entity account = new("account", accountId)
        {
            ["name"] = "Contoso",
            ["creditlimit"] = new Money(12.34m),
            ["createdon"] = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            ["statecode"] = new OptionSetValue(0),
            ["parentaccountid"] = parent,
            ["data"] = new byte[] { 1, 2, 3 }
        };
        account.KeyAttributes["accountnumber"] = "A-1";
        CachedEntity accountEntry = new(account, new[] { "name", "creditlimit" });
        EntityKey accountKey = EntityKey.ById("account", accountId);

        await cache.SetAsync(accountKey, accountEntry, TimeSpan.FromMinutes(1));

        using ConnectionMultiplexer readerConnection = await RedisIntegrationTest.ConnectAsync();
        RedisEntityCache reader = new(readerConnection, prefix);
        CachedEntity? loaded = await reader.GetAsync(accountKey);
        Assert.NotNull(loaded);
        Assert.Equal("Contoso", loaded.Record.GetAttributeValue<string>("name"));
        Assert.Equal(12.34m, loaded.Record.GetAttributeValue<Money>("creditlimit").Value);
        Assert.Equal(
            new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            loaded.Record.GetAttributeValue<DateTime>("createdon")
        );
        Assert.Equal("A-1", loaded.Record.KeyAttributes["accountnumber"]);
        Assert.Equal(0, loaded.Record.GetAttributeValue<OptionSetValue>("statecode").Value);
        Assert.Equal(new byte[] { 1, 2, 3 }, loaded.Record.GetAttributeValue<byte[]>("data"));

        EntityReference restoredParent = loaded.Record.GetAttributeValue<EntityReference>(
            "parentaccountid"
        );
        Assert.Equal(parent.Id, restoredParent.Id);
        Assert.Equal("Parent", restoredParent.Name);
        Assert.Equal("P-1", restoredParent.KeyAttributes["accountnumber"]);
        Assert.Contains("name", loaded.LoadedColumns);

        Guid contactId = Guid.NewGuid();
        EntityKey contactKey = EntityKey.ById("contact", contactId);
        await cache.SetAsync(
            contactKey,
            new CachedEntity(new Entity("contact", contactId) { ["fullname"] = "Alice" }),
            TimeSpan.FromMinutes(1)
        );

        await cache.RemoveByRecordIdAsync("account", accountId);
        Assert.Null(await cache.GetAsync(accountKey));
        Assert.NotNull(await cache.GetAsync(contactKey));

        await cache.SetAsync(accountKey, accountEntry, TimeSpan.FromMilliseconds(300));
        Assert.NotNull(await cache.GetAsync(accountKey));
        await Task.Delay(1500);
        Assert.Null(await cache.GetAsync(accountKey));

        await cache.SetAsync(accountKey, accountEntry, TimeSpan.FromMinutes(1));
        Assert.True(await database.KeyExistsAsync(accountIndexKey));
        await cache.ClearAsync("account");
        Assert.Null(await cache.GetAsync(accountKey));
        Assert.False(await database.KeyExistsAsync(accountIndexKey));
        Assert.NotNull(await cache.GetAsync(contactKey));

        await cache.RemoveAsync(contactKey);
        Assert.Null(await cache.GetAsync(contactKey));
    }

    [RedisFact]
    public async Task RedisCache_MissingKeyReturnsNull()
    {
        using ConnectionMultiplexer connection = await RedisIntegrationTest.ConnectAsync();
        RedisEntityCache cache = new(connection, RedisIntegrationTest.NewPrefix());

        Assert.Null(await cache.GetAsync(EntityKey.ById("account", Guid.NewGuid())));
    }

    [RedisFact]
    public async Task RedisCache_RemoveByRecordIdFallsBackToScanWhenIndexIsMissing()
    {
        using ConnectionMultiplexer connection = await RedisIntegrationTest.ConnectAsync();
        string prefix = RedisIntegrationTest.NewPrefix();
        RedisEntityCache cache = new(connection, prefix);
        IDatabase database = connection.GetDatabase();

        Guid id = Guid.NewGuid();
        EntityKey key = EntityKey.ById("account", id);
        Entity record = new("account", id) { ["name"] = "Contoso" };
        CachedEntity entry = new(record);

        // Write the cache entry without the id index to exercise the scan fallback.
        await database.StringSetAsync(
            prefix + key,
            RedisEntityCache.Serialize(entry),
            TimeSpan.FromMinutes(1)
        );

        Assert.NotNull(await cache.GetAsync(key));

        await cache.RemoveByRecordIdAsync("account", id);

        Assert.Null(await cache.GetAsync(key));
    }

    [RedisFact]
    public async Task RedisCache_UnsupportedAttributeTypeThrows()
    {
        using ConnectionMultiplexer connection = await RedisIntegrationTest.ConnectAsync();
        RedisEntityCache cache = new(connection, RedisIntegrationTest.NewPrefix());
        Entity record = new("account", Guid.NewGuid()) { ["unsupported"] = new object() };

        await Assert.ThrowsAsync<MapperException>(
            () =>
                cache.SetAsync(
                    EntityKey.ById("account", record.Id),
                    new CachedEntity(record),
                    TimeSpan.FromMinutes(1)
                )
        );
    }
}
