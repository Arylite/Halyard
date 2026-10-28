using System.Diagnostics;
using Halyard.Models.Downloads;

namespace Halyard.Services.Installs;

/// <summary>
/// Chooses the CDN for each chunk request and learns from the results. In failover mode the first
/// CDN takes everything and the others only serve retries. In spread mode (experimental) every CDN
/// is measured, then requests go to the CDNs close to the fastest one, least busy first; the others
/// are re-probed now and then with a single request, so a CDN that speeds up is noticed.
/// </summary>
/// <remarks>
/// Picking at random in proportion to speed looks fair but isn't: a CDN ten times slower gets a
/// tenth of the requests, yet each holds a worker ten times longer, so workers spend about as much
/// time on every CDN and throughput falls to the average CDN instead of the best one.
/// </remarks>
internal sealed class CdnBalancer
{
    private const double Smoothing = 0.2;

    /// <summary>CDNs at least this fraction of the best one's speed share the load.</summary>
    private const double Competitive = 0.75;

    private const double FailurePenalty = 4;

    /// <summary>Each success forgives part of past failures, so one blip doesn't sideline a CDN for good.</summary>
    private const double FailureDecay = 0.9;

    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MinHedgeDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxHedgeDelay = TimeSpan.FromSeconds(8);

    private readonly Uri[] _baseUris;
    private readonly Endpoint[] _endpoints;
    private readonly Func<bool> _spreadEnabled;
    private readonly Lock _gate = new();
    private double _averageChunkSeconds;
    private int _nextProbe;

    /// <param name="spreadEnabled">Read on every pick, so toggling the setting applies mid-download.</param>
    public CdnBalancer(IReadOnlyList<Uri> baseUris, Func<bool> spreadEnabled)
    {
        _baseUris = [.. baseUris];
        _endpoints = [.. baseUris.Select(_ => new Endpoint())];
        _spreadEnabled = spreadEnabled;
    }

    public CdnBalancer(IReadOnlyList<Uri> baseUris, bool spread)
        : this(baseUris, () => spread)
    {
    }

    public int Count => _baseUris.Length;

    /// <summary>Spreading across CDNs (and hedging slow requests) right now.</summary>
    public bool IsSpreading => Count > 1 && _spreadEnabled();

    /// <summary>How long a chunk may take before a backup request is sent to another CDN.</summary>
    public TimeSpan HedgeDelay
    {
        get
        {
            lock (_gate)
            {
                return _averageChunkSeconds == 0
                    ? MaxHedgeDelay
                    : TimeSpan.FromSeconds(Math.Clamp(_averageChunkSeconds * 3, MinHedgeDelay.TotalSeconds, MaxHedgeDelay.TotalSeconds));
            }
        }
    }

    public Uri this[int index] => _baseUris[index];

    /// <summary>Picks a CDN for one request, which must then end in <see cref="Report"/> or <see cref="Abandon"/>.</summary>
    public int Pick(int attempt, int exclude = -1)
    {
        var spreading = IsSpreading;
        lock (_gate)
        {
            var now = Stopwatch.GetTimestamp();
            if (!spreading)
            {
                var index = (attempt - 1) % Count;
                return Take(index == exclude && Count > 1 ? (index + 1) % Count : index, now);
            }

            // Measure every CDN once, then re-probe idle ones now and then, one request at a time.
            for (var i = 0; i < Count; i++)
            {
                var candidate = (_nextProbe + i) % Count;
                var endpoint = _endpoints[candidate];
                if (candidate != exclude && endpoint.InFlight == 0 &&
                    (endpoint.Samples == 0 || Stopwatch.GetElapsedTime(endpoint.LastPicked, now) >= ProbeInterval))
                {
                    _nextProbe = candidate + 1;
                    return Take(candidate, now);
                }
            }

            var best = 0.0;
            for (var i = 0; i < Count; i++)
            {
                if (i != exclude && _endpoints[i].Samples > 0)
                {
                    best = Math.Max(best, _endpoints[i].Score);
                }
            }

            var choice = -1;
            var lowestLoad = double.MaxValue;
            for (var i = 0; i < Count; i++)
            {
                var endpoint = _endpoints[i];
                if (i == exclude || endpoint.Samples == 0 || endpoint.Score < best * Competitive)
                {
                    continue;
                }

                var load = (endpoint.InFlight + 1) / endpoint.Score;
                if (load < lowestLoad)
                {
                    lowestLoad = load;
                    choice = i;
                }
            }

            // Nothing measured yet (every first probe still running): the least busy CDN.
            if (choice < 0)
            {
                for (var i = 0; i < Count; i++)
                {
                    if (i != exclude && (choice < 0 || _endpoints[i].InFlight < _endpoints[choice].InFlight))
                    {
                        choice = i;
                    }
                }
            }

            return Take(Math.Max(choice, 0), now);
        }
    }

    public void Report(int index, long bytes, TimeSpan elapsed, bool success)
    {
        lock (_gate)
        {
            var endpoint = _endpoints[index];
            endpoint.InFlight = Math.Max(0, endpoint.InFlight - 1);
            endpoint.Samples++;
            if (!success)
            {
                endpoint.Failures++;
                endpoint.Penalty++;
                return;
            }

            var seconds = Math.Max(elapsed.TotalSeconds, 0.001);
            endpoint.Penalty *= FailureDecay;
            endpoint.Bytes += bytes;
            endpoint.BytesPerSecond = Smooth(endpoint.BytesPerSecond, bytes / seconds);
            _averageChunkSeconds = Smooth(_averageChunkSeconds, seconds);
        }
    }

    /// <summary>A request canceled before it finished, usually because it lost a hedge race.</summary>
    public void Abandon(int index, long bytes, TimeSpan elapsed)
    {
        lock (_gate)
        {
            var endpoint = _endpoints[index];
            endpoint.InFlight = Math.Max(0, endpoint.InFlight - 1);

            // A request still unfinished after a while was at most this fast. Without this sample, a
            // CDN whose requests always get rescued would keep the speed it had before it slowed down.
            if (elapsed >= MinHedgeDelay)
            {
                endpoint.Samples++;
                endpoint.BytesPerSecond = Smooth(endpoint.BytesPerSecond, bytes / elapsed.TotalSeconds);
            }
        }
    }

    public IReadOnlyList<CdnStat> Snapshot()
    {
        lock (_gate)
        {
            return [.. _endpoints.Select((endpoint, i) =>
                new CdnStat(_baseUris[i].Host, endpoint.BytesPerSecond, endpoint.Bytes, endpoint.Failures))];
        }
    }

    private int Take(int index, long now)
    {
        _endpoints[index].InFlight++;
        _endpoints[index].LastPicked = now;
        return index;
    }

    private static double Smooth(double current, double sample) =>
        current == 0 ? sample : current + (Smoothing * (sample - current));

    private sealed class Endpoint
    {
        public double BytesPerSecond;
        public long Bytes;
        public int Failures;
        public double Penalty;
        public int InFlight;
        public int Samples;
        public long LastPicked;

        public double Score => Math.Max(BytesPerSecond, 1) / (1 + (Penalty * FailurePenalty));
    }
}
