using System.ComponentModel;
using System.Windows.Threading;
using DeepSeekStatus.Support;
using Microsoft.Win32;

namespace DeepSeekStatus.Models;

public sealed class PricingStore : INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer;
    private PricingSnapshot _snapshot;
    private PricePeriod? _preview;
    private bool _showsCountdown;
    private bool _launchAtLogin;
    private string? _launchAtLoginMessage;

    public PricingStore()
    {
        _snapshot = new PricingSnapshot(DeepSeekPricing.BeijingNow());
        _showsCountdown = UserSettings.GetBool("ShowsCountdown");
        _launchAtLogin = Support.LaunchAtLogin.IsEnabled;
        _timer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _timer.Tick += (_, _) => Refresh();

        SystemEvents.TimeChanged += (_, _) => Refresh();
        SystemEvents.PowerModeChanged += (_, args) =>
        {
            if (args.Mode == PowerModes.Resume)
            {
                Refresh();
            }
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? Tick;

    public PricingSnapshot Snapshot => _snapshot;

    public PricePeriod Period => _preview ?? _snapshot.Period;

    public bool IsPreviewing => _preview is not null;

    public PricePeriod? PreviewPeriod
    {
        get => _preview;
        set
        {
            if (_preview == value)
            {
                return;
            }

            _preview = value;
            OnPropertyChanged(nameof(PreviewPeriod));
            OnPropertyChanged(nameof(Period));
            OnPropertyChanged(nameof(IsPreviewing));
        }
    }

    public bool ShowsCountdown
    {
        get => _showsCountdown;
        set
        {
            if (_showsCountdown == value)
            {
                return;
            }

            _showsCountdown = value;
            UserSettings.SetBool("ShowsCountdown", value);
            OnPropertyChanged(nameof(ShowsCountdown));
        }
    }

    public bool LaunchAtLogin
    {
        get => _launchAtLogin;
        set
        {
            if (_launchAtLogin == value)
            {
                return;
            }

            var error = Support.LaunchAtLogin.Set(value);
            if (error is not null)
            {
                LaunchAtLoginMessage = string.Format(Strings.Get("launchAtLogin.failed"), error);
                _launchAtLogin = Support.LaunchAtLogin.IsEnabled;
                OnPropertyChanged(nameof(LaunchAtLogin));
                return;
            }

            _launchAtLogin = Support.LaunchAtLogin.IsEnabled;
            LaunchAtLoginMessage = null;
            OnPropertyChanged(nameof(LaunchAtLogin));
        }
    }

    public string? LaunchAtLoginMessage
    {
        get => _launchAtLoginMessage;
        private set
        {
            if (_launchAtLoginMessage == value)
            {
                return;
            }

            _launchAtLoginMessage = value;
            OnPropertyChanged(nameof(LaunchAtLoginMessage));
        }
    }

    public void Start()
    {
        _timer.Start();
        Refresh();
    }

    public void Refresh()
    {
        var next = new PricingSnapshot(DeepSeekPricing.BeijingNow());
        if (!next.Equals(_snapshot))
        {
            _snapshot = next;
            OnPropertyChanged(nameof(Snapshot));
            OnPropertyChanged(nameof(Period));
        }

        Tick?.Invoke();
    }

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public static class UserSettings
{
    private const string KeyPath = @"Software\DeepSeekStatus";

    public static bool GetBool(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(name) is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }

    public static void SetBool(string name, bool value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
            key?.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
        }
        catch
        {
        }
    }
}
