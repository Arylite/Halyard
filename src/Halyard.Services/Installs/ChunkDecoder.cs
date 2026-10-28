using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using Halyard.Models.Installs;

namespace Halyard.Services.Installs;

/// <summary>Unpacks a downloaded <c>.chunk</c> file: header, then zlib data, checked against the manifest.</summary>
internal static class ChunkDecoder
{
    private const uint Magic = 0xB1FE3AA2;
    private const byte StoredCompressed = 0x1;
    private const byte StoredEncrypted = 0x2;
    private const byte HashTypeSha1 = 0x2;

    public static PooledChunk Decode(byte[] file, int length, ManifestChunk expected)
    {
        var reader = new SpanReader(file.AsSpan(0, length));
        if (reader.ReadUInt32() != Magic)
        {
            throw new InvalidDataException($"Chunk {expected.Guid} is not a chunk file.");
        }

        var headerVersion = reader.ReadUInt32();
        var headerSize = reader.ReadInt32();
        var compressedSize = reader.ReadInt32();
        var guid = new ChunkGuid(reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32());
        _ = reader.ReadUInt64(); // rolling hash
        var storedAs = reader.ReadByte();

        var hashType = headerVersion >= 2 ? ReadHashType(ref reader) : (byte)0;
        var uncompressedSize = headerVersion >= 3 ? reader.ReadInt32() : (int)expected.WindowSize;

        if (guid != expected.Guid)
        {
            throw new InvalidDataException($"Expected chunk {expected.Guid} but received {guid}.");
        }

        if ((storedAs & StoredEncrypted) != 0)
        {
            throw new NotSupportedException("Encrypted chunks are not supported.");
        }

        var output = ArrayPool<byte>.Shared.Rent(uncompressedSize);
        try
        {
            if ((storedAs & StoredCompressed) != 0)
            {
                using var input = new MemoryStream(file, headerSize, compressedSize, writable: false);
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                zlib.ReadExactly(output.AsSpan(0, uncompressedSize));
            }
            else
            {
                file.AsSpan(headerSize, uncompressedSize).CopyTo(output);
            }

            if ((hashType & HashTypeSha1) != 0 &&
                !SHA1.HashData(output.AsSpan(0, uncompressedSize)).AsSpan().SequenceEqual(expected.Sha1))
            {
                throw new InvalidDataException($"Chunk {expected.Guid} failed its checksum.");
            }

            return new PooledChunk(output, uncompressedSize);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(output);
            throw;
        }
    }

    private static byte ReadHashType(ref SpanReader reader)
    {
        _ = reader.ReadBytes(SHA1.HashSizeInBytes);
        return reader.ReadByte();
    }
}
