using Halyard.Models.Library;

namespace Halyard.Models.Installs;

/// <summary>
/// A game build as Epic describes it: files made of parts of shared, compressed chunks.
/// </summary>
public sealed record BuildManifest(
    string AppName,
    string BuildVersion,
    string LaunchExecutable,
    string LaunchCommand,
    uint FeatureLevel,
    IReadOnlyDictionary<ChunkGuid, ManifestChunk> Chunks,
    IReadOnlyList<ManifestFile> Files)
{
    /// <summary>Installer to run once after install, if the build declares one.</summary>
    public GamePrerequisite? Prerequisite { get; init; }

    /// <summary>Folder of the chunk files on the CDN; it changed as the format evolved.</summary>
    public string ChunkDirectory => FeatureLevel switch
    {
        >= 15 => "ChunksV4",
        >= 6 => "ChunksV3",
        >= 3 => "ChunksV2",
        _ => "Chunks",
    };
}
