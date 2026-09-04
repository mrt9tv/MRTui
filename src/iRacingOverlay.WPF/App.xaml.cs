using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Services;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF;

/// <summary>
/// Application entry point — sets up DI and launches the main window.
/// Velopack hooks run in Program.cs Main() before this.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    /// <summary>Cancels telemetry monitoring on shutdown so the SDK loop stops cleanly.</summary>
    private readonly CancellationTokenSource _shutdownCts = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // File logging first: everything below should be diagnosable from the log.
        AppLog.Start();
        InstallGlobalExceptionHandlers();

        // Initialize application start time (single source of truth for uptime)
        ApplicationInfo.ApplicationStartTime = DateTime.Now;

        // Force settings to load before any window reads them, and log the outcome.
        _ = Models.AppSettings.Load();

        // Build dependency injection container
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<ITelemetryService, IRacingTelemetryService>();
                services.AddSingleton<FuelCalculatorService>();
                services.AddSingleton<WidgetManager>();
                services.AddSingleton<SessionConfigService>();
                services.AddSingleton<ProfileStorageService>();
                services.AddSingleton<UpdateService>();
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    // Console output goes nowhere in a WinExe — route ILogger to the log file
                    // so LogError calls across Core and the services are actually recoverable.
                    builder.AddProvider(new FileLoggerProvider());
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();

        // Start telemetry service
        var telemetryService = _host.Services.GetRequiredService<ITelemetryService>();
        StartTelemetry(telemetryService);

        // NOTE: FuelCalculatorService.Update() is already called inside
        // IRacingTelemetryService.OnTelemetryUpdate — no need to subscribe again here.
        // Duplicate subscription was causing double fuel calculation at 60Hz.

        // Show main window
        var mainWindow = new MainWindow(_host.Services);
        mainWindow.Show();
    }

    /// <summary>
    /// Connect and start monitoring, observing both tasks so a failure is logged
    /// rather than silently swallowed as an unobserved task exception.
    /// </summary>
    private void StartTelemetry(ITelemetryService telemetryService)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await telemetryService.ConnectAsync(_shutdownCts.Token).ConfigureAwait(false);

                if (telemetryService is IRacingTelemetryService racingService)
                    await racingService.MonitorAsync(_shutdownCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                AppLog.Info("Telemetry monitoring stopped (shutdown)");
            }
            catch (Exception ex)
            {
                AppLog.Error("Telemetry monitoring stopped unexpectedly", ex);
            }
        });
    }

    /// <summary>
    /// Catch everything that would otherwise close the app without a word.
    /// </summary>
    private void InstallGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled exception on the UI thread", args.Exception);

            // Keep the app alive: a single widget's render fault should not end the session.
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled exception (terminating: " + args.IsTerminating + ")",
                         args.ExceptionObject as Exception);
            AppLog.Shutdown();
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _shutdownCts.Cancel();
            Models.AppSettings.Flush();
            _host?.Dispose();
        }
        catch (Exception ex)
        {
            AppLog.Error("Error during shutdown", ex);
        }
        finally
        {
            _shutdownCts.Dispose();
            AppLog.Shutdown();
        }

        base.OnExit(e);
    }
}
