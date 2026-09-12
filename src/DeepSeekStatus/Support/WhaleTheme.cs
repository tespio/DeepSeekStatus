using System.Windows.Media;
using DeepSeekStatus.Models;

namespace DeepSeekStatus.Support;

public static class WhaleTheme
{
    public static readonly Color BrandBlue = Color.FromRgb(77, 107, 254);
    public static readonly Color BrandBlueBright = Color.FromRgb(122, 158, 255);
    public static readonly Color BrandBlueDeep = Color.FromRgb(46, 76, 214);

    public static readonly Color SleepBlue = Color.FromRgb(130, 145, 185);
    public static readonly Color SleepBlueLight = Color.FromRgb(176, 189, 219);
    public static readonly Color SleepBlueDeep = Color.FromRgb(96, 108, 145);

    public static readonly Color PeakAccent = Color.FromRgb(255, 138, 61);
    public static readonly Color OffPeakAccent = Color.FromRgb(48, 196, 141);

    public static readonly Color TankTop = Color.FromRgb(18, 30, 74);
    public static readonly Color TankBottom = Color.FromRgb(8, 14, 38);
    public static readonly Color TankTopSleep = Color.FromRgb(20, 26, 48);
    public static readonly Color TankBottomSleep = Color.FromRgb(9, 12, 24);

    public static Color Accent(PricePeriod period) =>
        period == PricePeriod.Peak ? PeakAccent : OffPeakAccent;
}

public sealed class WhalePalette
{
    public Color[] Body { get; init; } = Array.Empty<Color>();
    public Color Bubble { get; init; }
    public Color SleepMark { get; init; }
    public bool IsFlat { get; init; }

    public Color FillColor => Body[Body.Length / 2];

    public static WhalePalette MenuBar(PricePeriod period, bool dark)
    {
        return period switch
        {
            PricePeriod.Peak => dark
                ? new WhalePalette
                {
                    Body = new[]
                    {
                        Color.FromRgb(201, 227, 255),
                        Color.FromRgb(168, 212, 255),
                        Color.FromRgb(135, 191, 255),
                    },
                    Bubble = Color.FromRgb(168, 212, 255),
                    SleepMark = Color.FromRgb(168, 212, 255),
                    IsFlat = true,
                }
                : new WhalePalette
                {
                    Body = new[] { WhaleTheme.BrandBlueBright, WhaleTheme.BrandBlue, WhaleTheme.BrandBlueDeep },
                    Bubble = WhaleTheme.BrandBlue,
                    SleepMark = WhaleTheme.BrandBlue,
                    IsFlat = true,
                },
            _ => dark
                ? new WhalePalette
                {
                    Body = new[]
                    {
                        Color.FromRgb(230, 237, 255),
                        Color.FromRgb(204, 217, 247),
                        Color.FromRgb(179, 196, 237),
                    },
                    Bubble = Color.FromRgb(204, 217, 247),
                    SleepMark = Color.FromRgb(230, 237, 255),
                    IsFlat = true,
                }
                : new WhalePalette
                {
                    Body = new[]
                    {
                        Color.FromRgb(122, 135, 168),
                        Color.FromRgb(94, 107, 143),
                        Color.FromRgb(71, 84, 117),
                    },
                    Bubble = Color.FromRgb(94, 107, 143),
                    SleepMark = Color.FromRgb(94, 107, 143),
                    IsFlat = true,
                },
        };
    }

    public static WhalePalette Aquarium(PricePeriod period)
    {
        return period switch
        {
            PricePeriod.Peak => new WhalePalette
            {
                Body = new[]
                {
                    Color.FromRgb(158, 191, 255),
                    WhaleTheme.BrandBlue,
                    WhaleTheme.BrandBlueDeep,
                },
                Bubble = Color.FromArgb(166, 255, 255, 255),
                SleepMark = WhaleTheme.BrandBlueBright,
            },
            _ => new WhalePalette
            {
                Body = new[]
                {
                    WhaleTheme.SleepBlueLight,
                    WhaleTheme.SleepBlue,
                    WhaleTheme.SleepBlueDeep,
                },
                Bubble = Color.FromArgb(89, 255, 255, 255),
                SleepMark = WhaleTheme.SleepBlueLight,
            },
        };
    }
}
