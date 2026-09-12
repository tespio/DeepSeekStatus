using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using DeepSeekStatus.Models;

namespace DeepSeekStatus.Support;

public static class TrayIconRenderer
{
    public static Icon RenderTray(PricePeriod period, bool darkTaskbar, int size)
    {
        using var bitmap = RenderTrayBitmap(period, darkTaskbar, size);
        return IcoBuilder.Build(bitmap, size);
    }

    public static Bitmap RenderTrayBitmap(PricePeriod period, bool darkTaskbar, int size)
    {
        var palette = WhalePalette.MenuBar(period, darkTaskbar);
        return RenderBitmap(size, (graphics, canvas) => DrawWhaleIcon(graphics, canvas, period, palette), 3);
    }

    public static Icon RenderAppIcon(int size)
    {
        using var bitmap = RenderAppIconBitmap(size);
        return IcoBuilder.Build(bitmap, size);
    }

    public static Bitmap RenderAppIconBitmap(int size)
    {
        var palette = new WhalePalette
        {
            Body = new[] { WhaleTheme.BrandBlueBright, WhaleTheme.BrandBlue, WhaleTheme.BrandBlueDeep },
            Bubble = WhaleTheme.BrandBlue,
            SleepMark = WhaleTheme.BrandBlue,
            IsFlat = true,
        };
        return RenderBitmap(size, (graphics, canvas) => DrawWhaleIcon(graphics, canvas, PricePeriod.Peak, palette), 1);
    }

    private static Bitmap RenderBitmap(int size, Action<Graphics, int> draw, int supersample)
    {
        var factor = size <= 64 ? Math.Max(1, supersample) : 1;
        var canvas = size * factor;
        var big = new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(big))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            draw(graphics, canvas);
        }

        if (factor == 1)
        {
            return big;
        }

        var final = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(final))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            graphics.DrawImage(big, new Rectangle(0, 0, size, size));
        }

        big.Dispose();
        return final;
    }

    private static void DrawWhaleIcon(Graphics graphics, double size, PricePeriod period, WhalePalette palette)
    {
        var whaleWidth = Math.Min(size * 0.86, WhaleScene.MenuBarWhaleWidth(size));
        var bodyHeight = WhaleGeometry.HeightForWidth(whaleWidth);
        var left = (size - whaleWidth) / 2 - size * 0.035;
        var top = (size - bodyHeight) / 2;
        var color = ToDrawing(palette.FillColor);
        var path = WhaleGeometry.CreateGdiPath();

        if (period == PricePeriod.Peak)
        {
            DrawWhale(graphics, path, left, top, whaleWidth, bodyHeight, -WhaleScene.TiltDegrees, color);

            for (var index = 0; index < 3; index++)
            {
                var radius = size * 0.028 * (1 + 0.5 * index);
                var x = left + whaleWidth * 0.96 + index * size * 0.05;
                var y = top + bodyHeight * 0.22 - index * size * 0.135;
                x = Math.Clamp(x, radius + size * 0.02, size - radius - size * 0.02);
                y = Math.Max(y, radius + size * 0.02);
                using var brush = new SolidBrush(WithAlpha(ToDrawing(palette.Bubble), 0.5 - index * 0.1));
                graphics.FillEllipse(brush,
                                     (float)(x - radius), (float)(y - radius),
                                     (float)(radius * 2), (float)(radius * 2));
            }
        }
        else
        {
            var shift = size * 0.05;
            DrawWhale(graphics, path, left - shift, top, whaleWidth, bodyHeight, WhaleScene.SleepTiltDegrees, color);

            var markSize = size * 0.15;
            var startX = left - shift + whaleWidth * 1.04;
            var startY = top + bodyHeight * 0.32;
            for (var index = 0; index < 3; index++)
            {
                var sizeMark = markSize * (1 + 0.15 * index);
                var x = startX + index * size * 0.072;
                var y = startY - index * size * 0.145;
                x = Math.Min(x, size - sizeMark * 0.6);
                y = Math.Max(y, sizeMark * 0.6);
                using var pen = new Pen(WithAlpha(ToDrawing(palette.SleepMark), 0.55 + index * 0.18), (float)(sizeMark * 0.20))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round,
                };
                using var mark = ZMarkPath(x, y, sizeMark);
                graphics.DrawPath(pen, mark);
            }
        }
    }

    private static void DrawWhale(Graphics graphics,
                                  GraphicsPath path,
                                  double left,
                                  double top,
                                  double width,
                                  double height,
                                  double tiltDegrees,
                                  Color color)
    {
        var bounds = WhaleGeometry.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var scale = Math.Min(width / bounds.Width, height / bounds.Height);
        var state = graphics.Save();
        var matrix = new Matrix();
        matrix.Translate((float)(-bounds.X), (float)(-bounds.Y), MatrixOrder.Append);
        matrix.Scale((float)scale, (float)scale, MatrixOrder.Append);
        matrix.Translate((float)(-width / 2), (float)(-height / 2), MatrixOrder.Append);
        matrix.Rotate((float)tiltDegrees, MatrixOrder.Append);
        matrix.Translate((float)(left + width / 2), (float)(top + height / 2), MatrixOrder.Append);
        graphics.MultiplyTransform(matrix);
        using var brush = new SolidBrush(color);
        graphics.FillPath(brush, path);
        graphics.Restore(state);
    }

    private static GraphicsPath ZMarkPath(double centerX, double centerY, double size)
    {
        var halfWidth = (float)(size / 2);
        var halfHeight = (float)(size / 2);
        var x = (float)centerX;
        var y = (float)centerY;
        var path = new GraphicsPath();
        path.AddLine(x - halfWidth, y - halfHeight, x + halfWidth, y - halfHeight);
        path.AddLine(x + halfWidth, y - halfHeight, x - halfWidth, y + halfHeight);
        path.AddLine(x - halfWidth, y + halfHeight, x + halfWidth, y + halfHeight);
        return path;
    }

    private static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((int)Math.Clamp(alpha * 255, 0, 255), color.R, color.G, color.B);

    private static Color ToDrawing(System.Windows.Media.Color color) =>
        Color.FromArgb(color.A, color.R, color.G, color.B);
}

public static class IcoBuilder
{
    public static Icon Build(Bitmap bitmap, int size)
    {
        using var stream = new MemoryStream(BuildBytes(bitmap));
        return new Icon(stream, new Size(size, size));
    }

    public static byte[] BuildBytes(Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var pixels = new byte[width * height * 4];
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
                                   PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * width * 4, width * 4);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        var xorSize = width * height * 4;
        var andStride = (width + 31) / 32 * 4;
        var andSize = andStride * height;
        var imageSize = 40 + xorSize + andSize;

        using var stream = new MemoryStream(22 + imageSize);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write((byte)(width >= 256 ? 0 : width));
        writer.Write((byte)(height >= 256 ? 0 : height));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(imageSize);
        writer.Write(22);

        writer.Write(40);
        writer.Write(width);
        writer.Write(height * 2);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(xorSize + andSize);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        for (var y = height - 1; y >= 0; y--)
        {
            writer.Write(pixels, y * width * 4, width * 4);
        }

        writer.Write(new byte[andSize]);
        writer.Flush();
        return stream.ToArray();
    }
}
