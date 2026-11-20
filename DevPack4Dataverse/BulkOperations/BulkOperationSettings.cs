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

using Ardalis.GuardClauses;

namespace DevPack4Dataverse.BulkOperations;

/// <summary>
/// Settings for chunked bulk operation execution.
/// </summary>
public sealed class BulkOperationSettings
{
    /// <summary>
    /// Number of records sent in one bulk request. Defaults to 1,000.
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Number of chunks sent in parallel. Zero uses the number of available connections.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; }

    /// <summary>
    /// How often <see cref="ReportProgress"/> is called while chunks are running.
    /// </summary>
    public TimeSpan ReportProgressInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Optional progress callback; receives processed and total record counts.
    /// </summary>
    public Action<int, int>? ReportProgress { get; set; }

    internal BulkOperationSettings Validate()
    {
        Guard.Against.NegativeOrZero(BatchSize, nameof(BatchSize));
        Guard.Against.Negative(MaxDegreeOfParallelism, nameof(MaxDegreeOfParallelism));
        Guard.Against.NegativeOrZero(ReportProgressInterval, nameof(ReportProgressInterval));
        return this;
    }
}
