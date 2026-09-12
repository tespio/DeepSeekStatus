using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeepSeekStatus.Support;

public static class PanelExporter
{
    public static int Export(FrameworkElement panel, string directory, string name)
    {
        Directory.CreateDirectory(directory);
        panel.UpdateLayout();

        var width = panel.ActualWidth;
        var height = panel.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return 1;
        }

        var effect = panel.Effect;
        panel.Effect = null;
        try
        {
            const double scale = 2.0;
            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(width * scale),
                (int)Math.Ceiling(height * scale),
                96 * scale,
                96 * scale,
                PixelFormats.Pbgra32);
            bitmap.Render(panel);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(directory, $"{name}.png"));
            encoder.Save(stream);
        }
        finally
        {
            panel.Effect = effect;
        }

        return 0;
    }
}
