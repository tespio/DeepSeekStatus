using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Media;

namespace DeepSeekStatus.Support;

public static class WhaleGeometry
{
    public const string PathData = "M23.748 4.651c-.254-.124-.364.113-.512.233-.051.04-.094.09-.137.137-.372.397-.806.657-1.373.626-.829-.046-1.537.214-2.163.848-.133-.782-.575-1.248-1.247-1.548-.352-.155-.708-.311-.955-.65-.172-.24-.219-.509-.305-.774-.055-.16-.11-.323-.293-.35-.2-.031-.278.136-.356.276-.313.572-.434 1.202-.422 1.84.027 1.436.633 2.58 1.838 3.393.137.094.172.187.129.323-.082.28-.18.553-.266.833-.055.179-.137.218-.328.14a5.5 5.5 0 0 1-1.737-1.179c-.857-.828-1.631-1.743-2.597-2.46a12 12 0 0 0-.689-.47c-.985-.957.13-1.743.387-1.836.27-.098.094-.433-.778-.428-.872.003-1.67.295-2.687.685a3 3 0 0 1-.465.136 9.6 9.6 0 0 0-2.883-.101c-1.885.21-3.39 1.1-4.497 2.622C.082 8.776-.231 10.854.152 13.02c.403 2.284 1.568 4.175 3.36 5.653 1.857 1.533 3.997 2.284 6.438 2.14 1.482-.085 3.132-.284 4.994-1.86.47.234.962.328 1.78.398.629.058 1.235-.031 1.705-.129.735-.155.684-.836.418-.961-2.155-1.004-1.682-.595-2.112-.926 1.095-1.295 2.768-3.598 3.284-6.733.05-.346.115-.834.108-1.114-.004-.171.035-.238.23-.257a4.2 4.2 0 0 0 1.545-.475c1.397-.763 1.96-2.016 2.093-3.517.02-.23-.004-.467-.247-.588M11.58 18.168c-2.088-1.642-3.101-2.183-3.52-2.16-.39.024-.32.472-.234.763.09.288.207.487.371.74.114.167.192.416-.113.603-.673.416-1.842-.14-1.897-.168-1.361-.801-2.5-1.86-3.301-3.306-.775-1.393-1.225-2.888-1.299-4.482-.02-.385.094-.522.477-.592a4.7 4.7 0 0 1 1.53-.038c2.131.311 3.946 1.264 5.467 2.774.868.86 1.525 1.887 2.202 2.89.72 1.066 1.494 2.082 2.48 2.915.348.291.626.513.892.677-.802.09-2.14.109-3.055-.615zm1.001-6.44a.306.306 0 0 1 .415-.287.3.3 0 0 1 .113.074.3.3 0 0 1 .086.214c0 .17-.136.307-.308.307a.303.303 0 0 1-.306-.307m3.11 1.596c-.2.081-.4.151-.591.16a1.25 1.25 0 0 1-.798-.254c-.274-.23-.47-.358-.551-.758a1.7 1.7 0 0 1 .015-.588c.07-.327-.007-.537-.238-.727-.188-.156-.426-.199-.689-.199a.6.6 0 0 1-.254-.078.253.253 0 0 1-.114-.358 1 1 0 0 1 .192-.21c.356-.202.767-.136 1.146.016.352.144.618.408 1.001.782.392.451.462.576.685.915.176.264.336.536.446.848.066.194-.02.353-.25.45";

    private static readonly SvgPathData Parsed = SvgPathParser.Parse(PathData);

    private static readonly Geometry UnitGeometry = BuildGeometry(Parsed);

    private static readonly Dictionary<FitKey, Geometry> Cache = new();

    public static int FigureCount => Parsed.Figures.Count;

    public static int SegmentCount => Parsed.Figures.Sum(figure => figure.Segments.Count);

    public static Rect Bounds => Parsed.Bounds;

