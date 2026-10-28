using System.Text.Json;
using Halyard.Models.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Halyard.Views;

/// <summary>
/// Hosts Epic's sign-in. Once past the login pages, the browser is sent to the authorization code
/// page, which is read periodically; the first code means the user is signed in, and the dialog
/// closes with that code and the session cookies. Polling (rather than reacting to one navigation
/// event) survives Epic's redirects and client-side routing.
/// </summary>
public sealed partial class EpicLoginDialog : ContentDialog
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(1.5);

    private readonly EpicWebLoginFlow _flow;
    private readonly string _userDataDirectory;
    private readonly ILogger _logger;
    private readonly DispatcherQueueTimer _probeTimer;

    public EpicLoginDialog(EpicWebLoginFlow flow, string userDataDirectory, ILogger logger)
    {
        _flow = flow;
        _userDataDirectory = userDataDirectory;
        _logger = logger;

        InitializeComponent();

        _probeTimer = DispatcherQueue.CreateTimer();
        _probeTimer.Interval = ProbeInterval;
        _probeTimer.Tick += (_, _) => Probe();

        Loaded += OnLoaded;
        Closed += (_, _) => _probeTimer.Stop();
    }

    public EpicWebLogin? Result { get; private set; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // A dedicated profile keeps Epic's cookies out of the exe folder; Chromium encrypts them with DPAPI.
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                browserExecutableFolder: string.Empty,
                userDataFolder: _userDataDirectory,
                options: new CoreWebView2EnvironmentOptions
                {
                    // No UDP sockets for WebRTC: keeps local IPs private and avoids a Windows Firewall prompt.
                    AdditionalBrowserArguments = "--force-webrtc-ip-handling-policy=disable_non_proxied_udp",
                });

            await Browser.EnsureCoreWebView2Async(environment);
            var core = Browser.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.NavigationCompleted += (_, args) =>
            {
                _logger.LogDebug("Sign-in browser at {Page} (success: {Success})", core.Source, args.IsSuccess);
                OnPageLoaded(new Uri(core.Source));
            };
            core.WebMessageReceived += OnWebMessageReceived;

            Browser.Source = _flow.LoginUrl;
            _probeTimer.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The sign-in browser could not start");
            StatusText.Text = "The sign-in browser could not start. Is the WebView2 runtime installed?";
        }
    }

    /// <summary>
    /// Epic may land anywhere after login (account settings on accounts.epicgames.com, switch-account...).
    /// Any Epic page past the login means "signed in": go to the probe page to fetch the code.
    /// </summary>
    private void OnPageLoaded(Uri page)
    {
        var isEpicPage = page.Host == _flow.Domain || page.Host.EndsWith($".{_flow.Domain}", StringComparison.OrdinalIgnoreCase);
        var isLoginPage = page.AbsolutePath.StartsWith(_flow.LoginPathPrefix, StringComparison.OrdinalIgnoreCase);

        if (IsProbePage(page))
        {
            ShowStatus("Signing in...");
        }
        else if (isEpicPage && !isLoginPage)
        {
            ShowStatus("Signing in...");
            Browser.Source = _flow.ProbePage;
        }
        else
        {
            StatusPanel.Visibility = Visibility.Collapsed;
            Browser.Visibility = Visibility.Visible;
        }
    }

    private async void Probe()
    {
        if (Browser.CoreWebView2 is not { } core || Result is not null || !IsProbePage(new Uri(core.Source)))
        {
            return;
        }

        try
        {
            await core.ExecuteScriptAsync(_flow.ProbeScript);
        }
        catch (Exception ex)
        {
            // The page navigated away mid-call; the next tick tries again.
            _logger.LogDebug(ex, "Sign-in check interrupted");
        }
    }

    private bool IsProbePage(Uri page) =>
        page.Host.Equals(_flow.ProbePage.Host, StringComparison.OrdinalIgnoreCase) &&
        page.AbsolutePath.Equals(_flow.ProbePage.AbsolutePath, StringComparison.OrdinalIgnoreCase);

    private void ShowStatus(string text)
    {
        StatusText.Text = text;
        StatusPanel.Visibility = Visibility.Visible;
        Browser.Visibility = Visibility.Collapsed;
    }

    private async void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (Result is not null)
        {
            return;
        }

        string? code;
        using (var message = JsonDocument.Parse(args.WebMessageAsJson))
        {
            var root = message.RootElement;
            var ok = root.TryGetProperty("ok", out var okValue) && okValue.ValueKind == JsonValueKind.True;
            code = ok && root.TryGetProperty("code", out var codeValue) ? codeValue.GetString() : null;
        }

        if (string.IsNullOrEmpty(code))
        {
            _logger.LogInformation("Sign-in check on {Page}: {Response}", new Uri(sender.Source).AbsolutePath, args.WebMessageAsJson);
            return;
        }

        _probeTimer.Stop();
        var cookies = await sender.CookieManager.GetCookiesAsync(_flow.ProbePage.GetLeftPart(UriPartial.Authority));
        Result = new EpicWebLogin(code, new EpicWebCredentials([.. cookies.Select(ToSessionCookie)], sender.Settings.UserAgent));
        _logger.LogInformation("Browser sign-in detected");
        Hide();
    }

    private static SessionCookie ToSessionCookie(CoreWebView2Cookie cookie) => new(
        cookie.Name,
        cookie.Value,
        cookie.Domain,
        cookie.Path,
        cookie.IsSession ? null : DateTimeOffset.FromUnixTimeMilliseconds((long)(cookie.Expires * 1000)));
}
