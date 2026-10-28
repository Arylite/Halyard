namespace Halyard.Services.Auth;

internal static class EpicEndpoints
{
    // epicgames.com web session
    public static readonly Uri Site = new("https://www.epicgames.com");

    /// <summary>
    /// Shows JSON with an authorization code for the launcher client once the browser is signed in
    /// (and a null code otherwise). It's where login lands, and what renews an expired session.
    /// </summary>
    public static readonly Uri AuthorizationCodePage =
        new(Site, $"/id/api/redirect?clientId={EpicLauncherClient.ClientId}&responseType=code");

    public static readonly Uri Login = new(Site, $"/id/login?redirectUrl={Uri.EscapeDataString(AuthorizationCodePage.AbsoluteUri)}");

    public static readonly Uri Csrf = new(Site, "/id/api/csrf");

    public static readonly Uri Logout = new(Site, "/id/api/logout");

    // Account service (OAuth)
    private const string OAuth = "https://account-public-service-prod.ol.epicgames.com/account/api/oauth";

    public static readonly Uri Token = new($"{OAuth}/token");

    public static readonly Uri ExchangeCode = new($"{OAuth}/exchange");

    public static Uri KillSession(string accessToken) => new($"{OAuth}/sessions/kill/{Uri.EscapeDataString(accessToken)}");

    // Launcher and catalog services
    private const string Launcher = "https://launcher-public-service-prod06.ol.epicgames.com/launcher/api/public";

    public static readonly Uri OwnedAssets = new($"{Launcher}/assets/Windows?label=Live");

    public static Uri ManifestLocation(string @namespace, string catalogItemId, string appName) =>
        new($"{Launcher}/assets/v2/platform/Windows/namespace/{Uri.EscapeDataString(@namespace)}" +
            $"/catalogItem/{Uri.EscapeDataString(catalogItemId)}/app/{Uri.EscapeDataString(appName)}/label/Live");

    public static Uri CatalogItems(string @namespace, IEnumerable<string> ids) =>
        new($"https://catalog-public-service-prod06.ol.epicgames.com/catalog/api/shared/namespace/{Uri.EscapeDataString(@namespace)}/bulk/items" +
            $"?{string.Join('&', ids.Select(id => $"id={Uri.EscapeDataString(id)}"))}" +
            "&includeDLCDetails=true&includeMainGameDetails=true&country=US&locale=en-US");
}
