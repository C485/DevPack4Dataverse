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

using DevPack4Dataverse.ExecuteMultiple;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;

namespace DevPack4Dataverse.FieldMethods;

public sealed class FieldMethodsPack
{
    public readonly OptionSetFieldTypeMethods ChoiceFieldTypeMethods;
    public readonly FileOrImageFieldTypeMethods FileOrImageFieldTypeMethods;

    public FieldMethodsPack(SdkProxy sdkProxy, ExecuteMultipleLogic executeMultipleLogic, ILogger logger)
    {
        using EntryExitLogger logGuard = new(logger);
        ChoiceFieldTypeMethods = new OptionSetFieldTypeMethods(sdkProxy, logger);
        FileOrImageFieldTypeMethods = new FileOrImageFieldTypeMethods(sdkProxy, executeMultipleLogic, logger);
    }
}
