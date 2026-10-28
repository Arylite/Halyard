using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Halyard.Platform;

namespace Halyard.ViewModels;

public sealed partial class AboutViewModel : ObservableObject
{
    private readonly AppUpdater _updater;

    public AboutViewModel(AppUpdater updater)
    {
        _updater = updater;
        updater.StateChanged += (_, _) =>
        {
            OnPropertyChanged(string.Empty);
            CheckForUpdatesCommand.NotifyCanExecuteChanged();
        };
    }

    public string VersionText => $"Halyard {_updater.CurrentVersion}";

    public string UpdateStatus => _updater.State switch
    {
        AppUpdateState.NotInstalled => "Development build: updates apply to installed releases only.",
        AppUpdateState.Checking => "Checking for updates...",
        AppUpdateState.Downloading => $"Downloading version {_updater.AvailableVersion}...",
        AppUpdateState.ReadyToRestart => $"Version {_updater.AvailableVersion} is ready. Restart to finish updating.",
        AppUpdateState.Failed => "Couldn't check for updates. See the logs.",
        _ => "You're up to date.",
    };

    public bool IsBusy => _updater.State is AppUpdateState.Checking or AppUpdateState.Downloading;

    public bool CanRestart => _updater.State == AppUpdateState.ReadyToRestart;

    // A development build can't update itself: no button that could never be pressed.
    public bool CanCheck => _updater.State is not (AppUpdateState.ReadyToRestart or AppUpdateState.NotInstalled);

    private bool CanCheckNow => _updater.State is AppUpdateState.UpToDate or AppUpdateState.Failed;

    [RelayCommand(CanExecute = nameof(CanCheckNow))]
    private Task CheckForUpdatesAsync() => _updater.CheckAsync();

    [RelayCommand]
    private void RestartToUpdate() => _updater.RestartToUpdate();
}
