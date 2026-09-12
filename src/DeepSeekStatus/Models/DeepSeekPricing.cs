namespace DeepSeekStatus.Models;

public static class DeepSeekPricing
{
    public static readonly TimeSpan BeijingOffset = TimeSpan.FromHours(8);

    public static readonly (int Start, int End)[] PeakHourRanges = { (9, 12), (14, 18) };

    public static DateTimeOffset BeijingNow() => DateTimeOffset.UtcNow.ToOffset(BeijingOffset);

    public static PricePeriod PeriodAt(DateTimeOffset instant)
    {
        var beijing = instant.ToOffset(BeijingOffset);
        if (beijing.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return PricePeriod.OffPeak;
        }

        var minutes = beijing.Hour * 60 + beijing.Minute;
        foreach (var (start, end) in PeakHourRanges)
        {
            if (minutes >= start * 60 && minutes < end * 60)
            {
                return PricePeriod.Peak;
            }
        }

        return PricePeriod.OffPeak;
    }

    private static IEnumerable<DateTimeOffset> BoundariesOnDay(DateOnly day)
    {
        if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            yield break;
        }

        foreach (var (start, end) in PeakHourRanges)
        {
            yield return new DateTimeOffset(day.Year, day.Month, day.Day, start, 0, 0, BeijingOffset);
            yield return new DateTimeOffset(day.Year, day.Month, day.Day, end, 0, 0, BeijingOffset);
        }
    }

    public static DateTimeOffset NextTransition(DateTimeOffset now)
    {
        var current = PeriodAt(now);
        var day = DateOnly.FromDateTime(now.ToOffset(BeijingOffset).DateTime);
        for (var i = 0; i < 8; i++)
        {
            foreach (var candidate in BoundariesOnDay(day.AddDays(i)).Order())
            {
                if (candidate > now && PeriodAt(candidate) != current)
                {
                    return candidate;
                }
            }
        }

        return now.AddDays(7);
    }

    public static DateTimeOffset CurrentIntervalStart(DateTimeOffset now)
    {
        var day = DateOnly.FromDateTime(now.ToOffset(BeijingOffset).DateTime);
        DateTimeOffset? best = null;
        for (var i = -9; i <= 9; i++)
        {
            foreach (var boundary in BoundariesOnDay(day.AddDays(i)))
            {
                if (boundary <= now && (best is null || boundary > best.Value))
                {
                    best = boundary;
                }
            }
        }

        return best ?? now.ToOffset(BeijingOffset);
    }
}

public sealed class PricingSnapshot : IEquatable<PricingSnapshot>
{
    public DateTimeOffset Now { get; }
    public PricePeriod Period { get; }
    public DateTimeOffset IntervalStart { get; }
    public DateTimeOffset NextTransition { get; }
    public PricePeriod NextPeriod { get; }

    public double SecondsUntilTransition => Math.Max(0, (NextTransition - Now).TotalSeconds);

    public double IntervalProgress
    {
        get
        {
            var total = (NextTransition - IntervalStart).TotalSeconds;
            if (total <= 0)
            {
                return 0;
            }

            return Math.Clamp((Now - IntervalStart).TotalSeconds / total, 0, 1);
        }
    }

    public PricingSnapshot(DateTimeOffset now)
    {
        Now = now;
        Period = DeepSeekPricing.PeriodAt(now);
        IntervalStart = DeepSeekPricing.CurrentIntervalStart(now);
        NextTransition = DeepSeekPricing.NextTransition(now);
        NextPeriod = DeepSeekPricing.PeriodAt(NextTransition.AddSeconds(1));
    }

    public bool Equals(PricingSnapshot? other) =>
        other is not null
        && Now == other.Now
        && Period == other.Period
        && IntervalStart == other.IntervalStart
        && NextTransition == other.NextTransition
        && NextPeriod == other.NextPeriod;

    public override bool Equals(object? obj) => Equals(obj as PricingSnapshot);

    public override int GetHashCode() => HashCode.Combine(Now, Period, IntervalStart, NextTransition, NextPeriod);
}
