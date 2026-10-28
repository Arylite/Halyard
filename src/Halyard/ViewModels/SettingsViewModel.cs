using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Halyard.Core.Settings;
using Halyard.Models.Settings;
using Halyard.Platform;
using Halyard.UI;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.ViewModels;

/// <remarks>Numbers are <see cref="double"/> because that is what NumberBox binds to.</remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly StartupRegistration _startup;
    private readonly IPickerService _pickers;
    private readonly INotificationService _notifications;

    public SettingsViewModel(
        ISettingsService settings,
        StartupRegistration startup,
        IPickerService pickers,
        INotificationService notifications)
    {
        _settings = settings;
        _startup = startup;
        _pickers = pickers;
        _notifications = notifications;

        var current = settings.Current;

        // The registry is the truth for autostart: the user may have disabled it in Task Manager.
        LaunchAtStartup = startup.IsEnabled;
        CloseToTray = current.CloseToTray;
        ShowNotifications = current.ShowNotifications;
        PauseDownloadsWhilePlaying = current.PauseDownloadsWhilePlaying;
        AutoUpdateGames = current.AutoUpdateGames;
        InstallDirectory = current.InstallDirectory;
        MaxConcurrentDownloads = current.MaxConcurrentDownloads;
        DownloadWorkers = current.DownloadWorkers;
        BandwidthLimit = current.BandwidthLimitMegabytesPerSecond;
        ChunkCache = current.ChunkCacheMegabytes;
        ExperimentalMultiCdn = current.ExperimentalMultiCdn;
        ReceiveBetaUpdates = current.ReceiveBetaUpdates;

        // Like Windows Settings: every change applies on its own, no Save button.
        PropertyChanged += (_, _) => ScheduleSave();
    }

    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);
    private CancellationTokenSource? _pendingSave;

    [ObservableProperty]
    public partial bool LaunchAtStartup { get; set; }

    [ObservableProperty]
    public partial bool CloseToTray { get; set; }

    [ObservableProperty]
    public partial bool ShowNotifications { get; set; }

    [ObservableProperty]
    public partial bool PauseDownloadsWhilePlaying { get; set; }

    [ObservableProperty]
    public partial bool AutoUpdateGames { get; set; }

    [ObservableProperty]
    public partial bool ExperimentalMultiCdn { get; set; }

    [ObservableProperty]
    public partial bool ReceiveBetaUpdates { get; set; }

    [ObservableProperty]
    public partial string InstallDirectory { get; set; }

    [ObservableProperty]
    public partial double MaxConcurrentDownloads { get; set; }

    [ObservableProperty]
    public partial double DownloadWorkers { get; set; }

    [ObservableProperty]
    public partial double BandwidthLimit { get; set; }

    [ObservableProperty]
    public partial double ChunkCache { get; set; }

    [RelayCommand]
    private async Task BrowseInstallDirectoryAsync()
    {
        if (await _pickers.PickFolderAsync() is { } folder)
        {
            InstallDirectory = folder;
        }
    }

    /// <summary>Saves shortly after the last change, so typing in a box writes the file once.</summary>
    private async void ScheduleSave()
    {
        _pendingSave?.Cancel();
        var pending = _pendingSave = new CancellationTokenSource();
        try
        {
            await Task.Delay(SaveDelay, pending.Token);
            await SaveAsync();
        }
        catch (OperationCanceledException)
        {
            // A newer change superseded this one.
        }
    }

    private async Task SaveAsync()
    {
        var defaults = new LauncherSettings();
        var updated = _settings.Current with
        {
            LaunchAtStartup = LaunchAtStartup,
            CloseToTray = CloseToTray,
            ShowNotifications = ShowNotifications,
            PauseDownloadsWhilePlaying = PauseDownloadsWhilePlaying,
            AutoUpdateGames = AutoUpdateGames,
            InstallDirectory = string.IsNullOrWhiteSpace(InstallDirectory) ? defaults.InstallDirectory : InstallDirectory.Trim(),
            MaxConcurrentDownloads = ToInt(MaxConcurrentDownloads, 1, 4, defaults.MaxConcurrentDownloads),
            DownloadWorkers = ToInt(DownloadWorkers, 1, 64, defaults.DownloadWorkers),
            BandwidthLimitMegabytesPerSecond = ToInt(BandwidthLimit, 0, 10_000, 0),
            ChunkCacheMegabytes = ToInt(ChunkCache, 128, 8192, defaults.ChunkCacheMegabytes),
            ExperimentalMultiCdn = ExperimentalMultiCdn,
            ReceiveBetaUpdates = ReceiveBetaUpdates,
        };

        try
        {
            if (_startup.IsEnabled != LaunchAtStartup)
            {
                _startup.SetEnabled(LaunchAtStartup);
            }

            await _settings.SaveAsync(updated);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _notifications.Show("Couldn't save settings", ex.Message, InfoBarSeverity.Error);
        }
    }

    private static int ToInt(double value, int min, int max, int fallback) =>
        double.IsNaN(value) ? fallback : Math.Clamp((int)Math.Round(value), min, max);
}
