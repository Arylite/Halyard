namespace Halyard.Services.Installs;

/// <summary>What the chunk downloader reports while it works.</summary>
internal interface IDownloadMeter
{
    /// <summary>Bytes received; negative to take back bytes of a request that failed or lost a race.</summary>
    void AddDownloaded(long bytes);

    void ConnectionOpened();

    void ConnectionClosed();
}
