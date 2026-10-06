using System.ComponentModel;

namespace DeepSeekStatus.Models;

public sealed class UsageStore : INotifyPropertyChanged
{
    private readonly BalanceStore _balance;
    private readonly string _path;
    private readonly List<UsageSample> _samples;
    private UsageSummary _summary = new();

    public UsageStore(BalanceStore balance, string? path = null)
    {
        _balance = balance;
        _path = path ?? DefaultPath;
        _samples = UsageHistory.Load(_path);
        _balance.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(BalanceStore.LastRefreshed))
            {
                Record();
            }
        };
        Recompute();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeepSeekStatus",
        "balance-history.json");

    public bool HasData => _samples.Count > 1;

    public UsageSummary Summary => _summary;

    private void Record()
    {
        var info = _balance.Balance?.BalanceInfos.FirstOrDefault();
        if (info is null)
        {
            return;
        }

        var total = UsageCalculator.ParseAmount(info.TotalBalance);
        var granted = UsageCalculator.ParseAmount(info.GrantedBalance);
        var toppedUp = UsageCalculator.ParseAmount(info.ToppedUpBalance);
        var sample = new UsageSample(DateTimeOffset.UtcNow, info.Currency, total, granted, toppedUp);

        if (_samples.Count > 0 && IsSame(_samples[^1], sample))
        {
            return;
        }

        _samples.Add(sample);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-60);
        _samples.RemoveAll(existing => existing.Time < cutoff);
        UsageHistory.Save(_path, _samples);
        Recompute();
    }

    private static bool IsSame(UsageSample left, UsageSample right) =>
        left.Currency == right.Currency
        && left.Total == right.Total
        && left.Granted == right.Granted
        && left.ToppedUp == right.ToppedUp;

    private void Recompute()
    {
        _summary = UsageCalculator.Compute(_samples, DateTimeOffset.Now);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
    }

    public string FormatAmount(decimal amount) =>
        UsageCalculator.AmountText(_summary.Currency, amount);
}
