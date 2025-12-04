using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.Core.Services.Tire;
using iRacingOverlay.WPF.Services;
using iRacingOverlay.WPF.Views;
using iRacingOverlay.WPF.ViewModels;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Utils;
using iRacingOverlay.WPF.Widgets.PitStrategyWindow;
using iRacingOverlay.WPF.Widgets.RaceStrategy;

namespace iRacingOverlay.WPF;

public partial class MainWindow : Window
{
    private readonly WidgetManager _widgetManager;
    private readonly ITelemetryService _telemetryService;
    private readonly IServiceProvider _services;
    private readonly DispatcherTimer _uptimeTimer;
    private readonly DispatcherTimer _updateRateTimer;
    private GlobalHotkey? _toggleLockHotkey;
    private GlobalHotkey? _toggleVisibilityHotkey;
    private PitStrategyWindow? _pitStrategyWindow;

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();
        
        _services = services;
        _widgetManager = services.GetRequiredService<WidgetManager>();
        _telemetryService = services.GetRequiredService<ITelemetryService>();

        _telemetryService.StatusChanged += OnTelemetryStatusChanged;
        KeyDown += MainWindow_KeyDown;
        Closing += MainWindow_Closing;
        LocationChanged += MainWindow_LocationChanged;
        SizeChanged += MainWindow_SizeChanged;
        StateChanged += MainWindow_StateChanged;
        Loaded += MainWindow_Loaded;

        // Setup uptime timer for status bar (removed - replaced with update rate)
        _uptimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uptimeTimer.Tick += (s, e) => UpdateUptimeDisplay();
        _uptimeTimer.Start();

