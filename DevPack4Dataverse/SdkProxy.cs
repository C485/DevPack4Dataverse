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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security;
using DevPack4Dataverse.New.ExecuteMultiple;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

//namespace DevPack4Dataverse;
//
//public sealed class SdkProxy : IDisposable
//{
//
//
//
//    private static void OptimalizeConnections()
//    {
//        ServicePointManager.DefaultConnectionLimit = 65000;
//        ThreadPool.SetMinThreads(100, 100);
//        ServicePointManager.Expect100Continue = false;
//        ServicePointManager.UseNagleAlgorithm = false;
//    }
//}
