using System.Security.Cryptography;
using Halyard.Models.Installs;
using Halyard.Services.Installs;
using Halyard.Services.Tests.Fakes;

namespace Halyard.Services.Tests.Installs;

public sealed class ChunkDecoderTests
{
    private static readonly ChunkGuid Guid = new(1, 2, 3, 4);

    [Fact]
    public void Decode_InflatesAndVerifies()
    {
        var data = Enumerable.Range(0, 5000).Select(i => (byte)(i % 7)).ToArray();
        var file = EpicFormatWriter.WriteChunkFile(Guid, 99, data);
        var expected = new ManifestChunk(Guid, 99, SHA1.HashData(data), 0, (uint)data.Length, file.Length);

        using var chunk = ChunkDecoder.Decode(file, file.Length, expected);

        Assert.Equal(data, chunk.Memory.ToArray());
    }

    [Fact]
    public void Decode_RejectsTheWrongChunk()
    {
        var data = new byte[100];
        var file = EpicFormatWriter.WriteChunkFile(Guid, 1, data);
        var other = new ManifestChunk(new ChunkGuid(9, 9, 9, 9), 1, SHA1.HashData(data), 0, 100, file.Length);

        Assert.Throws<InvalidDataException>(() => ChunkDecoder.Decode(file, file.Length, other));
    }

    [Fact]
    public void Decode_RejectsCorruptContent()
    {
        var data = new byte[100];
        var file = EpicFormatWriter.WriteChunkFile(Guid, 1, data);
        var wrongHash = new ManifestChunk(Guid, 1, new byte[20], 0, 100, file.Length);

        Assert.Throws<InvalidDataException>(() => ChunkDecoder.Decode(file, file.Length, wrongHash));
    }
}
