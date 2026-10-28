using System.Buffers;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Halyard.Models.Downloads;
using Halyard.Models.Installs;

namespace Halyard.Services.Installs;

/// <summary>Hashes installed files in parallel and reports those that are missing or damaged.</summary>
internal static class FileVerifier
{
    private const int BufferSize = 1 << 20;

    public static async Task<IReadOnlyList<ManifestFile>> FindDamagedAsync(
        string installDirectory,
        IReadOnlyList<ManifestFile> files,
        InstallProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        tracker.StartPhase(InstallPhase.Verifying, files.Sum(file => file.Size));

        var damaged = new ConcurrentDictionary<ManifestFile, bool>();
        var options = new ParallelOptions
        {
            // Mostly I/O bound: a few readers saturate an SSD without starving the UI.
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 2, 8),
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(files, options, async (file, token) =>
        {
            if (!await MatchesAsync(Path.Combine(installDirectory, file.Path), file, tracker, token))
            {
                damaged.TryAdd(file, true);
            }
        });

        return [.. files.Where(damaged.ContainsKey)];
    }

    private static async Task<bool> MatchesAsync(
        string path,
        ManifestFile file,
        InstallProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != file.Size)
        {
            tracker.Add(file.Size);
            return false;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);

            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                tracker.Add(read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return hash.GetHashAndReset().AsSpan().SequenceEqual(file.Sha1);
    }
}
