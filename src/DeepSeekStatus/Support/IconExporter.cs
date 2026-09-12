using System.Drawing.Imaging;
using DeepSeekStatus.Models;

namespace DeepSeekStatus.Support;

public static class IconExporter
{
    public static int Export(string directory)
    {
        Directory.CreateDirectory(directory);

        using (var app = TrayIconRenderer.RenderAppIconBitmap(256))
        {
            app.Save(Path.Combine(directory, "app-icon.png"), ImageFormat.Png);
            File.WriteAllBytes(Path.Combine(directory, "app.ico"), IcoBuilder.BuildBytes(app));
        }

        foreach (var period in new[] { PricePeriod.Peak, PricePeriod.OffPeak })
        {
            foreach (var dark in new[] { false, true })
            {
                using var bitmap = TrayIconRenderer.RenderTrayBitmap(period, dark, 128);
                var suffix = dark ? "dark" : "light";
                bitmap.Save(Path.Combine(directory, $"tray-{period.Key()}-{suffix}.png"), ImageFormat.Png);
            }
        }

        return 0;
    }
}
