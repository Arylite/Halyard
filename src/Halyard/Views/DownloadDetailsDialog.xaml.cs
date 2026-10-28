using Halyard.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.Views;

public sealed partial class DownloadDetailsDialog : ContentDialog
{
    public DownloadDetailsDialog(DownloadItemViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public DownloadItemViewModel ViewModel { get; }
}
