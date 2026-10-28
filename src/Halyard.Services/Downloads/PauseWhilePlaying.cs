using Halyard.Core.Downloads;
using Halyard.Core.Hosting;
using Halyard.Core.Launching;
using Halyard.Core.Settings;
using Halyard.Models.Downloads;
using Halyard.Models.Launching;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Downloads;

/// <summary>
/// Pauses running downloads while a game is open, so it gets the full connection and disk, and
/// resumes exactly those downloads once the last game exits. Downloads the user paused stay paused.
/// </summary>
public sealed class PauseWhilePlaying(
    IGameLauncher launcher,
    IDownloadQueue queue,
    ISettingsService settings,
    ILogger<PauseWhilePlaying> logger) : IBackgroundStartupTask, IDisposable
{
    private readonly HashSet<DownloadJob> _pausedForPlay = [];
    private readonly Lock _gate = new();

    public Task RunAsync(CancellationToken cancellationToken)
    {
        launcher.Started += OnGameStarted;
        launcher.Exited += OnGameExited;
        return Task.CompletedTask;
    }

    private void OnGameStarted(object? sender, RunningGame game)
    {
        if (!settings.Current.PauseDownloadsWhilePlaying)
        {
            return;
        }

        // Queued jobs first: pausing a running one frees its slot, and the queue would start the next.
        var active = queue.Jobs
            .Where(job => job.State is DownloadState.Running or DownloadState.Queued)
            .OrderBy(job => job.State == DownloadState.Running)
            .ToList();
        lock (_gate)
        {
            _pausedForPlay.UnionWith(active);
        }

        foreach (var job in active)
        {
            queue.Pause(job);
        }

        if (active.Count > 0)
        {
            logger.LogInformation("Paused {Count} download(s) while {Game} runs", active.Count, game.Game.Title);
        }
    }

    private void OnGameExited(object? sender, RunningGame game)
    {
        if (launcher.Running.Count > 0)
        {
            return;
        }

        List<DownloadJob> toResume;
        lock (_gate)
        {
            toResume = [.. _pausedForPlay];
            _pausedForPlay.Clear();
        }

        foreach (var job in toResume.Where(job => job.State == DownloadState.Paused))
        {
            queue.Resume(job);
        }
    }

    public void Dispose()
    {
        launcher.Started -= OnGameStarted;
        launcher.Exited -= OnGameExited;
    }
}
