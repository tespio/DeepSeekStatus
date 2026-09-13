using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DeepSeekStatus.Models;
using DeepSeekStatus.Support;
using WinForms = System.Windows.Forms;

namespace DeepSeekStatus.Views;

public partial class PanelWindow : Window
{
    private readonly PricingStore _store;
    private readonly BalanceStore _balance;
    private readonly Action _quit;
    private bool _syncing;
    private bool _syncingBalanceKey;
    private bool _repositioning;
    private DateTime _hiddenAt = DateTime.MinValue;
    private int _lastHour = -1;

    public PanelWindow(PricingStore store, BalanceStore balance, Action quit)
    {
        _store = store;
        _balance = balance;
        _quit = quit;
        InitializeComponent();
        ApplyStaticTexts();
        WireEvents();

        _store.PropertyChanged += (_, _) =>
        {
            if (IsVisible)
            {
                Refresh();
            }
        };
        _balance.PropertyChanged += (_, _) =>
        {
            if (IsVisible)
            {
                RefreshBalance();
            }
        };
        Theme.Changed += RefreshTheme;
        RootBorder.SizeChanged += (_, _) => UpdateContentClip();
        Scroller.SizeChanged += (_, _) => UpdateContentClip();
        Deactivated += (_, _) =>
        {
            if (IsVisible)
            {
                HidePanel();
            }
        };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void UpdateContentClip()
    {
        if (Scroller.ActualWidth <= 0 || Scroller.ActualHeight <= 0)
        {
            Scroller.Clip = null;
            return;
        }

        var radius = Math.Max(0, RootBorder.CornerRadius.TopLeft - RootBorder.BorderThickness.Left);
        Scroller.Clip = new RectangleGeometry(
            new Rect(0, 0, Scroller.ActualWidth, Scroller.ActualHeight), radius, radius);
    }

    public bool RecentlyHidden => (DateTime.UtcNow - _hiddenAt).TotalMilliseconds < 350;

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (!IsVisible || _repositioning)
        {
            return;
        }

        _repositioning = true;
        try
        {
            PositionNearTray();
        }
        finally
        {
            _repositioning = false;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE).ToInt64();
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE,
                                       new IntPtr(style | NativeMethods.WS_EX_TOOLWINDOW));
    }

    public void ShowPanel()
    {
        _store.Refresh();
        Refresh();
        RefreshBalance();
        Show();
        UpdateLayout();
        UpdateContentClip();
        PositionNearTray();
        Activate();
    }

    public void HidePanel()
    {
        _hiddenAt = DateTime.UtcNow;
        Hide();
    }

    public void RefreshTheme()
    {
        OffPeakSwatch.Background = Theme.Brush(Color.FromArgb(0x2E, Theme.TextSecondary.R, Theme.TextSecondary.G, Theme.TextSecondary.B));
        OffPeakLegendSwatch.Background = OffPeakSwatch.Background;
        WeekGrid.InvalidateVisual();
        Refresh();
        RefreshBalance();
    }

    public void Refresh()
    {
        var snapshot = _store.Snapshot;
        var period = _store.Period;
        var accent = WhaleTheme.Accent(period);

        Aquarium.Period = period;
        PeriodTitle.Text = period.Title();
        Multiplier.Text = "×" + period.PriceMultiplier().ToString("0.0", CultureInfo.InvariantCulture);
        Multiplier.Foreground = Theme.Brush(accent);
        PeriodSummary.Text = period.Summary();

        RateText.Text = period.PriceText();
        RateText.Foreground = Theme.Brush(accent);
        RateBar.Value = period.PriceMultiplier();
        RateBar.Accent = Theme.Brush(accent);

        BadgeDot.Fill = Theme.Brush(accent);
        BadgePeriod.Text = period.Title();
        BadgeTime.Text = PricingFormatter.PreciseTime(snapshot.Now);

        CountdownTitle.Text = string.Format(Strings.Get("popover.countdown.title"), snapshot.NextPeriod.Title());
        CountdownValue.Text = PricingFormatter.Duration(snapshot.SecondsUntilTransition);
        CountdownDetail.Text = string.Format(Strings.Get("popover.countdown.detail"),
                                             PricingFormatter.TransitionDescription(snapshot.NextTransition, snapshot.Now),
                                             snapshot.NextPeriod.Title());
        IntervalBar.Value = snapshot.IntervalProgress;
        IntervalBar.Accent = Theme.Brush(accent);

        PreviewBanner.Visibility = _store.IsPreviewing ? Visibility.Visible : Visibility.Collapsed;
        PreviewBanner.Background = Theme.Brush(Color.FromArgb(0x29, accent.R, accent.G, accent.B));
        if (_store.IsPreviewing)
        {
            PreviewBannerText.Text = string.Format(Strings.Get("popover.preview.banner"), period.Title());
        }

        Footnote.Text = BuildFootnote();

        _syncing = true;
        CountdownToggle.IsChecked = _store.ShowsCountdown;
        LaunchToggle.IsChecked = _store.LaunchAtLogin;
        SegmentAuto.IsChecked = _store.PreviewPeriod is null;
        SegmentPeak.IsChecked = _store.PreviewPeriod == PricePeriod.Peak;
        SegmentOffPeak.IsChecked = _store.PreviewPeriod == PricePeriod.OffPeak;
        _syncing = false;

        LaunchMessage.Text = _store.LaunchAtLoginMessage ?? string.Empty;
        LaunchMessage.Visibility = _store.LaunchAtLoginMessage is null ? Visibility.Collapsed : Visibility.Visible;

        if (snapshot.Now.Hour != _lastHour)
        {
            _lastHour = snapshot.Now.Hour;
            WeekGrid.InvalidateVisual();
        }
    }

    private void RefreshBalance()
    {
        BalanceRefreshButton.Content = _balance.IsRefreshing
            ? Strings.Get("balance.loading")
            : Strings.Get("balance.refresh");
        BalanceRefreshButton.IsEnabled = !_balance.IsRefreshing;
        BalanceUpdatedLabel.Text = _balance.LastRefreshed is { } updated
            ? string.Format(Strings.Get("balance.updated"),
                            updated.ToLocalTime().ToString("t", Strings.Culture))
            : string.Empty;

        var editing = _balance.IsEditingKey;
        BalanceEditorPanel.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        BalanceHeaderActions.Visibility = !editing && _balance.HasKey ? Visibility.Visible : Visibility.Collapsed;
        BalanceEnterKeyButton.Visibility = !editing && !_balance.HasKey ? Visibility.Visible : Visibility.Collapsed;
        BalanceRemoveButton.Visibility = _balance.HasKey ? Visibility.Visible : Visibility.Collapsed;
        BalanceKeyErrorText.Text = _balance.KeyError ?? string.Empty;
        BalanceKeyErrorText.Visibility = _balance.KeyError is null ? Visibility.Collapsed : Visibility.Visible;

        if (editing)
        {
            _syncingBalanceKey = true;
            if (BalanceKeyBox.Password != _balance.KeyDraft)
            {
                BalanceKeyBox.Password = _balance.KeyDraft;
            }

            _syncingBalanceKey = false;
            BalanceKeyWatermark.Visibility = BalanceKeyBox.Password.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        var showNoKey = !editing && _balance.State == BalanceStore.BalanceState.NoKey;
        var showLoading = !editing && _balance.State == BalanceStore.BalanceState.Loading;
        var showLoaded = !editing && _balance.State == BalanceStore.BalanceState.Loaded;
        var showFailed = !editing && _balance.State == BalanceStore.BalanceState.Failed;

        BalanceNoKeyHint.Visibility = showNoKey ? Visibility.Visible : Visibility.Collapsed;
        BalanceLoadingText.Visibility = showLoading ? Visibility.Visible : Visibility.Collapsed;
        BalanceRowsPanel.Visibility = showLoaded ? Visibility.Visible : Visibility.Collapsed;
        BalanceErrorPanel.Visibility = showFailed ? Visibility.Visible : Visibility.Collapsed;

        if (showLoaded)
        {
            BuildBalanceRows(_balance.Balance);
        }

        if (showFailed)
        {
            BalanceErrorText.Text = _balance.ErrorMessage ?? string.Empty;
            BuildErrorLinks(_balance.ErrorSuggestsReplacingKey);
        }
    }

    private void BuildBalanceRows(DeepSeekBalance? balance)
    {
        BalanceRowsPanel.Children.Clear();
        if (balance is null)
        {
            return;
        }

        if (balance.BalanceInfos.Count == 0)
        {
            BalanceRowsPanel.Children.Add(MakeText(Strings.Get("balance.empty"),
                                                    14, FontWeights.Normal, Theme.TextSecondary));
        }

        foreach (var info in balance.BalanceInfos)
        {
            var amount = MakeText(info.Amount(info.TotalBalance), 22.7, FontWeights.Bold, Theme.TextPrimary);
            amount.FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
            amount.Margin = new Thickness(0, 4, 0, 0);
            Typography.SetNumeralAlignment(amount, FontNumeralAlignment.Tabular);
            BalanceRowsPanel.Children.Add(amount);

            var breakdown = MakeText(
                $"{string.Format(Strings.Get("balance.granted"), info.Amount(info.GrantedBalance))} · " +
                $"{string.Format(Strings.Get("balance.toppedUp"), info.Amount(info.ToppedUpBalance))}",
                13.3, FontWeights.Normal, Theme.TextSecondary);
            breakdown.Margin = new Thickness(0, 1, 0, 0);
            Typography.SetNumeralAlignment(breakdown, FontNumeralAlignment.Tabular);
            BalanceRowsPanel.Children.Add(breakdown);
        }

        if (!balance.IsAvailable)
        {
            var warning = MakeText(Strings.Get("balance.unavailable"),
                                   13.3, FontWeights.Normal, WhaleTheme.PeakAccent);
            warning.Margin = new Thickness(0, 5, 0, 0);
            warning.TextWrapping = TextWrapping.Wrap;
            BalanceRowsPanel.Children.Add(warning);
        }
    }

    private void BuildErrorLinks(bool suggestsReplacingKey)
    {
        BalanceErrorLinks.Children.Clear();
        var retry = MakeLink(Strings.Get("balance.retry"), () => _balance.Refresh());
        var change = MakeLink(Strings.Get("balance.key.change"), () => _balance.BeginEditingKey());
        if (suggestsReplacingKey)
        {
            BalanceErrorLinks.Children.Add(change);
            retry.Margin = new Thickness(14, 0, 0, 0);
            BalanceErrorLinks.Children.Add(retry);
            change.FontWeight = FontWeights.SemiBold;
        }
        else
        {
            BalanceErrorLinks.Children.Add(retry);
            change.Margin = new Thickness(14, 0, 0, 0);
            BalanceErrorLinks.Children.Add(change);
            retry.FontWeight = FontWeights.SemiBold;
        }
    }

    private static TextBlock MakeText(string text, double fontSize, FontWeight weight, Color color)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = weight,
            Foreground = Theme.Brush(color),
        };
    }

    private Button MakeLink(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            Style = (Style)FindResource("LinkButton"),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void ApplyStaticTexts()
    {
        ClockLabel.Text = PricingFormatter.IsLocalZoneBeijing
            ? Strings.Get("aquarium.beijingTime")
            : Strings.Get("aquarium.localTime");
        CurrentRateLabel.Text = Strings.Get("popover.currentRate");
        ScheduleTitleLabel.Text = Strings.Get("popover.schedule.title");
        PeakRuleLabel.Text = PricePeriod.Peak.ShortTitle();
        OffPeakRuleLabel.Text = PricePeriod.OffPeak.ShortTitle();
        PeakRuleDetail.Text = Strings.Get("popover.schedule.peak.detail");
        OffPeakRuleDetail.Text = Strings.Get("popover.schedule.offPeak.detail");
        LegendPeak.Text = Strings.Get("schedule.legend.peak");
        LegendOffPeak.Text = Strings.Get("schedule.legend.offPeak");
        CountdownOptionLabel.Text = Strings.Get("popover.option.countdown");
        LaunchOptionLabel.Text = Strings.Get("popover.option.launchAtLogin");
        PreviewLabel.Text = Strings.Get("popover.preview.label");
        SegmentAuto.Content = Strings.Get("popover.preview.auto");
        SegmentPeak.Content = PricePeriod.Peak.ShortTitle();
        SegmentOffPeak.Content = PricePeriod.OffPeak.ShortTitle();
        PreviewResume.Content = Strings.Get("popover.preview.resume");
        QuitButton.Content = Strings.Get("popover.quit");
        VersionText.Text = AppInfo.DisplayName;
        OffPeakSwatch.Background = Theme.Brush(Color.FromArgb(0x2E, Theme.TextSecondary.R, Theme.TextSecondary.G, Theme.TextSecondary.B));
        OffPeakLegendSwatch.Background = OffPeakSwatch.Background;

        BalanceTitleLabel.Text = Strings.Get("balance.title");
        BalanceNoKeyHint.Text = Strings.Get("balance.noKey.hint");
        BalanceLoadingText.Text = Strings.Get("balance.loading");
        BalanceKeyHintText.Text = Strings.Get("balance.key.hint");
        BalanceKeyWatermark.Text = Strings.Get("balance.key.placeholder");
        BalanceRemoveButton.Content = Strings.Get("balance.key.remove");
        BalanceCancelButton.Content = Strings.Get("balance.key.cancel");
        BalanceSaveButton.Content = Strings.Get("balance.key.save");
        BalanceChangeButton.Content = "\uE192";
        BalanceChangeButton.FontFamily = new FontFamily("Segoe MDL2 Assets");
        BalanceChangeButton.ToolTip = Strings.Get("balance.key.changeHelp");
        BalanceEnterKeyButton.Content = BuildEnterKeyContent();
    }

    private static StackPanel BuildEnterKeyContent()
    {
        var icon = new TextBlock
        {
            Text = "\uE192",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 13.3,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var text = new TextBlock
        {
            Text = Strings.Get("balance.enterKey"),
            Margin = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(icon);
        panel.Children.Add(text);
        return panel;
    }

    private void WireEvents()
    {
        CountdownToggle.Checked += (_, _) => SetCountdown(true);
        CountdownToggle.Unchecked += (_, _) => SetCountdown(false);
        LaunchToggle.Checked += (_, _) => SetLaunchAtLogin(true);
        LaunchToggle.Unchecked += (_, _) => SetLaunchAtLogin(false);
        SegmentAuto.Checked += (_, _) => SetPreview(null);
        SegmentPeak.Checked += (_, _) => SetPreview(PricePeriod.Peak);
        SegmentOffPeak.Checked += (_, _) => SetPreview(PricePeriod.OffPeak);
        PreviewResume.Click += (_, _) => _store.PreviewPeriod = null;
        QuitButton.Click += (_, _) => _quit();

        BalanceRefreshButton.Click += (_, _) => _balance.Refresh();
        BalanceEnterKeyButton.Click += (_, _) => _balance.BeginEditingKey();
        BalanceChangeButton.Click += (_, _) => _balance.BeginEditingKey();
        BalanceSaveButton.Click += (_, _) => _balance.SaveKey();
        BalanceCancelButton.Click += (_, _) => _balance.CancelEditingKey();
        BalanceRemoveButton.Click += (_, _) => _balance.RemoveKey();
        BalanceKeyBox.PasswordChanged += (_, _) =>
        {
            if (_syncingBalanceKey)
            {
                return;
            }

            _balance.KeyDraft = BalanceKeyBox.Password;
            BalanceKeyWatermark.Visibility = BalanceKeyBox.Password.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        };
        BalanceKeyBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                _balance.SaveKey();
                e.Handled = true;
            }
        };
    }

    private void SetCountdown(bool value)
    {
        if (!_syncing)
        {
            _store.ShowsCountdown = value;
        }
    }

    private void SetLaunchAtLogin(bool value)
    {
        if (!_syncing)
        {
            _store.LaunchAtLogin = value;
        }
    }

    private void SetPreview(PricePeriod? period)
    {
        if (!_syncing)
        {
            _store.PreviewPeriod = period;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HidePanel();
            e.Handled = true;
        }
    }

    private static string BuildFootnote()
    {
        if (PricingFormatter.IsLocalZoneBeijing)
        {
            return Strings.Get("popover.footnote.beijing");
        }

        return string.Format(Strings.Get("popover.footnote.localZone"), PricingFormatter.LocalOffsetText());
    }

    private void PositionNearTray()
    {
        var screen = WinForms.Screen.FromPoint(WinForms.Cursor.Position);
        var area = screen.WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        Scroller.MaxHeight = Math.Max(240, area.Height / dpi.DpiScaleY - 24);
        if (!IsMeasureValid || !IsArrangeValid)
        {
            UpdateLayout();
        }

        var width = ActualWidth * dpi.DpiScaleX;
        var height = ActualHeight * dpi.DpiScaleY;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        double x;
        if (NativeMethods.GetTrayNotifyRect() is { } tray && Math.Abs(tray.Bottom - area.Bottom) <= 2)
        {
            x = tray.Right - width - 8 * dpi.DpiScaleX;
        }
        else
        {
            x = area.Right - width - 8 * dpi.DpiScaleX;
        }

        var y = area.Bottom - height - 8 * dpi.DpiScaleY;
        x = Math.Clamp(x, area.Left + 8, Math.Max(area.Left + 8, area.Right - width - 8));

        NativeMethods.SetWindowPos(new WindowInteropHelper(this).Handle, NativeMethods.HWND_TOPMOST,
                                   (int)Math.Round(x), (int)Math.Round(y), 0, 0,
                                   NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }
}
