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
using DevPack4Dataverse.Utils;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse.Extension;

public static class FileOrImageFieldTypeMethods
{
    private const int BytesInMegabyte = 1_048_576;
    private const int MaxBlockSizeInBytes = MaxBlockSizeInMegabytes * BytesInMegabyte;
    private const int MaxBlockSizeInMegabytes = 4;

    public static async Task<bool> ExtDeleteFile(
        this IOrganizationServiceAsync organizationService,
        EntityReference recordId,
        string fieldName
    )
    {
        Guard.Against.Null(recordId);
        Guard.Against.NullOrEmpty(fieldName);
        Entity fileFieldData = await organizationService.RetrieveAsync(
            recordId.LogicalName,
            recordId.Id,
            new ColumnSet(fieldName)
        );
        if (!fileFieldData.TryGetAttributeValue(fieldName, out Guid fileId) && fileId != Guid.Empty)
        {
            return false;
        }
        await organizationService.ExtDeleteFile(fileId);
        return true;
    }

    public static async Task ExtDeleteFile(this IOrganizationServiceAsync organizationService, Guid fileId)
    {
        DeleteFileRequest deleteFileRequest = new() { FileId = Guard.Against.Default(fileId) };
        _ = await organizationService.ExtExecuteAsync<DeleteFileResponse>(deleteFileRequest);
    }

    public static async Task<FileData> ExtDownloadFile(
        this IOrganizationServiceAsync organizationService,
        Guid recordId,
        string logicalName,
        string fieldName
    )
    {
        return await organizationService.ExtDownloadFile(
            EntityReferenceUtils.CreateEntityReference(recordId, logicalName),
            fieldName
        );
    }

    public static async Task<FileData> ExtDownloadFile(
        this IOrganizationServiceAsync organizationService,
        EntityReference recordId,
        string fieldName
    )
    {
        InitializeFileBlocksDownloadRequest fileBlocksRequest =
            new() { Target = Guard.Against.Null(recordId), FileAttributeName = Guard.Against.NullOrEmpty(fieldName) };

        InitializeFileBlocksDownloadResponse? fileBlockResponse =
            await organizationService.ExtExecuteAsync<InitializeFileBlocksDownloadResponse>(fileBlocksRequest);
        Guard.Against.Null(fileBlockResponse);
        Guard.Against.Negative(fileBlockResponse.FileSizeInBytes);
        Guard
            .Against
            .OutOfRange(
                fileBlockResponse.FileSizeInBytes,
                nameof(InitializeFileBlocksDownloadResponse.FileSizeInBytes),
                0,
                int.MaxValue,
                $"File size exceeded int maximum [{int.MaxValue}], it's a limitation of MemoryStream class. Maximum file size for 'file' field is 128MB."
            );

        int sizeOfFileToDownload = Convert.ToInt32(fileBlockResponse.FileSizeInBytes);

        using MemoryStream fileContentStream = new(sizeOfFileToDownload);

        ExecuteMultipleRequestBuilder requestBuilder = ExecuteMultipleRequestBuilder.Create();
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

        ExecuteMultipleResponse? executeMultipleLogicResult = await organizationService.ExtExecuteAsync(requestBuilder);
        foreach (
            DownloadBlockResponse downloadBlockResponse in Guard
                .Against
                .Null(executeMultipleLogicResult)
                .Results
                .Select(p => p.Value)
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

    public static async Task<UploadFileFieldResult> ExtUploadFile(
        this IOrganizationServiceAsync organizationService,
        string fileName,
        EntityReference recordId,
        string fieldName,
        byte[] data
    )
    {
        return await organizationService.ExtUploadFile(
            fileName,
            recordId,
            fieldName,
            new MemoryStream(Guard.Against.Null(data))
        );
    }

    public static async Task<UploadFileFieldResult> ExtUploadFile(
        this IOrganizationServiceAsync organizationService,
        string fileName,
        EntityReference recordId,
        string fieldName,
        Stream dataStream
    )
    {
        InitializeFileBlocksUploadRequest fileBlocksRequest =
            new()
            {
                FileName = Guard.Against.NullOrEmpty(fileName),
                Target = Guard.Against.Null(recordId),
                FileAttributeName = Guard.Against.NullOrEmpty(fieldName),
            };
        InitializeFileBlocksUploadResponse? fileBlockResponse =
            await organizationService.ExtExecuteAsync<InitializeFileBlocksUploadResponse>(fileBlocksRequest);

        ExecuteMultipleRequestBuilder requestBuilder = ExecuteMultipleRequestBuilder.Create();
        BinaryReader dataBinaryReader = new(Guard.Against.Null(dataStream));
        while (true)
        {
            byte[] chunkData = dataBinaryReader.ReadBytes(MaxBlockSizeInBytes);
            if (chunkData == null || chunkData.Length == 0)
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
        await organizationService.ExtExecuteAsync(requestBuilder);

        CommitFileBlocksUploadRequest commitFileBlocksUploadRequest =
            new()
            {
                FileContinuationToken = fileBlockResponse.FileContinuationToken,
                FileName = fileName,
                MimeType = MimeMapping.MimeUtility.GetMimeMapping(fileName),
                BlockList = requestBuilder.Build().Requests.Cast<UploadBlockRequest>().Select(p => p.BlockId).ToArray()
            };
        CommitFileBlocksUploadResponse? commitFileBlocksUploadResponse =
            await organizationService.ExtExecuteAsync<CommitFileBlocksUploadResponse>(commitFileBlocksUploadRequest);
        return new UploadFileFieldResult
        {
            SavedBytes = Guard.Against.Null(commitFileBlocksUploadResponse).FileSizeInBytes,
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
