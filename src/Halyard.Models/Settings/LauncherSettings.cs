namespace Halyard.Models.Settings;

public sealed record LauncherSettings
{
    /// <summary>Self-updates follow the beta channel (GitHub pre-releases) instead of stable releases.</summary>
    public bool ReceiveBetaUpdates { get; init; }

    /// <summary>The first-run welcome was shown (or skipped).</summary>
    public bool HasCompletedWelcome { get; init; }

    /// <summary>Start (hidden, in the tray) when the user signs in to Windows.</summary>
    public bool LaunchAtStartup { get; init; }

    /// <summary>Closing the window keeps Halyard running in the tray, so downloads continue.</summary>
    public bool CloseToTray { get; init; } = true;

    /// <summary>Windows notifications when installs finish or fail.</summary>
    public bool ShowNotifications { get; init; } = true;

    /// <summary>Downloads pause while a game runs (no lag spikes) and resume when it exits.</summary>
    public bool PauseDownloadsWhilePlaying { get; init; } = true;

    /// <summary>Queues updates for Halyard-installed games as soon as Epic publishes a new build.</summary>
    public bool AutoUpdateGames { get; init; } = true;

    /// <summary>Folder new games are installed into (one sub-folder per game).</summary>
    public string InstallDirectory { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games");

    /// <summary>Games installing at the same time. Each install is already parallel internally.</summary>
    public int MaxConcurrentDownloads { get; init; } = 1;

    /// <summary>Parallel chunk downloads per install.</summary>
    public int DownloadWorkers { get; init; } = 16;

    /// <summary>Network speeds shown in megabits per second (Mb/s) instead of megabytes (MB/s).</summary>
    public bool ShowSpeedInBits { get; init; }

    /// <summary>Download speed cap in MB/s (fractions allowed); 0 means unlimited.</summary>
    public double BandwidthLimitMegabytesPerSecond { get; init; }

    /// <summary>Memory kept for downloaded chunks waiting to be written, in MB.</summary>
    public int ChunkCacheMegabytes { get; init; } = 1024;

    /// <summary>
    /// Experimental: spreads chunks across every CDN by measured speed, and re-requests slow chunks
    /// from another CDN (first answer wins).
    /// </summary>
    public bool ExperimentalMultiCdn { get; init; }
}
