using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Halyard.Converters;

/// <summary>Maps a log level to a theme-aware Fluent brush.</summary>
public sealed partial class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = value switch
        {
            LogLevel.Critical or LogLevel.Error => "SystemFillColorCriticalBrush",
            LogLevel.Warning => "SystemFillColorCautionBrush",
            LogLevel.Information => "AccentTextFillColorPrimaryBrush",
            _ => "TextFillColorTertiaryBrush",
        };

        return (Brush)Application.Current.Resources[key];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
