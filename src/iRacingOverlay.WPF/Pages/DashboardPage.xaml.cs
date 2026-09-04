using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Services;

namespace iRacingOverlay.WPF.Pages;

public partial class DashboardPage : UserControl, IDisposable
{
    private readonly WidgetManager _widgetManager;
    private readonly ITelemetryService _telemetryService;
    private readonly SessionConfigService _sessionConfig;
    private readonly DispatcherTimer _refreshTimer;

    // Performance monitoring
    private DateTime _lastCpuCheck = DateTime.UtcNow;
    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private double _cpuPercent;
    private int _perfTickCounter;

    public DashboardPage(WidgetManager widgetManager, ITelemetryService telemetryService,
                         SessionConfigService sessionConfig)
    {
        InitializeComponent();
        _widgetManager = widgetManager;
        _telemetryService = telemetryService;
        _sessionConfig = sessionConfig;

        _telemetryService.StatusChanged += OnStatusChanged;
        _sessionConfig.SessionCategoryChanged += OnSessionChanged;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _refreshTimer.Tick += (_, _) => RefreshDashboard();

        // Only tick while the page is actually on screen. It used to run twice a
        // second for the life of the app — rebuilding a list and re-templating the
        // whole ItemsControl even while another page was showing or the window was
        // minimised to tray, which is exactly when the user is driving.
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue)
            {
                RefreshDashboard();
                _refreshTimer.Start();
            }
            else
            {
                _refreshTimer.Stop();
            }
        };

        RefreshDashboard();
        UpdateHotkeysDisplay();
        InitSessionPresetUI();
    }

    // ── Widget item model for the dashboard list ────────────────────

    public class WidgetItem
    {
        public string Icon { get; set; } = "";
        public string Name { get; set; } = "";
        public string Status { get; set; } = "Hidden";
        public bool IsActive { get; set; }
        public WidgetType Type { get; set; }
    }

    // ── Refresh ─────────────────────────────────────────────────────

    private void RefreshDashboard()
    {
        // Connection
        UpdateConnectionDisplay(_telemetryService.Status);

        // Update rate
        var rate = _telemetryService.UpdateRate;
        TxtUpdateRate.Text = rate > 0 ? $"{rate:F0} Hz" : "0 Hz";

        // Session
        UpdateSessionDisplay();

        // Widget list
        // Single source of truth for which widgets this build ships. This used to be
        // a separate #if DEBUG list, in parallel with WidgetManager.SupportedWidgetTypes
        // and hardcoded XAML visibility on the Widgets page — three lists that drifted.
        var items = new List<WidgetItem>();
        foreach (WidgetType wt in Enum.GetValues<WidgetType>())
        {
            if (!WidgetManager.SupportedWidgetTypes.Contains(wt)) continue;

            bool exists = _widgetManager.HasWidgetType(wt);
            bool visible = exists && _widgetManager.GetWidgetsByType(wt).Any(w => w.UserWantsVisible);
            items.Add(new WidgetItem
            {
                Icon = GetWidgetIcon(wt),
                Name = wt.GetDisplayName(),
                Status = visible ? "Shown" : exists ? "Hidden" : "Not added",
                IsActive = visible,
                Type = wt,
            });
        }
        WidgetList.ItemsSource = items;

        // Lock button label
        BtnLockAll.Content = AppSettings.Instance.LockWindows ? "🔓 Unlock All" : "🔒 Lock All";

        // Performance (update every ~2s = 4 ticks at 500ms)
        _perfTickCounter++;
        if (_perfTickCounter >= 4)
        {
            _perfTickCounter = 0;
            UpdatePerformanceDisplay();
        }
    }

    private static string GetWidgetIcon(WidgetType type) => type switch
    {
        WidgetType.MRTOne => "◉",
        WidgetType.TurnDisplay => "↩",
        WidgetType.FuelCalculator => "⛽",
        WidgetType.Relative => "↕",
        WidgetType.ProximityFeed => "⚡",
        WidgetType.Standings => "🏆",
        _ => "▪",
    };

    // ── Connection ──────────────────────────────────────────────────

    private void OnStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        // Fires on the SDK thread — must not block it.
        Dispatcher.BeginInvoke(() => UpdateConnectionDisplay(e.Status));
    }

    private void UpdateConnectionDisplay(ConnectionStatus status)
    {
        TxtConnectionStatus.Text = status switch
        {
            ConnectionStatus.Connected => "Connected",
            ConnectionStatus.Connecting => "Connecting...",
            _ => "Disconnected"
        };

        // Same theme tokens the status bar uses, rather than a second hardcoded copy.
        var key = status switch
        {
            ConnectionStatus.Connected => "StatusConnected",
            ConnectionStatus.Connecting => "StatusConnecting",
            _ => "StatusDisconnected"
        };
        var brush = FindResource(key) as Brush ?? Brushes.Gray;

        ConnectionDot.Fill = brush;
        TxtConnectionStatus.Foreground = brush;
    }

    // ── Session ─────────────────────────────────────────────────────

    private void OnSessionChanged(object? sender, SessionCategory category)
    {
        Dispatcher.BeginInvoke(UpdateSessionDisplay);
    }

    private void UpdateSessionDisplay()
    {
        var cat = _sessionConfig.CurrentCategory;
        TxtSessionType.Text = cat == SessionCategory.Unknown ? "—" : cat.ToString();
        TxtSessionType.Foreground = cat switch
        {
            SessionCategory.Practice => new SolidColorBrush(Color.FromRgb(0, 188, 212)),
            SessionCategory.Qualifying => new SolidColorBrush(Color.FromRgb(171, 71, 188)),
            SessionCategory.Race => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            SessionCategory.Warmup => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
            _ => new SolidColorBrush(Color.FromRgb(136, 136, 136)),
        };
    }

    // ── Quick actions ───────────────────────────────────────────────

    private void WidgetToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox chk) return;
        if (chk.DataContext is not WidgetItem item) return;

        if (item.IsActive)
        {
            if (!_widgetManager.HasWidgetType(item.Type))
                _widgetManager.CreateWidget(item.Type);
            else
                foreach (var w in _widgetManager.GetWidgetsByType(item.Type))
                    w.SetUserVisibility(true);
        }
        else
        {
            // Hide, don't remove. Removing rewrote layout.json without this widget,
            // discarding its position, size and settings — the same data-loss the
            // Widgets page's Show/Hide button had.
            foreach (var w in _widgetManager.GetWidgetsByType(item.Type))
                w.SetUserVisibility(false);
        }

        _widgetManager.SaveCurrentLayout();
    }

    private void BtnLockAll_Click(object sender, RoutedEventArgs e)
    {
        var settings = AppSettings.Instance;
        settings.LockWindows = !settings.LockWindows;
        settings.Save();
        _widgetManager.LockAllWidgets(settings.LockWindows);
        BtnLockAll.Content = settings.LockWindows ? "🔓 Unlock All" : "🔒 Lock All";
    }

    private void BtnShowHideAll_Click(object sender, RoutedEventArgs e)
    {
        _widgetManager.ToggleAllWidgets();
    }

    // ── Hotkeys display ─────────────────────────────────────────────

    private void UpdateHotkeysDisplay()
    {
        var settings = AppSettings.Instance;
        string FormatMod(string m) => m == "None" ? "" : m;

        string lockMod = FormatMod(settings.ToggleLockModifier);
        string lockKey = settings.ToggleLockKey;
        string lockHk = string.IsNullOrEmpty(lockMod) ? lockKey : $"{lockMod}+{lockKey}";

        string visMod = FormatMod(settings.ToggleVisibilityModifier);
        string visKey = settings.ToggleVisibilityKey;
        string visHk = string.IsNullOrEmpty(visMod) ? visKey : $"{visMod}+{visKey}";

        TxtHotkeys.Text = $"{lockHk} Lock | {visHk} Show/Hide";
    }

    // ── Session presets UI ─────────────────────────────────────────

    private void InitSessionPresetUI()
    {
        ChkSessionPresetsEnabled.IsChecked = _sessionConfig.IsEnabled;
        PanelSessionPresets.IsEnabled = _sessionConfig.IsEnabled;
        PanelSessionPresets.Opacity = _sessionConfig.IsEnabled ? 1.0 : 0.4;

        PopulatePresetToggles(WrapPractice, SessionCategory.Practice);
        PopulatePresetToggles(WrapQualifying, SessionCategory.Qualifying);
        PopulatePresetToggles(WrapRace, SessionCategory.Race);
        PopulatePresetToggles(WrapWarmup, SessionCategory.Warmup);
    }

    private void PopulatePresetToggles(WrapPanel panel, SessionCategory category)
    {
        panel.Children.Clear();

        var preset = _sessionConfig.GetPreset(category);
        if (preset == null) return;

#if DEBUG
        var widgetTypes = Enum.GetValues<WidgetType>();
#else
        var widgetTypes = new[] { WidgetType.MRTOne, WidgetType.ProximityFeed };
#endif

        foreach (var wt in widgetTypes)
        {
            bool visible = preset.WidgetVisibility.TryGetValue(wt, out bool v) && v;
            var chk = new CheckBox
            {
                Content = wt.GetDisplayName(),
                IsChecked = visible,
                Style = (Style)FindResource("MRT.ToggleButton"),
                Margin = new Thickness(0, 0, 8, 4),
                Tag = new PresetToggleTag(category, wt),
                FontSize = 10,
            };
            chk.Checked += PresetWidget_Changed;
            chk.Unchecked += PresetWidget_Changed;
            panel.Children.Add(chk);
        }
    }

    private record PresetToggleTag(SessionCategory Category, WidgetType WidgetType);

    private void SessionPresetsEnabled_Changed(object sender, RoutedEventArgs e)
    {
        bool enabled = ChkSessionPresetsEnabled.IsChecked == true;
        _sessionConfig.IsEnabled = enabled;
        _sessionConfig.Save();
        PanelSessionPresets.IsEnabled = enabled;
        PanelSessionPresets.Opacity = enabled ? 1.0 : 0.4;
    }

    private void PresetWidget_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox chk) return;
        if (chk.Tag is not PresetToggleTag tag) return;

        var preset = _sessionConfig.GetPreset(tag.Category);
        if (preset == null) return;

        preset.WidgetVisibility[tag.WidgetType] = chk.IsChecked == true;
        _sessionConfig.Save();
    }

    private void BtnExpandPresets_Click(object sender, RoutedEventArgs e)
    {
        if (PanelPresetDetails.Visibility == Visibility.Collapsed)
        {
            PanelPresetDetails.Visibility = Visibility.Visible;
            BtnExpandPresets.Content = "▼ Hide preset details";
        }
        else
        {
            PanelPresetDetails.Visibility = Visibility.Collapsed;
            BtnExpandPresets.Content = "▶ Show preset details";
        }
    }

    // ── Performance monitoring ────────────────────────────────────

    private void UpdatePerformanceDisplay()
    {
        try
        {
            using var proc = Process.GetCurrentProcess();
            var now = DateTime.UtcNow;
            var cpuTime = proc.TotalProcessorTime;
            var elapsed = (now - _lastCpuCheck).TotalMilliseconds;

            if (elapsed > 0 && _lastCpuTime != TimeSpan.Zero)
            {
                var cpuUsed = (cpuTime - _lastCpuTime).TotalMilliseconds;
                _cpuPercent = cpuUsed / (elapsed * Environment.ProcessorCount) * 100.0;
            }

            _lastCpuCheck = now;
            _lastCpuTime = cpuTime;

            TxtCpuUsage.Text = $"{_cpuPercent:F1}%";
            TxtMemUsage.Text = $"{proc.WorkingSet64 / (1024.0 * 1024.0):F0} MB";
        }
        catch
        {
            // Ignore – non-critical display
        }
    }

    // ── Cleanup ─────────────────────────────────────────────────────

    public void Dispose()
    {
        _refreshTimer.Stop();
        _telemetryService.StatusChanged -= OnStatusChanged;
        _sessionConfig.SessionCategoryChanged -= OnSessionChanged;
    }
}
