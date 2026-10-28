using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Halyard.UI;

public sealed class WindowContext : IWindowContext
{
    private Window? _window;

    public nint Handle => WindowNative.GetWindowHandle(Window);

    /// <summary><see langword="null"/> until the window's content has been laid out once.</summary>
    public XamlRoot XamlRoot => Window.Content?.XamlRoot!;

    private Window Window => _window ?? throw new InvalidOperationException("The main window has not been created yet.");

    public void Attach(Window window) => _window = window;
}
