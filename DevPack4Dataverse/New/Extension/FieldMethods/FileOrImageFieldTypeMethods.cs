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

using CommunityToolkit.Diagnostics;
using DevPack4Dataverse.Models;
using DevPack4Dataverse.New.ExecuteMultiple;
using DevPack4Dataverse.Utils;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using MimeMapping;

namespace DevPack4Dataverse.New.Extension.FieldMethods;

public sealed class FileOrImageFieldTypeMethods
{
    private const int BytesInMegabyte = 1_048_576;
    private const int MaxBlockSizeInBytes = MaxBlockSizeInMegabytes * BytesInMegabyte;
    private const int MaxBlockSizeInMegabytes = 4;
    private readonly ExecuteMultipleLogic _executeMultipleLogic;
    private readonly ServiceClient _serviceClient;

    public FileOrImageFieldTypeMethods(ServiceClient serviceClient, ExecuteMultipleLogic executeMultipleLogic)
    {
        Guard.IsNotNull(serviceClient);
        Guard.IsNotNull(executeMultipleLogic);
        _serviceClient = serviceClient;
        _executeMultipleLogic = executeMultipleLogic;
    }

    public async Task<bool> Delete(EntityReference recordId, string fieldName)
    {
        Guard.IsNotNull(recordId);
        Guard.IsNotDefault(recordId.Id);
        Guard.IsNotNullOrEmpty(recordId.LogicalName);
        Guard.IsNotNullOrEmpty(fieldName);
        Entity fileFieldData = await _serviceClient.RetrieveAsync(recordId.LogicalName,
            recordId.Id,
            new ColumnSet(fieldName));

        if (!fileFieldData.TryGetAttributeValue(fieldName, out Guid fileId) && fileId != Guid.Empty)
        {
            return false;
        }

        await Delete(fileId);

        return true;
    }

    public async Task Delete(Guid fileId)
    {
        Guard.IsNotDefault(fileId);

        DeleteFileRequest deleteFileRequest = new()
        {
            FileId = fileId
        };

        _ = await _serviceClient.Extension().ExecuteAsync<DeleteFileResponse>(deleteFileRequest);
    }

    public async Task<FileData> Receive(Guid recordId, string logicalName, string fieldName)
    {
        Guard.IsNotDefault(recordId);
        Guard.IsNotNullOrEmpty(logicalName);
        Guard.IsNotNullOrEmpty(fieldName);

        return await Receive(EntityReferenceUtils.CreateEntityReference(recordId, logicalName), fieldName);
    }

