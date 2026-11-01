using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Xunit;
using DevPack4Dataverse.ExpressionBuilder;

namespace DevPack4Dataverse.Tests;

/// <summary>
/// Verifies the predicate shapes produced by <see cref="LinqExpressionBuilder"/> against
/// the Dataverse LINQ provider in Microsoft.Xrm.Sdk 9.0.0.
/// </summary>
/// <remarks>
/// The provider accepts a boolean constant only as the left identity of
/// <c>AndAlso</c> / <c>OrElse</c>. It skips that constant and translates the other
/// side, so <c>true &amp;&amp; filter</c> and <c>false || filter</c> keep the filter.
/// A bare constant, or a constant on the right side or in the wrong operator, is
/// rejected with <see cref="NotSupportedException"/>.
/// </remarks>
public class DataverseLinqProviderBehaviorTests
{
    [Fact]
    public void GetAttributeValueExpression_TranslatesToCondition()
    {
        QueryExpression query = Translate(entity => entity.GetAttributeValue<string>("name") == "Alice");

        AssertSingleCondition(query, "name", ConditionOperator.Equal, "Alice");
    }

    [Fact]
    public void TrueAndFilter_KeepsFilter_AndDropsOnlyTrue()
    {
        QueryExpression query = Translate(entity => true && entity["name"] == "Alice");

        AssertSingleCondition(query, "name", ConditionOperator.Equal, "Alice");
    }

    [Fact]
    public void FalseOrFilter_KeepsFilter_AndDropsOnlyFalse()
    {
        QueryExpression query = Translate(entity => false || entity["name"] == "Alice");

        AssertSingleCondition(query, "name", ConditionOperator.Equal, "Alice");
    }

    [Fact]
    public void Builder_IgnoresAddAndTrue_ThenFilter()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        builder.AddAnd(entity => true);
        Assert.Equal(0u, builder.ExpressionsAdded);

        builder.AddAnd(entity => entity["name"] == "Alice");

