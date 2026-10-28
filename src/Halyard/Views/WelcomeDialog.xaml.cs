using Halyard.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.Views;

public sealed partial class WelcomeDialog : ContentDialog
{
    public WelcomeDialog(SettingsViewModel settings)
    {
        Settings = settings;
        InitializeComponent();
    }

    /// <summary>The live settings: changes made here apply (and save) immediately.</summary>
    public SettingsViewModel Settings { get; }
}
