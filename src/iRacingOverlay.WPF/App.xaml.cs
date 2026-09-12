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

    /// <summary>Logs UI-thread stalls so a freeze report comes with timings.</summary>
    private UiWatchdog? _uiWatchdog;

    /// <summary>Cancels telemetry monitoring on shutdown so the SDK loop stops cleanly.</summary>
    private readonly CancellationTokenSource _shutdownCts = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // File logging first: everything below should be diagnosable from the log.
        AppLog.Start();
        InstallGlobalExceptionHandlers();
        _uiWatchdog = new UiWatchdog(Dispatcher);

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
    /// Connect and start monitoring, and keep doing so for the life of the app.
    ///
    /// The SDK's Monitor loop can fault — a shared-memory read failing while the
    /// sim reloads between sessions is the likely case. Before this loop a fault
    /// was logged once and the overlay sat on its last frame until restart, which
    /// from the driver's seat is indistinguishable from a freeze. Now the client
    /// is torn down and rebuilt, with a backoff so a persistent failure does not
    /// spin.
    /// </summary>
    private void StartTelemetry(ITelemetryService telemetryService)
    {
        _ = Task.Run(async () =>
        {
            var token = _shutdownCts.Token;
            var backoff = TimeSpan.FromSeconds(2);
            var maxBackoff = TimeSpan.FromSeconds(30);

            while (!token.IsCancellationRequested)
            {
                var started = DateTime.UtcNow;
                try
                {
                    await telemetryService.ConnectAsync(token).ConfigureAwait(false);

                    if (telemetryService is IRacingTelemetryService racingService)
                        await racingService.MonitorAsync(token).ConfigureAwait(false);

                    // Monitor returns normally only on cancellation.
                    AppLog.Info("Telemetry monitoring stopped (shutdown)");
                    return;
                }
                catch (OperationCanceledException)
                {
                    AppLog.Info("Telemetry monitoring stopped (shutdown)");
                    return;
                }
                catch (Exception ex)
                {
                    // A fault after a healthy run is a fresh incident, not a retry.
                    if (DateTime.UtcNow - started > TimeSpan.FromMinutes(1))
                        backoff = TimeSpan.FromSeconds(2);

                    AppLog.Error($"Telemetry monitoring faulted — restarting in {backoff.TotalSeconds:F0} s", ex);
                }

                try
                {
                    await telemetryService.DisconnectAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    AppLog.Error("Could not release the faulted telemetry client", ex);
                }

                try
                {
                    await Task.Delay(backoff, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, maxBackoff.TotalSeconds));
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
            _uiWatchdog?.Dispose();
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
