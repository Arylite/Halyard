using System.Buffers.Binary;
using System.Text;

namespace Halyard.Services.Installs;

/// <summary>Little-endian reader over a span: no stream, no copies.</summary>
internal ref struct SpanReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;

    public int Position { get; set; }

    public byte ReadByte() => _data[Position++];

    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(sizeof(uint)));

    public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(Take(sizeof(int)));

    public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(sizeof(ulong)));

    public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(Take(sizeof(long)));

    public ReadOnlySpan<byte> ReadBytes(int count) => Take(count);

    /// <summary>Unreal's FString: signed length, negative for UTF-16, always null-terminated.</summary>
    public string ReadFString()
    {
        var length = ReadInt32();
        return length switch
        {
            0 => string.Empty,
            > 0 => Encoding.ASCII.GetString(Take(length)[..^1]),
            _ => Encoding.Unicode.GetString(Take(-length * 2)[..^2]),
        };
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if (Position + count > _data.Length)
        {
            throw new InvalidDataException("Unexpected end of manifest data.");
        }

        var slice = _data.Slice(Position, count);
        Position += count;
        return slice;
    }
}
