using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Xunit;
using DevPack4Dataverse.ExpressionBuilder;
using DevPack4Dataverse.Interfaces;

namespace DevPack4Dataverse.Tests;

/// <summary>
/// Black-box tests for the public expression-builder contract. These tests describe
/// how <see cref="LinqExpressionBuilder"/> should behave for callers; they do not
/// depend on the internal implementation.
/// </summary>
public class LinqExpressionBuilderBehaviorTests
{
    private static Entity Account(Guid id, params (string Name, object Value)[] attributes)
    {
        var entity = new Entity("account") { Id = id };
        foreach ((string name, object value) in attributes)
        {
            entity[name] = value;
        }

        return entity;
    }

    private static Expression<Func<Entity, bool>> Flag(string attributeName) =>
        entity => entity.GetAttributeValue<bool>(attributeName);

    [Fact]
    public void EmptyBuilder_MatchesNothing()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        Assert.Equal(0u, builder.ExpressionsAdded);
        Assert.False(builder.Result.Compile()(new Entity("account")));
    }

    [Fact]
    public void Create_Throws_WhenLoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => LinqExpressionBuilder.Create<Entity>(null));
    }

    [Fact]
    public void AddAnd_SingleCondition_MatchesOnlyWhenConditionIsTrue()
    {
        var id = Guid.NewGuid();
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        builder.AddAnd(entity => entity.Id == id);
        var predicate = builder.Result.Compile();

        Assert.Equal(1u, builder.ExpressionsAdded);
        Assert.True(predicate(Account(id)));
        Assert.False(predicate(Account(Guid.NewGuid())));
    }

    [Fact]
    public void AddAnd_MultipleConditions_RequireAllConditions()
    {
        var id = Guid.NewGuid();
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        builder.AddAnd(entity => entity.Id == id);
        builder.AddAnd(entity => entity.GetAttributeValue<string>("name") == "Alice");
        var predicate = builder.Result.Compile();

        Assert.Equal(2u, builder.ExpressionsAdded);
        Assert.True(predicate(Account(id, ("name", "Alice"))));
        Assert.False(predicate(Account(Guid.NewGuid(), ("name", "Alice"))));
        Assert.False(predicate(Account(id, ("name", "Bob"))));
    }

    [Fact]
    public void AddOr_MultipleConditions_MatchWhenAnyConditionMatches()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        builder.AddOr(entity => entity.GetAttributeValue<string>("name") == "Alice");
        builder.AddOr(entity => entity.GetAttributeValue<string>("name") == "Bob");
        var predicate = builder.Result.Compile();

        Assert.Equal(2u, builder.ExpressionsAdded);
        Assert.True(predicate(Account(Guid.NewGuid(), ("name", "Alice"))));
        Assert.True(predicate(Account(Guid.NewGuid(), ("name", "Bob"))));
        Assert.False(predicate(Account(Guid.NewGuid(), ("name", "Carol"))));
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void AddAndThenAddOr_CombinesBlocksLeftToRight(bool a, bool b, bool c, bool expected)
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        builder.AddAnd(Flag("a"));
        builder.AddAnd(Flag("b"));
        builder.AddOr(Flag("c"));
        var predicate = builder.Result.Compile();

        Assert.Equal(expected, predicate(Account(Guid.NewGuid(), ("a", a), ("b", b), ("c", c))));
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, false)]
    public void AddOrThenAddAnd_CombinesBlocksLeftToRight(bool a, bool b, bool c, bool expected)
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        builder.AddOr(Flag("a"));
        builder.AddOr(Flag("b"));
        builder.AddAnd(Flag("c"));
        var predicate = builder.Result.Compile();

        Assert.Equal(expected, predicate(Account(Guid.NewGuid(), ("a", a), ("b", b), ("c", c))));
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void AddAndBuilder_AddsInnerExpressionAsSingleBlock(bool a, bool b, bool c, bool expected)
    {
        var inner = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        inner.AddAnd(Flag("a"));
        inner.AddAnd(Flag("b"));

        var outer = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        outer.AddAnd(inner);
        outer.AddOr(Flag("c"));
        var predicate = outer.Result.Compile();

        Assert.Equal(3u, outer.ExpressionsAdded);
        Assert.Equal(expected, predicate(Account(Guid.NewGuid(), ("a", a), ("b", b), ("c", c))));
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, false)]
    public void AddOrBuilder_AddsInnerExpressionAsSingleBlock(bool a, bool b, bool c, bool expected)
    {
        var inner = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        inner.AddOr(Flag("a"));
        inner.AddOr(Flag("b"));

        var outer = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        outer.AddOr(inner);
        outer.AddAnd(Flag("c"));
        var predicate = outer.Result.Compile();

        Assert.Equal(3u, outer.ExpressionsAdded);
        Assert.Equal(expected, predicate(Account(Guid.NewGuid(), ("a", a), ("b", b), ("c", c))));
    }

    [Fact]
    public void ExpressionsAdded_CountsInnerExpressions()
    {
        var inner = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        inner.AddAnd(Flag("a"));
        inner.AddAnd(Flag("b"));

        var outer = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        outer.AddAnd(inner);

        Assert.Equal(2u, outer.ExpressionsAdded);

        outer.AddOr(Flag("c"));

        Assert.Equal(3u, outer.ExpressionsAdded);
    }

    [Fact]
    public void MaximumExpressions_Allows500AndRejects501()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        for (int index = 0; index < 500; index++)
        {
            builder.AddAnd(entity => true);
        }

        Assert.Equal(500u, builder.ExpressionsAdded);
        Assert.True(builder.Result.Compile()(new Entity("account")));
        Assert.ThrowsAny<ArgumentException>(() => builder.AddAnd(entity => true));
    }

    [Fact]
    public void AddingInnerBuilder_RespectsMaximumExpressions()
    {
        var inner = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        for (int index = 0; index < 500; index++)
        {
            inner.AddAnd(entity => true);
        }

        var outer = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        outer.AddAnd(inner);

        Assert.Equal(500u, outer.ExpressionsAdded);
        Assert.ThrowsAny<ArgumentException>(() => outer.AddOr(entity => true));
    }

    [Fact]
    public void AddAnd_NullExpression_Throws()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        Assert.Throws<ArgumentNullException>(
            () => builder.AddAnd((Expression<Func<Entity, bool>>)null)
        );
    }

    [Fact]
    public void AddOr_NullExpression_Throws()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        Assert.Throws<ArgumentNullException>(
            () => builder.AddOr((Expression<Func<Entity, bool>>)null)
        );
    }

    [Fact]
    public void AddAnd_NullBuilder_Throws()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        Assert.Throws<ArgumentNullException>(
            () => builder.AddAnd((ILinqExpressionBuilder<Entity>)null)
        );
    }

    [Fact]
    public void AddOr_NullBuilder_Throws()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        Assert.Throws<ArgumentNullException>(
            () => builder.AddOr((ILinqExpressionBuilder<Entity>)null)
        );
    }

    [Fact]
    public void AddingEmptyBuilder_Throws()
    {
        var empty = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        Assert.ThrowsAny<ArgumentException>(() => builder.AddAnd(empty));
        Assert.ThrowsAny<ArgumentException>(() => builder.AddOr(empty));
    }

    [Fact]
    public void LateBoundAttributeExpressions_Work()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        builder.AddOr(entity => (string)entity["name"] == "Alice");
        builder.AddOr(entity => (string)entity["name"] == "Bob");
        var predicate = builder.Result.Compile();

        Assert.True(predicate(Account(Guid.NewGuid(), ("name", "Alice"))));
        Assert.True(predicate(Account(Guid.NewGuid(), ("name", "Bob"))));
        Assert.False(predicate(Account(Guid.NewGuid(), ("name", "Carol"))));
    }

    [Fact]
    public void AddAnd_ReplacesParameterInsideNestedLambda()
    {
        var id = Guid.NewGuid();
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        builder.AddAnd(entity => new[] { "Alice", "Bob" }.Any(name => name == (string)entity["name"]));
        builder.AddAnd(entity => entity.Id == id);
        var predicate = builder.Result.Compile();

        Assert.True(predicate(Account(id, ("name", "Alice"))));
        Assert.False(predicate(Account(Guid.NewGuid(), ("name", "Alice"))));
        Assert.False(predicate(Account(id, ("name", "Carol"))));
    }

    [Fact]
    public void AddAnd_ShortCircuits_WhenLeftConditionIsFalse()
    {
        var counter = new CallCounter();
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        builder.AddAnd(entity => false);
        builder.AddAnd(entity => counter.Record());

        builder.Result.Compile()(new Entity("account"));

        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public void AddOr_ShortCircuits_WhenLeftConditionIsTrue()
    {
        var counter = new CallCounter();
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        builder.AddOr(entity => true);
        builder.AddOr(entity => counter.Record());

        builder.Result.Compile()(new Entity("account"));

        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public void Result_CanBeUsedWithQueryableWhere()
    {
        var id = Guid.NewGuid();
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        builder.AddAnd(entity => entity.Id == id);

        Entity[] matches = new[] { Account(id), Account(Guid.NewGuid()) }
            .AsQueryable()
            .Where(builder.Result)
            .ToArray();

        Assert.Single(matches);
        Assert.Equal(id, matches[0].Id);
    }

    [Fact]
    public void Result_IsStableAfterAddingMoreExpressions()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        builder.AddAnd(Flag("a"));
        var firstPredicate = builder.Result.Compile();

        builder.AddAnd(Flag("b"));
        var secondPredicate = builder.Result.Compile();

        Assert.True(firstPredicate(Account(Guid.NewGuid(), ("a", true), ("b", false))));
        Assert.False(secondPredicate(Account(Guid.NewGuid(), ("a", true), ("b", false))));
        Assert.True(secondPredicate(Account(Guid.NewGuid(), ("a", true), ("b", true))));
    }

    [Fact]
    public void Create_WorksWithEarlyBoundEntityType()
    {
        var builder = LinqExpressionBuilder.Create<TestAccount>(NullLogger.Instance);

        builder.AddAnd(account => account.Name == "Alice");
        var predicate = builder.Result.Compile();

        Assert.True(predicate(new TestAccount { Name = "Alice" }));
        Assert.False(predicate(new TestAccount { Name = "Bob" }));
    }

    [Fact]
    public void DataverseDevPack_CreateLinqExpressionBuilder_ReturnsUsableBuilder()
    {
        var id = Guid.NewGuid();
        var devPack = new DataverseDevPack(NullLogger.Instance, applyConnectionOptimalization: false);

        try
        {
            var builder = devPack.CreateLinqExpressionBuilder<Entity>();
            builder.AddAnd(entity => entity.Id == id);

            Assert.True(builder.Result.Compile()(Account(id)));
        }
        finally
        {
            devPack.SdkProxy.Dispose();
        }
    }

    [Fact]
    public void AddingBuilder_WithHugeExpressionCount_ThrowsInsteadOfOverflowing()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        builder.AddAnd(entity => true);
        var huge = new FixedExpressionBuilder(uint.MaxValue, entity => true);

        Assert.ThrowsAny<ArgumentException>(() => builder.AddAnd(huge));
        Assert.Equal(1u, builder.ExpressionsAdded);
        Assert.True(builder.Result.Compile()(new Entity("account")));
    }

    [Fact]
    public void AddingBuilderWithHugeExpressionCount_ToOr_ThrowsInsteadOfOverflowing()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        builder.AddOr(entity => false);
        var huge = new FixedExpressionBuilder(uint.MaxValue, entity => true);

        Assert.ThrowsAny<ArgumentException>(() => builder.AddOr(huge));
        Assert.Equal(1u, builder.ExpressionsAdded);
        Assert.False(builder.Result.Compile()(new Entity("account")));
    }

    private sealed class CallCounter
    {
        public int Count { get; private set; }

        public bool Record()
        {
            Count++;
            return true;
        }
    }

    private sealed class FixedExpressionBuilder : ILinqExpressionBuilder<Entity>
    {
        public FixedExpressionBuilder(uint expressionsAdded, Expression<Func<Entity, bool>> result)
        {
            ExpressionsAdded = expressionsAdded;
            Result = result;
        }

        public uint ExpressionsAdded { get; }

        public Expression<Func<Entity, bool>> Result { get; }

        public void AddAnd(ILinqExpressionBuilder<Entity> expressionToAdd) =>
            throw new NotSupportedException();

        public void AddAnd(Expression<Func<Entity, bool>> expressionToAdd) =>
            throw new NotSupportedException();

        public void AddOr(ILinqExpressionBuilder<Entity> expressionToAdd) =>
            throw new NotSupportedException();

        public void AddOr(Expression<Func<Entity, bool>> expressionToAdd) =>
            throw new NotSupportedException();
    }

    private sealed class TestAccount : Entity
    {
        public TestAccount() : base("account") { }

        public string Name
        {
            get => GetAttributeValue<string>("name");
            set => this["name"] = value;
        }
    }
}
