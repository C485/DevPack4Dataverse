using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;

namespace DevPack4Dataverse;

public sealed class GlobalLogger : ILogger
{
    private static readonly object s_lockObject = new();
    private bool _consoleEnabled;
    private string? _logFilePath;
    private LogLevel _minimumLogLevel = LogLevel.Information;
    private readonly ConcurrentBag<ILogger> _loggers = [];

    static GlobalLogger() { }

    private GlobalLogger() { }

    public GlobalLogger SetMinimumLogLevel(LogLevel minimumLogLevel)
    {
        _minimumLogLevel = minimumLogLevel;
        return this;
    }

    public GlobalLogger AddLogger(ILogger logger)
    {
        _loggers.Add(Guard.Against.Null(logger));
        return this;
    }

    public GlobalLogger SetLogFilePath(string logFilePath)
    {
        _logFilePath = logFilePath;
        return this;
    }

    public GlobalLogger EnableConsole()
    {
        _consoleEnabled = true;
        return this;
    }

    public static GlobalLogger Instance { get; } = new GlobalLogger();

    public IDisposable? BeginScope<TState>(TState state)
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _minimumLogLevel;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter
    )
    {
        foreach (ILogger logger in _loggers)
        {
            logger.Log(logLevel, eventId, state, exception, formatter);
        }
        if (!IsEnabled(logLevel))
        {
            return;
        }

        if (_consoleEnabled || !string.IsNullOrEmpty(_logFilePath))
        {
            lock (s_lockObject)
            {
                string logEntry = formatter(state, exception);
                if (_consoleEnabled)
                {
                    Console.WriteLine(logEntry);
                }
                if (!string.IsNullOrEmpty(_logFilePath))
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
            }
        }
    }
}
