using Halyard.Models.Library;

namespace Halyard.Services.Library;

/// <summary>
/// Folds freshly discovered games into the stored library. Refreshing never loses anything the user
/// did: installations made by Halyard and manual games always win over discovered data.
/// </summary>
internal static class LibraryMerger
{
    /// <param name="launcherScanned">
    /// The official launcher's installs were read successfully. Otherwise (unreadable folder...) its
    /// installs are kept as they were, rather than taken for uninstalled.
    /// </param>
    /// <param name="ownedScanned">
    /// The signed-in account's games were listed successfully (an empty list when signed out). Epic
    /// games it doesn't own and that aren't installed then leave the library: after a sign-out or an
    /// account switch, the previous account's games would otherwise stay, with an Install that fails.
    /// </param>
    public static IReadOnlyList<Game> Merge(
        IReadOnlyList<Game> stored,
        IReadOnlyList<Game> discovered,
        bool launcherScanned = true,
        bool ownedScanned = false)
    {
        var games = stored.ToDictionary(game => game.Id);

        foreach (var found in discovered)
        {
            games[found.Id] = games.TryGetValue(found.Id, out var existing) ? Combine(existing, found) : found;
        }

        if (ownedScanned)
        {
            var owned = discovered.Select(game => game.Id).ToHashSet();
            foreach (var game in games.Values.Where(game => game is { IsEpicGame: true, IsInstalled: false } && !owned.Contains(game.Id)).ToList())
            {
                games.Remove(game.Id);
            }
        }

        if (!launcherScanned)
        {
            return [.. games.Values];
        }

        // An install the official launcher no longer reports was uninstalled there.
        var launcherInstalls = discovered
            .Where(game => game.Installation?.Source == InstallationSource.EpicLauncher)
            .Select(game => game.Id)
            .ToHashSet();

        foreach (var game in games.Values.ToList())
        {
            if (game.Installation?.Source == InstallationSource.EpicLauncher && !launcherInstalls.Contains(game.Id))
            {
                games[game.Id] = game with { Installation = null };
            }
        }

        return [.. games.Values];
    }

    private static Game Combine(Game existing, Game found) => existing with
    {
        Title = string.IsNullOrWhiteSpace(found.Title) ? existing.Title : found.Title,
        EpicNamespace = found.EpicNamespace ?? existing.EpicNamespace,
        EpicCatalogItemId = found.EpicCatalogItemId ?? existing.EpicCatalogItemId,
        CoverImageUrl = found.CoverImageUrl ?? existing.CoverImageUrl,
        LatestVersion = found.LatestVersion ?? existing.LatestVersion,
        Installation = existing.Installation is { Source: not InstallationSource.EpicLauncher }
            ? existing.Installation
            : found.Installation ?? existing.Installation,
    };
}
