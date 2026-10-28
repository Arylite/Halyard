namespace Halyard.Models.Auth;

/// <summary>
/// How to sign in through a browser. Open <paramref name="LoginUrl"/>; once the browser is on any
/// <paramref name="Domain"/> page outside <paramref name="LoginPathPrefix"/>, the user is past the
/// login and is sent to <paramref name="ProbePage"/>. There, <paramref name="ProbeScript"/> posts
/// <c>{ ok, code }</c> through <c>chrome.webview.postMessage</c>; <c>ok</c> is true once signed in,
/// and <c>code</c> is an authorization code.
/// </summary>
public sealed record EpicWebLoginFlow(
    Uri LoginUrl,
    Uri ProbePage,
    string Domain,
    string LoginPathPrefix,
    string ProbeScript);
