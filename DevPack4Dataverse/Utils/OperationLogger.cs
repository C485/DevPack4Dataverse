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
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;

namespace DevPack4Dataverse.Utils;

/// <summary>
/// Logs the outcome and duration of a batch operation. Unlike a method-boundary guard,
/// this helper awaits the whole async operation, so the elapsed time is the real duration.
/// </summary>
internal static class OperationLogger
{
    public static async Task<T> RunLoggedAsync<T>(
        ILogger logger,
        string operation,
        Func<Task<T>> action
    )
    {
        Guard.Against.Null(logger);
        Guard.Against.NullOrWhiteSpace(operation);
        Guard.Against.Null(action);

        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            T result = await action().ConfigureAwait(false);
            stopwatch.Stop();
            logger.LogDebug(
                "{LibName}: {Operation} finished in {Elapsed}.",
                nameof(DevPack4Dataverse),
                operation,
                stopwatch.Elapsed
            );
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            logger.LogError(
                exception,
                "{LibName}: {Operation} failed after {Elapsed}.",
                nameof(DevPack4Dataverse),
                operation,
                stopwatch.Elapsed
            );
            throw;
        }
    }

    public static T RunLogged<T>(ILogger logger, string operation, Func<T> action)
    {
        Guard.Against.Null(logger);
        Guard.Against.NullOrWhiteSpace(operation);
        Guard.Against.Null(action);

        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            T result = action();
            stopwatch.Stop();
            logger.LogDebug(
                "{LibName}: {Operation} finished in {Elapsed}.",
                nameof(DevPack4Dataverse),
                operation,
                stopwatch.Elapsed
            );
            return result;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            logger.LogError(
                exception,
                "{LibName}: {Operation} failed after {Elapsed}.",
                nameof(DevPack4Dataverse),
                operation,
                stopwatch.Elapsed
            );
            throw;
        }
    }
}
