using Halyard.Models.Logging;

namespace Halyard.Core.Logging;

/// <summary>Keeps the most recent entries in memory so the UI can display them.</summary>
public sealed class InMemoryLogStore : ILogSink
{
    public const int Capacity = 5_000;

    private readonly Queue<LogEntry> _entries = new(Capacity);
    private readonly Lock _gate = new();

    public event EventHandler<LogEntry>? EntryWritten;

    public event EventHandler? Cleared;

    public void Write(LogEntry entry)
    {
        lock (_gate)
        {
            if (_entries.Count == Capacity)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(entry);
        }

        EntryWritten?.Invoke(this, entry);
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate)
        {
            return [.. _entries];
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }

        Cleared?.Invoke(this, EventArgs.Empty);
    }
}
