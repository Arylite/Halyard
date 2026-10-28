using Halyard.Models.Downloads;

namespace Halyard.Services.Installs;

/// <summary>
/// Thread-safe counters for the current phase, reported on a fixed cadence with smoothed speeds,
/// so hot loops only do an interlocked add.
/// </summary>
internal sealed class InstallProgressTracker : IDownloadMeter, IAsyncDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);
    private const double Smoothing = 0.25;

    private readonly IProgress<DownloadProgress> _progress;
    private readonly TimeProvider _time;
    private readonly PeriodicTimer _timer;
    private readonly Task _reporter;

    private InstallPhase _phase;
    private long _completed;
    private long _written;
    private long _total;
    private int _connections;

    private long _lastCompleted;
    private long _lastWritten;
    private long _lastTimestamp;
    private double _bytesPerSecond;
    private double _diskBytesPerSecond;

    public InstallProgressTracker(IProgress<DownloadProgress> progress, TimeProvider time)
    {
        _progress = progress;
        _time = time;
        _timer = new PeriodicTimer(Interval, time);
        _reporter = ReportLoopAsync();
    }

    /// <summary>Per-CDN statistics to include in reports, while a download runs.</summary>
    public Func<IReadOnlyList<CdnStat>>? CdnStats { get; set; }

    public Func<bool>? MultiCdn { get; set; }

    public void StartPhase(InstallPhase phase, long total)
    {
        _phase = phase;
        _total = total;
        Interlocked.Exchange(ref _completed, 0);
        Interlocked.Exchange(ref _written, 0);
        _lastCompleted = 0;
        _lastWritten = 0;
        _lastTimestamp = _time.GetTimestamp();
        _bytesPerSecond = 0;
        _diskBytesPerSecond = 0;
        CdnStats = null;
        Report();
    }

    public void Add(long bytes) => Interlocked.Add(ref _completed, bytes);

    public void AddWritten(long bytes) => Interlocked.Add(ref _written, bytes);

    void IDownloadMeter.AddDownloaded(long bytes) => Add(bytes);

    void IDownloadMeter.ConnectionOpened() => Interlocked.Increment(ref _connections);

    void IDownloadMeter.ConnectionClosed() => Interlocked.Decrement(ref _connections);

    private async Task ReportLoopAsync()
    {
        while (await _timer.WaitForNextTickAsync())
        {
            var completed = Interlocked.Read(ref _completed);
            var written = Interlocked.Read(ref _written);
            var now = _time.GetTimestamp();
            var seconds = _time.GetElapsedTime(_lastTimestamp, now).TotalSeconds;
            if (seconds > 0)
            {
                _bytesPerSecond = Smooth(_bytesPerSecond, Math.Max(0, completed - _lastCompleted) / seconds);
                _diskBytesPerSecond = Smooth(_diskBytesPerSecond, Math.Max(0, written - _lastWritten) / seconds);
            }

            _lastCompleted = completed;
            _lastWritten = written;
            _lastTimestamp = now;
            Report();
        }
    }

    private static double Smooth(double current, double instant) =>
        current == 0 ? instant : current + (Smoothing * (instant - current));

    private void Report() =>
        _progress.Report(new DownloadProgress(_phase, Interlocked.Read(ref _completed), _total, _bytesPerSecond)
        {
            DiskBytesPerSecond = _diskBytesPerSecond,
            ActiveConnections = Volatile.Read(ref _connections),
            Cdns = CdnStats?.Invoke() ?? [],
            MultiCdn = MultiCdn?.Invoke() ?? false,
        });

    public async ValueTask DisposeAsync()
    {
        _timer.Dispose();
        await _reporter;
    }
}
