using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using Halyard.Core.Auth;
using Halyard.Core.Epic;
using Halyard.Models.Epic;
using Halyard.Services.Auth;
using Halyard.Services.Http;
using Halyard.Services.Serialization;

namespace Halyard.Services.Epic;

public sealed class EpicLauncherApi(HttpClient http, IAccessTokenProvider tokens) : IEpicLauncherApi
{
    // Portrait art first: it is what the library grid shows.
    private static readonly string[] CoverImageTypes = ["DieselGameBoxTall", "OfferImageTall", "Thumbnail", "DieselGameBox"];

    public async Task<IReadOnlyList<EpicAsset>> GetOwnedAssetsAsync(CancellationToken cancellationToken)
    {
        var assets = await GetAsync(EpicEndpoints.OwnedAssets, EpicJsonContext.Default.ListEpicAssetResponse, cancellationToken);
        return [.. assets.Select(asset => new EpicAsset(asset.AppName, asset.Namespace, asset.CatalogItemId, asset.BuildVersion ?? string.Empty))];
    }

    public async Task<IReadOnlyDictionary<string, EpicCatalogItem>> GetCatalogItemsAsync(
        string @namespace,
        IReadOnlyCollection<string> catalogItemIds,
        CancellationToken cancellationToken)
    {
        var items = await GetAsync(
            EpicEndpoints.CatalogItems(@namespace, catalogItemIds),
            EpicJsonContext.Default.DictionaryStringEpicCatalogItemResponse,
            cancellationToken);

        return items.Values.ToDictionary(
            item => item.Id,
            item => new EpicCatalogItem(item.Id, item.Title ?? item.Id, item.MainGameItem is not null, FindCover(item)));
    }

    public async Task<ManifestLocation> GetManifestLocationAsync(
        string @namespace,
        string catalogItemId,
        string appName,
        CancellationToken cancellationToken)
    {
        var location = await GetAsync(
            EpicEndpoints.ManifestLocation(@namespace, catalogItemId, appName),
            EpicJsonContext.Default.EpicManifestLocationResponse,
            cancellationToken);

        var element = location.Elements.FirstOrDefault()
            ?? throw new EpicAuthException($"Epic has no build of {appName} for Windows.");

        return new ManifestLocation(
            element.BuildVersion ?? string.Empty,
            [.. element.Manifests.Select(ToUri)]);
    }

    private async Task<T> GetAsync<T>(Uri uri, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAccessTokenAsync(cancellationToken));
        request.Headers.UserAgent.ParseAdd(EpicLauncherClient.UserAgent);

        using var response = await http.SendAsync(request, cancellationToken);
        await EpicHttp.EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken)
            ?? throw new EpicAuthException($"Epic returned an empty response for {uri.AbsolutePath}.");
    }

    private static string? FindCover(EpicCatalogItemResponse item) =>
        CoverImageTypes
            .Select(type => item.KeyImages.FirstOrDefault(image => image.Type == type)?.Url)
            .FirstOrDefault(url => url is not null);

    private static Uri ToUri(EpicManifestUri manifest)
    {
        if (manifest.QueryParams.Count == 0)
        {
            return new Uri(manifest.Uri);
        }

        // Values arrive URL-ready (signed CDN tokens); escaping them again would break the signature.
        var query = string.Join('&', manifest.QueryParams.Select(param => $"{param.Name}={param.Value}"));
        return new Uri($"{manifest.Uri}?{query}");
    }
}
