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

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Logging;

namespace DevPack4Dataverse.Interfaces;

public interface IConnectionCreator
{
    /// <summary>
    ///
    /// </summary>
    bool IsCreated { get; }

    /// <summary>
    ///
    /// </summary>
    bool IsError { get; }

    /// <summary>
    ///
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// Number of connections this creator produces in one <see cref="Create"/> call.
    /// </summary>
    int ConnectionCount { get; }

    /// <summary>
    ///  <para>
    ///   Creates one or more <see cref="Connection"/> objects. The first connection wraps the
    ///   validated client; the rest are clones of it.
    ///  </para>
    ///  <para>
    ///   The first connection is checked with <see cref="WhoAmIRequest"/> before any clone is made.
    ///  </para>
    /// </summary>
    /// <returns>Connections created from the validated client.</returns>
    IReadOnlyList<IConnection> Create(ILogger logger);
}
