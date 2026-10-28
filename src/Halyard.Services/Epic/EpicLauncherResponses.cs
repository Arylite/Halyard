using System.Text.Json.Serialization;

namespace Halyard.Services.Epic;

internal sealed class EpicAssetResponse
{
    [JsonPropertyName("appName")]
    public required string AppName { get; init; }

    [JsonPropertyName("namespace")]
    public required string Namespace { get; init; }

    [JsonPropertyName("catalogItemId")]
    public required string CatalogItemId { get; init; }

    [JsonPropertyName("buildVersion")]
    public string? BuildVersion { get; init; }
}

internal sealed class EpicCatalogItemResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("keyImages")]
    public List<EpicKeyImage> KeyImages { get; init; } = [];

    /// <summary>Set on add-ons: the game they belong to.</summary>
    [JsonPropertyName("mainGameItem")]
    public EpicCatalogItemReference? MainGameItem { get; init; }
}

internal sealed class EpicCatalogItemReference
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
}

internal sealed class EpicKeyImage
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

internal sealed class EpicManifestLocationResponse
{
    [JsonPropertyName("elements")]
    public List<EpicManifestElement> Elements { get; init; } = [];
}

internal sealed class EpicManifestElement
{
    [JsonPropertyName("buildVersion")]
    public string? BuildVersion { get; init; }

    [JsonPropertyName("manifests")]
    public List<EpicManifestUri> Manifests { get; init; } = [];
}

internal sealed class EpicManifestUri
{
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }

    [JsonPropertyName("queryParams")]
    public List<EpicQueryParam> QueryParams { get; init; } = [];
}

internal sealed class EpicQueryParam
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }
}
