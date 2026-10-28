using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.Controls;

/// <summary>Page title with its actions on the right, or below it when there isn't room.</summary>
public sealed partial class PageHeader : UserControl
{
    private const double StackedBelowWidth = 560;

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionsProperty =
        DependencyProperty.Register(nameof(Actions), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    public PageHeader() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var stacked = e.NewSize.Width < StackedBelowWidth;
        Grid.SetRow(ActionsPresenter, stacked ? 1 : 0);
        Grid.SetColumn(ActionsPresenter, stacked ? 0 : 1);
    }
}
