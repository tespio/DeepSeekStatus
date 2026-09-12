using System.Globalization;
using System.Windows;

namespace DeepSeekStatus.Support;

public abstract class SvgSegment;

public sealed class SvgLineSegment(Point point) : SvgSegment
{
    public Point Point { get; } = point;
}

public sealed class SvgCubicSegment(Point control1, Point control2, Point point) : SvgSegment
{
    public Point Control1 { get; } = control1;
    public Point Control2 { get; } = control2;
    public Point Point { get; } = point;
}

public sealed class SvgQuadSegment(Point control, Point point) : SvgSegment
{
    public Point Control { get; } = control;
    public Point Point { get; } = point;
}

public sealed class SvgFigure
{
    public Point Start { get; set; }
    public List<SvgSegment> Segments { get; } = new();
    public bool IsClosed { get; set; }
}

public sealed class SvgPathData
{
    public List<SvgFigure> Figures { get; } = new();
    public Rect Bounds { get; internal set; } = Rect.Empty;
}

public static class SvgPathParser
{
    private const string CommandLetters = "MmLlHhVvCcSsQqTtAaZz";

    public static SvgPathData Parse(string data)
    {
        var scanner = new Scanner(data);
        var result = new SvgPathData();

        var current = new Point(0, 0);
        var subpathStart = new Point(0, 0);
        Point? lastControl = null;
        var segmentCommand = ' ';
        var command = ' ';
        SvgFigure? figure = null;

        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;

        void Track(Point point)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        void EnsureFigure()
        {
            if (figure is not null)
            {
                return;
            }

            figure = new SvgFigure { Start = current };
            result.Figures.Add(figure);
            Track(current);
        }

        while (true)
        {
            scanner.SkipSeparators();
            if (scanner.IsAtEnd)
            {
                break;
            }

            var peeked = scanner.Peek();
            if (peeked is char letter && CommandLetters.Contains(letter))
            {
                command = letter;
                scanner.Advance();
            }
            else if (segmentCommand is ' ' or 'Z' or 'z')
            {
                break;
            }
            else
            {
                command = segmentCommand switch
                {
                    'M' => 'L',
                    'm' => 'l',
                    _ => segmentCommand,
                };
            }

            var relative = char.IsLower(command);
            var absolute = char.ToUpperInvariant(command);

            Point Resolve(double x, double y) => relative
                ? new Point(current.X + x, current.Y + y)
                : new Point(x, y);

            switch (absolute)
            {
                case 'M':
                {
                    if (scanner.ScanNumber() is not double x || scanner.ScanNumber() is not double y)
                    {
                        goto End;
                    }

                    var point = Resolve(x, y);
                    figure = new SvgFigure { Start = point };
                    result.Figures.Add(figure);
                    Track(point);
                    current = point;
                    subpathStart = point;
                    lastControl = null;
                    break;
                }

                case 'L':
                {
                    if (scanner.ScanNumber() is not double x || scanner.ScanNumber() is not double y)
                    {
                        goto End;
                    }

                    EnsureFigure();
                    var point = Resolve(x, y);
                    figure!.Segments.Add(new SvgLineSegment(point));
                    Track(point);
                    current = point;
                    lastControl = null;
                    break;
                }

                case 'H':
                {
                    if (scanner.ScanNumber() is not double x)
                    {
                        goto End;
                    }

                    EnsureFigure();
                    var point = new Point(relative ? current.X + x : x, current.Y);
                    figure!.Segments.Add(new SvgLineSegment(point));
                    Track(point);
                    current = point;
                    lastControl = null;
                    break;
                }

                case 'V':
                {
                    if (scanner.ScanNumber() is not double y)
                    {
                        goto End;
                    }

                    EnsureFigure();
                    var point = new Point(current.X, relative ? current.Y + y : y);
                    figure!.Segments.Add(new SvgLineSegment(point));
                    Track(point);
                    current = point;
                    lastControl = null;
                    break;
                }

                case 'C':
                {
                    if (scanner.ScanNumber() is not double x1 || scanner.ScanNumber() is not double y1
                        || scanner.ScanNumber() is not double x2 || scanner.ScanNumber() is not double y2
                        || scanner.ScanNumber() is not double x || scanner.ScanNumber() is not double y)
                    {
                        goto End;
                    }

                    EnsureFigure();
                    var control1 = Resolve(x1, y1);
                    var control2 = Resolve(x2, y2);
                    var point = Resolve(x, y);
                    figure!.Segments.Add(new SvgCubicSegment(control1, control2, point));
                    Track(control1);
                    Track(control2);
                    Track(point);
                    current = point;
                    lastControl = control2;
                    break;
                }

                case 'S':
                {
                    if (scanner.ScanNumber() is not double x2 || scanner.ScanNumber() is not double y2
                        || scanner.ScanNumber() is not double x || scanner.ScanNumber() is not double y)
                    {
                        goto End;
                    }

                    EnsureFigure();
                    var reflected = lastControl is { } control
                                   && segmentCommand is 'C' or 'c' or 'S' or 's'
                        ? new Point(current.X * 2 - control.X, current.Y * 2 - control.Y)
                        : current;
                    var control2 = Resolve(x2, y2);
                    var point = Resolve(x, y);
                    figure!.Segments.Add(new SvgCubicSegment(reflected, control2, point));
                    Track(reflected);
                    Track(control2);
                    Track(point);
                    current = point;
                    lastControl = control2;
                    break;
                }

                case 'Q':
                {
                    if (scanner.ScanNumber() is not double x1 || scanner.ScanNumber() is not double y1
                        || scanner.ScanNumber() is not double x || scanner.ScanNumber() is not double y)
                    {
                        goto End;
                    }

                    EnsureFigure();
                    var control = Resolve(x1, y1);
                    var point = Resolve(x, y);
                    figure!.Segments.Add(new SvgQuadSegment(control, point));
                    Track(control);
                    Track(point);
                    current = point;
                    lastControl = control;
                    break;
                }

                case 'T':
                {
                    if (scanner.ScanNumber() is not double x || scanner.ScanNumber() is not double y)
                    {
                        goto End;
                    }

                    EnsureFigure();
                    var reflected = lastControl is { } control
                                   && segmentCommand is 'Q' or 'q' or 'T' or 't'
                        ? new Point(current.X * 2 - control.X, current.Y * 2 - control.Y)
                        : current;
                    var point = Resolve(x, y);
                    figure!.Segments.Add(new SvgQuadSegment(reflected, point));
                    Track(reflected);
                    Track(point);
                    current = point;
                    lastControl = reflected;
                    break;
                }

                case 'A':
                {
                    if (scanner.ScanNumber() is not double rx || scanner.ScanNumber() is not double ry
                        || scanner.ScanNumber() is not double rotation
                        || scanner.ScanFlag() is not bool largeArc || scanner.ScanFlag() is not bool sweep
                        || scanner.ScanNumber() is not double x || scanner.ScanNumber() is not double y)
                    {
                        goto End;
                    }

                    EnsureFigure();
                    var point = Resolve(x, y);
                    AppendArc(figure!, current, point, rx, ry, rotation, largeArc, sweep, Track);
                    current = point;
                    lastControl = null;
                    break;
                }

                case 'Z':
                {
                    if (figure is not null)
                    {
                        figure.IsClosed = true;
                    }

                    current = subpathStart;
                    lastControl = null;
                    figure = null;
                    break;
                }

                default:
                    goto End;
            }

            segmentCommand = command;
        }

    End:
        if (!double.IsPositiveInfinity(minX))
        {
            result.Bounds = new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        return result;
    }

    private static void AppendArc(SvgFigure figure,
                                  Point start,
                                  Point end,
                                  double inputRx,
                                  double inputRy,
                                  double xAxisRotation,
                                  bool largeArc,
                                  bool sweep,
                                  Action<Point> track)
    {
        var rx = Math.Abs(inputRx);
        var ry = Math.Abs(inputRy);

        if (rx == 0 || ry == 0)
        {
            figure.Segments.Add(new SvgLineSegment(end));
            track(end);
            return;
        }

        if (Math.Abs(start.X - end.X) < 1e-12 && Math.Abs(start.Y - end.Y) < 1e-12)
        {
            return;
        }

        var phi = xAxisRotation % 360 * Math.PI / 180;
        var cosPhi = Math.Cos(phi);
        var sinPhi = Math.Sin(phi);

        var dx2 = (start.X - end.X) / 2;
        var dy2 = (start.Y - end.Y) / 2;
        var x1p = cosPhi * dx2 + sinPhi * dy2;
        var y1p = -sinPhi * dx2 + cosPhi * dy2;

        var lambda = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
        if (lambda > 1)
        {
            var scale = Math.Sqrt(lambda);
            rx *= scale;
            ry *= scale;
        }

        var sign = largeArc != sweep ? 1.0 : -1.0;
        var numerator = Math.Max(0, rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p);
        var denominator = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
        var coefficient = denominator == 0 ? 0 : sign * Math.Sqrt(numerator / denominator);
        var cxp = coefficient * (rx * y1p / ry);
        var cyp = coefficient * (-ry * x1p / rx);

        var cx = cosPhi * cxp - sinPhi * cyp + (start.X + end.X) / 2;
        var cy = sinPhi * cxp + cosPhi * cyp + (start.Y + end.Y) / 2;

        static double Angle(double ux, double uy, double vx, double vy)
        {
            var dot = ux * vx + uy * vy;
            var length = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
            if (length <= 0)
            {
                return 0;
            }

            var value = Math.Acos(Math.Clamp(dot / length, -1, 1));
            if (ux * vy - uy * vx < 0)
            {
                value = -value;
            }

            return value;
        }

        Point Map(double x, double y) => new(
            cosPhi * x - sinPhi * y + cx,
            sinPhi * x + cosPhi * y + cy);

        var theta1 = Angle(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
        var deltaTheta = Angle((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
        if (!sweep && deltaTheta > 0)
        {
            deltaTheta -= 2 * Math.PI;
        }

        if (sweep && deltaTheta < 0)
        {
            deltaTheta += 2 * Math.PI;
        }

        var segmentCount = Math.Max(1, (int)Math.Ceiling(Math.Abs(deltaTheta) / (Math.PI / 2)));
        var delta = deltaTheta / segmentCount;

        var theta = theta1;
        for (var i = 0; i < segmentCount; i++)
        {
            var theta2 = theta + delta;
            var alpha = 4.0 / 3.0 * Math.Tan(delta / 4);
            var endPoint = Map(rx * Math.Cos(theta2), ry * Math.Sin(theta2));
            var control1 = Map(rx * Math.Cos(theta) - alpha * rx * Math.Sin(theta),
                               ry * Math.Sin(theta) + alpha * ry * Math.Cos(theta));
            var control2 = Map(rx * Math.Cos(theta2) + alpha * rx * Math.Sin(theta2),
                               ry * Math.Sin(theta2) - alpha * ry * Math.Cos(theta2));
            figure.Segments.Add(new SvgCubicSegment(control1, control2, endPoint));
            track(control1);
            track(control2);
            track(endPoint);
            theta = theta2;
        }
    }

    private sealed class Scanner(string text)
    {
        private readonly string _text = text;
        private int _index;

        public bool IsAtEnd => _index >= _text.Length;

        public void SkipSeparators()
        {
            while (_index < _text.Length)
            {
                var character = _text[_index];
                if (character is ' ' or ',' or '\n' or '\r' or '\t')
                {
                    _index++;
                }
                else
                {
                    break;
                }
            }
        }

        public char? Peek()
        {
            SkipSeparators();
            return _index < _text.Length ? _text[_index] : null;
        }

        public void Advance() => _index++;

        public double? ScanNumber()
        {
            SkipSeparators();
            var start = _index;
            var sawDigit = false;
            var sawDot = false;
            var sawExponent = false;

            while (_index < _text.Length)
            {
                var character = _text[_index];
                if (character is >= '0' and <= '9')
                {
                    sawDigit = true;
                    _index++;
                }
                else if (character is '+' or '-')
                {
                    var isLeadingSign = _index == start;
                    var isExponentSign = sawExponent && _index > start
                        && _text[_index - 1] is 'e' or 'E';
                    if (isLeadingSign || isExponentSign)
                    {
                        _index++;
                    }
                    else
                    {
                        break;
                    }
                }
                else if (character == '.')
                {
                    if (sawDot || sawExponent)
                    {
                        break;
                    }

                    sawDot = true;
                    _index++;
                }
                else if (character is 'e' or 'E')
                {
                    if (sawExponent || !sawDigit)
                    {
                        break;
                    }

                    sawExponent = true;
                    _index++;
                }
                else
                {
                    break;
                }
            }

            if (!sawDigit)
            {
                _index = start;
                return null;
            }

            return double.TryParse(_text.AsSpan(start, _index - start), NumberStyles.Float,
                                   CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        public bool? ScanFlag()
        {
            SkipSeparators();
            if (_index >= _text.Length)
            {
                return null;
            }

            return _text[_index] switch
            {
                '0' => AdvanceFalse(),
                '1' => AdvanceTrue(),
                _ => null,
            };

            bool? AdvanceFalse()
            {
                _index++;
                return false;
            }

            bool? AdvanceTrue()
            {
                _index++;
                return true;
            }
        }
    }
}
