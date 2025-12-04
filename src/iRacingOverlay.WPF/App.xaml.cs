using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using iRacingOverlay.Core.Services;
using iRacingOverlay.Core.Services.Tire;
using iRacingOverlay.WPF.Services;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;
    private static StreamWriter? _logWriter;

    // Import Windows API to allocate console
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllocConsole();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Setup log file
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wheel_lockup_debug.log");
        _logWriter = new StreamWriter(logPath, false) { AutoFlush = true };
        
        // Redirect Console.Out to both console and file
        var multiWriter = new MultiTextWriter(Console.Out, _logWriter);
        Console.SetOut(multiWriter);

        // Allocate console window for diagnostic output
        AllocConsole();
        Console.WriteLine("=== iRacing Overlay Debug Console ===");
        Console.WriteLine($"Started: {DateTime.Now}");
        Console.WriteLine($"Log file: {logPath}");
        Console.WriteLine("Wheel Lockup Diagnostics: ENABLED\n");

        // Initialize application start time (single source of truth for uptime)
        ApplicationInfo.ApplicationStartTime = DateTime.Now;

        // Build dependency injection container
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register telemetry service
                services.AddSingleton<ITelemetryService, IRacingTelemetryService>();

                // Register fuel calculator service
                services.AddSingleton<FuelCalculatorService>();

                // Register tire strategy service (Phase 8)
                services.AddSingleton<TireStrategyService>();

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

        // Wire up fuel calculator service to telemetry updates
        var fuelCalculatorService = _host.Services.GetRequiredService<FuelCalculatorService>();
        telemetryService.TelemetryUpdated += (sender, data) =>
        {
            fuelCalculatorService.Update(data);
        };

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
        _logWriter?.Dispose();
        _host?.Dispose();
        base.OnExit(e);
    }
}

/// <summary>
/// TextWriter that writes to multiple destinations simultaneously
/// </summary>
public class MultiTextWriter : TextWriter
{
    private readonly TextWriter[] _writers;

    public MultiTextWriter(params TextWriter[] writers)
    {
        _writers = writers;
    }

    public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

    public override void Write(char value)
    {
        foreach (var writer in _writers)
            writer.Write(value);
    }

    public override void Write(string? value)
    {
        foreach (var writer in _writers)
            writer.Write(value);
    }

    public override void WriteLine(string? value)
    {
        foreach (var writer in _writers)
            writer.WriteLine(value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var writer in _writers)
                writer?.Dispose();
        }
        base.Dispose(disposing);
    }
}

