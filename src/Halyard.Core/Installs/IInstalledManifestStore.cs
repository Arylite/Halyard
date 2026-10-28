using Halyard.Models.Installs;

namespace Halyard.Core.Installs;

/// <summary>Keeps the manifest of the installed build inside the install folder, for updates and repairs.</summary>
public interface IInstalledManifestStore
{
    Task SaveAsync(string installDirectory, byte[] rawManifest, CancellationToken cancellationToken);

    Task<BuildManifest?> LoadAsync(string installDirectory, CancellationToken cancellationToken);
}
