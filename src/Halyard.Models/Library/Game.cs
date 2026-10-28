using System.Text.Json.Serialization;

namespace Halyard.Models.Library;

/// <summary>A library entry: an owned Epic title or a manually added executable, installed or not.</summary>
public sealed record Game
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    /// <summary>Epic app name (e.g. <c>Fortnite</c>); <see langword="null"/> for manually added games.</summary>
    public string? EpicAppName { get; init; }

    public string? EpicNamespace { get; init; }

    public string? EpicCatalogItemId { get; init; }

    /// <summary>Portrait cover art.</summary>
    public string? CoverImageUrl { get; init; }

    /// <summary>Newest build Epic offers; compared with the installed version to detect updates.</summary>
    public string? LatestVersion { get; init; }

    public GameInstallation? Installation { get; init; }

    /// <summary>Total time played through Halyard.</summary>
    public TimeSpan PlayTime { get; init; }

    public DateTimeOffset? LastPlayedAt { get; init; }

    /// <summary>Extra command-line arguments the user wants on every launch.</summary>
    public string? CustomArguments { get; init; }

    /// <summary>Launch without Epic authentication (single player, no network).</summary>
    public bool LaunchOffline { get; init; }

    [JsonIgnore]
    public bool IsInstalled => Installation is not null;

    [JsonIgnore]
    public bool IsEpicGame => EpicAppName is not null;

    /// <summary>Halyard can install, update and repair it from Epic's servers.</summary>
    [JsonIgnore]
    public bool IsDownloadable => EpicAppName is not null && EpicNamespace is not null && EpicCatalogItemId is not null;

    [JsonIgnore]
    public bool HasUpdate =>
        Installation is { Source: InstallationSource.Halyard } installation &&
        LatestVersion is not null &&
        LatestVersion != installation.Version;

    public static string EpicId(string appName) => $"epic:{appName}";

    public static Game FromExecutable(string executablePath) => new()
    {
        Id = $"manual:{Guid.NewGuid():N}",
        Title = Path.GetFileNameWithoutExtension(executablePath),
        Installation = new GameInstallation
        {
            InstallDirectory = Path.GetDirectoryName(executablePath)!,
            Executable = Path.GetFileName(executablePath),
            Source = InstallationSource.Manual,
        },
    };
}
