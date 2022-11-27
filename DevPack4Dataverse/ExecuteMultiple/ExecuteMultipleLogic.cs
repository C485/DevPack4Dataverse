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
using DevPack4Dataverse.Models;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System.Diagnostics;

namespace DevPack4Dataverse.ExecuteMultiple;

public sealed class ExecuteMultipleLogic
{
    private readonly ILogger _logger;
    private readonly SdkProxy _sdkProxy;

    public ExecuteMultipleLogic(SdkProxy sdkProxy, ILogger logger)
    {
        using EntryExitLogger logGuard = new(logger);
        _sdkProxy = Guard.Against.Null(sdkProxy);
        _logger = Guard.Against.Null(logger);
    }

    public ExecuteMultipleRequestBuilder CreateRequestBuilder(bool continueOnError = true)
    {
        return new ExecuteMultipleRequestBuilder(_logger, continueOnError);
    }

    public async Task<AdvancedExecuteMultipleRequestsStatistics> Execute(
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSettings executeMultipleRequestSettings,
        CancellationToken cancellationToken = default)
    {
        using EntryExitLogger logGuard = new(_logger);

        Guard
           .Against
           .Null(executeMultipleRequestSettings);

        Guard
           .Against
           .Null(executeMultipleRequestBuilder);

        if (executeMultipleRequestBuilder.Count == 0)
        {
            return new AdvancedExecuteMultipleRequestsStatistics
            {
                RecordsProcessed = 0,
                Stopwatch = new Stopwatch(),
                ThreadsUsed = 0
            };
        }

        int threadsCount = executeMultipleRequestSettings.MaxDegreeOfParallelism <= 0
            ? _sdkProxy.ConnectionCount
            : executeMultipleRequestSettings.MaxDegreeOfParallelism;

        AdvancedExecuteMultipleRequestsStatistics chunksStatistics = new()
        {
            Stopwatch = Stopwatch.StartNew(),
            ThreadsUsed = threadsCount
        };

        int progress = 0;

        RepeatedTask repeatedTask = new(executeMultipleRequestSettings.ReportProgressInterval, () =>
        {
            executeMultipleRequestSettings.ReportProgress(Thread.VolatileRead(ref progress),
                executeMultipleRequestBuilder.Count);
        }, _logger);
        repeatedTask.Start();

        try
        {
            OrganizationRequest[][] allRequestChunks = RequestsToChunks(executeMultipleRequestBuilder, executeMultipleRequestSettings);

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
                        ExecuteMultipleRequest requestWithResults = new()
                        {
                            Settings = new ExecuteMultipleSettings
                            {
                                ContinueOnError = true,
                                ReturnResponses = true
                            },
                            Requests = new OrganizationRequestCollection()
                        };

                        requestWithResults
                           .Requests
                           .AddRange(packOfRequests);

                        ExecuteMultipleResponse responseWithResults =
                            await _sdkProxy
                               .ExecuteAsync<ExecuteMultipleResponse>(requestWithResults);

                        foreach (ExecuteMultipleResponseItem responseItem in responseWithResults.Responses)
                        {
                            if (responseItem.Fault == null)
                            {
                                continue;
                            }

                            OrganizationRequest requestOrigin = requestWithResults
                               .Requests[responseItem.RequestIndex];

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
            await repeatedTask
                .StopAsync();
        }

        chunksStatistics
           .Stopwatch
           .Stop();

        chunksStatistics.RecordsRequested = executeMultipleRequestBuilder
           .RequestWithResults
           .Requests
           .Count;

        chunksStatistics.RecordsProcessed = progress;

        chunksStatistics.Cancelled = cancellationToken
           .IsCancellationRequested;

        executeMultipleRequestSettings.ReportProgress(progress, executeMultipleRequestBuilder.Count);

        return chunksStatistics;
    }

    private OrganizationRequest[][] RequestsToChunks(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSettings executeMultipleRequestSettings)
    {
        using EntryExitLogger logGuard = new(_logger);
        return executeMultipleRequestBuilder
           .RequestWithResults
           .Requests
           .Select((s, i) => new
           {
               Value = s,
               Index = i
           })
           .GroupBy(p => p.Index / executeMultipleRequestSettings.RequestSize)
           .Select(p => p
               .Select(x => x.Value)
               .ToArray())
           .ToArray();
    }
}