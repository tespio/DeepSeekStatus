using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DeepSeekStatus.Models;
using DeepSeekStatus.Support;

namespace DeepSeekStatus.Views;

public sealed class AquariumControl : FrameworkElement
{
    private readonly DispatcherTimer _timer;

    public AquariumControl()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 30),
        };
        _timer.Tick += (_, _) =>
        {
            Time = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            InvalidateVisual();
        };
        IsVisibleChanged += (_, _) => UpdateTimer();
        Unloaded += (_, _) => _timer.Stop();
    }

    private PricePeriod _period = PricePeriod.Peak;

    public PricePeriod Period
    {
        get => _period;
        set
        {
            if (_period == value)
            {
                return;
            }

            _period = value;
            InvalidateVisual();
        }
    }

    public double WhaleWidth { get; set; } = 133.3;

    public double Time { get; private set; }

    public void Restart()
    {
        Time = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        InvalidateVisual();
        UpdateTimer();
    }

    protected override void OnRender(DrawingContext context)
    {
        var size = RenderSize;
        if (size.Width <= 1 || size.Height <= 1)
        {
            return;
        }

        context.PushClip(new RectangleGeometry(new Rect(size)));
        DrawBackground(context, size, Period);
        DrawDecorations(context, size, Time, Period);
        WhaleScene.Draw(context, size, Time, Period, WhaleWidth, WhalePalette.Aquarium(Period));
        context.Pop();
    }

    private void UpdateTimer()
    {
        if (IsVisible)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private static void DrawBackground(DrawingContext context, Size size, PricePeriod period)
    {
        var top = period == PricePeriod.Peak ? WhaleTheme.TankTop : WhaleTheme.TankTopSleep;
        var bottom = period == PricePeriod.Peak ? WhaleTheme.TankBottom : WhaleTheme.TankBottomSleep;
        var gradient = new LinearGradientBrush(top, bottom, new Point(0.5, 0), new Point(0.5, 1));
        context.DrawRectangle(gradient, null, new Rect(size));

        var light = new RadialGradientBrush
        {
            Center = new Point(0.5, -0.1),
            GradientOrigin = new Point(0.5, -0.1),
            RadiusX = 0.9,
            RadiusY = 1.2,
        };
        light.GradientStops.Add(new GradientStop(
            Color.FromArgb((byte)(period == PricePeriod.Peak ? 41 : 18), 255, 255, 255), 0));
        light.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        context.DrawRectangle(light, null, new Rect(size));
    }

    private static void DrawDecorations(DrawingContext context, Size size, double time, PricePeriod period)
    {
        if (period == PricePeriod.Peak)
        {
            DrawBubbles(context, size, time);
        }
        else
        {
            DrawNight(context, size, time);
        }
    }

    private static void DrawBubbles(DrawingContext context, Size size, double time)
    {
        if (size.Width <= 10 || size.Height <= 10)
        {
            return;
        }

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(0xB3, 255, 255, 255)), 1.07);
        pen.Freeze();
        for (var index = 0; index < 9; index++)
        {
            var seedX = PseudoRandom(index, 1);
            var seedSpeed = 0.045 + PseudoRandom(index, 2) * 0.05;
            var radius = (1.2 + PseudoRandom(index, 3) * 2.4) * 4.0 / 3.0;
            var progress = Mod(time * seedSpeed + PseudoRandom(index, 4), 1);
            var x = size.Width * (0.05 + seedX * 0.9) + Math.Sin(time * 0.8 + index) * 4;
            var y = size.Height + 8 - progress * (size.Height + 21);
            context.PushOpacity(Math.Sin(progress * Math.PI) * 0.4);
            context.DrawEllipse(null, pen, new Point(x, y), radius, radius);
            context.Pop();
        }
    }

    private static void DrawNight(DrawingContext context, Size size, double time)
    {
        if (size.Width <= 10 || size.Height <= 10)
        {
            return;
        }

        var starBrush = new SolidColorBrush(Colors.White);
        starBrush.Freeze();
        for (var index = 0; index < 26; index++)
        {
            var x = size.Width * PseudoRandom(index, 11);
            var y = size.Height * 0.78 * PseudoRandom(index, 12);
            var radius = 0.5 + PseudoRandom(index, 13) * 0.9;
            var twinkle = 0.35 + 0.45 * (0.5 + 0.5 * Math.Sin(time * (0.7 + PseudoRandom(index, 14)) + index));
            context.PushOpacity(twinkle);
            context.DrawEllipse(starBrush, null, new Point(x, y), radius, radius);
            context.Pop();
        }

        const double moonRadius = 17.3;
        var moonCenter = new Point(size.Width - 69.3, 69.3);
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new EllipseGeometry(moonCenter, moonRadius, moonRadius));
        group.Children.Add(new EllipseGeometry(
            new Point(moonCenter.X + 8.7, moonCenter.Y - 4.7), moonRadius, moonRadius));
        var moonBrush = new SolidColorBrush(Color.FromRgb(230, 237, 255));
        context.PushOpacity(0.85);
        context.DrawGeometry(moonBrush, null, group);
        context.Pop();
    }

    private static double PseudoRandom(int index, double salt)
    {
        var value = Math.Sin(index * 12.9898 + salt * 78.233) * 43758.5453;
        return value - Math.Floor(value);
    }

    private static double Mod(double value, double period)
    {
        var result = value % period;
        return result < 0 ? result + period : result;
    }
}
