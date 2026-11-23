using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Xunit;
using DevPack4Dataverse.FieldMethods;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

public class OptionSetFieldTypeMethodsTests
{
    private static (SdkProxy Proxy, OptionSetFieldTypeMethods Methods) Create(
        FakeConnection connection
    )
    {
        var proxy = new SdkProxy(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(connection)
        );
        proxy.WaitForConnections(1);
        return (proxy, new OptionSetFieldTypeMethods(proxy, NullLogger.Instance));
    }

    [Fact]
    public async Task MapStringToOptionSet_WithLanguage_IgnoresCaseByDefault()
    {
        var connection = new FakeConnection { ExecuteHandler = _ => MetadataResponse() };
        var (proxy, methods) = Create(connection);

        using (proxy)
        {
            OptionSetValue value = await methods.MapStringToOptionSet(
                "account",
                "industrycode",
                "finance",
                languageCode: 1033
            );

            Assert.Equal(1, value.Value);
        }
    }

    [Fact]
    public async Task MapStringToOptionSet_ReadsMetadataOnceForRepeatedCalls()
    {
        int metadataCalls = 0;
        var connection = new FakeConnection
        {
            ExecuteHandler = _ =>
            {
                metadataCalls++;
                return MetadataResponse();
            }
        };
        var (proxy, methods) = Create(connection);

        using (proxy)
        {
            await methods.MapStringToOptionSet("account", "industrycode", "Finance", 1033);
            await methods.MapStringToOptionSet("account", "industrycode", "Finance", 1033);
        }

        Assert.Equal(1, metadataCalls);
    }

    [Fact]
    public async Task MapOptionSetToString_MissingLanguage_FallsBackToBase()
    {
        var connection = new FakeConnection
        {
            ExecuteHandler = _ => MetadataResponseWithTranslations()
        };
        var (proxy, methods) = Create(connection);

        using (proxy)
        {
            string label = await methods.MapOptionSetToString(
                "account",
                "industrycode",
                1,
                languageCode: 1036
            );

            Assert.Equal("Red", label);
        }
    }

    [Fact]
    public async Task MapStringToOptionSet_MissingLanguage_Throws()
    {
        var connection = new FakeConnection
        {
            ExecuteHandler = _ => MetadataResponseWithTranslations()
        };
        var (proxy, methods) = Create(connection);

        using (proxy)
        {
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () =>
                    methods.MapStringToOptionSet(
                        "account",
                        "industrycode",
                        "Red",
                        languageCode: 1036
                    )
            );
        }
    }

    [Fact]
    public async Task MapStringToOptionSet_ExactTranslationWinsOverBaseLabel()
    {
        var connection = new FakeConnection
        {
            ExecuteHandler = _ => MetadataResponseWithConflictingTranslations()
        };
        var (proxy, methods) = Create(connection);

        using (proxy)
        {
            OptionSetValue value = await methods.MapStringToOptionSet(
                "account",
                "industrycode",
                "Red",
                languageCode: 1036
            );

            Assert.Equal(2, value.Value);
        }
    }

    [Fact]
    public async Task MapStringToEnum_NonIntEnum_Works()
    {
        var connection = new FakeConnection { ExecuteHandler = _ => MetadataResponse() };
        var (proxy, methods) = Create(connection);

        using (proxy)
        {
            ShortIndustry value = await methods.MapStringToEnum<ShortIndustry>(
                "account",
                "industrycode",
                "Finance",
                languageCode: 1033
            );

            Assert.Equal(ShortIndustry.Finance, value);
        }
    }

    private enum ShortIndustry : short
    {
        Finance = 1
    }

    private static RetrieveAttributeResponse MetadataResponse()
    {
        var metadata = new PicklistAttributeMetadata("industrycode")
        {
            OptionSet = new OptionSetMetadata()
        };
        metadata.OptionSet.Options.Add(
            new OptionMetadata(new Label("Finance", 1033), 1)
        );

        var response = new RetrieveAttributeResponse();
        response.Results["AttributeMetadata"] = metadata;
        return response;
    }

    private static RetrieveAttributeResponse MetadataResponseWithTranslations()
    {
        var metadata = new PicklistAttributeMetadata("industrycode")
        {
            OptionSet = new OptionSetMetadata()
        };
        var label = new Label("Czerwony", 1045);
        label.LocalizedLabels.Add(new LocalizedLabel("Red", 1033));
        metadata.OptionSet.Options.Add(new OptionMetadata(label, 1));

        var response = new RetrieveAttributeResponse();
        response.Results["AttributeMetadata"] = metadata;
        return response;
    }

    private static RetrieveAttributeResponse MetadataResponseWithConflictingTranslations()
    {
        var metadata = new PicklistAttributeMetadata("industrycode")
        {
            OptionSet = new OptionSetMetadata()
        };
        metadata.OptionSet.Options.Add(
            new OptionMetadata(new Label("Red", 1033), 1)
        );
        var translated = new Label("Blue", 1033);
        translated.LocalizedLabels.Add(new LocalizedLabel("Red", 1036));
        metadata.OptionSet.Options.Add(new OptionMetadata(translated, 2));

        var response = new RetrieveAttributeResponse();
        response.Results["AttributeMetadata"] = metadata;
        return response;
    }
}
