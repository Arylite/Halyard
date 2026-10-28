using System.Net;
using Halyard.Core.Auth;

namespace Halyard.Services.Http;

internal static class EpicHttp
{
    /// <summary>Turns Epic's error responses into exceptions the UI can show as-is.</summary>
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new EpicSessionExpiredException();
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new EpicAuthException(
            $"Epic returned {(int)response.StatusCode} {response.ReasonPhrase} for {response.RequestMessage?.RequestUri?.AbsolutePath}: {Truncate(body)}");
    }

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300] + "...";
}
