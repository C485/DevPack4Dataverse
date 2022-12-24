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
using DevPack4Dataverse.ExecuteMultiple;
using DevPack4Dataverse.ExpressionBuilder;
using DevPack4Dataverse.Interfaces;
using DevPack4Dataverse.Utils;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System.Collections.Concurrent;

namespace DevPack4Dataverse;

public sealed class ChoiceFieldTypeMethods
{
    private const int NoLanguageFilter = -1;
    private readonly ConcurrentDictionary<string, OptionMetadataCollection> _cachedMetadata; //TODO use cache
    private readonly SdkProxy _sdkProxy;
    //TODO add string to optionset logic

    public ChoiceFieldTypeMethods(SdkProxy sdkProxy)
    {
        _sdkProxy = sdkProxy;
        _cachedMetadata = new ConcurrentDictionary<string, OptionMetadataCollection>();
    }

    /// <summary>
    /// Utilizes SDK functionality called FormattedValues that contains label for optionset in users language. <para />
    /// Type of field is not checked, cache is not used.
    /// </summary>
    /// <param name="sourceRecord">Required.</param>
    /// <param name="fieldName">Required.</param>
    /// <returns>Label for field.</returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public static string MapOptionSetToStringUsingFormatedValues(Entity sourceRecord, string fieldName)
    {
        Guard
            .Against
            .Null(sourceRecord);
        Guard
            .Against
            .NullOrEmpty(fieldName);
        if (sourceRecord.FormattedValues.Contains(fieldName))
        {
            return sourceRecord.FormattedValues[fieldName];
        }
        throw new KeyNotFoundException($"Formated value for field[{fieldName}] was not found, check image/query settings.");
    }

    public async Task<string> MapOptionSetToString(string tableName, string fieldName, OptionSetValue valueToMap, int languageCode = NoLanguageFilter)
    {
        Guard
            .Against
            .Null(valueToMap);
        return await MapOptionSetToString(tableName, fieldName, valueToMap.Value, languageCode);
    }

    public async Task<string> MapOptionSetToString(string tableName, string fieldName, bool valueToMap, int languageCode = NoLanguageFilter)
    {
        Guard
            .Against
            .Null(valueToMap);
        return await MapOptionSetToString(tableName, fieldName, Convert.ToInt32(valueToMap), languageCode);
    }

    public async Task<string> MapOptionSetToString<T>(string tableName, string fieldName, T valueToMap, int languageCode = NoLanguageFilter) where T : struct, Enum
    {
        Guard
            .Against
            .Null(valueToMap);
        return await MapOptionSetToString(tableName, fieldName, Convert.ToInt32(valueToMap), languageCode);
    }

    /// <summary>
    /// It'll map int value to optionset label. Language code can be provided to get exact label for language.
    /// </summary>
    /// <param name="tableName">Required.</param>
    /// <param name="fieldName">Required.</param>
    /// <param name="valueToMap">Value of optionset as <see cref="int"/>. Required.</param>
    /// <param name="languageCode">Optional, by default UserLocalizedLabel will be used, otherwise LocalizedLabels will be searched for label.</param>
    /// <returns>Returns label of value.</returns>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentNullException"></exception>
    public async Task<string> MapOptionSetToString(string tableName, string fieldName, int valueToMap, int languageCode = NoLanguageFilter)
    {
        Guard
            .Against
            .NullOrEmpty(tableName);
        Guard
            .Against
            .NullOrEmpty(fieldName);
        string key = $"{tableName.ToLower()}_{fieldName.ToLower()}";

        await DownloadMetadataForField(tableName, fieldName, key);
        if (!_cachedMetadata.ContainsKey(key))
        {
            throw new KeyNotFoundException($"Key[{key}] was not found in dataverse metadata.");
        }
        OptionMetadataCollection metadataResponse = _cachedMetadata[key];
        OptionMetadata mappedFieldValueMetadata = metadataResponse.FirstOrDefault(p => p.Value == valueToMap);
        Guard
            .Against
            .Null(mappedFieldValueMetadata, message: $"Metadata for field was valid but optionset value of {valueToMap} was not found.");
        if (languageCode == NoLanguageFilter)
        {
            return mappedFieldValueMetadata.Label.UserLocalizedLabel.Label;
        }
        LocalizedLabel languageDependentLabel = mappedFieldValueMetadata.Label.LocalizedLabels.SingleOrDefault(p => p.LanguageCode == languageCode);
        return Guard
            .Against
            .Null(languageDependentLabel, message: $"Unable to find label for language {languageCode}.")
            .Label;
    }

