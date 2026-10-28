using System.Security.Cryptography;
using System.Threading.Channels;
using Halyard.Models.Installs;

namespace Halyard.Services.Installs;

/// <summary>
/// Runs an <see cref="InstallPlan"/>: a dispatcher hands chunk downloads to parallel workers in plan
/// order, each holding a memory permit until its last part is written; a single writer streams the
/// files to disk in order, hashing them as it goes. Permits are taken in plan order, so the chunk the
/// writer waits for always gets one: no deadlock, bounded memory.
/// </summary>
public sealed class InstallPipeline(ChunkDownloader downloader)
{
    private const int WriteBufferSize = 1 << 20;

    internal async Task RunAsync(
        InstallPlan plan,
        BuildManifest manifest,
        string installDirectory,
        IReadOnlyList<Uri> chunkBaseUris,
        int workers,
        int maxBufferedChunks,
        Func<bool> multiCdn,
        InstallJournal journal,
        InstallProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var permits = new SemaphoreSlim(maxBufferedChunks);
        var queue = Channel.CreateBounded<ChunkTask>(new BoundedChannelOptions(workers * 2) { SingleWriter = true });
        var cdns = new CdnBalancer(chunkBaseUris, multiCdn);
        tracker.CdnStats = cdns.Snapshot;
        tracker.MultiCdn = () => cdns.IsSpreading;

        Task[] background =
        [
            DispatchAsync(plan.Tasks, permits, queue.Writer, stop.Token),
            .. Enumerable.Range(0, workers).Select(_ =>
                DownloadLoopAsync(queue.Reader, manifest.ChunkDirectory, cdns, tracker, stop.Token)),
        ];

        try
        {
            await WriteFilesAsync(plan, installDirectory, permits, journal, tracker, stop.Token);
        }
        finally
        {
            await stop.CancelAsync();
            try
            {
                await Task.WhenAll(background);
            }
            catch
            {
                // Background failures already reached the writer through the chunk it was awaiting.
            }

            ReleaseUnwritten(plan);
        }
    }

    private static async Task DispatchAsync(
        IReadOnlyList<ChunkTask> tasks,
        SemaphoreSlim permits,
        ChannelWriter<ChunkTask> queue,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var task in tasks)
            {
                await permits.WaitAsync(cancellationToken);
                await queue.WriteAsync(task, cancellationToken);
            }
        }
        finally
        {
            queue.TryComplete();
        }
    }

    private async Task DownloadLoopAsync(
        ChannelReader<ChunkTask> queue,
        string chunkDirectory,
        CdnBalancer cdns,
        IDownloadMeter meter,
        CancellationToken cancellationToken)
    {
        await foreach (var task in queue.ReadAllAsync(cancellationToken))
        {
            try
            {
                var data = await downloader.DownloadAsync(task.Chunk, chunkDirectory, cdns, meter, cancellationToken);
                task.Data.TrySetResult(data);
            }
            catch (Exception ex)
            {
                task.Data.TrySetException(ex);
                throw;
            }
        }
    }

    private static async Task WriteFilesAsync(
        InstallPlan plan,
        string installDirectory,
        SemaphoreSlim permits,
        InstallJournal journal,
        InstallProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        foreach (var planned in plan.Files)
        {
            var path = Path.Combine(installDirectory, planned.File.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            await using (var stream = new FileStream(
                path, FileMode.Create, FileAccess.Write, FileShare.None, WriteBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                // Reserving the full size up front keeps large files contiguous on disk.
                stream.SetLength(planned.File.Size);

                foreach (var part in planned.Parts)
                {
                    var chunk = await part.Task.Data.Task.WaitAsync(cancellationToken);
                    var slice = chunk.Memory.Slice(part.Offset, part.Size);
                    await stream.WriteAsync(slice, cancellationToken);
                    hash.AppendData(slice.Span);
                    tracker.AddWritten(part.Size);
                    Release(part.Task, permits);
                }
            }

            if (!hash.GetHashAndReset().AsSpan().SequenceEqual(planned.File.Sha1))
            {
                throw new InvalidDataException($"{planned.File.Path} doesn't match the manifest after download.");
            }

            await journal.MarkCompletedAsync(planned.File.Path, cancellationToken);
        }
    }

    private static void Release(ChunkTask task, SemaphoreSlim permits)
    {
        if (Interlocked.Decrement(ref task.RemainingUses) == 0)
        {
            task.Data.Task.Result.Dispose();
            permits.Release();
        }
    }

    /// <summary>After a failure or cancel, returns buffers of chunks that were downloaded but never written.</summary>
    private static void ReleaseUnwritten(InstallPlan plan)
    {
        foreach (var task in plan.Tasks)
        {
            if (task.Data.Task.IsCompletedSuccessfully && task.RemainingUses > 0)
            {
                task.Data.Task.Result.Dispose();
            }
        }
    }
}
