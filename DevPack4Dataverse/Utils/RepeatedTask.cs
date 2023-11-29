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
using Microsoft.Extensions.Logging;

namespace DevPack4Dataverse.Utils;

public sealed class RepeatedTask
{
    private readonly Action _action;
    private readonly PeriodicTimer _periodicTimer;
    private readonly CancellationTokenSource cancellationTokenSource;
    private Task _task;

    public RepeatedTask(TimeSpan timeSpan, Action action)
    {
        _action = Guard.Against.Null(action);
        cancellationTokenSource = new CancellationTokenSource();
        _periodicTimer = new PeriodicTimer(timeSpan);
    }

    public void Start()
    {
        _task = TickAsync();
    }

    public async Task StopAsync()
    {
        if (_task == null)
        {
            return;
        }

        cancellationTokenSource.Cancel();
        await _task;
        cancellationTokenSource.Dispose();
    }

    private async Task TickAsync()
    {
        try
        {
            while (await _periodicTimer.WaitForNextTickAsync(cancellationTokenSource.Token))
            {
                try
                {
                    _action();
                }
                catch (Exception e) { }
            }
        }
        catch (OperationCanceledException) { }
    }
}
