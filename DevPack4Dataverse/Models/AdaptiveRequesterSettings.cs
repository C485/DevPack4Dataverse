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
/// and uses <see cref="DegreeOfParallelism"/> concurrent requests. Every
/// <see cref="EvaluationRecordCount"/> records it compares the wave duration with
/// <see cref="TargetBatchDuration"/>. Slow waves scale the batch size down toward the target;
/// fast waves grow it. The degree of parallelism is fixed for the run and defaults to the
/// connection pool size, so the batch size only controls the duration of one request.
/// </remarks>
public sealed class AdaptiveRequesterSettings
{
    public int MinBatchSize { get; set; } = 1;

    public int MaxBatchSize { get; set; } = 1000;

    public int InitialBatchSize { get; set; } = 4;

    /// <summary>
    /// Number of concurrent ExecuteMultiple requests. When null, the connection pool size is
    /// used. The value is capped at the pool size. This is fixed for the whole run; only the
    /// batch size adapts.
    /// </summary>
    public int? DegreeOfParallelism { get; set; }

    /// <summary>
    /// Target duration of one adaptive wave. This is not a target for the whole run; a run of
    /// N records takes roughly N / (batch size × degree of parallelism) waves.
    /// </summary>
    public TimeSpan TargetBatchDuration { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Number of records processed before the next adjustment.</summary>
    public int EvaluationRecordCount { get; set; } = 40;

    /// <summary>
    /// Degree of parallelism used by the adaptive run. It is fixed for the run and can be read
    /// from <see cref="ReportProgress"/> while the run is in progress.
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
        if (DegreeOfParallelism is { } degreeOfParallelism)
        {
            Guard.Against.NegativeOrZero(degreeOfParallelism, nameof(DegreeOfParallelism));
        }

        Guard.Against.NegativeOrZero(TargetBatchDuration, nameof(TargetBatchDuration));
        Guard.Against.NegativeOrZero(EvaluationRecordCount, nameof(EvaluationRecordCount));

        return this;
    }
}
