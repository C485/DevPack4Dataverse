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
using DevPack4Dataverse.Entities;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse.EnvironmentComparer;

public sealed class EnvironmentComparer
{
    private readonly DataverseDevPack _sourceDataverseDevPack;
    private readonly DataverseDevPack _targetDataverseDevPack;
    private RetrieveAllEntitiesResponse _targetMetadata;
    private RetrieveAllEntitiesResponse _sourceMetadata;

    private EnvironmentComparer(DataverseDevPack sourceDataverseDevPack, DataverseDevPack targetDataverseDevPack)
    {
        _sourceDataverseDevPack = Guard.Against.Null(sourceDataverseDevPack);
        _targetDataverseDevPack = Guard.Against.Null(targetDataverseDevPack);
    }

    public static EnvironmentComparer CreateComparer(
        DataverseDevPack sourceDataverseDevPack,
        DataverseDevPack targetDataverseDevPack
    )
    {
        return new EnvironmentComparer(sourceDataverseDevPack, targetDataverseDevPack);
    }

    public async Task Compare()
    {
        //add OrgDBOrgSettings
        await DownloadMetadata();
    }

    public async Task DownloadMetadata()
    {
        Task<RetrieveAllEntitiesResponse> sourceMetadataTask =
            _sourceDataverseDevPack.SdkProxy.ExecuteAsync<RetrieveAllEntitiesResponse>(
                new RetrieveAllEntitiesRequest
                {
                    EntityFilters = EntityFilters.Entity | EntityFilters.Attributes,
                    RetrieveAsIfPublished = true
                }
            );
        Task<RetrieveAllEntitiesResponse> targetMetadataTask =
            _targetDataverseDevPack.SdkProxy.ExecuteAsync<RetrieveAllEntitiesResponse>(
                new RetrieveAllEntitiesRequest
                {
                    EntityFilters = EntityFilters.Entity | EntityFilters.Attributes,
                    RetrieveAsIfPublished = true
                }
            );
        await Task.WhenAll(targetMetadataTask, sourceMetadataTask);
        _sourceMetadata = sourceMetadataTask.Result;
        _targetMetadata = targetMetadataTask.Result;
    }

    private static void CompareFields(
        Dictionary<string, Dictionary<string, AttributeMetadata>> devMetadataDict,
        Dictionary<string, Dictionary<string, AttributeMetadata>> uatMetadataDict,
        string compareToEnvName
    )
    {
        foreach (string key in devMetadataDict.Keys)
        {
            if (uatMetadataDict.ContainsKey(key))
            {
                Dictionary<string, AttributeMetadata> metadataDev = devMetadataDict[key];
                Dictionary<string, AttributeMetadata> metadataUat = uatMetadataDict[key];
                foreach (KeyValuePair<string, AttributeMetadata> metadataDevVal in metadataDev)
                {
                    if (metadataUat.ContainsKey(metadataDevVal.Key))
                    {
                        AttributeMetadata metadataUatVal = metadataUat[metadataDevVal.Key];
                        if (metadataUatVal.AttributeType != metadataDevVal.Value.AttributeType)
                        {
                            Console.WriteLine(
                                $"Found field {metadataDevVal.Key} in table {key}, but type is different."
                            );
                        }
                        else
                        {
                            switch (metadataUatVal)
                            {
                                case IntegerAttributeMetadata metadataUatValInteger
                                    when metadataDevVal.Value is IntegerAttributeMetadata metadataDevValInteger:
                                {
                                    if (metadataDevValInteger.MaxValue != metadataUatValInteger.MaxValue)
                                    {
                                        Console.WriteLine(
                                            $"Field {metadataDevVal.Key} in table {key} has different max value."
                                        );
                                    }

                                    break;
                                }
                                case LookupAttributeMetadata metadataUatValLookup
                                    when metadataDevVal.Value is LookupAttributeMetadata metadataDevValLookup:
                                {
                                    if (!metadataDevValLookup.Targets.SequenceEqual(metadataUatValLookup.Targets))
                                    {
                                        Console.WriteLine(
                                            $"Field {metadataDevVal.Key} in table {key} has different targets."
                                        );
                                    }

                                    break;
                                }
                                case StringAttributeMetadata metadataUatValString
                                    when metadataDevVal.Value is StringAttributeMetadata metadataDevValString:
                                {
                                    if (metadataDevValString.MaxLength != metadataUatValString.MaxLength)
                                    {
                                        Console.WriteLine(
                                            $"Field {metadataDevVal.Key} in table {key} has different max length."
                                        );
                                    }

                                    if (metadataDevValString.Format != metadataUatValString.Format)
                                    {
                                        Console.WriteLine(
                                            $"Field {metadataDevVal.Key} in table {key} has different max length."
                                        );
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine(
                            $"Missing field in {compareToEnvName} - LogicalName[{metadataDevVal.Key}] in Table[{key}]"
                        );
                    }
                }
            }
            else
            {
                Console.WriteLine($"Missing table [{key}] in {compareToEnvName}.");
            }
        }
    }
}
