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

using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using DevPack4Dataverse.Creators;
using DevPack4Dataverse.ExecuteMultiple;
using DevPack4Dataverse.Models;

namespace DevPack4Dataverse.Playground.Examples;

public class AdvancedMultipleRequest(ILogger microsoftLogger) : IExecute
{
    private const string EntityName = "cr4cc_autocountervc";
    private const string EntityNameFieldName = "cr4cc_name";
    private const int RecordsToCreate = 2000;

    //You'll need at least 4 accounts/application accounts
    //Make sure they've ExecuteMultiple
    public async Task Execute()
    {
        DataverseDevPack connectionManager = new(
            microsoftLogger,
            true,
            new ClientSecretConnectionCreator("", "", SdkProxy.StringToSecureString(""), 6),
            new ClientSecretConnectionCreator("", "", SdkProxy.StringToSecureString(""), 6),
            new ClientSecretConnectionCreator("", "", SdkProxy.StringToSecureString(""), 6),
            new ClientSecretConnectionCreator("", "", SdkProxy.StringToSecureString(""), 6)
        );
        await DeleteRecords(connectionManager).ConfigureAwait(false);

        await AddNewRecords(connectionManager).ConfigureAwait(false);
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

        await connectionManager.ExecuteMultiple
            .ExecuteAsync(
                executeMultipleRequestBuilder,
                new ExecuteMultipleRequestSettings
                {
                    ErrorReport = (obj, error) => Console.WriteLine($"Error: {error}"),
                    ReportProgress = (cur, max) => Console.Title = $"Progress[{cur}/{max}]",
                    ReportProgressInterval = TimeSpan.FromMilliseconds(100),
                    MaxDegreeOfParallelism = 4 * 6,
                    RequestSize = 30
                })
            .ConfigureAwait(false);
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

        await dataverseDevPack.ExecuteMultiple
            .ExecuteAsync(
                executeMultipleRequestBuilder,
                new ExecuteMultipleRequestSettings
                {
                    ErrorReport = (obj, error) => Console.WriteLine($"Error: {error}"),
                    ReportProgress = (cur, max) => Console.Title = $"Progress delete[{cur}/{max}]",
                    ReportProgressInterval = TimeSpan.FromMilliseconds(100),
                    MaxDegreeOfParallelism = 4 * 2
                })
            .ConfigureAwait(false);
    }
}
