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
using DevPack4Dataverse.Creators;
using DevPack4Dataverse.ExecuteMultiple;
using DevPack4Dataverse.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.Playground.Examples;

/// <summary>
/// Builds a small graph of standard Dataverse records and drills through it with
/// <see cref="FieldDrill"/>:
/// task -> regarding contact -> parent account -> parent account -> owner/currency/creator.
///
/// Set DATAVERSE_CONNECTION_STRING, or DATAVERSE_URL + DATAVERSE_CLIENT_ID +
/// DATAVERSE_CLIENT_SECRET. The example creates and deletes its own records.
/// Run it with: dotnet run -- drill
/// </summary>
public sealed class FieldDrillExample(ILogger logger) : IExecute
{
    private const string ConnectionStringVariable = "DATAVERSE_CONNECTION_STRING";
    private const string UrlVariable = "DATAVERSE_URL";
    private const string ClientIdVariable = "DATAVERSE_CLIENT_ID";
    private const string ClientSecretVariable = "DATAVERSE_CLIENT_SECRET";

    public async Task Execute()
    {
        using DataverseDevPack devPack = CreateDevPack(logger);
        List<EntityReference> created = new();

        try
        {
            Section("Connection");
            devPack.WaitForConnections(2);
            Console.WriteLine($"Connections ready: {devPack.SdkProxy.ConnectionCount}");

            DrillGraph graph = await CreateGraphAsync(devPack, created).ConfigureAwait(false);
            Console.WriteLine(
                $"Created graph: root={graph.RootAccount.Id}, child={graph.ChildAccount.Id}, "
                    + $"contact={graph.Contact.Id}, task={graph.Activity.Id}"
            );

            await RunScalarDrillsAsync(devPack, graph).ConfigureAwait(false);
            await RunSyncVsAsyncDrillsAsync(devPack, graph).ConfigureAwait(false);
            await RunArrayAndDelimiterDrillsAsync(devPack, graph).ConfigureAwait(false);
            await RunValueTypeDrillsAsync(devPack, graph).ConfigureAwait(false);
            await RunParallelDrillsAsync(devPack, graph).ConfigureAwait(false);
            await RunTimedDrillsAsync(devPack, graph).ConfigureAwait(false);
            await RunErrorDrillsAsync(devPack, graph).ConfigureAwait(false);
        }
        finally
        {
            await DeleteCreatedAsync(devPack, created).ConfigureAwait(false);
        }
    }

    private static DataverseDevPack CreateDevPack(ILogger logger)
    {
        string? connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new DataverseDevPack(
                logger,
                false,
                new ConnectionStringConnectionCreator(connectionString, connectionCount: 2)
            );
        }

        string? url = Environment.GetEnvironmentVariable(UrlVariable);
        string? clientId = Environment.GetEnvironmentVariable(ClientIdVariable);
        string? secret = Environment.GetEnvironmentVariable(ClientSecretVariable);
        if (
            string.IsNullOrWhiteSpace(url)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(secret)
        )
        {
            throw new InvalidOperationException(
                $"Set {ConnectionStringVariable}, or {UrlVariable} + {ClientIdVariable} + {ClientSecretVariable}."
            );
        }

