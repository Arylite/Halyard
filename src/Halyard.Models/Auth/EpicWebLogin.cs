namespace Halyard.Models.Auth;

/// <summary>What a completed browser sign-in yields: a fresh authorization code and the browser's session.</summary>
public sealed record EpicWebLogin(string AuthorizationCode, EpicWebCredentials Credentials);
