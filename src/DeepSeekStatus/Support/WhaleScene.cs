using System.Windows;
using System.Windows.Media;
using DeepSeekStatus.Models;

namespace DeepSeekStatus.Support;

public static class WhaleScene
{
    public const double SwimCycle = 4.2;
    public const double BobPeriod = 4.05;
    public const double TiltDegrees = 8;
    public const double SleepTiltDegrees = -7;
    public const double SleepFloatPeriod = 7.4;
    public const double SleepBreathPeriod = 6.0;
    public const double MenuBarMargin = 2;
    public const double MenuBarFrameRate = 20;
    public const double SleepFrameRate = 12;

    public static double MenuBarWhaleWidth(double stageHeight) =>
        stageHeight * WhaleGeometry.AspectRatio * 0.75;

    public static void Draw(DrawingContext context,
                            Size size,
                            double time,
                            PricePeriod period,
                            double whaleWidth,
                            WhalePalette palette,
                            bool showsBubbles = true,
                            bool showsSleepMarks = true)
    {
        if (size.Width <= 1 || size.Height <= 1)
        {
            return;
        }

        if (period == PricePeriod.Peak)
        {
            DrawSwimming(context, size, time, whaleWidth, palette, showsBubbles);
        }
        else
        {
            DrawSleeping(context, size, time, whaleWidth, palette, showsSleepMarks);
        }
    }

    public static void DrawSwimming(DrawingContext context,
                                    Size size,
                                    double time,
                                    double whaleWidth,
                                    WhalePalette palette,
                                    bool showsBubbles)
    {
        const double margin = 2;
        var bodyWidth = Math.Min(whaleWidth, Math.Max(4, size.Width - margin * 2));
        var bodyHeight = WhaleGeometry.HeightForWidth(bodyWidth);

        var u = Mod(time / SwimCycle, 1);
        var triangle = u < 0.5 ? u * 2 : (1 - u) * 2;
        var eased = 0.5 - 0.5 * Math.Cos(triangle * Math.PI);
        var facingLeft = u >= 0.5;

        var travel = Math.Max(0, size.Width - bodyWidth - margin * 2);
        var x = margin + travel * eased;

        var verticalRoom = Math.Max(0, size.Height - bodyHeight);
        var bob = Math.Sin(time * 1.55) * verticalRoom * 0.26;
        var y = verticalRoom / 2 + bob;
        var tilt = Math.Sin(time * 1.55 + 0.75) * 8;

        var centerX = bodyWidth / 2;
        var centerY = bodyHeight / 2;
        var matrix = new Matrix();
        matrix.Translate(-centerX, -centerY);
        matrix.Rotate(tilt);
        matrix.Scale(facingLeft ? -1 : 1, 1);
        matrix.Translate(x + centerX, y + centerY);

        context.PushTransform(new MatrixTransform(matrix));
        FillWhale(context, palette, new Size(bodyWidth, bodyHeight));
        context.Pop();

        if (!showsBubbles)
        {
            return;
        }

        DrawSwimBubbles(context, time, new Point(x, y), new Size(bodyWidth, bodyHeight), facingLeft, palette);
    }

    private static void DrawSwimBubbles(DrawingContext context,
                                        double time,
                                        Point bodyOrigin,
                                        Size bodySize,
                                        bool facingLeft,
                                        WhalePalette palette)
    {
        var unit = bodySize.Width / 20;
        var tailX = facingLeft ? bodyOrigin.X + bodySize.Width * 0.92 : bodyOrigin.X + bodySize.Width * 0.08;
        var direction = facingLeft ? 1.0 : -1.0;
        var brush = new SolidColorBrush(palette.Bubble);
        brush.Freeze();

        for (var index = 0; index < 3; index++)
        {
            var offset = index * 0.34;
            var progress = Mod(time * 0.42 + offset, 1);
            var radius = (0.75 + index * 0.4) * unit;
            var point = new Point(
                tailX + direction * (2.5 + progress * 7) * unit,
                bodyOrigin.Y + bodySize.Height * 0.72 - progress * (bodySize.Height + 7 * unit));
            if (point.Y <= -4)
            {
                continue;
            }

            context.PushOpacity((1 - progress) * 0.55);
            context.DrawEllipse(brush, null, point, radius, radius);
            context.Pop();
        }
    }

