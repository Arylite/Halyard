using Halyard.Models.Logging;
using Microsoft.Extensions.Logging;

namespace Halyard.Core.Logging;

public static class LogEntryFormatter
{
    public static string ShortLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "---",
    };

    /// <summary>Strips the namespace: <c>Halyard.Core.Downloads.DownloadQueue</c> becomes <c>DownloadQueue</c>.</summary>
    public static string ShortCategory(string category) => category[(category.LastIndexOf('.') + 1)..];

    public static string Format(LogEntry entry)
    {
        var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{ShortLevel(entry.Level)}] {ShortCategory(entry.Category)}: {entry.Message}";
        return entry.Exception is null ? line : $"{line}{Environment.NewLine}{entry.Exception}";
    }
}
