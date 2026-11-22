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
using System.ServiceModel;
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
    public Task<ExecuteMultipleLogicResult> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken = default,
        RequestSettings? requestSettings = null
    ) =>
        OperationLogger.RunLoggedAsync(
            _logger,
            nameof(ExecuteAsync),
            () =>
                ExecuteCoreAsync(
                    executeMultipleRequestBuilder,
                    executeMultipleRequestSettings,
                    cancellationToken,
                    requestSettings
                )
        );

    private async Task<ExecuteMultipleLogicResult> ExecuteCoreAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken,
        RequestSettings? requestSettings
    )
    {
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
                    Volatile.Read(ref progress),
                    requests.Length
                );
            },
            _logger
        );
        repeatedTask.Start();

        ConcurrentDictionary<int, ExecuteMultipleResponseItem> responsesByIndex = new();
        bool cancelled = false;

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
                        async (chunk, token) =>
                        {
                            OrganizationRequest[] packOfRequests = chunk.Requests;
                            try
                            {
                                List<ExecuteMultipleResponseItem> responseWithResults =
                                    await ExecutePackAsync(
                                            packOfRequests,
                                            true,
                                            requestSettings,
                                            executeMultipleRequestSettings.ErrorReport,
                                            token
                                        )
                                        .ConfigureAwait(false);

                                _ = StoreResponses(
                                    responseWithResults,
                                    packOfRequests,
                                    chunk.StartIndex,
                                    responsesByIndex,
                                    executeMultipleRequestSettings.ErrorReport,
                                    false
                                );
                            }
                            catch (OperationCanceledException) when (token.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception e)
                            {
                                foreach (OrganizationRequest recordToSave in packOfRequests)
                                {
                                    InvokeErrorReport(
                                        executeMultipleRequestSettings.ErrorReport,
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
                cancelled = true;
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
            cancelled
        );
        ReportProgressSafely(
            executeMultipleRequestSettings.ReportProgress,
            progress,
            requests.Length
        );
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
    public Task<ExecuteMultipleLogicResult> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSimpleSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken = default,
        RequestSettings? requestSettings = null
    ) =>
        OperationLogger.RunLoggedAsync(
            _logger,
            "ExecuteAsync (simple)",
            () =>
                ExecuteSimpleCoreAsync(
                    executeMultipleRequestBuilder,
                    executeMultipleRequestSettings,
                    cancellationToken,
                    requestSettings
                )
        );

    private async Task<ExecuteMultipleLogicResult> ExecuteSimpleCoreAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSimpleSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken,
        RequestSettings? requestSettings
    )
    {
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
        bool cancelled = false;
        ExecuteMultipleResponseItem? firstFault = null;
        int firstFaultIndex = -1;

        RequestChunk[] chunks = RequestsToChunks(requests, executeMultipleRequestSettings.RequestSize);

        try
        {
            foreach (RequestChunk chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                OrganizationRequest[] packOfRequests = chunk.Requests;
                List<ExecuteMultipleResponseItem> responseWithResults = await ExecutePackAsync(
                        packOfRequests,
                        false,
                        requestSettings,
                        null,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                firstFault = StoreResponses(
                    responseWithResults,
                    packOfRequests,
                    chunk.StartIndex,
                    responsesByIndex,
                    null,
                    true
                );

                if (firstFault != null)
                {
                    firstFaultIndex = chunk.StartIndex + firstFault.RequestIndex;
                    Interlocked.Add(ref progress, firstFault.RequestIndex + 1);
                    break;
                }

                Interlocked.Add(ref progress, packOfRequests.Length);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is reported through logicResult.Cancelled.
            cancelled = true;
        }

        ExecuteMultipleLogicResult result = CompleteResult(
            logicResult,
            responsesByIndex,
            progress,
            requests.Length,
            cancelled
        );

        if (firstFault != null)
        {
            throw new ExecuteMultipleException(
                $"Request on index {firstFaultIndex} failed with error, {firstFault.Fault}",
                result,
                firstFault
            );
        }

        return result;
    }

    private async Task<List<ExecuteMultipleResponseItem>> ExecutePackAsync(
        OrganizationRequest[] packOfRequests,
        bool continueOnError,
        RequestSettings? requestSettings,
        Action<OrganizationRequest, string>? errorReport,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await ExecutePackCoreAsync(
                    packOfRequests,
                    continueOnError,
                    requestSettings,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (FaultException<OrganizationServiceFault> fault)
            when (TryGetMaxBatchSize(fault, out int maxBatchSize)
                && maxBatchSize > 0
                && maxBatchSize < packOfRequests.Length)
        {
            List<ExecuteMultipleResponseItem> combined = new(packOfRequests.Length);
            for (int offset = 0; offset < packOfRequests.Length; offset += maxBatchSize)
            {
                int count = Math.Min(maxBatchSize, packOfRequests.Length - offset);
                OrganizationRequest[] part = new OrganizationRequest[count];
                Array.Copy(packOfRequests, offset, part, 0, count);

                List<ExecuteMultipleResponseItem> partResponse;
                try
                {
                    partResponse = await ExecutePackAsync(
                            part,
                            continueOnError,
                            requestSettings,
                            errorReport,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    if (!continueOnError)
                    {
                        throw;
                    }

                    // The split parts are separate requests. Report only this part
                    // and keep the responses already collected from earlier parts.
                    foreach (OrganizationRequest request in part)
                    {
                        InvokeErrorReport(errorReport, request, exception.ToString());
                    }

                    continue;
                }

                foreach (ExecuteMultipleResponseItem item in partResponse)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    item.RequestIndex += offset;
                    combined.Add(item);
                }

                if (!continueOnError && partResponse.Any(item => item is { Fault: not null }))
                {
                    break;
                }
            }

            return combined;
        }
    }

    private async Task<List<ExecuteMultipleResponseItem>> ExecutePackCoreAsync(
        OrganizationRequest[] packOfRequests,
        bool continueOnError,
        RequestSettings? requestSettings,
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
        requestSettings?.ApplyToInnerRequests(requestWithResults.Requests);

        ExecuteMultipleResponse response = (await _sdkProxy
            .ExecuteAsync<ExecuteMultipleResponse>(
                requestWithResults,
                requestSettings,
                cancellationToken
            )
            .ConfigureAwait(false))!;

        Guard.Against.Null(response);

        List<ExecuteMultipleResponseItem> items = new(
            response.Responses?.Count ?? packOfRequests.Length
        );
        if (response.Responses != null)
        {
            items.AddRange(response.Responses);
        }

        return items;
    }

    private static bool TryGetMaxBatchSize(
        FaultException<OrganizationServiceFault> fault,
        out int maxBatchSize
    )
    {
        maxBatchSize = 0;
        OrganizationServiceFault? detail = fault.Detail;
        if (detail?.ErrorDetails == null || !detail.ErrorDetails.Contains("MaxBatchSize"))
        {
            return false;
        }

        try
        {
            maxBatchSize = Convert.ToInt32(detail.ErrorDetails["MaxBatchSize"]);
            return maxBatchSize > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private ExecuteMultipleResponseItem? StoreResponses(
        IEnumerable<ExecuteMultipleResponseItem> responseItems,
        OrganizationRequest[] packOfRequests,
        int startIndex,
        ConcurrentDictionary<int, ExecuteMultipleResponseItem> responsesByIndex,
        Action<OrganizationRequest, string>? errorReport,
        bool stopAtFirstFault
    )
    {
        ExecuteMultipleResponseItem? firstFault = null;
        foreach (ExecuteMultipleResponseItem responseItem in responseItems)
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

            if (firstFault == null)
            {
                firstFault = responseItem;
            }

            if (stopAtFirstFault)
            {
                break;
            }

            InvokeErrorReport(
                errorReport,
                packOfRequests[responseItem.RequestIndex],
                responseItem.Fault.ToString()
            );
        }

        return firstFault;
    }

    /// <summary>
    /// Executes requests adaptively, adjusting ExecuteMultiple batch size first and then parallelism.
    /// </summary>
    /// <param name="executeMultipleRequestBuilder">Builder containing requests. Cannot be null.</param>
    /// <param name="settings">Adaptive settings. Defaults are used when null.</param>
    /// <param name="requestSettings">Optional settings applied to the outer batch requests.</param>
    /// <param name="cancellationToken">Token used to stop scheduling further waves.</param>
    /// <returns>Execution statistics and responses ordered by the original request order.</returns>
    public Task<ExecuteMultipleLogicResult> ExecuteAdaptiveAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        AdaptiveRequesterSettings? settings = null,
        RequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default
    ) =>
        OperationLogger.RunLoggedAsync(
            _logger,
            nameof(ExecuteAdaptiveAsync),
            () =>
                ExecuteAdaptiveCoreAsync(
                    executeMultipleRequestBuilder,
                    settings,
                    requestSettings,
                    cancellationToken
                )
        );

    private async Task<ExecuteMultipleLogicResult> ExecuteAdaptiveCoreAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        AdaptiveRequesterSettings? settings,
        RequestSettings? requestSettings,
        CancellationToken cancellationToken
    )
    {
        Guard.Against.Null(executeMultipleRequestBuilder, parameterName: nameof(executeMultipleRequestBuilder));
        settings = (settings ?? new AdaptiveRequesterSettings()).Validate();

        OrganizationRequest[] requests = executeMultipleRequestBuilder.RequestWithResults.Requests.ToArray();
        if (requests.Length == 0)
        {
            return CreateEmptyResult();
        }

        int batchSize = settings.InitialBatchSize;
        int degreeOfParallelism = settings.InitialDegreeOfParallelism;
        settings.CurrentDegreeOfParallelism = degreeOfParallelism;
        settings.CurrentBatchSize = batchSize;
        bool warmup = true;
        ExecuteMultipleLogicResult logicResult = new()
        {
            Stopwatch = Stopwatch.StartNew(),
            ThreadsUsed = degreeOfParallelism,
            BatchSizeUsed = batchSize
        };

        int progress = 0;
        int nextIndex = 0;
        long recordsSinceAdjustment = 0;
        int lastEvaluatedBatchSize = 0;
        bool dopProbeActive = false;
        bool dopSettled = false;
        int dopBeforeProbe = 0;
        long dopProbeBaselineTicks = 0;
        int dopProbeBaselineRecords = 0;
        ConcurrentDictionary<int, ExecuteMultipleResponseItem> responsesByIndex = new();
        bool cancelled = false;

        while (nextIndex < requests.Length)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            int waveBatchSize = batchSize;
            int waveDop = degreeOfParallelism;
            List<RequestChunk> wave = TakeAdaptiveWave(
                requests,
                ref nextIndex,
                waveBatchSize,
                waveDop
            );
            Stopwatch waveStopwatch = Stopwatch.StartNew();
            int waveServiceLimitFaults = 0;

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
                                List<ExecuteMultipleResponseItem> responseWithResults =
                                    await ExecutePackAsync(
                                            packOfRequests,
                                            true,
                                            requestSettings,
                                            settings.ErrorReport,
                                            token
                                        )
                                        .ConfigureAwait(false);

                                int serviceLimitFaults = 0;
                                foreach (
                                    ExecuteMultipleResponseItem responseItem in responseWithResults
                                )
                                {
                                    if (
                                        responseItem?.Fault is { } fault
                                        && IsServiceProtectionLimit(fault.ErrorCode)
                                    )
                                    {
                                        serviceLimitFaults++;
                                    }
                                }

                                if (serviceLimitFaults > 0)
                                {
                                    Interlocked.Add(
                                        ref waveServiceLimitFaults,
                                        serviceLimitFaults
                                    );
                                }

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
                                if (
                                    exception is FaultException<OrganizationServiceFault> fault
                                    && fault.Detail is { } faultDetail
                                    && IsServiceProtectionLimit(faultDetail.ErrorCode)
                                )
                                {
                                    Interlocked.Increment(ref waveServiceLimitFaults);
                                }

                                foreach (OrganizationRequest request in packOfRequests)
                                {
                                    InvokeErrorReport(
                                        settings.ErrorReport,
                                        request,
                                        exception.ToString()
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
                cancelled = true;
                break;
            }

            waveStopwatch.Stop();
            int waveRecords = 0;
            foreach (RequestChunk chunk in wave)
            {
                waveRecords += chunk.Requests.Length;
            }

            recordsSinceAdjustment += waveRecords;
            bool isWarmupWave = warmup;
            warmup = false;
            if (wave.Count == waveDop)
            {
                bool evaluated = false;

                if (dopProbeActive)
                {
                    // A probe is one full wave at the next degree of parallelism. Keep it only
                    // when throughput did not regress and no service limit was hit.
                    dopProbeActive = false;
                    double baselineThroughput =
                        dopProbeBaselineRecords / (double)Math.Max(1, dopProbeBaselineTicks);
                    double probeThroughput =
                        waveRecords / (double)Math.Max(1, waveStopwatch.Elapsed.Ticks);
                    if (
                        waveServiceLimitFaults > 0
                        || probeThroughput < baselineThroughput * 0.8
                    )
                    {
                        degreeOfParallelism = dopBeforeProbe;
                        dopSettled = true;
                    }

                    evaluated = true;
                }
                else if (waveServiceLimitFaults > 0)
                {
                    // Service protection limits mean the service cannot take more load:
                    // back off one step and stop growing parallelism for this run.
                    if (degreeOfParallelism > settings.MinDegreeOfParallelism)
                    {
                        degreeOfParallelism--;
                    }
                    else if (batchSize > settings.MinBatchSize)
                    {
                        batchSize = Math.Max(settings.MinBatchSize, batchSize / 2);
                    }

                    dopSettled = true;
                    evaluated = true;
                }
                else if (
                    !isWarmupWave
                    && recordsSinceAdjustment >= settings.EvaluationRecordCount
                )
                {
                    // The first wave includes JIT, connection setup, and warm-up work. Measure
                    // it, but do not let it distort the batch size.
                    bool isSlow = IsSlow(settings, waveStopwatch.Elapsed);
                    bool isFast = IsFast(settings, waveStopwatch.Elapsed);
                    bool batchStable =
                        lastEvaluatedBatchSize > 0
                        && Math.Abs(waveBatchSize - lastEvaluatedBatchSize)
                            <= Math.Max(1, lastEvaluatedBatchSize / 5);
                    if (
                        !isSlow
                        && !dopSettled
                        && degreeOfParallelism < settings.MaxDegreeOfParallelism
                        && (isFast ? batchSize >= settings.MaxBatchSize : batchStable)
                    )
                    {
                        // The batch is at its target (or the cap) and the run is healthy.
                        // Probe one more parallel request; the next full wave decides.
                        dopBeforeProbe = degreeOfParallelism;
                        dopProbeBaselineTicks = Math.Max(1, waveStopwatch.Elapsed.Ticks);
                        dopProbeBaselineRecords = waveRecords;
                        degreeOfParallelism++;
                        dopProbeActive = true;
                    }
                    else
                    {
                        AdjustAdaptiveSettings(
                            settings,
                            waveStopwatch.Elapsed,
                            ref batchSize,
                            ref degreeOfParallelism
                        );
                    }

                    evaluated = true;
                }

                if (evaluated)
                {
                    recordsSinceAdjustment = 0;
                }

                lastEvaluatedBatchSize = waveBatchSize;
            }

            settings.CurrentDegreeOfParallelism = degreeOfParallelism;
            settings.CurrentBatchSize = batchSize;
            ReportProgressSafely(settings.ReportProgress, progress, requests.Length);
        }

        ExecuteMultipleLogicResult result = CompleteResult(
            logicResult,
            responsesByIndex,
            progress,
            requests.Length,
            cancelled
        );
        result.ThreadsUsed = degreeOfParallelism;
        result.BatchSizeUsed = batchSize;
        settings.CurrentDegreeOfParallelism = degreeOfParallelism;
        settings.CurrentBatchSize = batchSize;
        ReportProgressSafely(settings.ReportProgress, progress, requests.Length);
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
        if (IsSlow(settings, waveDuration))
        {
            if (batchSize > settings.MinBatchSize)
            {
                // Scale to the target in one step instead of subtracting one per wave. A big
                // overshoot is exactly where a fixed one-step decrease is too slow.
                double ratio =
                    settings.TargetBatchDuration.Ticks / (double)Math.Max(1, waveDuration.Ticks);
                int scaled = (int)Math.Floor(batchSize * ratio);
                if (scaled >= batchSize)
                {
                    scaled = batchSize - 1;
                }

                batchSize = Math.Max(settings.MinBatchSize, scaled);
            }
            else if (degreeOfParallelism > settings.MinDegreeOfParallelism)
            {
                degreeOfParallelism--;
            }

            return;
        }

        if (IsFast(settings, waveDuration) && batchSize < settings.MaxBatchSize)
        {
            int growth = Math.Max(1, batchSize / 2);
            batchSize = Math.Min(settings.MaxBatchSize, batchSize + growth);
        }
    }

    private static bool IsSlow(AdaptiveRequesterSettings settings, TimeSpan waveDuration) =>
        waveDuration > settings.TargetBatchDuration;

    private static bool IsFast(AdaptiveRequesterSettings settings, TimeSpan waveDuration) =>
        waveDuration.Ticks < settings.TargetBatchDuration.Ticks / 10 * 7;

    /// <summary>
    /// Detects Dataverse service protection limit faults. Codes are documented in
    /// docs/powerapps-docs/powerapps-docs/developer/data-platform/api-limits.md.
    /// </summary>
    private static bool IsServiceProtectionLimit(int errorCode) =>
        errorCode == -2147015902
        || errorCode == -2147015903
        || errorCode == -2147015898
        || errorCode == 429;

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
        bool cancelled
    )
    {
        logicResult.Stopwatch.Stop();
        logicResult.RecordsRequested = requestCount;
        logicResult.RecordsProcessed = progress;
        List<ExecuteMultipleResponseItem> orderedResponses = new(responsesByIndex.Count);
        for (int index = 0; index < requestCount; index++)
        {
            if (responsesByIndex.TryGetValue(index, out ExecuteMultipleResponseItem? response))
            {
                orderedResponses.Add(response);
            }
        }

        logicResult.Results = orderedResponses.ToArray();
        logicResult.Cancelled = cancelled;
        return logicResult;
    }

    private void ReportProgressSafely(Action<int, int>? callback, int current, int total)
    {
        if (callback == null)
        {
            return;
        }

        try
        {
            callback(current, total);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "ReportProgress callback failed. Execution continues.");
        }
    }

    private void InvokeErrorReport(
        Action<OrganizationRequest, string>? callback,
        OrganizationRequest request,
        string error
    )
    {
        if (callback == null)
        {
            return;
        }

        try
        {
            callback(request, error);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "ErrorReport callback failed. Execution continues.");
        }
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
