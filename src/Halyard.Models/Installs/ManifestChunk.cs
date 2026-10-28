namespace Halyard.Models.Installs;

/// <param name="Hash">Rolling hash; part of the chunk's file name on the CDN.</param>
/// <param name="WindowSize">Uncompressed size.</param>
/// <param name="FileSize">Size of the chunk file to download.</param>
public sealed record ManifestChunk(
    ChunkGuid Guid,
    ulong Hash,
    byte[] Sha1,
    byte GroupNumber,
    uint WindowSize,
    long FileSize)
{
    public string GetRelativePath(string chunkDirectory) =>
        $"{chunkDirectory}/{GroupNumber:D2}/{Hash:X16}_{Guid}.chunk";
}
