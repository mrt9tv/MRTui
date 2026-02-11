using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Pages;
using iRacingOverlay.WPF.Services;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF;

public partial class MainWindow : Window
{
    private readonly WidgetManager _widgetManager;
    private readonly ITelemetryService _telemetryService;
    private readonly SessionConfigService _sessionConfig;
    private readonly ProfileStorageService _profileService;
    private readonly ILogger<MainWindow> _logger;
    private readonly DispatcherTimer _updateRateTimer;
    private GlobalHotkey? _toggleLockHotkey;
    private GlobalHotkey? _toggleVisibilityHotkey;

    // ── Pages ────────────────────────────────────────────────────────────
    private DashboardPage? _dashboardPage;
    private WidgetsPage? _widgetsPage;
    private SessionsPage? _sessionsPage;
    private SettingsPage? _settingsPage;
    private AboutPage? _aboutPage;
    private string _currentNav = "Dashboard";

    /// <summary>Map nav button names to teal highlight.</summary>
    private readonly Dictionary<string, Button> _navButtons = new();

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();

        _widgetManager = services.GetRequiredService<WidgetManager>();
        _telemetryService = services.GetRequiredService<ITelemetryService>();
        _sessionConfig = services.GetRequiredService<SessionConfigService>();
        _profileService = services.GetRequiredService<ProfileStorageService>();
        _logger = services.GetRequiredService<ILogger<MainWindow>>();

        // Wire profile service into session config for auto-switching
        _sessionConfig.ProfileService = _profileService;

        _telemetryService.StatusChanged += OnTelemetryStatusChanged;
        _telemetryService.TelemetryUpdated += OnTelemetryUpdatedForSession;
        _sessionConfig.SessionCategoryChanged += OnSessionCategoryChanged;
        _sessionConfig.Load();
        Closing += MainWindow_Closing;
        LocationChanged += MainWindow_LocationChanged;
        SizeChanged += MainWindow_SizeChanged;
        StateChanged += MainWindow_StateChanged;
        Loaded += MainWindow_Loaded;

        // Update rate display timer
        _updateRateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _updateRateTimer.Tick += (_, _) => UpdateStatusBar();
        _updateRateTimer.Start();

        // Apply saved settings and restore widget layout
        ApplyWindowSettings();
        if (!_widgetManager.LoadSavedLayout())
        {
            // First run — create default widgets (MRT One, Relative, ProxFeed)
            _widgetManager.CreateWidget(Models.WidgetType.MRTOne);
            _widgetManager.CreateWidget(Models.WidgetType.Relative);
            _widgetManager.CreateWidget(Models.WidgetType.ProximityFeed);
        }

        // Create pages (lazy-init on first nav, but pre-build dashboard)
        _dashboardPage = new DashboardPage(_widgetManager, _telemetryService, _sessionConfig);
        _widgetsPage = new WidgetsPage(_widgetManager, _telemetryService);
        _sessionsPage = new SessionsPage(_sessionConfig);
        _sessionsPage.SetProfileService(_profileService, _widgetManager);
        _settingsPage = new SettingsPage();
        _aboutPage = new AboutPage();

        // Wire settings change events
        _settingsPage.SettingsChanged += () => Topmost = AppSettings.Instance.AlwaysOnTop;

        // Show last-used page (persisted between sessions)
        UpdateConnectionStatus(_telemetryService.Status);
        TitleVersionText.Text = VersionInfo.DisplayVersion;
        UpdateHotkeysDisplay();

        NavigateTo(AppSettings.Instance.LastNavPage);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Build nav button lookup
        _navButtons["Dashboard"] = NavDashboard;
        _navButtons["Widgets"] = NavWidgets;
        _navButtons["Sessions"] = NavSessions;
        _navButtons["Settings"] = NavSettings;
        _navButtons["About"] = NavAbout;