    private async Task DownloadMetadataForField(string tableName, string fieldName, string key)
    {
        if (_cachedMetadata.ContainsKey(key))
        {
            return;
        }
        RetrieveAttributeResponse metadataInfo = await _sdkProxy
            .ExecuteAsync<RetrieveAttributeResponse>(new RetrieveAttributeRequest
            {
                EntityLogicalName = tableName,
                LogicalName = fieldName,
                RetrieveAsIfPublished = true
            });
        if (metadataInfo == null || metadataInfo.AttributeMetadata == null)
        {
            return;
        }
        Guard.Against.Null(metadataInfo.AttributeMetadata.AttributeType);
        Guard.Against.AgainstExpression((attributeType) =>
        {
            return attributeType is AttributeTypeCode.State or AttributeTypeCode.Status
            or AttributeTypeCode.Picklist or AttributeTypeCode.Boolean;
        }, metadataInfo.AttributeMetadata.AttributeType.Value, $"Field[{fieldName}] in table {tableName} is not a valid optionset or state/status field.");
        switch (metadataInfo.AttributeMetadata)
        {
            case StatusAttributeMetadata statusAttributeMetadata:
                _cachedMetadata.TryAdd(key, statusAttributeMetadata.OptionSet.Options);
                break;

            case StateAttributeMetadata stateAttributeMetadata:
                _cachedMetadata.TryAdd(key, stateAttributeMetadata.OptionSet.Options);

                break;

            case PicklistAttributeMetadata picklistAttributeMetadata:
                _cachedMetadata.TryAdd(key, picklistAttributeMetadata.OptionSet.Options);

                break;

            case BooleanAttributeMetadata booleanAttributeMetadata:
                _cachedMetadata.TryAdd(key, new OptionMetadataCollection(new OptionMetadata[]{
                    booleanAttributeMetadata.OptionSet.TrueOption,
                    booleanAttributeMetadata.OptionSet.FalseOption
                }));

                break;

            case EnumAttributeMetadata enumAttributeMetadata:
                _cachedMetadata.TryAdd(key, enumAttributeMetadata.OptionSet.Options);

                break;

            default:
                throw new InvalidCastException($"Field[{fieldName}] in table {tableName} is not a valid option set, enum, boolean or state/status field.");
        }
    }
}

public sealed class FileFieldTypeMethods
{
    private readonly SdkProxy _sdkProxy;

    public FileFieldTypeMethods(SdkProxy sdkProxy)
    {
        _sdkProxy = sdkProxy;
    }

    public async Task<FileData> ReceiveFieldContent(Guid recordId, string tableName, string fieldName)
    {
        Guard.Against.NullOrEmpty(tableName);
        Guard.Against.Default(recordId);
        return await ReceiveFieldContent(new EntityReference(tableName, recordId), fieldName);
    }

    //TODO add read function for files bigger than 2gb

    public async Task<FileData> ReceiveFieldContent(EntityReference recordId, string fieldName)
    {
        Guard.Against.NullOrEmpty(fieldName);
        Guard.Against.Null(recordId);
        InitializeFileBlocksDownloadRequest fileBlocksRequest = new()
        {
            Target = recordId,
            FileAttributeName = fieldName
        };

        InitializeFileBlocksDownloadResponse fileBlockResponse = await _sdkProxy.ExecuteAsync<InitializeFileBlocksDownloadResponse>(fileBlocksRequest);
        if (fileBlockResponse.FileSizeInBytes > int.MaxValue)
        {
            throw new InvalidProgramException($"File size exceeded int maximum [{int.MaxValue}], it's a limitation of MemoryStream class. Consider using version of this method with callback.");
        }

        int sizeOfFileToDownload = Convert.ToInt32(fileBlockResponse.FileSizeInBytes);

        using MemoryStream fileContentStream = new(sizeOfFileToDownload);

        DownloadBlockRequest downloadBlockRequest = new()
        {
            FileContinuationToken = fileBlockResponse.FileContinuationToken
        };

        DownloadBlockResponse downloadBlockResponse = await _sdkProxy.ExecuteAsync<DownloadBlockResponse>(downloadBlockRequest);

        await fileContentStream.WriteAsync(downloadBlockResponse.Data);

        long downloadedBytes = downloadBlockResponse.Data.LongLength;
        while (downloadedBytes < fileBlockResponse.FileSizeInBytes)
        {
            DownloadBlockRequest downloadBlockRequestNext = new()
            {
                FileContinuationToken = fileBlockResponse.FileContinuationToken,
                Offset = downloadedBytes
            };

            DownloadBlockResponse downloadBlockResponseNext = await _sdkProxy.ExecuteAsync<DownloadBlockResponse>(downloadBlockRequestNext);
            await fileContentStream.WriteAsync(downloadBlockResponse.Data);
            downloadedBytes += downloadBlockResponseNext.Data.LongLength;
        }

        return new FileData
        {
            Data = fileContentStream.ToArray(),
            Name = fileBlockResponse.FileName,
            Size = fileBlockResponse.FileSizeInBytes
        };
    }

    public sealed class FileData
    {
        public byte[] Data { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
    }
}

public sealed class DataverseDevPack
{
    public readonly ExecuteMultipleLogic ExecuteMultiple;
    public readonly FieldDrill FieldDrill;
    public readonly SdkProxy SdkProxy;
    private readonly ILogger _logger;

    public DataverseDevPack(ILogger logger, bool applyConnectionOptimalization = true, params IConnectionCreator[] connectionCreators)
    {
        using EntryExitLogger logGuard = new(logger);
        SdkProxy = new SdkProxy(logger, applyConnectionOptimalization, connectionCreators);
        ExecuteMultiple = new ExecuteMultipleLogic(SdkProxy, logger);
        FieldDrill = new FieldDrill(SdkProxy, logger);
        _logger = logger;
    }

    public ILinqExpressionBuilder<T> CreateLinqExpressionBuilder<T>() where T : Entity, new() => LinqExpressionBuilder.Create<T>(_logger);
}