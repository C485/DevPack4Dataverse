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

using Ardalis.GuardClauses;
using DevPack4Dataverse.Extension;
using DevPack4Dataverse.Models;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DevPack4Dataverse.ExecuteMultiple;

public sealed class ExecuteMultipleLogic
{
    private readonly ServiceClientContainer _sdkProxy;

    public ExecuteMultipleLogic(ServiceClientContainer sdkProxy)
    {
        _sdkProxy = Guard.Against.Null(sdkProxy);
    }

    public ExecuteMultipleRequestBuilder CreateRequestBuilder(bool continueOnError = true)
    {
        return new ExecuteMultipleRequestBuilder(continueOnError);
    }

    public async Task<ExecuteMultipleLogicResult> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken = default
    )
    {

        Guard.Against.Null(executeMultipleRequestSettings);

        Guard.Against.Null(executeMultipleRequestBuilder);

        if (executeMultipleRequestBuilder.Count == 0)
        {
            return new ExecuteMultipleLogicResult
            {
                RecordsProcessed = 0,
                Stopwatch = new Stopwatch(),
                ThreadsUsed = 0
            };
        }

        int threadsCount =
            executeMultipleRequestSettings.MaxDegreeOfParallelism <= 0
                ? _sdkProxy.Count
                : executeMultipleRequestSettings.MaxDegreeOfParallelism;

        //int progress = 0;
        InnerExecuteMultipleLogicWorker innerExecuteMultipleLogicWorker =
            new(
                _sdkProxy,
                threadsCount,
                executeMultipleRequestBuilder,
                executeMultipleRequestSettings,
                cancellationToken
            );
        ExecuteMultipleLogicResult logicResult =
            new()
            {
                Stopwatch = Stopwatch.StartNew(),
                ThreadsUsed = threadsCount,
                RecordsRequested = executeMultipleRequestBuilder.Count
            };
        RepeatedTask repeatedTask =
            new(
                executeMultipleRequestSettings.ReportProgressInterval,
                () =>
                {
                    executeMultipleRequestSettings.ReportProgress(
                        innerExecuteMultipleLogicWorker.Progress,
                        executeMultipleRequestBuilder.Count
                    );
                }
                
            );
        repeatedTask.Start();

        try
        {
            ////OrganizationRequest[][] allRequestChunks = RequestsToChunks(
            ////    executeMultipleRequestBuilder,
            ////    executeMultipleRequestSettings
            ////);

            //await Parallel.ForEachAsync(
            //    allRequestChunks,
            //    new ParallelOptions { MaxDegreeOfParallelism = threadsCount, CancellationToken = cancellationToken },
            //    async (packOfRequests, _) =>
            //    {

            //    }
            //);
            await innerExecuteMultipleLogicWorker.ProcessItemsAsync();
        }
        finally
        {
            await repeatedTask.StopAsync();
        }

        executeMultipleRequestSettings.ReportProgress(
            innerExecuteMultipleLogicWorker.Progress,
            executeMultipleRequestBuilder.Count
        );

        logicResult.Stopwatch.Stop();
        logicResult.RecordsProcessed = innerExecuteMultipleLogicWorker.Progress;
        logicResult.Results = responsesList;
        logicResult.Canceled = _cancellationToken.IsCancellationRequested;

        return logicResult;
    }

    private sealed class InnerExecuteMultipleLogicWorker
    {
        private readonly ServiceClientContainer _sdkProxy;
        private readonly ILogger _logger;
        private readonly ThreadSafeBagTakeMany<OrganizationRequest> _bag;
        private readonly int _threadCount;
        private readonly ExecuteMultipleRequestBuilder _executeMultipleRequestBuilder;
        private readonly ExecuteMultipleRequestSettings _executeMultipleRequestSettings;
        private readonly CancellationToken _cancellationToken;
        private readonly AdaptiveRequester _adaptiveRequester;
        private int progress;

        public InnerExecuteMultipleLogicWorker(
            ServiceClientContainer sdkProxy,
            ILogger logger,
            int threadCount,
            ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
            ExecuteMultipleRequestSettings executeMultipleRequestSettings,
            CancellationToken cancellationToken
        )
        {
            _sdkProxy = sdkProxy;
            _logger = logger;
            _threadCount = threadCount;
            _executeMultipleRequestBuilder = executeMultipleRequestBuilder;
            _executeMultipleRequestSettings = executeMultipleRequestSettings;
            _cancellationToken = cancellationToken;
            _adaptiveRequester = new AdaptiveRequester(executeMultipleRequestSettings.AdaptiveRequesterSettings);
            _bag = [.. _executeMultipleRequestBuilder.RequestWithResults.Requests];
        }

        public int Progress => Thread.VolatileRead(ref progress);

        public async Task<ExecuteMultipleLogicResult> ProcessItemsAsync()
        {
            List<Task> tasks = [];
            ConcurrentBag<ExecuteMultipleResponseItem> responsesList = [];

            for (int i = 0; i < _threadCount; i++)
            {
                tasks.Add(
                    Task.Run(async () =>
                    {
                        bool threadFinished = false;
                        while (!_cancellationToken.IsCancellationRequested && !threadFinished)
                        {
                            await _adaptiveRequester.ExecuteAsync(
                                async (requestedSize) =>
                                {
                                    IReadOnlyList<OrganizationRequest> workingItems = _bag.TakeMany(requestedSize);

                                    if (workingItems.Count == 0)
                                    {
                                        threadFinished = true;
                                    }

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
                                                Requests = []
                                            };

                                        requestWithResults.Requests.AddRange(workingItems);

                                        ExecuteMultipleResponse? responseWithResults =
                                            await _sdkProxy.GetRandom().ExtExecuteAsync<ExecuteMultipleResponse>(requestWithResults);

                                        foreach (
                                            ExecuteMultipleResponseItem responseItem in responseWithResults.Responses
                                        )
                                        {
                                            responsesList.Add(responseItem);

                                            if (responseItem.Fault == null)
                                            {
                                                continue;
                                            }

                                            OrganizationRequest requestOrigin = requestWithResults.Requests[
                                                responseItem.RequestIndex
                                            ];

                                            _executeMultipleRequestSettings.ErrorReport(
                                                requestOrigin,
                                                responseItem.Fault.ToString()
                                            );
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        foreach (OrganizationRequest recordToSave in workingItems)
                                        {
                                            _executeMultipleRequestSettings.ErrorReport(recordToSave, e.ToString());
                                        }
                                    }

                                    Interlocked.Add(ref progress, workingItems.Count);

                                    return workingItems.Count;
                                }
                            );
                        }
                    })
                );
            }

            await Task.WhenAll(tasks);

            return logicResult;
        }
    }

    public async Task<ExecuteMultipleLogicResult> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSimpleSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken = default
    )
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard.Against.Null(executeMultipleRequestSettings);

        Guard.Against.Null(executeMultipleRequestBuilder);

        if (executeMultipleRequestBuilder.Count == 0)
        {
            return new ExecuteMultipleLogicResult
            {
                RecordsProcessed = 0,
                Stopwatch = new Stopwatch(),
                ThreadsUsed = 0
            };
        }

        int threadsCount =
            executeMultipleRequestSettings.MaxDegreeOfParallelism <= 0
                ? _sdkProxy.ConnectionCount
                : executeMultipleRequestSettings.MaxDegreeOfParallelism;

        ExecuteMultipleLogicResult logicResult = new() { Stopwatch = Stopwatch.StartNew(), ThreadsUsed = threadsCount };

        int progress = 0;

        //OrganizationRequest[][] allRequestChunks = RequestsToChunks(
        //    executeMultipleRequestBuilder,
        //    executeMultipleRequestSettings
        //);

        ConcurrentBag<ExecuteMultipleResponseItem> responsesList = [];

        //await Parallel.ForEachAsync(
        //    allRequestChunks,
        //    new ParallelOptions { MaxDegreeOfParallelism = threadsCount, CancellationToken = cancellationToken },
        //    async (packOfRequests, _) =>
        //    {
        //        ExecuteMultipleRequest requestWithResults =
        //            new()
        //            {
        //                Settings = new ExecuteMultipleSettings { ContinueOnError = false, ReturnResponses = true },
        //                Requests = new OrganizationRequestCollection()
        //            };

        //        requestWithResults.Requests.AddRange(packOfRequests);

        //        ExecuteMultipleResponse responseWithResults = await _sdkProxy.ExecuteAsync<ExecuteMultipleResponse>(
        //            requestWithResults
        //        );

        //        foreach (ExecuteMultipleResponseItem responseItem in responseWithResults.Responses)
        //        {
        //            responsesList.Add(responseItem);
        //            if (responseItem.Fault == null)
        //            {
        //                continue;
        //            }

        //            throw new InvalidProgramException(
        //                $"Request on index {responseItem.RequestIndex} failed with error, {responseItem.Fault}"
        //            );
        //        }

        //        Interlocked.Add(ref progress, packOfRequests.Length);
        //    }
        //);

        //logicResult.Stopwatch.Stop();
        //logicResult.RecordsRequested = executeMultipleRequestBuilder.RequestWithResults.Requests.Count;
        //logicResult.RecordsProcessed = progress;
        //logicResult.Results = responsesList;
        //logicResult.Canceled = cancellationToken.IsCancellationRequested;

        return logicResult;
    }

    //private OrganizationRequest[][] RequestsToChunks(
    //    ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
    //    ExecuteMultipleRequestSimpleSettings executeMultipleRequestSettings
    //)
    //{
    //    using EntryExitLogger logGuard = new(_logger);
    //    return executeMultipleRequestBuilder.RequestWithResults.Requests
    //        .Select((s, i) => new { Value = s, Index = i })
    //        .GroupBy(
    //            p =>
    //                p.Index
    //                / /*executeMultipleRequestSettings.RequestSize*/
    //                1
    //        )
    //        .Select(p => p.Select(x => x.Value).ToArray())
    //        .ToArray();
    //}
}
