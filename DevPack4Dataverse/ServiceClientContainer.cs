/*
Copyright 2024 Kamil Skoracki / C485@GitHub

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

using System.Collections.Concurrent;
using Ardalis.GuardClauses;
using DevPack4Dataverse.Extension;
using DevPack4Dataverse.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace DevPack4Dataverse;

public sealed class ServiceClientContainer
{
    public ConcurrentBag<ServiceClient> Connections { get; private init; } = [];

    public int Count => Connections.Count;

    public ServiceClientContainer Clone(ILogger? logger = null)
    {
        return new ServiceClientContainer
        {
            Connections = new ConcurrentBag<ServiceClient>(Connections.Select(c => c.Clone(logger)))
        };
    }

    public ServiceClient CloneRandom(ILogger? logger = null)
    {
        int index = Random.Shared.Next(Connections.Count - 1);
        return Connections.ElementAt(index).Clone(logger);
    }

    public ServiceClient GetRandom()
    {
        int index = Random.Shared.Next(Connections.Count - 1);
        return Connections.ElementAt(index);
    }
}

public sealed class ServiceClientContainerBuilder
{
    private readonly bool _applyConnectionOptymization;
    private readonly ConcurrentBag<IConnectionCreator> _connectionCreators;

    public ServiceClientContainerBuilder(bool applyConnectionOptimization = false, params IConnectionCreator[] connectionCreators)
    {
        _connectionCreators = new ConcurrentBag<IConnectionCreator>(connectionCreators);
        _applyConnectionOptymization = applyConnectionOptimization;
    }

    public static ServiceClientContainerBuilder NewInstance(bool applyConnectionOptimization = false, params IConnectionCreator[] connectionCreators) => new(applyConnectionOptimization, connectionCreators);

    public void AddNewConnection(IConnectionCreator connectionCreator)
    {
        Guard.Against.Null(connectionCreator);

        _connectionCreators.Add(connectionCreator);
    }

    public ServiceClientContainer Build()
    {
        if (_connectionCreators.Any(p => p.IsError))
        {
            throw new InvalidProgramException($"Unable to build {nameof(ServiceClientContainer)} because of at least one {nameof(IConnectionCreator)} has error state set to true.");
        }
        ServiceClientContainer serviceClientContainer = new();
        foreach (IConnectionCreator connectionCreator in _connectionCreators)
        {
            ServiceClient createdServiceClient = connectionCreator.Create(_applyConnectionOptymization);
            if (!connectionCreator.IsValid)
            {
                throw new InvalidProgramException($"Unable to build {nameof(ServiceClientContainer)} because of at least one {nameof(IConnectionCreator)} encountered error while creating new instance.");
            }
            if (_applyConnectionOptymization)
            {
                _ = createdServiceClient.ApplyConnectionOptimization();
            }
            serviceClientContainer.Connections.Add(createdServiceClient);
        }

        return serviceClientContainer;
    }
}
