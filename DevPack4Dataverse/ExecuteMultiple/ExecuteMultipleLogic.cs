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
    private readonly SdkProxy _connectionManager;
    private readonly ILogger _logger;

    public ExecuteMultipleLogic(SdkProxy connectionManager, ILogger logger)
    {
        _connectionManager = Guard.Against.Null(connectionManager);
        _logger = Guard.Against.Null(logger);
    }

    public AdvancedExecuteMultipleRequestsStatistics AdvancedExecuteMultipleRequests(
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
            ? _connectionManager.ConnectionCount
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

        try
        {
            OrganizationRequest[][] allRequestChunks = RequestsToChunks(executeMultipleRequestBuilder, executeMultipleRequestSettings);

            _ = Parallel.ForEach(allRequestChunks,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = threadsCount,
                    CancellationToken = cancellationToken
                },
                packOfRequests =>
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
                            _connectionManager
                               .Execute<ExecuteMultipleResponse>(requestWithResults);

                        foreach (ExecuteMultipleResponseItem responseItem in responseWithResults.Responses)
                        {
                            if (responseItem.Fault == null)
                            {
                                continue;
                            }

                            //TODO fix, parse index from error message

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
            repeatedTask
                .StopAsync()
                .RunSynchronously();
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

        return chunksStatistics;
    }

    private static OrganizationRequest[][] RequestsToChunks(ExecuteMultipleRequestBuilder executeMultipleRequestBuilder,
        ExecuteMultipleRequestSettings executeMultipleRequestSettings)
    {
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