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
using Microsoft.PowerPlatform.Dataverse.Client;

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
    ///  <para>
    ///   In this method class which implements this interface should create
    ///   <see
    ///    cref="Connection" />
    ///   object.
    ///  </para>
    ///  <para>
    ///   Class should also check if connection is active, while adding connection
    ///   <see
    ///    cref="WhoAmIRequest" />
    ///   is executed to finally confirm that connection was established.
    ///  </para>
    /// </summary>
    /// <returns><see cref="IConnection" /> as instance of <see cref="Connection" /></returns>
    ServiceClient Create(bool applyConnectionOptimization);
}