    public async Task<FileData> Receive(EntityReference recordId, string fieldName)
    {
        Guard.IsNotNull(recordId);
        Guard.IsNotDefault(recordId.Id);
        Guard.IsNotNullOrEmpty(recordId.LogicalName);
        Guard.IsNotNullOrEmpty(fieldName);

        InitializeFileBlocksDownloadRequest fileBlocksRequest =
            new()
            {
                Target = recordId,
                FileAttributeName = fieldName
            };

        InitializeFileBlocksDownloadResponse fileBlockResponse =
            await _serviceClient.Extension().ExecuteAsync<InitializeFileBlocksDownloadResponse>(fileBlocksRequest);

        if (fileBlockResponse.FileSizeInBytes > int.MaxValue)
        {
            throw new InvalidProgramException(
                $"File size exceeded int maximum [{int.MaxValue}], it's a limitation of MemoryStream class. Maximum file size for 'file' field is 128MB.");
        }

        Guard.IsLessThan(fileBlockResponse.FileSizeInBytes, 0);

        int sizeOfFileToDownload = Convert.ToInt32(fileBlockResponse.FileSizeInBytes);

        using MemoryStream fileContentStream = new(sizeOfFileToDownload);

        ExecuteMultipleRequestBuilder requestBuilder = _executeMultipleLogic.CreateRequestBuilder();

        while (sizeOfFileToDownload > 0)
        {
            int currentChunkSize =
                sizeOfFileToDownload < MaxBlockSizeInBytes ? sizeOfFileToDownload : MaxBlockSizeInBytes;

            sizeOfFileToDownload -= currentChunkSize;
            DownloadBlockRequest downloadBlockRequest =
                new()
                {
                    FileContinuationToken = fileBlockResponse.FileContinuationToken,
                    Offset = sizeOfFileToDownload,
                    BlockLength = currentChunkSize
                };

            requestBuilder.AddRequest(downloadBlockRequest);
        }

        ExecuteMultipleLogicResult executeMultipleLogicResult = await _executeMultipleLogic.ExecuteAsync(requestBuilder,
            new ExecuteMultipleRequestSimpleSettings());

        foreach (
            DownloadBlockResponse downloadBlockResponse in executeMultipleLogicResult.Results
               .OrderBy(p => p.RequestIndex)
               .Select(p => p.Response)
               .Cast<DownloadBlockResponse>()
        )
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

    public async Task<UploadFileFieldResult> Upload(
        string fileName,
        EntityReference recordId,
        string fieldName,
        byte[] data)
    {
        Guard.IsNotNull(data);
        Guard.IsNotNullOrEmpty(fieldName);
        Guard.IsNotNullOrEmpty(fileName);
        Guard.IsNotNull(recordId);
        Guard.IsNotDefault(recordId.Id);
        Guard.IsNotNullOrEmpty(recordId.LogicalName);
        using MemoryStream memoryStream = new(data);

        return await Upload(fileName, recordId, fieldName, memoryStream);
    }

    public async Task<UploadFileFieldResult> Upload(
        string fileName,
        EntityReference recordId,
        string fieldName,
        Stream dataStream)
    {
        Guard.IsNotNull(dataStream);
        Guard.IsNotNullOrEmpty(fieldName);
        Guard.IsNotNullOrEmpty(fileName);
        Guard.IsNotNull(recordId);
        Guard.IsNotDefault(recordId.Id);
        Guard.IsNotNullOrEmpty(recordId.LogicalName);

        InitializeFileBlocksUploadRequest fileBlocksRequest =
            new()
            {
                FileName = fileName,
                Target = recordId,
                FileAttributeName = fieldName
            };

        InitializeFileBlocksUploadResponse fileBlockResponse =
            await _serviceClient.Extension().ExecuteAsync<InitializeFileBlocksUploadResponse>(fileBlocksRequest);

        ExecuteMultipleRequestBuilder requestBuilder = _executeMultipleLogic.CreateRequestBuilder();
        BinaryReader dataBinaryReader = new(dataStream);

        while (true)
        {
            byte[] chunkData = dataBinaryReader.ReadBytes(MaxBlockSizeInBytes);

            if (chunkData.Length == 0)
            {
                break;
            }

            string randomBase64 = GetRandomBase64FromRandomGuid();
            UploadBlockRequest uploadBlockRequest =
                new()
                {
                    BlockData = chunkData,
                    FileContinuationToken = fileBlockResponse.FileContinuationToken,
                    BlockId = randomBase64
                };

            requestBuilder.AddRequest(uploadBlockRequest);
        }

        await _executeMultipleLogic.ExecuteAsync(requestBuilder, new ExecuteMultipleRequestSimpleSettings());

        CommitFileBlocksUploadRequest commitFileBlocksUploadRequest =
            new()
            {
                FileContinuationToken = fileBlockResponse.FileContinuationToken,
                FileName = fileName,
                MimeType = MimeUtility.GetMimeMapping(fileName),
                BlockList = requestBuilder
                   .Build()
                   .Requests
                   .Cast<UploadBlockRequest>()
                   .Select(p => p.BlockId)
                   .ToArray()
            };

        CommitFileBlocksUploadResponse commitFileBlocksUploadResponse =
            await _serviceClient.Extension()
               .ExecuteAsync<CommitFileBlocksUploadResponse>(commitFileBlocksUploadRequest);

        return new UploadFileFieldResult
        {
            SavedBytes = commitFileBlocksUploadResponse.FileSizeInBytes,
            FileId = commitFileBlocksUploadResponse.FileId
        };
    }

    private static string GetRandomBase64FromRandomGuid()
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
