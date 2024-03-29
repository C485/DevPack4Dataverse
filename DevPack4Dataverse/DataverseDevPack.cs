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
using DevPack4Dataverse.FieldMethods;
using DevPack4Dataverse.Interfaces;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse;

public sealed class DataverseDevPack
{
    public readonly ExecuteMultipleLogic ExecuteMultiple;
    public readonly FieldDrill FieldDrill;
    public readonly FieldMethodsPack FieldMethods;
    public readonly SdkProxy SdkProxy;
    private readonly ILogger _logger;

    public DataverseDevPack(
        ILogger logger,
        bool applyConnectionOptimalization = true,
        params IConnectionCreator[] connectionCreators
    )
    {
        using EntryExitLogger logGuard = new(logger);
        SdkProxy = new SdkProxy(logger, applyConnectionOptimalization, connectionCreators);
        ExecuteMultiple = new ExecuteMultipleLogic(SdkProxy, logger);
        FieldDrill = new FieldDrill(SdkProxy, logger);
        FieldMethods = new FieldMethodsPack(SdkProxy, ExecuteMultiple, logger);
        _logger = logger;
    }

    public ILinqExpressionBuilder<T> CreateLinqExpressionBuilder<T>() where T : Entity, new() =>
        LinqExpressionBuilder.Create<T>(_logger);
}
