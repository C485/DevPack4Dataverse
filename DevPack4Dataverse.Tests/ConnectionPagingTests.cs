using DevPack4Dataverse;
using Microsoft.Xrm.Sdk.Query;
using Xunit;

namespace DevPack4Dataverse.Tests;

public class ConnectionPagingTests
{
    [Fact]
    public void PrepareQuery_WithTopCount_DoesNotAddPaging()
    {
        var source = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("name"),
            TopCount = 5
        };

        QueryExpression query = Connection.PrepareQuery(source, out PagingInfo? paging);

        Assert.Null(paging);
        Assert.NotSame(source, query);
        Assert.Null(query.PageInfo);
        Assert.Equal(5, query.TopCount);
    }

    [Fact]
    public void PrepareQuery_WithoutPaging_ReturnsPrivateCopy()
    {
        var source = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("name")
        };

        QueryExpression first = Connection.PrepareQuery(source, out PagingInfo? firstPaging);
        QueryExpression second = Connection.PrepareQuery(source, out PagingInfo? secondPaging);

        Assert.NotNull(firstPaging);
        Assert.NotNull(secondPaging);
        Assert.NotSame(source, first);
        Assert.NotSame(first, second);
        Assert.NotSame(firstPaging, secondPaging);
        Assert.NotSame(source.PageInfo, firstPaging);
        Assert.Equal(5000, firstPaging!.Count);
        Assert.Null(first.TopCount);
        Assert.NotNull(first.PageInfo);
    }

    [Fact]
    public void PrepareQuery_WithCallerPageInfo_IsLeftAlone()
    {
        var source = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("name"),
            PageInfo = new PagingInfo { Count = 10, PageNumber = 2 }
        };

        QueryExpression query = Connection.PrepareQuery(source, out PagingInfo? paging);

        Assert.Null(paging);
        Assert.Same(source, query);
        Assert.Equal(2, source.PageInfo!.PageNumber);
    }
}
