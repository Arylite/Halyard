namespace Halyard.Services.Auth;

/// <summary>
/// Runs on the authorization code page, which displays its JSON as the document body; posts the
/// code once the user is signed in (it is null before that).
/// </summary>
internal static class EpicWebLoginScript
{
    public const string ReadAuthorizationCode = """
        (() => {
            const post = message => window.chrome.webview.postMessage(message);
            try {
                const body = JSON.parse(document.body.innerText);
                post({ ok: !!body.authorizationCode, code: body.authorizationCode ?? null });
            } catch (error) {
                post({ ok: false, error: String(error) });
            }
        })();
        """;
}
