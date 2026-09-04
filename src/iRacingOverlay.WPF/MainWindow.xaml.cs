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
    private readonly IServiceProvider _services;

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

        _services = services;
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
        // Status bar refresh — only while the window is actually on screen. It kept
        // ticking while minimised to tray, which is precisely when the user is driving
        // and the UI thread should be doing nothing.
        _updateRateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _updateRateTimer.Tick += (_, _) => UpdateStatusBar();
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) { UpdateStatusBar(); _updateRateTimer.Start(); }
            else _updateRateTimer.Stop();
        };
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

        // Dashboard "Settings" button jumps to that widget's controls
        _dashboardPage.ConfigureWidgetRequested += type =>
        {
            _widgetsPage?.SelectWidget(type);
            NavigateTo("Widgets");
        };

        // Wire settings change events
        _settingsPage.SettingsChanged += OnSettingsChanged;
        _settingsPage.ResetLayoutRequested += OnResetLayout;
        _settingsPage.UndoResetRequested += OnUndoResetLayout;

        // Initialize tray icon
        _trayIcon.Initialize();
        _trayIcon.RestoreRequested += (_, _) => Dispatcher.BeginInvoke(RestoreFromTray);
        _trayIcon.ExitRequested += (_, _) => Dispatcher.BeginInvoke(() => { _forceClose = true; Close(); });

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
        // Sessions is no longer DEBUG-only: SessionConfigService switches profiles
        // automatically in Release too, so hiding this page left shipped users with
        // automatic behaviour and no way to see or edit what it was switching between.
        _navButtons["Sessions"] = NavSessions;
        _navButtons["Settings"] = NavSettings;
        _navButtons["About"] = NavAbout;

        RegisterGlobalHotkeys();
        HookSingleInstanceMessage();
    }

    /// <summary>
    /// Listen for the broadcast a second launch sends before exiting, and bring
    /// this window forward instead of letting the user think nothing happened.
    /// </summary>
    private void HookSingleInstanceMessage()
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
        source?.AddHook((IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled) =>
        {
            if ((uint)msg == Program.ShowExistingWindowMessage)
            {
                _logger.LogInformation("Second instance launched — restoring existing window");
                RestoreFromTray();
                handled = true;
            }
            return IntPtr.Zero;
        });
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

    private int _sessionCheckCounter;

    private void OnTelemetryUpdatedForSession(object? sender, TelemetryData data)
    {
        // Throttle to ~2Hz (every 30 ticks at 60Hz) — session/pit changes are human-timescale events.
        // Use non-blocking BeginInvoke so the telemetry thread is never stalled.
        if (++_sessionCheckCounter % 30 != 0) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
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
        // Non-blocking: this arrives on the telemetry thread, and the body creates
        // and destroys windows and writes the layout. A blocking Invoke here stalled
        // the whole telemetry feed for the duration of that work.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
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
        // BeginInvoke, not Invoke — this fires on the SDK thread.
        Dispatcher.BeginInvoke(() => UpdateConnectionStatus(e.Status));
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
        // From the theme, not hardcoded here — the same three colours were
        // duplicated in DashboardPage and disagreed with the theme's own tokens.
        var key = status switch
        {
            ConnectionStatus.Connected => "StatusConnected",
            ConnectionStatus.Connecting => "StatusConnecting",
            _ => "StatusDisconnected"
        };

        ConnectionStatusText.Text = text;
        ConnectionStatusText.Foreground = FindResource(key) as Brush ?? Brushes.Gray;
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

    /// <summary>Format a modifier + key pair for display, e.g. "Ctrl+L".</summary>
    public static string FormatHotkey(string modifier, string key)
    {
        var mod = modifier == "None" ? "" : modifier;
        return string.IsNullOrEmpty(mod) ? key : $"{mod}+{key}";
    }

    private void UpdateHotkeysDisplay()
    {
        var s = AppSettings.Instance;
        string lockHk = FormatHotkey(s.ToggleLockModifier, s.ToggleLockKey);
        string visHk = FormatHotkey(s.ToggleVisibilityModifier, s.ToggleVisibilityKey);

        // Mark a binding Windows refused so the status bar stops advertising a dead key.
        string lockLabel = LockHotkeyRegistered ? lockHk : $"{lockHk} (unavailable)";
        string visLabel = VisibilityHotkeyRegistered ? visHk : $"{visHk} (unavailable)";

        HotkeysText.Text = $"{lockLabel} Lock | {visLabel} Show/Hide";
        HotkeysText.Foreground = (LockHotkeyRegistered && VisibilityHotkeyRegistered)
            ? (FindResource("MutedText") as SolidColorBrush ?? Brushes.Gray)
            : (FindResource("OrangePrimary") as SolidColorBrush ?? Brushes.Orange);
    }

    /// <summary>Which hotkeys failed to register, for display in the status bar and Settings.</summary>
    public bool LockHotkeyRegistered { get; private set; }
    public bool VisibilityHotkeyRegistered { get; private set; }

    /// <summary>Raised when hotkey registration state changes, so Settings can refresh.</summary>
    public event Action? HotkeyStateChanged;

    /// <summary>
    /// Register both global hotkeys and record whether Windows accepted them.
    /// Registration failure (another app already owns the combination) used to be
    /// discarded, leaving the feature silently dead while the UI still advertised it.
    /// </summary>
    public void RegisterGlobalHotkeys()
    {
        var s = AppSettings.Instance;

        _toggleLockHotkey?.Dispose();
        _toggleVisibilityHotkey?.Dispose();
        LockHotkeyRegistered = false;
        VisibilityHotkeyRegistered = false;

        if (s.HotkeysConflict())
        {
            // Shown inline rather than as a modal dialog on startup.
            _logger.LogWarning("Toggle Lock and Toggle Visibility hotkeys are bound to the same key");
            UpdateHotkeysDisplay();
            HotkeyStateChanged?.Invoke();
            return;
        }

        var hotkeyLogger = _services.GetService<ILogger<GlobalHotkey>>();

        _toggleLockHotkey = new GlobalHotkey(this, hotkeyId: 9001, hotkeyLogger);
        _toggleLockHotkey.HotkeyPressed += (_, _) => Dispatcher.BeginInvoke(ToggleWidgetLock);
        LockHotkeyRegistered = _toggleLockHotkey.Register(s.ToggleLockModifier, s.ToggleLockKey);

        _toggleVisibilityHotkey = new GlobalHotkey(this, hotkeyId: 9002, hotkeyLogger);
        _toggleVisibilityHotkey.HotkeyPressed += (_, _) => Dispatcher.BeginInvoke(ToggleWidgetVisibility);
        VisibilityHotkeyRegistered = _toggleVisibilityHotkey.Register(s.ToggleVisibilityModifier, s.ToggleVisibilityKey);

        if (!LockHotkeyRegistered || !VisibilityHotkeyRegistered)
        {
            _logger.LogWarning(
                "Hotkey registration failed (lock: {Lock}, visibility: {Vis}) — another application may already own the combination",
                LockHotkeyRegistered, VisibilityHotkeyRegistered);
        }

        UpdateHotkeysDisplay();
        HotkeyStateChanged?.Invoke();
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
        _updateRateTimer.Stop();
        _toggleLockHotkey?.Dispose();
        _toggleVisibilityHotkey?.Dispose();

        // Snapshot the layout while the widgets still exist, then force both
        // debounced writers to disk before the process goes away.
        _widgetManager.SaveCurrentLayout();
        _widgetManager.FlushLayout();
        AppSettings.Flush();

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
        s.SaveQuiet();

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

        // Recreate the same default set startup would create. Reset previously
        // made only MRT One, so a reset left the user with fewer widgets than a
        // fresh install.
        _widgetManager.CreateWidget(Models.WidgetType.MRTOne);
        _widgetManager.CreateWidget(Models.WidgetType.ProximityFeed);

        _widgetManager.SaveCurrentLayout();
        _widgetsPage?.SyncPanelToActiveWidget();
    }

    /// <summary>Reload the layout the user restored after undoing a reset.</summary>
    private void OnUndoResetLayout()
    {
        if (_widgetManager.LoadSavedLayout())
        {
            _widgetsPage?.SyncPanelToActiveWidget();
            _logger.LogInformation("Layout restored after reset");
        }
    }

    /// <summary>
    /// Record the window geometry for next launch.
    ///
    /// Called from LocationChanged and SizeChanged, i.e. on every mouse-move while
    /// dragging or resizing. It uses SaveQuiet so the write is coalesced onto a
    /// background thread and no listener is notified — nothing needs to react to the
    /// config window being moved. Previously this serialised the whole settings
    /// object and hit the disk synchronously on the UI thread for every pixel.
    /// </summary>
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
        s.SaveQuiet();
    }
}
