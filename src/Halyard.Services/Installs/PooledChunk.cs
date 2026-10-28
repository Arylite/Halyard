using System.Buffers;

namespace Halyard.Services.Installs;

/// <summary>Decompressed chunk data in a pooled buffer; dispose to give the buffer back.</summary>
internal sealed class PooledChunk(byte[] buffer, int length) : IDisposable
{
    private byte[]? _buffer = buffer;

    public ReadOnlyMemory<byte> Memory =>
        (_buffer ?? throw new ObjectDisposedException(nameof(PooledChunk))).AsMemory(0, length);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _buffer, null) is { } rented)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