        RegisterGlobalHotkeys();
    }

    // ── Page Navigation ─────────────────────────────────────────────────

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            NavigateTo(tag);
    }

    private void NavigateTo(string page)
    {
        _currentNav = page;
        PageHost.Content = page switch
        {
            "Dashboard" => _dashboardPage,
            "Widgets" => _widgetsPage,
            "Sessions" => _sessionsPage,
            "Settings" => _settingsPage,
            "About" => _aboutPage,
            _ => _dashboardPage,
        };

        // Persist selected page between sessions
        AppSettings.Instance.LastNavPage = page;
        AppSettings.Instance.Save();

        // Update nav button highlights
        var teal = FindResource("TealPrimary") as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(0, 128, 128));
        var light = FindResource("LightText") as SolidColorBrush ?? new SolidColorBrush(Colors.White);
        foreach (var (name, btn) in _navButtons)
            btn.Foreground = name == page ? teal : light;
    }

    // ── Session auto-detect forwarding ──────────────────────────────────

    private void OnTelemetryUpdatedForSession(object? sender, TelemetryData data)
    {
        Dispatcher.Invoke(() => _sessionConfig.CheckSessionChange(data));
    }

    private void OnSessionCategoryChanged(object? sender, SessionCategory category)
    {
        Dispatcher.Invoke(() =>
        {
            // Check if a matching profile exists (profile takes priority)
            var matchedProfile = _sessionConfig.CheckProfileMatch();
            if (matchedProfile != null)
            {
                _profileService.ApplyProfile(matchedProfile.Id, _widgetManager);
                _logger.LogInformation("Applied profile: {Name} for {Category}", matchedProfile.Name, category);
            }
            else
            {
                // Fall back to basic session preset (visibility only)
                var preset = _sessionConfig.GetPreset(category);
                if (preset != null)
                {
                    _widgetManager.ApplySessionPreset(preset);
                    _logger.LogInformation("Applied session preset: {Category}", category);
                }
            }
            _widgetsPage?.SyncPanelToActiveWidget();
        });
    }



    // ── Status bar ──────────────────────────────────────────────────────

    private void OnTelemetryStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        Dispatcher.Invoke(() => UpdateConnectionStatus(e.Status));
    }

    private void UpdateConnectionStatus(ConnectionStatus status)
    {
        var text = status switch
        {
            ConnectionStatus.Connected => "Connected",
            ConnectionStatus.Connecting => "Connecting...",
            ConnectionStatus.Disconnected => "Disconnected",
            _ => "Unknown"
        };
        var brush = new SolidColorBrush(status switch
        {
            ConnectionStatus.Connected => Color.FromRgb(0, 188, 212),
            ConnectionStatus.Connecting => Color.FromRgb(255, 152, 0),
            _ => Color.FromRgb(136, 136, 136)
        });
        ConnectionStatusText.Text = text;
        ConnectionStatusText.Foreground = brush;
    }

    private void UpdateStatusBar()
    {
        var rate = _telemetryService.UpdateRate;
        UpdateRateText.Text = rate > 0 ? $"{rate:F0} Hz" : "0 Hz";
    }

    // ── Global hotkeys ──────────────────────────────────────────────────

    private void UpdateHotkeysDisplay()
    {
        var s = AppSettings.Instance;
        string Fmt(string m) => m == "None" ? "" : m;
        string lockHk = string.IsNullOrEmpty(Fmt(s.ToggleLockModifier)) ? s.ToggleLockKey : $"{Fmt(s.ToggleLockModifier)}+{s.ToggleLockKey}";
        string visHk = string.IsNullOrEmpty(Fmt(s.ToggleVisibilityModifier)) ? s.ToggleVisibilityKey : $"{Fmt(s.ToggleVisibilityModifier)}+{s.ToggleVisibilityKey}";
        HotkeysText.Text = $"{lockHk} Lock | {visHk} Show/Hide";
    }

    private void RegisterGlobalHotkeys()
    {
        var s = AppSettings.Instance;
        if (s.HotkeysConflict())
        {
            MessageBox.Show(
                "Toggle Lock and Toggle Visibility hotkeys conflict.\nPlease change one in settings.",
                "Hotkey Conflict", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _toggleLockHotkey = new GlobalHotkey(this, hotkeyId: 9001);
        _toggleLockHotkey.HotkeyPressed += (_, _) => Dispatcher.Invoke(ToggleWidgetLock);
        _toggleLockHotkey.Register(s.ToggleLockModifier, s.ToggleLockKey);

        _toggleVisibilityHotkey = new GlobalHotkey(this, hotkeyId: 9002);
        _toggleVisibilityHotkey.HotkeyPressed += (_, _) => Dispatcher.Invoke(ToggleWidgetVisibility);
        _toggleVisibilityHotkey.Register(s.ToggleVisibilityModifier, s.ToggleVisibilityKey);
    }

    private void ToggleWidgetLock()
    {
        var s = AppSettings.Instance;
        s.LockWindows = !s.LockWindows;
        s.Save();
        _widgetManager.LockAllWidgets(s.LockWindows);
    }

    private void ToggleWidgetVisibility() => _widgetManager.ToggleAllWidgets();

    // ── Window state persistence ────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        _telemetryService.StatusChanged -= OnTelemetryStatusChanged;
        _telemetryService.TelemetryUpdated -= OnTelemetryUpdatedForSession;
        _sessionConfig.SessionCategoryChanged -= OnSessionCategoryChanged;
        _sessionConfig.Save();
        _toggleLockHotkey?.Dispose();
        _toggleVisibilityHotkey?.Dispose();
        _widgetManager.SaveCurrentLayout();
        _widgetManager.RemoveAllWidgets();
        (_dashboardPage as IDisposable)?.Dispose();
        (_widgetsPage as IDisposable)?.Dispose();
        (_sessionsPage as IDisposable)?.Dispose();
        System.Windows.Application.Current.Shutdown();
        base.OnClosed(e);
    }

    private void ApplyWindowSettings()
    {
        var s = AppSettings.Instance;
        Width = s.WindowWidth;
        Height = s.WindowHeight;
        if (s.WindowLeft.HasValue && s.WindowTop.HasValue &&
            IsPositionOnScreen(s.WindowLeft.Value, s.WindowTop.Value))
        {
            Left = s.WindowLeft.Value;
            Top = s.WindowTop.Value;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
        if (s.WindowMaximized) WindowState = WindowState.Maximized;
        if (s.StartMinimized) WindowState = WindowState.Minimized;
        Topmost = s.AlwaysOnTop;
    }

    private static bool IsPositionOnScreen(double left, double top)
    {
        return left >= SystemParameters.VirtualScreenLeft &&
               left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
               top >= SystemParameters.VirtualScreenTop &&
               top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e) => SaveWindowState();
    private void MainWindow_LocationChanged(object? sender, EventArgs e) { if (WindowState == WindowState.Normal) SaveWindowState(); }
    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e) { if (WindowState == WindowState.Normal) SaveWindowState(); }
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        var s = AppSettings.Instance;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        s.Save();
    }

    private void SaveWindowState()
    {
        if (!IsLoaded) return;
        var s = AppSettings.Instance;
        if (WindowState == WindowState.Normal)
        {
            s.WindowWidth = Width;
            s.WindowHeight = Height;
            s.WindowLeft = Left;
            s.WindowTop = Top;
        }
        s.WindowMaximized = WindowState == WindowState.Maximized;
        s.Save();
    }
}
