using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DeepSeekStatus.Models;

public sealed class DeepSeekBalance
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public bool IsAvailable { get; set; }

    public List<BalanceInfo> BalanceInfos { get; set; } = new();

    public static DeepSeekBalance? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<DeepSeekBalance>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public sealed class BalanceInfo
    {
        public string Currency { get; set; } = string.Empty;

        public string TotalBalance { get; set; } = string.Empty;

        public string GrantedBalance { get; set; } = string.Empty;

        public string ToppedUpBalance { get; set; } = string.Empty;

        public string Symbol =>
            string.Equals(Currency, "USD", StringComparison.OrdinalIgnoreCase) ? "$" : "¥";

        public string Amount(string value) => Symbol + value;
    }
}

public enum BalanceErrorKind
{
    Unauthorized,
    Http,
    Network,
    Decoding,
}

public sealed class BalanceException : Exception
{
    public BalanceException(BalanceErrorKind kind, string? message = null, int status = 0)
        : base(message)
    {
        Kind = kind;
        Status = status;
    }

    public BalanceErrorKind Kind { get; }

    public int Status { get; }

    public bool SuggestsReplacingKey => Kind == BalanceErrorKind.Unauthorized;

    public string Describe() => Kind switch
    {
        BalanceErrorKind.Unauthorized => Support.Strings.Get("balance.error.unauthorized"),
        BalanceErrorKind.Http => string.Format(Support.Strings.Get("balance.error.server"), Status),
        BalanceErrorKind.Network => string.Format(Support.Strings.Get("balance.error.network"), Message),
        _ => Support.Strings.Get("balance.error.decoding"),
    };
}

public static class DeepSeekBalanceClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    public static async Task<DeepSeekBalance> FetchAsync(string apiKey,
                                                         CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.deepseek.com/user/balance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new BalanceException(BalanceErrorKind.Network, exception.Message);
        }

        using (response)
        {
            switch ((int)response.StatusCode)
            {
                case 200:
                    break;
                case 401:
                case 403:
                    throw new BalanceException(BalanceErrorKind.Unauthorized);
                default:
                    throw new BalanceException(BalanceErrorKind.Http,
                                               status: (int)response.StatusCode);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return DeepSeekBalance.FromJson(json)
                   ?? throw new BalanceException(BalanceErrorKind.Decoding);
        }
    }
}
