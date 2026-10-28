using System.Text.Json;
using Halyard.Core.Library;
using Halyard.Models.Library;
using Halyard.Services.Serialization;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Library;

/// <summary>Games installed by the official Epic Games Launcher, read from its install manifests.</summary>
public sealed class EpicLauncherGameSource(ILogger<EpicLauncherGameSource> logger) : IGameSource
{
    private static readonly string ManifestsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Manifests");

    public string Name => "Epic Games Launcher";

    public async Task<IReadOnlyList<Game>> DiscoverAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(ManifestsDirectory))
        {
            return [];
        }

        List<Game> games = [];
        foreach (var path in Directory.EnumerateFiles(ManifestsDirectory, "*.item"))
        {
            var item = await TryReadAsync(path, cancellationToken);
            if (item is { IsIncompleteInstall: false } && Directory.Exists(item.InstallLocation))
            {
                games.Add(ToGame(item));
            }
        }

        logger.LogInformation("Found {Count} game(s) installed by the {Source}", games.Count, Name);
        return games;
    }

    private async Task<EpicManifestItem?> TryReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync(stream, EpicJsonContext.Default.EpicManifestItem, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(ex, "Skipping unreadable manifest {Path}", path);
            return null;
        }
    }

    private static Game ToGame(EpicManifestItem item) => new()
    {
        Id = Game.EpicId(item.AppName),
        Title = item.DisplayName,
        EpicAppName = item.AppName,
        EpicNamespace = item.CatalogNamespace,
        EpicCatalogItemId = item.CatalogItemId,
        Installation = new GameInstallation
        {
            InstallDirectory = item.InstallLocation,
            Executable = item.LaunchExecutable,
            Arguments = item.LaunchCommand?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
            Version = item.AppVersionString,
            InstallTags = item.InstallTags,
            InstallSize = item.InstallSize,
            Source = InstallationSource.EpicLauncher,
        },
    };
}
