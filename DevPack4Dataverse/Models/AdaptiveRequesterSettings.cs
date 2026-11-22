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
using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.Models;

/// <summary>
/// Settings for adaptive ExecuteMultiple processing.
/// </summary>
/// <remarks>
/// The requester starts with <see cref="InitialBatchSize"/> records per ExecuteMultiple request
/// and <see cref="InitialDegreeOfParallelism"/> parallel requests. Every
/// <see cref="EvaluationRecordCount"/> records it compares the wave duration with
/// <see cref="TargetBatchDuration"/>. Slow waves decrease the batch size first, then the
/// degree of parallelism. Fast waves increase the batch size first, then the degree.
/// </remarks>
public sealed class AdaptiveRequesterSettings
{
    public int MinBatchSize { get; set; } = 1;

    public int MaxBatchSize { get; set; } = 1000;

    public int InitialBatchSize { get; set; } = 4;

    public int MinDegreeOfParallelism { get; set; } = 1;

    public int MaxDegreeOfParallelism { get; set; } = 16;

    public int InitialDegreeOfParallelism { get; set; } = 4;

    /// <summary>
    /// Target duration of one adaptive wave. This is not a target for the whole run; a run of
    /// N records takes roughly N / (batch size × degree of parallelism) waves.
    /// </summary>
    public TimeSpan TargetBatchDuration { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Number of records processed before the next adjustment.</summary>
    public int EvaluationRecordCount { get; set; } = 40;

    /// <summary>
    /// Degree of parallelism selected by the adaptive run. It is updated as the run adjusts and
    /// can be read from <see cref="ReportProgress"/> while the run is in progress.
    /// </summary>
    public int CurrentDegreeOfParallelism { get; internal set; }

    /// <summary>
    /// Batch size selected by the adaptive run. It is updated as the run adjusts and can be read
    /// from <see cref="ReportProgress"/> while the run is in progress.
    /// </summary>
    public int CurrentBatchSize { get; internal set; }

    /// <summary>Optional callback for failed requests.</summary>
    public Action<OrganizationRequest, string> ErrorReport { get; set; } = (_, _) => { };

    /// <summary>Optional callback reporting processed and total request counts.</summary>
    public Action<int, int> ReportProgress { get; set; } = (_, _) => { };

    public AdaptiveRequesterSettings Validate()
    {
        Guard.Against.NegativeOrZero(MinBatchSize, nameof(MinBatchSize));
        Guard.Against.OutOfRange(MaxBatchSize, nameof(MaxBatchSize), MinBatchSize, 1000);
        Guard.Against.OutOfRange(
            InitialBatchSize,
            nameof(InitialBatchSize),
            MinBatchSize,
            MaxBatchSize
        );
        Guard.Against.NegativeOrZero(MinDegreeOfParallelism, nameof(MinDegreeOfParallelism));
        Guard.Against.OutOfRange(
            MaxDegreeOfParallelism,
            nameof(MaxDegreeOfParallelism),
            MinDegreeOfParallelism,
            500
        );
        Guard.Against.OutOfRange(
            InitialDegreeOfParallelism,
            nameof(InitialDegreeOfParallelism),
            MinDegreeOfParallelism,
            MaxDegreeOfParallelism
        );
        Guard.Against.NegativeOrZero(TargetBatchDuration, nameof(TargetBatchDuration));
        Guard.Against.NegativeOrZero(EvaluationRecordCount, nameof(EvaluationRecordCount));

        return this;
    }
}
