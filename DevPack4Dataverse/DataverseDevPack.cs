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
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Concurrent;

namespace DevPack4Dataverse;

public sealed class ChoiceFieldTypeMethods
{
    private const int NoLanguageFilter = -1;
    private readonly ConcurrentDictionary<string, OptionMetadataCollection> _cachedMetadata; //TODO use cache
    private readonly SdkProxy _sdkProxy;
    //TODO add string to optionset logic
    //TODO add map multiple and use executemultiplelogic
    //TODO use key struct instead of string
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

public sealed class FileOrImageFieldTypeMethods
{
    private const int _bytesInMegabyte = 1_048_576;
    private const int _maxBlockSizeInBytes = _maxBlockSizeInMegabytes * _bytesInMegabyte;
    private const int _maxBlockSizeInMegabytes = 4;
    private readonly ExecuteMultipleLogic _executeMultipleLogic;
    private readonly SdkProxy _sdkProxy;

    public FileOrImageFieldTypeMethods(SdkProxy sdkProxy, ExecuteMultipleLogic executeMultipleLogic)
    {
        _sdkProxy = sdkProxy;
        _executeMultipleLogic = executeMultipleLogic;
    }

    public async Task<bool> Delete(EntityReference recordId, string fieldName)
    {
        Guard.Against.Null(recordId);
        Guard.Against.NullOrEmpty(fieldName);
        Entity fileFieldData = await _sdkProxy.RetrieveAsync(recordId.LogicalName, recordId.Id, new ColumnSet(fieldName));
        if (!fileFieldData.TryGetAttributeValue(fieldName, out Guid fileId) && fileId != Guid.Empty)
        {
            return false;
        }
        await Delete(fileId);
        return true;
    }

    public async Task Delete(Guid fileId)
    {
        Guard.Against.Default(fileId);
        DeleteFileRequest deleteFileRequest = new()
        {
            FileId = fileId
        };
        _ = await _sdkProxy.ExecuteAsync<DeleteFileResponse>(deleteFileRequest);
    }

    public async Task<FileData> Receive(Guid recordId, string tableName, string fieldName)
    {
        Guard
            .Against
            .NullOrEmpty(tableName);
        Guard
            .Against
            .Default(recordId);
        return await Receive(new EntityReference(tableName, recordId), fieldName);
    }

    public async Task<FileData> Receive(EntityReference recordId, string fieldName)
    {
        Guard.Against.NullOrEmpty(fieldName);
        Guard.Against.Null(recordId);
        InitializeFileBlocksDownloadRequest fileBlocksRequest = new()
        {
            Target = recordId,
            FileAttributeName = fieldName
        };

        InitializeFileBlocksDownloadResponse fileBlockResponse = await _sdkProxy.ExecuteAsync<InitializeFileBlocksDownloadResponse>(fileBlocksRequest);

        Guard
            .Against
            .Negative(fileBlockResponse.FileSizeInBytes);
        Guard
            .Against
            .OutOfRange(fileBlockResponse.FileSizeInBytes, nameof(InitializeFileBlocksDownloadResponse.FileSizeInBytes), 0, int.MaxValue, $"File size exceeded int maximum [{int.MaxValue}], it's a limitation of MemoryStream class. Maximum file size for 'file' field is 128MB.");

        int sizeOfFileToDownload = Convert.ToInt32(fileBlockResponse.FileSizeInBytes);

        using MemoryStream fileContentStream = new(sizeOfFileToDownload);

        ExecuteMultipleRequestBuilder requestBuilder = _executeMultipleLogic.CreateRequestBuilder();
        while (sizeOfFileToDownload > 0)
        {
            int currentChunkSize = sizeOfFileToDownload < _maxBlockSizeInBytes ? sizeOfFileToDownload : _maxBlockSizeInBytes;
            sizeOfFileToDownload -= currentChunkSize;
            DownloadBlockRequest downloadBlockRequest = new()
            {
                FileContinuationToken = fileBlockResponse.FileContinuationToken,
                Offset = sizeOfFileToDownload,
                BlockLength = currentChunkSize
            };
            requestBuilder.AddRequest(downloadBlockRequest);
        }

        Models.ExecuteMultipleLogicResult executeMultipleLogicResult = await _executeMultipleLogic.ExecuteAsync(requestBuilder, new Models.ExecuteMultipleRequestSimpleSettings());
        foreach (DownloadBlockResponse downloadBlockResponse in executeMultipleLogicResult.Results.OrderBy(p => p.RequestIndex).Cast<DownloadBlockResponse>())
        {
            await fileContentStream.WriteAsync(downloadBlockResponse.Data);
        }

        return new FileData
        {
            Data = fileContentStream.ToArray(),
            Name = fileBlockResponse.FileName,
            Size = fileBlockResponse.FileSizeInBytes
        };
    }

