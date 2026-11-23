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

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using DevPack4Dataverse.Creators;
using DevPack4Dataverse.ExecuteMultiple;
using DevPack4Dataverse.Models;

namespace DevPack4Dataverse.Playground.Examples;

public class AdaptiveMultipleRequest(ILogger microsoftLogger) : IExecute
{
    private const string EntityName = "tablename";
    private const string EntityNameFieldName = "name_fieldname";
    private const int RecordsToCreate = 30000;

    // Set DEVPACK_DATAVERSE_CONNECTION to a ServiceClient connection string.
    // The connection string creator adds RequireNewInstance=True.
    public async Task Execute()
    {
        string connectionString = File.ReadAllText("path_to_connection_file");

        using DataverseDevPack connectionManager = new(
            microsoftLogger,
            true,
            new ConnectionStringConnectionCreator(connectionString, 5)
        );

        await AddNewRecords(connectionManager).ConfigureAwait(false);
        await DeleteRecords(connectionManager).ConfigureAwait(false);
    }

    private async Task AddNewRecords(DataverseDevPack connectionManager)
    {
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder = new(microsoftLogger, true);
        foreach (int item in Enumerable.Range(0, RecordsToCreate))
        {
            Entity et = new(EntityName);
            et[EntityNameFieldName] = $"Record [{item}]";
            executeMultipleRequestBuilder.AddCreate(et);
        }

        var rate = new RateTracker();
        var settings = new AdaptiveRequesterSettings
        {
            InitialBatchSize = 10,
            MinBatchSize = 1,
            MaxBatchSize = 800,
            TargetBatchDuration = TimeSpan.FromSeconds(8),
            EvaluationRecordCount = 50,
            ErrorReport = (obj, error) => Console.WriteLine($"Error: {error}")
        };
        settings.ReportProgress = (cur, max) =>
            Console.Title = "Adaptive progress "
                + rate.Format(cur, max)
                + $" DOP={settings.CurrentDegreeOfParallelism}"
                + $" batch={settings.CurrentBatchSize}";

        ExecuteMultipleLogicResult result = await connectionManager.ExecuteMultiple
            .ExecuteAdaptiveAsync(executeMultipleRequestBuilder, settings)
            .ConfigureAwait(false);

        Console.WriteLine(result.ToString());
    }

    private async Task DeleteRecords(DataverseDevPack dataverseDevPack)
    {
        Entity[] recordsInCrm = dataverseDevPack.SdkProxy.RetrieveMultiple(
            new QueryExpression { EntityName = EntityName, ColumnSet = new ColumnSet(false) }
        );
        ExecuteMultipleRequestBuilder executeMultipleRequestBuilder = dataverseDevPack.ExecuteMultiple
            .CreateRequestBuilder();
        foreach (Entity item in recordsInCrm)
        {
            executeMultipleRequestBuilder.AddDelete(item.ToEntityReference());
        }

        if (executeMultipleRequestBuilder.Count == 0)
        {
            return;
        }

        var rate = new RateTracker();
        var settings = new AdaptiveRequesterSettings
        {
            InitialBatchSize = 10,
            MinBatchSize = 1,
            MaxBatchSize = 800,
            TargetBatchDuration = TimeSpan.FromSeconds(8),
            EvaluationRecordCount = 50,
            ErrorReport = (obj, error) => Console.WriteLine($"Error: {error}")
        };
        settings.ReportProgress = (cur, max) =>
        {
            string v = $"Adaptive delete {rate.Format(cur, max)} DOP={settings.CurrentDegreeOfParallelism} batch={settings.CurrentBatchSize}";
            Console.Title = v;
            Console.WriteLine(v);
        };

        ExecuteMultipleLogicResult result = await dataverseDevPack.ExecuteMultiple
            .ExecuteAdaptiveAsync(executeMultipleRequestBuilder, settings)
            .ConfigureAwait(false);

        Console.WriteLine(result.ToString());
    }

    private sealed class RateTracker
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private int _lastCurrent;
        private TimeSpan _lastElapsed;

        public string Format(int current, int total)
        {
            TimeSpan elapsed = _stopwatch.Elapsed;
            TimeSpan interval = elapsed - _lastElapsed;
            int processed = current - _lastCurrent;
            double currentRate = interval.TotalSeconds > 0
                ? processed / interval.TotalSeconds
                : 0;
            double averageRate = elapsed.TotalSeconds > 0
                ? current / elapsed.TotalSeconds
                : 0;

            _lastCurrent = current;
            _lastElapsed = elapsed;
            return $"[{current}/{total}] {currentRate:F0}/s (avg {averageRate:F0}/s)";
        }
    }
}
