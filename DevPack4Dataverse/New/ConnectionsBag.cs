using System.Collections.Concurrent;
using CommunityToolkit.Diagnostics;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace DevPack4Dataverse.New;

public class ConnectionsBag
{
    private readonly ConcurrentBag<ServiceClient> _connections = new();

    public int Count => _connections.Count;

    public ServiceClient[] Clone()
    {
        return _connections.Select(p => p.Clone()).ToArray();
    }

    public void Add(ServiceClient connection)
    {
        Guard.IsNotNull(connection);
        _connections.Add(connection);
    }

    public ServiceClient CloneRandom()
    {
        int index = Random.Shared.Next(_connections.Count);

        return _connections.ElementAt(index).Clone();
    }
}
