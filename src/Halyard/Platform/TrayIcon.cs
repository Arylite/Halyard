using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Halyard.Core.Downloads;
using Halyard.Core.Hosting;
using Halyard.Models.Downloads;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Halyard.Platform;

/// <summary>
/// Notification area icon: click to open, right-click for the menu. Its tooltip follows the
/// running download, so progress is visible while the window is closed.
/// </summary>
public sealed class TrayIcon(
    WindowPresenter window,
    IDownloadQueue queue,
    DownloadActivity activity,
    DispatcherQueue dispatcherQueue) : IBackgroundStartupTask, IDisposable
{
    private const string AppName = "Halyard";

    private TaskbarIcon? _icon;

    public Task RunAsync(CancellationToken cancellationToken)
    {
        var menu = new MenuFlyout();
        menu.Items.Add(Item("Open Halyard", window.Show));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Pause all downloads", PauseAll));
        menu.Items.Add(Item("Resume all downloads", ResumeAll));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Exit", window.Exit));

        _icon = new TaskbarIcon
        {
            ToolTipText = AppName,
            IconSource = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"))),
            ContextMenuMode = ContextMenuMode.PopupMenu,
            ContextFlyout = menu,
            NoLeftClickDelay = true,
            LeftClickCommand = new RelayCommand(window.Show),
        };
        _icon.ForceCreate(false);

        activity.Changed += OnActivityChanged;
        return Task.CompletedTask;
    }

    /// <summary>Shown by Windows as a regular notification in the notification center.</summary>
    public void ShowNotification(string title, string message) => dispatcherQueue.TryEnqueue(() =>
        _icon?.ShowNotification(title, message, H.NotifyIcon.Core.NotificationIcon.Info));

    private void OnActivityChanged(object? sender, DownloadActivitySnapshot snapshot) => dispatcherQueue.TryEnqueue(() =>
    {
        if (_icon is not null)
        {
            _icon.ToolTipText = snapshot.Label is null ? AppName : $"{AppName} - {snapshot.Label}";
        }
    });

    private void PauseAll()
    {
        // Queued jobs first: pausing a running one frees its slot, and the queue would start the next.
        foreach (var job in queue.Jobs
            .Where(job => job.State is DownloadState.Running or DownloadState.Queued)
            .OrderBy(job => job.State == DownloadState.Running)
            .ToList())
        {
            queue.Pause(job);
        }
    }

    private void ResumeAll()
    {
        foreach (var job in queue.Jobs.Where(job => job.State is DownloadState.Paused or DownloadState.Failed))
        {
            queue.Resume(job);
        }
    }

    private static MenuFlyoutItem Item(string text, Action action) => new() { Text = text, Command = new RelayCommand(action) };

    public void Dispose()
    {
        activity.Changed -= OnActivityChanged;
        _icon?.Dispose();
    }
}
