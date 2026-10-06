using DeepSeekStatus.Support;

namespace DeepSeekStatus.Models;

public enum PricingDayKind
{
    PublicHoliday,
    AlternateWorkdayWeekend,
    Weekend,
    RegularWeekday,
}

public sealed record PricingDayInfo
{
    public PricingDayKind Kind { get; init; }

    public string HolidayName { get; init; } = string.Empty;

    public bool ScheduleIsCovered { get; init; }

    public bool IsAllDayOffPeak => Kind != PricingDayKind.RegularWeekday;

    public static PricingDayInfo For(DateOnly day)
    {
        var isWeekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var covered = ChineseHolidays.CoversYear(day.Year);

        if (isWeekend && ChineseHolidays.AlternateWorkdayName(day) is { } workdayName)
        {
            return new PricingDayInfo
            {
                Kind = PricingDayKind.AlternateWorkdayWeekend,
                HolidayName = workdayName,
                ScheduleIsCovered = covered,
            };
        }

        if (ChineseHolidays.HolidayName(day) is { } holidayName)
        {
            return new PricingDayInfo
            {
                Kind = PricingDayKind.PublicHoliday,
                HolidayName = holidayName,
                ScheduleIsCovered = covered,
            };
        }

        return new PricingDayInfo
        {
            Kind = isWeekend ? PricingDayKind.Weekend : PricingDayKind.RegularWeekday,
            ScheduleIsCovered = covered,
        };
    }

    public string LocalizedDetail() => Kind switch
    {
        PricingDayKind.PublicHoliday =>
            string.Format(Strings.Get("calendar.detail.holiday"), LocalizedName(HolidayName)),
        PricingDayKind.AlternateWorkdayWeekend =>
            string.Format(Strings.Get("calendar.detail.alternateWorkday"), LocalizedName(HolidayName)),
        PricingDayKind.Weekend => Strings.Get("calendar.detail.weekend"),
        _ => Strings.Get("calendar.detail.weekday"),
    };

    public static string LocalizedName(string raw) => raw switch
    {
        "元旦" => Strings.Get("holiday.newYear"),
        "春节" => Strings.Get("holiday.springFestival"),
        "清明" or "清明节" => Strings.Get("holiday.qingming"),
        "劳动节" => Strings.Get("holiday.labourDay"),
        "端午节" => Strings.Get("holiday.dragonBoat"),
        "中秋节" => Strings.Get("holiday.midAutumn"),
        "国庆节" => Strings.Get("holiday.nationalDay"),
        "国庆节、中秋节" => Strings.Get("holiday.nationalDayMidAutumn"),
        _ => Strings.Get("holiday.public"),
    };
}
