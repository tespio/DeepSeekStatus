using System.Reflection;
using System.Text.Json;

namespace DeepSeekStatus.Support;

public static class ChineseHolidays
{
    private const string ResourceName = "DeepSeekStatus.Assets.china-holidays.json";

    private static readonly Dictionary<DateOnly, string> Holidays = new();
    private static readonly Dictionary<DateOnly, string> Workdays = new();

    static ChineseHolidays()
    {
        Load();
    }

    public static bool HasData => Holidays.Count > 0;

    public static bool CoversYear(int year) => Holidays.Keys.Any(day => day.Year == year);

    public static bool IsHoliday(DateOnly day) => Holidays.ContainsKey(day);

    public static string? HolidayName(DateOnly day) => Holidays.TryGetValue(day, out var name) ? name : null;

    public static bool IsAlternateWorkday(DateOnly day) => Workdays.ContainsKey(day);

    public static string? AlternateWorkdayName(DateOnly day) =>
        Workdays.TryGetValue(day, out var name) ? name : null;

    private static void Load()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                return;
            }

            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("years", out var years))
            {
                return;
            }

            foreach (var year in years.EnumerateObject())
            {
                if (year.Value.TryGetProperty("holidays", out var holidays))
                {
                    Read(holidays, Holidays);
                }

                if (year.Value.TryGetProperty("workdays", out var workdays))
                {
                    Read(workdays, Workdays);
                }
            }
        }
        catch
        {
        }
    }

    private static void Read(JsonElement array, Dictionary<DateOnly, string> target)
    {
        foreach (var entry in array.EnumerateArray())
        {
            if (!entry.TryGetProperty("date", out var dateElement)
                || dateElement.GetString() is not { } dateText
                || !DateOnly.TryParse(dateText, out var date))
            {
                continue;
            }

            var name = entry.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;
            target[date] = name;
        }
    }
}
