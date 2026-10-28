using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.Controls;

/// <summary>
/// A row in the style of the Windows 11 Settings app: icon, header, description and a control on the right.
/// The control moves under the text when the card gets narrow.
/// </summary>
public sealed partial class SettingsCard : UserControl
{
    private const double StackedBelowWidth = 520;

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionProperty =
        DependencyProperty.Register(nameof(Action), typeof(object), typeof(SettingsCard), new PropertyMetadata(null));

    public SettingsCard() => InitializeComponent();

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public object? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var stacked = e.NewSize.Width < StackedBelowWidth;
        Grid.SetRow(ActionPresenter, stacked ? 1 : 0);
        Grid.SetColumn(ActionPresenter, stacked ? 1 : 2);
        ActionPresenter.HorizontalAlignment = stacked ? HorizontalAlignment.Left : HorizontalAlignment.Right;
    }
}
