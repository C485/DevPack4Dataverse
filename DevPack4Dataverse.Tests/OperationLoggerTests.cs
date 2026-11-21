using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DevPack4Dataverse.Tests;

public class OperationLoggerTests
{
    [Fact]
    public async Task RunLoggedAsync_LogsDebugDurationOnSuccess()
    {
        var logger = new RecordingLogger();

        int result = await OperationLogger.RunLoggedAsync(
            logger,
            "test",
            () => Task.FromResult(42)
        );

        Assert.Equal(42, result);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Debug && entry.Message.Contains("finished")
        );
    }

    [Fact]
    public async Task RunLoggedAsync_LogsErrorAndRethrows()
    {
        var logger = new RecordingLogger();
        InvalidOperationException failure = new("boom");

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                OperationLogger.RunLoggedAsync<int>(
                    logger,
                    "test",
                    () => Task.FromException<int>(failure)
                )
        );

        Assert.Same(failure, thrown);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error && ReferenceEquals(entry.Exception, failure)
        );
    }

    [Fact]
    public async Task RunLoggedAsync_DoesNotLogCancellationAsError()
    {
        var logger = new RecordingLogger();
        using CancellationTokenSource source = new();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                OperationLogger.RunLoggedAsync<int>(
                    logger,
                    "test",
                    () => Task.FromCanceled<int>(source.Token)
                )
        );

        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public void RunLogged_LogsErrorAndRethrows()
    {
        var logger = new RecordingLogger();
        InvalidOperationException failure = new("boom");

        Assert.Throws<InvalidOperationException>(
            () => OperationLogger.RunLogged<int>(logger, "test", () => throw failure)
        );

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error && ReferenceEquals(entry.Exception, failure)
        );
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = new();

#if NET11_0_OR_GREATER
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
#else
        public IDisposable BeginScope<TState>(TState state) => null!;
#endif

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);
}
