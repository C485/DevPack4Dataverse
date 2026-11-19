using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Xunit;
using DevPack4Dataverse.Mapper;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

public class MapperValidationTests
{
    [Fact]
    public async Task MapperValidation_CollectsErrorsForAllSavedRecordsAndReadsFresh()
    {
        List<Guid> createdIds = new();
        var fake = new FakeConnection
        {
            CreateRecordHandler = _ =>
            {
                Guid id = Guid.NewGuid();
                createdIds.Add(id);
                return id;
            },
            RetrieveMultipleHandler = query =>
            {
                Assert.Contains(
                    query.Criteria.Conditions,
                    condition => condition.Operator == ConditionOperator.In
                );
                return createdIds
                    .Select(id => new Entity("account", id) { ["name"] = "Saved" })
                    .ToArray();
            }
        };
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<OrderSource, Entity>("orders")
                .WithLogicalName("account")
                .Map("name", context => context.Source.Name)
                .WithOptions(
                    options =>
                    {
                        options.ValidateAfterSave = true;
                        options.Validator = (entity, context) =>
                            new[] { $"validation:{((OrderSource)context.Source).Name}" };
                    }
                )
                .Build();

            var result = await mapper.ExecuteAsync(
                new[]
                {
                    new OrderSource { Name = "One" },
                    new OrderSource { Name = "Two" }
                }
            );

            Assert.Equal(2, result.Records.Count);
            Assert.Equal(2, result.Errors.Count);
            Assert.Contains(result.Errors, error => error.Message == "validation:One");
            Assert.Contains(result.Errors, error => error.Message == "validation:Two");
        }
    }

    [Fact]
    public async Task MapperValidation_DefaultValidatorReportsMissingSavedRecord()
    {
        Guid id = Guid.NewGuid();
        var fake = new FakeConnection
        {
            CreateRecordHandler = _ => id,
            RetrieveMultipleHandler = _ => Array.Empty<Entity>()
        };
        var (proxy, wrapper) = Create(fake);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<OrderSource, Entity>("orders")
                .WithLogicalName("account")
                .Map("name", context => context.Source.Name)
                .WithOptions(options => options.ValidateAfterSave = true)
                .Build();

            var result = await mapper.ExecuteAsync(new OrderSource { Name = "One" });

            Assert.True(result.HasErrors);
            Assert.Contains(result.Errors, error => error.Message.Contains("was not found"));
        }
    }

    [Fact]
    public async Task WrapperValidation_ValidatesSavedReferences()
    {
        Guid id = Guid.NewGuid();
        int validationReads = 0;
        var fake = new FakeConnection
        {
            CreateRecordHandler = _ => id,
            RetrieveMultipleHandler = _ =>
            {
                validationReads++;
                return new[] { new Entity("account", id) { ["name"] = "Saved" } };
            }
        };
        var wrapperOptions = new MapperWrapperOptions
        {
            ValidateAfterSave = true,
            Validator = (entity, context) => new[] { "wrapper-error" }
        };
        wrapperOptions.ValidationFields.Add("name");
        var (proxy, wrapper) = Create(fake, wrapperOptions);

        using (proxy)
        {
            var mapper = wrapper
                .CreateMapperBuilder<OrderSource, Entity>("orders")
                .WithLogicalName("account")
                .Map("name", context => context.Source.Name)
                .Build();

            Assert.True(wrapper.Register("orders", mapper));

            var result = await wrapper.ExecuteAsync(
                "orders",
                new OrderSource { Name = "One" }
            );

            Assert.True(result.HasErrors);
            Assert.Contains(result.Errors, error => error.Message == "wrapper-error");
            Assert.Equal(1, validationReads);
        }
    }

    [Fact]
    public async Task WrapperValidation_RequiresFieldsWhenValidatorIsSet()
    {
        var fake = new FakeConnection();
        var wrapperOptions = new MapperWrapperOptions
        {
            ValidateAfterSave = true,
            Validator = (_, _) => Array.Empty<string>()
        };
        var (proxy, wrapper) = Create(fake, wrapperOptions);

        using (proxy)
        {
            await Assert.ThrowsAsync<MapperConfigurationException>(
                () => wrapper.ValidateAsync(Array.Empty<EntityReference>())
            );
        }
    }

    private static (SdkProxy Proxy, MapperWrapper Wrapper) Create(
        FakeConnection fake,
        MapperWrapperOptions options = null
    )
    {
        SdkProxy proxy = new(NullLogger.Instance, false, new FakeConnectionCreator(fake));
        proxy.WaitForConnections(1);
        return (proxy, new MapperWrapper(proxy, NullLogger.Instance, options));
    }

    public sealed class OrderSource
    {
        public string Name { get; set; }
    }
}
