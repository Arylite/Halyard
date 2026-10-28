using Halyard.Models.Logging;

namespace Halyard.Core.Logging;

/// <summary>A destination for log entries. Implementations must not block the caller.</summary>
public interface ILogSink
{
    void Write(LogEntry entry);
}
