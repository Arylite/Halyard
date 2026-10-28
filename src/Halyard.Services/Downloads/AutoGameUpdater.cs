using Halyard.Core.Downloads;
using Halyard.Core.Hosting;
using Halyard.Core.Launching;
using Halyard.Core.Library;
using Halyard.Core.Settings;
using Halyard.Models.Downloads;
using Halyard.Models.Library;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Downloads;

/// <summary>
/// Whenever the library learns about a newer build, queues the update of every game Halyard
/// installed, unless it is running, already queued, or auto-update is off.
/// </summary>
public sealed class AutoGameUpdater(
    ILibraryService library,
    IDownloadQueue queue,
    IGameLauncher launcher,
    ISettingsService settings,
    ILogger<AutoGameUpdater> logger) : IBackgroundStartupTask, IDisposable
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        library.Changed += OnLibraryChanged;
        launcher.Exited += OnGameExited;
        settings.Changed += OnSettingsChanged;
        QueueUpdates();
        return Task.CompletedTask;
    }

    private void OnLibraryChanged(object? sender, EventArgs e) => QueueUpdates();

    // Turning auto-update on catches up with the updates already known.
    private void OnSettingsChanged(object? sender, Models.Settings.LauncherSettings e) => QueueUpdates();

    // An update skipped because the game was running is queued once it closes.
    private void OnGameExited(object? sender, Models.Launching.RunningGame e) => QueueUpdates();

    private void QueueUpdates()
    {
        if (!settings.Current.AutoUpdateGames)
        {
            return;
        }

        foreach (var game in library.Games.Where(NeedsUpdate))
        {
            var installation = game.Installation!;
            queue.Enqueue(new InstallRequest(game.Id, game.Title, installation.InstallDirectory, installation.InstallTags, InstallKind.Update));
            logger.LogInformation("Queued automatic update of {Game} to {Version}", game.Title, game.LatestVersion);
        }
    }

    // A failed update stays in the queue for the user to retry: re-queuing it automatically would loop.
    private bool NeedsUpdate(Game game) =>
        game.HasUpdate &&
        !launcher.IsRunning(game.Id) &&
        !queue.Jobs.Any(job => job.Request.GameId == game.Id && (!job.IsFinished || job.State == DownloadState.Failed));

    public void Dispose()
    {
        library.Changed -= OnLibraryChanged;
        launcher.Exited -= OnGameExited;
        settings.Changed -= OnSettingsChanged;
    }
}
