using Halyard.Formatting;
using Halyard.Models.Downloads;

namespace Halyard.ViewModels;

/// <summary>One CDN row of the active download: host, speed, and its share relative to the fastest.</summary>
public sealed record CdnStatViewModel(CdnStat Stat, double MaxBytesPerSecond)
{
    public string Host => Stat.Host;

    public string Detail => Stat.Failures > 0
        ? $"{ByteSize.Format(Stat.BytesPerSecond)}/s per connection - {ByteSize.Format(Stat.Bytes)} - {Stat.Failures} failed"
        : $"{ByteSize.Format(Stat.BytesPerSecond)}/s per connection - {ByteSize.Format(Stat.Bytes)}";

    public double Share => MaxBytesPerSecond > 0 ? Stat.BytesPerSecond / MaxBytesPerSecond * 100 : 0;
}
