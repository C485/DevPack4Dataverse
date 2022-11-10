using Ardalis.GuardClauses;
using C485.DataverseClientProxy.Interfaces;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace C485.DataverseClientProxy;

public sealed class ConnectionManager
{
    private readonly ConcurrentBag<IConnection> _connections;

    private readonly TimeSpan _sleepTimeForConnectionGetter = TimeSpan.FromMilliseconds(10);

    public ConnectionManager()
    {
        _connections = new ConcurrentBag<IConnection>();
    }

    public int ConnectionCount => _connections.Count;

    public async Task AddNewConnection(IConnectionCreator connectionCreator)
    {
        await Task.Run(() =>
        {
            Guard
               .Against
               .Null(connectionCreator);

            IConnection newConnection = connectionCreator
               .Create();

            Guard
               .Against
               .Null(newConnection);

            _connections
               .Add(newConnection);
        });
    }

    [SuppressMessage("Minor Code Smell",
                    "S3267:Loops should be simplified with \"LINQ\" expressions",
        Justification = "No locking in LINQ")]
    public ConnectionLease GetConnection()
    {
        Guard.Against.InvalidInput(_connections, nameof(_connections), p => p.IsEmpty, "Please add at least one connection.");
        while (true)
        {
            foreach (IConnection connection in _connections)
            {
                if (connection.TryLock())
                {
                    return new ConnectionLease(connection);
                }
            }

            Thread
               .Sleep(_sleepTimeForConnectionGetter);
        }
    }

    [SuppressMessage("Minor Code Smell",
        "S3267:Loops should be simplified with \"LINQ\" expressions",
        Justification = "No locking in LINQ")]
    public async Task<ConnectionLease> GetConnectionAsync()
    {
        Guard.Against.InvalidInput(_connections, nameof(_connections), p => p.IsEmpty, "Please add at least one connection.");

        return await Task.Run(async () =>
        {
            while (true)
            {
                foreach (IConnection connection in _connections)
                {
                    if (connection.TryLock())
                    {
                        return new ConnectionLease(connection);
                    }
                }

                await Task
                   .Delay(_sleepTimeForConnectionGetter);
            }
        });
    }
}