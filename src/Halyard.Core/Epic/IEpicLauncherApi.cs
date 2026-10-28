using Halyard.Models.Epic;

namespace Halyard.Core.Epic;

/// <summary>The launcher-facing Epic services: owned apps, catalog metadata and build manifests.</summary>
public interface IEpicLauncherApi
{
    Task<IReadOnlyList<EpicAsset>> GetOwnedAssetsAsync(CancellationToken cancellationToken);

    /// <returns>Items keyed by catalog item id; unknown ids are left out.</returns>
    Task<IReadOnlyDictionary<string, EpicCatalogItem>> GetCatalogItemsAsync(
        string @namespace,
        IReadOnlyCollection<string> catalogItemIds,
        CancellationToken cancellationToken);

    Task<ManifestLocation> GetManifestLocationAsync(
        string @namespace,
        string catalogItemId,
        string appName,
        CancellationToken cancellationToken);
}
