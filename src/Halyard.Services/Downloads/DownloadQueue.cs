using Halyard.Core.Downloads;
using Halyard.Core.Hosting;
using Halyard.Core.Installs;
using Halyard.Core.Serialization;
using Halyard.Core.Settings;
using Halyard.Core.Storage;
using Halyard.Models.Downloads;
using Halyard.Models.Settings;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Downloads;

/// <summary>
/// Runs installs in queue order, up to the configured concurrency. Unfinished jobs are saved and
/// come back paused after a restart; the installer's journal lets them resume where they stopped.
/// </summary>
public sealed class DownloadQueue : IDownloadQueue, IAsyncInitializer, IDisposable
{
    private readonly List<DownloadJob> _jobs = [];
    private readonly Dictionary<DownloadJob, CancellationTokenSource> _running = [];
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    private readonly IGameInstaller _installer;
    private readonly ISettingsService _settings;
    private readonly JsonFileStore<List<InstallRequest>> _store;
    private readonly ILogger<DownloadQueue> _logger;

    public DownloadQueue(IGameInstaller installer, ISettingsService settings, AppPaths paths, ILogger<DownloadQueue> logger)
    {
        _installer = installer;
        _settings = settings;
        _store = new JsonFileStore<List<InstallRequest>>(paths.QueueFile, HalyardJsonContext.Default.ListInstallRequest);
        _logger = logger;
        _settings.Changed += OnSettingsChanged;
    }

    public IReadOnlyList<DownloadJob> Jobs
    {
        get
        {
            lock (_gate)
            {
                return [.. _jobs];
            }
        }
    }

    public event EventHandler<DownloadJob>? JobAdded;

    public event EventHandler<DownloadJob>? JobRemoved;

    public event EventHandler<DownloadJobMovedEventArgs>? JobMoved;

    public event EventHandler<DownloadJob>? JobStateChanged;

