using Microsoft.UI.Xaml.Controls;

namespace Halyard.UI;

/// <summary>
/// WinUI allows one open <see cref="ContentDialog"/> at a time and throws for a second one (the
/// welcome and a session-expired sign-in, a quick-search install over a confirmation...). Every
/// dialog goes through here and waits for the previous one to close instead.
/// </summary>
/// <remarks>A dialog must never open another one from inside: the second would wait forever.</remarks>
public static class DialogQueue
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        await Gate.WaitAsync();
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            Gate.Release();
        }
    }
}
