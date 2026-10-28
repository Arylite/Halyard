namespace Halyard.Models.Auth;

/// <summary>What Epic's token endpoint returns: the account and its tokens.</summary>
public sealed record EpicGrant(string AccountId, string DisplayName, EpicTokens Tokens);