    private int MaxConcurrency => Math.Max(1, _settings.Current.MaxConcurrentDownloads);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        foreach (var request in await _store.ReadAsync(cancellationToken) ?? [])
        {
            var job = new DownloadJob(request);
            job.TransitionTo(DownloadState.Paused);
            lock (_gate)
            {
                _jobs.Add(job);
            }
        }
    }

    public DownloadJob Enqueue(InstallRequest request)
    {
        if (FindActive(request.GameId) is { } existing)
        {
            return existing;
        }

        var job = new DownloadJob(request);
        lock (_gate)
        {
            _jobs.Add(job);
        }

        _logger.LogInformation("Queued {Kind} of {Game} into {Directory}", request.Kind, request.DisplayName, request.InstallDirectory);
        JobAdded?.Invoke(this, job);
        Persist();
        Pump();
        return job;
    }

    public DownloadJob? FindActive(string gameId)
    {
        lock (_gate)
        {
            return _jobs.FirstOrDefault(job => job.Request.GameId == gameId && !job.IsFinished);
        }
    }

    public void Pause(DownloadJob job)
    {
        if (job.State is not (DownloadState.Queued or DownloadState.Running))
        {
            return;
        }

        CancelRun(job);
        SetState(job, DownloadState.Paused);
        _logger.LogInformation("Paused {Game}", job.Request.DisplayName);
        Pump();
    }

    public void Resume(DownloadJob job)
    {
        if (job.State is not (DownloadState.Paused or DownloadState.Failed))
        {
            return;
        }

        SetState(job, DownloadState.Queued);
        _logger.LogInformation("Resumed {Game}", job.Request.DisplayName);
        Pump();
    }

    public void Cancel(DownloadJob job)
    {
        if (job.IsFinished)
        {
            return;
        }

        // Written files and the journal stay on disk: installing again later resumes from them.
        CancelRun(job);
        SetState(job, DownloadState.Canceled);
        _logger.LogInformation("Canceled {Game}", job.Request.DisplayName);
        Persist();
        Pump();
    }

    public void Move(DownloadJob job, int offset)
    {
        int oldIndex, newIndex;
        lock (_gate)
        {
            oldIndex = _jobs.IndexOf(job);
            newIndex = Math.Clamp(oldIndex + offset, 0, _jobs.Count - 1);
            if (oldIndex < 0 || oldIndex == newIndex)
            {
                return;
            }

            _jobs.RemoveAt(oldIndex);
            _jobs.Insert(newIndex, job);
        }

        JobMoved?.Invoke(this, new DownloadJobMovedEventArgs(job, oldIndex, newIndex));
        Persist();
        Pump();
    }

    public void Remove(DownloadJob job)
    {
        Cancel(job);

        bool removed;
        lock (_gate)
        {
            removed = _jobs.Remove(job);
        }

        if (removed)
        {
            JobRemoved?.Invoke(this, job);
            Persist();
        }
    }

    public void ClearFinished()
    {
        foreach (var job in Jobs.Where(job => job.IsFinished))
        {
            Remove(job);
        }
    }

    /// <summary>Starts queued jobs, in order, until every concurrency slot is used.</summary>
    private void Pump()
    {
        List<(DownloadJob Job, CancellationTokenSource Cancellation)> started = [];

        lock (_gate)
        {
            var freeSlots = MaxConcurrency - _running.Count;
            foreach (var job in _jobs)
            {
                if (freeSlots == 0)
                {
                    break;
                }

                // A paused-then-resumed job may still be unwinding; wait for its worker to exit.
                if (job.State != DownloadState.Queued || _running.ContainsKey(job))
                {
                    continue;
                }

                var cancellation = new CancellationTokenSource();
                _running.Add(job, cancellation);
                started.Add((job, cancellation));
                freeSlots--;
            }
        }

        foreach (var (job, cancellation) in started)
        {
            SetState(job, DownloadState.Running);
            // On the thread pool, never on the caller's context: enqueued from the UI, an install
            // would otherwise resume every await (decompression, hashing, disk writes) on the UI thread.
            _ = Task.Run(() => RunAsync(job, cancellation));
        }
    }

    private async Task RunAsync(DownloadJob job, CancellationTokenSource cancellation)
    {
        try
        {
            await _installer.RunAsync(job.Request, job, cancellation.Token);
            SetState(job, DownloadState.Completed);
            _logger.LogInformation("{Kind} of {Game} completed", job.Request.Kind, job.Request.DisplayName);
        }
        catch (Exception) when (cancellation.IsCancellationRequested)
        {
            // Pause or Cancel already recorded the new state. Whatever the install threw while
            // unwinding (often not an OperationCanceledException) must not turn it into Failed.
        }
        catch (Exception ex)
        {
            SetState(job, DownloadState.Failed, ex.Message);
            _logger.LogError(ex, "{Kind} of {Game} failed", job.Request.Kind, job.Request.DisplayName);
        }
        finally
        {
            lock (_gate)
            {
                _running.Remove(job);
                cancellation.Dispose();
            }

            Persist();
            Pump();
        }
    }

    /// <returns><see langword="true"/> if the job had an active worker.</returns>
    private bool CancelRun(DownloadJob job)
    {
        lock (_gate)
        {
            if (!_running.TryGetValue(job, out var cancellation))
            {
                return false;
            }

            cancellation.Cancel();
            return true;
        }
    }

    private void SetState(DownloadJob job, DownloadState state, string? error = null)
    {
        job.TransitionTo(state, error);
        JobStateChanged?.Invoke(this, job);
    }

    private void Persist()
    {
        List<InstallRequest> pending;
        lock (_gate)
        {
            pending = [.. _jobs.Where(job => !job.IsFinished).Select(job => job.Request)];
        }

        _ = SaveAsync(pending);
    }

    private async Task SaveAsync(List<InstallRequest> pending)
    {
        await _saveGate.WaitAsync();
        try
        {
            await _store.WriteAsync(pending);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not save the download queue");
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private void OnSettingsChanged(object? sender, LauncherSettings settings) => Pump();

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;

        lock (_gate)
        {
            foreach (var cancellation in _running.Values)
            {
                cancellation.Cancel();
            }
        }

        _saveGate.Dispose();
    }
}
