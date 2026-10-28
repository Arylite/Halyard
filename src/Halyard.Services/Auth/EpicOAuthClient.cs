using System.Net.Http.Headers;
using System.Net.Http.Json;
using Halyard.Core.Auth;
using Halyard.Models.Auth;
using Halyard.Services.Http;
using Halyard.Services.Serialization;

namespace Halyard.Services.Auth;

public sealed class EpicOAuthClient(HttpClient http) : IEpicOAuthClient
{
    public Task<EpicGrant> RedeemAuthorizationCodeAsync(string authorizationCode, CancellationToken cancellationToken) =>
        RequestTokenAsync(
            [
                new("grant_type", "authorization_code"),
                new("code", authorizationCode),
                new("token_type", "eg1"),
            ],
            cancellationToken);

    public Task<EpicGrant> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        RequestTokenAsync(
            [
                new("grant_type", "refresh_token"),
                new("refresh_token", refreshToken),
                new("token_type", "eg1"),
            ],
            cancellationToken);

    public async Task<string> CreateExchangeCodeAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, EpicEndpoints.ExchangeCode);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        await EpicHttp.EnsureSuccessAsync(response, cancellationToken);

        var exchange = await response.Content.ReadFromJsonAsync(EpicJsonContext.Default.EpicExchangeCodeResponse, cancellationToken);
        return exchange?.Code ?? throw new EpicAuthException("Epic returned an empty exchange code.");
    }

    public async Task KillSessionAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, EpicEndpoints.KillSession(accessToken));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        await EpicHttp.EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<EpicGrant> RequestTokenAsync(
        IEnumerable<KeyValuePair<string, string>> form,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, EpicEndpoints.Token)
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Authorization = EpicLauncherClient.BasicAuthorization;
        request.Headers.UserAgent.ParseAdd(EpicLauncherClient.UserAgent);

        using var response = await http.SendAsync(request, cancellationToken);
        await EpicHttp.EnsureSuccessAsync(response, cancellationToken);

        var token = await response.Content.ReadFromJsonAsync(EpicJsonContext.Default.EpicTokenResponse, cancellationToken)
            ?? throw new EpicAuthException("Epic returned an empty token response.");

        return new EpicGrant(
            token.AccountId,
            token.DisplayName ?? token.AccountId,
            new EpicTokens(token.AccessToken, token.ExpiresAt, token.RefreshToken, token.RefreshExpiresAt));
    }
}
