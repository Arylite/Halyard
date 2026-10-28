namespace Halyard.Core.Installs;

public interface IGameUninstaller
{
    /// <summary>Deletes the game's files and marks it as not installed.</summary>
    Task UninstallAsync(string gameId, CancellationToken cancellationToken);
}
