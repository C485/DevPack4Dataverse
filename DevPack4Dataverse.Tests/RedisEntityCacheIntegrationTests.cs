using DevPack4Dataverse.Mapper;
using DevPack4Dataverse.Mapper.Cache;
using DevPack4Dataverse.Mapper.Redis;
using Microsoft.Xrm.Sdk;
using StackExchange.Redis;
using Xunit;

namespace DevPack4Dataverse.Tests;

internal sealed class RedisFactAttribute : FactAttribute
{
    internal const string ConnectionVariable = "DEVPACK_REDIS_CONNECTION";

    public RedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
        {
            Skip = $"Set {ConnectionVariable} to a Redis connection string (for example 127.0.0.1:6390,allowAdmin=true) to run this test.";
        }
    }
}

public sealed class RedisEntityCacheIntegrationTests
{
    [RedisFact]
    public async Task RedisCache_RoundTrip_Expiry_RemoveByIdAndClear()
    {
        string connectionString = Environment.GetEnvironmentVariable(
            RedisFactAttribute.ConnectionVariable
        )!;

        using ConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(
            connectionString
        );
        string prefix = $"devpack:test:{Guid.NewGuid():N}:";
        RedisEntityCache cache = new(connection, prefix);

        Guid accountId = Guid.NewGuid();
        Entity account = new("account", accountId)
        {
            ["name"] = "Contoso",
            ["creditlimit"] = new Money(12.34m),
            ["createdon"] = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc)
        };
        account.KeyAttributes["accountnumber"] = "A-1";
        CachedEntity accountEntry = new(account, new[] { "name", "creditlimit" });
        EntityKey accountKey = EntityKey.ById("account", accountId);

        await cache.SetAsync(accountKey, accountEntry, TimeSpan.FromMinutes(1));

        CachedEntity? loaded = await cache.GetAsync(accountKey);
        Assert.NotNull(loaded);
        Assert.Equal("Contoso", loaded.Record.GetAttributeValue<string>("name"));
        Assert.Equal(12.34m, loaded.Record.GetAttributeValue<Money>("creditlimit").Value);
        Assert.Equal(
            new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            loaded.Record.GetAttributeValue<DateTime>("createdon")
        );
        Assert.Equal("A-1", loaded.Record.KeyAttributes["accountnumber"]);
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
        await cache.ClearAsync("account");
        Assert.Null(await cache.GetAsync(accountKey));
        Assert.NotNull(await cache.GetAsync(contactKey));

        await cache.RemoveAsync(contactKey);
        Assert.Null(await cache.GetAsync(contactKey));
    }
}