        QueryExpression query = Translate(builder.Result);
        Assert.Equal(1u, builder.ExpressionsAdded);
        AssertSingleCondition(query, "name", ConditionOperator.Equal, "Alice");
    }

    [Fact]
    public void Builder_IgnoresAddOrFalse_ThenFilter()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        builder.AddOr(entity => false);
        Assert.Equal(0u, builder.ExpressionsAdded);

        builder.AddOr(entity => entity["name"] == "Alice");

        QueryExpression query = Translate(builder.Result);
        Assert.Equal(1u, builder.ExpressionsAdded);
        AssertSingleCondition(query, "name", ConditionOperator.Equal, "Alice");
    }

    [Fact]
    public void AddAndBuilder_TranslatesToAndFilter()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        builder.AddAnd(entity => entity["name"] == "Alice");
        builder.AddAnd(entity => (int)entity["age"] == 30);

        QueryExpression query = Translate(builder.Result);
        FilterExpression andFilter = Assert.Single(query.Criteria.Filters);

        Assert.Equal(LogicalOperator.And, andFilter.FilterOperator);
        Assert.Collection(
            andFilter.Conditions,
            condition => AssertCondition(condition, "name", ConditionOperator.Equal, "Alice"),
            condition => AssertCondition(condition, "age", ConditionOperator.Equal, 30)
        );
    }

    [Fact]
    public void AddOrBuilder_TranslatesToOrFilter()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        builder.AddOr(entity => entity["name"] == "Alice");
        builder.AddOr(entity => entity["name"] == "Bob");

        QueryExpression query = Translate(builder.Result);
        FilterExpression orFilter = Assert.Single(query.Criteria.Filters);

        Assert.Equal(LogicalOperator.Or, orFilter.FilterOperator);
        Assert.Collection(
            orFilter.Conditions,
            condition => AssertCondition(condition, "name", ConditionOperator.Equal, "Alice"),
            condition => AssertCondition(condition, "name", ConditionOperator.Equal, "Bob")
        );
    }

    [Fact]
    public void MixedAndOrBuilder_TranslatesToNestedFilters()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);
        builder.AddAnd(entity => entity["name"] == "Alice");
        builder.AddAnd(entity => (int)entity["age"] == 30);
        builder.AddOr(entity => entity["name"] == "Bob");

        QueryExpression query = Translate(builder.Result);
        FilterExpression orFilter = Assert.Single(query.Criteria.Filters);

        Assert.Equal(LogicalOperator.Or, orFilter.FilterOperator);
        ConditionExpression bobCondition = Assert.Single(orFilter.Conditions);
        AssertCondition(bobCondition, "name", ConditionOperator.Equal, "Bob");

        FilterExpression andFilter = Assert.Single(orFilter.Filters);
        Assert.Equal(LogicalOperator.And, andFilter.FilterOperator);
        Assert.Collection(
            andFilter.Conditions,
            condition => AssertCondition(condition, "name", ConditionOperator.Equal, "Alice"),
            condition => AssertCondition(condition, "age", ConditionOperator.Equal, 30)
        );
    }

    [Fact]
    public void ConstantBooleanOutsideLeftIdentity_IsRejectedBySdk()
    {
        var unsupported = new (string Label, Expression<Func<Entity, bool>> Predicate)[]
        {
            ("bare true", entity => true),
            ("bare false", entity => false),
            ("false && filter", entity => false && entity["name"] == "Alice"),
            ("filter && true", entity => entity["name"] == "Alice" && true),
            ("filter || true", entity => entity["name"] == "Alice" || true),
            ("filter || false", entity => entity["name"] == "Alice" || false),
        };

        foreach ((string label, Expression<Func<Entity, bool>> predicate) in unsupported)
        {
            Exception exception = Record.Exception(() => Translate(predicate));

            Assert.True(
                exception is NotSupportedException,
                $"{label}: expected NotSupportedException, got {exception?.GetType().Name ?? "no exception"}."
            );
            Assert.Contains("Invalid 'where' condition", exception.Message);
        }
    }

    [Fact]
    public void EmptyBuilder_Result_ThrowsInvalidOperationException()
    {
        var builder = LinqExpressionBuilder.Create<Entity>(NullLogger.Instance);

        Assert.Throws<InvalidOperationException>(() => builder.Result);
    }

    private static QueryExpression Translate(Expression<Func<Entity, bool>> predicate)
    {
        var service = new CapturingOrganizationService();
        using var context = new OrganizationServiceContext(service);

        _ = context.CreateQuery("account").Where(predicate).ToArray();

        Assert.NotNull(service.LastQuery);
        return service.LastQuery;
    }

    private static void AssertSingleCondition(
        QueryExpression query,
        string attributeName,
        ConditionOperator conditionOperator,
        object value
    )
    {
        Assert.NotNull(query.Criteria);
        Assert.Equal(LogicalOperator.And, query.Criteria.FilterOperator);
        Assert.Empty(query.Criteria.Filters);

        ConditionExpression condition = Assert.Single(query.Criteria.Conditions);
        AssertCondition(condition, attributeName, conditionOperator, value);
    }

    private static void AssertCondition(
        ConditionExpression condition,
        string attributeName,
        ConditionOperator conditionOperator,
        object value
    )
    {
        Assert.Equal(attributeName, condition.AttributeName);
        Assert.Equal(conditionOperator, condition.Operator);
        Assert.Equal(value, Assert.Single(condition.Values));
    }

    private sealed class CapturingOrganizationService : IOrganizationService
    {
        public QueryExpression LastQuery { get; private set; }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            LastQuery = (QueryExpression)query;
            return new EntityCollection();
        }

        public Guid Create(Entity entity) => throw new NotSupportedException();

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => throw new NotSupportedException();

        public void Update(Entity entity) => throw new NotSupportedException();

        public void Delete(string entityName, Guid id) => throw new NotSupportedException();

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            if (request is RetrieveMultipleRequest retrieveMultipleRequest)
            {
                LastQuery = (QueryExpression)retrieveMultipleRequest.Query;
                var response = new RetrieveMultipleResponse();
                response.Results["EntityCollection"] = new EntityCollection();
                return response;
            }

            throw new NotSupportedException();
        }

        public void Associate(
            string entityName,
            Guid entityId,
            Relationship relationship,
            EntityReferenceCollection relatedEntities
        ) => throw new NotSupportedException();

        public void Disassociate(
            string entityName,
            Guid entityId,
            Relationship relationship,
            EntityReferenceCollection relatedEntities
        ) => throw new NotSupportedException();
    }
}
