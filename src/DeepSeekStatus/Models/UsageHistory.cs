using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeepSeekStatus.Models;

public sealed record UsageSample(DateTimeOffset Time, string Currency, decimal Total, decimal Granted, decimal ToppedUp);

public sealed class UsageSummary
{
    public string Currency { get; init; } = string.Empty;

    public decimal SpendToday { get; init; }

    public decimal Spend7Days { get; init; }

    public decimal Spend30Days { get; init; }

    public IReadOnlyList<decimal> DailyTotals { get; init; } = Array.Empty<decimal>();

    public decimal MaxDaily { get; init; }
}

public static class UsageCalculator
{
    public const int WindowDays = 30;

    public static UsageSummary Compute(IReadOnlyList<UsageSample> samples, DateTimeOffset now)
    {
        var start = DateOnly.FromDateTime(now.LocalDateTime).AddDays(-(WindowDays - 1));
        var buckets = new decimal[WindowDays];
        var currency = samples.Count > 0 ? samples[^1].Currency : string.Empty;

        for (var index = 1; index < samples.Count; index++)
        {
            var previous = samples[index - 1];
            var current = samples[index];
            if (previous.Currency != current.Currency)
            {
                continue;
            }

            var spend = previous.Total - current.Total;
            if (spend <= 0)
            {
                continue;
            }

            var bucket = DateOnly.FromDateTime(current.Time.LocalDateTime).DayNumber - start.DayNumber;
            if (bucket >= 0 && bucket < WindowDays)
            {
                buckets[bucket] += spend;
            }
        }

        return new UsageSummary
        {
            Currency = currency,
            SpendToday = buckets[^1],
            Spend7Days = buckets.Skip(WindowDays - 7).Sum(),
            Spend30Days = buckets.Sum(),
            DailyTotals = buckets,
            MaxDaily = buckets.Max(),
        };
    }

    public static decimal ParseAmount(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : 0m;

    public static string AmountText(string currency, decimal amount)
    {
        var symbol = string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ? "$" : "¥";
        return symbol + amount.ToString("0.00", CultureInfo.InvariantCulture);
    }

    public static string TokenEstimateText(decimal spend, string pricePerMillion)
    {
        if (spend <= 0
            || !decimal.TryParse(pricePerMillion, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
            || price <= 0)
        {
            return string.Empty;
        }

        var millions = spend / price;
        if (millions >= 1_000)
        {
            return (millions / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "B";
        }

        if (millions >= 1)
        {
            return millions.ToString("0.#", CultureInfo.InvariantCulture) + "M";
        }

        var thousands = millions * 1_000;
        return thousands >= 1
            ? thousands.ToString("0.#", CultureInfo.InvariantCulture) + "K"
            : "0";
    }
}

public static class UsageHistory
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
    };

    public static List<UsageSample> Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new List<UsageSample>();
            }

            var entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path), Options);
            if (entries is null)
            {
                return new List<UsageSample>();
            }

            return entries
                .Select(entry => new UsageSample(entry.Time, entry.Currency, entry.Total, entry.Granted, entry.ToppedUp))
                .OrderBy(sample => sample.Time)
                .ToList();
        }
        catch
        {
            return new List<UsageSample>();
        }
    }

    public static void Save(string path, IReadOnlyList<UsageSample> samples)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var entries = samples.Select(sample => new Entry
            {
                Time = sample.Time,
                Currency = sample.Currency,
                Total = sample.Total,
                Granted = sample.Granted,
                ToppedUp = sample.ToppedUp,
            }).ToList();

            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(entries, Options));
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
        }
    }

    private sealed class Entry
    {
        [JsonPropertyName("time")]
        public DateTimeOffset Time { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("total")]
        public decimal Total { get; set; }

        [JsonPropertyName("granted")]
        public decimal Granted { get; set; }

        [JsonPropertyName("toppedUp")]
        public decimal ToppedUp { get; set; }
    }
}
