namespace Halyard.Models.Downloads;

public enum DownloadState
{
    Queued,
    Running,
    Paused,
    Completed,
    Failed,
    Canceled,
}
