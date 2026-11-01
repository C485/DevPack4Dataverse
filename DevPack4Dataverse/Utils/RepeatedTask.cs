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
    private readonly ILogger _logger;
#if NET6_0_OR_GREATER
    private readonly PeriodicTimer _periodicTimer;
#else
    private readonly TimeSpan _timeSpan;
#endif
    private readonly CancellationTokenSource _cancellationTokenSource;
    private Task _task;

    public RepeatedTask(TimeSpan timeSpan, Action action, ILogger logger)
    {
        using EntryExitLogger logGuard = new(logger);
        _logger = Guard.Against.Null(logger);
        _action = Guard.Against.Null(action);
        _cancellationTokenSource = new CancellationTokenSource();
#if NET6_0_OR_GREATER
        _periodicTimer = new PeriodicTimer(timeSpan);
#else
        _timeSpan = timeSpan;
#endif
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

        _cancellationTokenSource.Cancel();
        await _task.ConfigureAwait(false);
        _cancellationTokenSource.Dispose();
    }

    private async Task TickAsync()
    {
        using EntryExitLogger logGuard = new(_logger);

        try
        {
            while (true)
            {
#if NET6_0_OR_GREATER
                if (!await _periodicTimer
                    .WaitForNextTickAsync(_cancellationTokenSource.Token)
                    .ConfigureAwait(false))
                {
                    break;
                }
#else
                await Task.Delay(_timeSpan, _cancellationTokenSource.Token).ConfigureAwait(false);
#endif
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
        catch (OperationCanceledException) { }
    }
}
