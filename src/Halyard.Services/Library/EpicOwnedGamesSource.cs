using Halyard.Core.Auth;
using Halyard.Core.Epic;
using Halyard.Core.Library;
using Halyard.Models.Epic;
using Halyard.Models.Library;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Library;

/// <summary>Every game the signed-in account owns, installed or not.</summary>
public sealed class EpicOwnedGamesSource(
    IEpicLauncherApi api,
    IAuthService auth,
    ILogger<EpicOwnedGamesSource> logger) : IGameSource
{
    /// <summary>Unreal Engine marketplace content shows up as assets but isn't playable.</summary>
    private const string UnrealEngineNamespace = "ue";

    private const int MaxParallelCatalogRequests = 8;

    public string Name => "Epic account";

    public async Task<IReadOnlyList<Game>> DiscoverAsync(CancellationToken cancellationToken)
    {
        if (auth.Session is null)
        {
            return [];
        }

        var assets = (await api.GetOwnedAssetsAsync(cancellationToken))
            .Where(asset => asset.Namespace != UnrealEngineNamespace)
            .ToList();

        var catalog = await GetCatalogAsync(assets, cancellationToken);

        List<Game> games = [];
        foreach (var asset in assets)
        {
            catalog.TryGetValue(asset.CatalogItemId, out var item);
            if (item is { IsAddOn: true })
            {
                continue;
            }

            games.Add(new Game
            {
                Id = Game.EpicId(asset.AppName),
                Title = item?.Title ?? asset.AppName,
                EpicAppName = asset.AppName,
                EpicNamespace = asset.Namespace,
                EpicCatalogItemId = asset.CatalogItemId,
                CoverImageUrl = item?.CoverImageUrl,
                LatestVersion = asset.BuildVersion,
            });
        }

        logger.LogInformation("Account owns {Count} game(s)", games.Count);
        return games;
    }

    private async Task<Dictionary<string, EpicCatalogItem>> GetCatalogAsync(
        IReadOnlyList<EpicAsset> assets,
        CancellationToken cancellationToken)
    {
        var catalog = new Dictionary<string, EpicCatalogItem>();
        var gate = new Lock();

        await Parallel.ForEachAsync(
            assets.GroupBy(asset => asset.Namespace),
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelCatalogRequests, CancellationToken = cancellationToken },
            async (group, token) =>
            {
                try
                {
                    var items = await api.GetCatalogItemsAsync(group.Key, [.. group.Select(asset => asset.CatalogItemId).Distinct()], token);
                    lock (gate)
                    {
                        foreach (var (id, item) in items)
                        {
                            catalog[id] = item;
                        }
                    }
                }
                catch (Exception ex) when (ex is EpicAuthException or HttpRequestException)
                {
                    // Titles fall back to app names; the games stay usable.
                    logger.LogWarning(ex, "No catalog details for namespace {Namespace}", group.Key);
                }
            });

        return catalog;
    }
}
