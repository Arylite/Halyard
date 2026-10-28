using Halyard.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.Views;

public sealed partial class LibraryPage : Page
{
    /// <summary>Below this toolbar width the search box moves under the filters.</summary>
    private const double StackedToolbarWidth = 820;

    public LibraryPage()
    {
        ViewModel = App.GetRequiredService<LibraryViewModel>();
        InitializeComponent();
        FilterBar.SelectedItem = FilterBar.Items[ViewModel.FilterIndex];
    }

    public LibraryViewModel ViewModel { get; }

    // SelectorBar has no index property to bind; translate its selection here.
    private void OnFilterChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) =>
        ViewModel.FilterIndex = sender.Items.IndexOf(sender.SelectedItem);

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var stacked = e.NewSize.Width < StackedToolbarWidth;
        SearchColumn.Width = stacked ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
        Grid.SetRow(SearchArea, stacked ? 1 : 0);
        Grid.SetColumn(SearchArea, stacked ? 0 : 1);
    }
}
