using System.Windows;
using System.Windows.Media;

namespace DeepSeekStatus.Views;

public sealed class DailySpendChart : FrameworkElement
{
    private IReadOnlyList<decimal> _values = Array.Empty<decimal>();
    private decimal _max;
    private Brush _accent = Brushes.CornflowerBlue;
    private Brush _track = Brushes.Gray;

    public void SetData(IReadOnlyList<decimal> values, decimal max, Brush accent, Brush track)
    {
        _values = values;
        _max = max;
        _accent = accent;
        _track = track;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 1 || height <= 1 || _values.Count == 0)
        {
            return;
        }

        const double gap = 2;
        var count = _values.Count;
        var barWidth = Math.Max(1.5, (width - gap * (count - 1)) / count);
        var radius = Math.Min(2, barWidth / 2);

        for (var index = 0; index < count; index++)
        {
            var value = _values[index];
            var ratio = _max > 0 ? (double)(value / _max) : 0;
            var barHeight = value > 0 ? Math.Max(2.5, ratio * (height - 1)) : 1.5;
            var x = index * (barWidth + gap);
            var rect = new Rect(x, height - barHeight, barWidth, barHeight);
            context.DrawRoundedRectangle(value > 0 ? _accent : _track, null, rect, radius, radius);
        }
    }
}
