using System.Buffers;
using System.Diagnostics;
using Halyard.Core.Downloads;
using Halyard.Models.Installs;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Installs;

/// <summary>
/// Fetches one chunk. Transient failures are retried on another CDN; with hedging on, a chunk that
/// takes too long is also requested from a second CDN and the first complete answer wins.
/// </summary>
public sealed class ChunkDownloader(HttpClient http, IBandwidthLimiter limiter, ILogger<ChunkDownloader> logger)
{
    private const int MaxAttempts = 5;
    private const int ReadSize = 1 << 16;

    internal async Task<PooledChunk> DownloadAsync(
        ManifestChunk chunk,
        string chunkDirectory,
        CdnBalancer cdns,
        IDownloadMeter meter,
        CancellationToken cancellationToken)
    {
        var relativePath = chunk.GetRelativePath(chunkDirectory);

        for (var attempt = 1; ; attempt++)
        {
            var cdn = cdns.Pick(attempt);
            try
            {
                // Hedging belongs to the multi-CDN mode, and follows it live.
                return cdns.IsSpreading
                    ? await FetchHedgedAsync(chunk, relativePath, cdns, cdn, meter, cancellationToken)
                    : await FetchAsync(chunk, relativePath, cdns, cdn, meter, cancellationToken);
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex) && !cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Chunk {Chunk} failed on attempt {Attempt}, retrying", chunk.Guid, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt * attempt), cancellationToken);
            }
        }
    }

    private async Task<PooledChunk> FetchHedgedAsync(
        ManifestChunk chunk,
        string relativePath,
        CdnBalancer cdns,
        int cdn,
        IDownloadMeter meter,
        CancellationToken cancellationToken)
    {
        using var race = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var primary = FetchAsync(chunk, relativePath, cdns, cdn, meter, race.Token);

        if (await Task.WhenAny(primary, Task.Delay(cdns.HedgeDelay, race.Token)) == primary)
        {
            // Stops the hedge timer: at full speed, hundreds of chunks a second would each leave one pending.
            await race.CancelAsync();
            return await primary;
        }

        var backup = FetchAsync(chunk, relativePath, cdns, cdns.Pick(1, exclude: cdn), meter, race.Token);
        List<Task<PooledChunk>> pending = [primary, backup];

        Exception? lastError = null;
        while (pending.Count > 0)
        {
            var finished = await Task.WhenAny(pending);
            pending.Remove(finished);

            if (finished.IsCompletedSuccessfully)
            {
                await race.CancelAsync();
                foreach (var loser in pending)
                {
                    // A loser that completes anyway still holds a pooled buffer; a canceled one is observed.
                    _ = loser.ContinueWith(
                        task =>
                        {
                            if (task.IsCompletedSuccessfully)
                            {
                                task.Result.Dispose();
                            }
                            else
                            {
                                _ = task.Exception;
                            }
                        },
                        TaskScheduler.Default);
                }

                return finished.Result;
            }

            lastError = finished.Exception?.InnerException;
        }

        throw lastError ?? new HttpRequestException($"Chunk {chunk.Guid} failed on every CDN.");
    }

    private async Task<PooledChunk> FetchAsync(
        ManifestChunk chunk,
        string relativePath,
        CdnBalancer cdns,
        int cdn,
        IDownloadMeter meter,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        long received = 0;
        meter.ConnectionOpened();
        try
        {
            using var response = await http.GetAsync(new Uri(cdns[cdn], relativePath), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var capacity = (int)Math.Max(chunk.FileSize, response.Content.Headers.ContentLength ?? 0);

            // One spare byte so an oversized body is detected instead of silently truncated.
            var buffer = ArrayPool<byte>.Shared.Rent(capacity + 1);
            try
            {
                await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                var length = 0;
                int read;
                while ((read = await body.ReadAsync(buffer.AsMemory(length, Math.Min(ReadSize, buffer.Length - length)), cancellationToken)) > 0)
                {
                    await limiter.WaitAsync(read, cancellationToken);
                    length += read;
                    received += read;
                    meter.AddDownloaded(read);

                    if (length > capacity)
                    {
                        throw new InvalidDataException($"Chunk {chunk.Guid} is larger than announced.");
                    }
                }

                var decoded = ChunkDecoder.Decode(buffer, length, chunk);
                cdns.Report(cdn, received, Stopwatch.GetElapsedTime(started), success: true);
                return decoded;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (Exception ex)
        {
            // Bytes of a failed or abandoned request don't count towards progress.
            meter.AddDownloaded(-received);
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                cdns.Abandon(cdn, received, Stopwatch.GetElapsedTime(started));
            }
            else
            {
                cdns.Report(cdn, 0, Stopwatch.GetElapsedTime(started), success: false);
            }

            throw;
        }
        finally
        {
            meter.ConnectionClosed();
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or IOException or InvalidDataException ||
        (ex is TaskCanceledException { InnerException: TimeoutException });
}
