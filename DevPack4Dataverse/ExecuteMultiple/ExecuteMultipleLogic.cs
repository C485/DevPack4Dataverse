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
using System.Diagnostics;
using Ardalis.GuardClauses;
using DevPack4Dataverse.Models;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace DevPack4Dataverse.ExecuteMultiple;

/// <summary>
/// Executes batches of organization requests with <see cref="ExecuteMultipleRequest"/>.
/// </summary>
/// <remarks>
/// Requests are split into chunks of <see cref="ExecuteMultipleRequestSimpleSettings.RequestSize"/> and
/// executed with bounded parallelism. Results are returned in the original request order, across all chunks.
/// The full settings overload continues after request failures and reports them through
/// <see cref="ExecuteMultipleRequestSettings.ErrorReport"/>; the simple settings overload stops at the
/// first failed request. Cancellation returns the partial result with
/// <see cref="ExecuteMultipleLogicResult.Cancelled"/> set.
/// </remarks>
public sealed class ExecuteMultipleLogic
{
    private const int MaxRequestSize = 1000;
    private readonly ILogger _logger;
    private readonly SdkProxy _sdkProxy;

    /// <summary>
    /// Creates a new ExecuteMultiple executor.
    /// </summary>
    /// <param name="sdkProxy">Connection layer used to send requests. Cannot be null.</param>
    /// <param name="logger">Logger used by the executor. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public ExecuteMultipleLogic(SdkProxy sdkProxy, ILogger logger)
    {
        using EntryExitLogger logGuard = new(logger);
        _sdkProxy = Guard.Against.Null(sdkProxy);
        _logger = Guard.Against.Null(logger);
    }

    /// <summary>
    /// Creates a new builder for ExecuteMultiple requests.
    /// </summary>
    /// <param name="continueOnError">
    /// Value used by <see cref="ExecuteMultipleRequestBuilder.RequestWithResults"/> when the builder is
    /// executed directly. <see cref="ExecuteAsync(ExecuteMultipleRequestBuilder, ExecuteMultipleRequestSettings, CancellationToken)"/>
    /// and <see cref="ExecuteAsync(ExecuteMultipleRequestBuilder, ExecuteMultipleRequestSimpleSettings, CancellationToken)"/>
    /// define continuation behavior through their overloads.
    /// </param>
    /// <returns>A new, empty request builder.</returns>
    public ExecuteMultipleRequestBuilder CreateRequestBuilder(bool continueOnError = true)
    {
        return new ExecuteMultipleRequestBuilder(_logger, continueOnError);
    }

