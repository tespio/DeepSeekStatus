using System.Threading;
using System.Windows;
using DeepSeekStatus.Models;
using DeepSeekStatus.Support;
using DeepSeekStatus.Views;
using Microsoft.Win32;

namespace DeepSeekStatus;

public partial class App : Application
{
    private PricingStore _store = null!;
    private TrayIcon _tray = null!;
    private PanelWindow _panel = null!;
    private CountdownOverlay _overlay = null!;
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private Thread? _listener;
    private string? _exportPanelDirectory;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--selftest"))
        {
            Environment.ExitCode = SelfTest.Run();
            Shutdown();
            return;
        }

        var exportIndex = Array.IndexOf(e.Args, "--export-icons");
        if (exportIndex >= 0 && exportIndex + 1 < e.Args.Length)
        {
            Environment.ExitCode = IconExporter.Export(e.Args[exportIndex + 1]);
            Shutdown();
            return;
        }

        var exportPanelIndex = Array.IndexOf(e.Args, "--export-panel");
        if (exportPanelIndex >= 0 && exportPanelIndex + 1 < e.Args.Length)
        {
            _exportPanelDirectory = e.Args[exportPanelIndex + 1];
        }

        Theme.Init();

        _mutex = new Mutex(true, @"Local\DeepSeekStatus.Singleton", out var createdNew);
        if (!createdNew)
        {
            try
            {
                using var handle = EventWaitHandle.OpenExisting(@"Local\DeepSeekStatus.ShowPanel");
                handle.Set();
            }
            catch
            {
            }

            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\DeepSeekStatus.ShowPanel");
        _listener = new Thread(ListenForShowRequests)
        {
            IsBackground = true,
            Name = "DeepSeekStatus.SingleInstance",
        };
        _listener.Start();

        _store = new PricingStore();
        ApplyLaunchOverrides();
        _panel = new PanelWindow(_store, Quit);
        _overlay = new CountdownOverlay(_store, TogglePanel);
        _tray = new TrayIcon(_store, TogglePanel, Quit);
        _store.Tick += OnTick;
        Theme.Changed += OnThemeChanged;
        SystemEvents.DisplaySettingsChanged += (_, _) =>
        {
            _tray.Update();
            _overlay.Reposition(force: true);
            if (_panel.IsVisible)
            {
                _panel.Refresh();
            }
        };

        _store.Start();

        if (_exportPanelDirectory is not null)
        {
            _panel.ShowPanel();
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(900),
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                var name = $"panel-{_store.Period.Key()}";
                Environment.ExitCode = PanelExporter.Export(_panel.RootBorder, _exportPanelDirectory, name);
                Shutdown();
            };
            timer.Start();
            return;
        }

        if (Environment.GetEnvironmentVariable("DEEPSEEK_STATUS_SHOW_PANEL") == "1")
        {
            Dispatcher.BeginInvoke(() => ShowPanel());
        }
    }

    private void ApplyLaunchOverrides()
    {
        switch (Environment.GetEnvironmentVariable("DEEPSEEK_STATUS_PREVIEW")?.ToLowerInvariant())
        {
            case "peak":
                _store.PreviewPeriod = PricePeriod.Peak;
                break;
            case "offpeak":
                _store.PreviewPeriod = PricePeriod.OffPeak;
                break;
        }

        if (Environment.GetEnvironmentVariable("DEEPSEEK_STATUS_COUNTDOWN") == "1")
        {
            _store.ShowsCountdown = true;
        }
    }

    public void TogglePanel()
    {
        if (_panel.IsVisible)
        {
            _panel.HidePanel();
            return;
        }

        if (_panel.RecentlyHidden)
        {
            return;
        }

        _panel.ShowPanel();
    }

    public void ShowPanel()
    {
        if (!_panel.IsVisible)
        {
            _panel.ShowPanel();
        }
    }

    private void ListenForShowRequests()
    {
        while (true)
        {
            _showEvent?.WaitOne();
            Dispatcher.BeginInvoke(ShowPanel);
        }
    }

    private void OnTick()
    {
        _tray.Update();
        _overlay.Update();
        if (_panel.IsVisible)
        {
            _panel.Refresh();
        }
    }

    private void OnThemeChanged()
    {
        _tray.Update();
        _panel.RefreshTheme();
        _overlay.Update();
    }

    private void Quit() => Shutdown();

    protected override void OnExit(ExitEventArgs e)
    {
        if (_store is not null)
        {
            _store.Tick -= OnTick;
        }

        _tray?.Dispose();
        _overlay?.Close();
        _showEvent?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
