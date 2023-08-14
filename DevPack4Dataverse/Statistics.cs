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

internal class Statistics : IStatistics
{
    private readonly object _lock = new();
    private readonly List<Statistic> _statistics = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public ulong TotalUsageInSeconds()
    {
        lock (_lock)
        {
            return _statistics.Aggregate<Statistic, ulong>(0, (current, item) => current + item.ElapsedSeconds);
        }
    }

    public void Finish(Statistic statistic)
    {
        statistic.SetEnd(_stopwatch.Elapsed);
    }

    public Statistic StartNew()
    {
        lock (_lock)
        {
            Statistic statsEntry = new(_stopwatch.Elapsed);
            _statistics.Add(statsEntry);

            return statsEntry;
        }
    }

    public ulong UsageWeightFromLastMinutes(uint minutes)
    {
        lock (_lock)
        {
            if (minutes == 0)
            {
                return 0;
            }

            ulong totalSeconds = 0;
            uint skippedStatistics = 0;

            for (int i = _statistics.Count - 1; i >= 0; i--)
            {
                if (skippedStatistics >= 100)
                {
                    break;
                }

                Statistic statistic = _statistics[i];

                if (!statistic.IsFromLastNMinutes(_stopwatch.Elapsed, minutes))
                {
                    skippedStatistics++;

                    continue;
                }

                totalSeconds += statistic.ElapsedMilliseconds / 10ul;
            }

            return totalSeconds;
        }
    }
}
