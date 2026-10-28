using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace Halyard.Platform;

/// <summary>Shows, hides and really closes the main window (closing normally only hides it to the tray).</summary>
public sealed partial class WindowPresenter(MainWindow window, DispatcherQueue dispatcherQueue)
{
    public bool IsExiting { get; private set; }

    public bool IsVisible => window.AppWindow.IsVisible;

    /// <summary>Brings the window back from the tray or from behind other windows; safe from any thread.</summary>
    public void Show() => dispatcherQueue.TryEnqueue(() =>
    {
        window.AppWindow.Show();
        window.Activate();
        SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(window));
    });

    public void Hide() => window.AppWindow.Hide();

    /// <summary>Closes for good, bypassing close-to-tray.</summary>
    public void Exit() => dispatcherQueue.TryEnqueue(() =>
    {
        IsExiting = true;
        window.Close();
    });

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hwnd);
}
