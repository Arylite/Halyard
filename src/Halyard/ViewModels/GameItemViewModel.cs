using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Halyard.Core.Auth;
using Halyard.Core.Downloads;
using Halyard.Core.Installs;
using Halyard.Core.Launching;
using Halyard.Core.Library;
using Halyard.Formatting;
using Halyard.Models.Downloads;
using Halyard.Models.Library;
using Halyard.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Halyard.ViewModels;

/// <summary>A library card: what state the game is in, and the actions that make sense for it.</summary>
public sealed partial class GameItemViewModel : ObservableObject
{
    /// <summary>Covers are decoded at card size, not at their (often 4K) source size.</summary>
    private const int CoverDecodeWidth = 360;

    private readonly IGameLauncher _launcher;
    private readonly ILibraryService _library;
    private readonly IDownloadQueue _queue;
    private readonly IGameUninstaller _uninstaller;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly IPickerService _pickers;
    private readonly INotificationService _notifications;
    private readonly INavigationService _navigation;
    private readonly IUiDispatcher _dispatcher;
    private DownloadJob? _job;

    public GameItemViewModel(
        Game game,
        IGameLauncher launcher,
        ILibraryService library,
        IDownloadQueue queue,
        IGameUninstaller uninstaller,
        IDialogService dialogs,
        IShellService shell,
        IPickerService pickers,
        INotificationService notifications,
        INavigationService navigation,
        IUiDispatcher dispatcher)
    {
        _launcher = launcher;
        _library = library;
        _queue = queue;
        _uninstaller = uninstaller;
        _dialogs = dialogs;
        _shell = shell;
        _pickers = pickers;
        _notifications = notifications;
        _navigation = navigation;
        _dispatcher = dispatcher;

        Game = game;
        Cover = CreateCover(game.CoverImageUrl);
        IsRunning = launcher.IsRunning(game.Id);
        AttachJob(queue.FindActive(game.Id));
    }

    public Game Game { get; private set; }

    public string Title => Game.Title;

    public BitmapImage? Cover { get; private set; }

    public bool ShowInitials => Cover is null;

