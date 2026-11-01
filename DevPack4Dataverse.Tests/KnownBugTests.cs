using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Xunit;
using DevPack4Dataverse.Creators;
using DevPack4Dataverse.ExpressionBuilder;
using DevPack4Dataverse.Models;

namespace DevPack4Dataverse.Tests;

/// <summary>
/// Each test in this class encodes the behaviour the library <em>should</em> have.
/// A failure here is a confirmed defect, not a broken test. The offending source
/// locations are listed in the accompanying analysis report.
/// </summary>
[Trait("Type", "KnownBug")]
public class KnownBugTests
{
    // Regression: LinqExpressionBuilder.AddAnd used to AND the first condition with
    // ExpressionCombiner.Empty (`_ => false`), so the in-memory predicate was always false.
    // The Dataverse LINQ provider also rejects `false && ...` as an invalid where condition,
    // verified in DataverseLinqProviderBehaviorTests. The first condition must replace the
    // empty identity instead of being combined with it.
    [Fact]
    public void AddAnd_ShouldMatch_WhenSingleConditionMatches()
    {
        var id = Guid.NewGuid();
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        builder.AddAnd(e => e.Id == id);

        var predicate = builder.Result.Compile();
        Assert.True(predicate(new Entity("account") { Id = id }));
    }

    [Fact]
    public void AddAnd_ShouldMatch_WhenTwoConditionsMatch()
    {
        var id = Guid.NewGuid();
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        builder.AddAnd(e => e.Id == id);
        builder.AddAnd(e => e.Id == id);

        var predicate = builder.Result.Compile();
        Assert.True(predicate(new Entity("account") { Id = id }));
    }

    // Bug 2: SdkProxy.Dispose calls StopAsync().RunSynchronously(), which throws because the
    // task has already been started.
    [Fact]
    public void SdkProxy_Dispose_ShouldNotThrow()
    {
        var proxy = new SdkProxy(NullLogger.Instance, false);

        var exception = Record.Exception(proxy.Dispose);

        Assert.Null(exception);
    }

    // Bug 3: Statistic rejects a zero elapsed time. Stopwatch.Elapsed is exactly zero when the
    // very first request starts within the same tick as the shared stopwatch, so this is reachable.
    [Fact]
    public void Statistic_ShouldAcceptZeroStart()
    {
        var exception = Record.Exception(() => _ = new Statistic(TimeSpan.Zero));

        Assert.Null(exception);
    }

    [Fact]
    public void Statistic_ShouldAcceptZeroEnd()
    {
        var statistic = new Statistic(TimeSpan.FromSeconds(1));

        var exception = Record.Exception(() => statistic.SetEnd(TimeSpan.Zero));

        Assert.Null(exception);
    }

    // Bug 4: maximumConcurrentlyUsage is not validated when the connection string already ends with
    // ';' (or already contains RequireNewInstance=True), because the constructor returns early.
    [Fact]
    public void ConnectionStringCreator_ShouldRejectNegativeConcurrency_WhenStringEndsWithSemicolon()
    {
        var exception = Record.Exception(() =>
            new ConnectionStringConnectionCreator(
                "AuthType=ClientSecret;Url=https://example.crm.dynamics.com;",
                maximumConcurrentlyUsage: -1));

        Assert.NotNull(exception);
    }

    [Fact]
    public void ConnectionStringCreator_ShouldRejectNegativeConcurrency_WhenRequireNewInstancePresent()
    {
        var exception = Record.Exception(() =>
            new ConnectionStringConnectionCreator(
                "AuthType=ClientSecret;Url=https://example.crm.dynamics.com;RequireNewInstance=True",
                maximumConcurrentlyUsage: -1));

        Assert.NotNull(exception);
    }
}
