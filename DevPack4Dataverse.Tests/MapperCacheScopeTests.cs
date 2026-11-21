using DevPack4Dataverse.Mapper;
using DevPack4Dataverse.Mapper.Cache;
using DevPack4Dataverse.Mapper.Internal;
using DevPack4Dataverse.Models;
using DevPack4Dataverse.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace DevPack4Dataverse.Tests;

public class MapperCacheScopeTests
{
    [Fact]
    public async Task ImpersonatedRead_BypassesAndRemovesUnscopedCacheEntry()
    {
        Guid id = Guid.NewGuid();
        EntityKey key = EntityKey.ById("account", id);
        var cache = new InMemoryEntityCache();
        await cache.SetAsync(
            key,
            new CachedEntity(
                new Entity("account", id) { ["name"] = "stale" },
                new[] { "name" }
            ),
            TimeSpan.FromMinutes(1)
        );
        int retrievals = 0;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ =>
            {
                retrievals++;
                return new[] { new Entity("account", id) { ["name"] = "fresh" } };
            }
        };
        using var proxy = new SdkProxy(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(fake)
        );
        proxy.WaitForConnections(1);
        var store = new RecordStore(
            proxy,
            NullLogger.Instance,
            cache,
            new MapperWrapperOptions(),
            new PrimaryKeyAttributeProvider(proxy)
        );
        var settings = new RequestSettings
        {
            ImpersonateAsUserByDataverseId = Guid.NewGuid()
        };

        Entity? fresh = await store.GetAsync(
            key,
            new[] { "name" },
            true,
            false,
            settings,
            CancellationToken.None
        );

        Assert.NotNull(fresh);
        Assert.Equal("fresh", fresh!["name"]);
        Assert.Equal(1, retrievals);
        Assert.Null(await cache.GetAsync(key));
    }

    [Fact]
    public async Task ImpersonatedCreate_DoesNotWriteCache()
    {
        Guid id = Guid.NewGuid();
        var cache = new InMemoryEntityCache();
        var fake = new FakeConnection { CreateRecordHandler = _ => id };
        using var proxy = new SdkProxy(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(fake)
        );
        proxy.WaitForConnections(1);
        var wrapper = new MapperWrapper(proxy, NullLogger.Instance, null, cache);
        var mapper = wrapper
            .CreateMapperBuilder<MapperCoreTests.OrderSource, Entity>("orders")
            .WithLogicalName("account")
            .Map("name", context => context.Source.Name)
            .WithOptions(
                options =>
                    options.RequestSettings.ImpersonateAsUserByDataverseId = Guid.NewGuid()
            )
            .Build();

        MappingResult<Entity> result = await mapper.ExecuteAsync(
            new MapperCoreTests.OrderSource { Name = "Contoso" }
        );

        Assert.Single(result.Records);
        Assert.Null(await cache.GetAsync(EntityKey.ById("account", id)));
    }

    [Fact]
    public async Task NormalCreate_WritesCache()
    {
        Guid id = Guid.NewGuid();
        var cache = new InMemoryEntityCache();
        var fake = new FakeConnection { CreateRecordHandler = _ => id };
        using var proxy = new SdkProxy(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(fake)
        );
        proxy.WaitForConnections(1);
        var wrapper = new MapperWrapper(proxy, NullLogger.Instance, null, cache);
        var mapper = wrapper
            .CreateMapperBuilder<MapperCoreTests.OrderSource, Entity>("orders")
            .WithLogicalName("account")
            .Map("name", context => context.Source.Name)
            .Build();

        await mapper.ExecuteAsync(new MapperCoreTests.OrderSource { Name = "Contoso" });

        Assert.NotNull(await cache.GetAsync(EntityKey.ById("account", id)));
    }
}
