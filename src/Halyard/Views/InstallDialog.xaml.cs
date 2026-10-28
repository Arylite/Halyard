using Halyard.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.Views;

public sealed partial class InstallDialog : ContentDialog
{
    public InstallDialog(InstallDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public InstallDialogViewModel ViewModel { get; }
}
