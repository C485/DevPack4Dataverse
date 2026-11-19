using Microsoft.Xrm.Sdk;
using Xunit;
using DevPack4Dataverse.Mapper;
using DevPack4Dataverse.Mapper.Cache;
using DevPack4Dataverse.Mapper.Lock;

namespace DevPack4Dataverse.Tests;

public class MapperCacheLockTests
{
    [Fact]
    public async Task InMemoryCache_ReturnsEntryBeforeTtl()
    {
        var cache = new InMemoryEntityCache();
        EntityKey key = EntityKey.ById("account", Guid.NewGuid());
        var entry = new CachedEntity(new Entity("account", key.Id.Value) { ["name"] = "Contoso" }, new[] { "name" });

        await cache.SetAsync(key, entry, TimeSpan.FromMinutes(1));

        Assert.Same(entry, await cache.GetAsync(key));
    }

    [Fact]
    public async Task InMemoryCache_ExpiresEntry()
    {
        var cache = new InMemoryEntityCache();
        EntityKey key = EntityKey.ById("account", Guid.NewGuid());
        var entry = new CachedEntity(new Entity("account", key.Id.Value));

        await cache.SetAsync(key, entry, TimeSpan.FromMilliseconds(20));
        await Task.Delay(80);

        Assert.Null(await cache.GetAsync(key));
    }

    [Fact]
    public async Task InMemoryCache_ClearRemovesAllEntityEntries()
    {
        var cache = new InMemoryEntityCache();
        EntityKey first = EntityKey.ById("account", Guid.NewGuid());
        EntityKey second = EntityKey.ById("account", Guid.NewGuid());
        EntityKey other = EntityKey.ById("contact", Guid.NewGuid());

        await cache.SetAsync(first, new CachedEntity(new Entity("account", first.Id.Value)), TimeSpan.FromMinutes(1));
        await cache.SetAsync(second, new CachedEntity(new Entity("account", second.Id.Value)), TimeSpan.FromMinutes(1));
        await cache.SetAsync(other, new CachedEntity(new Entity("contact", other.Id.Value)), TimeSpan.FromMinutes(1));

        await cache.ClearAsync("account");

        Assert.Null(await cache.GetAsync(first));
        Assert.Null(await cache.GetAsync(second));
        Assert.NotNull(await cache.GetAsync(other));
    }

    [Fact]
    public async Task InMemoryLock_BlocksUntilReleased()
    {
        var entityLock = new InMemoryEntityLock();
        EntityKey key = EntityKey.ById("account", Guid.NewGuid());

        using (IEntityLockHandle handle = await entityLock.AcquireAsync(key, TimeSpan.FromSeconds(1)))
        {
            await Assert.ThrowsAsync<MapperLockException>(
                () => entityLock.AcquireAsync(key, TimeSpan.FromMilliseconds(20))
            );
        }

        using IEntityLockHandle second = await entityLock.AcquireAsync(
            key,
            TimeSpan.FromSeconds(1)
        );
        Assert.NotNull(second);
    }

    [Fact]
    public void WrapperOptions_UsesPerTableTtl()
    {
        var options = new MapperWrapperOptions { DefaultCacheTtl = TimeSpan.FromMinutes(5) };
        options.CacheTtlByEntity["account"] = TimeSpan.FromMinutes(1);

        Assert.Equal(TimeSpan.FromMinutes(1), options.GetCacheTtl("account"));
        Assert.Equal(TimeSpan.FromMinutes(5), options.GetCacheTtl("contact"));
    }
}
