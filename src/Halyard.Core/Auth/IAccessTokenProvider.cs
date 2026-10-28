namespace Halyard.Core.Auth;

/// <summary>Hands out a valid access token for Epic's APIs, refreshing it as needed.</summary>
public interface IAccessTokenProvider
{
    /// <exception cref="EpicAuthException">Nobody is signed in, or the session can't be renewed.</exception>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
