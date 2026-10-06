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
    private readonly UsageStore _usage;
    private readonly PricingCatalog _catalog = PricingCatalog.Load();
    private readonly Action _quit;
    private bool _syncing;
    private bool _syncingBalanceKey;
    private bool _syncingPricing;
    private bool _repositioning;
    private int _pricingModelIndex;
    private bool _pricingCollapsed;
    private bool _usageCollapsed;
    private bool _scheduleShowsCalendar = true;
    private bool _syncingSchedule;
    private bool _syncingTheme;
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Now);
    private DateOnly _lastCalendarDay = DateOnly.MinValue;
    private DateTime _hiddenAt = DateTime.MinValue;
    private int _lastHour = -1;

    public PanelWindow(PricingStore store, BalanceStore balance, UsageStore usage, Action quit)
    {
        _store = store;
        _balance = balance;
        _usage = usage;
        _quit = quit;
        InitializeComponent();
        _pricingModelIndex = Math.Clamp(UserSettings.GetInt("PricingModelIndex"),
                                        0, Math.Max(0, _catalog.Models.Count - 1));
        _pricingCollapsed = UserSettings.GetBool("PricingCollapsed");
        _usageCollapsed = UserSettings.GetBool("UsageCollapsed");
        ApplyStaticTexts();
        ApplyPricingCollapsed();
        ApplyUsageCollapsed();
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
        _usage.PropertyChanged += (_, _) =>
        {
            if (IsVisible)
            {
                RefreshUsage();
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

    public bool ExportMode { get; set; }

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

        var today = DateOnly.FromDateTime(DateTime.Now);
        _selectedDate = today;
        _lastCalendarDay = today;
        ScheduleCalendar.SetState(today, today);
        WeekGrid.ReferenceDate = today;
        ApplyScheduleMode(true);

        RefreshBalance();
        RefreshUsage();
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
        ScheduleCalendar.InvalidateVisual();
        Refresh();
        RefreshBalance();
        RefreshUsage();
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
        var dayInfo = snapshot.DayInfo;
        var holidayLike = dayInfo.Kind is PricingDayKind.PublicHoliday or PricingDayKind.AlternateWorkdayWeekend;
        HolidayNotice.Text = holidayLike ? dayInfo.LocalizedDetail() : string.Empty;
        HolidayNotice.Visibility = holidayLike ? Visibility.Visible : Visibility.Collapsed;

        var localToday = DateOnly.FromDateTime(snapshot.Now.LocalDateTime);
        if (localToday != _lastCalendarDay)
        {
            _lastCalendarDay = localToday;
            ScheduleCalendar.SetState(localToday, _selectedDate);
            UpdateCalendarHeader();
        }

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

        _syncingPricing = true;
        PricingFlashSegment.IsChecked = _pricingModelIndex == 0;
        PricingProSegment.IsChecked = _pricingModelIndex == 1;
        _syncingPricing = false;
        RenderPricing();

        _syncingTheme = true;
        ThemeSystemSegment.IsChecked = Theme.Mode == AppTheme.System;
        ThemeLightSegment.IsChecked = Theme.Mode == AppTheme.Light;
        ThemeDarkSegment.IsChecked = Theme.Mode == AppTheme.Dark;
        _syncingTheme = false;

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
        ScheduleCalendarSegment.Content = Strings.Get("calendar.mode.calendar");
        ScheduleWeekSegment.Content = Strings.Get("calendar.mode.week");
        CalendarTodayButton.Content = Strings.Get("calendar.today");
        CalendarPrevButton.ToolTip = Strings.Get("calendar.previousMonth");
        CalendarNextButton.ToolTip = Strings.Get("calendar.nextMonth");
        CountdownOptionLabel.Text = Strings.Get("popover.option.countdown");
        LaunchOptionLabel.Text = Strings.Get("popover.option.launchAtLogin");
        PreviewLabel.Text = Strings.Get("popover.preview.label");
        SegmentAuto.Content = Strings.Get("popover.preview.auto");
        SegmentPeak.Content = PricePeriod.Peak.ShortTitle();
        SegmentOffPeak.Content = PricePeriod.OffPeak.ShortTitle();
        ThemeOptionLabel.Text = Strings.Get("popover.option.theme");
        ThemeSystemSegment.Content = Strings.Get("theme.system");
        ThemeLightSegment.Content = Strings.Get("theme.light");
        ThemeDarkSegment.Content = Strings.Get("theme.dark");
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

        PricingTitleLabel.Text = Strings.Get("pricing.title");
        PricingUnitLabel.Text = Strings.Get("pricing.unit");
        PricingPeakHeader.Text = Strings.Get("pricing.peak");
        PricingOffPeakHeader.Text = Strings.Get("pricing.offPeak");
        PricingCacheHitLabel.Text = Strings.Get("pricing.inputCacheHit");
        PricingCacheMissLabel.Text = Strings.Get("pricing.inputCacheMiss");
        PricingOutputLabel.Text = Strings.Get("pricing.output");
        PricingConcurrencyLabel.Text = Strings.Get("pricing.concurrency");
        PricingFootnote.Text = string.Format(Strings.Get("pricing.footnote"), _catalog.Updated);

        if (_catalog.Models.Count > 0)
        {
            PricingFlashSegment.Content = _catalog.Models[0].Name;
        }

        if (_catalog.Models.Count > 1)
        {
            PricingProSegment.Content = _catalog.Models[1].Name;
        }
        else
        {
            PricingProSegment.Visibility = Visibility.Collapsed;
        }

        UsageTitleLabel.Text = Strings.Get("usage.title");
        UsageWindowLabel.Text = Strings.Get("usage.window");
        UsageFootnote.Text = Strings.Get("usage.footnote");
    }

    private void ApplyPricingCollapsed()
    {
        PricingBody.Visibility = _pricingCollapsed ? Visibility.Collapsed : Visibility.Visible;
        PricingToggleButton.Content = _pricingCollapsed ? "\uE76C" : "\uE70D";
    }

    private void ApplyUsageCollapsed()
    {
        UsageBody.Visibility = _usageCollapsed ? Visibility.Collapsed : Visibility.Visible;
        UsageToggleButton.Content = _usageCollapsed ? "\uE76C" : "\uE70D";
    }

    private void RefreshUsage()
    {
        var summary = _usage.Summary;
        var hasData = _usage.HasData && summary.Currency.Length > 0;
        UsageHeaderRow.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
        UsageBody.Visibility = hasData && !_usageCollapsed ? Visibility.Visible : Visibility.Collapsed;
        if (!hasData)
        {
            return;
        }

        UsageTotalValue.Text = _usage.FormatAmount(summary.Spend30Days);
        UsageBreakdownLabel.Text =
            $"{string.Format(Strings.Get("usage.today"), _usage.FormatAmount(summary.SpendToday))} · " +
            $"{string.Format(Strings.Get("usage.week"), _usage.FormatAmount(summary.Spend7Days))}";
        UsageChart.SetData(summary.DailyTotals, summary.MaxDaily,
                           Theme.Brush(WhaleTheme.BrandBlue), Theme.Brush(Theme.ControlTrack));

        var model = _catalog.Models.Count > 0
            ? _catalog.Models[Math.Clamp(_pricingModelIndex, 0, _catalog.Models.Count - 1)]
            : null;
        var estimate = model is null
            ? string.Empty
            : UsageCalculator.TokenEstimateText(summary.Spend30Days, model.InputCacheMiss.OffPeak);
        UsageEstimateLabel.Text = estimate.Length == 0
            ? string.Empty
            : string.Format(Strings.Get("usage.estimate"), estimate, model!.Name);
        UsageEstimateLabel.ToolTip = Strings.Get("usage.estimateHelp");
        UsageEstimateLabel.Visibility = estimate.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetPricingModel(int index)
    {
        if (_syncingPricing || index == _pricingModelIndex)
        {
            return;
        }

        _pricingModelIndex = index;
        UserSettings.SetInt("PricingModelIndex", index);
        RenderPricing();
        RefreshUsage();
    }

    private void RenderPricing()
    {
        if (_catalog.Models.Count == 0)
        {
            return;
        }

        var model = _catalog.Models[Math.Clamp(_pricingModelIndex, 0, _catalog.Models.Count - 1)];
        PricingCacheHitPeak.Text = "$" + model.InputCacheHit.Peak;
        PricingCacheHitOffPeak.Text = "$" + model.InputCacheHit.OffPeak;
        PricingCacheMissPeak.Text = "$" + model.InputCacheMiss.Peak;
        PricingCacheMissOffPeak.Text = "$" + model.InputCacheMiss.OffPeak;
        PricingOutputPeak.Text = "$" + model.Output.Peak;
        PricingOutputOffPeak.Text = "$" + model.Output.OffPeak;
        PricingConcurrencyValue.Text = model.Concurrency;

        var isPeak = _store.Period == PricePeriod.Peak;
        var accent = Theme.Brush(WhaleTheme.Accent(_store.Period));
        var dim = Theme.Brush(Theme.TextTertiary);
        var strong = Theme.Brush(Theme.TextPrimary);

        PricingPeakHeader.Foreground = isPeak ? accent : dim;
        PricingOffPeakHeader.Foreground = isPeak ? dim : accent;
        PricingCacheHitPeak.Foreground = isPeak ? strong : dim;
        PricingCacheHitOffPeak.Foreground = isPeak ? dim : strong;
        PricingCacheMissPeak.Foreground = isPeak ? strong : dim;
        PricingCacheMissOffPeak.Foreground = isPeak ? dim : strong;
        PricingOutputPeak.Foreground = isPeak ? strong : dim;
        PricingOutputOffPeak.Foreground = isPeak ? dim : strong;
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

        PricingFlashSegment.Checked += (_, _) => SetPricingModel(0);
        PricingProSegment.Checked += (_, _) => SetPricingModel(1);
        PricingToggleButton.Click += (_, _) =>
        {
            _pricingCollapsed = !_pricingCollapsed;
            UserSettings.SetBool("PricingCollapsed", _pricingCollapsed);
            ApplyPricingCollapsed();
        };
        UsageToggleButton.Click += (_, _) =>
        {
            _usageCollapsed = !_usageCollapsed;
            UserSettings.SetBool("UsageCollapsed", _usageCollapsed);
            ApplyUsageCollapsed();
            RefreshUsage();
        };

        ScheduleCalendarSegment.Checked += (_, _) => SetScheduleMode(true);
        ScheduleWeekSegment.Checked += (_, _) => SetScheduleMode(false);
        CalendarPrevButton.Click += (_, _) =>
        {
            ScheduleCalendar.MoveMonth(-1);
            UpdateCalendarHeader();
        };
        CalendarNextButton.Click += (_, _) =>
        {
            ScheduleCalendar.MoveMonth(1);
            UpdateCalendarHeader();
        };
        CalendarTodayButton.Click += (_, _) =>
        {
            ScheduleCalendar.GoToday();
            UpdateCalendarHeader();
        };
        ScheduleCalendar.SelectedDateChanged += date =>
        {
            _selectedDate = date;
            WeekGrid.ReferenceDate = date;
        };

        ThemeSystemSegment.Checked += (_, _) => SetThemeMode(AppTheme.System);
        ThemeLightSegment.Checked += (_, _) => SetThemeMode(AppTheme.Light);
        ThemeDarkSegment.Checked += (_, _) => SetThemeMode(AppTheme.Dark);
    }

    private void SetThemeMode(AppTheme mode)
    {
        if (!_syncingTheme)
        {
            Theme.SetMode(mode);
        }
    }

    private void SetScheduleMode(bool calendar)
    {
        if (_syncingSchedule)
        {
            return;
        }

        ApplyScheduleMode(calendar);
    }

    private void ApplyScheduleMode(bool calendar)
    {
        _scheduleShowsCalendar = calendar;
        CalendarHeaderRow.Visibility = calendar ? Visibility.Visible : Visibility.Collapsed;
        ScheduleCalendar.Visibility = calendar ? Visibility.Visible : Visibility.Collapsed;
        WeekPanel.Visibility = calendar ? Visibility.Collapsed : Visibility.Visible;
        _syncingSchedule = true;
        ScheduleCalendarSegment.IsChecked = calendar;
        ScheduleWeekSegment.IsChecked = !calendar;
        _syncingSchedule = false;
        UpdateCalendarHeader();
    }

    private void UpdateCalendarHeader()
    {
        CalendarMonthLabel.Text = PricingFormatter.MonthYear(ScheduleCalendar.DisplayedMonth);
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
        if (ExportMode)
        {
            return;
        }

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
