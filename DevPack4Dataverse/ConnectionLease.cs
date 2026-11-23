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

namespace DevPack4Dataverse;

public sealed class ConnectionLease : IDisposable
{
    private int _disposedValue;

    internal ConnectionLease(IConnection connection)
    {
        Connection = Guard.Against.Null(connection);
    }

    public IConnection Connection { get; }

    public ServiceClient PureServiceClient => Connection.PureServiceClient;

    public void Dispose()
    {
        DisposeConnection();
    }

    private void DisposeConnection()
    {
        if (Interlocked.Exchange(ref _disposedValue, 1) != 0)
        {
            return;
        }

        Connection.ReleaseLock();
    }
}
