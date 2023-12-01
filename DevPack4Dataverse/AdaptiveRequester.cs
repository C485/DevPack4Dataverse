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
using DevPack4Dataverse.Models;

namespace DevPack4Dataverse;

/// <summary>
///     Represents an adaptive request executor that dynamically adjusts the number of requests based on response times.
/// </summary>
public class AdaptiveRequester
{
    private readonly AdaptiveRequesterSettings _settings;
    private readonly AdaptiveRequesterSharedState _sharedState;
    private readonly ReaderWriterLockSlim _sharedStateLock = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="AdaptiveRequester" /> class with the specified settings.
    /// </summary>
    /// <param name="settings">The settings to use for the adaptive requester.</param>
    public AdaptiveRequester(AdaptiveRequesterSettings settings)
    {
        _settings = settings.Validate();
        int initialRequestCount = Math.Clamp(
            Convert.ToInt32(settings.InitialRequestsPerSecond * settings.TargetResponseTime.TotalMilliseconds / 1000),
            settings.MinRequests,
            settings.MaxRequests
        );

        _sharedState = new AdaptiveRequesterSharedState
        {
            BufferSize = _settings.BufferSize,
            ResponseTimeBuffer = new TimeSpan[_settings.BufferSize],
            RequestCountBuffer = new int[_settings.BufferSize],
            BufferIndex = 0
        };

        _sharedState.ResponseTimeBuffer[0] = settings.TargetResponseTime;
        _sharedState.RequestCountBuffer[0] = initialRequestCount;
    }

    /// <summary>
    ///     Executes the specified work function asynchronously, using the dynamically calculated number of requests.
    /// </summary>
    /// <param name="work">
    ///     The work function to execute, which takes an integer representing the number of requests and returns
    ///     a task with the actual number of completed requests.
    /// </param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<bool> ExecuteAsync(Func<int, Task<int>> work)
    {
        int requestCount;

        _sharedStateLock.EnterReadLock();

        try
        {
            requestCount = CalculateRequestCount();
        }
        finally
        {
            _sharedStateLock.ExitReadLock();
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        int actualRequestCount = 0;

        try
        {
            actualRequestCount = await work(requestCount);
        }
        finally
        {
            stopwatch.Stop();

            TimeSpan responseTime = stopwatch.Elapsed;

            _sharedStateLock.EnterWriteLock();

            try
            {
                _sharedState.BufferIndex = (_sharedState.BufferIndex + 1) % _sharedState.BufferSize;
                _sharedState.ResponseTimeBuffer[_sharedState.BufferIndex] = responseTime;
                _sharedState.RequestCountBuffer[_sharedState.BufferIndex] = actualRequestCount;
            }
            finally
            {
                _sharedStateLock.ExitWriteLock();
            }
        }
        return actualRequestCount != 0;
    }

    /// <summary>
    ///     Calculates the number of requests to execute based on the exponential moving average (EMA) of response times and
    ///     request counts, taking into account dynamic weights.
    /// </summary>
    /// <returns>The calculated number of requests to execute.</returns>
    private int CalculateRequestCount()
    {
        TimeSpan targetResponseTime = _settings.TargetResponseTime;
        (_, _, double requestsPerSecond) = _sharedState.CalculateEmaWithDynamicWeight();
        double multiplier = targetResponseTime.TotalMilliseconds;
        double possibleRequestsPerSecond = requestsPerSecond * multiplier / 1000;

        return Convert.ToInt32(
            Math.Ceiling(Math.Clamp(possibleRequestsPerSecond, _settings.MinRequests, _settings.MaxRequests))
        );
    }
}
