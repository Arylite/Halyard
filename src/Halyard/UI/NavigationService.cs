using Halyard.Views;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.UI;

public sealed class NavigationService : INavigationService
{
    private static readonly Dictionary<string, Type> Pages = new()
    {
        [PageKeys.Library] = typeof(LibraryPage),
        [PageKeys.Downloads] = typeof(DownloadsPage),
        [PageKeys.Logs] = typeof(LogsPage),
        [PageKeys.Settings] = typeof(SettingsPage),
    };

    private Frame? _frame;

    public void Initialize(Frame frame) => _frame = frame;

    public bool NavigateTo(string pageKey)
    {
        var frame = _frame ?? throw new InvalidOperationException("Navigation has not been initialized with a frame.");
        if (!Pages.TryGetValue(pageKey, out var pageType))
        {
            throw new ArgumentException($"Unknown page '{pageKey}'.", nameof(pageKey));
        }

        return frame.CurrentSourcePageType == pageType || frame.Navigate(pageType);
    }
}
