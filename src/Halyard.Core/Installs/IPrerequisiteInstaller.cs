using Halyard.Models.Library;

namespace Halyard.Core.Installs;

/// <summary>Runs a game's post-install step (redistributables, anti-cheat...) once, before it first starts.</summary>
public interface IPrerequisiteInstaller
{
    /// <exception cref="InvalidOperationException">The step was declined or failed; the game should not start.</exception>
    Task EnsureInstalledAsync(Game game, CancellationToken cancellationToken);
}
