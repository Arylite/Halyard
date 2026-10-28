using Microsoft.UI.Xaml.Controls;

namespace Halyard.UI;

public interface INavigationService
{
    void Initialize(Frame frame);

    bool NavigateTo(string pageKey);
}
