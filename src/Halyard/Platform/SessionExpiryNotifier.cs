using Halyard.Core.Auth;
using Halyard.Core.Hosting;
using Halyard.UI;
using Halyard.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.Platform;

/// <summary>When Epic ends the session, says so plainly and offers to sign in again in one click.</summary>
public sealed class SessionExpiryNotifier(
    IAuthService auth,
    AccountViewModel account,
    INotificationService notifications,
    IUiDispatcher dispatcher) : IBackgroundStartupTask, IDisposable
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        auth.SessionExpired += OnSessionExpired;
        return Task.CompletedTask;
    }

    private void OnSessionExpired(object? sender, EventArgs e) => dispatcher.Post(() =>
        notifications.Show(new Notification(
            "Your Epic session expired",
            "Sign in again to see your games, install and play online.",
            InfoBarSeverity.Warning,
            "Sign in",
            () => account.SignInCommand.Execute(null))));

    public void Dispose() => auth.SessionExpired -= OnSessionExpired;
}
