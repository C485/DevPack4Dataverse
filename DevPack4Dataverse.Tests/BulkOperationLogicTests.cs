using System.Collections.Concurrent;
using DevPack4Dataverse.BulkOperations;
using DevPack4Dataverse.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Xunit;

namespace DevPack4Dataverse.Tests;

public class BulkOperationLogicTests
{
    [Fact]
    public async Task CreateMultipleAsync_ChunksAndPreservesOrder()
    {
        Dictionary<string, Guid> ids = Enumerable
            .Range(0, 5)
            .ToDictionary(index => "R" + index, _ => Guid.NewGuid());
        ConcurrentBag<int> chunkSizes = new();
        var fake = new FakeConnection
        {
            CreateMultipleHandler = targets =>
            {
                chunkSizes.Add(targets.Entities.Count);
                return targets.Entities
                    .Select(entity => ids[(string)entity["name"]])
                    .ToArray();
            }
        };
        var (proxy, logic) = Create(fake);

        using (proxy)
        {
            Entity[] records = Enumerable
                .Range(0, 5)
                .Select(index => new Entity("account") { ["name"] = "R" + index })
                .ToArray();

            Guid[] result = await logic.CreateMultipleAsync(
                records,
                new BulkOperationSettings
                {
                    BatchSize = 2,
                    MaxDegreeOfParallelism = 1
                }
            );

            Assert.Equal(records.Select(record => ids[(string)record["name"]]), result);
            Assert.Equal(new[] { 1, 2, 2 }, chunkSizes.OrderBy(size => size).ToArray());
        }
    }

    [Fact]
    public async Task CreateMultipleAsync_EmptyInput_DoesNotCallDataverse()
    {
        bool called = false;
        var fake = new FakeConnection
        {
            CreateMultipleHandler = _ =>
            {
                called = true;
                return Array.Empty<Guid>();
            }
        };
        var (proxy, logic) = Create(fake);

        using (proxy)
        {
            Guid[] result = await logic.CreateMultipleAsync(Array.Empty<Entity>());

            Assert.Empty(result);
            Assert.False(called);
        }
    }

    [Fact]
    public async Task UpdateMultipleAsync_ChunksTargets()
    {
        int calls = 0;
        int records = 0;
        var fake = new FakeConnection
        {
            UpdateMultipleHandler = targets =>
            {
                Interlocked.Increment(ref calls);
                Interlocked.Add(ref records, targets.Entities.Count);
            }
        };
        var (proxy, logic) = Create(fake);

        using (proxy)
        {
            Entity[] updates = Enumerable
                .Range(0, 5)
                .Select(_ => new Entity("account", Guid.NewGuid()))
                .ToArray();

            await logic.UpdateMultipleAsync(
                updates,
                new BulkOperationSettings
                {
                    BatchSize = 2,
                    MaxDegreeOfParallelism = 1
                }
            );

            Assert.Equal(3, calls);
            Assert.Equal(5, records);
        }
    }

    [Fact]
    public async Task UpsertMultipleAsync_ChunksAndReturnsResults()
    {
        var fake = new FakeConnection
        {
            UpsertMultipleHandler = targets =>
                targets.Entities.Select(_ => new UpsertResponse()).ToArray()
        };
        var (proxy, logic) = Create(fake);

        using (proxy)
        {
            Entity[] records = Enumerable
                .Range(0, 5)
                .Select(_ => new Entity("account", Guid.NewGuid()))
                .ToArray();

            UpsertResponse[] results = await logic.UpsertMultipleAsync(
                records,
                new BulkOperationSettings
                {
                    BatchSize = 2,
                    MaxDegreeOfParallelism = 1
                }
            );

            Assert.Equal(5, results.Length);
        }
    }

    [Fact]
    public async Task DeleteMultipleAsync_ChunksReferences()
    {
        int calls = 0;
        var fake = new FakeConnection
        {
            DeleteMultipleHandler = _ => Interlocked.Increment(ref calls)
        };
        var (proxy, logic) = Create(fake);

        using (proxy)
        {
            EntityReference[] references = Enumerable
                .Range(0, 5)
                .Select(_ => new EntityReference("account", Guid.NewGuid()))
                .ToArray();

            await logic.DeleteMultipleAsync(
                references,
                new BulkOperationSettings
                {
                    BatchSize = 2,
                    MaxDegreeOfParallelism = 1
                }
            );

            Assert.Equal(3, calls);
        }
    }

