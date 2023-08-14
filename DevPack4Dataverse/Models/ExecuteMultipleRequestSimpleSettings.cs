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

using Microsoft.Xrm.Sdk.Messages;

namespace DevPack4Dataverse.Models;

public class ExecuteMultipleRequestSimpleSettings
{
    /// <summary>
    ///     <para>Optional.</para>
    ///     <para>Represents a number of record packs which size is defined by <see cref="RequestSize" />.</para>
    ///     <para>This number should be equal or less to amount of connections in <see cref="DataverseDevPack" />.</para>
    ///     <para>By default it's set to automatic, will always try to use all connections.</para>
    ///     <para>When, concurrent settings for connection are ignored in automatic mode - for example</para>
    ///     <para> in scenario when you've 4 connections with 4 concurrent setting, maximum threads used will be 4.</para>
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = -1;

    /// <summary>
    ///     <para>Optional.</para>
    ///     <para>
    ///         Represents a number of records that will be send to Dataverse in one <see cref="ExecuteMultipleRequest" />
    ///     </para>
    ///     <para>By default this number is set to 6, which in benchmarks gave best performance.</para>
    ///     <para>See more information at project site.</para>
    /// </summary>
    public int RequestSize { get; set; } = 6;
}
