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

namespace DevPack4Dataverse.Models;

public class Statistic(TimeSpan startTimeSpan)
{
    private TimeSpan? _end;

    public ulong ElapsedMilliseconds =>
        _end.HasValue ? Convert.ToUInt64((_end.Value - startTimeSpan).TotalMilliseconds) : 0;
    public ulong ElapsedMinutes => _end.HasValue ? Convert.ToUInt64((_end.Value - startTimeSpan).TotalMinutes) : 0;
    public ulong ElapsedSeconds => _end.HasValue ? Convert.ToUInt64((_end.Value - startTimeSpan).TotalSeconds) : 0;

    public bool IsFromLastNMinutes(TimeSpan currentTimeSpan, uint minutes)
    {
        if (ElapsedMilliseconds == 0)
        {
            return false;
        }
        TimeSpan minimalTimeSpan = currentTimeSpan - TimeSpan.FromMinutes(minutes);
        return _end >= minimalTimeSpan && _end <= currentTimeSpan;
    }

    public void SetEnd(TimeSpan endTimeSpan)
    {
        _end = endTimeSpan;
    }
}
