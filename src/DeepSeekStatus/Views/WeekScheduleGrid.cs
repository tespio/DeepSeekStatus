using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DeepSeekStatus.Models;
using DeepSeekStatus.Support;

namespace DeepSeekStatus.Views;

public sealed class WeekScheduleGrid : FrameworkElement
{
    private const double CellHeight = 10;
    private const double Gap = 2;
    private const double LabelWidth = 35;
    private const double TickHeight = 12;
    private const int Columns = 24;
    private const int Rows = 7;

    private DateOnly _referenceDate = DateOnly.FromDateTime(DateTime.Now);

    public DateOnly ReferenceDate
    {
        get => _referenceDate;
        set
        {
            if (_referenceDate == value)
            {
                return;
            }

            _referenceDate = value;
            InvalidateVisual();
        }
    }

    public static double ContentHeight => Rows * CellHeight + (Rows - 1) * Gap + TickHeight;

    protected override void OnRender(DrawingContext context)
    {
        var size = RenderSize;
        var availableWidth = size.Width - LabelWidth - (Columns - 1) * Gap;
        if (availableWidth <= 0)
        {
            return;
        }

        var cellWidth = availableWidth / Columns;
        var radius = Math.Min(2.7, cellWidth / 3);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var now = DateTimeOffset.Now;
        var today = DateOnly.FromDateTime(now.DateTime);
        var referenceRow = ((int)_referenceDate.DayOfWeek + 6) % 7;
        var weekStart = _referenceDate.AddDays(-referenceRow);
        var isCurrentWeek = today >= weekStart && today < weekStart.AddDays(7);
        var currentRow = ((int)today.DayOfWeek + 6) % 7;
        var currentHour = now.Hour;

        var primary = Theme.Brush(Theme.TextPrimary);
        var secondary = Theme.Brush(Theme.TextSecondary);
        var normalTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                                          FontWeights.Normal, FontStretches.Normal);
        var boldTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                                        FontWeights.Bold, FontStretches.Normal);

        Rect CellRect(int row, int column) => new(
            LabelWidth + column * (cellWidth + Gap),
            row * (CellHeight + Gap),
            cellWidth,
            CellHeight);

        if (isCurrentWeek)
        {
            var bandColor = Color.FromArgb(0x12, Theme.TextPrimary.R, Theme.TextPrimary.G, Theme.TextPrimary.B);
            var hourBand = new Rect(
                LabelWidth + currentHour * (cellWidth + Gap) - Gap / 2,
                0,
                cellWidth + Gap,
                Rows * CellHeight + (Rows - 1) * Gap);
            context.DrawRoundedRectangle(Theme.Brush(bandColor), null, hourBand, 2.7, 2.7);
        }

        var peakBrush = Theme.Brush(Color.FromArgb(0xEB, WhaleTheme.BrandBlue.R, WhaleTheme.BrandBlue.G, WhaleTheme.BrandBlue.B));
        var offBrush = Theme.Brush(Color.FromArgb(0x2E, Theme.TextSecondary.R, Theme.TextSecondary.G, Theme.TextSecondary.B));
        var ringPen = new Pen(Theme.Brush(Color.FromArgb(0xD9, Theme.TextPrimary.R, Theme.TextPrimary.G, Theme.TextPrimary.B)), 1.7);

        var labels = PricingFormatter.WeekdaySymbolsMondayFirst();
        for (var row = 0; row < Rows; row++)
        {
            var isReference = row == referenceRow;
            var day = weekStart.AddDays(row);
            var label = new FormattedText(labels[row], Strings.Culture, FlowDirection.LeftToRight,
                                          isReference ? boldTypeface : normalTypeface, 10.7,
                                          isReference ? primary : secondary, dpi);
            context.DrawText(label, new Point(LabelWidth - 9 - label.Width, CellRect(row, 0).Y + CellHeight / 2 - label.Height / 2));

            for (var column = 0; column < Columns; column++)
            {
                var isPeak = PricingFormatter.IsPeakAtLocal(day, column, TimeZoneInfo.Local);
                var cell = CellRect(row, column);
                context.DrawRoundedRectangle(isPeak ? peakBrush : offBrush, null, cell, radius, radius);
                if (isCurrentWeek && row == currentRow && column == currentHour)
                {
                    context.DrawRoundedRectangle(null, ringPen, cell, radius, radius);
                }
            }
        }

        var tickY = Rows * (CellHeight + Gap) + 1;
        foreach (var hour in new[] { 0, 6, 12, 18 })
        {
            var tick = new FormattedText(hour.ToString(CultureInfo.InvariantCulture), Strings.Culture,
                                         FlowDirection.LeftToRight, normalTypeface, 10, secondary, dpi);
            context.DrawText(tick, new Point(
                LabelWidth + hour * (cellWidth + Gap) + cellWidth / 2 - tick.Width / 2,
                tickY));
        }

        var end = new FormattedText("24", Strings.Culture, FlowDirection.LeftToRight,
                                    normalTypeface, 10, secondary, dpi);
        context.DrawText(end, new Point(
            LabelWidth + Columns * (cellWidth + Gap) - Gap - end.Width,
            tickY));
    }
}
