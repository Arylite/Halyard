using Halyard.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.Views;

public sealed partial class DownloadsPage : Page
{
    public DownloadsPage()
    {
        ViewModel = App.GetRequiredService<DownloadsViewModel>();
        InitializeComponent();
    }

    public DownloadsViewModel ViewModel { get; }
}
