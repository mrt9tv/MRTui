using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Services;
using iRacingOverlay.WPF.Views;
using iRacingOverlay.WPF.ViewModels;

namespace iRacingOverlay.WPF;

public partial class MainWindow : Window
{
    private readonly WidgetManager _widgetManager;
    private readonly ITelemetryService _telemetryService;
    private readonly IServiceProvider _services;

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();
        
        _services = services;
        _widgetManager = services.GetRequiredService<WidgetManager>();
        _telemetryService = services.GetRequiredService<ITelemetryService>();

        _telemetryService.StatusChanged += OnTelemetryStatusChanged;
        KeyDown += MainWindow_KeyDown;

        // Initialize status bar with current connection status
        UpdateConnectionStatus(_telemetryService.Status);

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

    private void SetActiveButton(Button activeButton)
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
        base.OnClosed(e);
    }
}
