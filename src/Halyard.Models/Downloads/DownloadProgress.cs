namespace Halyard.Models.Downloads;

public enum InstallPhase
{
    Preparing,
    Verifying,
    Downloading,
    Finalizing,
}

/// <param name="Completed">Bytes done in the current phase (downloaded, or hashed when verifying).</param>
/// <param name="Total">Bytes the current phase will process.</param>
/// <param name="BytesPerSecond">Network speed while downloading, hashing speed while verifying.</param>
public readonly record struct DownloadProgress(InstallPhase Phase, long Completed, long Total, double BytesPerSecond)
{
    /// <summary>Speed at which files are being written to disk.</summary>
    public double DiskBytesPerSecond { get; init; }

    public int ActiveConnections { get; init; }

    /// <summary>The experimental multi-CDN mode is spreading and hedging this download.</summary>
    public bool MultiCdn { get; init; }

    private readonly IReadOnlyList<CdnStat>? _cdns;

    /// <summary>Per-CDN throughput of the running download; empty outside the download phase.</summary>
    /// <remarks>Backed by a nullable field: <c>default(DownloadProgress)</c> skips initializers.</remarks>
    public IReadOnlyList<CdnStat> Cdns
    {
        get => _cdns ?? [];
        init => _cdns = value;
    }

    public double? Fraction => Total > 0 ? Math.Clamp((double)Completed / Total, 0, 1) : null;

    public TimeSpan? Remaining => BytesPerSecond > 0 && Total > Completed
        ? TimeSpan.FromSeconds((Total - Completed) / BytesPerSecond)
        : null;
}

/// <param name="BytesPerSecond">Smoothed throughput of a single connection to this CDN.</param>
/// <param name="Bytes">Bytes served by this CDN so far.</param>
public sealed record CdnStat(string Host, double BytesPerSecond, long Bytes, int Failures);
