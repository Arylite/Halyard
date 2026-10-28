using Halyard.Core.Hosting;
using Halyard.Core.Launching;
using Halyard.Core.Library;
using Halyard.Models.Launching;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Library;

/// <summary>Adds each play session to the game's total play time.</summary>
public sealed class PlaytimeTracker(
    IGameLauncher launcher,
    ILibraryService library,
    TimeProvider time,
    ILogger<PlaytimeTracker> logger) : IBackgroundStartupTask, IDisposable
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        launcher.Exited += OnGameExited;
        return Task.CompletedTask;
    }

    private async void OnGameExited(object? sender, RunningGame running)
    {
        var endedAt = time.GetLocalNow();
        var session = endedAt - running.StartedAt;

        try
        {
            await library.UpdateAsync(running.Game.Id, game => game with
            {
                PlayTime = game.PlayTime + session,
                LastPlayedAt = endedAt,
            });
        }
        catch (Exception ex)
        {
            // async void on the process's exit thread: anything escaping here would end Halyard.
            logger.LogWarning(ex, "Could not record play time for {Game}", running.Game.Title);
        }
    }

    public void Dispose() => launcher.Exited -= OnGameExited;
}
