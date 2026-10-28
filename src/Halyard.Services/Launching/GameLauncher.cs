using System.Collections.Concurrent;
using System.Diagnostics;
using Halyard.Core.Installs;
using Halyard.Core.Launching;
using Halyard.Models.Launching;
using Halyard.Models.Library;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Launching;

public sealed class GameLauncher(
    ILaunchArgumentsBuilder argumentsBuilder,
    IPrerequisiteInstaller prerequisites,
    TimeProvider time,
    ILogger<GameLauncher> logger) : IGameLauncher, IDisposable
{
    private readonly ConcurrentDictionary<string, (RunningGame Game, Process Process)> _running = new();

    public IReadOnlyCollection<RunningGame> Running => [.. _running.Values.Select(entry => entry.Game)];

    public event EventHandler<RunningGame>? Started;

    public event EventHandler<RunningGame>? Exited;

    public bool IsRunning(string gameId) => _running.ContainsKey(gameId);

    public async Task<RunningGame> LaunchAsync(Game game, CancellationToken cancellationToken = default)
    {
        if (_running.TryGetValue(game.Id, out var existing))
        {
            return existing.Game;
        }

        var installation = game.Installation ?? throw new InvalidOperationException($"{game.Title} is not installed.");
        if (!File.Exists(installation.ExecutablePath))
        {
            throw new FileNotFoundException($"{game.Title} executable was not found.", installation.ExecutablePath);
        }

        // Post-install step (runtimes, anti-cheat...): only the first time, like the official launcher.
        await prerequisites.EnsureInstalledAsync(game, cancellationToken);

        var arguments = await argumentsBuilder.BuildAsync(game, cancellationToken);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(installation.ExecutablePath, arguments)
            {
                WorkingDirectory = Path.GetDirectoryName(installation.ExecutablePath),
                UseShellExecute = false,
            },
            EnableRaisingEvents = true,
        };

        try
        {
            process.Start();
        }
        catch
        {
            process.Dispose();
            throw;
        }

        var running = new RunningGame(game, process.Id, time.GetLocalNow());
        _running[game.Id] = (running, process);
        process.Exited += (_, _) => OnExited(game.Id);

        // Arguments are deliberately not logged: they carry a live exchange code.
        logger.LogInformation("Launched {Game} (pid {ProcessId})", game.Title, process.Id);
        Started?.Invoke(this, running);

        // The process may have exited before the handler was attached.
        if (process.HasExited)
        {
            OnExited(game.Id);
        }

        return running;
    }

    public void Stop(string gameId)
    {
        if (_running.TryGetValue(gameId, out var entry))
        {
            logger.LogInformation("Stopping {Game}", entry.Game.Game.Title);
            entry.Process.Kill(entireProcessTree: true);
        }
    }

    private void OnExited(string gameId)
    {
        if (!_running.TryRemove(gameId, out var entry))
        {
            return;
        }

        using var process = entry.Process;
        var duration = time.GetLocalNow() - entry.Game.StartedAt;
        logger.LogInformation(
            "{Game} exited with code {ExitCode} after {Duration:hh\\:mm\\:ss}",
            entry.Game.Game.Title,
            process.ExitCode,
            duration);

        Exited?.Invoke(this, entry.Game);
    }

    public void Dispose()
    {
        foreach (var (_, process) in _running.Values)
        {
            process.Dispose();
        }

        _running.Clear();
    }
}
