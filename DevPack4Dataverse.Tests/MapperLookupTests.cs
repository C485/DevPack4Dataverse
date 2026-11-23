using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Xunit;
using DevPack4Dataverse.Mapper;
using DevPack4Dataverse.Mapper.Cache;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

public class MapperLookupTests
{
    [Fact]
    public void EntityKey_SingleAlternateAttributeOverload()
    {
        EntityKey key = EntityKey.ByAlternateKey("account", "accountnumber", "A-1");

        Assert.Equal("account", key.LogicalName);
        Assert.False(key.IsEmpty);
        Assert.Equal("A-1", key.AlternateKey["accountnumber"]);
    }

    [Fact]
    public void EntityKey_AlternateReferenceValues_DoNotCollide()
    {
        EntityReference first = new("account");
        first.KeyAttributes["accountnumber"] = "A-1";
        EntityReference second = new("account");
        second.KeyAttributes["accountnumber"] = "A-2";

        EntityKey firstKey = EntityKey.ByAlternateKey(
            "contact",
            "parentcustomerid",
            first
        );
        EntityKey secondKey = EntityKey.ByAlternateKey(
            "contact",
            "parentcustomerid",
            second
        );

        Assert.NotEqual(firstKey, secondKey);
    }

    [Fact]
    public void EntityKey_DecimalScale_DoesNotChangeIdentity()
    {
        EntityKey first = EntityKey.ByAlternateKey("account", "amount", 1.0m);
        EntityKey second = EntityKey.ByAlternateKey("account", "amount", 1.00m);

        Assert.Equal(first, second);
        Assert.Equal(first.ToString(), second.ToString());
    }

    [Fact]
    public void EntityKey_FromEntityReference_PrefersId()
    {
        Guid id = Guid.NewGuid();
        var reference = new EntityReference("account", id);
        reference.KeyAttributes["accountnumber"] = "A-1";

        EntityKey? key = EntityKey.FromEntityReference(reference);

        Assert.NotNull(key);
        Assert.Equal(id, key!.Id);
        Assert.False(key.IsAlternateKey);
    }

    [Fact]
    public void EntityKey_CompositeWithEmptyPart_IsEmpty()
    {
        EntityKey nullPart = EntityKey.ByAlternateKey(
            "account",
            new Dictionary<string, object>
            {
                ["accountnumber"] = "A-1",
                ["new_code"] = null!
            }
        );
        EntityKey blankPart = EntityKey.ByAlternateKey(
            "account",
            new Dictionary<string, object>
            {
                ["accountnumber"] = "A-1",
                ["new_code"] = ""
            }
        );

        Assert.True(nullPart.IsEmpty);
        Assert.True(blankPart.IsEmpty);
    }

    [Fact]
    public void EntityKey_TwoPairAlternateKeyOverload()
    {
        EntityKey key = EntityKey.ByAlternateKey(
            "account",
            new KeyValuePair<string, object>("accountnumber", "A-1"),
            new KeyValuePair<string, object>("name", "Contoso")
        );

        Assert.Equal("A-1", key.AlternateKey["accountnumber"]);
        Assert.Equal("Contoso", key.AlternateKey["name"]);
    }

    [Fact]
    public void AlternateKey_WithSeparatorInValue_DoesNotCollide()
    {
        EntityKey first = EntityKey.ByAlternateKey(
            "account",
            new Dictionary<string, object> { ["a"] = "1;b=2", ["b"] = "3" }
        );
        EntityKey second = EntityKey.ByAlternateKey(
            "account",
            new Dictionary<string, object> { ["a"] = "1", ["b"] = "2;b=3" }
        );

        Assert.NotEqual(first.ToString(), second.ToString());
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task BasicKey_ResolvesLookupAndRequestsOnlyConfiguredColumns()
    {
        Guid id = Guid.NewGuid();
        QueryExpression? captured = null;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = query =>
            {
                captured = query;
                return new[] { new Entity("account", id) { ["name"] = "Contoso" } };
            }
        };
        var (proxy, wrapper) = Create(fake);
        var options = new LookupMappingOptions();
        options.Fields.Add("name");

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    context => EntityKey.ById("account", context.Source.AccountId),
                    options
                )
                .Build();

            var result = await mapper.ExecuteAsync(
                new LookupSource { AccountId = id }
            );

