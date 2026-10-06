using System.Runtime.InteropServices;
using DeepSeekStatus.Models;

namespace DeepSeekStatus.Support;

public static class SelfTest
{
    private static int _checks;
    private static int _failures;

    public static int Run()
    {
        AttachConsole(-1);
        try
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
        catch
        {
        }

        Console.WriteLine("DeepSeek Status self-test");
        Console.WriteLine(new string('-', 48));

        var monday = new DateTimeOffset(2026, 9, 14, 0, 0, 0, DeepSeekPricing.BeijingOffset);
        DateTimeOffset At(int dayOffset, int hour, int minute = 0, int second = 0) =>
            monday.AddDays(dayOffset).AddHours(hour).AddMinutes(minute).AddSeconds(second);

        Check("Mon 08:59:59", DeepSeekPricing.PeriodAt(At(0, 8, 59, 59)), PricePeriod.OffPeak);
        Check("Mon 09:00:00", DeepSeekPricing.PeriodAt(At(0, 9)), PricePeriod.Peak);
        Check("Mon 11:59:59", DeepSeekPricing.PeriodAt(At(0, 11, 59, 59)), PricePeriod.Peak);
        Check("Mon 12:00:00", DeepSeekPricing.PeriodAt(At(0, 12)), PricePeriod.OffPeak);
        Check("Mon 13:59:59", DeepSeekPricing.PeriodAt(At(0, 13, 59, 59)), PricePeriod.OffPeak);
        Check("Mon 14:00:00", DeepSeekPricing.PeriodAt(At(0, 14)), PricePeriod.Peak);
        Check("Mon 17:59:59", DeepSeekPricing.PeriodAt(At(0, 17, 59, 59)), PricePeriod.Peak);
        Check("Mon 18:00:00", DeepSeekPricing.PeriodAt(At(0, 18)), PricePeriod.OffPeak);
        Check("Sat 10:00:00", DeepSeekPricing.PeriodAt(At(5, 10)), PricePeriod.OffPeak);
        Check("Sun 20:00:00", DeepSeekPricing.PeriodAt(At(6, 20)), PricePeriod.OffPeak);

        Check("next from Mon 08:00", DeepSeekPricing.NextTransition(At(0, 8)), At(0, 9));
        Check("next from Mon 09:00", DeepSeekPricing.NextTransition(At(0, 9)), At(0, 12));
        Check("next from Mon 12:00", DeepSeekPricing.NextTransition(At(0, 12)), At(0, 14));
        Check("next from Mon 14:00", DeepSeekPricing.NextTransition(At(0, 14)), At(0, 18));
        Check("next from Mon 18:00", DeepSeekPricing.NextTransition(At(0, 18)), At(1, 9));
        Check("next from Fri 17:30", DeepSeekPricing.NextTransition(At(4, 17, 30)), At(4, 18));
        Check("next from Sat 10:00", DeepSeekPricing.NextTransition(At(5, 10)), At(7, 9));
        Check("next from Sun 23:59", DeepSeekPricing.NextTransition(At(6, 23, 59, 59)), At(7, 9));

        Check("interval start Mon 00:30", DeepSeekPricing.CurrentIntervalStart(At(0, 0, 30)), At(-3, 18));
        Check("interval start Mon 09:30", DeepSeekPricing.CurrentIntervalStart(At(0, 9, 30)), At(0, 9));
        Check("interval start Mon 13:00", DeepSeekPricing.CurrentIntervalStart(At(0, 13)), At(0, 12));
        Check("interval start Sat 08:00", DeepSeekPricing.CurrentIntervalStart(At(5, 8)), At(4, 18));

        var morning = new PricingSnapshot(At(0, 11));
        Check("snapshot Mon 11:00 next", morning.NextPeriod, PricePeriod.OffPeak);
        var noon = new PricingSnapshot(At(0, 12, 30));
        Check("snapshot Mon 12:30 next", noon.NextPeriod, PricePeriod.Peak);
        Check("snapshot Mon 12:30 remaining", noon.SecondsUntilTransition, 5400.0);

        Check("countdown 23:59:59", PricingFormatter.CompactCountdown(86399), "23:59:59");
        Check("countdown 00:00:00", PricingFormatter.CompactCountdown(0), "00:00:00");
        Check("countdown 99h cap", PricingFormatter.CompactCountdown(360_000), "99:00:00");

        Check("offset +3", PricingFormatter.OffsetText(TimeSpan.FromHours(3)), "GMT+3");
        Check("offset -4", PricingFormatter.OffsetText(TimeSpan.FromHours(-4)), "GMT-4");
        Check("offset +5:30", PricingFormatter.OffsetText(TimeSpan.FromMinutes(330)), "GMT+5:30");
        Check("offset +0", PricingFormatter.OffsetText(TimeSpan.Zero), "GMT+0");

        var eastern = TimeZoneInfo.CreateCustomTimeZone("TestEastern", TimeSpan.FromHours(-4), "EST", "EST");
        var beijing = TimeZoneInfo.CreateCustomTimeZone("TestBeijing", DeepSeekPricing.BeijingOffset, "CST", "CST");
        var mondayLocal = new DateOnly(2026, 9, 14);
        var fridayLocal = new DateOnly(2026, 9, 18);
        var saturdayLocal = new DateOnly(2026, 9, 19);

        Check("local heat Mon 20:00 UTC-4", PricingFormatter.IsPeakAtLocal(mondayLocal, 20, eastern), false);
        Check("local heat Mon 21:00 UTC-4", PricingFormatter.IsPeakAtLocal(mondayLocal, 21, eastern), true);
        Check("local heat Mon 01:00 UTC-4", PricingFormatter.IsPeakAtLocal(mondayLocal, 1, eastern), false);
        Check("local heat Mon 02:00 UTC-4", PricingFormatter.IsPeakAtLocal(mondayLocal, 2, eastern), true);
        Check("local heat Fri 05:00 UTC-4", PricingFormatter.IsPeakAtLocal(fridayLocal, 5, eastern), true);
        Check("local heat Fri 06:00 UTC-4", PricingFormatter.IsPeakAtLocal(fridayLocal, 6, eastern), false);
        Check("local heat Sat 01:00 UTC-4", PricingFormatter.IsPeakAtLocal(saturdayLocal, 1, eastern), false);
        Check("local heat Mon 09:00 UTC+8", PricingFormatter.IsPeakAtLocal(mondayLocal, 9, beijing), true);
        Check("local heat Mon 12:00 UTC+8", PricingFormatter.IsPeakAtLocal(mondayLocal, 12, beijing), false);

        var balanceJson = """{"is_available":true,"balance_infos":[{"currency":"CNY","total_balance":"110.00","granted_balance":"10.00","topped_up_balance":"100.00"}]}""";
        var parsed = DeepSeekBalance.FromJson(balanceJson);
        Check("balance parsed", parsed is not null, true);
        Check("balance available", parsed?.IsAvailable ?? false, true);
        Check("balance currencies", parsed?.BalanceInfos.Count ?? 0, 1);
        var info = parsed?.BalanceInfos.FirstOrDefault();
        Check("balance total", info is null ? "" : info.Amount(info.TotalBalance), "¥110.00");
        Check("balance granted", info is null ? "" : info.Amount(info.GrantedBalance), "¥10.00");
        Check("balance invalid json", DeepSeekBalance.FromJson("{oops") is null, true);
        Check("balance amount text", BalanceStore.FormatAmounts(parsed), "¥110.00");
        Check("balance amount text empty", BalanceStore.FormatAmounts(null) is null, true);
        Check("balance unauthorized replaces key", new BalanceException(BalanceErrorKind.Unauthorized).SuggestsReplacingKey, true);
        Check("balance http keeps key", new BalanceException(BalanceErrorKind.Http, status: 500).SuggestsReplacingKey, false);

        DateTimeOffset D(int year, int month, int day, int hour, int minute = 0) =>
            new(year, month, day, hour, minute, 0, DeepSeekPricing.BeijingOffset);

        Check("holiday data covers 2026", ChineseHolidays.CoversYear(2026), true);
        Check("holiday data lacks 2027", ChineseHolidays.CoversYear(2027), false);
        Check("national day Thu 10:00", DeepSeekPricing.PeriodAt(D(2026, 10, 1, 10)), PricePeriod.OffPeak);
        Check("day after holiday Thu 10:00", DeepSeekPricing.PeriodAt(D(2026, 10, 8, 10)), PricePeriod.Peak);
        Check("adjusted workday Sat 10:00", DeepSeekPricing.PeriodAt(D(2026, 10, 10, 10)), PricePeriod.OffPeak);
        Check("mid-autumn Fri 10:00", DeepSeekPricing.PeriodAt(D(2026, 9, 25, 10)), PricePeriod.OffPeak);
        Check("day before mid-autumn Thu 10:00", DeepSeekPricing.PeriodAt(D(2026, 9, 24, 10)), PricePeriod.Peak);
        Check("spring festival Tue 10:00", DeepSeekPricing.PeriodAt(D(2026, 2, 17, 10)), PricePeriod.OffPeak);
        Check("2025 national day Wed 10:00", DeepSeekPricing.PeriodAt(D(2025, 10, 1, 10)), PricePeriod.OffPeak);
        Check("unknown year falls back", DeepSeekPricing.PeriodAt(D(2030, 1, 1, 10)), PricePeriod.Peak);
        Check("next after holiday starts", DeepSeekPricing.NextTransition(D(2026, 10, 1, 8)), D(2026, 10, 8, 9));
        Check("next after spring festival", DeepSeekPricing.NextTransition(D(2026, 2, 13, 19)), D(2026, 2, 24, 9));
        Check("interval start across holiday", DeepSeekPricing.CurrentIntervalStart(D(2026, 10, 5, 10)), D(2026, 9, 30, 18));
        Check("snapshot holiday flag", new PricingSnapshot(D(2026, 10, 1, 10)).IsHoliday, true);
        Check("snapshot non-holiday flag", new PricingSnapshot(D(2026, 10, 8, 10)).IsHoliday, false);

        var catalog = PricingCatalog.Load();
        Check("pricing model count", catalog.Models.Count, 2);
        Check("pricing flash id", catalog.Models.FirstOrDefault()?.Id ?? "", "flash");
        Check("pricing flash cache miss off-peak", catalog.Models.FirstOrDefault()?.InputCacheMiss.OffPeak ?? "", "0.15");
        Check("pricing pro output peak", catalog.Models.Skip(1).FirstOrDefault()?.Output.Peak ?? "", "3.96");
        Check("pricing updated", catalog.Updated.Length > 0, true);

        var localNow = new DateTimeOffset(DateTime.Today.AddHours(15));
        var usageSamples = new List<UsageSample>
        {
            new(localNow.AddDays(-1).AddHours(-5), "USD", 100m, 0m, 100m),
            new(localNow.AddDays(-1).AddHours(-3), "USD", 99m, 0m, 99m),
            new(localNow.AddHours(-4), "USD", 98m, 0m, 98m),
            new(localNow.AddHours(-2), "USD", 103m, 0m, 103m),
            new(localNow.AddHours(-1), "USD", 102.5m, 0m, 102.5m),
        };
        var usage = UsageCalculator.Compute(usageSamples, localNow);
        Check("usage today", usage.SpendToday, 1.5m);
        Check("usage 7 days", usage.Spend7Days, 2.5m);
        Check("usage 30 days", usage.Spend30Days, 2.5m);
        Check("usage max daily", usage.MaxDaily, 1.5m);
        Check("usage currency", usage.Currency, "USD");
        Check("usage amount usd", UsageCalculator.AmountText("USD", 1.5m), "$1.50");
        Check("usage amount cny", UsageCalculator.AmountText("CNY", 2m), "¥2.00");
        Check("usage tokens k", UsageCalculator.TokenEstimateText(0.00015m, "0.15"), "1K");
        Check("usage tokens m", UsageCalculator.TokenEstimateText(0.15m, "0.15"), "1M");
        Check("usage tokens b", UsageCalculator.TokenEstimateText(1500m, "0.15"), "10B");
        Check("usage tokens none", UsageCalculator.TokenEstimateText(0m, "0.15").Length, 0);

        var historyPath = Path.Combine(Path.GetTempPath(), "dsstatus-usage-test.json");
        UsageHistory.Save(historyPath, usageSamples);
        var loadedHistory = UsageHistory.Load(historyPath);
        File.Delete(historyPath);
        Check("usage history round-trip", loadedHistory.Count, usageSamples.Count);
        Check("usage history newest", loadedHistory[^1].Total, 102.5m);

        const string testTarget = "DeepSeekStatus/selftest-key";
        CredentialManager.Delete(testTarget);
        CredentialManager.Save("sk-selftest-123", testTarget);
        Check("credential round-trip", CredentialManager.Load(testTarget), "sk-selftest-123");
        CredentialManager.Delete(testTarget);
        Check("credential deleted", CredentialManager.Load(testTarget) is null, true);

        Console.WriteLine(new string('-', 48));
        Console.WriteLine($"figures={WhaleGeometry.FigureCount} segments={WhaleGeometry.SegmentCount}");
        Console.WriteLine($"bounds={WhaleGeometry.Bounds} aspect={WhaleGeometry.AspectRatio:0.000}");

        _checks++;
        if (WhaleGeometry.FigureCount != 4 || WhaleGeometry.SegmentCount < 50)
        {
            _failures++;
            Console.WriteLine("FAIL  whale geometry did not parse as expected");
        }
        else
        {
            Console.WriteLine("  ok  whale geometry");
        }

        _checks++;
        var aspect = WhaleGeometry.AspectRatio;
        if (aspect is < 1.0 or > 2.5 || WhaleGeometry.Bounds.Width > 25 || WhaleGeometry.Bounds.Height > 25)
        {
            _failures++;
            Console.WriteLine("FAIL  whale bounds out of range");
        }
        else
        {
            Console.WriteLine("  ok  whale bounds");
        }

        Console.WriteLine(new string('-', 48));
        Console.WriteLine($"{_checks - _failures}/{_checks} checks passed");
        return _failures == 0 ? 0 : 1;
    }

    private static void Check<T>(string name, T actual, T expected)
    {
        _checks++;
        if (EqualityComparer<T>.Default.Equals(actual, expected))
        {
            Console.WriteLine($"  ok  {name}");
            return;
        }

        _failures++;
        Console.WriteLine($"FAIL  {name}: expected {expected}, got {actual}");
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
}
