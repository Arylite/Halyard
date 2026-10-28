using Halyard.Models.Auth;

namespace Halyard.Core.Auth;

/// <summary>
/// Calls epicgames.com with a signed-in browser's cookies. Epic may refresh cookies on any call,
/// so every method returns the credentials to keep using.
/// </summary>
public interface IEpicWebClient
{
    /// <summary>A single-use code for the launcher client, minted from the browser session.</summary>
    /// <exception cref="EpicSessionExpiredException">The browser session has ended.</exception>
    Task<(string AuthorizationCode, EpicWebCredentials Credentials)> CreateAuthorizationCodeAsync(
        EpicWebCredentials credentials,
        CancellationToken cancellationToken);

    Task LogoutAsync(EpicWebCredentials credentials, CancellationToken cancellationToken);
}
