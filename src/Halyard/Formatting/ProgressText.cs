using Halyard.Models.Downloads;

namespace Halyard.Formatting;

/// <summary>Human-readable status of an install job, shared by the library cards and the queue.</summary>
public static class ProgressText
{
    public static string Format(DownloadJob job) => job.State switch
    {
        DownloadState.Queued => "Queued",
        DownloadState.Running => FormatRunning(job.Progress),
        DownloadState.Paused => job.Progress.Fraction is { } fraction ? $"Paused - {fraction:P0}" : "Paused",
        DownloadState.Completed => job.Request.Kind switch
        {
            InstallKind.Update => "Updated",
            InstallKind.Repair => "Repaired",
            _ => "Installed",
        },
        DownloadState.Failed => $"Failed: {job.Error}",
        DownloadState.Canceled => "Canceled",
        _ => string.Empty,
    };

    private static string FormatRunning(DownloadProgress progress) => progress.Phase switch
    {
        InstallPhase.Preparing => "Preparing...",
        InstallPhase.Finalizing => "Finishing...",
        InstallPhase.Verifying => $"Verifying files - {progress.Fraction ?? 0:P0}",
        _ => FormatTransfer(progress),
    };

    private static string FormatTransfer(DownloadProgress progress)
    {
        var amount = $"{ByteSize.Format(progress.Completed)} of {ByteSize.Format(progress.Total)}";
        if (progress.BytesPerSecond <= 0)
        {
            return amount;
        }

        var speed = ByteSize.FormatSpeed(progress.BytesPerSecond);
        return progress.Remaining is { } remaining
            ? $"{amount} - {speed} - {Duration.Format(remaining)} left"
            : $"{amount} - {speed}";
    }
}
