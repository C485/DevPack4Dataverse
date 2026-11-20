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
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse.FieldMethods;

public sealed class FileOrImageFieldTypeMethods
{
    private const int BytesInMegabyte = 1_048_576;
    private const int MaxBlockSizeInBytes = MaxBlockSizeInMegabytes * BytesInMegabyte;
    private const int MaxBlockSizeInMegabytes = 4;
    private readonly ExecuteMultipleLogic _executeMultipleLogic;
    private readonly ILogger _logger;
    private readonly SdkProxy _sdkProxy;

    public FileOrImageFieldTypeMethods(SdkProxy sdkProxy, ExecuteMultipleLogic executeMultipleLogic, ILogger logger)
    {
        using EntryExitLogger logGuard = new(logger);

        _logger = Guard.Against.Null(logger);
        _sdkProxy = Guard.Against.Null(sdkProxy);
        _executeMultipleLogic = Guard.Against.Null(executeMultipleLogic);
    }

    public async Task<bool> Delete(EntityReference recordId, string fieldName)
    {

        Guard.Against.Null(recordId);
        Guard.Against.NullOrEmpty(fieldName);
        Entity fileFieldData = await _sdkProxy
            .RetrieveAsync(recordId.LogicalName, recordId.Id, new ColumnSet(fieldName))
            .ConfigureAwait(false);
        if (!fileFieldData.TryGetAttributeValue(fieldName, out Guid fileId) || fileId == Guid.Empty)
        {
            return false;
        }
        await Delete(fileId).ConfigureAwait(false);
        return true;
    }

    public async Task Delete(Guid fileId)
    {

        DeleteFileRequest deleteFileRequest = new() { FileId = Guard.Against.Default(fileId) };
        _ = await _sdkProxy.ExecuteAsync<DeleteFileResponse>(deleteFileRequest).ConfigureAwait(false);
    }

    public async Task<FileData> Receive(Guid recordId, string logicalName, string fieldName)
    {

        return await Receive(EntityReferenceUtils.CreateEntityReference(recordId, logicalName, _logger), fieldName)
            .ConfigureAwait(false);
    }

    public async Task<FileData> Receive(EntityReference recordId, string fieldName)
    {

        InitializeFileBlocksDownloadRequest fileBlocksRequest =
            new() { Target = Guard.Against.Null(recordId), FileAttributeName = Guard.Against.NullOrEmpty(fieldName) };

        InitializeFileBlocksDownloadResponse fileBlockResponse = (await _sdkProxy
            .ExecuteAsync<InitializeFileBlocksDownloadResponse>(fileBlocksRequest)
            .ConfigureAwait(false))!;

        Guard.Against.Negative(fileBlockResponse.FileSizeInBytes);
        Guard.Against.OutOfRange(
            fileBlockResponse.FileSizeInBytes,
            nameof(InitializeFileBlocksDownloadResponse.FileSizeInBytes),
            0,
            int.MaxValue,
            $"File size exceeded int maximum [{int.MaxValue}], it's a limitation of MemoryStream class. Maximum file size for 'file' field is 128MB."
        );

        int sizeOfFileToDownload = Convert.ToInt32(fileBlockResponse.FileSizeInBytes);

        using MemoryStream fileContentStream = new(sizeOfFileToDownload);

        ExecuteMultipleRequestBuilder requestBuilder = _executeMultipleLogic.CreateRequestBuilder();
        int offset = 0;
        while (sizeOfFileToDownload > 0)
        {
            int currentChunkSize =
                sizeOfFileToDownload < MaxBlockSizeInBytes ? sizeOfFileToDownload : MaxBlockSizeInBytes;
            DownloadBlockRequest downloadBlockRequest =
                new()
                {
                    FileContinuationToken = fileBlockResponse.FileContinuationToken,
                    Offset = offset,
                    BlockLength = currentChunkSize
                };
            requestBuilder.AddRequest(downloadBlockRequest);
            offset += currentChunkSize;
            sizeOfFileToDownload -= currentChunkSize;
        }

        Models.ExecuteMultipleLogicResult executeMultipleLogicResult = await _executeMultipleLogic
            .ExecuteAsync(requestBuilder, new Models.ExecuteMultipleRequestSimpleSettings())
            .ConfigureAwait(false);
        // ExecuteMultipleLogic returns responses in the original request order across all chunks.
        foreach (ExecuteMultipleResponseItem responseItem in executeMultipleLogicResult.Results)
        {
            DownloadBlockResponse downloadBlockResponse =
                (DownloadBlockResponse)responseItem.Response;
            await fileContentStream
                .WriteAsync(
                    downloadBlockResponse.Data,
                    0,
                    downloadBlockResponse.Data.Length
                )
                .ConfigureAwait(false);
        }

        return new FileData
        {
            Data = GetFileBytes(fileContentStream),
            Name = fileBlockResponse.FileName,
            Size = fileBlockResponse.FileSizeInBytes
        };
    }

