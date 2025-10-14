using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iRacingOverlay.Core;

/// <summary>
/// Background service that manages telemetry connection and displays data
/// </summary>
public class TelemetryWorker : BackgroundService
{
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<TelemetryWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly TelemetryDisplayOptions _displayOptions;
    private TelemetryData? _lastData;
    private int _updateCount = 0;
    private DateTime _startTime;

    public TelemetryWorker(
        ITelemetryService telemetryService,
        ILogger<TelemetryWorker> logger,
        IHostApplicationLifetime lifetime,
        TelemetryDisplayOptions displayOptions)
    {
        _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _displayOptions = displayOptions ?? new TelemetryDisplayOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telemetry Worker starting...");
        _startTime = DateTime.UtcNow;

        // Subscribe to events
        _telemetryService.TelemetryUpdated += OnTelemetryUpdated;
        _telemetryService.StatusChanged += OnStatusChanged;

        try
        {
            // Start connection (creates client and subscribes to SDK events)
            await _telemetryService.ConnectAsync(stoppingToken);

            _logger.LogInformation("Waiting for iRacing...");
            _logger.LogInformation("Start iRacing and enter a session to see telemetry data");
            _logger.LogInformation("Press Ctrl+C to exit");
            _logger.LogInformation("");

            // Start two concurrent tasks:
            // 1. Monitor telemetry (blocks until cancelled or error)
            // 2. Update display every second
            var monitorTask = Task.CompletedTask;
            if (_telemetryService is IRacingTelemetryService racingService)
            {
                monitorTask = racingService.MonitorAsync(stoppingToken);
            }

            var displayTask = Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_lastData != null)
                    {
                        if (_displayOptions.QuietMode)
                        {
                            DisplayQuietMode(_lastData);
                        }
                        else
                        {
                            DisplayTelemetry(_lastData);
                        }
                    }
                    // Refresh every 100ms for ultra-smooth updates (10 Hz display)
                    await Task.Delay(100, stoppingToken);
                }
            }, stoppingToken);

            // Wait for either task to complete (or cancellation)
            await Task.WhenAny(monitorTask, displayTask);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in telemetry worker");
        }
        finally
        {
            // Cleanup
            _telemetryService.TelemetryUpdated -= OnTelemetryUpdated;
            _telemetryService.StatusChanged -= OnStatusChanged;
            await _telemetryService.DisconnectAsync();
            _logger.LogInformation("Telemetry Worker stopped");
        }
    }

    private void OnStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        var statusMessage = e.Status switch
        {
            ConnectionStatus.Disconnected => "❌ Disconnected from iRacing",
            ConnectionStatus.Connecting => "🔄 Connecting to iRacing...",
            ConnectionStatus.Connected => "✅ Connected to iRacing!",
            ConnectionStatus.Reconnecting => "🔄 Reconnecting...",
            ConnectionStatus.Error => $"⚠️  Connection Error: {e.Message}",
            _ => $"Unknown status: {e.Status}"
        };

        _logger.LogInformation(statusMessage);
    }

    private void OnTelemetryUpdated(object? sender, TelemetryData data)
    {
        _lastData = data;
        _updateCount++;
    }

    private void DisplayTelemetry(TelemetryData data)
    {
        Console.Clear();
        Console.WriteLine("===========================================");
        Console.WriteLine("iRacing Telemetry - Live Data");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        // Connection info
        var uptime = DateTime.UtcNow - _startTime;
        Console.WriteLine($"Status: {(_telemetryService.IsConnected ? "✅ Connected" : "❌ Disconnected")}");
        Console.WriteLine($"Uptime: {uptime:hh\\:mm\\:ss}");
        Console.WriteLine($"Updates: {_updateCount:N0} ({_updateCount / uptime.TotalSeconds:F1} Hz)");
        Console.WriteLine();

        // Telemetry data
        Console.WriteLine("─────────── TELEMETRY DATA ────────────");
        Console.WriteLine();

        Console.WriteLine($"  Speed:    {data.SpeedKmh,7:F1} km/h  ({data.SpeedMph:F1} mph)");
        Console.WriteLine($"  RPM:      {data.RPM,7:F0}");
        Console.WriteLine($"  Gear:     {FormatGear(data.Gear),7}");
        Console.WriteLine($"  Lap:      {data.Lap,7}");
        Console.WriteLine($"  Position: {data.Position,7}");
        Console.WriteLine();

        Console.WriteLine("─────────── INPUTS ────────────────────");
        Console.WriteLine();

        Console.WriteLine($"  Throttle: {FormatPercentageBar(data.Throttle)} {data.Throttle * 100:F1}%");
        Console.WriteLine($"  Brake:    {FormatPercentageBar(data.Brake)} {data.Brake * 100:F1}%");
        // Clutch: SDK reports 0=released, 1=pressed. Invert for display (0%=pressed, 100%=released)
        var clutchDisplay = 1.0f - data.Clutch;
        Console.WriteLine($"  Clutch:   {FormatPercentageBar(clutchDisplay)} {clutchDisplay * 100:F1}%");
        Console.WriteLine($"  Steering: {FormatSteeringBar(data.SteeringWheelAngle)} ({RadiansToDegrees(data.SteeringWheelAngle):F1}°)");
        Console.WriteLine();

        Console.WriteLine("───────────────────────────────────────");
        Console.WriteLine($"Last Update: {data.Timestamp:HH:mm:ss.fff}");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to exit");
    }

    private string FormatGear(int gear)
    {
        return gear switch
        {
            -1 => "R",
            0 => "N",
            _ => gear.ToString()
        };
    }

    private string FormatPercentageBar(float value, int barLength = 20)
    {
        var filled = (int)(value * barLength);
        var bar = new string('█', filled) + new string('░', barLength - filled);
        return $"[{bar}]";
    }

    private string FormatSteeringBar(float angle, int barLength = 40)
    {
        // Angle is in radians, typically -1 to +1 range
        // NOTE: User reported that steering was inverted, so we negate the angle
        // to match expected behavior: turn left = bar moves left
        var normalizedAngle = Math.Clamp(-angle / 1.0f, -1.0f, 1.0f);
        
        // Center position
        var center = barLength / 2;
        // Now: positive normalizedAngle (left) moves bar right (higher position)
        // and negative normalizedAngle (right) moves bar left (lower position)
        var position = center + (int)(normalizedAngle * center);
        position = Math.Clamp(position, 0, barLength - 1);

        var bar = new char[barLength];
        for (int i = 0; i < barLength; i++)
        {
            if (i == center)
                bar[i] = '│'; // Center marker
            else if (i == position)
                bar[i] = '█'; // Current position
            else
                bar[i] = '─';
        }

        return $"[{new string(bar)}] {angle:F2}rad";
    }

    private void DisplayQuietMode(TelemetryData data)
    {
        // Minimal single-line output
        var uptime = DateTime.UtcNow - _startTime;
        var status = _telemetryService.IsConnected ? "✅" : "❌";
        var updateRate = _updateCount / uptime.TotalSeconds;

        Console.Write($"\r{status} Connected | Uptime: {uptime:hh\\:mm\\:ss} | Updates: {_updateCount:N0} ({updateRate:F1} Hz) | Speed: {data.SpeedKmh:F0} km/h | RPM: {data.RPM:F0} | Gear: {FormatGear(data.Gear)}     ");
    }

    private float RadiansToDegrees(float radians)
    {
        return radians * (180.0f / (float)Math.PI);
    }
}
