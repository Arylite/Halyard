using Halyard.Models.Library;

namespace Halyard.Core.Launching;

/// <summary>Builds the command-line arguments a game is started with.</summary>
public interface ILaunchArgumentsBuilder
{
    Task<IReadOnlyList<string>> BuildAsync(Game game, CancellationToken cancellationToken);
}
