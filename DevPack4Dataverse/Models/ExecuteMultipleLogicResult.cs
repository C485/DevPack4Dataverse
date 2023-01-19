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
using System.Diagnostics;
using System.Text;

namespace DevPack4Dataverse.Models;

public class ExecuteMultipleLogicResult
{
    public bool Cancelled { get; set; }
    public int RecordsProcessed { get; set; }

    public int RecordsRequested { get; set; }

    public IReadOnlyCollection<ExecuteMultipleResponseItem> Results { get; set; } =
        Array.Empty<ExecuteMultipleResponseItem>();
    public Stopwatch Stopwatch { get; set; }

    public int ThreadsUsed { get; set; }

    public override string ToString()
    {
        StringBuilder sb = new();
        sb.AppendLine("Statistics:");
        sb.Append(nameof(Cancelled));
        sb.Append(": ");
        sb.Append(Cancelled);
        sb.AppendLine();
        sb.Append(nameof(RecordsProcessed));
        sb.Append(": ");
        sb.Append(RecordsProcessed);
        sb.AppendLine();
        sb.Append(nameof(RecordsRequested));
        sb.Append(": ");
        sb.Append(RecordsRequested);
        sb.AppendLine();
        sb.Append(nameof(Stopwatch));
        sb.Append(": ");
        sb.Append(Stopwatch.Elapsed);
        sb.AppendLine();
        sb.Append(nameof(ThreadsUsed));
        sb.Append(": ");
        sb.Append(ThreadsUsed);
        sb.AppendLine();
        sb.Append("Results count: ");
        sb.Append(Results.Count);
        sb.AppendLine();
        return sb.ToString();
    }
}