    public async Task<UploadFileFieldResult> Upload(string fileName, EntityReference recordId, string fieldName, byte[] data)
    {
        return await Upload(fileName, recordId, fieldName, new MemoryStream(data));
    }

    public async Task<UploadFileFieldResult> Upload(string fileName, EntityReference recordId, string fieldName, Stream dataStream)
    {
        InitializeFileBlocksUploadRequest fileBlocksRequest = new()
        {
            FileName = fileName,
            Target = recordId,
            FileAttributeName = fieldName,
        };
        InitializeFileBlocksUploadResponse fileBlockResponse =
            await _sdkProxy.ExecuteAsync<InitializeFileBlocksUploadResponse>(fileBlocksRequest);

        ExecuteMultipleRequestBuilder requestBuilder = _executeMultipleLogic.CreateRequestBuilder();
        BinaryReader dataBinaryReader = new(dataStream);
        while (true)
        {
            byte[] chunkData = dataBinaryReader.ReadBytes(_maxBlockSizeInBytes);
            if (chunkData == null || chunkData.Length == 0)
            {
                break;
            }
            string randomBase64 = GetRandomBase64FromRandomGuid();
            UploadBlockRequest uploadBlockRequest = new()
            {
                BlockData = chunkData,
                FileContinuationToken = fileBlockResponse.FileContinuationToken,
                BlockId = randomBase64
            };
            requestBuilder.AddRequest(uploadBlockRequest);
        }
        await _executeMultipleLogic.ExecuteAsync(requestBuilder, new Models.ExecuteMultipleRequestSimpleSettings());

        CommitFileBlocksUploadRequest commitFileBlocksUploadRequest = new()
        {
            FileContinuationToken = fileBlockResponse.FileContinuationToken,
            FileName = fileName,
            MimeType = MimeMapping.MimeUtility.GetMimeMapping(fileName),
            BlockList = requestBuilder.RequestWithResults.Requests.Cast<UploadBlockRequest>().Select(p => p.BlockId).ToArray()
        };
        CommitFileBlocksUploadResponse commitFileBlocksUploadResponse =
            await _sdkProxy.ExecuteAsync<CommitFileBlocksUploadResponse>(commitFileBlocksUploadRequest);
        return new UploadFileFieldResult
        {
            SavedBytes = commitFileBlocksUploadResponse.FileSizeInBytes,
            FileId = commitFileBlocksUploadResponse.FileId
        };
    }

    private string GetRandomBase64FromRandomGuid()
    {
        Guid genGuid = Guid.NewGuid();
        Span<byte> guidBytes = stackalloc byte[16];
        if (!genGuid.TryWriteBytes(guidBytes))
        {
            throw new InvalidPluginExecutionException("Failed to generate random base64 from random GUID.");
        }
        return Convert.ToBase64String(guidBytes);
    }

    public sealed class FileData
    {
        public byte[] Data { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
    }

    public sealed class UploadFileFieldResult
    {
        public Guid FileId { get; set; }
        public long SavedBytes { get; set; }
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