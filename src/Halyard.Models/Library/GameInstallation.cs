using System.Text.Json.Serialization;

namespace Halyard.Models.Library;

public enum InstallationSource
{
    /// <summary>Installed by Halyard: it owns the manifest, so it can update and repair.</summary>
    Halyard,

    /// <summary>Installed by the official Epic Games Launcher.</summary>
    EpicLauncher,

    Manual,
}

public sealed record GameInstallation
{
    public required string InstallDirectory { get; init; }

    /// <summary>Executable path, relative to <see cref="InstallDirectory"/>.</summary>
    public required string Executable { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = [];

    public string? Version { get; init; }

    /// <summary>Optional content selected at install time (Epic install tags).</summary>
    public IReadOnlyList<string> InstallTags { get; init; } = [];

    public long InstallSize { get; init; }

    public InstallationSource Source { get; init; }

    /// <summary>Post-install step from the manifest; run before the first launch if still needed.</summary>
    public GamePrerequisite? Prerequisite { get; init; }

    [JsonIgnore]
    public string ExecutablePath => Path.Combine(InstallDirectory, Executable);
}
