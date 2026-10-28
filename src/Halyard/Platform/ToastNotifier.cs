namespace Halyard.Platform;

/// <summary>
/// Windows notifications, sent through the tray icon: Windows shows them as regular toasts in the
/// notification center, without the app registration that self-contained builds can't provide.
/// </summary>
public sealed class ToastNotifier(TrayIcon tray)
{
    public void Show(string title, string message) => tray.ShowNotification(title, message);
}
