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

public partial class DashboardPage : UserControl
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
        _refreshTimer.Start();

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
        var items = new List<WidgetItem>();
#if DEBUG
        var widgetTypes = Enum.GetValues<WidgetType>();
#else
        // Release build: only show MRT One and Proximity Feed (Relative hidden for now)
        var widgetTypes = new[] { WidgetType.MRTOne, WidgetType.ProximityFeed };
#endif
        foreach (WidgetType wt in widgetTypes)
        {
            bool active = _widgetManager.HasWidgetType(wt);
            items.Add(new WidgetItem
            {
                Icon = GetWidgetIcon(wt),
                Name = wt.GetDisplayName(),
                Status = active ? "Active" : "Hidden",
                IsActive = active,
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
        Dispatcher.Invoke(() => UpdateConnectionDisplay(e.Status));
    }

    private void UpdateConnectionDisplay(ConnectionStatus status)
    {
        TxtConnectionStatus.Text = status switch
        {
            ConnectionStatus.Connected => "Connected",
            ConnectionStatus.Connecting => "Connecting...",
            _ => "Disconnected"
        };

        var color = status switch
        {
            ConnectionStatus.Connected => Color.FromRgb(0, 188, 212),
            ConnectionStatus.Connecting => Color.FromRgb(255, 152, 0),
            _ => Color.FromRgb(136, 136, 136)
        };

        ConnectionDot.Fill = new SolidColorBrush(color);
        TxtConnectionStatus.Foreground = new SolidColorBrush(color);
    }

    // ── Session ─────────────────────────────────────────────────────

    private void OnSessionChanged(object? sender, SessionCategory category)
    {
        Dispatcher.Invoke(UpdateSessionDisplay);
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

        if (item.IsActive && !_widgetManager.HasWidgetType(item.Type))
        {
            _widgetManager.CreateWidget(item.Type);
        }
        else if (!item.IsActive && _widgetManager.HasWidgetType(item.Type))
        {
            var widgets = _widgetManager.GetWidgetsByType(item.Type).ToList();
            foreach (var w in widgets)
                _widgetManager.RemoveWidget(w.WidgetId);
        }
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
