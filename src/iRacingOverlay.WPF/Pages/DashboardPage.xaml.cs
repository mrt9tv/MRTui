using System;
using System.Collections.Generic;
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
        foreach (WidgetType wt in Enum.GetValues<WidgetType>())
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

    // ── Cleanup ─────────────────────────────────────────────────────

    public void Dispose()
    {
        _refreshTimer.Stop();
        _telemetryService.StatusChanged -= OnStatusChanged;
        _sessionConfig.SessionCategoryChanged -= OnSessionChanged;
    }
}
