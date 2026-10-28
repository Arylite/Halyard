using Halyard.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        Settings = App.GetRequiredService<SettingsViewModel>();
        Account = App.GetRequiredService<AccountViewModel>();
        About = App.GetRequiredService<AboutViewModel>();
        InitializeComponent();
    }

    public AboutViewModel About { get; }

    public SettingsViewModel Settings { get; }

    public AccountViewModel Account { get; }
}
