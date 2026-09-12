using DeepSeekStatus.Support;

namespace DeepSeekStatus.Models;

public enum PricePeriod
{
    Peak,
    OffPeak,
}

public static class PricePeriodExtensions
{
    public static string Key(this PricePeriod period) =>
        period == PricePeriod.Peak ? "peak" : "offPeak";

    public static string Title(this PricePeriod period) =>
        Strings.Get(period == PricePeriod.Peak ? "period.peak.title" : "period.offPeak.title");

    public static string ShortTitle(this PricePeriod period) =>
        Strings.Get(period == PricePeriod.Peak ? "period.peak.shortTitle" : "period.offPeak.shortTitle");

    public static string PriceText(this PricePeriod period) =>
        Strings.Get(period == PricePeriod.Peak ? "period.peak.priceText" : "period.offPeak.priceText");

    public static string Summary(this PricePeriod period) =>
        Strings.Get(period == PricePeriod.Peak ? "period.peak.summary" : "period.offPeak.summary");

    public static double PriceMultiplier(this PricePeriod period) =>
        period == PricePeriod.Peak ? 1.0 : 0.5;

    public static PricePeriod Opposite(this PricePeriod period) =>
        period == PricePeriod.Peak ? PricePeriod.OffPeak : PricePeriod.Peak;
}
