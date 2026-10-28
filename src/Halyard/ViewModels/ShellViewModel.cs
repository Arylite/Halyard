using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Halyard.Core.Downloads;
using Halyard.Models.Downloads;
using Halyard.UI;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.ViewModels;

/// <summary>State of the main window chrome: navigation badges and the notification bar.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IDownloadQueue _downloads;
    private readonly IUiDispatcher _dispatcher;

    public ShellViewModel(IDownloadQueue downloads, INotificationService notifications, IUiDispatcher dispatcher)
    {
        _downloads = downloads;
        _dispatcher = dispatcher;

        NotificationTitle = string.Empty;
        NotificationMessage = string.Empty;

        downloads.JobAdded += OnDownloadsChanged;
        downloads.JobRemoved += OnDownloadsChanged;
        downloads.JobStateChanged += OnDownloadsChanged;
        notifications.Raised += (_, notification) => dispatcher.Post(() => Show(notification));

        UpdateActiveDownloads();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveDownloads))]
    public partial int ActiveDownloads { get; set; }

    public bool HasActiveDownloads => ActiveDownloads > 0;

    [ObservableProperty]
    public partial bool IsNotificationOpen { get; set; }

    [ObservableProperty]
    public partial string NotificationTitle { get; set; }

    [ObservableProperty]
    public partial string NotificationMessage { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity NotificationSeverity { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotificationAction))]
    public partial string? NotificationActionText { get; set; }

    public bool HasNotificationAction => NotificationActionText is not null;

    private Action? _notificationAction;

    [RelayCommand]
    private void RunNotificationAction()
    {
        IsNotificationOpen = false;
        _notificationAction?.Invoke();
    }

    private int _notificationGeneration;

    /// <summary>Set by the view while the pointer is over the notification: it then stays open.</summary>
    public bool IsNotificationHovered { get; set; }

    private async void Show(Notification notification)
    {
        NotificationTitle = notification.Title;
        NotificationMessage = notification.Message;
        NotificationSeverity = notification.Severity;
        NotificationActionText = notification.Action is null ? null : notification.ActionText;
        _notificationAction = notification.Action;
        IsNotificationOpen = true;

        // Close on its own, unless a newer notification replaced it or the user is reading it.
        var generation = ++_notificationGeneration;
        var lifetime = DisplayTime(notification);
        do
        {
            await Task.Delay(lifetime);
            lifetime = TimeSpan.FromSeconds(2);
        }
        while (IsNotificationHovered && generation == _notificationGeneration);

        if (generation == _notificationGeneration)
        {
            IsNotificationOpen = false;
        }
    }

    private static TimeSpan DisplayTime(Notification notification) => notification switch
    {
        { Action: not null } => TimeSpan.FromSeconds(20),
        { Severity: InfoBarSeverity.Error or InfoBarSeverity.Warning } => TimeSpan.FromSeconds(10),
        _ => TimeSpan.FromSeconds(5),
    };

    private void OnDownloadsChanged(object? sender, DownloadJob job) => _dispatcher.Post(UpdateActiveDownloads);

    private void UpdateActiveDownloads() =>
        // Paused jobs count: they are still waiting for the user, and the badge says where.
        ActiveDownloads = _downloads.Jobs.Count(job => !job.IsFinished);
}
