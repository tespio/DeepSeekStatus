using System.Globalization;
using DeepSeekStatus.Models;

namespace DeepSeekStatus.Support;

public static class PricingFormatter
{
    public static string Time(DateTimeOffset date) =>
        date.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string PreciseTime(DateTimeOffset date) =>
        date.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public static string Day(DateTimeOffset date)
    {
        var local = date.ToLocalTime();
        return Strings.IsChinese
            ? local.ToString("M月d日dddd", Strings.Culture)
            : local.ToString("dddd, MMMM d", Strings.Culture);
    }

    public static string WeekdayName(DateTimeOffset date) =>
        date.ToLocalTime().ToString("ddd", Strings.Culture);

    public static string[] WeekdaySymbolsMondayFirst()
    {
        var symbols = Strings.Culture.DateTimeFormat.AbbreviatedDayNames;
        return Enumerable.Range(1, 7).Select(index => symbols[index % 7]).ToArray();
    }

    public static string TransitionDescription(DateTimeOffset date, DateTimeOffset now)
    {
        var localDate = date.ToLocalTime();
        var localNow = now.ToLocalTime();
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var target = DateOnly.FromDateTime(localDate.DateTime);
        var dayDelta = target.DayNumber - today.DayNumber;
        var timeText = Time(date);

        return dayDelta switch
        {
            < 0 => timeText,
            0 => string.Format(Strings.Get("transition.today"), timeText),
            1 => string.Format(Strings.Get("transition.tomorrow"), timeText),
            2 => string.Format(Strings.Get("transition.dayAfterTomorrow"), timeText),
            _ when dayDelta < 7 => string.Format(Strings.Get("transition.weekday"), WeekdayName(date), timeText),
            _ => localDate.ToString("MMM d HH:mm", Strings.Culture),
        };
    }

    public static bool IsLocalZoneBeijing
    {
        get
        {
            var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
            return offset == DeepSeekPricing.BeijingOffset;
        }
    }

    public static string LocalOffsetText() =>
        OffsetText(TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow));

    public static string OffsetText(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset.Duration();
        var hours = (int)absolute.TotalHours;
        var minutes = absolute.Minutes;
        return minutes == 0
            ? $"GMT{sign}{hours}"
            : $"GMT{sign}{hours}:{minutes:00}";
    }

    public static bool IsPeakAtLocal(DateOnly day, int hour, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(new TimeOnly(hour, 0));
        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
            if (zone.IsInvalidTime(local))
            {
                local = local.AddHours(1);
            }
        }

        var instant = new DateTimeOffset(local, zone.GetUtcOffset(local));
        return DeepSeekPricing.PeriodAt(instant) == PricePeriod.Peak;
    }

    public static string Duration(double seconds)
    {
        var total = (long)Math.Floor(seconds);
        var days = total / 86_400;
        var hours = total % 86_400 / 3_600;
        var minutes = total % 3_600 / 60;
        var secs = total % 60;

        if (days > 0)
        {
            return hours > 0 ? $"{DayUnit(days)} {HourUnit(hours)}" : DayUnit(days);
        }

        if (hours > 0)
        {
            return minutes > 0 ? $"{HourUnit(hours)} {MinuteUnit(minutes)}" : HourUnit(hours);
        }

        if (minutes > 0)
        {
            return $"{MinuteUnit(minutes)} {SecondUnit(secs)}";
        }

        return SecondUnit(secs);
    }

    public static string CompactCountdown(double seconds)
    {
        var total = (long)Math.Floor(Math.Max(0, seconds));
        var hours = Math.Min(total / 3_600, 99);
        var minutes = total % 3_600 / 60;
        var secs = total % 60;
        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", hours, minutes, secs);
    }

    private static string Counted(long value, string oneKey, string otherKey) =>
        value == 1
            ? Strings.Get(oneKey)
            : string.Format(Strings.Get(otherKey), value);

    private static string DayUnit(long value) =>
        Counted(value, "duration.day.one", "duration.day.other");

    private static string HourUnit(long value) =>
        Counted(value, "duration.hour.one", "duration.hour.other");

    private static string MinuteUnit(long value) =>
        Counted(value, "duration.minute.one", "duration.minute.other");

    private static string SecondUnit(long value) =>
        Counted(value, "duration.second.one", "duration.second.other");
}
