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
}
