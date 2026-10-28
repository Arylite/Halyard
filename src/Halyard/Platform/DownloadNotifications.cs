using Halyard.Core.Downloads;
using Halyard.Core.Hosting;
using Halyard.Core.Settings;
using Halyard.Models.Downloads;

namespace Halyard.Platform;

/// <summary>Tells the user when an install finishes or fails, even with the window closed.</summary>
public sealed class DownloadNotifications(
    IDownloadQueue queue,
    ISettingsService settings,
    ToastNotifier toasts) : IBackgroundStartupTask, IDisposable
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        queue.JobStateChanged += OnJobStateChanged;
        return Task.CompletedTask;
    }

    private void OnJobStateChanged(object? sender, DownloadJob job)
    {
        if (!settings.Current.ShowNotifications)
        {
            return;
        }

        var name = job.Request.DisplayName;
        switch (job.State)
        {
            case DownloadState.Completed:
                toasts.Show(
                    job.Request.Kind switch
                    {
                        InstallKind.Update => $"{name} is up to date",
                        InstallKind.Repair => $"{name} was repaired",
                        _ => $"{name} is ready to play",
                    },
                    "Open Halyard to launch it.");
                break;

            case DownloadState.Failed:
                toasts.Show(
                    job.Request.Kind switch
                    {
                        InstallKind.Update => $"{name} could not be updated",
                        InstallKind.Repair => $"{name} could not be repaired",
                        _ => $"{name} could not be installed",
                    },
                    job.Error ?? "See the logs for details.");
                break;
        }
    }

    public void Dispose() => queue.JobStateChanged -= OnJobStateChanged;
}
