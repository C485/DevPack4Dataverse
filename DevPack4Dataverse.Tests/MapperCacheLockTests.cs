using Microsoft.Xrm.Sdk;
using Xunit;
using DevPack4Dataverse.Mapper;
using DevPack4Dataverse.Mapper.Cache;
using DevPack4Dataverse.Mapper.Internal;
using DevPack4Dataverse.Mapper.Lock;

namespace DevPack4Dataverse.Tests;

public class MapperCacheLockTests
{
    [Fact]
    public async Task InMemoryCache_ReturnsEntryBeforeTtl()
    {
        var cache = new InMemoryEntityCache();
        EntityKey key = EntityKey.ById("account", Guid.NewGuid());
        var entry = new CachedEntity(new Entity("account", key.Id!.Value) { ["name"] = "Contoso" }, new[] { "name" });

        await cache.SetAsync(key, entry, TimeSpan.FromMinutes(1));

        Assert.Same(entry, await cache.GetAsync(key));
    }

    [Fact]
    public async Task InMemoryCache_ReusesTheCompletedReadTask()
    {
        var cache = new InMemoryEntityCache();
        EntityKey key = EntityKey.ById("account", Guid.NewGuid());
        var entry = new CachedEntity(
            new Entity("account", key.Id!.Value) { ["name"] = "Contoso" },
            new[] { "name" }
        );

        await cache.SetAsync(key, entry, TimeSpan.FromMinutes(1));

        Task<CachedEntity?> first = cache.GetAsync(key);
        Task<CachedEntity?> second = cache.GetAsync(key);

        Assert.Same(first, second);
        Assert.Same(entry, await first);
    }

    [Fact]
    public async Task InMemoryCache_ExpiresEntry()
    {
        var cache = new InMemoryEntityCache();
        EntityKey key = EntityKey.ById("account", Guid.NewGuid());
        var entry = new CachedEntity(new Entity("account", key.Id!.Value));

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

        await cache.SetAsync(first, new CachedEntity(new Entity("account", first.Id!.Value)), TimeSpan.FromMinutes(1));
        await cache.SetAsync(second, new CachedEntity(new Entity("account", second.Id!.Value)), TimeSpan.FromMinutes(1));
        await cache.SetAsync(other, new CachedEntity(new Entity("contact", other.Id!.Value)), TimeSpan.FromMinutes(1));

        await cache.ClearAsync("account");

        Assert.Null(await cache.GetAsync(first));
        Assert.Null(await cache.GetAsync(second));
        Assert.NotNull(await cache.GetAsync(other));
    }

    [Fact]
    public async Task InMemoryCache_ZeroTtl_RemovesExistingEntry()
    {
        var cache = new InMemoryEntityCache();
        EntityKey key = EntityKey.ById("account", Guid.NewGuid());
        var entry = new CachedEntity(
            new Entity("account", key.Id!.Value) { ["name"] = "Contoso" },
            new[] { "name" }
        );

        await cache.SetAsync(key, entry, TimeSpan.FromMinutes(1));
        await cache.SetAsync(key, entry, TimeSpan.Zero);

        Assert.Null(await cache.GetAsync(key));
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
    public void EntityCopy_DeepCopiesMutableValuesAndFormattedValues()
    {
        Guid id = Guid.NewGuid();
        Guid parentId = Guid.NewGuid();
        byte[] data = { 1, 2, 3 };
        var parent = new EntityReference("account", parentId) { Name = "Parent" };
        parent.KeyAttributes["accountnumber"] = "A-1";
        var entity = new Entity("account", id)
        {
            ["data"] = data,
            ["parentaccountid"] = parent
        };
        entity.FormattedValues["statecode"] = "Active";

        Entity clone = EntityCopy.Clone(entity)!;

        var clonedData = Assert.IsType<byte[]>(clone["data"]);
        Assert.NotSame(data, clonedData);
        Assert.Equal(data, clonedData);

        var clonedParent = Assert.IsType<EntityReference>(clone["parentaccountid"]);
        Assert.NotSame(parent, clonedParent);
        Assert.Equal(parentId, clonedParent.Id);
        Assert.Equal("Parent", clonedParent.Name);
        Assert.Equal("A-1", clonedParent.KeyAttributes["accountnumber"]);
        Assert.Equal("Active", clone.FormattedValues["statecode"]);
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
