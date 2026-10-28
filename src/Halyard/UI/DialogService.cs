using Halyard.Core.Installs;
using Halyard.Core.Settings;
using Halyard.Models.Downloads;
using Halyard.Models.Library;
using Halyard.ViewModels;
using Halyard.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.UI;

public sealed class DialogService(
    IWindowContext window,
    IInstallPreparer preparer,
    ISettingsService settings,
    IPickerService pickers) : IDialogService
{
    public async Task<InstallRequest?> ShowInstallAsync(Game game, string? existingFolder = null)
    {
        var viewModel = new InstallDialogViewModel(game, preparer, settings, pickers, existingFolder);
        var dialog = new InstallDialog(viewModel) { XamlRoot = window.XamlRoot };

        // The manifest loads while the dialog is already on screen.
        _ = viewModel.LoadAsync();
        return await DialogQueue.ShowAsync(dialog) == ContentDialogResult.Primary ? viewModel.CreateRequest() : null;
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = confirmText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = window.XamlRoot,

            // WinUI 3 only applies the Windows 11 dialog look when asked explicitly.
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
        };

        return await DialogQueue.ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    public async Task ShowDownloadDetailsAsync(DownloadItemViewModel download) =>
        await DialogQueue.ShowAsync(new DownloadDetailsDialog(download) { XamlRoot = window.XamlRoot });

    public async Task<LaunchOptions?> EditLaunchOptionsAsync(Game game)
    {
        var arguments = new TextBox
        {
            Header = "Extra arguments",
            PlaceholderText = "-dx12 -nosplash",
            Text = game.CustomArguments ?? string.Empty,
        };

        var offline = new ToggleSwitch
        {
            Header = "Launch offline",
            IsOn = game.LaunchOffline,
            Visibility = game.IsEpicGame ? Visibility.Visible : Visibility.Collapsed,
        };

        var hint = new TextBlock
        {
            Text = "Offline skips Epic sign-in: online features and multiplayer won't work.",
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["SecondaryTextStyle"],
            Visibility = offline.Visibility,
        };

        var dialog = new ContentDialog
        {
            Title = $"{game.Title} launch options",
            Content = new StackPanel { Spacing = 12, MinWidth = 420, Children = { arguments, offline, hint } },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
        };

        return await DialogQueue.ShowAsync(dialog) == ContentDialogResult.Primary
            ? new LaunchOptions(string.IsNullOrWhiteSpace(arguments.Text) ? null : arguments.Text.Trim(), offline.IsOn)
            : null;
    }
}
