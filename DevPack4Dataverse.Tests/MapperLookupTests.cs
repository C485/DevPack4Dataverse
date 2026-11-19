using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Xunit;
using DevPack4Dataverse.Mapper;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

public class MapperLookupTests
{
    [Fact]
    public async Task BasicKey_ResolvesLookupAndRequestsOnlyConfiguredColumns()
    {
        Guid id = Guid.NewGuid();
        QueryExpression captured = null;
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
            Assert.Contains("name", captured.ColumnSet.Columns);
            Assert.DoesNotContain("email", captured.ColumnSet.Columns);
        }
    }

    [Fact]
    public async Task AlternateKey_ResolvesLookup()
    {
        Guid id = Guid.NewGuid();
        QueryExpression captured = null;
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
                captured.Criteria.Conditions,
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

            options.Fields.Clear();
            options.Fields.Add("email");
            await mapper.ExecuteAsync(new LookupSource { AccountId = id });

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
                    _ => null,
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
        var fake = new FakeConnection
        {
            RetrieveMultipleHandler = _ =>
            {
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
                    _ => null,
                    new LookupMappingOptions { ThrowIfEmpty = false }
                )
                .Build();

            var result = await mapper.ExecuteAsync(new LookupSource());

            Assert.True(result.HasErrors);
            Assert.Null(result.Records[0].Target.Account);
        }
    }

    private static (SdkProxy Proxy, MapperWrapper Wrapper) Create(FakeConnection fake)
    {
        SdkProxy proxy = new(NullLogger.Instance, false, new FakeConnectionCreator(fake));
        proxy.WaitForConnections(1);
        return (proxy, new MapperWrapper(proxy, NullLogger.Instance));
    }

    public sealed class LookupSource
    {
        public Guid AccountId { get; set; }
        public Guid OtherAccountId { get; set; }
        public string AccountNumber { get; set; }
    }

    public sealed class LookupTarget
    {
        public EntityReference Account { get; set; }
    }

    public sealed class LookupPairs
    {
        public EntityReference First { get; set; }
        public EntityReference Second { get; set; }
    }

    public sealed class AlternateLookupSource
    {
        public string FirstNumber { get; set; }
        public string SecondNumber { get; set; }
    }
}
