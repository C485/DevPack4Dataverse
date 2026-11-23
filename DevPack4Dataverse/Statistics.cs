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
using DevPack4Dataverse.Interfaces;
using DevPack4Dataverse.Models;

namespace DevPack4Dataverse;

internal sealed class Statistics : IStatistics
{
    // shortcut: weights are retained for 2 minutes, the window used by connection selection.
    private const int MaxBucketMinutes = 2;
    private const int BucketsPerMinute = 60;
    private const int BucketCount = MaxBucketMinutes * BucketsPerMinute;
    private const ulong FailurePenalty = 100_000;
    private readonly object _lock = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly long[] _secondByBucket = new long[BucketCount];
    private readonly ulong[] _weightBySecond = new ulong[BucketCount];
    private ulong _totalMilliseconds;

    public Statistics()
    {
        for (int index = 0; index < _secondByBucket.Length; index++)
        {
            _secondByBucket[index] = -1;
        }
    }

    public void Finish(Statistic statistic, bool succeeded = true)
    {
        statistic.SetEnd(_stopwatch.Elapsed);
        lock (_lock)
        {
            _totalMilliseconds += statistic.ElapsedMilliseconds;
            ulong weight = statistic.ElapsedMilliseconds / 10ul;
            if (!succeeded)
            {
                // A fast failure would otherwise look cheaper than a healthy request and
                // keep receiving traffic. The penalty decays with the two-minute window.
                weight += FailurePenalty;
            }

            AddWeight(weight);
        }
    }

    public Statistic StartNew() => new(_stopwatch.Elapsed);

    public ulong TotalUsageInSeconds()
    {
        lock (_lock)
        {
            return _totalMilliseconds / 1000;
        }
    }

    public ulong UsageWeightFromLastMinutes(uint minutes)
    {
        if (minutes == 0)
        {
            return 0;
        }

        lock (_lock)
        {
            long currentSecond = (long)_stopwatch.Elapsed.TotalSeconds;
            long oldestSecond =
                currentSecond - (long)Math.Min(minutes, (uint)MaxBucketMinutes) * BucketsPerMinute;
            ulong total = 0;
            for (int bucket = 0; bucket < BucketCount; bucket++)
            {
                long second = _secondByBucket[bucket];
                if (second >= oldestSecond && second <= currentSecond)
                {
                    total += _weightBySecond[bucket];
                }
            }

            return total;
        }
    }

    private void AddWeight(ulong weight)
    {
        if (weight == 0)
        {
            return;
        }

        long second = (long)_stopwatch.Elapsed.TotalSeconds;
        int bucket = (int)(second % BucketCount);
        if (_secondByBucket[bucket] != second)
        {
            _secondByBucket[bucket] = second;
            _weightBySecond[bucket] = 0;
        }

        _weightBySecond[bucket] += weight;
    }
}
