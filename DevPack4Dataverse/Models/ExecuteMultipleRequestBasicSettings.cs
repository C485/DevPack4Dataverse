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

using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.Models;

public class ExecuteMultipleRequestBasicSettings
{
    /// <summary>
    ///  <para>Optional.</para>
    ///  <para>Callback used for reporting error.</para>
    ///  <para>
    ///   Called each time when error occurs, please have in mind that thread may vary between errors.
    ///  </para>
    /// </summary>
    public Action<OrganizationRequest, string> ErrorReport { get; set; } = (_, _) => { };

    /// <summary>
    ///  <para>Optional.</para>
    ///  <para>Represents a number of record packs which size is defined by <see cref="AdaptiveRequesterSettings" />.</para>
    ///  <para>This number should be equal or less to amount of connections in <see cref="DataverseDevPack" />.</para>
    ///  <para>By default it's set to automatic, will always try to use all connections.</para>
    ///  <para>When, concurrent settings for connection are ignored in automatic mode - for example</para>
    ///  <para> in scenario when you've 4 connections with 4 concurrent setting, maximum threads used will be 4.</para>
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = -1;

    /// <summary>
    ///  <para>Optional.</para>
    ///  <para>Callback used for reporting progress.</para>
    ///  <para>It's executed every <see cref="ReportProgressInterval" /> from separate thread.</para>
    ///  <para>Any access to objects from other threads needs to be atomic/locked.</para>
    /// </summary>
    public Action<int, int> ReportProgress { get; set; } = (_, _) => { };

    /// <summary>
    ///  <para>Optional.</para>
    ///  <para>Sets sleep interval for thread that is used for reporting progress.</para>
    ///  <para>Defaults to 1 second.</para>
    ///  <para>See <see cref="ReportProgress" /> callback.</para>
    /// </summary>
    public TimeSpan ReportProgressInterval { get; set; } = TimeSpan.FromSeconds(1);
    public int RequestSize { get; set; } = 20;
}
