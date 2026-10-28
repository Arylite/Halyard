using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Halyard.Core.Auth;
using Halyard.UI;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.ViewModels;

public sealed partial class AccountViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly IEpicLoginPrompt _loginPrompt;
    private readonly INotificationService _notifications;

    public AccountViewModel(
        IAuthService auth,
        IEpicLoginPrompt loginPrompt,
        IUiDispatcher dispatcher,
        INotificationService notifications)
    {
        _auth = auth;
        _loginPrompt = loginPrompt;
        _notifications = notifications;

        auth.SessionChanged += (_, _) => dispatcher.Post(() => OnPropertyChanged(string.Empty));
    }

    public bool IsSignedIn => _auth.Session is not null;

    public bool IsSignedOut => !IsSignedIn;

    public string DisplayName => _auth.Session?.DisplayName ?? "Not signed in";

    public string Details => _auth.Session is { } session
        ? $"Account {session.AccountId}"
        : "Sign in to see your games, install them and play online.";

    [RelayCommand]
    private async Task SignInAsync()
    {
        try
        {
            if (await _loginPrompt.SignInAsync(_auth.WebLoginFlow) is not { } login)
            {
                return;
            }

            await _auth.SignInAsync(login);
            _notifications.Show("Signed in", $"Welcome, {DisplayName}. Your library is loading.", InfoBarSeverity.Success);
        }
        catch (Exception ex) when (ex is EpicAuthException or HttpRequestException)
        {
            _notifications.Show("Sign-in failed", ex.Message, InfoBarSeverity.Error);
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _auth.SignOutAsync();
        _loginPrompt.ForgetBrowserSession();
    }
}
