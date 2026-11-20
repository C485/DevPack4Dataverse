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

using DevPack4Dataverse.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace DevPack4Dataverse.Creators;

internal static class ConnectionFactory
{
    /// <summary>
    /// Wraps a validated client and creates the requested number of clones. Each clone is a
    /// separate <see cref="ServiceClient" />, so each connection can run one request at a time
    /// in parallel with the others.
    /// </summary>
    public static IReadOnlyList<IConnection> Create(
        ServiceClient validatedClient,
        int connectionCount,
        ILogger logger
    )
    {
        List<ServiceClient> clients = new(connectionCount) { validatedClient };
        List<IConnection> connections = new(connectionCount);
        try
        {
            Connection first = new(validatedClient, logger);
            if (!first.Test())
            {
                throw new InvalidProgramException("Test on connection failed.");
            }

            connections.Add(first);
            for (int index = 1; index < connectionCount; index++)
            {
                ServiceClient? clone = validatedClient.Clone(logger);
                if (clone == null)
                {
                    throw new InvalidProgramException(
                        "Dataverse returned no ServiceClient clone for the connection."
                    );
                }

                clients.Add(clone);
                if (!clone.IsReady)
                {
                    throw new InvalidProgramException(
                        $"Cloned connection is not ready. LatestError: {clone.LastError}"
                    );
                }

                connections.Add(new Connection(clone, logger));
            }

            return connections;
        }
        catch
        {
            foreach (ServiceClient client in clients)
            {
                client.Dispose();
            }

            throw;
        }
    }
}
