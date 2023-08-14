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

using CommunityToolkit.Diagnostics;

namespace DevPack4Dataverse.Utils;

public sealed class RepeatedTask
{
    private readonly Action _action;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly PeriodicTimer _periodicTimer;
    private Task _task;

    public RepeatedTask(TimeSpan timeSpan, Action action)
    {
        Guard.IsNotNull(action);
        _action = action;
        _cancellationTokenSource = new CancellationTokenSource();
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

        _cancellationTokenSource.Cancel();
        await _task;
        _cancellationTokenSource.Dispose();
    }

    private async Task TickAsync()
    {
        try
        {
            while (await _periodicTimer.WaitForNextTickAsync(_cancellationTokenSource.Token))
            {
                _action();
            }
        }
        catch (OperationCanceledException) { }
    }
}
