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

namespace DevPack4Dataverse.Creators;

/// <summary>
/// Bounded retry state for one connection creator. A transient failure is retried after a
/// short backoff; a creator is marked failed only after the configured number of attempts.
/// </summary>
internal sealed class ConnectionCreationRetry
{
    private readonly TimeSpan _baseDelay;
    private readonly int _maxAttempts;
    private int _attempts;
    private DateTime _retryAtUtc;

    public ConnectionCreationRetry(int maxAttempts, TimeSpan baseDelay)
    {
        _maxAttempts = Guard.Against.NegativeOrZero(maxAttempts);
        _baseDelay = Guard.Against.Negative(baseDelay);
    }

    public bool CanCreateNow => DateTime.UtcNow >= _retryAtUtc;

    public bool FailedPermanently { get; private set; }

    public void RecordFailure()
    {
        _attempts++;
        if (_attempts >= _maxAttempts)
        {
            FailedPermanently = true;
            return;
        }

        _retryAtUtc = DateTime.UtcNow.Add(
            TimeSpan.FromTicks(_baseDelay.Ticks * _attempts)
        );
    }

    public void RecordSuccess()
    {
        _attempts = 0;
        _retryAtUtc = default;
    }
}
