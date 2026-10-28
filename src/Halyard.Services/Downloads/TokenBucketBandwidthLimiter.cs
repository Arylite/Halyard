using Halyard.Core.Downloads;
using Halyard.Core.Settings;

namespace Halyard.Services.Downloads;

/// <summary>
/// Global token bucket with a one-second burst. Reads the limit on every call, so changing it in
/// the settings applies to downloads already running.
/// </summary>
public sealed class TokenBucketBandwidthLimiter(ISettingsService settings, TimeProvider time) : IBandwidthLimiter, IDisposable
{
    private const double BytesPerMegabyte = 1024 * 1024;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private double _tokens;
    private long _lastRefill = time.GetTimestamp();

    public async ValueTask WaitAsync(int bytes, CancellationToken cancellationToken)
    {
        var rate = settings.Current.BandwidthLimitMegabytesPerSecond * BytesPerMegabyte;
        if (rate <= 0)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            Refill(rate);
            _tokens -= bytes;
            if (_tokens < 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(-_tokens / rate), time, cancellationToken);
                Refill(rate);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Refill(double rate)
    {
        var now = time.GetTimestamp();
        _tokens = Math.Min(rate, _tokens + (time.GetElapsedTime(_lastRefill, now).TotalSeconds * rate));
        _lastRefill = now;
    }

    public void Dispose() => _gate.Dispose();
}
