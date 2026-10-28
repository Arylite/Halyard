using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Halyard.Controls;

/// <summary>
/// Area chart of recent speeds, scaled to its peak. Each instance owns its shapes and point
/// collections: WinUI forbids sharing a PointCollection between elements. The shapes sit in a
/// Canvas, which reports no size of its own: the graph fills the space it is given and redrawing
/// can never resize it (which would trigger another redraw, endlessly).
/// </summary>
public sealed partial class SpeedGraph : UserControl
{
    public static readonly DependencyProperty SamplesProperty = DependencyProperty.Register(
        nameof(Samples), typeof(IReadOnlyList<double>), typeof(SpeedGraph), new PropertyMetadata(null, (d, _) => ((SpeedGraph)d).Redraw()));

    private readonly Polygon _area = new() { Opacity = 0.35 };
    private readonly Polyline _line = new() { StrokeThickness = 1.5, StrokeLineJoin = PenLineJoin.Round };

    public SpeedGraph()
    {
        _area.Fill = (Brush)Application.Current.Resources["AccentFillColorSecondaryBrush"];
        _line.Stroke = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        Content = new Canvas { Children = { _area, _line } };
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        SizeChanged += (_, args) =>
        {
            // Only a real size change matters; ignore sub-pixel jitter.
            if (Math.Abs(args.NewSize.Width - args.PreviousSize.Width) >= 1 ||
                Math.Abs(args.NewSize.Height - args.PreviousSize.Height) >= 1)
            {
                Redraw();
            }
        };
    }

    public IReadOnlyList<double>? Samples
    {
        get => (IReadOnlyList<double>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    private void Redraw()
    {
        var samples = Samples;
        var width = ActualWidth;
        var height = ActualHeight;
        if (samples is not { Count: > 1 } || width <= 0 || height <= 0)
        {
            _area.Points = [];
            _line.Points = [];
            return;
        }

        var peak = Math.Max(samples.Max(), 1);
        var step = width / (samples.Count - 1);

        var line = new PointCollection();
        for (var i = 0; i < samples.Count; i++)
        {
            line.Add(new Point(i * step, height - (samples[i] / peak * height)));
        }

        var area = new PointCollection { new Point(0, height) };
        foreach (var point in line)
        {
            area.Add(point);
        }

        area.Add(new Point(width, height));

        _line.Points = line;
        _area.Points = area;
    }
}
