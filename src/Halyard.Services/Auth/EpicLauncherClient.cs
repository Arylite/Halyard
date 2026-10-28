using System.Net.Http.Headers;
using System.Text;

namespace Halyard.Services.Auth;

/// <summary>
/// The Epic Games Launcher's public OAuth client. Epic's library and download APIs only accept tokens
/// issued to a launcher client, so like every alternative launcher (Legendary, Heroic) Halyard presents
/// itself as it. The user never sees or configures this: they only sign in to their own account.
/// </summary>
internal static class EpicLauncherClient
{
    public const string ClientId = "34a02cf8f4414e29b15921876da36f9a";
    private const string ClientSecret = "daafbccc737745039dffe53d94fc76cf";

    /// <summary>Some launcher endpoints check for the launcher's user agent.</summary>
    public const string UserAgent = "UELauncher/17.0.1-37584233+++Portal+Release-Live Windows/10.0.26100.1.256.64bit";

    public static AuthenticationHeaderValue BasicAuthorization { get; } =
        new("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{ClientId}:{ClientSecret}")));
}
