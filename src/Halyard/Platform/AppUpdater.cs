using System.Reflection;
using Halyard.Core.Hosting;
using Halyard.Core.Settings;
using Halyard.UI;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Velopack;
using Velopack.Sources;

namespace Halyard.Platform;

public enum AppUpdateState
{
    /// <summary>Running from a build folder, not an install: updates don't apply.</summary>
    NotInstalled,
    Checking,
    UpToDate,
    Downloading,
    ReadyToRestart,
    Failed,
}

/// <summary>
/// Keeps Halyard itself up to date from the project's GitHub releases (Velopack). Checks once at
/// startup and on demand; a downloaded update applies on restart.
/// </summary>
public sealed class AppUpdater : IBackgroundStartupTask
{
    /// <summary>Velopack channel of beta builds; stable builds use Velopack's default channel.</summary>
    public const string BetaChannel = "beta";

    private readonly ISettingsService _settings;
    private readonly INotificationService _notifications;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<AppUpdater> _logger;
    private UpdateManager _manager;
    private UpdateInfo? _pending;

    public AppUpdater(ISettingsService settings, INotificationService notifications, IUiDispatcher dispatcher, ILogger<AppUpdater> logger)
    {
        _settings = settings;
        _notifications = notifications;
        _dispatcher = dispatcher;
        _logger = logger;
        _manager = CreateManager();
        State = _manager.IsInstalled ? AppUpdateState.UpToDate : AppUpdateState.NotInstalled;
    }

    /// <summary>Built per check, so switching between stable and beta takes effect on the next one.</summary>
    private UpdateManager CreateManager()
    {
        var beta = _settings.Current.ReceiveBetaUpdates;
        return new UpdateManager(
            new GithubSource(RepositoryUrl, accessToken: null, prerelease: beta),
            new UpdateOptions { ExplicitChannel = beta ? BetaChannel : null });
    }

    public static string RepositoryUrl { get; } =
        typeof(AppUpdater).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "RepositoryUrl")?.Value
        ?? string.Empty;

    public string CurrentVersion =>
        _manager.CurrentVersion?.ToString() ?? typeof(AppUpdater).Assembly.GetName().Version?.ToString(3) ?? "dev";

    public AppUpdateState State { get; private set; }

    public string? AvailableVersion { get; private set; }

    /// <summary>Raised on the UI thread.</summary>
    public event EventHandler? StateChanged;

    public Task RunAsync(CancellationToken cancellationToken) => CheckAsync();

    public async Task CheckAsync()
    {
        if (!_manager.IsInstalled || State is AppUpdateState.Checking or AppUpdateState.Downloading or AppUpdateState.ReadyToRestart)
        {
            return;
        }

        try
        {
            SetState(AppUpdateState.Checking);
            _manager = CreateManager();
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null)
            {
                SetState(AppUpdateState.UpToDate);
                return;
            }

            AvailableVersion = update.TargetFullRelease.Version.ToString();
            SetState(AppUpdateState.Downloading);
            await _manager.DownloadUpdatesAsync(update);

            _pending = update;
            SetState(AppUpdateState.ReadyToRestart);
            _dispatcher.Post(() => _notifications.Show(
                $"Halyard {AvailableVersion} is ready",
                "Restart from Settings to finish updating.",
                InfoBarSeverity.Informational));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed");
            SetState(AppUpdateState.Failed);
        }
    }

    public void RestartToUpdate()
    {
        if (_pending is not null)
        {
            _logger.LogInformation("Restarting to install Halyard {Version}", AvailableVersion);
            _manager.ApplyUpdatesAndRestart(_pending.TargetFullRelease);
        }
    }

    private void SetState(AppUpdateState state)
    {
        State = state;
        _dispatcher.Post(() => StateChanged?.Invoke(this, EventArgs.Empty));
    }
}
