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

namespace DevPack4Dataverse;

public class Statistics
{
    private readonly object _lock = new();
    private readonly List<Statistic> _statistics = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

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

    public long UsageWeightFromLastXMinutes(uint minutes)
    {
        lock (_lock)
        {
            if (minutes == 0)
            {
                return 0;
            }
            long totalSeconds = 0;
            uint skippedStatistics = 0;
            TimeSpan minimalTimeSpan = _stopwatch.Elapsed - TimeSpan.FromMinutes(minutes);
            for (int i = _statistics.Count - 1; i >= 0; i--)
            {
                if (skippedStatistics >= 100)
                {
                    break;
                }

                Statistic statistic = _statistics[i];
                if (!statistic.IsBeetwenTimeSpans(minimalTimeSpan))
                {
                    skippedStatistics++;
                    continue;
                }
                totalSeconds += statistic.ElapsedSeconds;
            }
            return totalSeconds;
        }
    }
}