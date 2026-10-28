namespace Halyard.Hosting;

/// <param name="StartHidden">Started with Windows: live in the tray until the user opens the window.</param>
public sealed record StartupOptions(bool StartHidden)
{
    public const string HiddenFlag = "--minimized";

    public static StartupOptions Parse(IEnumerable<string> args) =>
        new(args.Contains(HiddenFlag, StringComparer.OrdinalIgnoreCase));
}
