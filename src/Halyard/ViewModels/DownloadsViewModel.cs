using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Halyard.Core.Downloads;
using Halyard.Core.Library;
using Halyard.Models.Downloads;
using Halyard.UI;

namespace Halyard.ViewModels;

/// <summary>The active install in the spotlight, then what's queued, then what's done.</summary>
public sealed partial class DownloadsViewModel : ObservableObject
{
    private readonly IDownloadQueue _queue;
    private readonly ILibraryService _library;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private readonly Dictionary<DownloadJob, DownloadItemViewModel> _items = [];

    public DownloadsViewModel(
        IDownloadQueue queue,
        ILibraryService library,
        INavigationService navigation,
        IDialogService dialogs,
        IUiDispatcher dispatcher)
    {
        _queue = queue;
        _library = library;
        _navigation = navigation;
        _dialogs = dialogs;
        _dispatcher = dispatcher;

        queue.JobAdded += OnQueueChanged;
        queue.JobRemoved += OnQueueChanged;
        queue.JobStateChanged += OnQueueChanged;
        queue.JobMoved += (_, _) => dispatcher.Post(Sync);

        Sync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrent))]
    public partial DownloadItemViewModel? Current { get; set; }

    public bool HasCurrent => Current is not null;

    public ObservableCollection<DownloadItemViewModel> Upcoming { get; } = [];

    public ObservableCollection<DownloadItemViewModel> Finished { get; } = [];

    public bool HasUpcoming => Upcoming.Count > 0;

    public bool HasFinished => Finished.Count > 0;

    public bool IsEmpty => Current is null && Upcoming.Count == 0 && Finished.Count == 0;

    [RelayCommand]
    private void ClearFinished() => _queue.ClearFinished();

    [RelayCommand]
    private void BrowseLibrary() => _navigation.NavigateTo(PageKeys.Library);

    private void OnQueueChanged(object? sender, DownloadJob job) => _dispatcher.Post(Sync);

    private void Sync()
    {
        var jobs = _queue.Jobs;

        foreach (var gone in _items.Keys.Except(jobs).ToList())
        {
            _items[gone].Dispose();
            _items.Remove(gone);
        }

        var items = jobs.Select(GetOrCreate).ToList();
        var active = items.Where(item => !item.Job.IsFinished).ToList();

        Current = active.FirstOrDefault(item => item.Job.State == DownloadState.Running) ?? active.FirstOrDefault();
        Upcoming.SyncTo([.. active.Where(item => item != Current)]);
        Finished.SyncTo([.. items.Where(item => item.Job.IsFinished)]);

        OnPropertyChanged(nameof(HasUpcoming));
        OnPropertyChanged(nameof(HasFinished));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private DownloadItemViewModel GetOrCreate(DownloadJob job)
    {
        if (!_items.TryGetValue(job, out var item))
        {
            var cover = _library.Find(job.Request.GameId)?.CoverImageUrl;
            item = _items[job] = new DownloadItemViewModel(job, cover, _queue, _dialogs, _dispatcher);
        }

        return item;
    }
}
