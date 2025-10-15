using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Services;
using iRacingOverlay.WPF.Views;
using iRacingOverlay.WPF.ViewModels;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF;

public partial class MainWindow : Window
{
    private readonly WidgetManager _widgetManager;
    private readonly ITelemetryService _telemetryService;
    private readonly IServiceProvider _services;
    private readonly DispatcherTimer _uptimeTimer;

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

        // Setup uptime timer for status bar
        _uptimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uptimeTimer.Tick += (s, e) => UpdateUptimeDisplay();
        _uptimeTimer.Start();

        // Load and apply saved settings
        ApplyWindowSettings();

        // Initialize status bar with current connection status
        UpdateConnectionStatus(_telemetryService.Status);

        // Set version text from centralized source (full version)
        VersionText.Text = VersionInfo.FullVersion;

        NavigateToHome();
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
    }

    private void UpdateUptimeDisplay()
    {
        UptimeText.Text = ApplicationInfo.GetUptimeFormatted();
    }

    private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F12)
        {
            MessageBox.Show("Debug overlay hotkey (F12)\n\nFeature coming soon!", 
                "Debug Overlay", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _telemetryService.StatusChanged -= OnTelemetryStatusChanged;
        
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
}