    public static double AspectRatio => Bounds.Height > 0 ? Bounds.Width / Bounds.Height : 1;

    public static double HeightForWidth(double width) =>
        Bounds.Width > 0 ? width * Bounds.Height / Bounds.Width : width;

    public static Geometry Unit => UnitGeometry;

    public static Geometry Fit(Rect rect)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return UnitGeometry;
        }

        var scale = Math.Min(rect.Width / Bounds.Width, rect.Height / Bounds.Height);
        if (!double.IsFinite(scale) || scale <= 0)
        {
            return UnitGeometry;
        }

        var dx = rect.X + rect.Width / 2 - (Bounds.X + Bounds.Width / 2) * scale;
        var dy = rect.Y + rect.Height / 2 - (Bounds.Y + Bounds.Height / 2) * scale;
        var geometry = UnitGeometry.Clone();
        geometry.Transform = new MatrixTransform(new System.Windows.Media.Matrix(scale, 0, 0, scale, dx, dy));
        geometry.Freeze();
        return geometry;
    }

    public static Geometry FitCached(Rect rect)
    {
        var key = new FitKey(
            Math.Round(rect.X, 2),
            Math.Round(rect.Y, 2),
            Math.Round(rect.Width, 2),
            Math.Round(rect.Height, 2));
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var geometry = Fit(rect);
        if (Cache.Count >= 64)
        {
            Cache.Clear();
        }

        Cache[key] = geometry;
        return geometry;
    }

    public static GraphicsPath CreateGdiPath()
    {
        var path = new GraphicsPath(FillMode.Winding);
        foreach (var figure in Parsed.Figures)
        {
            var current = ToPointF(figure.Start);
            path.StartFigure();
            foreach (var segment in figure.Segments)
            {
                switch (segment)
                {
                    case SvgLineSegment line:
                    {
                        var point = ToPointF(line.Point);
                        path.AddLine(current, point);
                        current = point;
                        break;
                    }

                    case SvgCubicSegment cubic:
                    {
                        var point = ToPointF(cubic.Point);
                        path.AddBezier(current, ToPointF(cubic.Control1), ToPointF(cubic.Control2), point);
                        current = point;
                        break;
                    }

                    case SvgQuadSegment quad:
                    {
                        var point = ToPointF(quad.Point);
                        var control = ToPointF(quad.Control);
                        var control1 = new System.Drawing.PointF(
                            current.X + 2f / 3f * (control.X - current.X),
                            current.Y + 2f / 3f * (control.Y - current.Y));
                        var control2 = new System.Drawing.PointF(
                            point.X + 2f / 3f * (control.X - point.X),
                            point.Y + 2f / 3f * (control.Y - point.Y));
                        path.AddBezier(current, control1, control2, point);
                        current = point;
                        break;
                    }
                }
            }

            if (figure.IsClosed)
            {
                path.CloseFigure();
            }
        }

        return path;
    }

    private static Geometry BuildGeometry(SvgPathData data)
    {
        var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var context = geometry.Open())
        {
            foreach (var figure in data.Figures)
            {
                context.BeginFigure(figure.Start, isFilled: true, isClosed: figure.IsClosed);
                foreach (var segment in figure.Segments)
                {
                    switch (segment)
                    {
                        case SvgLineSegment line:
                            context.LineTo(line.Point, isStroked: true, isSmoothJoin: false);
                            break;
                        case SvgCubicSegment cubic:
                            context.BezierTo(cubic.Control1, cubic.Control2, cubic.Point,
                                             isStroked: true, isSmoothJoin: false);
                            break;
                        case SvgQuadSegment quad:
                            context.QuadraticBezierTo(quad.Control, quad.Point,
                                                      isStroked: true, isSmoothJoin: false);
                            break;
                    }
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static System.Drawing.PointF ToPointF(Point point) =>
        new((float)point.X, (float)point.Y);

    private readonly record struct FitKey(double X, double Y, double Width, double Height);
}
