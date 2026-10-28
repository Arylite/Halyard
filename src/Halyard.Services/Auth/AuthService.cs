using Halyard.Core.Auth;
using Halyard.Core.Hosting;
using Halyard.Models.Auth;
using Microsoft.Extensions.Logging;

namespace Halyard.Services.Auth;

/// <summary>
/// Signs in from a browser session, then keeps launcher tokens alive: refresh token first, and when
/// that has expired too, a new exchange code minted from the browser session. The user only signs
/// in again once Epic ends the web session itself.
/// </summary>
public sealed class AuthService(
    IEpicWebClient web,
    IEpicOAuthClient oauth,
    ISessionStore store,
    TimeProvider time,
    ILogger<AuthService> logger) : IAuthService, IAccessTokenProvider, IAsyncInitializer, IDisposable
{
    private readonly SemaphoreSlim _renewal = new(1, 1);

    public EpicSession? Session { get; private set; }

    public event EventHandler? SessionChanged;

    public event EventHandler? SessionExpired;

    public EpicWebLoginFlow WebLoginFlow { get; } =
        new(EpicEndpoints.Login, EpicEndpoints.AuthorizationCodePage, Domain: "epicgames.com", LoginPathPrefix: "/id/login", EpicWebLoginScript.ReadAuthorizationCode);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        Session = await store.LoadAsync(cancellationToken);
        if (Session is not null)
        {
            logger.LogInformation("Restored session for {Account}", Session.DisplayName);
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task SignInAsync(EpicWebLogin login, CancellationToken cancellationToken = default)
    {
        var grant = await oauth.RedeemAuthorizationCodeAsync(login.AuthorizationCode, cancellationToken);

        await SetSessionAsync(new EpicSession(grant.AccountId, grant.DisplayName, login.Credentials, grant.Tokens), cancellationToken);
        logger.LogInformation("Signed in as {Account}", grant.DisplayName);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        if (Session is not { } session)
        {
            return;
        }

        // Best effort on both sides; the local session goes away regardless.
        try
        {
            await oauth.KillSessionAsync(session.Tokens.AccessToken, cancellationToken);
            await web.LogoutAsync(session.Web, cancellationToken);
        }
        catch (Exception ex) when (ex is EpicAuthException or HttpRequestException)
        {
            logger.LogWarning(ex, "Could not end the session on Epic's side");
        }

        ClearSession();
        logger.LogInformation("Signed out {Account}", session.DisplayName);
    }

    public async Task<string> CreateExchangeCodeAsync(CancellationToken cancellationToken = default) =>
        await oauth.CreateExchangeCodeAsync(await GetAccessTokenAsync(cancellationToken), cancellationToken);

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _renewal.WaitAsync(cancellationToken);
        try
        {
            var session = Session ?? throw new EpicAuthException("Sign in to your Epic account first.");
            var now = time.GetUtcNow();
            if (session.Tokens.HasValidAccessToken(now))
            {
                return session.Tokens.AccessToken;
            }

            var renewed = await RenewAsync(session, now, cancellationToken);
            await SetSessionAsync(renewed, cancellationToken);
            return renewed.Tokens.AccessToken;
        }
        catch (EpicSessionExpiredException)
        {
            logger.LogWarning("Epic session expired");
            ClearSession();
            SessionExpired?.Invoke(this, EventArgs.Empty);
            throw;
        }
        finally
        {
            _renewal.Release();
        }
    }

    private async Task<EpicSession> RenewAsync(EpicSession session, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (session.Tokens.CanRefresh(now))
        {
            logger.LogDebug("Refreshing access token");
            var refreshed = await oauth.RefreshAsync(session.Tokens.RefreshToken, cancellationToken);
            return session with { Tokens = refreshed.Tokens };
        }

        logger.LogDebug("Refresh token expired; minting a new grant from the web session");
        var (authorizationCode, webCredentials) = await web.CreateAuthorizationCodeAsync(session.Web, cancellationToken);
        var grant = await oauth.RedeemAuthorizationCodeAsync(authorizationCode, cancellationToken);
        return session with { Web = webCredentials, Tokens = grant.Tokens };
    }

    private async Task SetSessionAsync(EpicSession session, CancellationToken cancellationToken)
    {
        await store.SaveAsync(session, cancellationToken);
        Session = session;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearSession()
    {
        store.Clear();
        Session = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _renewal.Dispose();
}
