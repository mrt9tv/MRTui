using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>
/// Application shell: header with brand, navigation and live connection state;
/// content; a status strip carrying telemetry health.
///
/// Three destinations rather than five. Dashboard and Widgets merged into
/// <see cref="OverlayPage"/> — they duplicated each other's widget list and
/// toggles — and Sessions folded into Settings as a section.
/// </summary>
public partial class MainWindow : Window
{
    private readonly WidgetManager _widgetManager;
    private readonly ITelemetryService _telemetryService;
    private readonly SessionConfigService _sessionConfig;
    private readonly ProfileStorageService _profileService;
    private readonly ILogger<MainWindow> _logger;
    private readonly DispatcherTimer _statusTimer;
    private readonly TrayIconService _trayIcon = new();
    private readonly UpdateService _updateService;
    private readonly IServiceProvider _services;

    private GlobalHotkey? _toggleLockHotkey;
    private GlobalHotkey? _toggleVisibilityHotkey;
    private GlobalHotkey? _cycleProfileHotkey;

    /// <summary>When true, Close() exits instead of minimising to tray.</summary>
    private bool _forceClose;

    /// <summary>Whether the player is currently in pit lane, for auto-hide.</summary>
    private bool _playerInPits;

    /// <summary>Widgets hidden by auto-hide-in-pits, so only those are restored.</summary>
    private readonly HashSet<Guid> _autoHiddenWidgets = new();

    private OverlayPage _overlayPage = null!;
    private SettingsPage _settingsPage = null!;
    private AboutPage _aboutPage = null!;

    private readonly Dictionary<string, RadioButton> _navTabs = new();

    // Overlay health, sampled for the status strip
    private long _lastDroppedFrames;
    private double _frameTimeTotalMs;
    private int _frameSamples;
    private TimeSpan _lastRenderTime = TimeSpan.Zero;

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

        _sessionConfig.ProfileService = _profileService;

        _telemetryService.StatusChanged += OnTelemetryStatusChanged;
        _telemetryService.TelemetryUpdated += OnTelemetryUpdatedForSession;
        _sessionConfig.SessionCategoryChanged += OnSessionCategoryChanged;
        _sessionConfig.Load();

        Closing += MainWindow_Closing;
        LocationChanged += (_, _) => { if (WindowState == WindowState.Normal) SaveWindowState(); };
        SizeChanged += (_, _) => { if (WindowState == WindowState.Normal) SaveWindowState(); };
        StateChanged += MainWindow_StateChanged;
        Loaded += MainWindow_Loaded;

