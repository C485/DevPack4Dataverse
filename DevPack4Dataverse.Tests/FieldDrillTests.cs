using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Xunit;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

/// <summary>
/// Black-box tests for <see cref="FieldDrill"/>: path parsing, relationship traversal,
/// value conversion, and the public error contract.
/// </summary>
public class FieldDrillTests
{
    private static (
        SdkProxy Proxy,
        FieldDrill Drill,
        List<(string EntityName, Guid Id, string[] Columns)> Calls
    ) Create(Func<string, Guid, ColumnSet, Entity> retrieve)
    {
        var calls = new List<(string EntityName, Guid Id, string[] Columns)>();
        var connection = new FakeConnection
        {
            RetrieveHandler = (entityName, id, columnSet) =>
            {
                calls.Add((entityName, id, columnSet.Columns.ToArray()));
                return retrieve(entityName, id, columnSet);
            },
        };
        var proxy = new SdkProxy(NullLogger.Instance, false, new FakeConnectionCreator(connection));
        return (proxy, new FieldDrill(proxy, NullLogger.Instance), calls);
    }

    private static Entity Record(
        string logicalName,
        Guid id,
        params (string Name, object Value)[] attributes
    )
    {
        var entity = new Entity(logicalName) { Id = id };
        foreach ((string name, object value) in attributes)
        {
            entity[name] = value;
        }

        return entity;
    }

    [Fact]
    public void Retreive_SingleField_ReturnsValueAndUsesColumnSet()
    {
        var accountId = Guid.NewGuid();
        var accountReference = new EntityReference("account", accountId);
        var (proxy, drill, calls) = Create(
            (entityName, id, _) => Record(entityName, id, ("name", "Alice"))
        );

        using (proxy)
        {
            Assert.Equal("Alice", drill.Retreive<string>(accountReference, "name"));
        }

        var call = Assert.Single(calls);
        Assert.Equal("account", call.EntityName);
        Assert.Equal(accountId, call.Id);
        Assert.Equal(new[] { "name" }, call.Columns);
    }

    [Fact]
    public void Retreive_EntityReferencePath_RetrievesEachRelationship()
    {
        var accountId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var accountReference = new EntityReference("account", accountId);
        var records = new Dictionary<(string, Guid), Entity>
        {
            [("account", accountId)] = Record(
                "account",
                accountId,
                ("ownerid", new EntityReference("systemuser", ownerId))
            ),
            [("systemuser", ownerId)] = Record("systemuser", ownerId, ("fullname", "Owner Name")),
        };
        var (proxy, drill, calls) = Create((entityName, id, _) => records[(entityName, id)]);

        using (proxy)
        {
            Assert.Equal("Owner Name", drill.Retreive<string>(accountReference, "ownerid.fullname"));
        }

        Assert.Collection(
            calls,
            call =>
            {
                Assert.Equal("account", call.EntityName);
                Assert.Equal(accountId, call.Id);
                Assert.Equal(new[] { "ownerid" }, call.Columns);
            },
            call =>
            {
                Assert.Equal("systemuser", call.EntityName);
                Assert.Equal(ownerId, call.Id);
                Assert.Equal(new[] { "fullname" }, call.Columns);
            }
        );
    }

    [Fact]
    public void Retreive_SupportsCustomDelimiterAndExplicitPathParts()
    {
        var accountId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var accountReference = new EntityReference("account", accountId);
        var records = new Dictionary<(string, Guid), Entity>
        {
            [("account", accountId)] = Record(
                "account",
                accountId,
                ("ownerid", new EntityReference("systemuser", ownerId))
            ),
            [("systemuser", ownerId)] = Record("systemuser", ownerId, ("fullname", "Owner Name")),
        };
        var (proxy, drill, _) = Create((entityName, id, _) => records[(entityName, id)]);

        using (proxy)
        {
            Assert.Equal(
                "Owner Name",
                drill.Retreive<string>(accountReference, "ownerid|fullname", "|")
            );
            Assert.Equal(
                "Owner Name",
                drill.Retreive<string>(accountReference, new[] { "ownerid", "fullname" })
            );
        }
    }

    [Fact]
    public async Task RetreiveAsync_SingleField_ReturnsValue()
    {
        var accountId = Guid.NewGuid();
        var accountReference = new EntityReference("account", accountId);
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(entityName, id, ("name", "Alice"))
        );