    /// <summary>
    /// Executes all requests, continuing after failed requests and reporting errors.
    /// </summary>
    /// <param name="executeMultipleRequestBuilder">Builder containing requests. Cannot be null.</param>
    /// <param name="executeMultipleRequestSettings">Execution settings. Cannot be null.</param>
    /// <param name="cancellationToken">Token used to stop scheduling further chunks.</param>
    /// <param name="requestSettings">Optional settings applied to the outer batch request.</param>
    /// <returns>Execution statistics and responses ordered by the original request order.</returns>
    public async Task<ExecuteMultipleLogicResult> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken = default,
        RequestSettings requestSettings = null
    )
    {
        using EntryExitLogger logGuard = new(_logger);

        ValidateSettings(executeMultipleRequestSettings);
        Guard.Against.Null(executeMultipleRequestBuilder, parameterName: nameof(executeMultipleRequestBuilder));

        OrganizationRequest[] requests = executeMultipleRequestBuilder.RequestWithResults.Requests.ToArray();
        if (requests.Length == 0)
        {
            return CreateEmptyResult();
        }

        int threadsCount = ResolveThreadsCount(executeMultipleRequestSettings.MaxDegreeOfParallelism);
        ExecuteMultipleLogicResult logicResult = new()
        {
            Stopwatch = Stopwatch.StartNew(),
            ThreadsUsed = threadsCount
        };

        int progress = 0;

        RepeatedTask repeatedTask = new(
            executeMultipleRequestSettings.ReportProgressInterval,
            () =>
            {
                executeMultipleRequestSettings.ReportProgress?.Invoke(
                    Thread.VolatileRead(ref progress),
                    requests.Length
                );
            },
            _logger
        );
        repeatedTask.Start();

        ConcurrentDictionary<int, ExecuteMultipleResponseItem> responsesByIndex = new();

        try
        {
            RequestChunk[] chunks = RequestsToChunks(requests, executeMultipleRequestSettings.RequestSize);

            try
            {
                await AsyncParallel
                    .ForEachAsync(
                        chunks,
                        threadsCount,
                        cancellationToken,
                        async (chunk, _) =>
                        {
                            OrganizationRequest[] packOfRequests = chunk.Requests;
                            try
                            {
                                ExecuteMultipleResponse responseWithResults =
                                    await ExecutePackAsync(
                                            packOfRequests,
                                            true,
                                            requestSettings,
                                            cancellationToken
                                        )
                                        .ConfigureAwait(false);

                                StoreResponses(
                                    responseWithResults,
                                    packOfRequests,
                                    chunk.StartIndex,
                                    responsesByIndex,
                                    executeMultipleRequestSettings.ErrorReport,
                                    false
                                );
                            }
                            catch (Exception e)
                            {
                                foreach (OrganizationRequest recordToSave in packOfRequests)
                                {
                                    executeMultipleRequestSettings.ErrorReport?.Invoke(
                                        recordToSave,
                                        e.ToString()
                                    );
                                }
                            }

                            Interlocked.Add(ref progress, packOfRequests.Length);
                        }
                    )
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation is reported through logicResult.Cancelled.
            }
        }
        finally
        {
            await repeatedTask.StopAsync().ConfigureAwait(false);
        }

        ExecuteMultipleLogicResult result = CompleteResult(
            logicResult,
            responsesByIndex,
            progress,
            requests.Length,
            cancellationToken
        );
        executeMultipleRequestSettings.ReportProgress?.Invoke(progress, requests.Length);
        return result;
    }

    /// <summary>
    /// Executes all requests, stopping at the first failed request.
    /// </summary>
    /// <param name="executeMultipleRequestBuilder">Builder containing requests. Cannot be null.</param>
    /// <param name="executeMultipleRequestSettings">Execution settings. Cannot be null.</param>
    /// <param name="cancellationToken">Token used to stop scheduling further chunks.</param>
    /// <param name="requestSettings">Optional settings applied to the outer batch request.</param>
    /// <returns>Execution statistics and responses ordered by the original request order.</returns>
    public async Task<ExecuteMultipleLogicResult> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSimpleSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken = default,
        RequestSettings requestSettings = null
    )
    {
        using EntryExitLogger logGuard = new(_logger);

        ValidateSettings(executeMultipleRequestSettings);
        Guard.Against.Null(executeMultipleRequestBuilder, parameterName: nameof(executeMultipleRequestBuilder));

        OrganizationRequest[] requests = executeMultipleRequestBuilder.RequestWithResults.Requests.ToArray();
        if (requests.Length == 0)
        {
            return CreateEmptyResult();
        }

        ExecuteMultipleLogicResult logicResult = new()
        {
            Stopwatch = Stopwatch.StartNew(),
            ThreadsUsed = 1
        };

        int progress = 0;

        ConcurrentDictionary<int, ExecuteMultipleResponseItem> responsesByIndex = new();

        RequestChunk[] chunks = RequestsToChunks(requests, executeMultipleRequestSettings.RequestSize);

        try
        {
            foreach (RequestChunk chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                OrganizationRequest[] packOfRequests = chunk.Requests;
                ExecuteMultipleResponse responseWithResults = await ExecutePackAsync(
                        packOfRequests,
                        false,
                        requestSettings,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                StoreResponses(
                    responseWithResults,
                    packOfRequests,
                    chunk.StartIndex,
                    responsesByIndex,
                    null,
                    true
                );

                Interlocked.Add(ref progress, packOfRequests.Length);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is reported through logicResult.Cancelled.
        }

        return CompleteResult(
            logicResult,
            responsesByIndex,
            progress,
            requests.Length,
            cancellationToken
        );
    }

    private async Task<ExecuteMultipleResponse> ExecutePackAsync(
        OrganizationRequest[] packOfRequests,
        bool continueOnError,
        RequestSettings requestSettings,
        CancellationToken cancellationToken
    )
    {
        ExecuteMultipleRequest requestWithResults =
            new()
            {
                Settings = new ExecuteMultipleSettings
                {
                    ContinueOnError = continueOnError,
                    ReturnResponses = true
                },
                Requests = new OrganizationRequestCollection()
            };

        requestWithResults.Requests.AddRange(packOfRequests);

        ExecuteMultipleResponse response = await _sdkProxy
            .ExecuteAsync<ExecuteMultipleResponse>(requestWithResults, requestSettings)
            .ConfigureAwait(false);

        return Guard.Against.Null(response);
    }

    private static void StoreResponses(
        ExecuteMultipleResponse response,
        OrganizationRequest[] packOfRequests,
        int startIndex,
        ConcurrentDictionary<int, ExecuteMultipleResponseItem> responsesByIndex,
        Action<OrganizationRequest, string> errorReport,
        bool throwOnFault
    )
    {
        foreach (ExecuteMultipleResponseItem responseItem in response.Responses)
        {
            if (responseItem == null)
            {
                continue;
            }

            EnsureValidResponseIndex(responseItem, packOfRequests.Length);
            int globalIndex = startIndex + responseItem.RequestIndex;
            responsesByIndex[globalIndex] = responseItem;

            if (responseItem.Fault == null)
            {
                continue;
            }

            if (throwOnFault)
            {
                throw new InvalidOperationException(
                    $"Request on index {globalIndex} failed with error, {responseItem.Fault}"
                );
            }

            errorReport?.Invoke(
                packOfRequests[responseItem.RequestIndex],
                responseItem.Fault.ToString()
            );
        }
    }

    /// <summary>
    /// Executes requests adaptively, adjusting ExecuteMultiple batch size first and then parallelism.
    /// </summary>
    /// <param name="executeMultipleRequestBuilder">Builder containing requests. Cannot be null.</param>
    /// <param name="settings">Adaptive settings. Defaults are used when null.</param>
    /// <param name="requestSettings">Optional settings applied to the outer batch requests.</param>
    /// <param name="cancellationToken">Token used to stop scheduling further waves.</param>
    /// <returns>Execution statistics and responses ordered by the original request order.</returns>
    public async Task<ExecuteMultipleLogicResult> ExecuteAdaptiveAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        AdaptiveRequesterSettings settings = null,
        RequestSettings requestSettings = null,
        CancellationToken cancellationToken = default
    )
    {
        using EntryExitLogger logGuard = new(_logger);
        Guard.Against.Null(executeMultipleRequestBuilder, parameterName: nameof(executeMultipleRequestBuilder));
        settings = (settings ?? new AdaptiveRequesterSettings()).Validate();

        OrganizationRequest[] requests = executeMultipleRequestBuilder.RequestWithResults.Requests.ToArray();
        if (requests.Length == 0)
        {
            return CreateEmptyResult();
        }

        int batchSize = settings.InitialBatchSize;
        int degreeOfParallelism = settings.InitialDegreeOfParallelism;
        ExecuteMultipleLogicResult logicResult = new()
        {
            Stopwatch = Stopwatch.StartNew(),
            ThreadsUsed = degreeOfParallelism,
            BatchSizeUsed = batchSize
        };

        int progress = 0;
        int nextIndex = 0;
        long recordsSinceAdjustment = 0;
        ConcurrentDictionary<int, ExecuteMultipleResponseItem> responsesByIndex = new();

        while (nextIndex < requests.Length)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            List<RequestChunk> wave = TakeAdaptiveWave(
                requests,
                ref nextIndex,
                batchSize,
                degreeOfParallelism
            );
            Stopwatch waveStopwatch = Stopwatch.StartNew();

            try
            {
                await AsyncParallel
                    .ForEachAsync(
                        wave,
                        degreeOfParallelism,
                        cancellationToken,
                        async (chunk, token) =>
                        {
                            OrganizationRequest[] packOfRequests = chunk.Requests;
                            try
                            {
                                ExecuteMultipleResponse responseWithResults =
                                    await ExecutePackAsync(
                                            packOfRequests,
                                            true,
                                            requestSettings,
                                            token
                                        )
                                        .ConfigureAwait(false);

                                StoreResponses(
                                    responseWithResults,
                                    packOfRequests,
                                    chunk.StartIndex,
                                    responsesByIndex,
                                    settings.ErrorReport,
                                    false
                                );
                            }
                            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception exception)
                            {
                                foreach (OrganizationRequest request in packOfRequests)
                                {
                                    settings.ErrorReport?.Invoke(request, exception.ToString());
                                }
                            }

                            Interlocked.Add(ref progress, packOfRequests.Length);
                        }
                    )
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation is reported through logicResult.Cancelled.
                break;
            }

            waveStopwatch.Stop();
            int waveRecords = 0;
            foreach (RequestChunk chunk in wave)
            {
                waveRecords += chunk.Requests.Length;
            }

            recordsSinceAdjustment += waveRecords;
            if (
                wave.Count == degreeOfParallelism
                && recordsSinceAdjustment >= settings.EvaluationRecordCount
            )
            {
                AdjustAdaptiveSettings(
                    settings,
                    waveStopwatch.Elapsed,
                    ref batchSize,
                    ref degreeOfParallelism
                );
                recordsSinceAdjustment = 0;
            }

            settings.ReportProgress?.Invoke(progress, requests.Length);
        }

        ExecuteMultipleLogicResult result = CompleteResult(
            logicResult,
            responsesByIndex,
            progress,
            requests.Length,
            cancellationToken
        );
        result.ThreadsUsed = degreeOfParallelism;
        result.BatchSizeUsed = batchSize;
        settings.ReportProgress?.Invoke(progress, requests.Length);
        return result;
    }

    private static List<RequestChunk> TakeAdaptiveWave(
        OrganizationRequest[] requests,
        ref int nextIndex,
        int batchSize,
        int degreeOfParallelism
    )
    {
        List<RequestChunk> chunks = new(degreeOfParallelism);
        for (
            int index = 0;
            index < degreeOfParallelism && nextIndex < requests.Length;
            index++
        )
        {
            int count = Math.Min(batchSize, requests.Length - nextIndex);
            OrganizationRequest[] packOfRequests = new OrganizationRequest[count];
            Array.Copy(requests, nextIndex, packOfRequests, 0, count);
            chunks.Add(new RequestChunk(nextIndex, packOfRequests));
            nextIndex += count;
        }

        return chunks;
    }

    private static void AdjustAdaptiveSettings(
        AdaptiveRequesterSettings settings,
        TimeSpan waveDuration,
        ref int batchSize,
        ref int degreeOfParallelism
    )
    {
        if (waveDuration > settings.TargetBatchDuration)
        {
            if (batchSize > settings.MinBatchSize)
            {
                batchSize--;
            }
            else if (degreeOfParallelism > settings.MinDegreeOfParallelism)
            {
                degreeOfParallelism--;
            }

            return;
        }

        long fastThresholdTicks = settings.TargetBatchDuration.Ticks * 7 / 10;
        if (waveDuration.Ticks < fastThresholdTicks)
        {
            if (batchSize < settings.MaxBatchSize)
            {
                batchSize++;
            }
            else if (degreeOfParallelism < settings.MaxDegreeOfParallelism)
            {
                degreeOfParallelism++;
            }
        }
    }

    private static void ValidateSettings(ExecuteMultipleRequestSimpleSettings settings)
    {
        Guard.Against.Null(settings, parameterName: nameof(settings));
        Guard.Against.NegativeOrZero(settings.RequestSize, parameterName: nameof(settings.RequestSize));
    }

    private int ResolveThreadsCount(int maxDegreeOfParallelism)
    {
        int threads = maxDegreeOfParallelism <= 0 ? _sdkProxy.ConnectionCount : maxDegreeOfParallelism;
        return Math.Max(1, threads);
    }

    private static ExecuteMultipleLogicResult CreateEmptyResult() =>
        new()
        {
            RecordsProcessed = 0,
            RecordsRequested = 0,
            Stopwatch = new Stopwatch(),
            ThreadsUsed = 0
        };

    private static ExecuteMultipleLogicResult CompleteResult(
        ExecuteMultipleLogicResult logicResult,
        ConcurrentDictionary<int, ExecuteMultipleResponseItem> responsesByIndex,
        int progress,
        int requestCount,
        CancellationToken cancellationToken
    )
    {
        logicResult.Stopwatch.Stop();
        logicResult.RecordsRequested = requestCount;
        logicResult.RecordsProcessed = progress;
        List<ExecuteMultipleResponseItem> orderedResponses = new(responsesByIndex.Count);
        for (int index = 0; index < requestCount; index++)
        {
            if (responsesByIndex.TryGetValue(index, out ExecuteMultipleResponseItem response))
            {
                orderedResponses.Add(response);
            }
        }

        logicResult.Results = orderedResponses.ToArray();
        logicResult.Cancelled = cancellationToken.IsCancellationRequested;
        return logicResult;
    }

    private static RequestChunk[] RequestsToChunks(OrganizationRequest[] requests, int requestSize)
    {
        // Dataverse's default maximum batch size is 1000; a deployment can lower it.
        requestSize = Math.Min(requestSize, MaxRequestSize);
        int chunkCount = (requests.Length + requestSize - 1) / requestSize;
        var chunks = new List<RequestChunk>(chunkCount);
        for (int offset = 0; offset < requests.Length; offset += requestSize)
        {
            int count = Math.Min(requestSize, requests.Length - offset);
            OrganizationRequest[] chunkRequests = new OrganizationRequest[count];
            Array.Copy(requests, offset, chunkRequests, 0, count);
            chunks.Add(new RequestChunk(offset, chunkRequests));
        }

        return chunks.ToArray();
    }

    private static void EnsureValidResponseIndex(
        ExecuteMultipleResponseItem responseItem,
        int requestCount
    )
    {
        if (responseItem.RequestIndex < 0 || responseItem.RequestIndex >= requestCount)
        {
            throw new InvalidOperationException(
                $"ExecuteMultiple response index {responseItem.RequestIndex} is outside the request range 0..{requestCount - 1}."
            );
        }
    }

    private sealed class RequestChunk(int startIndex, OrganizationRequest[] requests)
    {
        public int StartIndex { get; } = startIndex;
        public OrganizationRequest[] Requests { get; } = requests;
    }
}