    [Fact]
    public async Task InvalidBatchSize_Throws()
    {
        var fake = new FakeConnection();
        var (proxy, logic) = Create(fake);

        using (proxy)
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(
                () =>
                    logic.CreateMultipleAsync(
                        new[] { new Entity("account") },
                        new BulkOperationSettings { BatchSize = 0 }
                    )
            );
        }
    }

    [Fact]
    public async Task Progress_ReportsFinalCount()
    {
        ConcurrentBag<(int Current, int Total)> reports = new();
        var fake = new FakeConnection
        {
            CreateMultipleHandler = targets =>
                targets.Entities.Select(_ => Guid.NewGuid()).ToArray()
        };
        var (proxy, logic) = Create(fake);

        using (proxy)
        {
            Entity[] records = Enumerable
                .Range(0, 5)
                .Select(_ => new Entity("account"))
                .ToArray();

            await logic.CreateMultipleAsync(
                records,
                new BulkOperationSettings
                {
                    BatchSize = 2,
                    MaxDegreeOfParallelism = 1,
                    ReportProgress = (current, total) => reports.Add((current, total)),
                    ReportProgressInterval = TimeSpan.FromMilliseconds(5)
                }
            );

            Assert.Contains((5, 5), reports);
        }
    }

    [Fact]
    public async Task FailedChunk_AddsChunkContextToException()
    {
        var fake = new FakeConnection
        {
            CreateMultipleHandler = targets =>
            {
                if (targets.Entities.Any(entity => (string)entity["name"] == "R3"))
                {
                    throw new InvalidOperationException("failed chunk");
                }

                return targets.Entities.Select(_ => Guid.NewGuid()).ToArray();
            }
        };
        var (proxy, logic) = Create(fake);

        using (proxy)
        {
            Entity[] records = Enumerable
                .Range(0, 5)
                .Select(index => new Entity("account") { ["name"] = "R" + index })
                .ToArray();

            InvalidOperationException exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        logic.CreateMultipleAsync(
                            records,
                            new BulkOperationSettings
                            {
                                BatchSize = 2,
                                MaxDegreeOfParallelism = 1
                            }
                        )
                );

            Assert.Equal(2, exception.Data[BulkOperationLogic.FailedChunkStartIndexDataKey]);
            Assert.Equal(2, exception.Data[BulkOperationLogic.FailedChunkCountDataKey]);
            Assert.Equal(
                new[] { 0 },
                (int[])exception.Data[BulkOperationLogic.CompletedChunkIndicesDataKey]!
            );

            var completed = (Dictionary<int, Guid[]>)exception.Data[
                BulkOperationLogic.CompletedCreateIdsDataKey
            ]!;
            Assert.True(completed.ContainsKey(0));
            Assert.Equal(2, completed[0].Length);
            Assert.False(completed.ContainsKey(1));
        }
    }

    [Fact]
    public async Task UpdateMultipleAsync_RejectsDuplicateIdsBeforeChunking()
    {
        var fake = new FakeConnection
        {
            UpdateMultipleHandler = _ =>
                throw new InvalidOperationException("should not be called")
        };
        var (proxy, logic) = Create(fake);

        using (proxy)
        {
            Guid id = Guid.NewGuid();
            Entity[] records =
            {
                new Entity("account", id),
                new Entity("account", id)
            };

            await Assert.ThrowsAsync<ArgumentException>(
                () =>
                    logic.UpdateMultipleAsync(
                        records,
                        new BulkOperationSettings { BatchSize = 1 }
                    )
            );
        }
    }

    [Fact]
    public async Task UpsertMultipleAsync_RejectsDuplicateAlternateKeysBeforeChunking()
    {
        var fake = new FakeConnection
        {
            UpsertMultipleHandler = _ =>
                throw new InvalidOperationException("should not be called")
        };
        var (proxy, logic) = Create(fake);

        using (proxy)
        {
            var first = new Entity("account");
            first.KeyAttributes["accountnumber"] = "A-1";
            var second = new Entity("account");
            second.KeyAttributes["accountnumber"] = "A-1";

            await Assert.ThrowsAsync<ArgumentException>(
                () =>
                    logic.UpsertMultipleAsync(
                        new[] { first, second },
                        new BulkOperationSettings { BatchSize = 1 }
                    )
            );
        }
    }

    [Fact]
    public async Task UpsertMultipleAsync_FailedChunk_AddsCompletedResponses()
    {
        int calls = 0;
        var fake = new FakeConnection
        {
            UpsertMultipleHandler = targets =>
            {
                if (Interlocked.Increment(ref calls) == 2)
                {
                    throw new InvalidOperationException("failed chunk");
                }

                return targets.Entities.Select(_ => new UpsertResponse()).ToArray();
            }
        };
        var (proxy, logic) = Create(fake);

        using (proxy)
        {
            Entity[] records = Enumerable
                .Range(0, 2)
                .Select(_ => new Entity("account", Guid.NewGuid()))
                .ToArray();

            InvalidOperationException exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        logic.UpsertMultipleAsync(
                            records,
                            new BulkOperationSettings
                            {
                                BatchSize = 1,
                                MaxDegreeOfParallelism = 1
                            }
                        )
                );

            Assert.Equal(
                new[] { 0 },
                (int[])exception.Data[BulkOperationLogic.CompletedChunkIndicesDataKey]!
            );
            var completed = (Dictionary<int, UpsertResponse[]>)exception.Data[
                BulkOperationLogic.CompletedUpsertResponsesDataKey
            ]!;
            Assert.True(completed.ContainsKey(0));
            Assert.Single(completed[0]);
            Assert.False(completed.ContainsKey(1));
        }
    }

    [Fact]
    public async Task CreateMultipleAsync_ShortChunkResponse_Throws()
    {
        var fake = new FakeConnection
        {
            CreateMultipleHandler = targets =>
                targets.Entities
                    .Take(targets.Entities.Count - 1)
                    .Select(_ => Guid.NewGuid())
                    .ToArray()
        };
        var (proxy, logic) = Create(fake);

        using (proxy)
        {
            Entity[] records = Enumerable
                .Range(0, 3)
                .Select(_ => new Entity("account"))
                .ToArray();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    logic.CreateMultipleAsync(
                        records,
                        new BulkOperationSettings
                        {
                            BatchSize = 2,
                            MaxDegreeOfParallelism = 1
                        }
                    )
            );
        }
    }

    private static (SdkProxy Proxy, BulkOperationLogic Logic) Create(FakeConnection connection)
    {
        SdkProxy proxy = new(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(connection)
        );
        proxy.WaitForConnections(1);
        return (proxy, new BulkOperationLogic(proxy, NullLogger.Instance));
    }
}
