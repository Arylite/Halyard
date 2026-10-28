using Halyard.Models.Installs;
using Halyard.Models.Library;

namespace Halyard.Core.Installs;

/// <summary>Looks up what installing a game involves, before committing to it.</summary>
public interface IInstallPreparer
{
    Task<InstallPreparation> PrepareAsync(Game game, CancellationToken cancellationToken);

    InstallSize Measure(BuildManifest manifest, IReadOnlySet<string> installTags);
}
