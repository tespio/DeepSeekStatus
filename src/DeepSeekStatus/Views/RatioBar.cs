using System.Windows;
using System.Windows.Media;
using DeepSeekStatus.Support;

namespace DeepSeekStatus.Views;

public sealed class RatioBar : FrameworkElement
{
    private double _value;
    private Brush _accent = Brushes.CornflowerBlue;

    public double Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, 0, 1);
            if (Math.Abs(_value - clamped) < 0.0001)
            {
                return;
            }

            _value = clamped;
            InvalidateVisual();
        }
    }

    public Brush Accent
    {
        get => _accent;
        set
        {
            _accent = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext context)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var radius = height / 2;
        context.DrawRoundedRectangle(Theme.Brush(Theme.ControlTrack), null, new Rect(0, 0, width, height), radius, radius);

        var fillWidth = Math.Max(height * 0.7, width * _value);
        var accent = _accent;
        if (_value <= 0)
        {
            return;
        }

        if (accent is SolidColorBrush solid)
        {
            var start = Color.FromArgb(0xBF, solid.Color.R, solid.Color.G, solid.Color.B);
            var gradient = new LinearGradientBrush(start, solid.Color, new Point(0, 0), new Point(width, 0))
            {
                MappingMode = BrushMappingMode.Absolute,
            };
            gradient.Freeze();
            context.DrawRoundedRectangle(gradient, null, new Rect(0, 0, fillWidth, height), radius, radius);
        }
        else
        {
            context.DrawRoundedRectangle(accent, null, new Rect(0, 0, fillWidth, height), radius, radius);
        }
    }
}
