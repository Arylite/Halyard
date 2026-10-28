using Microsoft.UI.Xaml;

namespace Halyard.UI;

/// <summary>
/// What UI services need from the main window (a handle for shell pickers, a XAML root for dialogs),
/// without depending on the window itself.
/// </summary>
public interface IWindowContext
{
    nint Handle { get; }

    XamlRoot XamlRoot { get; }
}
