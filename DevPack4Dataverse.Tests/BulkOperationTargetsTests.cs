using DevPack4Dataverse.BulkOperations;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace DevPack4Dataverse.Tests;

public class BulkOperationTargetsTests
{
    [Fact]
    public void PrepareEntities_SetsEntityNameFromFirstTarget()
    {
        EntityCollection targets = new(
            new List<Entity> { new("account"), new("account") }
        );

        EntityCollection prepared = BulkOperationTargets.PrepareEntities(
            targets,
            false,
            nameof(targets)
        );

        Assert.Equal("account", prepared.EntityName);
    }

    [Fact]
    public void PrepareEntities_RejectsMixedTables()
    {
        EntityCollection targets = new(
            new List<Entity> { new("account"), new("contact") }
        );

        Assert.Throws<ArgumentException>(
            () => BulkOperationTargets.PrepareEntities(targets, false, nameof(targets))
        );
    }

    [Fact]
    public void PrepareEntities_RejectsEmptyCollection()
    {
        EntityCollection targets = new();

        Assert.Throws<ArgumentException>(
            () => BulkOperationTargets.PrepareEntities(targets, false, nameof(targets))
        );
    }

    [Fact]
    public void PrepareEntities_RequiresIdentityWhenRequested()
    {
        EntityCollection targets = new(new List<Entity> { new("account") });

        Assert.Throws<ArgumentException>(
            () => BulkOperationTargets.PrepareEntities(targets, true, nameof(targets))
        );
    }

    [Fact]
    public void PrepareEntities_AcceptsIdOrAlternateKey()
    {
        Entity byId = new("account", Guid.NewGuid());
        Entity byKey = new("account");
        byKey.KeyAttributes["accountnumber"] = "A-1";
        EntityCollection targets = new(new List<Entity> { byId, byKey });

        EntityCollection prepared = BulkOperationTargets.PrepareEntities(
            targets,
            true,
            nameof(targets)
        );

        Assert.Equal(2, prepared.Entities.Count);
    }

    [Fact]
    public void PrepareReferences_AcceptsIdOrAlternateKey()
    {
        EntityReference byId = new("account", Guid.NewGuid());
        EntityReference byKey = new("account");
        byKey.KeyAttributes["accountnumber"] = "A-1";
        EntityReferenceCollection targets = new(
            new List<EntityReference> { byId, byKey }
        );

        EntityReferenceCollection prepared = BulkOperationTargets.PrepareReferences(
            targets,
            nameof(targets)
        );

        Assert.Equal(2, prepared.Count);
    }

    [Fact]
    public void PrepareReferences_RejectsMissingIdentity()
    {
        EntityReferenceCollection targets = new(
            new List<EntityReference> { new("account") }
        );

        Assert.Throws<ArgumentException>(
            () => BulkOperationTargets.PrepareReferences(targets, nameof(targets))
        );
    }

    [Fact]
    public void PrepareReferences_RejectsMixedTables()
    {
        EntityReferenceCollection targets = new(
            new List<EntityReference>
            {
                new("account", Guid.NewGuid()),
                new("contact", Guid.NewGuid())
            }
        );

        Assert.Throws<ArgumentException>(
            () => BulkOperationTargets.PrepareReferences(targets, nameof(targets))
        );
    }

    [Fact]
    public void PrepareReferences_RejectsEmptyCollection()
    {
        EntityReferenceCollection targets = new();

        Assert.Throws<ArgumentException>(
            () => BulkOperationTargets.PrepareReferences(targets, nameof(targets))
        );
    }
}