        // Status strip. Only ticks while the window is on screen — it used to run
        // for the life of the app, including while minimised to tray.
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _statusTimer.Tick += (_, _) => UpdateStatusBar();
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue)
            {
                UpdateStatusBar();
                _statusTimer.Start();
                CompositionTarget.Rendering += OnRendering;
            }
            else
            {
                _statusTimer.Stop();
                CompositionTarget.Rendering -= OnRendering;
                _lastRenderTime = TimeSpan.Zero;
            }
        };

        ApplyWindowSettings();
        RestoreOrCreateWidgets();
        BuildPages();

        _trayIcon.Initialize();
        _trayIcon.RestoreRequested += (_, _) => Dispatcher.BeginInvoke(RestoreFromTray);
        _trayIcon.ExitRequested += (_, _) => Dispatcher.BeginInvoke(() => { _forceClose = true; Close(); });

        TitleVersionText.Text = VersionInfo.DisplayVersion;
        UpdateConnectionStatus(_telemetryService.Status);
        CheckForUpdateStatusAsync();
    }

    private void RestoreOrCreateWidgets()
    {
        if (!_widgetManager.LoadSavedLayout())
        {
            _widgetManager.CreateWidget(WidgetType.MRTOne);
            _widgetManager.CreateWidget(WidgetType.ProximityFeed);
            return;
        }

        // Guarantee the default set exists even if an older layout predates a widget.
        if (!_widgetManager.HasWidgetType(WidgetType.ProximityFeed))
            _widgetManager.CreateWidget(WidgetType.ProximityFeed);
    }

    private void BuildPages()
    {
        _overlayPage = new OverlayPage(_widgetManager);
        _settingsPage = new SettingsPage(_widgetManager, _sessionConfig, _profileService);
        _aboutPage = new AboutPage();
        _aboutPage.SetUpdateService(_updateService);

        _settingsPage.SettingsChanged += OnSettingsChanged;
        _settingsPage.HotkeysChanged += RegisterGlobalHotkeys;
        _settingsPage.LayoutReset += () => _overlayPage.Refresh();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _navTabs["Overlay"] = NavOverlay;
        _navTabs["Settings"] = NavSettings;
        _navTabs["About"] = NavAbout;

        RegisterGlobalHotkeys();
        HookSingleInstanceMessage();

        NavigateTo(AppSettings.Instance.LastNavPage);
    }

    // ── Navigation ────────────────────────────────────────────────────

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
            NavigateTo(tag);
    }

    private void NavigateTo(string page)
    {
        // Older builds persisted Dashboard/Widgets/Sessions; map them forward.
        page = page switch
        {
            "Dashboard" or "Widgets" => "Overlay",
            "Sessions" => "Settings",
            _ => page,
        };

        PageHost.Content = page switch
        {
            "Settings" => _settingsPage,
            "About" => _aboutPage,
            _ => _overlayPage,
        };

        if (page == "Overlay") _overlayPage.Refresh();
        if (page == "Settings") _settingsPage.Refresh();

        if (_navTabs.TryGetValue(page, out var tab) && tab.IsChecked != true)
            tab.IsChecked = true;

        AppSettings.Instance.LastNavPage = page;
        AppSettings.Instance.SaveQuiet();
    }

    // ── Single instance ───────────────────────────────────────────────

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

    // ── Updates ───────────────────────────────────────────────────────

    private async void CheckForUpdateStatusAsync()
    {
        try
        {
            bool hasUpdate = await _updateService.CheckForUpdatesAsync().ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() => _aboutPage.SetUpdateAvailable(hasUpdate));
        }
        catch (Exception ex)
        {
            AppLog.Warn("Update check failed", ex);
        }
    }

    // ── Session forwarding ────────────────────────────────────────────

    private int _sessionCheckCounter;

    private void OnTelemetryUpdatedForSession(object? sender, TelemetryData data)
    {
        // ~2 Hz. Session and pit transitions are human-timescale events, and this
        // must never block the telemetry thread.
        if (++_sessionCheckCounter % 30 != 0) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _sessionConfig.CheckSessionChange(data);
            CheckPlayerPitState(data);
        });
    }

    private void CheckPlayerPitState(TelemetryData data)
    {
        var settings = AppSettings.Instance;
        if (!settings.AutoHideInPitsEnabled) return;

        bool isInPits = data.OnPitRoad || data.PlayerCarInPitStall;

        if (isInPits && !_playerInPits)
        {
            _playerInPits = true;
            _autoHiddenWidgets.Clear();

            foreach (var kvp in settings.AutoHideInPitsWidgets)
            {
                if (!kvp.Value) continue;
                if (!Enum.TryParse<WidgetType>(kvp.Key, out var wt)) continue;

                foreach (var widget in _widgetManager.GetWidgetsByType(wt))
                {
                    if (!widget.UserWantsVisible) continue;
                    _autoHiddenWidgets.Add(widget.WidgetId);
                    widget.SetUserVisibility(false);
                }
            }
        }
        else if (!isInPits && _playerInPits)
        {
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
        // Non-blocking: this arrives on the telemetry thread and the body creates
        // and destroys windows and writes the layout.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            var matchedProfile = _sessionConfig.CheckProfileMatch();
            if (matchedProfile != null)
            {
                // The car becoming known re-raises this event; the same profile
                // must not rebuild every widget a second time.
                if (_profileService.ActiveProfileId == matchedProfile.Id) return;

                _profileService.ApplyProfile(matchedProfile.Id, _widgetManager);
                _logger.LogInformation("Applied profile: {Name} for {Category} / {Car}",
                    matchedProfile.Name, category, _sessionConfig.DetectedCarClass ?? "any car");
            }
            else if (_sessionConfig.IsEnabled)
            {
                var preset = _sessionConfig.GetPreset(category);
                if (preset != null)
                {
                    _widgetManager.ApplySessionPreset(preset);
                    _logger.LogInformation("Applied session preset: {Category}", category);
                }
            }
            _overlayPage.Refresh();
        });
    }

    // ── Status ────────────────────────────────────────────────────────

    private void OnTelemetryStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        Dispatcher.BeginInvoke(() => UpdateConnectionStatus(e.Status));
    }

    private void UpdateConnectionStatus(ConnectionStatus status)
    {
        ConnectionStatusText.Text = status switch
        {
            ConnectionStatus.Connected => "Connected",
            ConnectionStatus.Connecting => "Connecting",
            ConnectionStatus.Error => "Error",
            _ => "Disconnected",
        };

        var key = status switch
        {
            ConnectionStatus.Connected => "StatusConnected",
            ConnectionStatus.Connecting => "StatusConnecting",
            _ => "StatusDisconnected",
        };

        var brush = FindResource(key) as Brush ?? Brushes.Gray;
        ConnectionDot.Fill = brush;
        ConnectionStatusText.Foreground = brush;
    }

    /// <summary>
    /// Sample the interval between composition passes — the UI thread's real
    /// frame time, and the number that says whether the overlay is keeping up.
    /// </summary>
    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args) return;

        if (_lastRenderTime != TimeSpan.Zero)
        {
            double deltaMs = (args.RenderingTime - _lastRenderTime).TotalMilliseconds;
            if (deltaMs > 0 && deltaMs < 500)
            {
                _frameTimeTotalMs += deltaMs;
                _frameSamples++;
            }
        }

        _lastRenderTime = args.RenderingTime;
    }

    private void UpdateStatusBar()
    {
        var rate = _telemetryService.UpdateRate;
        UpdateRateText.Text = rate > 0 ? $"{rate:F0} Hz" : "0 Hz";

        var category = _sessionConfig.CurrentCategory;
        SessionTypeText.Text = category == SessionCategory.Unknown ? "" : category.ToString();
        StatusDivider.Visibility = SessionTypeText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Frame time
        double frameMs = _frameSamples > 0 ? _frameTimeTotalMs / _frameSamples : 0;
        _frameTimeTotalMs = 0;
        _frameSamples = 0;

        FrameTimeText.Text = frameMs > 0 ? $"{frameMs:F1} ms" : "—";
        FrameTimeText.Foreground = frameMs > 16.0
            ? (FindResource("Warn") as Brush ?? Brushes.Orange)
            : (FindResource("TealBright") as Brush ?? Brushes.Teal);

        // Dropped frames
        long dropped = _widgetManager.ActiveWidgets.Values.Sum(w => w.DroppedFrames);
        long delta = dropped - _lastDroppedFrames;
        _lastDroppedFrames = dropped;

        DroppedText.Text = delta > 0 ? $"{dropped} (+{delta})" : dropped.ToString();
        DroppedText.Foreground = delta > 30
            ? (FindResource("Warn") as Brush ?? Brushes.Orange)
            : (FindResource("TealBright") as Brush ?? Brushes.Teal);

        UpdateHotkeysDisplay();
    }

    // ── Hotkeys ───────────────────────────────────────────────────────

    /// <summary>Whether Windows accepted each binding, for display in Settings.</summary>
    public bool LockHotkeyRegistered { get; private set; }
    public bool VisibilityHotkeyRegistered { get; private set; }
    public bool ProfileHotkeyRegistered { get; private set; }

    /// <summary>Format a modifier + key pair for display, e.g. "Ctrl+L".</summary>
    public static string FormatHotkey(string modifier, string key)
    {
        var mod = modifier == "None" ? "" : modifier;
        return string.IsNullOrEmpty(mod) ? key : $"{mod}+{key}";
    }

    /// <summary>
    /// Register both global hotkeys and record whether Windows accepted them.
    /// Failure used to be discarded, leaving the feature silently dead while the
    /// UI still advertised it.
    /// </summary>
    public void RegisterGlobalHotkeys()
    {
        var s = AppSettings.Instance;

        _toggleLockHotkey?.Dispose();
        _toggleVisibilityHotkey?.Dispose();
        _cycleProfileHotkey?.Dispose();
        LockHotkeyRegistered = false;
        VisibilityHotkeyRegistered = false;
        ProfileHotkeyRegistered = false;

        if (s.HotkeysConflict())
        {
            _logger.LogWarning("Two hotkeys are bound to the same key");
            UpdateHotkeysDisplay();
            return;
        }

        var hotkeyLogger = _services.GetService<ILogger<GlobalHotkey>>();

        _toggleLockHotkey = new GlobalHotkey(this, hotkeyId: 9001, hotkeyLogger);
        _toggleLockHotkey.HotkeyPressed += (_, _) => Dispatcher.BeginInvoke(ToggleWidgetLock);
        LockHotkeyRegistered = _toggleLockHotkey.Register(s.ToggleLockModifier, s.ToggleLockKey);

        _toggleVisibilityHotkey = new GlobalHotkey(this, hotkeyId: 9002, hotkeyLogger);
        _toggleVisibilityHotkey.HotkeyPressed += (_, _) => Dispatcher.BeginInvoke(ToggleWidgetVisibility);
        VisibilityHotkeyRegistered = _toggleVisibilityHotkey.Register(s.ToggleVisibilityModifier, s.ToggleVisibilityKey);

        // Optional: an empty key means the driver has not asked for it.
        if (!string.IsNullOrEmpty(s.CycleProfileKey))
        {
            _cycleProfileHotkey = new GlobalHotkey(this, hotkeyId: 9003, hotkeyLogger);
            _cycleProfileHotkey.HotkeyPressed += (_, _) => Dispatcher.BeginInvoke(CycleProfile);
            ProfileHotkeyRegistered = _cycleProfileHotkey.Register(s.CycleProfileModifier, s.CycleProfileKey);
        }

        bool profileWanted = !string.IsNullOrEmpty(s.CycleProfileKey);
        if (!LockHotkeyRegistered || !VisibilityHotkeyRegistered || (profileWanted && !ProfileHotkeyRegistered))
        {
            _logger.LogWarning(
                "Hotkey registration failed (lock: {Lock}, visibility: {Vis}, profile: {Prof}) — another application may own the combination",
                LockHotkeyRegistered, VisibilityHotkeyRegistered, ProfileHotkeyRegistered);
        }

        UpdateHotkeysDisplay();
    }

    private void UpdateHotkeysDisplay()
    {
        var s = AppSettings.Instance;
        string lockHk = FormatHotkey(s.ToggleLockModifier, s.ToggleLockKey);
        string visHk = FormatHotkey(s.ToggleVisibilityModifier, s.ToggleVisibilityKey);

        string lockLabel = LockHotkeyRegistered ? lockHk : $"{lockHk} unavailable";
        string visLabel = VisibilityHotkeyRegistered ? visHk : $"{visHk} unavailable";

        bool profileWanted = !string.IsNullOrEmpty(s.CycleProfileKey);
        string profLabel = profileWanted
            ? $"  ·  {FormatHotkey(s.CycleProfileModifier, s.CycleProfileKey)}{(ProfileHotkeyRegistered ? "" : " unavailable")} profile"
            : "";

        HotkeysText.Text = $"{lockLabel} lock  ·  {visLabel} show/hide{profLabel}";
        HotkeysText.Foreground = LockHotkeyRegistered && VisibilityHotkeyRegistered && (!profileWanted || ProfileHotkeyRegistered)
            ? (FindResource("TextMuted") as Brush ?? Brushes.Gray)
            : (FindResource("Warn") as Brush ?? Brushes.Orange);
    }

    private void ToggleWidgetLock()
    {
        var s = AppSettings.Instance;
        s.LockWindows = !s.LockWindows;
        s.Save();
        _widgetManager.LockAllWidgets(s.LockWindows);
        _overlayPage.Refresh();
    }

    private void ToggleWidgetVisibility()
    {
        _widgetManager.ToggleAllWidgets();
        _overlayPage.Refresh();
    }

    /// <summary>Step to the next saved profile — the wheel-button path to a different layout.</summary>
    private void CycleProfile()
    {
        var next = _profileService.NextProfile();
        if (next == null) return;

        _profileService.ApplyProfile(next.Id, _widgetManager);
        _overlayPage.Refresh();
        _logger.LogInformation("Cycled to profile: {Name}", next.Name);
    }

    // ── Window lifecycle ──────────────────────────────────────────────

    private void OnSettingsChanged() => Topmost = AppSettings.Instance.AlwaysOnTop;

    private void ApplyWindowSettings()
    {
        var s = AppSettings.Instance;
        Width = Math.Max(s.WindowWidth, MinWidth);
        Height = Math.Max(s.WindowHeight, MinHeight);

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

    private static bool IsPositionOnScreen(double left, double top) =>
        left >= SystemParameters.VirtualScreenLeft - 100 &&
        left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth + 100 &&
        top >= SystemParameters.VirtualScreenTop - 100 &&
        top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight + 100;

    /// <summary>
    /// Record window geometry. Called on every mouse-move while dragging, so it
    /// uses SaveQuiet — the write is coalesced onto a background thread and no
    /// listener is notified.
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

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var s = AppSettings.Instance;
        if (!_forceClose && s.MinimizeToTray && s.CloseToTray)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            return;
        }
        SaveWindowState();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        var s = AppSettings.Instance;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        s.SaveQuiet();

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

    protected override void OnClosed(EventArgs e)
    {
        _statusTimer.Stop();
        CompositionTarget.Rendering -= OnRendering;

        _telemetryService.StatusChanged -= OnTelemetryStatusChanged;
        _telemetryService.TelemetryUpdated -= OnTelemetryUpdatedForSession;
        _sessionConfig.SessionCategoryChanged -= OnSessionCategoryChanged;
        _sessionConfig.Save();

        _toggleLockHotkey?.Dispose();
        _toggleVisibilityHotkey?.Dispose();

        // Snapshot while the widgets still exist, then force both debounced
        // writers to disk before the process goes away.
        _widgetManager.SaveCurrentLayout();
        _widgetManager.FlushLayout();
        AppSettings.Flush();

        _widgetManager.RemoveAllWidgets();
        _trayIcon.Dispose();
        (_settingsPage as IDisposable)?.Dispose();

        System.Windows.Application.Current.Shutdown();
        base.OnClosed(e);
    }
}
