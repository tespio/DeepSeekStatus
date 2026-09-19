using System.Reflection;
using System.Text.Json;

namespace DeepSeekStatus.Support;

public static class ChineseHolidays
{
    private const string ResourceName = "DeepSeekStatus.Assets.china-holidays.json";

    private static readonly HashSet<DateOnly> OffDays = Load();

    public static bool HasData => OffDays.Count > 0;

    public static bool CoversYear(int year) => OffDays.Any(day => day.Year == year);

    public static bool IsHoliday(DateOnly day) => OffDays.Contains(day);

    private static HashSet<DateOnly> Load()
    {
        var days = new HashSet<DateOnly>();
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                return days;
            }

            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("years", out var years))
            {
                return days;
            }

            foreach (var year in years.EnumerateObject())
            {
                foreach (var entry in year.Value.EnumerateArray())
                {
                    if (entry.GetString() is { } text && DateOnly.TryParse(text, out var date))
                    {
                        days.Add(date);
                    }
                }
            }
        }
        catch
        {
        }

        return days;
    }
}
