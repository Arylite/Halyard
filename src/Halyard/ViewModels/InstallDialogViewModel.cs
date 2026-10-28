using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Halyard.Core.Auth;
using Halyard.Core.Installs;
using Halyard.Core.Settings;
using Halyard.Formatting;
using Halyard.Models.Downloads;
using Halyard.Models.Installs;
using Halyard.Models.Library;
using Halyard.UI;

namespace Halyard.ViewModels;

/// <summary>Where to install, and which optional content, with sizes that follow every choice.</summary>
public sealed partial class InstallDialogViewModel : ObservableObject
{
    private const long MinimumVisibleSize = 1024 * 1024;

    private readonly Game _game;
    private readonly IInstallPreparer _preparer;
    private readonly IPickerService _pickers;
    private readonly bool _isImport;
    private InstallPreparation? _preparation;

    /// <param name="existingFolder">
    /// A folder that already holds the game (copied from another PC or launcher): it is verified and
    /// only missing or damaged files are downloaded.
    /// </param>
    public InstallDialogViewModel(
        Game game,
        IInstallPreparer preparer,
        ISettingsService settings,
        IPickerService pickers,
        string? existingFolder = null)
    {
        _game = game;
        _preparer = preparer;
        _pickers = pickers;
        _isImport = existingFolder is not null;

        InstallDirectory = existingFolder ?? Path.Combine(settings.Current.InstallDirectory, ToFolderName(game.Title));
        SizeSummary = string.Empty;
        SpaceSummary = string.Empty;
        IsLoading = true;
    }

    public string Title => _isImport ? $"Import {_game.Title}" : $"Install {_game.Title}";

    public string PrimaryText => _isImport ? "Verify and import" : "Install";

    public string ModeHint => _isImport
        ? "Files already in this folder are checked and kept; only missing or damaged files are downloaded."
        : string.Empty;

    public bool HasModeHint => _isImport;

    /// <summary>Content the build marks as optional (high-resolution assets...), off by default.</summary>
    public ObservableCollection<InstallOptionViewModel> OptionalContent { get; } = [];

    /// <summary>Parts of the standard install, on by default; listed for users who want to trim further.</summary>
    public ObservableCollection<InstallOptionViewModel> Components { get; } = [];

    public bool HasOptionalContent => OptionalContent.Count > 0;

    public bool HasComponents => Components.Count > 0;

    public string ComponentsHeader => $"Game components ({Components.Count})";

    /// <summary>Everything, including options too small to be worth showing (kept selected).</summary>
    private readonly List<InstallOptionViewModel> _allOptions = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial string InstallDirectory { get; set; }

    [ObservableProperty]
    public partial string SizeSummary { get; set; }

    [ObservableProperty]
    public partial string SpaceSummary { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    public partial bool HasEnoughSpace { get; set; }

    public bool HasError => Error is not null;

    public bool IsReady => !IsLoading && !HasError;

    public bool CanInstall => IsReady && HasEnoughSpace;

    public async Task LoadAsync()
    {
        try
        {
            _preparation = await _preparer.PrepareAsync(_game, CancellationToken.None);
            foreach (var option in _preparation.Options.OrderByDescending(option => option.ExtraInstallSize))
            {
                // Placeholder tags weigh a few bytes: install them silently rather than show noise.
                var isVisible = option.ExtraInstallSize >= MinimumVisibleSize;
                var item = new InstallOptionViewModel(option, isSelected: option.IsRecommended || !isVisible);
                item.PropertyChanged += OnOptionChanged;
                _allOptions.Add(item);

                if (isVisible)
                {
                    (option.IsRecommended ? Components : OptionalContent).Add(item);
                }
            }

            OnPropertyChanged(nameof(HasOptionalContent));
            OnPropertyChanged(nameof(HasComponents));
            OnPropertyChanged(nameof(ComponentsHeader));
            UpdateSizes();
        }
        catch (Exception ex)
        {
            // Nobody awaits this load (the dialog is already up): whatever went wrong, show it there
            // rather than leaving the dialog loading forever.
            Error = ex is TaskCanceledException ? "Epic's servers took too long to answer. Try again." : ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public InstallRequest CreateRequest() => new(
        _game.Id,
        _game.Title,
        InstallDirectory.Trim(),
        [.. SelectedTags()],
        _isImport ? InstallKind.Repair : InstallKind.Install);

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (await _pickers.PickFolderAsync() is { } folder)
        {
            InstallDirectory = _isImport ? folder : Path.Combine(folder, ToFolderName(_game.Title));
        }
    }

    partial void OnInstallDirectoryChanged(string value) => UpdateSizes();

    private void OnOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstallOptionViewModel.IsSelected))
        {
            UpdateSizes();
        }
    }

    private void UpdateSizes()
    {
        if (_preparation is null)
        {
            return;
        }

        var size = _preparer.Measure(_preparation.Manifest, SelectedTags().ToHashSet(StringComparer.Ordinal));
        SizeSummary = $"Download {ByteSize.Format(size.DownloadBytes)} - Install {ByteSize.Format(size.InstallBytes)}";

        // A relative or empty path would install next to Halyard's own files.
        var isValidFolder = IsValidFolder(InstallDirectory);
        var freeSpace = isValidFolder ? GetFreeSpace(InstallDirectory) : null;

        // An import mostly reuses files already on disk; the installer checks space for what it downloads.
        // Free space is unknown on network shares: let the installer check it.
        HasEnoughSpace = isValidFolder && (_isImport || freeSpace is null || freeSpace >= size.InstallBytes);
        SpaceSummary = !isValidFolder
            ? "Choose a valid folder (a full path, such as D:\\Games\\MyGame)"
            : freeSpace is { } free
                ? $"{ByteSize.Format(free)} free on {Path.GetPathRoot(InstallDirectory.Trim())}"
                : "Free space unknown";
    }

    private IEnumerable<string> SelectedTags() => _allOptions.Where(option => option.IsSelected).Select(option => option.Tag);

    private static bool IsValidFolder(string path)
    {
        var trimmed = path.Trim();
        return trimmed.Length > 0 &&
            Path.IsPathFullyQualified(trimmed) &&
            trimmed.IndexOfAny(Path.GetInvalidPathChars()) < 0 &&
            !string.Equals(Path.GetPathRoot(trimmed), trimmed, StringComparison.OrdinalIgnoreCase);
    }

    private static long? GetFreeSpace(string path)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path.Trim()))!).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static string ToFolderName(string title) =>
        string.Concat(title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
}