    public static void DrawSleeping(DrawingContext context,
                                    Size size,
                                    double time,
                                    double whaleWidth,
                                    WhalePalette palette,
                                    bool showsSleepMarks)
    {
        const double margin = 2;
        var bodyWidth = Math.Min(whaleWidth, Math.Max(4, size.Width - margin * 2));
        var bodyHeight = WhaleGeometry.HeightForWidth(bodyWidth);

        var breath = 1 + Math.Sin(time * 1.05) * 0.022;
        var renderedWidth = bodyWidth * breath;
        var renderedHeight = bodyHeight * breath;
        var verticalRoom = Math.Max(0, size.Height - renderedHeight);

        var x = (size.Width - renderedWidth) / 2 - size.Width * 0.04;
        var y = verticalRoom / 2 + Math.Sin(time * 0.85) * verticalRoom * 0.14;
        var tilt = SleepTiltDegrees + Math.Sin(time * 0.6) * 1.6;

        var centerX = renderedWidth / 2;
        var centerY = renderedHeight / 2;
        var matrix = new Matrix();
        matrix.Translate(-centerX, -centerY);
        matrix.Rotate(tilt);
        matrix.Translate(x + centerX, y + centerY);

        context.PushTransform(new MatrixTransform(matrix));
        FillWhale(context, palette, new Size(renderedWidth, renderedHeight));
        context.Pop();

        if (!showsSleepMarks)
        {
            return;
        }

        DrawSleepMarks(context, time, new Point(x, y), new Size(renderedWidth, renderedHeight), palette);
    }

    private static void DrawSleepMarks(DrawingContext context,
                                       double time,
                                       Point origin,
                                       Size bodySize,
                                       WhalePalette palette)
    {
        var markSize = Math.Max(3.5, bodySize.Width * 0.15);
        var start = new Point(origin.X + bodySize.Width * 1.06, origin.Y + bodySize.Height * 0.30);
        var brush = new SolidColorBrush(palette.SleepMark);
        brush.Freeze();

        for (var index = 0; index < 3; index++)
        {
            var progress = Mod(time * 0.30 + index / 3.0, 1);
            var point = new Point(
                start.X + progress * bodySize.Width * 0.24,
                start.Y - progress * bodySize.Height * 0.60);
            var size = markSize * (1 + progress * 0.30);
            var pen = new Pen(brush, size * 0.20)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            pen.Freeze();

            context.PushOpacity(Math.Sin(progress * Math.PI) * 0.9);
            context.DrawGeometry(null, pen, ZMarkPath(point, size));
            context.Pop();
        }
    }

    public static Geometry ZMarkPath(Point center, double size)
    {
        var halfWidth = size * 0.5;
        var halfHeight = size * 0.5;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(center.X - halfWidth, center.Y - halfHeight), false, false);
            context.LineTo(new Point(center.X + halfWidth, center.Y - halfHeight), true, false);
            context.LineTo(new Point(center.X - halfWidth, center.Y + halfHeight), true, false);
            context.LineTo(new Point(center.X + halfWidth, center.Y + halfHeight), true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static void FillWhale(DrawingContext context, WhalePalette palette, Size size)
    {
        var geometry = WhaleGeometry.FitCached(new Rect(0, 0, size.Width, size.Height));
        Brush brush;
        if (palette.IsFlat || palette.Body.Length == 0)
        {
            brush = new SolidColorBrush(palette.FillColor);
        }
        else
        {
            var gradient = new LinearGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                StartPoint = new Point(0, 0),
                EndPoint = new Point(size.Width * 0.35, size.Height),
            };
            var count = palette.Body.Length;
            for (var index = 0; index < count; index++)
            {
                gradient.GradientStops.Add(new GradientStop(palette.Body[index],
                                                            count == 1 ? 0 : (double)index / (count - 1)));
            }

            brush = gradient;
        }

        brush.Freeze();
        context.DrawGeometry(brush, null, geometry);
    }

    private static double Mod(double value, double period)
    {
        var result = value % period;
        return result < 0 ? result + period : result;
    }
}
