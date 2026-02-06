using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Services;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF;

public partial class MainWindow : Window
{
    private readonly WidgetManager _widgetManager;
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<MainWindow> _logger;
    private readonly DispatcherTimer _updateRateTimer;
    private GlobalHotkey? _toggleLockHotkey;
    private GlobalHotkey? _toggleVisibilityHotkey;

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();

        _widgetManager = services.GetRequiredService<WidgetManager>();
        _telemetryService = services.GetRequiredService<ITelemetryService>();
        _logger = services.GetRequiredService<ILogger<MainWindow>>();

        _telemetryService.StatusChanged += OnTelemetryStatusChanged;
        Closing += MainWindow_Closing;
        LocationChanged += MainWindow_LocationChanged;
        SizeChanged += MainWindow_SizeChanged;
        StateChanged += MainWindow_StateChanged;
        Loaded += MainWindow_Loaded;

        // Update rate display timer
        _updateRateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _updateRateTimer.Tick += (_, _) => UpdateUpdateRateDisplay();
        _updateRateTimer.Start();

        // Apply saved window settings and load widget layout
        ApplyWindowSettings();
        _widgetManager.LoadSavedLayout();

        // Initialize status
        UpdateConnectionStatus(_telemetryService.Status);
        TitleVersionText.Text = VersionInfo.DisplayVersion;
        UpdateHotkeysDisplay();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RegisterGlobalHotkeys();
    }

    // ── Button handlers ─────────────────────────────────────────────────
    
    private void BtnToggleMRTOne_Click(object sender, RoutedEventArgs e)
    {
        if (_widgetManager.HasWidgetType(WidgetType.MRTOne))
        {
            // Remove existing MRT One widgets
            var widgets = _widgetManager.GetWidgetsByType(WidgetType.MRTOne);
            foreach (var w in widgets)
                _widgetManager.RemoveWidget(w.WidgetId);
            BtnToggleMRTOne.Content = "Show MRT One";
            WidgetStatusText.Text = "MRT One widget closed.";
        }
        else
        {
            _widgetManager.CreateWidget(WidgetType.MRTOne);
            BtnToggleMRTOne.Content = "Hide MRT One";
            WidgetStatusText.Text = "MRT One widget is active.";
        }
    }

    private void BtnLockWidgets_Click(object sender, RoutedEventArgs e)
    {
        ToggleWidgetLock();
    }

    // ── Connection status ───────────────────────────────────────────────

    private void OnTelemetryStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        Dispatcher.Invoke(() => UpdateConnectionStatus(e.Status));
    }

    private void UpdateConnectionStatus(ConnectionStatus status)
    {
        ConnectionStatusText.Text = status switch
        {
            ConnectionStatus.Connected => "Connected",
            ConnectionStatus.Connecting => "Connecting...",
            ConnectionStatus.Disconnected => "Disconnected",
            _ => "Unknown"
        };

        ConnectionStatusText.Foreground = new SolidColorBrush(status switch
        {
            ConnectionStatus.Connected => Color.FromRgb(0, 188, 212),
            ConnectionStatus.Connecting => Color.FromRgb(255, 152, 0),
            _ => Color.FromRgb(136, 136, 136)
        });
    }

    private void UpdateUpdateRateDisplay()
    {
        var rate = _telemetryService.UpdateRate;
        UpdateRateText.Text = rate > 0 ? $"{rate:F0} Hz" : "0 Hz";
    }

    // ── Global hotkeys ──────────────────────────────────────────────────

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

        HotkeysText.Text = $"{lockHk} Lock | {visHk} Show/Hide";
    }

    private void RegisterGlobalHotkeys()
    {
        var settings = AppSettings.Instance;

        if (settings.HotkeysConflict())
        {
            MessageBox.Show(
                "Toggle Lock and Toggle Visibility hotkeys conflict.\nPlease change one in settings.",
                "Hotkey Conflict", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _toggleLockHotkey = new GlobalHotkey(this, hotkeyId: 9001);
        _toggleLockHotkey.HotkeyPressed += (_, _) => Dispatcher.Invoke(ToggleWidgetLock);
        _toggleLockHotkey.Register(settings.ToggleLockModifier, settings.ToggleLockKey);

        _toggleVisibilityHotkey = new GlobalHotkey(this, hotkeyId: 9002);
        _toggleVisibilityHotkey.HotkeyPressed += (_, _) => Dispatcher.Invoke(ToggleWidgetVisibility);
        _toggleVisibilityHotkey.Register(settings.ToggleVisibilityModifier, settings.ToggleVisibilityKey);
    }

    private void ToggleWidgetLock()
    {
        var settings = AppSettings.Instance;
        settings.LockWindows = !settings.LockWindows;
        settings.Save();
        _widgetManager.LockAllWidgets(settings.LockWindows);
        BtnLockWidgets.Content = settings.LockWindows ? "Unlock Widgets" : "Lock Widgets";
    }

    private void ToggleWidgetVisibility()
    {
        _widgetManager.ToggleAllWidgets();
    }

    // ── Window state persistence ────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        _telemetryService.StatusChanged -= OnTelemetryStatusChanged;
        _toggleLockHotkey?.Dispose();
        _toggleVisibilityHotkey?.Dispose();
        _widgetManager.SaveCurrentLayout();
        _widgetManager.RemoveAllWidgets();
        System.Windows.Application.Current.Shutdown();
        base.OnClosed(e);
    }

    private void ApplyWindowSettings()
    {
        var settings = AppSettings.Instance;
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;

        if (settings.WindowLeft.HasValue && settings.WindowTop.HasValue &&
            IsPositionOnScreen(settings.WindowLeft.Value, settings.WindowTop.Value))
        {
            Left = settings.WindowLeft.Value;
            Top = settings.WindowTop.Value;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        if (settings.WindowMaximized) WindowState = WindowState.Maximized;
        if (settings.StartMinimized) WindowState = WindowState.Minimized;
        Topmost = settings.AlwaysOnTop;
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
        var settings = AppSettings.Instance;
        settings.WindowMaximized = WindowState == WindowState.Maximized;
        settings.Save();
    }

    private void SaveWindowState()
    {
        if (!IsLoaded) return;
        var settings = AppSettings.Instance;
        if (WindowState == WindowState.Normal)
        {
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            settings.WindowLeft = Left;
            settings.WindowTop = Top;
        }
        settings.WindowMaximized = WindowState == WindowState.Maximized;
        settings.Save();
    }
}
