namespace Halyard.Models.Auth;

/// <summary>
/// A signed-in Epic account. The web credentials prove who the user is; the tokens are what Epic's
/// launcher APIs accept. When the tokens expire for good, the web session mints new ones.
/// </summary>
public sealed record EpicSession(
    string AccountId,
    string DisplayName,
    EpicWebCredentials Web,
    EpicTokens Tokens);
