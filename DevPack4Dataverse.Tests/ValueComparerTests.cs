using DevPack4Dataverse.Mapper.Internal;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace DevPack4Dataverse.Tests;

public class ValueComparerTests
{
    [Fact]
    public void MoneyAndDecimal_AreComparedByValue()
    {
        Assert.True(ValueComparer.AreEqual(new Money(100m), 100m));
        Assert.True(ValueComparer.AreEqual(100m, new Money(100.00m)));
        Assert.False(ValueComparer.AreEqual(new Money(100m), 101m));
    }

    [Fact]
    public void OptionSetAndInt_AreComparedByValue()
    {
        Assert.True(ValueComparer.AreEqual(new OptionSetValue(3), 3));
        Assert.True(ValueComparer.AreEqual(3, new OptionSetValue(3)));
        Assert.False(ValueComparer.AreEqual(new OptionSetValue(3), 4));
    }

    [Fact]
    public void OptionSetCollections_AreComparedBySequence()
    {
        var first = new OptionSetValueCollection
        {
            new OptionSetValue(1),
            new OptionSetValue(2)
        };
        var second = new OptionSetValueCollection
        {
            new OptionSetValue(1),
            new OptionSetValue(2)
        };
        var different = new OptionSetValueCollection
        {
            new OptionSetValue(2),
            new OptionSetValue(1)
        };

        Assert.True(ValueComparer.AreEqual(first, second));
        Assert.False(ValueComparer.AreEqual(first, different));
    }

    [Fact]
    public void DateTimeValues_AreComparedAsInstants()
    {
        var utc = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(ValueComparer.AreEqual(utc, utc.ToLocalTime()));
        Assert.False(ValueComparer.AreEqual(utc, utc.AddSeconds(1)));
    }

    [Fact]
    public void AlternateKeyReferences_AreComparedByKeyAttributes()
    {
        var first = new EntityReference("account");
        first.KeyAttributes["accountnumber"] = "A-1";
        var second = new EntityReference("account");
        second.KeyAttributes["accountnumber"] = "A-1";
        var different = new EntityReference("account");
        different.KeyAttributes["accountnumber"] = "A-2";

        Assert.True(ValueComparer.AreEqual(first, second));
        Assert.False(ValueComparer.AreEqual(first, different));
        Assert.False(ValueComparer.AreEqual(first, new EntityReference("account", Guid.NewGuid())));
    }

    [Fact]
    public void NumericTypes_AreComparedByValue()
    {
        Assert.True(ValueComparer.AreEqual(5, 5L));
        Assert.True(ValueComparer.AreEqual(5, 5.0d));
        Assert.False(ValueComparer.AreEqual(5, 6L));
    }
}
