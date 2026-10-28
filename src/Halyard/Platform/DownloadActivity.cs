using Halyard.Core.Downloads;
using Halyard.Models.Downloads;

namespace Halyard.Platform;

/// <summary>Combined progress of every unfinished install, for the taskbar and the tray.</summary>
public sealed record DownloadActivitySnapshot(int ActiveCount, double? Fraction, bool IsPaused, string? Label)
{
    public static DownloadActivitySnapshot Idle { get; } = new(0, null, false, null);
}

/// <summary>Watches the queue and summarises it whenever a job moves or progresses.</summary>
public sealed class DownloadActivity
{
    private readonly IDownloadQueue _queue;
    private readonly HashSet<DownloadJob> _watched = [];
    private readonly Lock _gate = new();

    public DownloadActivity(IDownloadQueue queue)
    {
        _queue = queue;
        queue.JobAdded += (_, job) => Watch(job);
        queue.JobStateChanged += (_, _) => Publish();
        queue.JobRemoved += (_, job) => Unwatch(job);

        foreach (var job in queue.Jobs)
        {
            Watch(job);
        }
    }

    /// <summary>Raised on background threads.</summary>
    public event EventHandler<DownloadActivitySnapshot>? Changed;

    public DownloadActivitySnapshot Current { get; private set; } = DownloadActivitySnapshot.Idle;

    private void Watch(DownloadJob job)
    {
        lock (_gate)
        {
            if (_watched.Add(job))
            {
                job.Changed += OnJobChanged;
            }
        }

        Publish();
    }

    private void Unwatch(DownloadJob job)
    {
        lock (_gate)
        {
            if (_watched.Remove(job))
            {
                job.Changed -= OnJobChanged;
            }
        }

        Publish();
    }

    private void OnJobChanged(object? sender, EventArgs e) => Publish();

    private void Publish()
    {
        var active = _queue.Jobs.Where(job => !job.IsFinished).ToList();
        var running = active.FirstOrDefault(job => job.State == DownloadState.Running);

        Current = active.Count == 0
            ? DownloadActivitySnapshot.Idle
            : new DownloadActivitySnapshot(
                active.Count,
                (running ?? active[0]).Progress.Fraction,
                running is null,
                running is null
                    ? $"{active.Count} download(s) paused"
                    : $"{running.Request.DisplayName} - {running.Progress.Fraction ?? 0:P0}");

        Changed?.Invoke(this, Current);
    }
}
