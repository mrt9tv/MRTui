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
    private readonly TrayIconService _trayIcon = new();
    private readonly UpdateService _updateService;

    /// <summary>When true, Close() will actually exit instead of minimizing to tray.</summary>
    private bool _forceClose;

    /// <summary>Tracks whether the player is currently in pit lane for auto-hide.</summary>
    private bool _playerInPits;
    /// <summary>Widget IDs that were hidden by auto-hide-in-pits (to restore only those).</summary>
    private readonly HashSet<Guid> _autoHiddenWidgets = new();

    // ── Pages ────────────────────────────────────────────────────────────
    private DashboardPage? _dashboardPage;
    private WidgetsPage? _widgetsPage;
#if DEBUG
    private SessionsPage? _sessionsPage;
#endif
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
        _updateService = services.GetRequiredService<UpdateService>();

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
            // First run — create default widgets
            _widgetManager.CreateWidget(Models.WidgetType.MRTOne);
            _widgetManager.CreateWidget(Models.WidgetType.ProximityFeed);
        }
        else
        {
            // Ensure ProximityFeed exists even if saved layout didn't include it
            if (!_widgetManager.ActiveWidgets.Values.Any(w => w.WidgetType == Models.WidgetType.ProximityFeed))
                _widgetManager.CreateWidget(Models.WidgetType.ProximityFeed);
        }

        // Create pages (lazy-init on first nav, but pre-build dashboard)
        _dashboardPage = new DashboardPage(_widgetManager, _telemetryService, _sessionConfig);
        _widgetsPage = new WidgetsPage(_widgetManager, _telemetryService);
#if DEBUG
        _sessionsPage = new SessionsPage(_sessionConfig);
        _sessionsPage.SetProfileService(_profileService, _widgetManager);
#endif
        _settingsPage = new SettingsPage();
        _aboutPage = new AboutPage();
        _aboutPage.SetUpdateService(_updateService);

        // Wire settings change events
        _settingsPage.SettingsChanged += OnSettingsChanged;
        _settingsPage.ResetLayoutRequested += OnResetLayout;

        // Initialize tray icon
        _trayIcon.Initialize();
        _trayIcon.RestoreRequested += (_, _) => Dispatcher.Invoke(RestoreFromTray);
        _trayIcon.ExitRequested += (_, _) => Dispatcher.Invoke(() => { _forceClose = true; Close(); });

        // Show last-used page (persisted between sessions)
        UpdateConnectionStatus(_telemetryService.Status);
        TitleVersionText.Text = VersionInfo.DisplayVersion;
        CheckForUpdateStatusAsync();
        UpdateHotkeysDisplay();

        NavigateTo(AppSettings.Instance.LastNavPage);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Build nav button lookup
        _navButtons["Dashboard"] = NavDashboard;
        _navButtons["Widgets"] = NavWidgets;
#if DEBUG
        _navButtons["Sessions"] = NavSessions;
#else
        NavSessions.Visibility = Visibility.Collapsed;
#endif
        _navButtons["Settings"] = NavSettings;
        _navButtons["About"] = NavAbout;

        RegisterGlobalHotkeys();
    }

    /// <summary>
    /// Check for updates in the background and display status next to version.
    /// </summary>
    private async void CheckForUpdateStatusAsync()
    {
        try
        {
            bool hasUpdate = await _updateService.CheckForUpdatesAsync().ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() =>
            {
                if (hasUpdate)
                {
                    TitleUpdateStatus.Text = "(update available)";
                    TitleUpdateStatus.Foreground = FindResource("OrangePrimary") as System.Windows.Media.Brush
                        ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 160, 0));
                }
                else if (_updateService.IsInstalled)
                {
                    TitleUpdateStatus.Text = "(up to date)";
                }
                else
                {
                    TitleUpdateStatus.Text = "(dev build)";
                }
            });
        }
        catch
        {
            // Silently ignore update check failures
        }
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
#if DEBUG
            "Sessions" => _sessionsPage,
