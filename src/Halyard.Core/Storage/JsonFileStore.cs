using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Halyard.Core.Storage;

/// <summary>Reads and atomically writes a single JSON document.</summary>
public sealed class JsonFileStore<T>(string path, JsonTypeInfo<T> typeInfo)
{
    public async Task<T?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken);
    }

    public async Task WriteAsync(T value, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Write next to the target then swap, so a crash never leaves a truncated file behind.
        var temporaryPath = path + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, typeInfo, cancellationToken);
        }

        File.Move(temporaryPath, path, overwrite: true);
    }
}
