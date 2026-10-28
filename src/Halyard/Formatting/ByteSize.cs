namespace Halyard.Formatting;

public static class ByteSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];
    private static readonly string[] BitRateUnits = ["b/s", "Kb/s", "Mb/s", "Gb/s"];

    /// <summary>Speeds in bits (Mb/s, the unit internet plans use) rather than bytes (MB/s). Follows the settings.</summary>
    public static bool SpeedInBits { get; set; }

    public static string Format(double bytes)
    {
        var unit = 0;
        while (bytes >= 1024 && unit < Units.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes:0} {Units[unit]}" : $"{bytes:0.0} {Units[unit]}";
    }

    public static string FormatSpeed(double bytesPerSecond) =>
        SpeedInBits ? FormatBitRate(bytesPerSecond * 8) : $"{Format(bytesPerSecond)}/s";

    /// <summary>Network speeds are decimal: 1 Mb/s is a million bits per second.</summary>
    private static string FormatBitRate(double bitsPerSecond)
    {
        var unit = 0;
        while (bitsPerSecond >= 1000 && unit < BitRateUnits.Length - 1)
        {
            bitsPerSecond /= 1000;
            unit++;
        }

        return unit == 0 ? $"{bitsPerSecond:0} {BitRateUnits[unit]}" : $"{bitsPerSecond:0.0} {BitRateUnits[unit]}";
    }
}
