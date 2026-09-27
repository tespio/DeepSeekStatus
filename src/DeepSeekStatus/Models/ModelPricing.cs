using System.Reflection;
using System.Text.Json;

namespace DeepSeekStatus.Models;

public sealed class PricePair
{
    public string Peak { get; set; } = string.Empty;

    public string OffPeak { get; set; } = string.Empty;

    public string For(PricePeriod period) => period == PricePeriod.Peak ? Peak : OffPeak;
}

public sealed class ModelPricing
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Concurrency { get; set; } = string.Empty;

    public PricePair InputCacheHit { get; set; } = new();

    public PricePair InputCacheMiss { get; set; } = new();

    public PricePair Output { get; set; } = new();
}

public sealed class PricingCatalog
{
    private const string ResourceName = "DeepSeekStatus.Assets.pricing.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Source { get; set; } = string.Empty;

    public string Updated { get; set; } = string.Empty;

    public string Unit { get; set; } = "USD";

    public List<ModelPricing> Models { get; set; } = new();

    public static PricingCatalog Load()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                return new PricingCatalog();
            }

            return JsonSerializer.Deserialize<PricingCatalog>(stream, Options) ?? new PricingCatalog();
        }
        catch
        {
            return new PricingCatalog();
        }
    }
}
