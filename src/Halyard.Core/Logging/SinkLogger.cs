using Halyard.Models.Logging;
using Microsoft.Extensions.Logging;

namespace Halyard.Core.Logging;

internal sealed class SinkLogger(string category, IReadOnlyList<ILogSink> sinks, TimeProvider time) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var entry = new LogEntry(time.GetLocalNow(), logLevel, category, formatter(state, exception), exception);
        foreach (var sink in sinks)
        {
            sink.Write(entry);
        }
    }
}
