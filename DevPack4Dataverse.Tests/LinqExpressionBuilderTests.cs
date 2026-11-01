using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Xunit;
using DevPack4Dataverse.ExpressionBuilder;

namespace DevPack4Dataverse.Tests;

public class LinqExpressionBuilderTests
{
    private static Entity Account(Guid id, string name = null)
    {
        var entity = new Entity("account") { Id = id };
        if (name != null)
        {
            entity["name"] = name;
        }

        return entity;
    }

    [Fact]
    public void AddOr_MatchesWhenEitherConditionMatches()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        builder.AddOr(e => e.Id == id1);
        builder.AddOr(e => e.Id == id2);

        var predicate = builder.Result.Compile();

        Assert.True(predicate(Account(id2)));
        Assert.False(predicate(Account(Guid.NewGuid())));
    }

    [Fact]
    public void ExpressionsAdded_TracksCount()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        var other = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        other.AddOr(e => e.Id == Guid.NewGuid());
        other.AddOr(e => e.Id == Guid.NewGuid());
        builder.AddOr(other);

        Assert.Equal(2u, builder.ExpressionsAdded);
    }
}
