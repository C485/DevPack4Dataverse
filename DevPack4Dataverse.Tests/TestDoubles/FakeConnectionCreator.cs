using Microsoft.Extensions.Logging;
using DevPack4Dataverse.Interfaces;

namespace DevPack4Dataverse.Tests.TestDoubles;

public sealed class FakeConnectionCreator : IConnectionCreator
{
    private readonly IConnection[] _connections;

    public FakeConnectionCreator(params IConnection[] connections)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
    }

    public bool IsCreated { get; private set; }
    public bool IsError { get; private set; }
    public bool IsValid => IsCreated && !IsError;
    public int ConnectionCount => _connections.Length;

    public IReadOnlyList<IConnection> Create(ILogger logger)
    {
        if (_connections.Length == 0)
        {
            throw new InvalidOperationException("No connection configured.");
        }

        IsCreated = true;
        return _connections;
    }
}