        return new DataverseDevPack(
            logger,
            false,
            new ClientSecretConnectionCreator(
                url,
                clientId,
                SdkProxy.StringToSecureString(secret)!,
                connectionCount: 2
            )
        );
    }

    private static async Task<DrillGraph> CreateGraphAsync(
        DataverseDevPack devPack,
        List<EntityReference> created
    )
    {
        Section("Create graph");
        string suffix = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        Entity rootAccount = new("account") { ["name"] = $"DevPack Drill Root {suffix}" };
        EntityReference rootAccountReference = new(
            "account",
            await devPack.SdkProxy.CreateRecordAsync(rootAccount).ConfigureAwait(false)
        );
        created.Add(rootAccountReference);

        Entity childAccount = new("account")
        {
            ["name"] = $"DevPack Drill Child {suffix}",
            ["parentaccountid"] = rootAccountReference
        };
        EntityReference childAccountReference = new(
            "account",
            await devPack.SdkProxy.CreateRecordAsync(childAccount).ConfigureAwait(false)
        );
        created.Add(childAccountReference);

        Entity contact = new("contact")
        {
            ["firstname"] = "Ada",
            ["lastname"] = $"Drill {suffix}",
            ["parentcustomerid"] = childAccountReference,
            ["birthdate"] = new DateTime(1990, 1, 2),
            ["donotemail"] = true
        };
        EntityReference contactReference = new(
            "contact",
            await devPack.SdkProxy.CreateRecordAsync(contact).ConfigureAwait(false)
        );
        created.Add(contactReference);

        // Makes the graph cyclic on purpose: task -> contact -> child -> primary contact -> contact.
        await devPack.SdkProxy
            .UpdateRecordAsync(
                new Entity("account", childAccountReference.Id)
                {
                    ["primarycontactid"] = contactReference
                }
            )
            .ConfigureAwait(false);

        Entity looseContact = new("contact")
        {
            ["firstname"] = "Loose",
            ["lastname"] = $"Drill {suffix}"
        };
        EntityReference looseContactReference = new(
            "contact",
            await devPack.SdkProxy.CreateRecordAsync(looseContact).ConfigureAwait(false)
        );
        created.Add(looseContactReference);

        Entity activity = new("task")
        {
            ["subject"] = $"DevPack Drill Task {suffix}",
            ["regardingobjectid"] = contactReference,
            ["scheduledend"] = DateTime.UtcNow.AddDays(1)
        };
        EntityReference activityReference = new(
            "task",
            await devPack.SdkProxy.CreateRecordAsync(activity).ConfigureAwait(false)
        );
        created.Add(activityReference);

        EntityReference currency = await devPack.FieldDrill
            .RetreiveAsync<EntityReference>(rootAccountReference, "transactioncurrencyid")
            .ConfigureAwait(false);
        EntityReference owner = await devPack.FieldDrill
            .RetreiveAsync<EntityReference>(rootAccountReference, "ownerid")
            .ConfigureAwait(false);

        Console.WriteLine($"Root currency: {currency.Id}, owner: {owner.Id}");
        return new DrillGraph(
            rootAccountReference,
            childAccountReference,
            contactReference,
            looseContactReference,
            activityReference,
            currency,
            owner
        );
    }

    private static async Task RunScalarDrillsAsync(DataverseDevPack devPack, DrillGraph graph)
    {
        Section("Scalar drills (task -> contact -> account -> account -> user/currency)");
        FieldDrill drill = devPack.FieldDrill;

        string ownerName = await drill
            .RetreiveAsync<string>(
                graph.Activity,
                "regardingobjectid.parentcustomerid.parentaccountid.ownerid.fullname"
            )
            .ConfigureAwait(false);
        Console.WriteLine($"Owner full name: {ownerName}");

        string currencyName = await drill
            .RetreiveAsync<string>(
                graph.Activity,
                "regardingobjectid.parentcustomerid.parentaccountid.transactioncurrencyid.currencyname"
            )
            .ConfigureAwait(false);
        Console.WriteLine($"Currency name: {currencyName}");

        string rootName = await drill
            .RetreiveAsync<string>(
                graph.Activity,
                "regardingobjectid.parentcustomerid.parentaccountid.name"
            )
            .ConfigureAwait(false);
        Console.WriteLine($"Root account name: {rootName}");

        string childName = await drill
            .RetreiveAsync<string>(graph.Activity, "regardingobjectid.parentcustomerid.name")
            .ConfigureAwait(false);
        Console.WriteLine($"Child account name: {childName}");

        string contactName = await drill
            .RetreiveAsync<string>(graph.Activity, "regardingobjectid.fullname")
            .ConfigureAwait(false);
        Console.WriteLine($"Contact full name: {contactName}");

        string createdByDomain = await drill
            .RetreiveAsync<string>(
                graph.Activity,
                "regardingobjectid.parentcustomerid.parentaccountid.createdby.domainname"
            )
            .ConfigureAwait(false);
        Console.WriteLine($"Root created by (domain): {createdByDomain}");

        string primaryContactName = await drill
            .RetreiveAsync<string>(
                graph.Activity,
                "regardingobjectid.parentcustomerid.primarycontactid.fullname"
            )
            .ConfigureAwait(false);
        Console.WriteLine($"Child primary contact (cycle back): {primaryContactName}");
    }

    private static async Task RunSyncVsAsyncDrillsAsync(
        DataverseDevPack devPack,
        DrillGraph graph
    )
    {
        Section("Sync vs async");
        const string path =
            "regardingobjectid.parentcustomerid.parentaccountid.ownerid.fullname";

        string syncValue = devPack.FieldDrill.Retreive<string>(graph.Activity, path);
        string asyncValue = await devPack.FieldDrill
            .RetreiveAsync<string>(graph.Activity, path)
            .ConfigureAwait(false);

        Console.WriteLine($"Sync : {syncValue}");
        Console.WriteLine($"Async: {asyncValue}");
        Console.WriteLine($"Equal: {syncValue == asyncValue}");
    }

    private static async Task RunArrayAndDelimiterDrillsAsync(
        DataverseDevPack devPack,
        DrillGraph graph
    )
    {
        Section("Array path, custom delimiter and whitespace trimming");
        FieldDrill drill = devPack.FieldDrill;

        string[] pathParts =
        [
            "regardingobjectid",
            "parentcustomerid",
            "parentaccountid",
            "ownerid",
            "domainname"
        ];
        string domainFromArray = await drill
            .RetreiveAsync<string>(graph.Activity, pathParts)
            .ConfigureAwait(false);
        Console.WriteLine($"Array path: {domainFromArray}");

        string nameFromCustomDelimiter = await drill
            .RetreiveAsync<string>(
                graph.Activity,
                "regardingobjectid=>parentcustomerid=>parentaccountid=>name",
                "=>"
            )
            .ConfigureAwait(false);
        Console.WriteLine($"Custom delimiter '=>': {nameFromCustomDelimiter}");

        string nameWithSpaces = await drill
            .RetreiveAsync<string>(
                graph.Activity,
                " regardingobjectid . parentcustomerid . parentaccountid . name ",
                "."
            )
            .ConfigureAwait(false);
        Console.WriteLine($"Trimmed path: {nameWithSpaces}");
    }

    private static async Task RunValueTypeDrillsAsync(DataverseDevPack devPack, DrillGraph graph)
    {
        Section("Final value types");
        FieldDrill drill = devPack.FieldDrill;

        OptionSetValue? category = await drill
            .RetreiveAsync<OptionSetValue?>(
                graph.Activity,
                "regardingobjectid.parentcustomerid.parentaccountid.accountcategorycode"
            )
            .ConfigureAwait(false);
        Console.WriteLine($"Account category: {category?.Value}");

        Money? creditLimit = await drill
            .RetreiveAsync<Money?>(
                graph.Activity,
                "regardingobjectid.parentcustomerid.parentaccountid.creditlimit"
            )
            .ConfigureAwait(false);
        Console.WriteLine($"Credit limit: {creditLimit?.Value}");

        DateTime birthdate = await drill
            .RetreiveAsync<DateTime>(graph.Activity, "regardingobjectid.birthdate")
            .ConfigureAwait(false);
        Console.WriteLine($"Contact birthdate: {birthdate:yyyy-MM-dd}");

        bool doNotEmail = await drill
            .RetreiveAsync<bool>(graph.Activity, "regardingobjectid.donotemail")
            .ConfigureAwait(false);
        Console.WriteLine($"Contact do not email: {doNotEmail}");

        int? employees = await drill
            .RetreiveAsync<int?>(
                graph.Activity,
                "regardingobjectid.parentcustomerid.parentaccountid.numberofemployees"
            )
            .ConfigureAwait(false);
        Console.WriteLine($"Root employees: {employees?.ToString() ?? "<null>"}");

        decimal exchangeRate = await drill
            .RetreiveAsync<decimal>(graph.RootAccount, "transactioncurrencyid.exchangerate")
            .ConfigureAwait(false);
        Console.WriteLine($"Exchange rate: {exchangeRate}");

        EntityReference regarding = await drill
            .RetreiveAsync<EntityReference>(graph.Activity, "regardingobjectid")
            .ConfigureAwait(false);
        Console.WriteLine($"Regarding: {regarding.LogicalName}:{regarding.Id}");
    }

    private static async Task RunParallelDrillsAsync(DataverseDevPack devPack, DrillGraph graph)
    {
        Section("Parallel drills over all available connections");
        string[] paths =
        [
            "regardingobjectid.parentcustomerid.parentaccountid.ownerid.fullname",
            "regardingobjectid.parentcustomerid.parentaccountid.transactioncurrencyid.currencyname",
            "regardingobjectid.parentcustomerid.parentaccountid.createdby.domainname",
            "regardingobjectid.parentcustomerid.primarycontactid.fullname",
            "regardingobjectid.fullname",
            "regardingobjectid.parentcustomerid.name",
            "regardingobjectid.parentcustomerid.parentaccountid.name",
            "regardingobjectid.parentcustomerid.parentaccountid.ownerid.domainname"
        ];

        Task<string>[] drills = paths
            .Select(path => devPack.FieldDrill.RetreiveAsync<string>(graph.Activity, path))
            .ToArray();
        string[] results = await Task.WhenAll(drills).ConfigureAwait(false);

        for (int index = 0; index < paths.Length; index++)
        {
            Console.WriteLine($"{paths[index]} => {results[index]}");
        }
    }

    private static async Task RunTimedDrillsAsync(DataverseDevPack devPack, DrillGraph graph)
    {
        Section("Timed deep drill");
        const int iterations = 10;
        const string path =
            "regardingobjectid.parentcustomerid.parentaccountid.transactioncurrencyid.currencyname";

        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int index = 0; index < iterations; index++)
        {
            _ = await devPack.FieldDrill
                .RetreiveAsync<string>(graph.Activity, path)
                .ConfigureAwait(false);
        }

        stopwatch.Stop();
        Console.WriteLine(
            $"{iterations} deep drills: {stopwatch.Elapsed}; "
                + $"average {stopwatch.Elapsed.TotalMilliseconds / iterations:F1} ms"
        );
    }

    private static async Task RunErrorDrillsAsync(DataverseDevPack devPack, DrillGraph graph)
    {
        Section("Expected failures");
        FieldDrill drill = devPack.FieldDrill;

        await ExpectFailureAsync(
                "missing field",
                async () =>
                    _ = await drill
                        .RetreiveAsync<string>(graph.Activity, "regardingobjectid.notAField")
                        .ConfigureAwait(false)
            )
            .ConfigureAwait(false);

        await ExpectFailureAsync(
                "non-lookup intermediate",
                async () =>
                    _ = await drill
                        .RetreiveAsync<string>(
                            graph.Activity,
                            "regardingobjectid.fullname.parentcustomerid"
                        )
                        .ConfigureAwait(false)
            )
            .ConfigureAwait(false);

        await ExpectFailureAsync(
                "empty segment",
                async () =>
                    _ = await drill
                        .RetreiveAsync<string>(
                            graph.Activity,
                            "regardingobjectid..parentcustomerid"
                        )
                        .ConfigureAwait(false)
            )
            .ConfigureAwait(false);

        await ExpectFailureAsync(
                "wrong final type",
                async () =>
                    _ = await drill
                        .RetreiveAsync<string>(graph.Activity, "regardingobjectid")
                        .ConfigureAwait(false)
            )
            .ConfigureAwait(false);

        await ExpectFailureAsync(
                "null intermediate lookup",
                async () =>
                    _ = await drill
                        .RetreiveAsync<string>(graph.LooseContact, "parentcustomerid.name")
                        .ConfigureAwait(false)
            )
            .ConfigureAwait(false);

        await ExpectFailureAsync(
                "null start reference",
                async () => _ = await drill.RetreiveAsync<string>(null!, "name").ConfigureAwait(false)
            )
            .ConfigureAwait(false);
    }

    private static async Task ExpectFailureAsync(string label, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            Console.WriteLine($"UNEXPECTED SUCCESS: {label}");
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"Expected failure [{label}]: {exception.GetType().Name}: {exception.Message}"
            );
        }
    }

    private static Task DeleteCreatedAsync(
        DataverseDevPack devPack,
        IReadOnlyList<EntityReference> created
    )
    {
        Section("Cleanup");
        if (created.Count == 0)
        {
            return Task.CompletedTask;
        }

        return DeleteCreatedCoreAsync(devPack, created);
    }

    private static async Task DeleteCreatedCoreAsync(
        DataverseDevPack devPack,
        IReadOnlyList<EntityReference> created
    )
    {
        // Clear the account -> contact reference before deleting the contact.
        foreach (
            EntityReference account in created.Where(reference =>
                string.Equals(reference.LogicalName, "account", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            try
            {
                await devPack.SdkProxy
                    .UpdateRecordAsync(
                        new Entity("account", account.Id) { ["primarycontactid"] = null }
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Could not clear primarycontactid on {account.Id}: {exception.Message}");
            }
        }

        ExecuteMultipleRequestBuilder builder = devPack.ExecuteMultiple.CreateRequestBuilder();
        for (int index = created.Count - 1; index >= 0; index--)
        {
            builder.AddDelete(created[index]);
        }

        try
        {
            await devPack.ExecuteMultiple
                .ExecuteAsync(
                    builder,
                    new ExecuteMultipleRequestSettings
                    {
                        ErrorReport = (_, error) => Console.WriteLine($"Cleanup error: {error}"),
                        ReportProgress = (current, maximum) =>
                            Console.WriteLine($"Cleanup progress: {current}/{maximum}")
                    }
                )
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Cleanup failed: {exception.Message}");
            Console.WriteLine("Created records were:");
            foreach (EntityReference reference in created)
            {
                Console.WriteLine($"  {reference.LogicalName}:{reference.Id}");
            }
        }
    }

    private static void Section(string title) => Console.WriteLine($"\n=== {title} ===");

    private sealed record DrillGraph(
        EntityReference RootAccount,
        EntityReference ChildAccount,
        EntityReference Contact,
        EntityReference LooseContact,
        EntityReference Activity,
        EntityReference Currency,
        EntityReference Owner
    );
}
