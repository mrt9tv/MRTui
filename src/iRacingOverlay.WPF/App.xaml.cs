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
/// Application entry point — sets up DI and launches the main window.
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
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<ITelemetryService, IRacingTelemetryService>();
                services.AddSingleton<FuelCalculatorService>();
                services.AddSingleton<WidgetManager>();
                services.AddSingleton<SessionConfigService>();
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

        // Wire fuel calculator to telemetry updates
        var fuelCalculatorService = _host.Services.GetRequiredService<FuelCalculatorService>();
        telemetryService.TelemetryUpdated += (_, data) => fuelCalculatorService.Update(data);

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

