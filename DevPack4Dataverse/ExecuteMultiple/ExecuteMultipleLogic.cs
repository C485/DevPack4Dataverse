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
    /// <returns>Execution statistics and responses ordered by the original request order.</returns>
    public async Task<ExecuteMultipleLogicResult> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken = default
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
                                ExecuteMultipleRequest requestWithResults =
                                    new()
                                    {
                                        Settings = new ExecuteMultipleSettings
                                        {
                                            ContinueOnError = true,
                                            ReturnResponses = true
                                        },
                                        Requests = new OrganizationRequestCollection()
                                    };

                                requestWithResults.Requests.AddRange(packOfRequests);

                                ExecuteMultipleResponse responseWithResults = await _sdkProxy
                                    .ExecuteAsync<ExecuteMultipleResponse>(requestWithResults)
                                    .ConfigureAwait(false);

                                foreach (
                                    ExecuteMultipleResponseItem responseItem in responseWithResults.Responses
                                )
                                {
                                    EnsureValidResponseIndex(responseItem, packOfRequests.Length);
                                    responsesByIndex[chunk.StartIndex + responseItem.RequestIndex] = responseItem;

                                    if (responseItem.Fault == null)
                                    {
                                        continue;
                                    }

                                    executeMultipleRequestSettings.ErrorReport?.Invoke(
                                        packOfRequests[responseItem.RequestIndex],
                                        responseItem.Fault.ToString()
                                    );
                                }
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
    /// <returns>Execution statistics and responses ordered by the original request order.</returns>
    public async Task<ExecuteMultipleLogicResult> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSimpleSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken = default
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

        ConcurrentDictionary<int, ExecuteMultipleResponseItem> responsesByIndex = new();

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
                        ExecuteMultipleRequest requestWithResults =
                            new()
                            {
                                Settings = new ExecuteMultipleSettings
                                {
                                    ContinueOnError = false,
                                    ReturnResponses = true
                                },
                                Requests = new OrganizationRequestCollection()
                            };

                        requestWithResults.Requests.AddRange(packOfRequests);

                        ExecuteMultipleResponse responseWithResults = await _sdkProxy
                            .ExecuteAsync<ExecuteMultipleResponse>(requestWithResults)
                            .ConfigureAwait(false);

                        foreach (ExecuteMultipleResponseItem responseItem in responseWithResults.Responses)
                        {
                            EnsureValidResponseIndex(responseItem, packOfRequests.Length);
                            int globalIndex = chunk.StartIndex + responseItem.RequestIndex;
                            responsesByIndex[globalIndex] = responseItem;

                            if (responseItem.Fault != null)
                            {
                                throw new InvalidOperationException(
                                    $"Request on index {globalIndex} failed with error, {responseItem.Fault}"
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

        return CompleteResult(
            logicResult,
            responsesByIndex,
            progress,
            requests.Length,
            cancellationToken
        );
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
        logicResult.Results = responsesByIndex
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value)
            .ToArray();
        logicResult.Cancelled = cancellationToken.IsCancellationRequested;
        return logicResult;
    }

    private static RequestChunk[] RequestsToChunks(OrganizationRequest[] requests, int requestSize)
    {
        var chunks = new List<RequestChunk>();
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
