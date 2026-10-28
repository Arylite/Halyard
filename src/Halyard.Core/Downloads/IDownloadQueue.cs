using Halyard.Models.Downloads;

namespace Halyard.Core.Downloads;

/// <summary>
/// Ordered install queue. Jobs run in list order, up to the configured concurrency.
/// Events are raised on background threads.
/// </summary>
public interface IDownloadQueue
{
    IReadOnlyList<DownloadJob> Jobs { get; }

    event EventHandler<DownloadJob>? JobAdded;

    event EventHandler<DownloadJob>? JobRemoved;

    event EventHandler<DownloadJobMovedEventArgs>? JobMoved;

    /// <summary>Raised when a job changes state (not on progress updates).</summary>
    event EventHandler<DownloadJob>? JobStateChanged;

    /// <summary>Queues a request, or returns the unfinished job already queued for that game.</summary>
    DownloadJob Enqueue(InstallRequest request);

    DownloadJob? FindActive(string gameId);

    void Pause(DownloadJob job);

    void Resume(DownloadJob job);

    void Cancel(DownloadJob job);

    /// <summary>Moves a job up (negative) or down (positive) in the queue.</summary>
    void Move(DownloadJob job, int offset);

    void Remove(DownloadJob job);

    void ClearFinished();
}
