using Microsoft.Extensions.Logging;
using DevPack4Dataverse.Interfaces;

namespace DevPack4Dataverse.Tests.TestDoubles;

public sealed class FakeConnectionCreator(IConnection connection) : IConnectionCreator
{
    public bool IsCreated { get; private set; }
    public bool IsError { get; private set; }
    public bool IsValid => IsCreated && !IsError;

    public IConnection Create(ILogger logger)
    {
        if (connection == null)
        {
            throw new InvalidOperationException("No connection configured.");
        }

        IsCreated = true;
        return connection;
    }
}
