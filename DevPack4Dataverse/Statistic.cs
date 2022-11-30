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

namespace DevPack4Dataverse;

public class Statistic
{
    private readonly TimeSpan _start;
    private TimeSpan _end;

    public Statistic(TimeSpan startTimeSpan)
    {
        _start = Guard.Against.Zero(startTimeSpan);
    }

    public int ElapsedMinutes => Convert.ToInt32((_end - _start).TotalMinutes);

    public int ElapsedSeconds => Convert.ToInt32((_end - _start).TotalSeconds);

    public bool IsBeetwenTimeSpans(TimeSpan timeSpan)
    {
        return timeSpan >= _start && timeSpan <= _end;
    }

    public void SetEnd(TimeSpan endTimeSpan)
    {
        _end = Guard.Against.Zero(endTimeSpan);
    }
}