        // Setup update rate timer for status bar
        _updateRateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500) // Update twice per second for smooth display
        };
        _updateRateTimer.Tick += (s, e) => UpdateUpdateRateDisplay();
        _updateRateTimer.Start();

        // Load and apply saved settings
        ApplyWindowSettings();

        // Load saved layout (widgets and their configurations)
        // If no saved layout exists, user can manually create widgets via Overlay Manager
        _widgetManager.LoadSavedLayout();

        // Initialize status bar with current connection status
        UpdateConnectionStatus(_telemetryService.Status);

        // Set version text in title bar from centralized source
        TitleVersionText.Text = VersionInfo.DisplayVersion;

        // Set hotkeys text in status bar
        UpdateHotkeysDisplay();

        NavigateToHome();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Register global hotkeys after window is loaded
        System.Diagnostics.Debug.WriteLine("=== MainWindow_Loaded - About to register hotkeys ===");
        RegisterGlobalHotkeys();
        System.Diagnostics.Debug.WriteLine("=== MainWindow_Loaded - Hotkey registration complete ===");
    }

    private void BtnHome_Click(object sender, RoutedEventArgs e) => NavigateToHome();
    private void BtnDashboard_Click(object sender, RoutedEventArgs e) => NavigateToDashboard();
    private void BtnOverlay_Click(object sender, RoutedEventArgs e) => NavigateToOverlay();
    private void BtnSettings_Click(object sender, RoutedEventArgs e) => NavigateToSettings();

    private void NavigateToHome()
    {
        ContentFrame.Content = new TextBlock
        {
            Text = "🏠 Home\n\nWelcome to MRT UI!\n\nReserved for future features...",
            FontSize = 24,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 128, 128)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        SetActiveButton(BtnHome);
    }

    private void NavigateToDashboard()
    {
        var viewModel = new DashboardViewModel(_telemetryService, _widgetManager);
        var dashboardView = new DashboardView(viewModel);
        ContentFrame.Content = dashboardView;
        SetActiveButton(BtnDashboard);
    }

    public void NavigateToOverlayPage()
    {
        NavigateToOverlay();
    }

    private void NavigateToOverlay()
    {
        var viewModel = new OverlayViewModel(_widgetManager);
        var overlayView = new OverlayView(viewModel);
        ContentFrame.Content = overlayView;
        SetActiveButton(BtnOverlay);
    }

    private void NavigateToSettings()
    {
        var settings = Models.AppSettings.Instance;
        var viewModel = new SettingsViewModel(settings, _widgetManager, this);
        var settingsView = new SettingsView(viewModel);
        ContentFrame.Content = settingsView;
        SetActiveButton(BtnSettings);
    }

    private void SetActiveButton(System.Windows.Controls.Button activeButton)
    {
        BtnHome.Tag = null;
        BtnDashboard.Tag = null;
        BtnOverlay.Tag = null;
        BtnSettings.Tag = null;
        activeButton.Tag = "Active";
    }

    private void OnTelemetryStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        Dispatcher.Invoke(() => UpdateConnectionStatus(e.Status));
    }

    private void UpdateConnectionStatus(ConnectionStatus status)
    {
        ConnectionStatusText.Text = status switch
        {
            ConnectionStatus.Connected => "🏁 Connected",
            ConnectionStatus.Connecting => "🤞 Connecting...",
            ConnectionStatus.Disconnected => "🤌 Disconnected",
            _ => "❓ Unknown"
        };

        // Color code the status: Teal = Connected, Orange = Connecting, Gray = Disconnected
        ConnectionStatusText.Foreground = new SolidColorBrush(status switch
        {
            ConnectionStatus.Connected => Color.FromRgb(0, 188, 212),    // TealPrimary
            ConnectionStatus.Connecting => Color.FromRgb(255, 152, 0),   // OrangePrimary
            ConnectionStatus.Disconnected => Color.FromRgb(136, 136, 136), // Gray
            _ => Color.FromRgb(136, 136, 136)
        });
    }

    private void UpdateUptimeDisplay()
    {
        // Kept for backward compatibility but not displayed in UI anymore
    }

    private void UpdateUpdateRateDisplay()
    {
        var rate = _telemetryService.UpdateRate;
        UpdateRateText.Text = rate > 0 ? $"{rate:F0} Hz" : "0 Hz";
    }

    private void UpdateHotkeysDisplay()
    {
        var settings = AppSettings.Instance;
        
        // Format modifier keys nicely (e.g., "Ctrl" instead of "Control")
        string FormatModifier(string modifier) => modifier switch
        {
            "None" => "",
            _ => modifier
        };

        string lockMod = FormatModifier(settings.ToggleLockModifier);
        string lockKey = settings.ToggleLockKey;
        string lockHotkey = string.IsNullOrEmpty(lockMod) ? lockKey : $"{lockMod}+{lockKey}";

        string visMod = FormatModifier(settings.ToggleVisibilityModifier);
        string visKey = settings.ToggleVisibilityKey;
        string visHotkey = string.IsNullOrEmpty(visMod) ? visKey : $"{visMod}+{visKey}";

        HotkeysText.Text = $"{lockHotkey} Lock | {visHotkey} Show/Hide";
    }

    public void RefreshHotkeysDisplay()
    {
        UpdateHotkeysDisplay();
    }

    private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Handle toggle lock hotkey
        if (CheckHotkeyMatch(e))
        {
            ToggleWidgetLock();
            e.Handled = true;
            return;
        }

        // Legacy debug hotkey
        if (e.Key == System.Windows.Input.Key.F12)
        {
            MessageBox.Show("Debug overlay hotkey (F12)\n\nFeature coming soon!",
                "Debug Overlay", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// Check if the current key event matches the configured hotkey
    /// </summary>
    private bool CheckHotkeyMatch(System.Windows.Input.KeyEventArgs e)
    {
        var settings = AppSettings.Instance;

        // Get the actual key (not modifier)
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;

        // Check if main key matches
        if (key.ToString() != settings.ToggleLockKey)
            return false;

        // Check if modifier matches
        bool hasCtrl = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control);
        bool hasAlt = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt);
        bool hasShift = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift);

        return settings.ToggleLockModifier switch
        {
            "Ctrl" => hasCtrl && !hasAlt && !hasShift,
            "Alt" => !hasCtrl && hasAlt && !hasShift,
            "Shift" => !hasCtrl && !hasAlt && hasShift,
            "None" => !hasCtrl && !hasAlt && !hasShift,
            _ => false
        };
    }

    /// <summary>
    /// Toggle widget lock state via hotkey
    /// </summary>
    /// <summary>
    /// Register both global hotkeys based on current settings
    /// </summary>
    private void RegisterGlobalHotkeys()
    {
        var settings = AppSettings.Instance;

        System.Diagnostics.Debug.WriteLine($"RegisterGlobalHotkeys - Lock: {settings.ToggleLockModifier} + {settings.ToggleLockKey}");
        System.Diagnostics.Debug.WriteLine($"RegisterGlobalHotkeys - Visibility: {settings.ToggleVisibilityModifier} + {settings.ToggleVisibilityKey}");

        // Check for conflicting hotkeys
        if (settings.HotkeysConflict())
        {
            System.Diagnostics.Debug.WriteLine("ERROR: Hotkeys conflict!");
            MessageBox.Show(
                "Toggle Lock and Toggle Visibility hotkeys cannot be the same!\n\n" +
                $"Both are currently set to: {settings.ToggleLockModifier} + {settings.ToggleLockKey}\n\n" +
                "Please change one of them in Settings > Widgets.",
                "Hotkey Conflict",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        // Create and register toggle lock hotkey
        _toggleLockHotkey = new GlobalHotkey(this, hotkeyId: 9001);
        _toggleLockHotkey.HotkeyPressed += OnToggleLockHotkeyPressed;

        bool lockSuccess = _toggleLockHotkey.Register(settings.ToggleLockModifier, settings.ToggleLockKey);

        System.Diagnostics.Debug.WriteLine($"Toggle Lock registration result: {lockSuccess}");

        if (!lockSuccess)
        {
            MessageBox.Show(
                $"Failed to register Toggle Lock hotkey: {settings.ToggleLockModifier} + {settings.ToggleLockKey}\n\n" +
                "The hotkey may already be in use by another application.",
                "Hotkey Registration Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // Create and register toggle visibility hotkey
        _toggleVisibilityHotkey = new GlobalHotkey(this, hotkeyId: 9002);
        _toggleVisibilityHotkey.HotkeyPressed += OnToggleVisibilityHotkeyPressed;

        bool visibilitySuccess = _toggleVisibilityHotkey.Register(settings.ToggleVisibilityModifier, settings.ToggleVisibilityKey);

        System.Diagnostics.Debug.WriteLine($"Toggle Visibility registration result: {visibilitySuccess}");

        if (!visibilitySuccess)
        {
            MessageBox.Show(
                $"Failed to register Toggle Visibility hotkey: {settings.ToggleVisibilityModifier} + {settings.ToggleVisibilityKey}\n\n" +
                "The hotkey may already be in use by another application.",
                "Hotkey Registration Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Re-register both global hotkeys with new settings
    /// Called when user changes hotkeys in Settings
    /// </summary>
    public void ReregisterGlobalHotkeys()
    {
        var settings = AppSettings.Instance;

        // Check for conflicting hotkeys
        if (settings.HotkeysConflict())
        {
            MessageBox.Show(
                "Toggle Lock and Toggle Visibility hotkeys cannot be the same!\n\n" +
                $"Both are currently set to: {settings.ToggleLockModifier} + {settings.ToggleLockKey}\n\n" +
                "Please change one of them.",
                "Hotkey Conflict",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        // Re-register toggle lock hotkey
        if (_toggleLockHotkey != null)
        {
            bool success = _toggleLockHotkey.Register(settings.ToggleLockModifier, settings.ToggleLockKey);

            if (success)
            {
                System.Diagnostics.Debug.WriteLine($"Toggle Lock hotkey re-registered: {settings.ToggleLockModifier} + {settings.ToggleLockKey}");
            }
            else
            {
                MessageBox.Show(
                    $"Failed to register Toggle Lock hotkey: {settings.ToggleLockModifier} + {settings.ToggleLockKey}\n\n" +
                    "The hotkey may already be in use by another application.",
                    "Hotkey Registration Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // Re-register toggle visibility hotkey
        if (_toggleVisibilityHotkey != null)
        {
            bool success = _toggleVisibilityHotkey.Register(settings.ToggleVisibilityModifier, settings.ToggleVisibilityKey);

            if (success)
            {
                System.Diagnostics.Debug.WriteLine($"Toggle Visibility hotkey re-registered: {settings.ToggleVisibilityModifier} + {settings.ToggleVisibilityKey}");
            }
            else
            {
                MessageBox.Show(
                    $"Failed to register Toggle Visibility hotkey: {settings.ToggleVisibilityModifier} + {settings.ToggleVisibilityKey}\n\n" +
                    "The hotkey may already be in use by another application.",
                    "Hotkey Registration Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // Refresh the hotkeys display in status bar
        UpdateHotkeysDisplay();
    }

    /// <summary>
    /// Called when toggle lock hotkey is pressed (works even when app is not focused)
    /// </summary>
    private void OnToggleLockHotkeyPressed(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() => ToggleWidgetLock());
    }

    /// <summary>
    /// Called when toggle visibility hotkey is pressed (works even when app is not focused)
    /// </summary>
    private void OnToggleVisibilityHotkeyPressed(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() => ToggleWidgetVisibility());
    }

    /// <summary>
    /// Toggle widget lock state via hotkey
    /// </summary>
    private void ToggleWidgetLock()
    {
        var settings = AppSettings.Instance;
        settings.LockWindows = !settings.LockWindows;
        settings.Save();

        // Apply lock state to all widgets immediately
        _widgetManager.LockAllWidgets(settings.LockWindows);

        System.Diagnostics.Debug.WriteLine($"Widgets {(settings.LockWindows ? "locked" : "unlocked")} via global hotkey");
    }

    /// <summary>
    /// Toggle widget visibility via hotkey
    /// </summary>
    private void ToggleWidgetVisibility()
    {
        _widgetManager.ToggleAllWidgets();
        System.Diagnostics.Debug.WriteLine("Widget visibility toggled via global hotkey");
    }

    protected override void OnClosed(EventArgs e)
    {
        _telemetryService.StatusChanged -= OnTelemetryStatusChanged;

        // Unregister global hotkeys
        _toggleLockHotkey?.Dispose();
        _toggleVisibilityHotkey?.Dispose();

        // Close pit strategy window
        if (_pitStrategyWindow != null)
        {
            _pitStrategyWindow.Closing -= null; // Remove handler to allow actual close
            _pitStrategyWindow.Close();
        }

        // Save current layout before closing
        _widgetManager.SaveCurrentLayout();

        // Close all widget windows before shutting down
        _widgetManager.RemoveAllWidgets();

        // Ensure application shuts down completely
        System.Windows.Application.Current.Shutdown();

        base.OnClosed(e);
    }

    #region Window State Management

    /// <summary>
    /// Apply saved window settings (size, position, state)
    /// </summary>
    private void ApplyWindowSettings()
    {
        var settings = AppSettings.Instance;

        // Apply size
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;

        // Apply position (center if first launch, otherwise use saved position)
        if (settings.WindowLeft.HasValue && settings.WindowTop.HasValue)
        {
            // Check if saved position is still valid (monitor might be disconnected)
            if (IsPositionOnScreen(settings.WindowLeft.Value, settings.WindowTop.Value))
            {
                Left = settings.WindowLeft.Value;
                Top = settings.WindowTop.Value;
                WindowStartupLocation = WindowStartupLocation.Manual;
            }
            else
            {
                // Monitor disconnected - center on primary monitor
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }
        else
        {
            // First launch - center on primary monitor
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        // Apply maximized state
        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }

        // Apply start minimized (only if setting is enabled)
        if (settings.StartMinimized)
        {
            WindowState = WindowState.Minimized;
        }

        // Apply opacity (always 100%) and topmost
        Opacity = 1.0;
        Topmost = settings.AlwaysOnTop;
    }

    /// <summary>
    /// Check if a window position is visible on screen (simplified - uses primary screen bounds)
    /// </summary>
    private bool IsPositionOnScreen(double left, double top)
    {
        // Simple check: is the window top-left corner within the virtual screen bounds?
        return left >= SystemParameters.VirtualScreenLeft && 
               left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
               top >= SystemParameters.VirtualScreenTop &&
               top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
    }

    /// <summary>
    /// Save window state when closing
    /// </summary>
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveWindowState();
    }

    /// <summary>
    /// Save window position when moved
    /// </summary>
    private void MainWindow_LocationChanged(object? sender, EventArgs e)
    {
        // Don't save position if minimized or maximized
        if (WindowState == WindowState.Normal)
        {
            SaveWindowState();
        }
    }

    /// <summary>
    /// Save window size when resized
    /// </summary>
    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Don't save size if minimized or maximized
        if (WindowState == WindowState.Normal)
        {
            SaveWindowState();
        }
    }

    /// <summary>
    /// Save window state when maximized/restored
    /// </summary>
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        var settings = AppSettings.Instance;
        settings.WindowMaximized = (WindowState == WindowState.Maximized);
        settings.Save();
    }

    /// <summary>
    /// Save current window state to settings
    /// </summary>
    private void SaveWindowState()
    {
        // Don't save if window is being initialized
        if (!IsLoaded) return;

        var settings = AppSettings.Instance;

        // Save size and position (only when in normal state)
        if (WindowState == WindowState.Normal)
        {
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            settings.WindowLeft = Left;
            settings.WindowTop = Top;
        }

        settings.WindowMaximized = (WindowState == WindowState.Maximized);
        settings.Save();
    }

    #endregion

    #region Pit Strategy Window Management

    /// <summary>
    /// Show the standalone Pit Strategy Window
    /// </summary>
    public void ShowPitStrategyWindow()
    {
        if (_pitStrategyWindow == null)
        {
            var fuelService = _services.GetRequiredService<FuelCalculatorService>();
            _pitStrategyWindow = new PitStrategyWindow(fuelService);
        }

        _pitStrategyWindow.Show();
        _pitStrategyWindow.Activate(); // Bring to front

        // Update setting
        var settings = AppSettings.Instance;
        settings.ShowPitStrategyWindow = true;
        settings.Save();
    }

    /// <summary>
    /// Hide the Pit Strategy Window
    /// </summary>
    public void HidePitStrategyWindow()
    {
        _pitStrategyWindow?.Hide();

        // Update setting
        var settings = AppSettings.Instance;
        settings.ShowPitStrategyWindow = false;
        settings.Save();
    }

    /// <summary>
    /// Toggle Pit Strategy Window visibility
    /// </summary>
    public void TogglePitStrategyWindow()
    {
        if (_pitStrategyWindow == null || !_pitStrategyWindow.IsVisible)
        {
            ShowPitStrategyWindow();
        }
        else
        {
            HidePitStrategyWindow();
        }
    }

    #endregion
}
