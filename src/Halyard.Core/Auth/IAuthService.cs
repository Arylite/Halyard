using Halyard.Models.Auth;

namespace Halyard.Core.Auth;

/// <summary>Owns the current Epic session: sign-in, sign-out and exchange codes for games.</summary>
public interface IAuthService
{
    EpicSession? Session { get; }

    /// <summary>Raised on any thread when the user signs in or out.</summary>
    event EventHandler? SessionChanged;

    /// <summary>Raised on any thread when Epic ended the session on its own: the user must sign in again.</summary>
    event EventHandler? SessionExpired;

    /// <summary>What an embedded browser needs to sign the user in.</summary>
    EpicWebLoginFlow WebLoginFlow { get; }

    Task SignInAsync(EpicWebLogin login, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);

    Task<string> CreateExchangeCodeAsync(CancellationToken cancellationToken = default);
}
