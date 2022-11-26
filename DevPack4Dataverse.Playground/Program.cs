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

using DevPack4Dataverse.Playground.Examples;
using Serilog;
using Serilog.Extensions.Logging;

namespace DevPack4Dataverse.Playground
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information().Enrich.FromLogContext().WriteTo.Console().CreateLogger();
            
            var microsoftLogger = new SerilogLoggerFactory(Log.Logger)
                .CreateLogger("");
            await new AdvancedMultipleRequest(microsoftLogger).Execute();
            Log.CloseAndFlush();
        }
    }
}