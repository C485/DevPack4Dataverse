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
using CommunityToolkit.Diagnostics;
using DevPack4Dataverse.Models;
using DevPack4Dataverse.Utils;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace DevPack4Dataverse.New.ExecuteMultiple;

public sealed class ExecuteMultipleLogic
{
    private readonly ConnectionsBag _connectionsBag;

    public ExecuteMultipleLogic(ConnectionsBag connectionsBag)
    {
        Guard.IsNotNull(connectionsBag);
        _connectionsBag = connectionsBag;
    }

    public ExecuteMultipleRequestBuilder CreateRequestBuilder(bool continueOnError = true)
    {
        return new ExecuteMultipleRequestBuilder(continueOnError);
    }

    public async Task<ExecuteMultipleLogicResult> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(executeMultipleRequestSettings);

        Guard.IsNotNull(executeMultipleRequestBuilder);

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
                ? _connectionsBag.Count
                : executeMultipleRequestSettings.MaxDegreeOfParallelism;

        ExecuteMultipleLogicResult logicResult = new()
        {
            Stopwatch = Stopwatch.StartNew(),
            ThreadsUsed = threadsCount
        };

        int progress = 0;

        RepeatedTask repeatedTask =
            new(executeMultipleRequestSettings.ReportProgressInterval,
                () =>
                {
                    executeMultipleRequestSettings.ReportProgress(Thread.VolatileRead(ref progress),
                        executeMultipleRequestBuilder.Count);
                });

        repeatedTask.Start();

        ConcurrentBag<ExecuteMultipleResponseItem> responsesList = new();

        try
        {
            OrganizationRequest[][] allRequestChunks = RequestsToChunks(executeMultipleRequestBuilder,
                executeMultipleRequestSettings);

            await Parallel.ForEachAsync(allRequestChunks,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = threadsCount,
                    CancellationToken = cancellationToken
                },
                async (packOfRequests, _) =>
                {
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

                        ExecuteMultipleResponse responseWithResults =
                            await _connectionsBag.ExecuteAsync<ExecuteMultipleResponse>(requestWithResults);

                        foreach (ExecuteMultipleResponseItem responseItem in responseWithResults.Responses)
                        {
                            responsesList.Add(responseItem);

                            if (responseItem.Fault == null)
                            {
                                continue;
                            }

                            OrganizationRequest requestOrigin = requestWithResults.Requests[responseItem.RequestIndex];

                            executeMultipleRequestSettings.ErrorReport(requestOrigin, responseItem.Fault.ToString());
                        }
                    }
                    catch (Exception e)
                    {
                        foreach (OrganizationRequest recordToSave in packOfRequests)
                        {
                            executeMultipleRequestSettings.ErrorReport(recordToSave, e.ToString());
                        }
                    }

                    Interlocked.Add(ref progress, packOfRequests.Length);
                });
        }
        finally
        {
            await repeatedTask.StopAsync();
        }

        logicResult.Stopwatch.Stop();
        logicResult.RecordsRequested = executeMultipleRequestBuilder.Build().Requests.Count;
        logicResult.RecordsProcessed = progress;
        logicResult.Results = responsesList;
        logicResult.Cancelled = cancellationToken.IsCancellationRequested;
        executeMultipleRequestSettings.ReportProgress(progress, executeMultipleRequestBuilder.Count);

        return logicResult;
    }

    public async Task<ExecuteMultipleLogicResult> ExecuteAsync(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSimpleSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(executeMultipleRequestSettings);

        Guard.IsNotNull(executeMultipleRequestBuilder);

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
                ? _connectionsBag.Count
                : executeMultipleRequestSettings.MaxDegreeOfParallelism;

        ExecuteMultipleLogicResult logicResult = new()
        {
            Stopwatch = Stopwatch.StartNew(),
            ThreadsUsed = threadsCount
        };

        int progress = 0;

        OrganizationRequest[][] allRequestChunks = RequestsToChunks(executeMultipleRequestBuilder,
            executeMultipleRequestSettings);

        ConcurrentBag<ExecuteMultipleResponseItem> responsesList = new();

        await Parallel.ForEachAsync(allRequestChunks,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = threadsCount,
                CancellationToken = cancellationToken
            },
            async (packOfRequests, _) =>
            {
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

                ExecuteMultipleResponse responseWithResults =
                    await _connectionsBag.ExecuteAsync<ExecuteMultipleResponse>(requestWithResults);

                foreach (ExecuteMultipleResponseItem responseItem in responseWithResults.Responses)
                {
                    responsesList.Add(responseItem);

                    if (responseItem.Fault == null)
                    {
                        continue;
                    }

                    throw new InvalidProgramException(
                        $"Request on index {responseItem.RequestIndex} failed with error, {responseItem.Fault}");
                }

                Interlocked.Add(ref progress, packOfRequests.Length);
            });

        logicResult.Stopwatch.Stop();
        logicResult.RecordsRequested = executeMultipleRequestBuilder.Build().Requests.Count;
        logicResult.RecordsProcessed = progress;
        logicResult.Results = responsesList;
        logicResult.Cancelled = cancellationToken.IsCancellationRequested;

        return logicResult;
    }

    private OrganizationRequest[][] RequestsToChunks(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSimpleSettings executeMultipleRequestSettings)
    {
        return executeMultipleRequestBuilder
           .Build()
           .Requests
           .Select((s, i) => new
            {
                Value = s,
                Index = i
            })
           .GroupBy(p => p.Index / executeMultipleRequestSettings.RequestSize)
           .Select(p => p.Select(x => x.Value).ToArray())
           .ToArray();
    }
}