    public async Task<UploadFileFieldResult> Upload(
        string fileName,
        EntityReference recordId,
        string fieldName,
        byte[] data
    )
    {
        return await Upload(fileName, recordId, fieldName, new MemoryStream(Guard.Against.Null(data)))
            .ConfigureAwait(false);
    }

    public async Task<UploadFileFieldResult> Upload(
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
        InitializeFileBlocksUploadResponse fileBlockResponse = (await _sdkProxy
            .ExecuteAsync<InitializeFileBlocksUploadResponse>(fileBlocksRequest)
            .ConfigureAwait(false))!;

        ExecuteMultipleRequestBuilder requestBuilder = _executeMultipleLogic.CreateRequestBuilder();
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
        await _executeMultipleLogic
            .ExecuteAsync(requestBuilder, new Models.ExecuteMultipleRequestSimpleSettings())
            .ConfigureAwait(false);

        CommitFileBlocksUploadRequest commitFileBlocksUploadRequest =
            new()
            {
                FileContinuationToken = fileBlockResponse.FileContinuationToken,
                FileName = fileName,
                MimeType = MimeMapping.MimeUtility.GetMimeMapping(fileName),
                BlockList = GetBlockIds(requestBuilder)
            };
        CommitFileBlocksUploadResponse commitFileBlocksUploadResponse = (await _sdkProxy
            .ExecuteAsync<CommitFileBlocksUploadResponse>(commitFileBlocksUploadRequest)
            .ConfigureAwait(false))!;
        return new UploadFileFieldResult
        {
            SavedBytes = commitFileBlocksUploadResponse.FileSizeInBytes,
            FileId = commitFileBlocksUploadResponse.FileId
        };
    }

    private string GetRandomBase64FromRandomGuid()
    {

        // The block id is sent as a query value; a '+' is decoded as a space.
        string blockId;
        do
        {
            blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }
        while (blockId.Contains('+'));

        return blockId;
    }

    private static byte[] GetFileBytes(MemoryStream stream)
    {
        if (
            stream.TryGetBuffer(out ArraySegment<byte> buffer)
            && buffer.Array != null
            && buffer.Count == buffer.Array.Length
        )
        {
            return buffer.Array;
        }

        return stream.ToArray();
    }

    private static string[] GetBlockIds(ExecuteMultipleRequestBuilder requestBuilder)
    {
        List<string> blockIds = new(requestBuilder.Count);
        foreach (OrganizationRequest request in requestBuilder.RequestWithResults.Requests)
        {
            blockIds.Add(((UploadBlockRequest)request).BlockId);
        }

        return blockIds.ToArray();
    }

    public sealed class FileData
    {
        public byte[] Data { get; set; } = null!;
        public string Name { get; set; } = null!;
        public long Size { get; set; }
    }

    public sealed class UploadFileFieldResult
    {
        public Guid FileId { get; set; }
        public long SavedBytes { get; set; }
    }
}
