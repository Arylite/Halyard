using System.Net;
using System.Net.Http.Json;
using Halyard.Core.Auth;
using Halyard.Models.Auth;
using Halyard.Services.Http;
using Halyard.Services.Serialization;

namespace Halyard.Services.Auth;

/// <summary>
/// Replays the signed-in browser's cookies and user agent. Every call uses its own cookie jar, which
/// is read back afterwards so cookies Epic rotates are kept.
/// </summary>
public sealed class EpicWebClient : IEpicWebClient
{
    private const string XsrfCookie = "XSRF-TOKEN";

    public Task<(string AuthorizationCode, EpicWebCredentials Credentials)> CreateAuthorizationCodeAsync(
        EpicWebCredentials credentials,
        CancellationToken cancellationToken) =>
        SendAsync(credentials, async (client, _) =>
        {
            using var response = await client.GetAsync(EpicEndpoints.AuthorizationCodePage, cancellationToken);
            await EpicHttp.EnsureSuccessAsync(response, cancellationToken);

            var redirect = await response.Content.ReadFromJsonAsync(EpicJsonContext.Default.EpicRedirectResponse, cancellationToken);
            return redirect?.AuthorizationCode ?? throw new EpicSessionExpiredException();
        });

    public async Task LogoutAsync(EpicWebCredentials credentials, CancellationToken cancellationToken) =>
        await SendAsync(credentials, async (client, cookies) =>
        {
            using var request = await CreateXsrfRequestAsync(client, cookies, EpicEndpoints.Logout, cancellationToken);
            using var response = await client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        });

    private static async Task<HttpRequestMessage> CreateXsrfRequestAsync(
        HttpClient client,
        CookieContainer cookies,
        Uri target,
        CancellationToken cancellationToken)
    {
        // Epic's id API wants the XSRF cookie echoed in a header; this call (re)issues the cookie.
        using (var csrf = await client.GetAsync(EpicEndpoints.Csrf, cancellationToken))
        {
            await EpicHttp.EnsureSuccessAsync(csrf, cancellationToken);
        }

        var token = cookies.GetCookies(EpicEndpoints.Site)[XsrfCookie]?.Value
            ?? throw new EpicAuthException("Epic did not issue an XSRF token.");

        var request = new HttpRequestMessage(HttpMethod.Post, target);
        request.Headers.Add("X-XSRF-TOKEN", token);
        return request;
    }

    private static async Task<(T Value, EpicWebCredentials Credentials)> SendAsync<T>(
        EpicWebCredentials credentials,
        Func<HttpClient, CookieContainer, Task<T>> send)
    {
        var cookies = CreateCookieJar(credentials.Cookies);
        using var handler = new SocketsHttpHandler { CookieContainer = cookies, AutomaticDecompression = DecompressionMethods.All };
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(credentials.UserAgent);
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        client.DefaultRequestHeaders.Referrer = EpicEndpoints.Site;

        var value = await send(client, cookies);
        return (value, credentials with { Cookies = [.. cookies.GetAllCookies().Select(ToSessionCookie)] });
    }

    private static CookieContainer CreateCookieJar(IEnumerable<SessionCookie> cookies)
    {
        var jar = new CookieContainer();
        foreach (var cookie in cookies)
        {
            try
            {
                jar.Add(new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain)
                {
                    Expires = cookie.Expires?.UtcDateTime ?? DateTime.MinValue,
                });
            }
            catch (CookieException)
            {
                // A value .NET's cookie model can't hold; Epic's auth cookies are not affected.
            }
        }

        return jar;
    }

    private static SessionCookie ToSessionCookie(Cookie cookie) => new(
        cookie.Name,
        cookie.Value,
        cookie.Domain,
        cookie.Path,
        cookie.Expires == DateTime.MinValue ? null : new DateTimeOffset(cookie.Expires.ToUniversalTime()));
}
