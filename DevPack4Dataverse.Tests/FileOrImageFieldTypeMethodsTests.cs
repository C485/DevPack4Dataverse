using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
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
}
