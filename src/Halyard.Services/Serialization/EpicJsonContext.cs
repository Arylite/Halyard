using System.Text.Json.Serialization;
using Halyard.Services.Auth;
using Halyard.Services.Epic;
using Halyard.Services.Library;

namespace Halyard.Services.Serialization;

/// <summary>Source-generated serializers for payloads produced by Epic (web APIs and launcher manifests).</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(EpicExchangeCodeResponse))]
[JsonSerializable(typeof(EpicRedirectResponse))]
[JsonSerializable(typeof(EpicTokenResponse))]
[JsonSerializable(typeof(List<EpicAssetResponse>))]
[JsonSerializable(typeof(Dictionary<string, EpicCatalogItemResponse>))]
[JsonSerializable(typeof(EpicManifestLocationResponse))]
[JsonSerializable(typeof(EpicManifestItem))]
internal sealed partial class EpicJsonContext : JsonSerializerContext;
