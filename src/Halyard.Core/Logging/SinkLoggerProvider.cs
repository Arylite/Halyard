using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Halyard.Core.Logging;

/// <summary>Bridges <see cref="ILogger"/> to every registered <see cref="ILogSink"/>.</summary>
[ProviderAlias("Halyard")]
public sealed class SinkLoggerProvider(IEnumerable<ILogSink> sinks, TimeProvider time) : ILoggerProvider
{
    private readonly IReadOnlyList<ILogSink> _sinks = [.. sinks];
    private readonly ConcurrentDictionary<string, SinkLogger> _loggers = new();

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new SinkLogger(name, _sinks, time));

    public void Dispose() => _loggers.Clear();
}
