using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Halyard.Models.Installs;

namespace Halyard.Services.Tests.Fakes;

/// <summary>Writes Epic's binary manifest and chunk formats, field for field, to feed the readers under test.</summary>
internal static class EpicFormatWriter
{
    public static byte[] WriteChunkFile(ChunkGuid guid, ulong hash, byte[] data)
    {
        var compressed = Deflate(data);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        const int headerSize = 66;
        writer.Write(0xB1FE3AA2u);
        writer.Write(3u); // header version
        writer.Write(headerSize);
        writer.Write(compressed.Length);
        WriteGuid(writer, guid);
        writer.Write(hash);
        writer.Write((byte)1); // compressed
        writer.Write(SHA1.HashData(data));
        writer.Write((byte)2); // hash type: SHA-1
        writer.Write(data.Length);
        writer.Write(compressed);
        return stream.ToArray();
    }

    public static byte[] WriteManifest(BuildManifest manifest)
    {
        using var body = new MemoryStream();
        using (var writer = new BinaryWriter(body, Encoding.ASCII, leaveOpen: true))
        {
            Section(writer, w =>
            {
                w.Write((byte)0); // data version
                w.Write(manifest.FeatureLevel);
                w.Write((byte)1); // is file data
                w.Write(0u); // app id
                WriteFString(w, manifest.AppName);
                WriteFString(w, manifest.BuildVersion);
                WriteFString(w, manifest.LaunchExecutable);
                WriteFString(w, manifest.LaunchCommand);
                var prerequisite = manifest.Prerequisite;
                w.Write(prerequisite?.Ids.Count ?? 0);
                foreach (var id in prerequisite?.Ids ?? [])
                {
                    WriteFString(w, id);
                }

                WriteFString(w, prerequisite?.Name ?? string.Empty);
                WriteFString(w, prerequisite?.Path.Replace(Path.DirectorySeparatorChar, '/') ?? string.Empty);
                WriteFString(w, prerequisite?.Arguments ?? string.Empty);
            });

            var chunks = manifest.Chunks.Values.ToList();
            Section(writer, w =>
            {
                w.Write((byte)0);
                w.Write(chunks.Count);
                chunks.ForEach(chunk => WriteGuid(w, chunk.Guid));
                chunks.ForEach(chunk => w.Write(chunk.Hash));
                chunks.ForEach(chunk => w.Write(chunk.Sha1));
                chunks.ForEach(chunk => w.Write(chunk.GroupNumber));
                chunks.ForEach(chunk => w.Write(chunk.WindowSize));
                chunks.ForEach(chunk => w.Write(chunk.FileSize));
            });

            var files = manifest.Files;
            Section(writer, w =>
            {
                w.Write((byte)0);
                w.Write(files.Count);
                foreach (var file in files)
                {
                    WriteFString(w, file.Path.Replace(Path.DirectorySeparatorChar, '/'));
                }

                foreach (var _ in files)
                {
                    WriteFString(w, string.Empty); // symlink target
                }

                foreach (var file in files)
                {
                    w.Write(file.Sha1);
                }

                foreach (var _ in files)
                {
                    w.Write((byte)0); // flags
                }

                foreach (var file in files)
                {
                    w.Write(file.InstallTags.Count);
                    foreach (var tag in file.InstallTags)
                    {
                        WriteFString(w, tag);
                    }
                }

                foreach (var file in files)
                {
                    w.Write(file.Parts.Count);
                    foreach (var part in file.Parts)
                    {
                        w.Write(28); // part struct size
                        WriteGuid(w, part.Chunk);
                        w.Write(part.Offset);
                        w.Write(part.Size);
                    }
                }
            });

            Section(writer, w =>
            {
                w.Write((byte)0);
                w.Write(0); // custom fields
            });
        }

        var raw = body.ToArray();
        var compressed = Deflate(raw);

        using var output = new MemoryStream();
        using var header = new BinaryWriter(output);
        header.Write(0x44BEC00Cu);
        header.Write(41); // header size
        header.Write(raw.Length);
        header.Write(compressed.Length);
        header.Write(SHA1.HashData(raw));
        header.Write((byte)1); // compressed
        header.Write(18u); // serialisation version
        header.Write(compressed);
        return output.ToArray();
    }

    /// <summary>Writes a size-prefixed section, the way every manifest section starts.</summary>
    private static void Section(BinaryWriter writer, Action<BinaryWriter> content)
    {
        var start = writer.BaseStream.Position;
        writer.Write(0);
        content(writer);
        var end = writer.BaseStream.Position;
        writer.BaseStream.Position = start;
        writer.Write((int)(end - start));
        writer.BaseStream.Position = end;
    }

    private static void WriteFString(BinaryWriter writer, string value)
    {
        if (value.Length == 0)
        {
            writer.Write(0);
            return;
        }

        writer.Write(value.Length + 1);
        writer.Write(Encoding.ASCII.GetBytes(value));
        writer.Write((byte)0);
    }

    private static void WriteGuid(BinaryWriter writer, ChunkGuid guid)
    {
        writer.Write(guid.A);
        writer.Write(guid.B);
        writer.Write(guid.C);
        writer.Write(guid.D);
    }

    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }
}
