using Microsoft.Windows.AppLifecycle;

namespace Halyard.Hosting;

/// <summary>
/// Keeps one Halyard per user: launching it again (shortcut, autostart, notification click)
/// forwards the activation to the running instance, which brings its window back.
/// </summary>
public static class SingleInstance
{
    private const string Key = "Halyard.Main";

    /// <summary>Raised in the running instance, on a background thread, when another launch was redirected to it.</summary>
    public static event EventHandler? Reactivated;

    /// <returns><see langword="true"/> if another instance took over and this process should exit.</returns>
    public static bool TryRedirectToRunningInstance()
    {
        var main = AppInstance.FindOrRegisterForKey(Key);
        if (main.IsCurrent)
        {
            main.Activated += (_, _) => Reactivated?.Invoke(null, EventArgs.Empty);
            return false;
        }

        // Redirect from a worker thread: blocking the STA main thread on it can deadlock.
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        Task.Run(() => main.RedirectActivationToAsync(activation).AsTask()).Wait();
        return true;
    }
}