            Assert.False(result.HasErrors);
            Assert.Equal(id, result.Records[0].Target.Account.Id);
            Assert.Contains("name", captured!.ColumnSet.Columns);
            Assert.DoesNotContain("email", captured!.ColumnSet.Columns);
            Assert.Contains(
                captured!.Criteria.Conditions,
                condition =>
                    condition.AttributeName == "accountid"
                    && Equals(condition.Values[0], id)
            );
            Assert.Null(captured.TopCount);
        }
    }

    [Fact]
    public async Task AlternateKeyLookup_WithMultipleMatches_Throws()
    {
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ => new[]
            {
                new Entity("account", Guid.NewGuid()) { ["accountnumber"] = "A-1" },
                new Entity("account", Guid.NewGuid()) { ["accountnumber"] = "A-1" }
            }
        };
        var (proxy, wrapper) = Create(fake);
        var options = new LookupMappingOptions();

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("multi-match")
                .MapLookup(
                    target => target.Account,
                    context =>
                        EntityKey.ByAlternateKey(
                            "account",
                            "accountnumber",
                            context.Source.AccountNumber
                        ),
                    options
                )
                .Build();

            await Assert.ThrowsAsync<MapperException>(
                () => mapper.ExecuteAsync(new LookupSource { AccountNumber = "A-1" })
            );
        }
    }

    [Fact]
    public async Task AlternateKey_ResolvesLookup()
    {
        Guid id = Guid.NewGuid();
        QueryExpression? captured = null;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = query =>
            {
                captured = query;
                return new[]
                {
                    new Entity("account", id) { ["accountnumber"] = "A-1", ["name"] = "Contoso" }
                };
            }
        };
        var (proxy, wrapper) = Create(fake);
        var options = new LookupMappingOptions();
        options.Fields.Add("name");
        options.AlternateKeyAttributes.Add("accountnumber");

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    context =>
                        EntityKey.ByAlternateKey(
                            "account",
                            new KeyValuePair<string, object>(
                                "accountnumber",
                                context.Source.AccountNumber
                            )
                        ),
                    options
                )
                .Build();

            var result = await mapper.ExecuteAsync(
                new LookupSource { AccountNumber = "A-1" }
            );

            Assert.Equal(id, result.Records[0].Target.Account.Id);
            Assert.Contains(
                captured!.Criteria.Conditions,
                condition =>
                    condition.AttributeName == "accountnumber"
                    && Equals(condition.Values[0], "A-1")
            );
        }
    }

    [Fact]
    public async Task Cache_ReusesRecordAcrossExecutions()
    {
        Guid id = Guid.NewGuid();
        int retrievals = 0;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ =>
            {
                retrievals++;
                return new[] { new Entity("account", id) { ["name"] = "Contoso" } };
            }
        };
        var (proxy, wrapper) = Create(fake);
        var options = new LookupMappingOptions();
        options.Fields.Add("name");

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    context => EntityKey.ById("account", context.Source.AccountId),
                    options
                )
                .Build();

            await mapper.ExecuteAsync(new LookupSource { AccountId = id });
            await mapper.ExecuteAsync(new LookupSource { AccountId = id });

            Assert.Equal(1, retrievals);
        }
    }

    [Fact]
    public async Task UseCacheFalse_AlwaysRetrieves()
    {
        Guid id = Guid.NewGuid();
        int retrievals = 0;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ =>
            {
                retrievals++;
                return new[] { new Entity("account", id) };
            }
        };
        var (proxy, wrapper) = Create(fake);
        var options = new LookupMappingOptions { UseCache = false };

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    context => EntityKey.ById("account", context.Source.AccountId),
                    options
                )
                .Build();

            await mapper.ExecuteAsync(new LookupSource { AccountId = id });
            await mapper.ExecuteAsync(new LookupSource { AccountId = id });

            Assert.Equal(2, retrievals);
        }
    }

    [Fact]
    public async Task ForceRefresh_BypassesAndRefreshesCache()
    {
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        int retrievals = 0;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ =>
            {
                retrievals++;
                return new[] { new Entity("account", retrievals == 1 ? firstId : secondId) };
            }
        };
        var (proxy, wrapper) = Create(fake);
        var options = new LookupMappingOptions();

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    context => EntityKey.ById("account", context.Source.AccountId),
                    options
                )
                .Build();

            var first = await mapper.ExecuteAsync(new LookupSource { AccountId = firstId });
            var second = await mapper.ExecuteAsync(
                new LookupSource { AccountId = firstId },
                new MappingRunOptions { ForceRefresh = true }
            );

            Assert.Equal(firstId, first.Records[0].Target.Account.Id);
            Assert.Equal(secondId, second.Records[0].Target.Account.Id);
            Assert.Equal(2, retrievals);
        }
    }

    [Fact]
    public async Task NewFields_RetrievesOnlyMissingColumns()
    {
        Guid id = Guid.NewGuid();
        List<ColumnSet> columnSets = new();
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = query =>
            {
                columnSets.Add(query.ColumnSet);
                return new[] { new Entity("account", id) { ["name"] = "Contoso", ["email"] = "a@b.c" } };
            }
        };
        var (proxy, wrapper) = Create(fake);
        var options = new LookupMappingOptions();
        options.Fields.Add("name");

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    context => EntityKey.ById("account", context.Source.AccountId),
                    options
                )
                .Build();

            await mapper.ExecuteAsync(new LookupSource { AccountId = id });

            LookupMappingOptions secondOptions = new();
            secondOptions.Fields.Add("email");
            var secondMapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup-email")
                .MapLookup(
                    target => target.Account,
                    context => EntityKey.ById("account", context.Source.AccountId),
                    secondOptions
                )
                .Build();
            await secondMapper.ExecuteAsync(new LookupSource { AccountId = id });

            Assert.Equal(2, columnSets.Count);
            Assert.DoesNotContain("name", columnSets[1].Columns);
            Assert.Contains("email", columnSets[1].Columns);
        }
    }

    [Fact]
    public async Task RetrieveAllRecords_LoadsTableOncePerRun()
    {
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        int retrievals = 0;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ =>
            {
                retrievals++;
                return new[]
                {
                    new Entity("account", firstId) { ["name"] = "First" },
                    new Entity("account", secondId) { ["name"] = "Second" }
                };
            }
        };
        var (proxy, wrapper) = Create(fake);
        var options = new LookupMappingOptions
        {
            RetrievalMode = LookupRetrievalMode.RetrieveAllRecords
        };
        options.Fields.Add("name");

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupPairs>("lookup")
                .MapLookup(
                    target => target.First,
                    context => EntityKey.ById("account", context.Source.AccountId),
                    options
                )
                .MapLookup(
                    target => target.Second,
                    context => EntityKey.ById("account", context.Source.OtherAccountId),
                    options
                )
                .Build();

            var result = await mapper.ExecuteAsync(
                new LookupSource
                {
                    AccountId = firstId,
                    OtherAccountId = secondId
                }
            );

            Assert.Equal(firstId, result.Records[0].Target.First.Id);
            Assert.Equal(secondId, result.Records[0].Target.Second.Id);
            Assert.Equal(1, retrievals);
        }
    }

    [Fact]
    public async Task RetrieveAllRecords_WithImpersonation_ThrowsConfigurationException()
    {
        int retrievals = 0;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ =>
            {
                retrievals++;
                return Array.Empty<Entity>();
            }
        };
        var (proxy, wrapper) = Create(fake);
        var options = new LookupMappingOptions
        {
            RetrievalMode = LookupRetrievalMode.RetrieveAllRecords
        };
        options.Fields.Add("name");

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    context => EntityKey.ById("account", context.Source.AccountId),
                    options
                )
                .WithOptions(
                    options =>
                        options.RequestSettings.ImpersonateAsUserByDataverseId =
                            Guid.NewGuid()
                )
                .Build();

            await Assert.ThrowsAsync<MapperConfigurationException>(
                () =>
                    mapper.ExecuteAsync(
                        new LookupSource { AccountId = Guid.NewGuid() }
                    )
            );
            Assert.Equal(0, retrievals);
        }
    }

    [Fact]
    public async Task RetrieveAllRecords_ReusesPreloadAcrossRuns()
    {
        Guid accountId = Guid.NewGuid();
        int retrievals = 0;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ =>
            {
                retrievals++;
                return new[] { new Entity("account", accountId) { ["name"] = "First" } };
            }
        };
        var (proxy, wrapper) = Create(fake);
        var options = new LookupMappingOptions
        {
            RetrievalMode = LookupRetrievalMode.RetrieveAllRecords
        };
        options.Fields.Add("name");

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    context => EntityKey.ById("account", context.Source.AccountId),
                    options
                )
                .Build();

            await mapper.ExecuteAsync(new LookupSource { AccountId = accountId });
            await mapper.ExecuteAsync(new LookupSource { AccountId = accountId });

            Assert.Equal(1, retrievals);
        }
    }

    [Fact]
    public async Task CreateIfNotExists_CreatesBasicKeyRecord()
    {
        Guid id = Guid.NewGuid();
        int creates = 0;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ => Array.Empty<Entity>(),
            ExecuteHandler = request =>
            {
                if (request is CreateRequest)
                {
                    creates++;
                    CreateResponse response = new();
                    response.Results["id"] = id;
                    return response;
                }

                throw new InvalidOperationException(request.RequestName);
            }
        };
        var (proxy, wrapper) = Create(fake);
        var options = new LookupMappingOptions { CreateIfNotExists = true };

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    context => EntityKey.ById("account", context.Source.AccountId),
                    options,
                    _ => new Entity("account") { ["name"] = "Created" }
                )
                .Build();

            var result = await mapper.ExecuteAsync(
                new LookupSource { AccountId = id }
            );

            Assert.False(result.HasErrors);
            Assert.Equal(id, result.Records[0].Target.Account.Id);
            Assert.Equal(1, creates);
        }
    }

    [Fact]
    public async Task ThrowIfNotFoundFalse_AddsMappingError()
    {
        var fake = new FakeConnection { RetrieveMultipleHandler = _ => Array.Empty<Entity>() };
        var (proxy, wrapper) = Create(fake);
        var options = new LookupMappingOptions { ThrowIfNotFound = false };

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    context => EntityKey.ById("account", context.Source.AccountId),
                    options
                )
                .Build();

            var result = await mapper.ExecuteAsync(
                new LookupSource { AccountId = Guid.NewGuid() }
            );

            Assert.True(result.HasErrors);
            Assert.Null(result.Records[0].Target.Account);
        }
    }

    [Fact]
    public async Task ThrowIfNotFoundTrue_Throws()
    {
        var fake = new FakeConnection { RetrieveMultipleHandler = _ => Array.Empty<Entity>() };
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    context => EntityKey.ById("account", context.Source.AccountId),
                    new LookupMappingOptions()
                )
                .Build();

            await Assert.ThrowsAsync<MapperNotFoundException>(
                () => mapper.ExecuteAsync(new LookupSource { AccountId = Guid.NewGuid() })
            );
        }
    }

    [Fact]
    public async Task ThrowIfEmpty_Throws()
    {
        var fake = new FakeConnection();
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    _ => null!,
                    new LookupMappingOptions { ThrowIfEmpty = true }
                )
                .Build();

            await Assert.ThrowsAsync<MapperException>(
                () => mapper.ExecuteAsync(new LookupSource())
            );
        }
    }

    [Fact]
    public async Task RetrieveAllRecords_ResolvesAlternateKeysFromPreloadedTable()
    {
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        int retrievals = 0;
        QueryExpression? preloadQuery = null;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = query =>
            {
                preloadQuery = query;
                retrievals++;
                return new[]
                {
                    new Entity("account", firstId) { ["accountnumber"] = "A-1" },
                    new Entity("account", secondId) { ["accountnumber"] = "A-2" }
                };
            }
        };
        var (proxy, wrapper) = Create(fake);
        var options = new LookupMappingOptions
        {
            RetrievalMode = LookupRetrievalMode.RetrieveAllRecords
        };
        options.AlternateKeyAttributes.Add("accountnumber");

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<AlternateLookupSource, LookupPairs>("lookup")
                .MapLookup(
                    target => target.First,
                    context =>
                        EntityKey.ByAlternateKey(
                            "account",
                            new KeyValuePair<string, object>(
                                "accountnumber",
                                context.Source.FirstNumber
                            )
                        ),
                    options
                )
                .MapLookup(
                    target => target.Second,
                    context =>
                        EntityKey.ByAlternateKey(
                            "account",
                            new KeyValuePair<string, object>(
                                "accountnumber",
                                context.Source.SecondNumber
                            )
                        ),
                    options
                )
                .Build();

            var result = await mapper.ExecuteAsync(
                new AlternateLookupSource { FirstNumber = "A-1", SecondNumber = "A-2" }
            );

            Assert.Equal(firstId, result.Records[0].Target.First.Id);
            Assert.Equal(secondId, result.Records[0].Target.Second.Id);
            Assert.Equal(1, retrievals);
            Assert.NotNull(preloadQuery);
            Assert.Equal("accountid", Assert.Single(preloadQuery!.Orders).AttributeName);
        }
    }

    [Fact]
    public async Task ThrowIfEmptyFalse_AddsMappingError()
    {
        var fake = new FakeConnection();
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    _ => null!,
                    new LookupMappingOptions { ThrowIfEmpty = false }
                )
                .Build();

            var result = await mapper.ExecuteAsync(new LookupSource());

            Assert.True(result.HasErrors);
            Assert.Null(result.Records[0].Target.Account);
        }
    }

    [Fact]
    public async Task CacheReadFailure_FallsBackToDataverse()
    {
        Guid id = Guid.NewGuid();
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ => new[] { new Entity("account", id) }
        };
        var (proxy, wrapper) = Create(fake, new ThrowingReadCache());

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    context => EntityKey.ById("account", context.Source.AccountId),
                    new LookupMappingOptions()
                )
                .Build();

            var result = await mapper.ExecuteAsync(
                new LookupSource { AccountId = id }
            );

            Assert.False(result.HasErrors);
            Assert.Equal(id, result.Records[0].Target.Account.Id);
        }
    }

    [Fact]
    public async Task UseCacheFalse_CreatesInsteadOfReturningStaleCacheEntry()
    {
        Guid staleId = Guid.NewGuid();
        int creates = 0;
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ => Array.Empty<Entity>(),
            ExecuteHandler = request =>
            {
                if (request is CreateRequest)
                {
                    creates++;
                    return new CreateResponse();
                }

                throw new InvalidOperationException($"Unexpected request {request.RequestName}.");
            }
        };
        var (proxy, wrapper) = Create(fake);
        var options = new LookupMappingOptions
        {
            UseCache = false,
            CreateIfNotExists = true
        };
        options.Fields.Add("name");

        using (proxy)
        {
            await wrapper.Cache.SetAsync(
                EntityKey.ById("account", staleId),
                new CachedEntity(
                    new Entity("account", staleId) { ["name"] = "Stale" },
                    new[] { "name" }
                ),
                TimeSpan.FromMinutes(10)
            );

            var mapper = wrapper
                .CreateMapperBuilder<LookupSource, LookupTarget>("lookup")
                .MapLookup(
                    target => target.Account,
                    context => EntityKey.ById("account", context.Source.AccountId),
                    options,
                    context => new Entity("account") { ["name"] = "Created" }
                )
                .Build();

            var result = await mapper.ExecuteAsync(
                new LookupSource { AccountId = staleId }
            );

            Assert.False(result.HasErrors);
            Assert.Equal(1, creates);
        }
    }

    private static (SdkProxy Proxy, MapperWrapper Wrapper) Create(
        FakeConnection fake,
        IEntityCache? cache = null
    )
    {
        SdkProxy proxy = new(NullLogger.Instance, false, new FakeConnectionCreator(fake));
        proxy.WaitForConnections(1);
        return (proxy, new MapperWrapper(proxy, NullLogger.Instance, null, cache));
    }

    public sealed class LookupSource
    {
        public Guid AccountId { get; set; }
        public Guid OtherAccountId { get; set; }
        public string AccountNumber { get; set; } = null!;
    }

    public sealed class LookupTarget
    {
        public EntityReference Account { get; set; } = null!;
    }

    public sealed class LookupPairs
    {
        public EntityReference First { get; set; } = null!;
        public EntityReference Second { get; set; } = null!;
    }

    public sealed class AlternateLookupSource
    {
        public string FirstNumber { get; set; } = null!;
        public string SecondNumber { get; set; } = null!;
    }

    private sealed class ThrowingReadCache : IEntityCache
    {
        public Task<CachedEntity?> GetAsync(EntityKey key, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("cache unavailable");

        public Task SetAsync(
            EntityKey key,
            CachedEntity entry,
            TimeSpan timeToLive,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task RemoveAsync(EntityKey key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveByRecordIdAsync(
            string logicalName,
            Guid id,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task ClearAsync(string logicalName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
