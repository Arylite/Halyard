using Halyard.Formatting;
using Halyard.Models.Downloads;

namespace Halyard.ViewModels;

/// <summary>One CDN row of the active download: host, what it delivers right now, and its total so far.</summary>
/// <param name="BytesPerSecond">Measured from the CDN's growing total, so it is the CDN's real throughput.</param>
public sealed record CdnStatViewModel(CdnStat Stat, double BytesPerSecond)
{
    public string Host => Stat.Host;

    public string SpeedText => ByteSize.FormatSpeed(BytesPerSecond);

    public string DownloadedText => Stat.Failures > 0
        ? $"{ByteSize.Format(Stat.Bytes)} - {Stat.Failures} failed"
        : ByteSize.Format(Stat.Bytes);
}
