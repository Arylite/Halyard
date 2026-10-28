using System.Globalization;
using Halyard.Core.Logging;
using Halyard.Models.Logging;
using Microsoft.Extensions.Logging;

namespace Halyard.ViewModels;

/// <summary>Display projection of a <see cref="LogEntry"/>.</summary>
public sealed class LogEntryItem(LogEntry entry)
{
    public LogEntry Entry => entry;

    public LogLevel Level => entry.Level;

    public string Time => entry.Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    public string LevelText => LogEntryFormatter.ShortLevel(entry.Level);

    public string Category => LogEntryFormatter.ShortCategory(entry.Category);

    public string Message => entry.Exception is { } exception
        ? $"{entry.Message} ({exception.GetType().Name}: {exception.Message})"
        : entry.Message;

    public string FullText => LogEntryFormatter.Format(entry);
}