        using (proxy)
        {
            Assert.Equal("Alice", await drill.RetreiveAsync<string>(accountReference, "name"));
        }
    }

    [Fact]
    public void Retreive_MissingField_Throws()
    {
        var accountReference = new EntityReference("account", Guid.NewGuid());
        var (proxy, drill, _) = Create((entityName, id, _) => Record(entityName, id));

        using (proxy)
        {
            Assert.Throws<InvalidOperationException>(
                () => drill.Retreive<string>(accountReference, "name")
            );
        }
    }

    [Fact]
    public void Retreive_NullFinalField_ReferenceType_ReturnsNull()
    {
        var accountReference = new EntityReference("account", Guid.NewGuid());
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(entityName, id, ("name", null))
        );

        using (proxy)
        {
            Assert.Null(drill.Retreive<string>(accountReference, "name"));
        }
    }

    [Fact]
    public void Retreive_NullFinalField_NonNullableValueType_Throws()
    {
        var accountReference = new EntityReference("account", Guid.NewGuid());
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(entityName, id, ("name", null))
        );

        using (proxy)
        {
            Assert.Throws<InvalidOperationException>(
                () => drill.Retreive<int>(accountReference, "name")
            );
        }
    }

    [Fact]
    public void Retreive_NullFinalField_NullableValueType_ReturnsNull()
    {
        var accountReference = new EntityReference("account", Guid.NewGuid());
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(entityName, id, ("name", null))
        );

        using (proxy)
        {
            Assert.Null(drill.Retreive<int?>(accountReference, "name"));
        }
    }

    [Fact]
    public void Retreive_FinalFieldTypeMismatch_Throws()
    {
        var accountReference = new EntityReference("account", Guid.NewGuid());
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(entityName, id, ("age", 42))
        );

        using (proxy)
        {
            Assert.Throws<InvalidOperationException>(
                () => drill.Retreive<string>(accountReference, "age")
            );
        }
    }

    [Fact]
    public void Retreive_ValueTypeField_ReturnsValue()
    {
        var accountReference = new EntityReference("account", Guid.NewGuid());
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(entityName, id, ("age", 42))
        );

        using (proxy)
        {
            Assert.Equal(42, drill.Retreive<int>(accountReference, "age"));
        }
    }

    [Fact]
    public void Retreive_FinalEntityReference_ReturnsReference()
    {
        var accountReference = new EntityReference("account", Guid.NewGuid());
        var ownerId = Guid.NewGuid();
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(
                entityName,
                id,
                ("ownerid", new EntityReference("systemuser", ownerId))
            )
        );

        using (proxy)
        {
            EntityReference ownerReference = drill.Retreive<EntityReference>(
                accountReference,
                "ownerid"
            );

            Assert.NotNull(ownerReference);
            Assert.Equal("systemuser", ownerReference.LogicalName);
            Assert.Equal(ownerId, ownerReference.Id);
        }
    }

    [Fact]
    public void Retreive_NullIntermediateField_Throws()
    {
        var accountReference = new EntityReference("account", Guid.NewGuid());
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(entityName, id, ("ownerid", null))
        );

        using (proxy)
        {
            Assert.Throws<InvalidOperationException>(
                () => drill.Retreive<string>(accountReference, "ownerid.fullname")
            );
        }
    }

    [Fact]
    public void Retreive_IntermediateFieldIsNotEntityReference_Throws()
    {
        var accountReference = new EntityReference("account", Guid.NewGuid());
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(entityName, id, ("name", "Alice"))
        );

        using (proxy)
        {
            Assert.Throws<InvalidOperationException>(
                () => drill.Retreive<string>(accountReference, "name.length")
            );
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retreive_InvalidIntermediateEntityReference_Throws(bool emptyId)
    {
        var accountReference = new EntityReference("account", Guid.NewGuid());
        EntityReference ownerReference = emptyId
            ? new EntityReference("systemuser", Guid.Empty)
            : new EntityReference { Id = Guid.NewGuid() };
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(entityName, id, ("ownerid", ownerReference))
        );

        using (proxy)
        {
            Assert.ThrowsAny<ArgumentException>(
                () => drill.Retreive<string>(accountReference, "ownerid.fullname")
            );
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retreive_InvalidStartingEntityReference_Throws(bool emptyId)
    {
        EntityReference accountReference = emptyId
            ? new EntityReference("account", Guid.Empty)
            : new EntityReference { Id = Guid.NewGuid() };
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(entityName, id, ("name", "Alice"))
        );

        using (proxy)
        {
            Assert.ThrowsAny<ArgumentException>(
                () => drill.Retreive<string>(accountReference, "name")
            );
        }
    }

    [Fact]
    public void Retreive_NullStartingEntityReference_Throws()
    {
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(entityName, id, ("name", "Alice"))
        );

        using (proxy)
        {
            Assert.Throws<ArgumentNullException>(
                () => drill.Retreive<string>((EntityReference)null, "name")
            );
        }
    }

    [Fact]
    public void Retreive_InvalidPathArguments_Throw()
    {
        var accountReference = new EntityReference("account", Guid.NewGuid());
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(entityName, id, ("name", "Alice"))
        );

        using (proxy)
        {
            Assert.ThrowsAny<ArgumentException>(() => drill.Retreive<string>(accountReference, (string)null));
            Assert.ThrowsAny<ArgumentException>(() => drill.Retreive<string>(accountReference, ""));
            Assert.ThrowsAny<ArgumentException>(() => drill.Retreive<string>(accountReference, " "));
            Assert.Throws<ArgumentNullException>(
                () => drill.Retreive<string>(accountReference, (string[])null)
            );
            Assert.ThrowsAny<ArgumentException>(
                () => drill.Retreive<string>(accountReference, new string[0])
            );
            Assert.ThrowsAny<ArgumentException>(
                () => drill.Retreive<string>(accountReference, new[] { "name", " " })
            );
            Assert.ThrowsAny<ArgumentException>(
                () => drill.Retreive<string>(accountReference, "name", null)
            );
            Assert.ThrowsAny<ArgumentException>(
                () => drill.Retreive<string>(accountReference, "name", "")
            );
        }
    }

    [Fact]
    public async Task RetreiveAsync_NullIntermediateField_Throws()
    {
        var accountReference = new EntityReference("account", Guid.NewGuid());
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(entityName, id, ("ownerid", null))
        );

        using (proxy)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => drill.RetreiveAsync<string>(accountReference, "ownerid.fullname")
            );
        }
    }

    [Fact]
    public async Task RetreiveAsync_InvalidPath_Throws()
    {
        var accountReference = new EntityReference("account", Guid.NewGuid());
        var (proxy, drill, _) = Create(
            (entityName, id, _) => Record(entityName, id, ("name", "Alice"))
        );

        using (proxy)
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => drill.RetreiveAsync<string>(accountReference, "")
            );
        }
    }
}
