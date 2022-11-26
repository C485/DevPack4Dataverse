﻿/*
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

using DevPack4Dataverse.ExecuteMultiple;
using DevPack4Dataverse.ExpressionBuilder;
using DevPack4Dataverse.Interfaces;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse;

public sealed class DataverseDevPack : IDataverseDevPack
{
    public readonly ExecuteMultipleLogic ExecuteMultiple;
    public readonly SdkProxy SdkProxy;

    public DataverseDevPack(ILogger logger, params IConnectionCreator[] connectionCreators)
    {
        using EntryExitLogger logGuard = new(logger);
        SdkProxy = new SdkProxy(logger, connectionCreators);
        ExecuteMultiple = new ExecuteMultipleLogic(SdkProxy, logger);
    }

    public static ILinqExpressionBuilder<T> CreateLinqExpressionBuilder<T>() where T : Entity => LinqExpressionBuilder.Create<T>();
}