using Halyard.Models.Launching;
using Halyard.Models.Library;

namespace Halyard.Core.Launching;

public interface IGameLauncher
{
    IReadOnlyCollection<RunningGame> Running { get; }

    /// <summary>Raised on any thread.</summary>
    event EventHandler<RunningGame>? Started;

    /// <summary>Raised on any thread.</summary>
    event EventHandler<RunningGame>? Exited;

    bool IsRunning(string gameId);

    Task<RunningGame> LaunchAsync(Game game, CancellationToken cancellationToken = default);

    void Stop(string gameId);
}
