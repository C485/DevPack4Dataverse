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

namespace DevPack4Dataverse;

public class Statistics
{
    private readonly Dictionary<int, Statistic> _Statistics = new();

    public void Increase()
    {
        var key = StatisticKeyLogic.GetKey();
        if (_Statistics.TryGetValue(key, out Statistic value))
        {
            value.Increase();
            return;
        }
        _Statistics[key] = new Statistic(1);
    }

    private static class StatisticKeyLogic
    {
        private static readonly DateTime startingDate = new(2000, 1, 1);

        public static int GetKey()
        {
            return (int)DateTime.Now.Subtract(startingDate).TotalMinutes;
        }

        public static DateTime RestoreDateTime(int key)
        {
            return startingDate.AddMinutes(key);
        }
    }
}