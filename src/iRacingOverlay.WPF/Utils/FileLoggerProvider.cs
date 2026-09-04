using System;
using Microsoft.Extensions.Logging;

namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Routes Microsoft.Extensions.Logging output into <see cref="AppLog"/>, so the
/// existing ILogger calls throughout Core and the services end up in the same
/// file the user can send back with a bug report.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName);

    public void Dispose() { }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;

        public FileLogger(string category)
        {
            // Trim the namespace — "IRacingTelemetryService" reads better than the full name.
            int lastDot = category.LastIndexOf('.');
            _category = lastDot >= 0 && lastDot < category.Length - 1
                ? category[(lastDot + 1)..]
                : category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                                Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = $"{_category}: {formatter(state, exception)}";

            switch (logLevel)
            {
                case LogLevel.Critical:
                case LogLevel.Error:
                    AppLog.Error(message, exception);
                    break;
                case LogLevel.Warning:
                    AppLog.Warn(message, exception);
                    break;
                default:
                    AppLog.Info(message);
                    break;
            }
        }
    }
}
