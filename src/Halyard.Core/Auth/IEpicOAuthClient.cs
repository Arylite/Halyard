using Halyard.Models.Auth;

namespace Halyard.Core.Auth;

/// <summary>Epic's account service, as the launcher client: the tokens its other APIs accept.</summary>
public interface IEpicOAuthClient
{
    Task<EpicGrant> RedeemAuthorizationCodeAsync(string authorizationCode, CancellationToken cancellationToken);

    Task<EpicGrant> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>A single-use code a game redeems for its own session.</summary>
    Task<string> CreateExchangeCodeAsync(string accessToken, CancellationToken cancellationToken);

    Task KillSessionAsync(string accessToken, CancellationToken cancellationToken);
}
