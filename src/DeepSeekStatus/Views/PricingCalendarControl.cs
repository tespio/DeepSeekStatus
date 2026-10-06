using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DeepSeekStatus.Models;
using DeepSeekStatus.Support;

namespace DeepSeekStatus.Views;

public sealed class PricingCalendarControl : FrameworkElement
{
    private const double CellHeight = 26;
    private const double CellGap = 3;
    private const double HeaderHeight = 16;
    private const double LegendHeight = 18;
    private const double SectionGap = 7;

    private DateOnly _today = DateOnly.FromDateTime(DateTime.Now);
    private DateOnly _selected = DateOnly.FromDateTime(DateTime.Now);
    private DateOnly _month = new(DateTime.Now.Year, DateTime.Now.Month, 1);

    public PricingCalendarControl()
    {
        Cursor = Cursors.Arrow;
    }

    public event Action<DateOnly>? SelectedDateChanged;

    public DateOnly SelectedDate => _selected;

    public DateOnly DisplayedMonth => _month;

    public void SetState(DateOnly today, DateOnly selected)
    {
        _today = today;
        _selected = selected;
        if (_month.Year != selected.Year || _month.Month != selected.Month)
        {
            _month = new DateOnly(selected.Year, selected.Month, 1);
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    public void MoveMonth(int delta)
    {
        _month = _month.AddMonths(delta);
        _selected = _month;
        InvalidateMeasure();
        InvalidateVisual();
        SelectedDateChanged?.Invoke(_selected);
    }

    public void GoToday()
    {
        _today = DateOnly.FromDateTime(DateTime.Now);
        _selected = _today;
        _month = new DateOnly(_today.Year, _today.Month, 1);
        InvalidateMeasure();
        InvalidateVisual();
        SelectedDateChanged?.Invoke(_selected);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 280 : availableSize.Width;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        return new Size(width, ComputeHeight(width, dpi));
    }

    protected override void OnRender(DrawingContext context)
    {
        var width = ActualWidth;
        if (width <= 60)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var boldTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var primary = Theme.Brush(Theme.TextPrimary);
        var secondary = Theme.Brush(Theme.TextSecondary);

        var rows = RowsInMonth(_month);
        var columnWidth = (width - CellGap * 6) / 7;

        var symbols = PricingFormatter.ShortestWeekdaySymbolsMondayFirst();
        for (var column = 0; column < 7; column++)
        {
            var symbol = new FormattedText(symbols[column], Strings.Culture, FlowDirection.LeftToRight,
                                           typeface, 10.7, secondary, dpi);
            context.DrawText(symbol, new Point(
                column * (columnWidth + CellGap) + (columnWidth - symbol.Width) / 2, 0));
        }

        var gridTop = HeaderHeight + 5;
        var leading = ((int)_month.DayOfWeek + 6) % 7;
        var daysInMonth = DateTime.DaysInMonth(_month.Year, _month.Month);
        var selectedPen = new Pen(Theme.Brush(WhaleTheme.BrandBlue), 1.6);
        var todayPen = new Pen(Theme.Brush(Color.FromArgb(0x8C, Theme.TextPrimary.R, Theme.TextPrimary.G, Theme.TextPrimary.B)), 1);
        var offPeakText = Theme.Brush(WhaleTheme.OffPeakAccent);
        var offPeakFill = Theme.Brush(Color.FromArgb(0x26, WhaleTheme.OffPeakAccent.R, WhaleTheme.OffPeakAccent.G, WhaleTheme.OffPeakAccent.B));
        var neutralFill = Theme.Brush(Color.FromArgb(0x12, Theme.TextSecondary.R, Theme.TextSecondary.G, Theme.TextSecondary.B));

        for (var day = 1; day <= daysInMonth; day++)
        {
            var slot = leading + day - 1;
            var row = slot / 7;
            var column = slot % 7;
            var date = new DateOnly(_month.Year, _month.Month, day);
            var info = PricingDayInfo.For(date);
            var isSelected = date == _selected;
            var isToday = date == _today;
            var rect = new Rect(
                column * (columnWidth + CellGap),
                gridTop + row * (CellHeight + CellGap),
                columnWidth,
                CellHeight);

            context.DrawRoundedRectangle(info.IsAllDayOffPeak ? offPeakFill : neutralFill,
                                         null, rect, 6, 6);

            if (isSelected)
            {
                context.DrawRoundedRectangle(null, selectedPen, rect, 6, 6);
            }
            else if (isToday)
            {
                context.DrawRoundedRectangle(null, todayPen, rect, 6, 6);
            }

            var label = new FormattedText(day.ToString(), Strings.Culture, FlowDirection.LeftToRight,
                                          isSelected || isToday ? boldTypeface : typeface, 13.3,
                                          info.IsAllDayOffPeak ? offPeakText : primary, dpi);
            context.DrawText(label, new Point(
                rect.X + (rect.Width - label.Width) / 2,
                rect.Y + (rect.Height - label.Height) / 2));
        }

        var gridBottom = gridTop + rows * (CellHeight + CellGap) - CellGap;
        var legendTop = gridBottom + SectionGap + 4;
        DrawLegend(context, typeface, secondary, dpi, legendTop);

        var detailTop = legendTop + LegendHeight + SectionGap;
        var detailHeader = new FormattedText(PricingFormatter.Day(_selected), Strings.Culture,
                                             FlowDirection.LeftToRight, boldTypeface, 14, primary, dpi);
        context.DrawText(detailHeader, new Point(0, detailTop));

        var detail = new FormattedText(PricingDayInfo.For(_selected).LocalizedDetail(), Strings.Culture,
                                       FlowDirection.LeftToRight, typeface, 13.3, secondary, dpi)
        {
            MaxTextWidth = width,
            MaxLineCount = 2,
        };
        context.DrawText(detail, new Point(0, detailTop + 17));

        if (!ChineseHolidays.CoversYear(_month.Year))
        {
            var warningTop = detailTop + 17 + detail.Height + SectionGap;
            var iconTypeface = new Typeface(new FontFamily("Segoe MDL2 Assets"), FontStyles.Normal,
                                            FontWeights.Normal, FontStretches.Normal);
            var icon = new FormattedText("\uE7BA", Strings.Culture, FlowDirection.LeftToRight,
                                         iconTypeface, 12.7, Theme.Brush(WhaleTheme.PeakAccent), dpi);
            context.DrawText(icon, new Point(0, warningTop + 1));

            var warning = new FormattedText(Strings.Get("calendar.coverage.warning"),
                                            Strings.Culture, FlowDirection.LeftToRight, typeface, 13.3,
                                            Theme.Brush(WhaleTheme.PeakAccent), dpi)
            {
                MaxTextWidth = Math.Max(60, width - 20),
                MaxLineCount = 3,
            };
            context.DrawText(warning, new Point(18, warningTop));
        }
    }

    private void DrawLegend(DrawingContext context, Typeface typeface, Brush secondary, double dpi, double top)
    {
        var offPeakSwatch = Theme.Brush(Color.FromArgb(0x33, WhaleTheme.OffPeakAccent.R, WhaleTheme.OffPeakAccent.G, WhaleTheme.OffPeakAccent.B));
        var neutralSwatch = Theme.Brush(Color.FromArgb(0x1F, Theme.TextSecondary.R, Theme.TextSecondary.G, Theme.TextSecondary.B));
        context.DrawRoundedRectangle(offPeakSwatch, null, new Rect(0, top, 12, 12), 3, 3);
        var first = new FormattedText(Strings.Get("calendar.legend.allDayOffPeak"), Strings.Culture,
                                      FlowDirection.LeftToRight, typeface, 11.3, secondary, dpi);
        context.DrawText(first, new Point(17, top - 1));

        var secondX = 17 + first.Width + 18;
        context.DrawRoundedRectangle(neutralSwatch, null, new Rect(secondX, top, 12, 12), 3, 3);
        var second = new FormattedText(Strings.Get("calendar.legend.timed"), Strings.Culture,
                                       FlowDirection.LeftToRight, typeface, 11.3, secondary, dpi);
        context.DrawText(second, new Point(secondX + 17, top - 1));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var width = ActualWidth;
        if (width <= 60)
        {
            return;
        }

        var position = e.GetPosition(this);
        var rows = RowsInMonth(_month);
        var columnWidth = (width - CellGap * 6) / 7;
        var gridTop = HeaderHeight + 5;
        var gridBottom = gridTop + rows * (CellHeight + CellGap) - CellGap;
        if (position.Y < gridTop || position.Y > gridBottom)
        {
            return;
        }

        var column = (int)(position.X / (columnWidth + CellGap));
        var row = (int)((position.Y - gridTop) / (CellHeight + CellGap));
        if (column is < 0 or > 6 || row < 0 || row >= rows)
        {
            return;
        }

        var leading = ((int)_month.DayOfWeek + 6) % 7;
        var day = row * 7 + column - leading + 1;
        var daysInMonth = DateTime.DaysInMonth(_month.Year, _month.Month);
        if (day < 1 || day > daysInMonth)
        {
            return;
        }

        _selected = new DateOnly(_month.Year, _month.Month, day);
        InvalidateMeasure();
        InvalidateVisual();
        SelectedDateChanged?.Invoke(_selected);
    }

    private double ComputeHeight(double width, double dpi)
    {
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var rows = RowsInMonth(_month);
        var gridTop = HeaderHeight + 5;
        var gridHeight = rows * (CellHeight + CellGap) - CellGap;
        var legendTop = gridTop + gridHeight + SectionGap + 4;
        var detailTop = legendTop + LegendHeight + SectionGap;
        var detail = new FormattedText(PricingDayInfo.For(_selected).LocalizedDetail(), Strings.Culture,
                                       FlowDirection.LeftToRight, typeface, 13.3, Brushes.Gray, dpi)
        {
            MaxTextWidth = width,
            MaxLineCount = 2,
        };
        var height = detailTop + 17 + detail.Height;
        if (!ChineseHolidays.CoversYear(_month.Year))
        {
            height += SectionGap + 36;
        }

        return height + 2;
    }

    private static int RowsInMonth(DateOnly month)
    {
        var leading = ((int)month.DayOfWeek + 6) % 7;
        var days = DateTime.DaysInMonth(month.Year, month.Month);
        return (leading + days + 6) / 7;
    }
}
