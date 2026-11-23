using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Xunit;
using DevPack4Dataverse.ExecuteMultiple;
using DevPack4Dataverse.FieldMethods;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

public class FileOrImageFieldTypeMethodsTests
{
    private static (SdkProxy Proxy, FileOrImageFieldTypeMethods Methods) Create(
        FakeConnection connection
    )
    {
        var proxy = new SdkProxy(
            NullLogger.Instance,
            false,
            new FakeConnectionCreator(connection)
        );
        proxy.WaitForConnections(1);
        return (
            proxy,
            new FileOrImageFieldTypeMethods(
                proxy,
                new ExecuteMultipleLogic(proxy, NullLogger.Instance),
                NullLogger.Instance
            )
        );
    }

    [Fact]
    public async Task Delete_WhenFieldIsMissing_ReturnsFalse()
    {
        var connection = new FakeConnection
        {
            RetrieveHandler = (entityName, id, _) => new Entity(entityName, id)
        };
        var (proxy, methods) = Create(connection);

        using (proxy)
        {
            bool deleted = await methods.Delete(
                new EntityReference("account", Guid.NewGuid()),
                "new_file"
            );

            Assert.False(deleted);
        }
    }

    [Fact]
    public async Task Delete_WhenFieldIsImage_ClearsTheColumn()
    {
        Entity? update = null;
        var connection = new FakeConnection
        {
            RetrieveHandler = (entityName, id, _) =>
                new Entity(entityName, id) { ["new_image"] = new byte[] { 1, 2, 3 } },
            UpdateRecordHandler = entity => update = entity
        };
        var (proxy, methods) = Create(connection);

        using (proxy)
        {
            bool deleted = await methods.Delete(
                new EntityReference("account", Guid.NewGuid()),
                "new_image"
            );

            Assert.True(deleted);
        }

        Assert.NotNull(update);
        Assert.True(update!.Contains("new_image"));
        Assert.Null(update["new_image"]);
    }

    [Fact]
    public async Task Delete_WhenFieldIsPresent_DeletesTheFile()
    {
        Guid fileId = Guid.NewGuid();
        DeleteFileRequest? captured = null;
        var connection = new FakeConnection
        {
            RetrieveHandler = (entityName, id, _) =>
                new Entity(entityName, id) { ["new_file"] = fileId },
            ExecuteHandler = request =>
            {
                captured = Assert.IsType<DeleteFileRequest>(request);
                return new DeleteFileResponse();
            }
        };
        var (proxy, methods) = Create(connection);

        using (proxy)
        {
            bool deleted = await methods.Delete(
                new EntityReference("account", Guid.NewGuid()),
                "new_file"
            );

            Assert.True(deleted);
        }

        Assert.NotNull(captured);
        Assert.Equal(fileId, captured.FileId);
    }

    [Fact]
    public async Task Receive_DownloadsAllBlocksInOffsetOrder()
    {
        const int blockSize = 4 * 1024 * 1024;
        byte[] data = new byte[(blockSize * 2) + 1024];
        for (int index = 0; index < data.Length; index++)
        {
            data[index] = (byte)(index % 251);
        }

        var offsets = new List<long>();
        var connection = new FakeConnection
        {
            ExecuteHandler = request =>
            {
                switch (request)
                {
                    case InitializeFileBlocksDownloadRequest:
                        var initialize = new InitializeFileBlocksDownloadResponse();
                        initialize.Results["FileSizeInBytes"] = (long)data.Length;
                        initialize.Results["FileContinuationToken"] = "token";
                        initialize.Results["FileName"] = "test.bin";
                        initialize.Results["IsChunkingSupported"] = true;
                        return initialize;
                    case ExecuteMultipleRequest executeMultiple:
                        var responses = new ExecuteMultipleResponseItemCollection();
                        for (int index = 0; index < executeMultiple.Requests.Count; index++)
                        {
                            var download = Assert.IsType<DownloadBlockRequest>(
                                executeMultiple.Requests[index]
                            );
                            offsets.Add(download.Offset);
                            var block = new DownloadBlockResponse();
                            block.Results["Data"] = data
                                .Skip((int)download.Offset)
                                .Take((int)download.BlockLength)
                                .ToArray();
                            responses.Add(
                                new ExecuteMultipleResponseItem
                                {
                                    RequestIndex = index,
                                    Response = block
                                }
                            );
                        }

                        var response = new ExecuteMultipleResponse();
                        response.Results["Responses"] = responses;
                        return response;
                    default:
                        throw new NotSupportedException(request.RequestName);
                }
            }
        };
        var (proxy, methods) = Create(connection);

        using (proxy)
        {
            FileOrImageFieldTypeMethods.FileData file = await methods.Receive(
                new EntityReference("account", Guid.NewGuid()),
                "new_file"
            );

            Assert.Equal(data.Length, file.Size);
            Assert.Equal("test.bin", file.Name);
            Assert.Equal(data, file.Data);
        }

        Assert.Equal(new long[] { 0, blockSize, blockSize * 2 }, offsets);
    }

    [Fact]
    public async Task Upload_SendsBlocksInOrderAndCommitsTheSameBlockList()
    {
        const int blockSize = 4 * 1024 * 1024;
        byte[] data = new byte[blockSize * 9];
        var uploadedBlockIds = new List<string>();
        var batchSizes = new List<int>();
        string[]? committedBlocks = null;
        var connection = new FakeConnection
        {
            ExecuteHandler = request =>
            {
                switch (request)
                {
                    case InitializeFileBlocksUploadRequest:
                        var initialize = new InitializeFileBlocksUploadResponse();
                        initialize.Results["FileContinuationToken"] = "token";
                        return initialize;
                    case ExecuteMultipleRequest executeMultiple:
                        batchSizes.Add(executeMultiple.Requests.Count);
                        var responses = new ExecuteMultipleResponseItemCollection();
                        for (int index = 0; index < executeMultiple.Requests.Count; index++)
                        {
                            var upload = Assert.IsType<UploadBlockRequest>(
                                executeMultiple.Requests[index]
                            );
                            uploadedBlockIds.Add(upload.BlockId);
                            responses.Add(
                                new ExecuteMultipleResponseItem
                                {
                                    RequestIndex = index,
                                    Response = new TestResponse()
                                }
                            );
                        }

                        var response = new ExecuteMultipleResponse();
                        response.Results["Responses"] = responses;
                        return response;
                    case CommitFileBlocksUploadRequest commit:
                        committedBlocks = commit.BlockList;
                        var commitResponse = new CommitFileBlocksUploadResponse();
                        commitResponse.Results["FileSizeInBytes"] = (long)data.Length;
                        commitResponse.Results["FileId"] = Guid.NewGuid();
                        return commitResponse;
                    default:
                        throw new NotSupportedException(request.RequestName);
                }
            }
        };
        var (proxy, methods) = Create(connection);

        using (proxy)
        {
            FileOrImageFieldTypeMethods.UploadFileFieldResult result = await methods.Upload(
                "test.bin",
                new EntityReference("account", Guid.NewGuid()),
                "new_file",
                data
            );

            Assert.Equal(data.Length, result.SavedBytes);
        }

        Assert.Equal(new[] { 6, 2, 1 }, batchSizes);
        Assert.Equal(9, uploadedBlockIds.Count);
        Assert.All(uploadedBlockIds, id => Assert.DoesNotContain("+", id));
        Assert.NotNull(committedBlocks);
        Assert.Equal(uploadedBlockIds, committedBlocks);
    }

    private sealed class TestResponse : OrganizationResponse { }
}
