namespace Halyard.Models.Auth;

public sealed record EpicTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt)
{
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(5);

    public bool HasValidAccessToken(DateTimeOffset now) => AccessTokenExpiresAt - now > ExpiryMargin;

    public bool CanRefresh(DateTimeOffset now) => RefreshTokenExpiresAt - now > ExpiryMargin;
}
