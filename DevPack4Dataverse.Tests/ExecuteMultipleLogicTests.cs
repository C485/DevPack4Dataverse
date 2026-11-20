using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Xunit;
using DevPack4Dataverse.ExecuteMultiple;
using DevPack4Dataverse.Models;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

/// <summary>
/// Black-box tests for <see cref="ExecuteMultipleLogic"/>: chunking, result ordering,
/// failure handling, progress, cancellation, and input validation.
/// </summary>
public class ExecuteMultipleLogicTests
{
    private static (SdkProxy Proxy, ExecuteMultipleLogic Logic) Create(
        Func<OrganizationRequest, OrganizationResponse> execute
    )
    {
        var connection = new FakeConnection { ExecuteHandler = execute };
        var proxy = new SdkProxy(NullLogger.Instance, false, new FakeConnectionCreator(connection));
        return (proxy, new ExecuteMultipleLogic(proxy, NullLogger.Instance));
    }

    private static ExecuteMultipleRequestBuilder BuilderWith(params string[] requestNames)
    {
        var builder = new ExecuteMultipleRequestBuilder(NullLogger.Instance);
        foreach (string requestName in requestNames)
        {
            builder.AddRequest(new OrganizationRequest(requestName));
        }

        return builder;
    }

    private static ExecuteMultipleResponse Response(params ExecuteMultipleResponseItem[] items)
    {
        var response = new ExecuteMultipleResponse();
        var collection = new ExecuteMultipleResponseItemCollection();
        foreach (ExecuteMultipleResponseItem item in items)
        {
            collection.Add(item);
        }

        response.Results["Responses"] = collection;
        return response;
    }

    private static ExecuteMultipleResponseItem Success(int requestIndex, string marker) =>
        new() { RequestIndex = requestIndex, Response = new MarkerResponse { Marker = marker } };

    private static ExecuteMultipleResponseItem Failure(int requestIndex) =>
        new() { RequestIndex = requestIndex, Fault = new OrganizationServiceFault() };

