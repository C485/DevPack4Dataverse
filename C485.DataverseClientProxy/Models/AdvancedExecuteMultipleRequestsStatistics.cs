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

using System.Diagnostics;

namespace C485.DataverseClientProxy.Models;

public class AdvancedExecuteMultipleRequestsStatistics
{
    public bool Cancelled { get; set; }
    public int RecordsProcessed { get; set; }

    public int RecordsRequested { get; set; }

    public Stopwatch Stopwatch { get; set; }

    public int ThreadsUsed { get; set; }
}