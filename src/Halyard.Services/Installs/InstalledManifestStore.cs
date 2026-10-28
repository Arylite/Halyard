using Halyard.Core.Installs;
using Halyard.Core.Storage;
using Halyard.Models.Installs;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Installs;

public sealed class InstalledManifestStore(IManifestParser parser, ILogger<InstalledManifestStore> logger) : IInstalledManifestStore
{
    public async Task SaveAsync(string installDirectory, byte[] rawManifest, CancellationToken cancellationToken)
    {
        var path = GetPath(installDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, rawManifest, cancellationToken);
    }

    public async Task<BuildManifest?> LoadAsync(string installDirectory, CancellationToken cancellationToken)
    {
        var path = GetPath(installDirectory);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var raw = await File.ReadAllBytesAsync(path, cancellationToken);
            return await Task.Run(() => parser.Parse(raw), cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            logger.LogWarning(ex, "Installed manifest in {Directory} is damaged", installDirectory);
            return null;
        }
    }

    private static string GetPath(string installDirectory) =>
        Path.Combine(AppPaths.InstallMetadataDirectory(installDirectory), "manifest.bin");
}
