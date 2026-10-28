using Halyard.Core.Installs;
using Halyard.Models.Installs;
using Halyard.Models.Library;

namespace Halyard.Services.Installs;

/// <summary>
/// Turns a manifest's install tags into choices with real sizes. This is what keeps installs lean:
/// optional content (high-resolution textures, extra modes, languages) is only downloaded if picked.
/// </summary>
public sealed class InstallPreparer(IManifestSource manifests) : IInstallPreparer
{
    private static readonly IReadOnlySet<string> NoTags = new HashSet<string>();

    public async Task<InstallPreparation> PrepareAsync(Game game, CancellationToken cancellationToken)
    {
        var manifest = (await manifests.GetLatestAsync(game, cancellationToken)).Manifest;
        return await Task.Run(() => Describe(manifest), cancellationToken);
    }

    public InstallSize Measure(BuildManifest manifest, IReadOnlySet<string> installTags)
    {
        long installBytes = 0;
        var chunks = new HashSet<ChunkGuid>();

        foreach (var file in manifest.Files.Where(file => file.IsSelectedBy(installTags)))
        {
            installBytes += file.Size;
            foreach (var part in file.Parts)
            {
                chunks.Add(part.Chunk);
            }
        }

        var downloadBytes = chunks.Sum(guid => manifest.Chunks.TryGetValue(guid, out var chunk) ? chunk.FileSize : 0);
        return new InstallSize(downloadBytes, installBytes);
    }

    private InstallPreparation Describe(BuildManifest manifest)
    {
        var required = Measure(manifest, NoTags);

        var options = manifest.Files
            .SelectMany(file => file.InstallTags)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(tag =>
            {
                var with = Measure(manifest, new HashSet<string> { tag });
                return new InstallOption(
                    tag,
                    with.InstallBytes - required.InstallBytes,
                    with.DownloadBytes - required.DownloadBytes,
                    IsRecommended(tag));
            })
            .Where(option => option.ExtraInstallSize > 0)
            .ToList();

        return new InstallPreparation(manifest, required, options);
    }

    /// <summary>
    /// Unreal marks on-demand content with "optional" in its chunk tags (typically high-resolution
    /// assets); everything else is pre-selected so the game is complete by default.
    /// </summary>
    private static bool IsRecommended(string tag) => !tag.Contains("optional", StringComparison.OrdinalIgnoreCase);
}
