namespace Halyard.Models.Downloads;

public enum InstallKind
{
    Install,

    /// <summary>Downloads only the files that changed since the installed build.</summary>
    Update,

    /// <summary>Hashes every installed file and re-downloads the bad ones.</summary>
    Repair,
}

/// <param name="InstallTags">Optional content to include (Epic install tags); untagged files are always installed.</param>
public sealed record InstallRequest(
    string GameId,
    string DisplayName,
    string InstallDirectory,
    IReadOnlyList<string> InstallTags,
    InstallKind Kind);
