using Microsoft.Xrm.Sdk;
using Xunit;
using DevPack4Dataverse.Mapper.Cache;
using DevPack4Dataverse.Mapper.Redis;

namespace DevPack4Dataverse.Tests;

public class RedisEntityCacheSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesSupportedValues()
    {
        Guid id = Guid.NewGuid();
        Guid parentId = Guid.NewGuid();
        DateTime created = new(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        EntityReference parent = new("account", parentId)
        {
            Name = "Parent",
            KeyAttributes = { ["accountnumber"] = "A-1" }
        };
        Entity record = new("account", id)
        {
            ["name"] = "Contoso",
            ["createdon"] = created,
            ["creditlimit"] = new Money(12.34m),
            ["statecode"] = new OptionSetValue(0),
            ["parentaccountid"] = parent,
            ["data"] = new byte[] { 1, 2, 3 }
        };
        record.KeyAttributes["accountnumber"] = "A-1";
        CachedEntity cached = new(record, new[] { "name", "createdon", "creditlimit" });

        string json = RedisEntityCache.Serialize(cached);
        CachedEntity restored = RedisEntityCache.Deserialize(json)!;

        Assert.Equal("account", restored.Record.LogicalName);
        Assert.Equal(id, restored.Record.Id);
        Assert.Equal("Contoso", restored.Record.GetAttributeValue<string>("name"));
        Assert.Equal(created, restored.Record.GetAttributeValue<DateTime>("createdon"));
        Assert.Equal(12.34m, restored.Record.GetAttributeValue<Money>("creditlimit").Value);
        Assert.Equal(0, restored.Record.GetAttributeValue<OptionSetValue>("statecode").Value);
        Assert.Equal(new byte[] { 1, 2, 3 }, restored.Record.GetAttributeValue<byte[]>("data"));

        EntityReference restoredParent = restored.Record.GetAttributeValue<EntityReference>(
            "parentaccountid"
        );
        Assert.Equal(parentId, restoredParent.Id);
        Assert.Equal("Parent", restoredParent.Name);
        Assert.Equal("A-1", restoredParent.KeyAttributes["accountnumber"]);
        Assert.Equal("A-1", restored.Record.KeyAttributes["accountnumber"]);
        Assert.Contains("name", restored.LoadedColumns);
    }

    [Fact]
    public void RoundTrip_PreservesDateOnlyNullAndFormattedValues()
    {
        Guid id = Guid.NewGuid();
        DateTime dateOnly = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        Entity record = new("account", id) { ["birthdate"] = dateOnly };
        record["name"] = null;
        record.FormattedValues["statecode"] = "Active";
        CachedEntity cached = new(record, new[] { "birthdate", "name" });

        string json = RedisEntityCache.Serialize(cached);
        CachedEntity restored = RedisEntityCache.Deserialize(json)!;

        DateTime restoredDate = restored.Record.GetAttributeValue<DateTime>("birthdate");
        Assert.Equal(dateOnly, restoredDate);
        Assert.Equal(DateTimeKind.Unspecified, restoredDate.Kind);
        Assert.True(restored.Record.Contains("name"));
        Assert.Null(restored.Record["name"]);
        Assert.Equal("Active", restored.Record.FormattedValues["statecode"]);
    }
}
