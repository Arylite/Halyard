using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Halyard.Core.Downloads;
using Halyard.Core.Launching;
using Halyard.Core.Library;
using Halyard.Models.Downloads;
using Halyard.Models.Library;
using Halyard.UI;

namespace Halyard.ViewModels;

public sealed partial class LibraryViewModel : ObservableObject
{
    // Filter and sort indexes follow the order of the items in the page.
    private const int FilterInstalled = 1;
    private const int FilterUpdates = 2;
    private const int FilterNotInstalled = 3;

    private const int SortRecentlyPlayed = 1;
    private const int SortSize = 2;

    private readonly ILibraryService _library;
    private readonly IDownloadQueue _queue;
    private readonly IPickerService _pickers;
    private readonly GameItemViewModelFactory _itemFactory;
    private readonly Dictionary<string, GameItemViewModel> _items = [];

    public LibraryViewModel(
        ILibraryService library,
        IGameLauncher launcher,
        IDownloadQueue queue,
        IPickerService pickers,
        GameItemViewModelFactory itemFactory,
        IUiDispatcher dispatcher)
    {
        _library = library;
        _queue = queue;
        _pickers = pickers;
        _itemFactory = itemFactory;
        SearchText = string.Empty;

        library.Changed += (_, _) => dispatcher.Post(Sync);
        launcher.Started += (_, running) => dispatcher.Post(() => SetRunning(running.Game.Id, isRunning: true));
        launcher.Exited += (_, running) => dispatcher.Post(() => SetRunning(running.Game.Id, isRunning: false));
        queue.JobAdded += (_, job) => dispatcher.Post(() => RouteJob(job));
        queue.JobStateChanged += (_, job) => dispatcher.Post(() => RouteJob(job));

        Sync();
    }

    /// <summary>The cards currently shown, after search and filter.</summary>
    public ObservableCollection<GameItemViewModel> Games { get; } = [];

    public IReadOnlyList<string> SortOptions { get; } = ["Name", "Recently played", "Size on disk"];

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial int FilterIndex { get; set; }

    [ObservableProperty]
    public partial int SortIndex { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    public partial bool IsRefreshing { get; set; }

    public bool IsEmpty => Games.Count == 0;

    public string EmptyTitle => _items.Count == 0 ? "No games yet" : "No matching games";

    public string EmptyMessage => _items.Count == 0
        ? "Sign in with your Epic account in Settings to see your games, or add any executable."
        : "Try another search or filter.";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnFilterIndexChanged(int value) => ApplyFilter();

    partial void OnSortIndexChanged(int value) => ApplyFilter();

    private bool CanRefresh => !IsRefreshing;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            await _library.RefreshAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task AddGameAsync()
    {
        if (await _pickers.PickExecutableAsync() is { } executablePath)
        {
            await _library.AddAsync(Game.FromExecutable(executablePath));
        }
    }

    /// <summary>Reuses existing cards so their state (and recycled UI) survives library refreshes.</summary>
    private void Sync()
    {
        var current = _library.Games.Select(game => game.Id).ToHashSet();
        foreach (var staleId in _items.Keys.Where(id => !current.Contains(id)).ToList())
        {
            _items.Remove(staleId);
        }

        foreach (var game in _library.Games)
        {
            if (_items.TryGetValue(game.Id, out var item))
            {
                item.Apply(game);
            }
            else
            {
                _items[game.Id] = _itemFactory.Create(game);
            }
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var visible = Sort(_library.Games.Where(Matches))
            .Select(game => _items[game.Id])
            .ToList();

        Games.SyncTo(visible);

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    private bool Matches(Game game)
    {
        var filterMatches = FilterIndex switch
        {
            FilterInstalled => game.IsInstalled,
            FilterUpdates => game.HasUpdate,
            FilterNotInstalled => !game.IsInstalled,
            _ => true,
        };

        return filterMatches &&
            _items.ContainsKey(game.Id) &&
            (string.IsNullOrWhiteSpace(SearchText) || game.Title.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>The library comes sorted by title; other orders keep title as the tie-breaker.</summary>
    private IEnumerable<Game> Sort(IEnumerable<Game> games) => SortIndex switch
    {
        SortRecentlyPlayed => games.OrderByDescending(game => game.LastPlayedAt ?? DateTimeOffset.MinValue),
        SortSize => games.OrderByDescending(game => game.Installation?.InstallSize ?? 0),
        _ => games,
    };

    private void RouteJob(DownloadJob job)
    {
        if (_items.TryGetValue(job.Request.GameId, out var item))
        {
            item.AttachJob(job);
        }
    }

    private void SetRunning(string gameId, bool isRunning)
    {
        if (_items.TryGetValue(gameId, out var item))
        {
            item.IsRunning = isRunning;
        }
    }
}
