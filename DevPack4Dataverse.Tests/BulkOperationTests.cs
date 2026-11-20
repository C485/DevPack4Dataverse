using DevPack4Dataverse.Models;
using DevPack4Dataverse.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Xunit;

namespace DevPack4Dataverse.Tests;

public class BulkOperationTests
{
    [Fact]
    public void CreateMultiple_DelegatesAndReturnsIds()
    {
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        var fake = new FakeConnection
        {
            CreateMultipleHandler = targets =>
            {
                Assert.Equal("account", targets.EntityName);
                Assert.Equal(2, targets.Entities.Count);
                return new[] { firstId, secondId };
            }
        };
        using SdkProxy proxy = Create(fake);
        EntityCollection targets = new(
            new List<Entity> { new("account"), new("account") }
        )
        {
            EntityName = "account"
        };
        RequestSettings settings = new();

        Guid[] ids = proxy.CreateMultiple(targets, settings);

        Assert.Equal(new[] { firstId, secondId }, ids);
        Assert.Same(settings, fake.LastRequestSettings);
    }

    [Fact]
    public async Task CreateMultipleAsync_Delegates()
    {
        Guid id = Guid.NewGuid();
        var fake = new FakeConnection
        {
            CreateMultipleHandler = targets =>
            {
                Assert.Equal("account", targets.EntityName);
                return new[] { id };
            }
        };
        using SdkProxy proxy = Create(fake);
        EntityCollection targets = new(new List<Entity> { new("account") })
        {
            EntityName = "account"
        };

        Guid[] ids = await proxy.CreateMultipleAsync(targets);

        Assert.Equal(new[] { id }, ids);
    }

    [Fact]
    public void UpdateMultiple_Delegates()
    {
        EntityCollection? captured = null;
        var fake = new FakeConnection
        {
            UpdateMultipleHandler = targets => captured = targets
        };
        using SdkProxy proxy = Create(fake);
        EntityCollection targets = new(
            new List<Entity> { new("account", Guid.NewGuid()) }
        )
        {
            EntityName = "account"
        };

        proxy.UpdateMultiple(targets);

        Assert.Same(targets, captured);
    }

    [Fact]
    public void UpsertMultiple_DelegatesAndReturnsResults()
    {
        var fake = new FakeConnection
        {
            UpsertMultipleHandler = targets =>
            {
                Assert.Equal("account", targets.EntityName);
                return new[] { new UpsertResponse() };
            }
        };
        using SdkProxy proxy = Create(fake);
        EntityCollection targets = new(
            new List<Entity> { new("account", Guid.NewGuid()) }
        )
        {
            EntityName = "account"
        };

        UpsertResponse[] results = proxy.UpsertMultiple(targets);

        Assert.Single(results);
    }

    [Fact]
    public void DeleteMultiple_Delegates()
    {
        EntityReferenceCollection? captured = null;
        var fake = new FakeConnection
        {
            DeleteMultipleHandler = targets => captured = targets
        };
        using SdkProxy proxy = Create(fake);
        EntityReferenceCollection targets = new(
            new List<EntityReference> { new("account", Guid.NewGuid()) }
        );

        proxy.DeleteMultiple(targets);

        Assert.Same(targets, captured);
    }

    private static SdkProxy Create(FakeConnection connection)
    {
        SdkProxy proxy = new(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(connection)
        );
        proxy.WaitForConnections(1);
        return proxy;
    }
}
