using DevPack4Dataverse;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
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

    [Fact]
    public void RetrieveAllPages_FollowsCookieAndIncrementsPageNumber()
    {
        var source = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("name")
        };
        var requestedPages = new List<(int PageNumber, string? PagingCookie)>();
        int call = 0;

        Entity[] rows = Connection
            .RetrieveAllPages(
                source,
                request =>
                {
                    var query = (QueryExpression)request.Query;
                    requestedPages.Add(
                        (query.PageInfo!.PageNumber, query.PageInfo.PagingCookie)
                    );
                    call++;
                    return call == 1
                        ? Response(
                            new[] { new Entity("account") { ["name"] = "first" } },
                            moreRecords: true,
                            pagingCookie: "cookie-1"
                        )
                        : Response(
                            new[] { new Entity("account") { ["name"] = "second" } },
                            moreRecords: false,
                            pagingCookie: null
                        );
                }
            )
            .ToArray();

        Assert.Equal(2, call);
        Assert.Equal(2, rows.Length);
        Assert.Equal("first", rows[0]["name"]);
        Assert.Equal("second", rows[1]["name"]);
        Assert.Equal(1, requestedPages[0].PageNumber);
        Assert.Equal(2, requestedPages[1].PageNumber);
        Assert.Equal("cookie-1", requestedPages[1].PagingCookie);
        Assert.Null(source.PageInfo!.PagingCookie);
    }

    private static RetrieveMultipleResponse Response(
        Entity[] entities,
        bool moreRecords,
        string? pagingCookie
    )
    {
        var collection = new EntityCollection(entities)
        {
            MoreRecords = moreRecords,
            PagingCookie = pagingCookie
        };
        var response = new RetrieveMultipleResponse();
        response.Results["EntityCollection"] = collection;
        return response;
    }
}
