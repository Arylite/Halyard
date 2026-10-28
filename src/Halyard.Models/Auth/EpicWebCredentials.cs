namespace Halyard.Models.Auth;

/// <summary>
/// What identifies the browser that signed in: its cookies and its user agent
/// (Epic's bot protection ties some cookies to the user agent that obtained them).
/// </summary>
public sealed record EpicWebCredentials(IReadOnlyList<SessionCookie> Cookies, string UserAgent);
