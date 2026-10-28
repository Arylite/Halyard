using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Halyard.Core.Downloads;
using Halyard.Formatting;
using Halyard.Models.Downloads;
using Halyard.UI;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Halyard.ViewModels;

public sealed partial class DownloadItemViewModel : ObservableObject, IDisposable
{
    /// <summary>About a minute of history at the tracker's 250 ms cadence.</summary>
    private const int HistoryLength = 240;

    private readonly IDownloadQueue _queue;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private readonly Queue<double> _speedHistory = new(HistoryLength);
    private long? _downloadStartedAt;
    private double _peakBytesPerSecond;

    public DownloadItemViewModel(
        DownloadJob job,
        string? coverImageUrl,
        IDownloadQueue queue,
        IDialogService dialogs,
        IUiDispatcher dispatcher)
    {
        Job = job;
        _queue = queue;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        Cover = Uri.TryCreate(coverImageUrl, UriKind.Absolute, out var uri)
            ? new BitmapImage { UriSource = uri, DecodePixelWidth = 240, DecodePixelType = DecodePixelType.Logical }
            : null;
        Job.Changed += OnJobChanged;
    }

    public DownloadJob Job { get; }

    public BitmapImage? Cover { get; }

    public string Name => Job.Request.DisplayName;

    public string Initials => string.Concat(
        Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpperInvariant(word[0])));

    public string Kind => Job.Request.Kind switch
    {
        InstallKind.Update => "Update",
        InstallKind.Repair => "Verify and repair",
        _ => "Install",
    };

    public string Destination => Job.Request.InstallDirectory;

    public double Percent => (Job.Progress.Fraction ?? 0) * 100;

    public string PercentText => Job.Progress.Fraction is { } fraction ? $"{fraction:P0}" : string.Empty;

    public bool IsIndeterminate => Job.State == DownloadState.Running && Job.Progress.Fraction is null;

    public bool ShowProgress => Job.State is DownloadState.Running or DownloadState.Paused or DownloadState.Queued;

    // Queued jobs are waiting, not paused: their bar keeps the normal color.
    public bool IsPaused => Job.State == DownloadState.Paused;

    public bool IsFailed => Job.State == DownloadState.Failed;

    public bool CanPause => Job.State is DownloadState.Queued or DownloadState.Running;

    public bool CanResume => Job.State is DownloadState.Paused or DownloadState.Failed;

    public bool CanCancel => !Job.IsFinished;

    /// <summary>Unfinished jobs are canceled first; "remove" only clears what is done.</summary>
    public bool CanRemove => Job.IsFinished;

    public string ResumeText => IsFailed ? "Retry" : "Resume";

    public string StatusText => ProgressText.Format(Job);

    public string PhaseText => Job.State != DownloadState.Running
        ? StatusText
        : Job.Progress.Phase switch
        {
            InstallPhase.Preparing => "Reading the build manifest",
            InstallPhase.Verifying => "Verifying installed files",
            InstallPhase.Finalizing => "Finishing up",
            _ => "Downloading",
        };

    // Live statistics for the active download.
    public string SpeedText => $"{ByteSize.Format(Job.Progress.BytesPerSecond)}/s";

    public string DiskSpeedText => $"{ByteSize.Format(Job.Progress.DiskBytesPerSecond)}/s";

    public string AmountText => Job.Progress.Total > 0
        ? $"{ByteSize.Format(Job.Progress.Completed)} / {ByteSize.Format(Job.Progress.Total)}"
        : "-";

    public string RemainingText => Job.Progress.Remaining is { } remaining ? Duration.Format(remaining) : "-";

    public string ConnectionsText => Job.Progress.ActiveConnections.ToString();

    public string PeakSpeedText => $"{ByteSize.Format(_peakBytesPerSecond)}/s";

    public string AverageSpeedText => DownloadElapsed is { TotalSeconds: > 1 } elapsed
        ? $"{ByteSize.Format(Job.Progress.Completed / elapsed.TotalSeconds)}/s"
        : "-";

    public string ElapsedText => DownloadElapsed is { } elapsed ? Duration.Format(elapsed) : "-";

    public string ModeText => Job.Progress.MultiCdn
        ? $"Multi-CDN, experimental ({Job.Progress.Cdns.Count} CDNs)"
        : Job.Progress.Cdns.Count > 1 ? $"Single CDN with failover ({Job.Progress.Cdns.Count} available)" : "Single CDN";

    public string ContentText => Job.Request.InstallTags.Count switch
    {
        0 => "Required files only",
        var count => $"Required files + {count} optional component(s)",
    };

    private TimeSpan? DownloadElapsed => _downloadStartedAt is { } started
        ? System.Diagnostics.Stopwatch.GetElapsedTime(started)
        : null;

    /// <summary>Recent network speeds, oldest first, for <see cref="Controls.SpeedGraph"/>.</summary>
    public IReadOnlyList<double> SpeedSamples { get; private set; } = [];

    public IReadOnlyList<CdnStatViewModel> Cdns { get; private set; } = [];

    public bool HasCdns => Cdns.Count > 0;

    [RelayCommand]
    private Task ShowDetailsAsync() => _dialogs.ShowDownloadDetailsAsync(this);

    [RelayCommand]
    private void Pause() => _queue.Pause(Job);

    [RelayCommand]
    private void Resume() => _queue.Resume(Job);

    [RelayCommand]
    private void Cancel() => _queue.Cancel(Job);

    [RelayCommand]
    private void Remove() => _queue.Remove(Job);

    [RelayCommand]
    private void MoveUp() => _queue.Move(Job, -1);

    [RelayCommand]
    private void MoveDown() => _queue.Move(Job, 1);

    private void OnJobChanged(object? sender, EventArgs e) => _dispatcher.Post(() =>
    {
        RecordSpeed();
        var max = Job.Progress.Cdns.Count > 0 ? Job.Progress.Cdns.Max(cdn => cdn.BytesPerSecond) : 0;
        Cdns = [.. Job.Progress.Cdns.Select(stat => new CdnStatViewModel(stat, max))];

        foreach (var name in LiveProperties)
        {
            OnPropertyChanged(name);
        }
    });

    /// <summary>What changes as the job runs; the cover and names never do (re-binding them would reload the image).</summary>
    private static readonly string[] LiveProperties =
    [
        nameof(Percent),
        nameof(PercentText),
        nameof(IsIndeterminate),
        nameof(ShowProgress),
        nameof(IsPaused),
        nameof(IsFailed),
        nameof(CanPause),
        nameof(CanResume),
        nameof(CanCancel),
        nameof(CanRemove),
        nameof(ResumeText),
        nameof(StatusText),
        nameof(PhaseText),
        nameof(SpeedText),
        nameof(DiskSpeedText),
        nameof(AmountText),
        nameof(RemainingText),
        nameof(ConnectionsText),
        nameof(PeakSpeedText),
        nameof(AverageSpeedText),
        nameof(ElapsedText),
        nameof(ModeText),
        nameof(SpeedSamples),
        nameof(Cdns),
        nameof(HasCdns),
    ];

    /// <summary>Keeps a rolling speed history for the graphs, plus the peak and average.</summary>
    private void RecordSpeed()
    {
        if (Job.State != DownloadState.Running || Job.Progress.Phase != InstallPhase.Downloading)
        {
            // Speeds restart from zero after a pause; so does the average.
            _downloadStartedAt = null;
            return;
        }

        _downloadStartedAt ??= System.Diagnostics.Stopwatch.GetTimestamp();
        _peakBytesPerSecond = Math.Max(_peakBytesPerSecond, Job.Progress.BytesPerSecond);

        if (_speedHistory.Count == HistoryLength)
        {
            _speedHistory.Dequeue();
        }

        _speedHistory.Enqueue(Job.Progress.BytesPerSecond);

        // A fresh array per update: bound graphs redraw when the reference changes.
        SpeedSamples = [.. _speedHistory];
    }

    public void Dispose() => Job.Changed -= OnJobChanged;
}