    public string Initials => string.Concat(
        Game.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpperInvariant(word[0])));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlay), nameof(ShowStop), nameof(ShowUpdate), nameof(ShowViewDownload), nameof(ShowPrimaryAction), nameof(PrimaryActionText), nameof(PrimaryActionCommand), nameof(ShowSecondaryAction), nameof(SecondaryActionText), nameof(SecondaryActionCommand), nameof(StatusText), nameof(CanVerify), nameof(CanUninstall), nameof(HasMoreActions))]
    public partial bool IsRunning { get; set; }

    private bool IsBusy => _job is not null;

    public bool ShowInstall => Game.IsDownloadable && !Game.IsInstalled && !IsBusy;

    public bool ShowPlay => Game.IsInstalled && !Game.HasUpdate && !IsRunning && !IsBusy;

    public bool ShowUpdate => Game.HasUpdate && !IsRunning && !IsBusy;

    public bool ShowStop => IsRunning;

    public bool ShowProgress => IsBusy;

    public bool ShowViewDownload => IsBusy && !IsRunning;

    // The tile shows one main button (install, play or update) and one secondary (stop or the
    // running download): two buttons per tile instead of five, most of them collapsed.
    public bool ShowPrimaryAction => ShowInstall || ShowPlay || ShowUpdate;

    public string PrimaryActionText => ShowPlay ? "Play" : ShowUpdate ? "Update" : "Install";

    public ICommand PrimaryActionCommand => ShowPlay ? PlayCommand : ShowUpdate ? InstallUpdateCommand : InstallCommand;

    public bool ShowSecondaryAction => ShowStop || ShowViewDownload;

    public string SecondaryActionText => ShowStop ? "Stop" : "View download";

    public ICommand SecondaryActionCommand => ShowStop ? StopCommand : ViewDownloadCommand;

    public double ProgressPercent => (_job?.Progress.Fraction ?? 0) * 100;

    public bool IsProgressIndeterminate => _job is { State: DownloadState.Running, Progress.Fraction: null };

    public bool IsProgressPaused => _job is { State: DownloadState.Paused };

    public bool CanVerify => Game.Installation is { Source: InstallationSource.Halyard } && !IsBusy && !IsRunning;

    public bool CanUninstall => Game.Installation is { Source: not InstallationSource.EpicLauncher } && !IsBusy && !IsRunning;

    public bool CanOpenFolder => Game.IsInstalled;

    /// <summary>The overflow menu only shows when it has at least one entry.</summary>
    public bool HasMoreActions => CanOpenFolder || CanVerify || CanUninstall || ShowInstall;

    public string UninstallText => Game.Installation?.Source == InstallationSource.Manual ? "Remove from library" : "Uninstall";

    public string StatusText
    {
        get
        {
            if (_job is { } job)
            {
                return ProgressText.Format(job);
            }

            if (IsRunning)
            {
                return "Playing";
            }

            return Game.Installation switch
            {
                null => Game.IsDownloadable ? "Not installed" : "Unavailable",
                _ when Game.HasUpdate => $"Update available - {Game.LatestVersion}",
                { Source: InstallationSource.EpicLauncher } => "Installed by the Epic Games Launcher",
                { Source: InstallationSource.Manual } => "Added manually",
                { InstallSize: > 0 } installation => WithPlayTime($"Installed - {ByteSize.Format(installation.InstallSize)}"),
                _ => WithPlayTime("Installed"),
            };
        }
    }

    private string WithPlayTime(string status) =>
        Game.PlayTime >= TimeSpan.FromMinutes(1) ? $"{status} - {Duration.Format(Game.PlayTime)} played" : status;

    /// <summary>Applies fresh library data without recreating the card.</summary>
    public void Apply(Game game)
    {
        if (game == Game)
        {
            return;
        }

        var previous = Game;
        Game = game;

        // Only touch what changed: re-binding the cover makes the image reload and the grid stutter.
        if (game.CoverImageUrl != previous.CoverImageUrl)
        {
            Cover = CreateCover(game.CoverImageUrl);
            OnPropertyChanged(nameof(Cover));
            OnPropertyChanged(nameof(ShowInitials));
        }

        if (game.Title != previous.Title)
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Initials));
        }

        RaiseStateChanged();
    }

    public void AttachJob(DownloadJob? job)
    {
        if (_job is not null)
        {
            _job.Changed -= OnJobChanged;
        }

        _job = job is { IsFinished: false } ? job : null;
        if (_job is not null)
        {
            _job.Changed += OnJobChanged;
        }

        RaiseStateChanged();
    }

    /// <summary>Everything that depends on install, job or running state; never the cover or title.</summary>
    private void RaiseStateChanged()
    {
        foreach (var name in StateProperties)
        {
            OnPropertyChanged(name);
        }
    }

    private static readonly string[] StateProperties =
    [
        nameof(ShowInstall),
        nameof(ShowPlay),
        nameof(ShowUpdate),
        nameof(ShowStop),
        nameof(ShowProgress),
        nameof(ShowViewDownload),
        nameof(ShowPrimaryAction),
        nameof(PrimaryActionText),
        nameof(PrimaryActionCommand),
        nameof(ShowSecondaryAction),
        nameof(SecondaryActionText),
        nameof(SecondaryActionCommand),
        nameof(ProgressPercent),
        nameof(IsProgressIndeterminate),
        nameof(IsProgressPaused),
        nameof(CanVerify),
        nameof(CanUninstall),
        nameof(CanOpenFolder),
        nameof(HasMoreActions),
        nameof(UninstallText),
        nameof(StatusText),
    ];

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (await _dialogs.ShowInstallAsync(Game) is { } request)
        {
            AttachJob(_queue.Enqueue(request));
        }
    }

    [RelayCommand]
    private async Task PlayAsync()
    {
        try
        {
            await _launcher.LaunchAsync(Game);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or EpicAuthException or HttpRequestException or Win32Exception)
        {
            _notifications.Show($"Couldn't launch {Title}", ex.Message, InfoBarSeverity.Error);
        }
    }

    /// <summary>Asks first: one stray click would otherwise kill a game mid-match.</summary>
    [RelayCommand]
    private async Task StopAsync()
    {
        if (await _dialogs.ConfirmAsync(
            $"Stop {Title}?",
            "The game closes right away; anything unsaved is lost.",
            "Stop game"))
        {
            _launcher.Stop(Game.Id);
        }
    }

    /// <summary>While an install runs, the card's button leads to where it can be paused or canceled.</summary>
    [RelayCommand]
    private void ViewDownload() => _navigation.NavigateTo(PageKeys.Downloads);

    [RelayCommand]
    private void InstallUpdate() => Enqueue(InstallKind.Update);

    [RelayCommand]
    private void Verify() => Enqueue(InstallKind.Repair);

    [RelayCommand]
    private async Task UninstallAsync()
    {
        var isManual = Game.Installation?.Source == InstallationSource.Manual;
        var confirmed = await _dialogs.ConfirmAsync(
            isManual ? $"Remove {Title}?" : $"Uninstall {Title}?",
            isManual
                ? "It disappears from the library; its files are left untouched."
                : $"Every file in {Game.Installation?.InstallDirectory} will be deleted.",
            isManual ? "Remove" : "Uninstall");

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _uninstaller.UninstallAsync(Game.Id, CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _notifications.Show($"Couldn't uninstall {Title}", ex.Message, InfoBarSeverity.Error);
        }
    }

    /// <summary>Adopts a copy of the game already on disk: verified, then only missing files are downloaded.</summary>
    [RelayCommand]
    private async Task ImportAsync()
    {
        if (await _pickers.PickFolderAsync() is { } folder &&
            await _dialogs.ShowInstallAsync(Game, folder) is { } request)
        {
            AttachJob(_queue.Enqueue(request));
        }
    }

    [RelayCommand]
    private async Task EditLaunchOptionsAsync()
    {
        if (await _dialogs.EditLaunchOptionsAsync(Game) is { } options)
        {
            await _library.UpdateAsync(Game.Id, game => game with
            {
                CustomArguments = options.CustomArguments,
                LaunchOffline = options.LaunchOffline,
            });
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (Game.Installation is { } installation)
        {
            _shell.OpenFolder(installation.InstallDirectory);
        }
    }

    private void Enqueue(InstallKind kind)
    {
        if (Game.Installation is { } installation)
        {
            AttachJob(_queue.Enqueue(new InstallRequest(Game.Id, Title, installation.InstallDirectory, installation.InstallTags, kind)));
        }
    }

    private void OnJobChanged(object? sender, EventArgs e) => _dispatcher.Post(() =>
    {
        if (_job is { IsFinished: true })
        {
            AttachJob(null);
            return;
        }

        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(IsProgressIndeterminate));
        OnPropertyChanged(nameof(IsProgressPaused));
        OnPropertyChanged(nameof(StatusText));
    });

    private static BitmapImage? CreateCover(string? url) => CoverImage.Create(url, CoverDecodeWidth);
}
