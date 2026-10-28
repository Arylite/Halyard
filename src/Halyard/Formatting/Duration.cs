namespace Halyard.Formatting;

public static class Duration
{
    public static string Format(TimeSpan duration) => duration.TotalSeconds switch
    {
        < 60 => $"{Math.Max(1, (int)duration.TotalSeconds)} s",
        < 3600 => $"{(int)duration.TotalMinutes} min",
        _ => $"{(int)duration.TotalHours} h {duration.Minutes:00} min",
    };
}
