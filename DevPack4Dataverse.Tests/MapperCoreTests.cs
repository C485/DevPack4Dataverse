using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Xunit;
using DevPack4Dataverse.Mapper;
using DevPack4Dataverse.Mapper.Cache;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

public class MapperCoreTests
{
    [Fact]
    public async Task PocoToEntity_MapsBothStagesAndCreatesRecord()
    {
        Entity captured = null;
        var fake = new FakeConnection
        {
            CreateRecordHandler = entity =>
            {
                captured = entity;
                return Guid.NewGuid();
            }
        };
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<OrderSource, Entity>("orders")
                .WithLogicalName("account")
                .Map("name", context => context.Source.Name)
                .NextStage()
                .Map(
                    "description",
                    context =>
                        context.Target.GetAttributeValue<string>("name")
                        + ":"
                        + context.Source.Quantity
                )
                .Build();

            var result = await mapper.ExecuteAsync(
                new OrderSource { Name = "Contoso", Quantity = 3 }
            );

            Assert.False(result.HasErrors);
            var record = Assert.Single(result.Records);
            Assert.True(record.Created);
            Assert.NotNull(record.Reference);
            Assert.NotNull(captured);
            Assert.Equal("Contoso", captured.GetAttributeValue<string>("name"));
            Assert.Equal("Contoso:3", captured.GetAttributeValue<string>("description"));
        }
    }

    [Fact]
    public async Task EntityToEntity_MapsSourceAttributes()
    {
        Entity captured = null;
        var fake = new FakeConnection
        {
            CreateRecordHandler = entity =>
            {
                captured = entity;
                return Guid.NewGuid();
            }
        };
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<Entity, Entity>("entity")
                .WithLogicalName("account")
                .Map("name", context => context.Source.GetAttributeValue<string>("name"))
                .Build();

            var result = await mapper.ExecuteAsync(
                new Entity("account") { ["name"] = "Contoso" }
            );

            Assert.False(result.HasErrors);
            Assert.Equal("Contoso", captured.GetAttributeValue<string>("name"));
        }
    }

    [Fact]
    public async Task PocoTarget_MapsWithoutConverterAndDoesNotSave()
    {
        var fake = new FakeConnection();
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<OrderSource, OrderModel>("model")
                .Map(model => model.Name, context => context.Source.Name)
                .Map(model => model.Quantity, context => context.Source.Quantity)
                .Build();

            var result = await mapper.ExecuteAsync(
                new OrderSource { Name = "Contoso", Quantity = 4 }
            );

            var record = Assert.Single(result.Records);
            Assert.Null(record.Reference);
            Assert.Equal("Contoso", record.Target.Name);
            Assert.Equal(4, record.Target.Quantity);
        }
    }

    [Fact]
    public async Task ExistingRecord_UpdatesOnlyChangedAttributes()
    {
        Guid id = Guid.NewGuid();
        Entity current = new("account", id)
        {
            ["name"] = "Old",
            ["creditlimit"] = 10m
        };
        Entity update = null;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ => new[] { current },
            UpdateRecordHandler = entity => update = entity
        };
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<AccountUpdate, Entity>("account-update")
                .WithLogicalName("account")
                .WithEntityKey(context => EntityKey.ById("account", context.Source.Id))
                .Map("name", context => context.Source.Name)
                .Map("creditlimit", context => context.Source.CreditLimit)
                .Build();

            var result = await mapper.ExecuteAsync(
                new AccountUpdate
                {
                    Id = id,
                    Name = "New",
                    CreditLimit = 20m
                }
            );

            var record = Assert.Single(result.Records);
            Assert.False(record.Created);
            Assert.False(record.Skipped);
            Assert.NotNull(update);
            Assert.Equal("New", update["name"]);
            Assert.Equal(20m, update["creditlimit"]);
            Assert.Equal(2, update.Attributes.Count);
        }
    }

    [Fact]
    public async Task ExistingRecord_Unchanged_SkipsSave()
    {
        Guid id = Guid.NewGuid();
        Entity current = new("account", id) { ["name"] = "Same" };
        int updates = 0;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ => new[] { current },
            UpdateRecordHandler = _ => updates++
        };
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<AccountUpdate, Entity>("account-update")
                .WithLogicalName("account")
                .WithEntityKey(context => EntityKey.ById("account", context.Source.Id))
                .Map("name", context => context.Source.Name)
                .Build();

            var result = await mapper.ExecuteAsync(
                new AccountUpdate { Id = id, Name = "Same" }
            );

            Assert.True(result.Records[0].Skipped);
            Assert.Equal(0, updates);
        }
    }

    [Fact]
    public async Task SkipFieldsToChanged_IgnoresConfiguredFields()
    {
        Guid id = Guid.NewGuid();
        Entity current = new("account", id)
        {
            ["name"] = "Same",
            ["modifiedon"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        int updates = 0;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ => new[] { current },
            UpdateRecordHandler = _ => updates++
        };
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<AccountUpdate, Entity>("account-update")
                .WithLogicalName("account")
                .WithEntityKey(context => EntityKey.ById("account", context.Source.Id))
                .Map("name", context => context.Source.Name)
                .Map(
                    "modifiedon",
                    context => new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                )
                .WithOptions(options => options.SkipFieldsToChanged.Add("modifiedon"))
                .Build();

            var result = await mapper.ExecuteAsync(
                new AccountUpdate { Id = id, Name = "Same" }
            );

            Assert.True(result.Records[0].Skipped);
            Assert.Equal(0, updates);
        }
    }

    [Fact]
    public async Task PreSave_RunsBeforeChangeDetection()
    {
        Guid id = Guid.NewGuid();
        Entity current = new("account", id) { ["name"] = "Old" };
        Entity update = null;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ => new[] { current },
            UpdateRecordHandler = entity => update = entity
        };
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<AccountUpdate, Entity>("account-update")
                .WithLogicalName("account")
                .WithEntityKey(context => EntityKey.ById("account", context.Source.Id))
                .Map("name", context => context.Source.Name)
                .WithOptions(
                    options =>
                        options.PreSave = context => context.Target["name"] = "PreSaved"
                )
                .Build();

            var result = await mapper.ExecuteAsync(
                new AccountUpdate { Id = id, Name = "Mapped" }
            );

            Assert.False(result.Records[0].Skipped);
            Assert.Equal("PreSaved", update["name"]);
        }
    }

    [Fact]
    public async Task PocoTarget_WithConverter_SavesEntity()
    {
        Entity captured = null;
        Guid id = Guid.NewGuid();
        var fake = new FakeConnection
        {
            CreateRecordHandler = entity =>
            {
                captured = entity;
                return id;
            }
        };
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<OrderSource, OrderModel>("model")
                .Map(model => model.Name, context => context.Source.Name)
                .Map(model => model.Quantity, context => context.Source.Quantity)
                .WithConverter(new OrderConverter())
                .Build();

            var result = await mapper.ExecuteAsync(
                new OrderSource { Name = "Contoso", Quantity = 5 }
            );

            Assert.Equal(id, result.Records[0].Reference.Id);
            Assert.NotNull(captured);
            Assert.Equal("account", captured.LogicalName);
            Assert.Equal("Contoso", captured["name"]);
        }
    }

    [Fact]
    public async Task RequestSettings_ArePassedToDataverse()
    {
        var settings = new DevPack4Dataverse.Models.RequestSettings
        {
            ImpersonateAsUserByDataverseId = Guid.NewGuid()
        };
        var fake = new FakeConnection { CreateRecordHandler = _ => Guid.NewGuid() };
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<OrderSource, Entity>("orders")
                .WithLogicalName("account")
                .Map("name", context => context.Source.Name)
                .WithOptions(options => options.RequestSettings = settings)
                .Build();

            await mapper.ExecuteAsync(new OrderSource { Name = "Contoso" });

            Assert.Same(settings, fake.LastRequestSettings);
        }
    }

    [Fact]
    public async Task AlternateKeySave_UpsertsAndReturnsReference()
    {
        Guid id = Guid.NewGuid();
        UpsertRequest captured = null;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ => Array.Empty<Entity>(),
            ExecuteHandler = request =>
            {
                captured = Assert.IsType<UpsertRequest>(request);
                UpsertResponse response = new();
                response.Results["Target"] = new EntityReference("account", id);
                response.Results["RecordCreated"] = true;
                return response;
            }
        };
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<AlternateKeySource, Entity>("alternate")
                .WithLogicalName("account")
                .WithEntityKey(
                    context =>
                        EntityKey.ByAlternateKey(
                            "account",
                            new KeyValuePair<string, object>(
                                "accountnumber",
                                context.Source.AccountNumber
                            )
                        )
                )
                .Map("name", context => context.Source.Name)
                .Build();

            var result = await mapper.ExecuteAsync(
                new AlternateKeySource { AccountNumber = "A-1", Name = "Contoso" }
            );

            var record = Assert.Single(result.Records);
            Assert.True(record.Created);
            Assert.Equal(id, record.Reference.Id);
            Assert.NotNull(captured);
            Assert.Equal("A-1", captured.Target.KeyAttributes["accountnumber"]);
        }
    }

    [Fact]
    public async Task SaveDiff_ReadsFreshRecordEvenWhenCacheHasCopy()
    {
        Guid id = Guid.NewGuid();
        var cache = new InMemoryEntityCache();
        await cache.SetAsync(
            EntityKey.ById("account", id),
            new CachedEntity(new Entity("account", id) { ["name"] = "Cached" }, new[] { "name" }),
            TimeSpan.FromMinutes(1)
        );
        Entity update = null;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ => new[]
            {
                new Entity("account", id) { ["name"] = "Database" }
            },
            UpdateRecordHandler = entity => update = entity
        };
        var (proxy, wrapper) = Create(fake, null, cache);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<AccountUpdate, Entity>("account-update")
                .WithLogicalName("account")
                .WithEntityKey(context => EntityKey.ById("account", context.Source.Id))
                .Map("name", context => context.Source.Name)
                .Build();

            var result = await mapper.ExecuteAsync(
                new AccountUpdate { Id = id, Name = "Cached" }
            );

            Assert.False(result.Records[0].Skipped);
            Assert.NotNull(update);
            Assert.Equal("Cached", update["name"]);
        }
    }

    [Fact]
    public async Task Update_InvalidatesAlternateKeyCacheEntries()
    {
        Guid id = Guid.NewGuid();
        Entity cached = new("account", id)
        {
            ["name"] = "Old",
            ["accountnumber"] = "A-1"
        };
        EntityKey alternateKey = EntityKey.ByAlternateKey(
            "account",
            new KeyValuePair<string, object>("accountnumber", "A-1")
        );
        var cache = new InMemoryEntityCache();
        await cache.SetAsync(
            EntityKey.ById("account", id),
            new CachedEntity(cached, new[] { "name", "accountnumber" }),
            TimeSpan.FromMinutes(1)
        );
        await cache.SetAsync(
            alternateKey,
            new CachedEntity(cached, new[] { "name", "accountnumber" }),
            TimeSpan.FromMinutes(1)
        );
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ => new[]
            {
                new Entity("account", id) { ["name"] = "Old", ["accountnumber"] = "A-1" }
            },
            UpdateRecordHandler = _ => { }
        };
        var (proxy, wrapper) = Create(fake, null, cache);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<AccountUpdate, Entity>("account-update")
                .WithLogicalName("account")
                .WithEntityKey(context => EntityKey.ById("account", context.Source.Id))
                .Map("name", context => context.Source.Name)
                .Build();

            await mapper.ExecuteAsync(new AccountUpdate { Id = id, Name = "New" });

            Assert.Null(await cache.GetAsync(alternateKey));
        }
    }

    [Fact]
    public async Task CacheFailure_DoesNotFailSuccessfulCreate()
    {
        Guid id = Guid.NewGuid();
        var fake = new FakeConnection { CreateRecordHandler = _ => id };
        var (proxy, wrapper) = Create(fake, null, new ThrowingCache());

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<OrderSource, Entity>("orders")
                .WithLogicalName("account")
                .Map("name", context => context.Source.Name)
                .Build();

            var result = await mapper.ExecuteAsync(new OrderSource { Name = "Contoso" });

            Assert.False(result.HasErrors);
            Assert.Equal(id, result.Records[0].Reference.Id);
        }
    }

    private static (SdkProxy Proxy, MapperWrapper Wrapper) Create(
        FakeConnection fake,
        MapperWrapperOptions options = null,
        IEntityCache cache = null
    )
    {
        SdkProxy proxy = new(NullLogger.Instance, false, new FakeConnectionCreator(fake));
        proxy.WaitForConnections(1);
        return (proxy, new MapperWrapper(proxy, NullLogger.Instance, options, cache));
    }

    public sealed class OrderSource
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class OrderModel
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class AccountUpdate
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public decimal CreditLimit { get; set; }
    }

    public sealed class AlternateKeySource
    {
        public string AccountNumber { get; set; }
        public string Name { get; set; }
    }

    private sealed class OrderConverter : IModelEntityConverter<OrderModel>
    {
        public Entity ToEntity(OrderModel model) =>
            new("account")
            {
                ["name"] = model.Name,
                ["quantity"] = model.Quantity
            };
    }

    private sealed class ThrowingCache : IEntityCache
    {
        public Task<CachedEntity> GetAsync(EntityKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult<CachedEntity>(null);

        public Task SetAsync(
            EntityKey key,
            CachedEntity entry,
            TimeSpan timeToLive,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("cache unavailable");

        public Task RemoveAsync(EntityKey key, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("cache unavailable");

        public Task RemoveByRecordIdAsync(
            string logicalName,
            Guid id,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("cache unavailable");

        public Task ClearAsync(string logicalName, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("cache unavailable");
    }
}
