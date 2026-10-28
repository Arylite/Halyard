using System.Threading.Channels;
using Halyard.Models.Logging;

namespace Halyard.Core.Logging;

/// <summary>Appends entries to one file per day, on a background writer so logging never blocks.</summary>
public sealed class FileLogSink : ILogSink, IDisposable, IAsyncDisposable
{
    // Not SingleReader: that implementation can't report Count, which the flush logic relies on.
    private readonly Channel<LogEntry> _channel = Channel.CreateUnbounded<LogEntry>();

    private readonly string _directory;
    private readonly Task _writer;

    public FileLogSink(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
        _writer = Task.Run(WriteLoopAsync);
    }

    public void Write(LogEntry entry) => _channel.Writer.TryWrite(entry);

    private async Task WriteLoopAsync()
    {
        StreamWriter? writer = null;
        DateOnly currentDay = default;

        try
        {
            await foreach (var entry in _channel.Reader.ReadAllAsync())
            {
                var day = DateOnly.FromDateTime(entry.Timestamp.LocalDateTime);
                if (writer is null || day != currentDay)
                {
                    if (writer is not null)
                    {
                        await writer.DisposeAsync();
                    }

                    writer = Open(day);
                    currentDay = day;
                }

                await writer.WriteLineAsync(LogEntryFormatter.Format(entry));

                // Flush once the burst is drained rather than after every line.
                if (_channel.Reader.Count == 0)
                {
                    await writer.FlushAsync();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nowhere left to report a failing log file; the in-memory sink still has the entries.
            // Completing the channel makes Write drop entries instead of queuing them forever.
            _channel.Writer.TryComplete();
        }
        finally
        {
            try
            {
                if (writer is not null)
                {
                    await writer.DisposeAsync();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The final flush failed: same as above, and shutdown must not throw.
            }
        }
    }

    private StreamWriter Open(DateOnly day)
    {
        var path = Path.Combine(_directory, $"halyard-{day:yyyyMMdd}.log");
        return new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read));
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _writer.GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _writer.ConfigureAwait(false);
    }
}
