using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Services;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize application start time (single source of truth for uptime)
        ApplicationInfo.ApplicationStartTime = DateTime.Now;

        // Build dependency injection container
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register telemetry service
                services.AddSingleton<ITelemetryService, IRacingTelemetryService>();
                
                // Register widget manager
                services.AddSingleton<WidgetManager>();
                
                // Register logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();

        // Start telemetry service
        var telemetryService = _host.Services.GetRequiredService<ITelemetryService>();
        _ = telemetryService.ConnectAsync();

        // Start monitoring in background
        if (telemetryService is IRacingTelemetryService racingService)
        {
            _ = racingService.MonitorAsync(default);
        }

        // Show main window
        var mainWindow = new MainWindow(_host.Services);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}

