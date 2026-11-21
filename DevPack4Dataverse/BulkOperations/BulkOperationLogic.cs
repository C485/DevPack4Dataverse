/*
Copyright 2022 Kamil Skoracki / C485@GitHub

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System.Collections.Concurrent;
using Ardalis.GuardClauses;
using DevPack4Dataverse.Models;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace DevPack4Dataverse.BulkOperations;

/// <summary>
/// Chunks records and sends the bulk operation messages in parallel across the connection pool.
/// </summary>
/// <remarks>
/// Each chunk is one Dataverse request. On a standard table a chunk is all-or-nothing, so a failure
/// leaves earlier chunks committed. When a chunk throws, the original exception is rethrown and its
/// <see cref="Exception.Data"/> contains the failed chunk start index and count.
/// </remarks>
public sealed class BulkOperationLogic
{
    /// <summary>Key in <see cref="Exception.Data"/> with the failed chunk start index.</summary>
    public const string FailedChunkStartIndexDataKey =
        "DevPack4Dataverse.BulkOperation.StartIndex";

    /// <summary>Key in <see cref="Exception.Data"/> with the failed chunk record count.</summary>
    public const string FailedChunkCountDataKey = "DevPack4Dataverse.BulkOperation.Count";

    /// <summary>
    /// Key in <see cref="Exception.Data"/> with the indexes of chunks that completed before the
    /// failure. Chunks that were still running when the failure happened are included only if
    /// they finished before the operation stopped.
    /// </summary>
    public const string CompletedChunkIndicesDataKey =
        "DevPack4Dataverse.BulkOperation.CompletedChunkIndices";

    /// <summary>
    /// Key in <see cref="Exception.Data"/> with a snapshot of the created ids per completed chunk.
    /// </summary>
    public const string CompletedCreateIdsDataKey =
        "DevPack4Dataverse.BulkOperation.CompletedCreateIds";

    /// <summary>
    /// Key in <see cref="Exception.Data"/> with a snapshot of the upsert responses per completed chunk.
    /// </summary>
    public const string CompletedUpsertResponsesDataKey =
        "DevPack4Dataverse.BulkOperation.CompletedUpsertResponses";

    private readonly ILogger _logger;
    private readonly SdkProxy _sdkProxy;

    public BulkOperationLogic(SdkProxy sdkProxy, ILogger logger)
    {
        _sdkProxy = Guard.Against.Null(sdkProxy);
        _logger = Guard.Against.Null(logger);
    }

    /// <summary>
    /// Creates records in chunks with the <c>CreateMultiple</c> message.
    /// </summary>
    /// <returns>Created ids in the same order as <paramref name="records"/>.</returns>
    public async Task<Guid[]> CreateMultipleAsync(
        IEnumerable<Entity> records,
        BulkOperationSettings? settings = null,
        RequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default
    )
    {
        List<Entity> items = Materialize(records, nameof(records));
        if (items.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        EntityCollection prepared = PrepareEntities(items, false, nameof(records));
        BulkOperationSettings bulkSettings = (settings ?? new BulkOperationSettings()).Validate();
        ConcurrentDictionary<int, Guid[]> results = new();

        try
        {
            await RunChunksAsync(
                    prepared.Entities.ToList(),
                    "CreateMultiple",
                    bulkSettings,
                    cancellationToken,
                    async (chunk, index, token) =>
                    {
                        Guid[] ids = await _sdkProxy
                            .CreateMultipleAsync(
                                CreateEntityCollection(chunk, prepared.EntityName),
                                requestSettings,
                                token
                            )
                            .ConfigureAwait(false);
                        results[index] = ids;
                    }
                )
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            exception.Data[CompletedCreateIdsDataKey] = new Dictionary<int, Guid[]>(results);
            throw;
        }

        List<Guid> merged = new(items.Count);
        for (int index = 0; index < results.Count; index++)
        {
            if (results.TryGetValue(index, out Guid[]? ids))
            {
                merged.AddRange(ids);
            }
        }

        return merged.ToArray();
    }

    /// <summary>
    /// Updates records in chunks with the <c>UpdateMultiple</c> message.
    /// Every target must have an id or alternate-key values.
    /// </summary>
    public async Task UpdateMultipleAsync(
        IEnumerable<Entity> records,
        BulkOperationSettings? settings = null,
        RequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default
    )
    {
        List<Entity> items = Materialize(records, nameof(records));
        if (items.Count == 0)
        {
            return;
        }

        EntityCollection prepared = PrepareEntities(items, true, nameof(records));
        BulkOperationSettings bulkSettings = (settings ?? new BulkOperationSettings()).Validate();

        await RunChunksAsync(
                prepared.Entities.ToList(),
                "UpdateMultiple",
                bulkSettings,
                cancellationToken,
                async (chunk, _, token) =>
                {
                    await _sdkProxy
                        .UpdateMultipleAsync(
                            CreateEntityCollection(chunk, prepared.EntityName),
                            requestSettings,
                            token
                        )
                        .ConfigureAwait(false);
                }
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates or updates records in chunks with the <c>UpsertMultiple</c> message.
    /// Every target must have an id or alternate-key values.
    /// </summary>
    /// <returns>One result per target, in the same order as <paramref name="records"/>.</returns>
    public async Task<UpsertResponse[]> UpsertMultipleAsync(
        IEnumerable<Entity> records,
        BulkOperationSettings? settings = null,
        RequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default
    )
    {
        List<Entity> items = Materialize(records, nameof(records));
        if (items.Count == 0)
        {
            return Array.Empty<UpsertResponse>();
        }

        EntityCollection prepared = PrepareEntities(items, true, nameof(records));
        BulkOperationSettings bulkSettings = (settings ?? new BulkOperationSettings()).Validate();
        ConcurrentDictionary<int, UpsertResponse[]> results = new();

        try
        {
            await RunChunksAsync(
                    prepared.Entities.ToList(),
                    "UpsertMultiple",
                    bulkSettings,
                    cancellationToken,
                    async (chunk, index, token) =>
                    {
                        UpsertResponse[] responses = await _sdkProxy
                            .UpsertMultipleAsync(
                                CreateEntityCollection(chunk, prepared.EntityName),
                                requestSettings,
                                token
                            )
                            .ConfigureAwait(false);
                        results[index] = responses;
                    }
                )
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            exception.Data[CompletedUpsertResponsesDataKey] =
                new Dictionary<int, UpsertResponse[]>(results);
            throw;
        }

        List<UpsertResponse> merged = new(items.Count);
        for (int index = 0; index < results.Count; index++)
        {
            if (results.TryGetValue(index, out UpsertResponse[]? responses))
            {
                merged.AddRange(responses);
            }
        }

        return merged.ToArray();
    }

    /// <summary>
    /// Deletes references in chunks with the <c>DeleteMultiple</c> message.
    /// This message is supported only for elastic tables.
    /// </summary>
    public async Task DeleteMultipleAsync(
        IEnumerable<EntityReference> references,
        BulkOperationSettings? settings = null,
        RequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default
    )
    {
        List<EntityReference> items = Materialize(references, nameof(references));
        if (items.Count == 0)
        {
            return;
        }

        EntityReferenceCollection prepared = new(items);
        BulkOperationTargets.PrepareReferences(prepared, nameof(references));
        BulkOperationSettings bulkSettings = (settings ?? new BulkOperationSettings()).Validate();

        await RunChunksAsync(
                items,
                "DeleteMultiple",
                bulkSettings,
                cancellationToken,
                async (chunk, _, token) =>
                {
                    await _sdkProxy
                        .DeleteMultipleAsync(
                            new EntityReferenceCollection(new List<EntityReference>(chunk)),
                            requestSettings,
                            token
                        )
                        .ConfigureAwait(false);
                }
            )
            .ConfigureAwait(false);
    }

    private async Task RunChunksAsync<T>(
        IReadOnlyList<T> items,
        string operation,
        BulkOperationSettings settings,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<T>, int, CancellationToken, Task> chunkAction
    )
    {
        List<BulkChunk<T>> chunks = ChunkItems(items, settings.BatchSize);
        int degreeOfParallelism =
            settings.MaxDegreeOfParallelism <= 0
                ? Math.Max(1, _sdkProxy.ConnectionCount)
                : settings.MaxDegreeOfParallelism;
        int processed = 0;
        ConcurrentDictionary<int, byte> completedChunks = new();
        RepeatedTask? progressTask = null;

        if (settings.ReportProgress != null)
        {
            progressTask = new RepeatedTask(
                settings.ReportProgressInterval,
                () =>
                    ReportProgressSafely(
                        settings,
                        Volatile.Read(ref processed),
                        items.Count
                    ),
                _logger
            );
            progressTask.Start();
        }

        try
        {
            try
            {
                await AsyncParallel
                    .ForEachAsync(
                        chunks,
                        degreeOfParallelism,
                        cancellationToken,
                        async (chunk, token) =>
                        {
                            try
                            {
                                await chunkAction(chunk.Items, chunk.Index, token)
                                    .ConfigureAwait(false);
                            }
                            catch (Exception exception)
                            {
                                exception.Data[FailedChunkStartIndexDataKey] = chunk.StartIndex;
                                exception.Data[FailedChunkCountDataKey] = chunk.Items.Count;
                                throw;
                            }

                            completedChunks[chunk.Index] = 0;
                            Interlocked.Add(ref processed, chunk.Items.Count);
                        }
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // Every in-flight chunk has finished by now. Record what committed so a caller
                // can resume without re-sending completed work.
                exception.Data[CompletedChunkIndicesDataKey] = completedChunks
                    .Keys.OrderBy(index => index)
                    .ToArray();
                throw;
            }
        }
        finally
        {
            if (progressTask != null)
            {
                await progressTask.StopAsync().ConfigureAwait(false);
            }
        }

        ReportProgressSafely(settings, processed, items.Count);
    }

    private void ReportProgressSafely(
        BulkOperationSettings settings,
        int current,
        int total
    )
    {
        if (settings.ReportProgress == null)
        {
            return;
        }

        try
        {
            settings.ReportProgress(current, total);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Bulk operation progress callback failed.");
        }
    }

    private static EntityCollection CreateEntityCollection(
        IReadOnlyList<Entity> items,
        string entityName
    ) => new(new List<Entity>(items)) { EntityName = entityName };

    private static EntityCollection PrepareEntities(
        List<Entity> items,
        bool requireIdentity,
        string parameterName
    )
    {
        EntityCollection collection = new(items);
        return BulkOperationTargets.PrepareEntities(collection, requireIdentity, parameterName);
    }

    private static List<T> Materialize<T>(IEnumerable<T> items, string parameterName)
    {
        Guard.Against.Null(items, parameterName);
        return items.ToList();
    }

    private static List<BulkChunk<T>> ChunkItems<T>(
        IReadOnlyList<T> items,
        int batchSize
    )
    {
        int chunkCount = (items.Count - 1) / batchSize + 1;
        List<BulkChunk<T>> chunks = new(chunkCount);
        for (int offset = 0; offset < items.Count; offset += batchSize)
        {
            int count = Math.Min(batchSize, items.Count - offset);
            T[] chunk = new T[count];
            for (int index = 0; index < count; index++)
            {
                chunk[index] = items[offset + index];
            }

            chunks.Add(new BulkChunk<T>(chunks.Count, offset, chunk));
        }

        return chunks;
    }

    private sealed class BulkChunk<T>
    {
        public BulkChunk(int index, int startIndex, IReadOnlyList<T> items)
        {
            Index = index;
            StartIndex = startIndex;
            Items = items;
        }

        public int Index { get; }
        public int StartIndex { get; }
        public IReadOnlyList<T> Items { get; }
    }
}
