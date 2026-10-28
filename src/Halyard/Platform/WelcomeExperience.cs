using Halyard.Core.Auth;
using Halyard.Core.Hosting;
using Halyard.Core.Settings;
using Halyard.Hosting;
using Halyard.UI;
using Halyard.ViewModels;
using Halyard.Views;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.Platform;

/// <summary>Shows the welcome once, the first time Halyard opens with a window, then offers sign-in.</summary>
public sealed class WelcomeExperience(
    StartupOptions options,
    ISettingsService settings,
    IAuthService auth,
    SettingsViewModel settingsViewModel,
    AccountViewModel account,
    IWindowContext window) : IBackgroundStartupTask
{
    private static readonly TimeSpan LayoutWait = TimeSpan.FromMilliseconds(100);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (options.StartHidden || settings.Current.HasCompletedWelcome)
        {
            return;
        }

        var xamlRoot = await WaitForXamlRootAsync(cancellationToken);
        var result = await DialogQueue.ShowAsync(new WelcomeDialog(settingsViewModel) { XamlRoot = xamlRoot });

        await settings.SaveAsync(settings.Current with { HasCompletedWelcome = true }, cancellationToken);

        // Only one dialog can be open at a time: sign-in starts once the welcome has closed.
        if (result == ContentDialogResult.Primary && auth.Session is null)
        {
            await account.SignInCommand.ExecuteAsync(null);
        }
    }

    /// <summary>The window's content may not be laid out yet when startup tasks begin.</summary>
    private async Task<Microsoft.UI.Xaml.XamlRoot> WaitForXamlRootAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (window.XamlRoot is { } root)
            {
                return root;
            }

            await Task.Delay(LayoutWait, cancellationToken);
        }
    }
}
