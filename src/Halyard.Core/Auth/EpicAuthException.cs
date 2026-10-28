namespace Halyard.Core.Auth;

/// <summary>Epic rejected a request.</summary>
public class EpicAuthException(string message) : Exception(message);

/// <summary>The web session is no longer valid; the user must sign in again.</summary>
public sealed class EpicSessionExpiredException()
    : EpicAuthException("Your Epic session has expired. Sign in again.");
