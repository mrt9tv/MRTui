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
using iRacingOverlay.WPF.Utils;

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

    // Overlay health — is the UI thread keeping up with the 60 Hz feed?
    private long _lastDroppedFrames;
    private double _frameTimeTotalMs;
    private int _frameSamples;
    private TimeSpan _lastRenderTime = TimeSpan.Zero;

    /// <summary>
    /// Sample the interval between WPF composition passes. CompositionTarget.Rendering
    /// fires once per rendered frame, so the gap between ticks is the UI thread's
    /// actual frame time — the number that says whether the overlay can keep up.
    /// </summary>
    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args) return;

        if (_lastRenderTime != TimeSpan.Zero)
        {
            double deltaMs = (args.RenderingTime - _lastRenderTime).TotalMilliseconds;

            // Ignore the long gaps that follow the window being hidden or restored.
            if (deltaMs > 0 && deltaMs < 500)
            {
                _frameTimeTotalMs += deltaMs;
                _frameSamples++;
            }
        }

        _lastRenderTime = args.RenderingTime;
    }

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
                CompositionTarget.Rendering += OnRendering;
            }
            else
            {
                _refreshTimer.Stop();
                CompositionTarget.Rendering -= OnRendering;
                _lastRenderTime = TimeSpan.Zero;
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

    /// <summary>
    /// Raised when the user asks to configure a widget from the dashboard list.
    /// MainWindow navigates to the Widgets page with that widget selected.
    /// </summary>
    public event Action<WidgetType>? ConfigureWidgetRequested;

    private void WidgetConfigure_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WidgetType type })
            ConfigureWidgetRequested?.Invoke(type);
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
        catch (Exception ex)
        {
            AppLog.Warn("Could not read process performance counters", ex);
        }

        UpdateOverlayHealth();
    }

    /// <summary>
    /// Show whether the overlay is actually keeping up with the telemetry feed.
    ///
    /// Frame time above ~16 ms, or a dropped-frame count that keeps climbing, means
    /// the UI thread is behind the 60 Hz feed — which is what a user experiences as
    /// the overlay stuttering or freezing.
    /// </summary>
    private void UpdateOverlayHealth()
    {
        long dropped = 0;
        foreach (var widget in _widgetManager.ActiveWidgets.Values)
            dropped += widget.DroppedFrames;

        long deltaDropped = dropped - _lastDroppedFrames;
        _lastDroppedFrames = dropped;

        TxtDroppedFrames.Text = deltaDropped > 0 ? $"{dropped} (+{deltaDropped})" : dropped.ToString();
        TxtDroppedFrames.Foreground = deltaDropped > 30
            ? (FindResource("OrangePrimary") as Brush ?? Brushes.Orange)
            : (FindResource("TealPrimary") as Brush ?? Brushes.Teal);

        double frameMs = _frameSamples > 0 ? _frameTimeTotalMs / _frameSamples : 0;
        _frameTimeTotalMs = 0;
        _frameSamples = 0;

        TxtFrameTime.Text = frameMs > 0 ? $"{frameMs:F1} ms" : "—";
        TxtFrameTime.Foreground = frameMs > 16.0
            ? (FindResource("OrangePrimary") as Brush ?? Brushes.Orange)
            : (FindResource("TealPrimary") as Brush ?? Brushes.Teal);
    }

    private void BtnOpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(AppLog.LogDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = AppLog.LogDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppLog.Warn("Could not open the log folder", ex);
        }
    }

    // ── Cleanup ─────────────────────────────────────────────────────

    public void Dispose()
    {
        _refreshTimer.Stop();
        CompositionTarget.Rendering -= OnRendering;
        _telemetryService.StatusChanged -= OnStatusChanged;
        _sessionConfig.SessionCategoryChanged -= OnSessionChanged;
    }
}
