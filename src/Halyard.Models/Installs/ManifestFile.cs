namespace Halyard.Models.Installs;

/// <param name="InstallTags">Optional-content tags; a file without tags is always installed.</param>
public sealed record ManifestFile(
    string Path,
    byte[] Sha1,
    IReadOnlyList<string> InstallTags,
    IReadOnlyList<ChunkPart> Parts)
{
    public long Size { get; } = Parts.Sum(part => (long)part.Size);

    public bool IsSelectedBy(IReadOnlySet<string> installTags) =>
        InstallTags.Count == 0 || InstallTags.Any(installTags.Contains);
}

/// <summary>A slice of a chunk that makes up a piece of a file, in file order.</summary>
public readonly record struct ChunkPart(ChunkGuid Chunk, uint Offset, uint Size);
