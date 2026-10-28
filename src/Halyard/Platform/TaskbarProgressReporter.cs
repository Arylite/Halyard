using Halyard.Core.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Halyard.Platform;

/// <summary>Mirrors overall download progress on the taskbar button.</summary>
public sealed class TaskbarProgressReporter(
    DownloadActivity activity,
    TaskbarProgress taskbar,
    DispatcherQueue dispatcherQueue,
    ILogger<TaskbarProgressReporter> logger) : IBackgroundStartupTask, IDisposable
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        activity.Changed += OnActivityChanged;
        Apply(activity.Current);
        return Task.CompletedTask;
    }

    private void OnActivityChanged(object? sender, DownloadActivitySnapshot snapshot) =>
        dispatcherQueue.TryEnqueue(() => Apply(snapshot));

    private void Apply(DownloadActivitySnapshot snapshot)
    {
        try
        {
            if (snapshot.ActiveCount == 0)
            {
                taskbar.Clear();
            }
            else if (snapshot.Fraction is { } fraction)
            {
                taskbar.Show(fraction, snapshot.IsPaused);
            }
            else
            {
                taskbar.ShowIndeterminate();
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            // No taskbar (e.g. Explorer restarting); the next update tries again.
            logger.LogDebug(ex, "Taskbar progress unavailable");
        }
    }

    public void Dispose() => activity.Changed -= OnActivityChanged;
}
