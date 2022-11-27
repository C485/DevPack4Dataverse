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
using DevPack4Dataverse.Interfaces;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Client;

namespace DevPack4Dataverse;

public sealed class ConnectionLease : IDisposable
{
    private bool _disposedValue;

    public ConnectionLease(IConnection connection)
    {
        Connection = connection;
        Guard
           .Against
           .Null(connection);
    }

    ~ConnectionLease()
    {
        Dispose(false);
    }

    public IConnection Connection { get; }

    public ServiceClient PureServiceClient => Connection.PureServiceClient;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public OrganizationServiceContext GetOrganizationServiceContext() => new(PureServiceClient);

    public T GetOrganizationServiceContext<T>() where T : OrganizationServiceContext
    {
        return (T)Activator.CreateInstance(typeof(T), PureServiceClient);
    }

    private void Dispose(bool disposing)
    {
        if (_disposedValue)
        {
            return;
        }

        Connection
           .ReleaseLock();

        _disposedValue = true;
    }
}