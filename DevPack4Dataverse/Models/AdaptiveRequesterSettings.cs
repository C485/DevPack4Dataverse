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
using DevPack4Dataverse.Utils;

namespace DevPack4Dataverse.Models;

/// <summary>
/// Represents settings for the <see cref="AdaptiveRequester"/> class.
/// </summary>
public class AdaptiveRequesterSettings
{
    /// <summary>
    /// Gets or sets the minimum number of requests allowed.
    /// </summary>
    public int MinRequests { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of requests allowed.
    /// </summary>
    public int MaxRequests { get; set; }

    /// <summary>
    /// Gets or sets the target response time for requests.
    /// </summary>
    public TimeSpan TargetResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the initial number of requests per second.
    /// </summary>
    public int InitialRequestsPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the buffer size used to store count and response time of previous requests.
    /// </summary>
    public int BufferSize { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveRequesterSettings"/> class with default values.
    /// </summary>
    /// <remarks>
    /// Default values:
    /// <see cref="MinRequests"/> = 1
    /// <see cref="MaxRequests"/> = 300
    /// <see cref="TargetResponseTime"/> = 3 seconds
    /// <see cref="InitialRequestsPerSecond"/> = 5
    /// <see cref="BufferSize"/> = 100
    /// </remarks>
    public AdaptiveRequesterSettings()
    {
        MinRequests = 1;
        MaxRequests = 300;
        TargetResponseTime = TimeSpan.FromSeconds(3);
        InitialRequestsPerSecond = 5;
        BufferSize = 100;
    }

    /// <summary>
    /// Validates the <see cref="AdaptiveRequesterSettings"/> and returns the instance if valid.
    /// </summary>
    /// <returns>The validated instance of <see cref="AdaptiveRequesterSettings"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any of the property values are out of the allowed range.</exception>
    public AdaptiveRequesterSettings Validate()
    {
        Guard.Against.NegativeOrZero(MinRequests);
        Guard.Against.OutOfRange(MaxRequests, nameof(MaxRequests), MinRequests, 500);
        Guard.Against.NegativeOrZero(TargetResponseTime);
        TimeSpan maxResponseTime = TimeSpan.FromMinutes(5);
        Guard.Against.OutOfRange(TargetResponseTime, nameof(TargetResponseTime), TimeSpan.Zero, maxResponseTime);
        Guard.Against.NegativeOrZero(InitialRequestsPerSecond);
        Guard.Against.OutOfRange(InitialRequestsPerSecond, nameof(InitialRequestsPerSecond), MinRequests, MaxRequests);
        Guard.Against.NegativeOrZero(BufferSize);
        return this;
    }
}
