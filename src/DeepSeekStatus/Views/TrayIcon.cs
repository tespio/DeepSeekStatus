using System.Diagnostics;
using DeepSeekStatus.Models;
using DeepSeekStatus.Support;

namespace DeepSeekStatus.Views;

public sealed class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.ContextMenuStrip _menu;
    private readonly System.Windows.Forms.ToolStripMenuItem _previewPeak;
    private readonly System.Windows.Forms.ToolStripMenuItem _previewOffPeak;
    private readonly System.Windows.Forms.ToolStripMenuItem _previewLive;
    private readonly System.Windows.Forms.ToolStripMenuItem _countdownItem;
    private readonly System.Windows.Forms.ToolStripMenuItem _launchItem;
    private readonly PricingStore _store;
    private readonly BalanceStore _balance;
    private readonly Action _togglePanel;
    private readonly Action _quit;

    private System.Drawing.Icon? _icon;
    private string _iconKey = string.Empty;
    private string _tooltip = string.Empty;

    public TrayIcon(PricingStore store, BalanceStore balance, Action togglePanel, Action quit)
    {
        _store = store;
        _balance = balance;
        _togglePanel = togglePanel;
        _quit = quit;

        _previewPeak = new System.Windows.Forms.ToolStripMenuItem(
            Strings.Get("menu.preview.peak"), null, (_, _) => SetPreview(PricePeriod.Peak));
        _previewOffPeak = new System.Windows.Forms.ToolStripMenuItem(
            Strings.Get("menu.preview.offPeak"), null, (_, _) => SetPreview(PricePeriod.OffPeak));
        _previewLive = new System.Windows.Forms.ToolStripMenuItem(
            Strings.Get("menu.preview.live"), null, (_, _) => SetPreview(null));
        _countdownItem = new System.Windows.Forms.ToolStripMenuItem(
            Strings.Get("menu.showCountdown"), null, (_, _) => _store.ShowsCountdown = !_store.ShowsCountdown);
        _launchItem = new System.Windows.Forms.ToolStripMenuItem(
            Strings.Get("menu.launchAtLogin"), null, (_, _) => _store.LaunchAtLogin = !_store.LaunchAtLogin);

        var menu = new System.Windows.Forms.ContextMenuStrip();
        var projectItem = new System.Windows.Forms.ToolStripMenuItem(
            Strings.Get("menu.projectPage"), null, (_, _) => OpenProjectPage());
        var quitItem = new System.Windows.Forms.ToolStripMenuItem(
            Strings.Get("menu.quit"), null, (_, _) => _quit());

        menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[]
        {
            _previewPeak,
            _previewOffPeak,
            _previewLive,
            new System.Windows.Forms.ToolStripSeparator(),
            _countdownItem,
            _launchItem,
            new System.Windows.Forms.ToolStripSeparator(),
            projectItem,
            new System.Windows.Forms.ToolStripSeparator(),
            quitItem,
        });
        menu.Opening += (_, _) => SyncMenu();

        _menu = menu;
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Visible = true,
            Text = "DeepSeek Status",
        };
        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Left)
            {
                _togglePanel();
            }
        };
    }

    public void Update()
    {
        var size = Math.Clamp(System.Windows.Forms.SystemInformation.SmallIconSize.Width, 12, 64);
        var dark = Theme.IsTaskbarDark;
        var key = $"{_store.Period.Key()}|{dark}|{size}";
        if (key != _iconKey)
        {
            _iconKey = key;
            var icon = TrayIconRenderer.RenderTray(_store.Period, dark, size);
            _notifyIcon.Icon = icon;
            _icon?.Dispose();
            _icon = icon;
        }

        var tooltip = BuildTooltip();
        if (tooltip != _tooltip)
        {
            _tooltip = tooltip;
            _notifyIcon.Text = tooltip;
        }
    }

    private string BuildTooltip()
    {
        var period = _store.Period;
        var multiplier = period.PriceMultiplier().ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        var balance = _balance.AmountText;
        var countdown = _store.ShowsCountdown
            ? PricingFormatter.CompactCountdown(_store.Snapshot.SecondsUntilTransition)
            : null;

        string Compose(string title, bool withCountdown)
        {
            var text = $"DeepSeek Status · {title} · ×{multiplier}";
            if (withCountdown && countdown is not null)
            {
                text += $" · {countdown}";
            }

            if (balance is not null)
            {
                text += $" · {balance}";
            }

            return text;
        }

        var full = Compose(period.Title(), true);
        if (full.Length <= 62)
        {
            return full;
        }

        var compact = Compose(period.ShortTitle(), true);
        if (compact.Length <= 62)
        {
            return compact;
        }

        var noCountdown = Compose(period.ShortTitle(), false);
        return noCountdown.Length <= 62 ? noCountdown : noCountdown[..62];
    }

    private void SyncMenu()
    {
        _previewPeak.Checked = _store.PreviewPeriod == PricePeriod.Peak;
        _previewOffPeak.Checked = _store.PreviewPeriod == PricePeriod.OffPeak;
        _previewLive.Checked = _store.PreviewPeriod is null;
        _countdownItem.Checked = _store.ShowsCountdown;
        _launchItem.Checked = _store.LaunchAtLogin;
    }

    private void SetPreview(PricePeriod? period) => _store.PreviewPeriod = period;

    private static void OpenProjectPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppInfo.ProjectUrl) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon?.Dispose();
        _menu.Dispose();
    }
}
