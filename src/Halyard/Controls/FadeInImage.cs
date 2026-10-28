using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Halyard.Controls;

/// <summary>Attached behaviour: an image stays transparent until decoded, then fades in instead of popping.</summary>
public static class FadeInImage
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(220);

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(FadeInImage), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(Image image) => (bool)image.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(Image image, bool value) => image.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not Image image || args.NewValue is not true)
        {
            return;
        }

        image.Opacity = 0;
        image.OpacityTransition = new ScalarTransition { Duration = Duration };
        image.ImageOpened += (_, _) => image.Opacity = 1;

        // Recycled containers get a new source: hide until it is decoded, unless it already is
        // (a cached image doesn't raise ImageOpened again).
        image.RegisterPropertyChangedCallback(Image.SourceProperty, (_, _) =>
            image.Opacity = image.Source is BitmapImage { PixelWidth: > 0 } ? 1 : 0);
    }
}