#endif
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
        Dispatcher.Invoke(() =>
        {
            _sessionConfig.CheckSessionChange(data);
            CheckPlayerPitState(data);
        });
    }

    /// <summary>
    /// Check if the player has entered or exited pit lane and auto-hide/show widgets.
    /// </summary>
    private void CheckPlayerPitState(TelemetryData data)
    {
        var settings = AppSettings.Instance;
        if (!settings.AutoHideInPitsEnabled) return;

        bool isInPits = data.OnPitRoad || data.PlayerCarInPitStall;

        if (isInPits && !_playerInPits)
        {
            // Player entered pits — hide configured widgets
            _playerInPits = true;
            _autoHiddenWidgets.Clear();

            foreach (var kvp in settings.AutoHideInPitsWidgets)
            {
                if (!kvp.Value) continue; // not configured to auto-hide
                if (!Enum.TryParse<WidgetType>(kvp.Key, out var wt)) continue;

                foreach (var widget in _widgetManager.GetWidgetsByType(wt))
                {
                    if (widget.IsVisible)
                    {
                        _autoHiddenWidgets.Add(widget.WidgetId);
                        widget.SetUserVisibility(false);
                    }
                }
            }
        }
        else if (!isInPits && _playerInPits)
        {
            // Player exited pits — restore auto-hidden widgets
            _playerInPits = false;

            foreach (var widgetId in _autoHiddenWidgets)
            {
                if (_widgetManager.ActiveWidgets.TryGetValue(widgetId, out var widget))
                    widget.SetUserVisibility(true);
            }
            _autoHiddenWidgets.Clear();
        }
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

        // Update session type badge from current telemetry
        var category = _sessionConfig.CurrentCategory;
        SessionTypeText.Text = category switch
        {
            SessionCategory.Practice => "Practice",
            SessionCategory.Qualifying => "Qualifying",
            SessionCategory.Race => "Race",
            SessionCategory.Warmup => "Warmup",
            _ => ""
        };
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
        _trayIcon.Dispose();
        (_dashboardPage as IDisposable)?.Dispose();
        (_widgetsPage as IDisposable)?.Dispose();
#if DEBUG
        (_sessionsPage as IDisposable)?.Dispose();
#endif
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
        // Check against full virtual screen (all monitors)
        return left >= SystemParameters.VirtualScreenLeft - 100 &&
               left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth + 100 &&
               top >= SystemParameters.VirtualScreenTop - 100 &&
               top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight + 100;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var s = AppSettings.Instance;
        // If CloseToTray + MinimizeToTray are both on, intercept close → tray
        // (but NOT when user explicitly clicks Exit from tray menu)
        if (!_forceClose && s.MinimizeToTray && s.CloseToTray)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized; // triggers StateChanged → tray
            return;
        }
        SaveWindowState();
    }
    private void MainWindow_LocationChanged(object? sender, EventArgs e) { if (WindowState == WindowState.Normal) SaveWindowState(); }
    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e) { if (WindowState == WindowState.Normal) SaveWindowState(); }
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        var s = AppSettings.Instance;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        s.Save();

        // Minimize to tray when setting is enabled
        if (WindowState == WindowState.Minimized && s.MinimizeToTray)
        {
            Hide();
            ShowInTaskbar = false;
            _trayIcon.Show();
            _trayIcon.ShowBalloon("MRT UI", "Minimized to tray. Double-click to restore.");
        }
    }

    private void RestoreFromTray()
    {
        _trayIcon.Hide();
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnSettingsChanged()
    {
        Topmost = AppSettings.Instance.AlwaysOnTop;
    }

    private void OnResetLayout()
    {
        _widgetManager.RemoveAllWidgets();
        _widgetManager.CreateWidget(Models.WidgetType.MRTOne);
        _widgetManager.SaveCurrentLayout();
        _widgetsPage?.SyncPanelToActiveWidget();
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
