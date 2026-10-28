using System.Text.Json.Serialization;

namespace Halyard.Services.Auth;

internal sealed class EpicExchangeCodeResponse
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }
}

/// <summary>What the authorization code page shows; the code is null while signed out.</summary>
internal sealed class EpicRedirectResponse
{
    [JsonPropertyName("authorizationCode")]
    public string? AuthorizationCode { get; init; }
}

internal sealed class EpicTokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; init; }

    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; init; }

    [JsonPropertyName("refresh_expires_at")]
    public DateTimeOffset RefreshExpiresAt { get; init; }

    [JsonPropertyName("account_id")]
    public required string AccountId { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }
}
