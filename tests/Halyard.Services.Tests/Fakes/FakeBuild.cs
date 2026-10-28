using System.Security.Cryptography;
using Halyard.Models.Installs;

namespace Halyard.Services.Tests.Fakes;

/// <summary>
/// A random game build: chunks with random content, and files stitched from slices of them, with
/// chunks shared between files as in real builds. Also serves as the CDN for its chunk files.
/// </summary>
internal sealed class FakeBuild
{
    public const int ChunkSize = 64 * 1024;

    private FakeBuild(BuildManifest manifest, Dictionary<string, byte[]> chunkFiles, Dictionary<string, byte[]> fileContents)
    {
        Manifest = manifest;
        ChunkFiles = chunkFiles;
        FileContents = fileContents;
    }

    public BuildManifest Manifest { get; }

    /// <summary>Chunk file bytes keyed by their path relative to the CDN base.</summary>
    public Dictionary<string, byte[]> ChunkFiles { get; }

    /// <summary>Expected content of every installed file.</summary>
    public Dictionary<string, byte[]> FileContents { get; }

    public static FakeBuild Create(int chunkCount, int fileCount, int seed = 42)
    {
        var random = new Random(seed);
        var chunkData = new Dictionary<ChunkGuid, byte[]>();
        var chunks = new Dictionary<ChunkGuid, ManifestChunk>();
        var chunkFiles = new Dictionary<string, byte[]>();

        for (var i = 0; i < chunkCount; i++)
        {
            var guid = new ChunkGuid((uint)random.Next(), (uint)random.Next(), (uint)random.Next(), (uint)i);
            var data = new byte[ChunkSize];
            random.NextBytes(data.AsSpan(0, ChunkSize / 2)); // half random, half zeros: compressible like real data

            var hash = (ulong)random.NextInt64();
            var file = EpicFormatWriter.WriteChunkFile(guid, hash, data);
            var chunk = new ManifestChunk(guid, hash, SHA1.HashData(data), (byte)(i % 100), ChunkSize, file.Length);

            chunkData[guid] = data;
            chunks[guid] = chunk;
            chunkFiles[chunk.GetRelativePath("ChunksV4")] = file;
        }

        var guids = chunks.Keys.ToArray();
        var files = new List<ManifestFile>();
        var contents = new Dictionary<string, byte[]>();

        for (var i = 0; i < fileCount; i++)
        {
            var parts = new List<ChunkPart>();
            using var content = new MemoryStream();
            for (var p = random.Next(1, 6); p > 0; p--)
            {
                var guid = guids[random.Next(guids.Length)];
                var offset = random.Next(ChunkSize / 2);
                var size = random.Next(1, ChunkSize - offset);
                parts.Add(new ChunkPart(guid, (uint)offset, (uint)size));
                content.Write(chunkData[guid], offset, size);
            }

            var path = Path.Combine($"dir{i % 3}", $"file{i}.bin");
            var bytes = content.ToArray();
            string[] tags = i % 4 == 0 ? ["optional_hd"] : [];
            files.Add(new ManifestFile(path, SHA1.HashData(bytes), tags, parts));
            contents[path] = bytes;
        }

        var manifest = new BuildManifest("FakeGame", "1.0.0", "Game.exe", "-fake", 18, chunks, files);
        return new FakeBuild(manifest, chunkFiles, contents);
    }
}
