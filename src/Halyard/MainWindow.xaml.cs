using Halyard.UI;
using Halyard.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace Halyard;

public sealed partial class MainWindow : Window
{
    private readonly INavigationService _navigation;
    private readonly QuickSearchViewModel _quickSearch;

    public MainWindow(
        ShellViewModel viewModel,
        QuickSearchViewModel quickSearch,
        INavigationService navigation,
        WindowContext windowContext)
    {
        ViewModel = viewModel;
        _quickSearch = quickSearch;
        _navigation = navigation;
        windowContext.Attach(this);

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new SizeInt32(1280, 820));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 420;
            presenter.PreferredMinimumHeight = 480;
        }

        _navigation.Initialize(ContentFrame);
        ContentFrame.Navigated += (_, _) => SyncSelection();
        RootNavigation.Loaded += (_, _) => _navigation.NavigateTo(PageKeys.Library);
    }

    public ShellViewModel ViewModel { get; }

    private void OnPaneToggleRequested(TitleBar sender, object args) =>
        RootNavigation.IsPaneOpen = !RootNavigation.IsPaneOpen;

    private void OnNotificationPointerEntered(object sender, PointerRoutedEventArgs e) =>
        ViewModel.IsNotificationHovered = true;

    private void OnNotificationPointerExited(object sender, PointerRoutedEventArgs e) =>
        ViewModel.IsNotificationHovered = false;

    private void OnQuickSearchShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        QuickSearch.Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    private void OnQuickSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            sender.ItemsSource = _quickSearch.Search(sender.Text);
        }
    }

    /// <summary>Enter takes the highlighted suggestion, or the best match for what was typed.</summary>
    private async void OnQuickSearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var result = args.ChosenSuggestion as QuickSearchResult ?? _quickSearch.Search(args.QueryText).FirstOrDefault();
        sender.Text = string.Empty;
        sender.ItemsSource = null;

        if (result is not null)
        {
            await _quickSearch.ActivateAsync(result);
        }
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string pageKey)
        {
            _navigation.NavigateTo(pageKey);
        }
    }

    /// <summary>The frame is the source of truth: highlight the item of whatever page is shown.</summary>
    private void SyncSelection()
    {
        var pageKey = ContentFrame.CurrentSourcePageType?.Name.Replace("Page", string.Empty, StringComparison.Ordinal);
        RootNavigation.SelectedItem = RootNavigation.MenuItems
            .Concat(RootNavigation.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag as string == pageKey);
    }
}
