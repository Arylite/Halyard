using Halyard.Core.Storage;
using Halyard.Models.Auth;
using Halyard.Views;
using Microsoft.Extensions.Logging;

namespace Halyard.UI;

/// <summary>Runs Epic's login page in an embedded WebView2 and captures the session once signed in.</summary>
public sealed class WebViewEpicLoginPrompt(IWindowContext window, AppPaths paths, ILogger<WebViewEpicLoginPrompt> logger) : IEpicLoginPrompt
{
    public async Task<EpicWebLogin?> SignInAsync(EpicWebLoginFlow flow)
    {
        var dialog = new EpicLoginDialog(flow, paths.WebViewDataDirectory, logger) { XamlRoot = window.XamlRoot };
        await DialogQueue.ShowAsync(dialog);
        return dialog.Result;
    }

    public void ForgetBrowserSession()
    {
        try
        {
            if (Directory.Exists(paths.WebViewDataDirectory))
            {
                Directory.Delete(paths.WebViewDataDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The browser process may still hold files for a moment; cookies expire on their own anyway.
            logger.LogWarning(ex, "Could not clear the sign-in browser profile");
        }
    }
}
