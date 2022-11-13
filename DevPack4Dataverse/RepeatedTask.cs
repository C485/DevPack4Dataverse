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

namespace DevPack4Dataverse;

public sealed class RepeatedTask
{
    private readonly Action _action;
    private readonly ILogger _logger;
    private readonly PeriodicTimer _periodicTimer;
    private readonly CancellationTokenSource cancellationTokenSource;
    private Task _task;

    public RepeatedTask(TimeSpan timeSpan, Action action, ILogger logger)
    {
        using EntryExitLogger logGuard = new(logger);
        _logger = Guard.Against.Null(logger);
        _action = Guard.Against.Null(action);
        cancellationTokenSource = new CancellationTokenSource();
        _periodicTimer = new PeriodicTimer(timeSpan);
    }

    public void Start()
    {
        using EntryExitLogger logGuard = new(_logger);

        _task = TickAsync();
    }

    public async Task StopAsync()
    {
        using EntryExitLogger logGuard = new(_logger);

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
        using EntryExitLogger logGuard = new(_logger);

        try
        {
            while (await _periodicTimer.WaitForNextTickAsync(cancellationTokenSource.Token))
            {
                try
                {
                    using EntryExitLogger logGuardInner = new(_logger, caller: $"{nameof(TickAsync)}-InnerFunction");
                    _action();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Action execution failed in {ClassName}", nameof(RepeatedTask));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}