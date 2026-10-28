using System.IO.Compression;
using System.Security.Cryptography;
using Halyard.Core.Installs;
using Halyard.Models.Installs;
using Halyard.Models.Library;

namespace Halyard.Services.Installs;

/// <summary>
/// Reads Epic's binary build manifests: a header, then a (usually zlib-compressed) body holding the
/// build metadata, the chunk list and the file list. Every section starts with its own size, so
/// fields added by newer versions are skipped safely.
/// </summary>
public sealed class BinaryManifestParser : IManifestParser
{
    private const uint Magic = 0x44BEC00C;
    private const byte StoredCompressed = 0x1;
    private const byte StoredEncrypted = 0x2;

    public BuildManifest Parse(byte[] data)
    {
        if (data.Length > 0 && data[0] == (byte)'{')
        {
            throw new NotSupportedException("This game still uses Epic's legacy JSON manifest format, which Halyard can't install yet.");
        }

        var body = ReadBody(data);
        var reader = new SpanReader(body);

        var meta = ReadMeta(ref reader);
        var chunks = ReadChunks(ref reader);
        var files = ReadFiles(ref reader);

        return new BuildManifest(meta.AppName, meta.BuildVersion, meta.LaunchExecutable, meta.LaunchCommand, meta.FeatureLevel, chunks, files)
        {
            Prerequisite = meta.Prerequisite,
        };
    }

    private sealed record ManifestMeta(
        string AppName,
        string BuildVersion,
        string LaunchExecutable,
        string LaunchCommand,
        uint FeatureLevel,
        GamePrerequisite? Prerequisite);

    private static byte[] ReadBody(byte[] data)
    {
        var header = new SpanReader(data);
        if (header.ReadUInt32() != Magic)
        {
            throw new InvalidDataException("Not an Epic manifest.");
        }

        var headerSize = header.ReadInt32();
        var uncompressedSize = header.ReadInt32();
        var compressedSize = header.ReadInt32();
        var sha1 = header.ReadBytes(SHA1.HashSizeInBytes);
        var storedAs = header.ReadByte();

        if ((storedAs & StoredEncrypted) != 0)
        {
            throw new NotSupportedException("Encrypted manifests are not supported.");
        }

        var body = (storedAs & StoredCompressed) != 0
            ? Inflate(data, headerSize, compressedSize, uncompressedSize)
            : data.AsSpan(headerSize, uncompressedSize).ToArray();

        if (!SHA1.HashData(body).AsSpan().SequenceEqual(sha1))
        {
            throw new InvalidDataException("Manifest checksum mismatch; the download is corrupt.");
        }

        return body;
    }

    private static ManifestMeta ReadMeta(ref SpanReader reader)
    {
        var start = reader.Position;
        var size = reader.ReadInt32();
        _ = reader.ReadByte(); // data version
        var featureLevel = reader.ReadUInt32();
        _ = reader.ReadByte(); // is file data
        _ = reader.ReadUInt32(); // app id
        var appName = reader.ReadFString();
        var buildVersion = reader.ReadFString();
        var launchExecutable = reader.ReadFString();
        var launchCommand = reader.ReadFString();

        var prerequisiteIds = new string[reader.ReadInt32()];
        for (var i = 0; i < prerequisiteIds.Length; i++)
        {
            prerequisiteIds[i] = reader.ReadFString();
        }

        var prerequisiteName = reader.ReadFString();
        var prerequisitePath = reader.ReadFString();
        var prerequisiteArguments = reader.ReadFString();

        // Build id and uninstall actions (newer versions) follow; the section size lets us skip them.
        reader.Position = start + size;

        var prerequisite = string.IsNullOrWhiteSpace(prerequisitePath)
            ? null
            : new GamePrerequisite(
                prerequisiteIds,
                prerequisiteName,
                prerequisitePath.Replace('/', Path.DirectorySeparatorChar),
                prerequisiteArguments);

        return new ManifestMeta(appName, buildVersion, launchExecutable, launchCommand, featureLevel, prerequisite);
    }

    private static Dictionary<ChunkGuid, ManifestChunk> ReadChunks(ref SpanReader reader)
    {
        var start = reader.Position;
        var size = reader.ReadInt32();
        _ = reader.ReadByte(); // version
        var count = reader.ReadInt32();

        // Stored column by column: all GUIDs, then all hashes, and so on.
        var guids = new ChunkGuid[count];
        for (var i = 0; i < count; i++)
        {
            guids[i] = ReadGuid(ref reader);
        }

        var hashes = new ulong[count];
        for (var i = 0; i < count; i++)
        {
            hashes[i] = reader.ReadUInt64();
        }

        var shas = new byte[count][];
        for (var i = 0; i < count; i++)
        {
            shas[i] = reader.ReadBytes(SHA1.HashSizeInBytes).ToArray();
        }

        var groups = new byte[count];
        for (var i = 0; i < count; i++)
        {
            groups[i] = reader.ReadByte();
        }

        var windowSizes = new uint[count];
        for (var i = 0; i < count; i++)
        {
            windowSizes[i] = reader.ReadUInt32();
        }

        var chunks = new Dictionary<ChunkGuid, ManifestChunk>(count);
        for (var i = 0; i < count; i++)
        {
            chunks[guids[i]] = new ManifestChunk(guids[i], hashes[i], shas[i], groups[i], windowSizes[i], reader.ReadInt64());
        }

        reader.Position = start + size;
        return chunks;
    }

    private static List<ManifestFile> ReadFiles(ref SpanReader reader)
    {
        var start = reader.Position;
        var size = reader.ReadInt32();
        _ = reader.ReadByte(); // version
        var count = reader.ReadInt32();

        var names = new string[count];
        for (var i = 0; i < count; i++)
        {
            names[i] = reader.ReadFString();
        }

        for (var i = 0; i < count; i++)
        {
            _ = reader.ReadFString(); // symlink target
        }

        var shas = new byte[count][];
        for (var i = 0; i < count; i++)
        {
            shas[i] = reader.ReadBytes(SHA1.HashSizeInBytes).ToArray();
        }

        for (var i = 0; i < count; i++)
        {
            _ = reader.ReadByte(); // flags (read-only, compressed, executable)
        }

        var tags = new string[count][];
        for (var i = 0; i < count; i++)
        {
            tags[i] = new string[reader.ReadInt32()];
            for (var j = 0; j < tags[i].Length; j++)
            {
                tags[i][j] = reader.ReadFString();
            }
        }

        var files = new List<ManifestFile>(count);
        for (var i = 0; i < count; i++)
        {
            var parts = new ChunkPart[reader.ReadInt32()];
            for (var j = 0; j < parts.Length; j++)
            {
                var partStart = reader.Position;
                var partSize = reader.ReadInt32();
                parts[j] = new ChunkPart(ReadGuid(ref reader), reader.ReadUInt32(), reader.ReadUInt32());
                reader.Position = partStart + partSize;
            }

            // Epic stores paths with forward slashes; normalize once here.
            files.Add(new ManifestFile(names[i].Replace('/', Path.DirectorySeparatorChar), shas[i], tags[i], parts));
        }

        // MD5s, MIME types and SHA-256s (newer versions) aren't needed: SHA-1 already verifies files.
        reader.Position = start + size;
        return files;
    }

    private static ChunkGuid ReadGuid(ref SpanReader reader) =>
        new(reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32());

    private static byte[] Inflate(byte[] data, int offset, int compressedSize, int uncompressedSize)
    {
        var output = new byte[uncompressedSize];
        using var input = new MemoryStream(data, offset, compressedSize, writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        zlib.ReadExactly(output);
        return output;
    }
}