    [Fact]
    public async Task ExecuteAsync_ReturnsResultsInOriginalRequestOrder()
    {
        var (proxy, logic) = Create(request =>
        {
            var executeMultipleRequest = (ExecuteMultipleRequest)request;
            ExecuteMultipleResponseItem[] items = executeMultipleRequest
                .Requests.Select((item, index) => Success(index, item.RequestName))
                .Reverse()
                .ToArray();
            return Response(items);
        });

        using (proxy)
        {
            ExecuteMultipleRequestBuilder builder = BuilderWith("req0", "req1", "req2", "req3", "req4");

            ExecuteMultipleLogicResult result = await logic.ExecuteAsync(
                builder,
                new ExecuteMultipleRequestSimpleSettings
                {
                    RequestSize = 2,
                    MaxDegreeOfParallelism = 1,
                }
            );

            Assert.Equal(
                new[] { "req0", "req1", "req2", "req3", "req4" },
                result.Results.Select(item => ((MarkerResponse)item.Response).Marker).ToArray()
            );
            Assert.Equal(5, result.RecordsRequested);
            Assert.Equal(5, result.RecordsProcessed);
            Assert.Equal(1, result.ThreadsUsed);
            Assert.False(result.Cancelled);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RejectsNonPositiveRequestSize()
    {
        var (proxy, logic) = Create(_ => Response(Success(0, "req")));

        using (proxy)
        {
            ExecuteMultipleRequestBuilder builder = BuilderWith("req");

            await Assert.ThrowsAsync<ArgumentException>(
                () => logic.ExecuteAsync(
                    builder,
                    new ExecuteMultipleRequestSimpleSettings { RequestSize = 0 }
                )
            );
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionCountIsZero_StillUsesOneWorker()
    {
        var (proxy, logic) = Create(request =>
        {
            var executeMultipleRequest = (ExecuteMultipleRequest)request;
            return Response(Success(0, executeMultipleRequest.Requests[0].RequestName));
        });

        using (proxy)
        {
            ExecuteMultipleRequestBuilder builder = BuilderWith("req");

            ExecuteMultipleLogicResult result = await logic.ExecuteAsync(
                builder,
                new ExecuteMultipleRequestSimpleSettings { RequestSize = 1 }
            );

            Assert.Equal(1, result.ThreadsUsed);
            Assert.Equal(1, result.RecordsProcessed);
        }
    }

    [Fact]
    public async Task ExecuteAsync_AllowsNullProgressAndErrorCallbacks()
    {
        var (proxy, logic) = Create(request =>
        {
            var executeMultipleRequest = (ExecuteMultipleRequest)request;
            return Response(Success(0, executeMultipleRequest.Requests[0].RequestName));
        });

        using (proxy)
        {
            ExecuteMultipleRequestBuilder builder = BuilderWith("req");

            ExecuteMultipleLogicResult result = await logic.ExecuteAsync(
                builder,
                new ExecuteMultipleRequestSettings
                {
                    RequestSize = 1,
                    ErrorReport = null,
                    ReportProgress = null,
                    ReportProgressInterval = TimeSpan.FromMilliseconds(10),
                }
            );

            Assert.Single(result.Results);
        }
    }

    [Fact]
    public async Task ExecuteAsync_AllowsNullErrorReport_WhenARequestFaults()
    {
        var (proxy, logic) = Create(_ => Response(Failure(0)));

        using (proxy)
        {
            ExecuteMultipleRequestBuilder builder = BuilderWith("req");

            ExecuteMultipleLogicResult result = await logic.ExecuteAsync(
                builder,
                new ExecuteMultipleRequestSettings
                {
                    RequestSize = 1,
                    ErrorReport = null,
                    ReportProgress = null,
                    ReportProgressInterval = TimeSpan.FromMilliseconds(10),
                }
            );

            ExecuteMultipleResponseItem item = Assert.Single(result.Results);
            Assert.NotNull(item.Fault);
        }
    }

    [Fact]
    public async Task SimpleExecuteAsync_StopsAtFirstFault()
    {
        var (proxy, logic) = Create(_ => Response(Failure(0)));

        using (proxy)
        {
            ExecuteMultipleRequestBuilder builder = BuilderWith("req");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => logic.ExecuteAsync(
                    builder,
                    new ExecuteMultipleRequestSimpleSettings { RequestSize = 1 }
                )
            );
        }
    }

    [Fact]
    public async Task SimpleExecuteAsync_PassesOuterRequestSettings()
    {
        var connection = new FakeConnection
        {
            ExecuteHandler = _ => Response(Success(0, "req"))
        };
        using var proxy = new SdkProxy(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(connection)
        );
        var logic = new ExecuteMultipleLogic(proxy, NullLogger.Instance);
        var requestSettings = new RequestSettings();
        ExecuteMultipleRequestBuilder builder = BuilderWith("req");

        await logic.ExecuteAsync(
            builder,
            new ExecuteMultipleRequestSimpleSettings { RequestSize = 1 },
            requestSettings: requestSettings
        );

        Assert.Same(requestSettings, connection.LastRequestSettings);
    }

    [Fact]
    public async Task SimpleExecuteAsync_DoesNotSendLaterChunksAfterFault()
    {
        int calls = 0;
        var (proxy, logic) = Create(_ =>
        {
            calls++;
            return Response(Failure(0));
        });

        using (proxy)
        {
            ExecuteMultipleRequestBuilder builder = BuilderWith("first", "second");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    logic.ExecuteAsync(
                        builder,
                        new ExecuteMultipleRequestSimpleSettings
                        {
                            RequestSize = 1,
                            MaxDegreeOfParallelism = 4
                        }
                    )
            );

            Assert.Equal(1, calls);
        }
    }

    [Fact]
    public async Task AdaptiveExecuteAsync_ProcessesAllRequestsInOrder()
    {
        var connection = new FakeConnection
        {
            ExecuteHandler = request =>
            {
                var batch = (ExecuteMultipleRequest)request;
                return Response(
                    batch
                        .Requests.Select(
                            (item, index) =>
                                Success(index, item.RequestName)
                        )
                        .ToArray()
                );
            }
        };
        using var proxy = new SdkProxy(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(connection)
        );
        var logic = new ExecuteMultipleLogic(proxy, NullLogger.Instance);
        string[] names = Enumerable.Range(0, 50).Select(i => $"req-{i}").ToArray();
        ExecuteMultipleRequestBuilder builder = BuilderWith(names);

        ExecuteMultipleLogicResult result = await logic.ExecuteAdaptiveAsync(
            builder,
            new AdaptiveRequesterSettings
            {
                InitialBatchSize = 3,
                InitialDegreeOfParallelism = 2,
                EvaluationRecordCount = 10,
                TargetBatchDuration = TimeSpan.FromHours(1),
                MinBatchSize = 1,
                MaxBatchSize = 10,
                MinDegreeOfParallelism = 1,
                MaxDegreeOfParallelism = 4
            }
        );

        Assert.False(result.Cancelled);
        Assert.Equal(50, result.RecordsRequested);
        Assert.Equal(50, result.RecordsProcessed);
        Assert.Equal(
            names,
            result.Results.Select(
                item => ((MarkerResponse)item.Response).Marker
            )
        );
        Assert.True(result.BatchSizeUsed >= 3);
        Assert.True(result.ThreadsUsed >= 2);
    }

    [Fact]
    public async Task AdaptiveExecuteAsync_ReducesBatchAndThreadsWhenSlow()
    {
        var connection = new FakeConnection
        {
            AsyncExecuteHandler = async request =>
            {
                await Task.Delay(30);
                var batch = (ExecuteMultipleRequest)request;
                return Response(
                    batch
                        .Requests.Select(
                            (item, index) =>
                                Success(index, item.RequestName)
                        )
                        .ToArray()
                );
            }
        };
        using var proxy = new SdkProxy(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(connection)
        );
        var logic = new ExecuteMultipleLogic(proxy, NullLogger.Instance);
        ExecuteMultipleRequestBuilder builder = BuilderWith(
            Enumerable.Range(0, 80).Select(i => $"req-{i}").ToArray()
        );

        ExecuteMultipleLogicResult result = await logic.ExecuteAdaptiveAsync(
            builder,
            new AdaptiveRequesterSettings
            {
                InitialBatchSize = 4,
                InitialDegreeOfParallelism = 4,
                EvaluationRecordCount = 8,
                TargetBatchDuration = TimeSpan.FromMilliseconds(1),
                MinBatchSize = 1,
                MaxBatchSize = 20,
                MinDegreeOfParallelism = 1,
                MaxDegreeOfParallelism = 8
            }
        );

        Assert.Equal(80, result.RecordsProcessed);
        Assert.Equal(1, result.BatchSizeUsed);
        Assert.Equal(1, result.ThreadsUsed);
    }

    [Fact]
    public async Task AdaptiveExecuteAsync_WithCanceledToken_ReturnsCancelledResult()
    {
        var connection = new FakeConnection
        {
            ExecuteHandler = _ => Response(Success(0, "req"))
        };
        using var proxy = new SdkProxy(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(connection)
        );
        var logic = new ExecuteMultipleLogic(proxy, NullLogger.Instance);
        ExecuteMultipleRequestBuilder builder = BuilderWith("req");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        ExecuteMultipleLogicResult result = await logic.ExecuteAdaptiveAsync(
            builder,
            new AdaptiveRequesterSettings(),
            cancellationToken: cancellationTokenSource.Token
        );

        Assert.True(result.Cancelled);
        Assert.Empty(result.Results);
        Assert.Equal(0, result.RecordsProcessed);
    }

    [Fact]
    public async Task ExecuteAsync_WithCanceledToken_ReturnsCancelledResult()
    {
        var (proxy, logic) = Create(_ => Response(Success(0, "req")));

        using (proxy)
        {
            ExecuteMultipleRequestBuilder builder = BuilderWith("req");
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            ExecuteMultipleLogicResult result = await logic.ExecuteAsync(
                builder,
                new ExecuteMultipleRequestSimpleSettings { RequestSize = 1 },
                cancellationTokenSource.Token
            );

            Assert.True(result.Cancelled);
            Assert.Empty(result.Results);
            Assert.Equal(0, result.RecordsProcessed);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ReportsFinalProgress()
    {
        var reports = new ConcurrentBag<(int Current, int Maximum)>();
        var (proxy, logic) = Create(request =>
        {
            var executeMultipleRequest = (ExecuteMultipleRequest)request;
            return Response(Success(0, executeMultipleRequest.Requests[0].RequestName));
        });

        using (proxy)
        {
            ExecuteMultipleRequestBuilder builder = BuilderWith("req");

            await logic.ExecuteAsync(
                builder,
                new ExecuteMultipleRequestSettings
                {
                    RequestSize = 1,
                    ReportProgress = (current, maximum) => reports.Add((current, maximum)),
                    ReportProgressInterval = TimeSpan.FromMilliseconds(10),
                }
            );

            Assert.Contains((1, 1), reports);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RequestSizeAboveServiceLimit_IsCappedAt1000()
    {
        var batchSizes = new List<int>();
        var (proxy, logic) = Create(request =>
        {
            batchSizes.Add(((ExecuteMultipleRequest)request).Requests.Count);
            return Response();
        });

        using (proxy)
        {
            ExecuteMultipleRequestBuilder builder = BuilderWith(
                Enumerable.Range(0, 1501).Select(index => "req" + index).ToArray()
            );

            await logic.ExecuteAsync(
                builder,
                new ExecuteMultipleRequestSimpleSettings { RequestSize = 1500 }
            );

            Assert.Equal(new[] { 1000, 501 }, batchSizes);
        }
    }

    [Fact]
    public void Result_ToString_WorksWithDefaultInstance()
    {
        string text = new ExecuteMultipleLogicResult().ToString();

        Assert.Contains("Results count", text);
    }

    private sealed class MarkerResponse : OrganizationResponse
    {
        public string Marker { get; set; }
    }
}
