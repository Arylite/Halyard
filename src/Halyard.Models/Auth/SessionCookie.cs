namespace Halyard.Models.Auth;

/// <param name="Expires"><see langword="null"/> for a session cookie.</param>
public sealed record SessionCookie(string Name, string Value, string Domain, string Path, DateTimeOffset? Expires);
