namespace Halyard.Core.Storage;

/// <summary>Well-known locations of Halyard's on-disk data.</summary>
public sealed record AppPaths(string Root)
{
    public static AppPaths Default { get; } = new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halyard"));

    public string LogsDirectory => Path.Combine(Root, "logs");

    public string SettingsFile => Path.Combine(Root, "settings.json");

    public string LibraryFile => Path.Combine(Root, "library.json");

    public string QueueFile => Path.Combine(Root, "queue.json");

    /// <summary>Ids of prerequisites already installed on this machine.</summary>
    public string PrerequisitesFile => Path.Combine(Root, "prerequisites.json");

    public string SessionFile => Path.Combine(Root, "session.bin");

    /// <summary>Profile of the embedded browser used for sign-in (cookies, cache).</summary>
    public string WebViewDataDirectory => Path.Combine(Root, "WebView2");

    /// <summary>Halyard's bookkeeping inside a game's install folder (manifest, resume journal).</summary>
    public static string InstallMetadataDirectory(string installDirectory) => Path.Combine(installDirectory, ".halyard");
}
