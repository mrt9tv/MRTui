# iRacing SDK Master Reference

**SDK**: SVappsLAB.iRacingTelemetrySDK
**Version**: 0.9.8.3 (Latest Stable)
**NuGet**: https://www.nuget.org/packages/SVappsLAB.iRacingTelemetrySDK/
**GitHub**: https://github.com/SVappsLAB/iRacingTelemetrySDK
**Last Updated**: October 16, 2025

---

## Table of Contents

1. [Overview](#overview)
2. [Core Architecture](#core-architecture)
3. [Getting Started](#getting-started)
4. [Event System](#event-system)
5. [Telemetry Variables](#telemetry-variables)
6. [SessionInfo YAML](#sessioninfo-yaml)
7. [IBT File Playback](#ibt-file-playback)
8. [Performance Characteristics](#performance-characteristics)
9. [Best Practices](#best-practices)
10. [Troubleshooting](#troubleshooting)
11. [Examples](#examples)

---

## Overview

The **SVappsLAB.iRacingTelemetrySDK** is a .NET library that provides real-time access to iRacing simulator telemetry data. It uses compile-time source generation to create strongly-typed data structures based on the telemetry variables you specify.

### Key Features

- **Live Telemetry**: Real-time data from iRacing simulator (60Hz updates)
- **IBT Playback**: Cross-platform playback of recorded telemetry files
- **Source Generation**: Compile-time struct generation for type safety
- **High Performance**: Processes 300,000+ records per second
- **Event-Driven**: Clean event-based architecture
- **Background Monitoring**: Non-blocking connection monitoring

### System Requirements

- **.NET 8.0** or later
- **Windows** (for live telemetry)
- **Cross-platform** (for IBT playback)
- **iRacing Subscription** (for live data)

---

## Core Architecture

### TelemetryClient<T>

The central component is `TelemetryClient<T>`, a generic class that manages connection and data flow:

```csharp
public interface ITelemetryClient<T>
{
    Task Monitor(CancellationToken cancellationToken);
    bool IsConnected();
    void Dispose();

    event EventHandler<ConnectStateChangedEventArgs> OnConnectStateChanged;
    event EventHandler<T> OnTelemetryUpdate;
    event EventHandler<ExceptionEventArgs> OnError;
}
```

### RequiredTelemetryVars Attribute

This attribute triggers source generation at compile time:

```csharp
[RequiredTelemetryVars([
    "Speed",
    "RPM",
    "Gear",
    "Throttle",
    "Brake"
])]
public class IRacingTelemetryService : ITelemetryService
{
    // Source generator creates SVappsLAB.iRacingTelemetrySDK.TelemetryData struct
    // with properties matching the specified variables
}
```

**Important Notes:**
- Variable names are **case-insensitive**
- Must match iRacing's internal naming exactly
- Use the DumpVariables sample to discover available variables
- Changes require project rebuild to regenerate struct

### Generated TelemetryData Struct

The source generator creates a record struct like:

```csharp
// Auto-generated at compile time
namespace SVappsLAB.iRacingTelemetrySDK
{
    public record struct TelemetryData
    {
        public float Speed { get; init; }
        public float RPM { get; init; }
        public int Gear { get; init; }
        public float Throttle { get; init; }
        public float Brake { get; init; }
    }
}
```

---

## Getting Started

### 1. Install NuGet Package

```bash
dotnet add package SVappsLAB.iRacingTelemetrySDK --version 0.9.8.3
dotnet add package Microsoft.Extensions.Logging
dotnet add package Microsoft.Extensions.Logging.Console
```

### 2. Add RequiredTelemetryVars Attribute

```csharp
using SVappsLAB.iRacingTelemetrySDK;
using Microsoft.Extensions.Logging;

[RequiredTelemetryVars([
    "Speed", "RPM", "Gear",
    "Throttle", "Brake", "Clutch",
    "LapDistPct", "Lap"
])]
public class MyTelemetryService
{
    private readonly ILogger<MyTelemetryService> _logger;
    private ITelemetryClient<SVappsLAB.iRacingTelemetrySDK.TelemetryData>? _client;

    public MyTelemetryService(ILogger<MyTelemetryService> logger)
    {
        _logger = logger;
    }
}
```

### 3. Create TelemetryClient

```csharp
public async Task ConnectAsync()
{
    // Create client (live mode)
    _client = TelemetryClient<SVappsLAB.iRacingTelemetrySDK.TelemetryData>.Create(_logger);

    // Subscribe to events
    _client.OnConnectStateChanged += OnConnectStateChanged;
    _client.OnTelemetryUpdate += OnTelemetryUpdate;
    _client.OnError += OnError;

    _logger.LogInformation("Telemetry client created");
}
```

### 4. Start Monitoring

```csharp
public async Task MonitorAsync(CancellationToken cancellationToken)
{
    if (_client == null)
        throw new InvalidOperationException("Client not initialized");

    _logger.LogInformation("Starting telemetry monitoring...");

    // This blocks until cancelled or error
    await _client.Monitor(cancellationToken);
}
```

### 5. Handle Events

```csharp
private void OnConnectStateChanged(object? sender, ConnectStateChangedEventArgs e)
{
    _logger.LogInformation("Connection state: {State}", e.State);

    if (e.State == ConnectState.Connected)
    {
        _logger.LogInformation("Connected to iRacing!");
    }
    else if (e.State == ConnectState.Disconnected)
    {
        _logger.LogInformation("Disconnected from iRacing");
    }
}

private void OnTelemetryUpdate(object? sender, SVappsLAB.iRacingTelemetrySDK.TelemetryData data)
{
    // Process telemetry data (fires 60 times per second when connected)
    _logger.LogDebug("Speed: {Speed} m/s, RPM: {RPM}", data.Speed, data.RPM);
}

private void OnError(object? sender, ExceptionEventArgs e)
{
    _logger.LogError(e.Exception, "SDK Error: {Message}", e.Exception.Message);
}
```

---

## Event System

### Connection State Changes

`OnConnectStateChanged` fires when iRacing connection state changes:

```csharp
public enum ConnectState
{
    Disconnected = 0,  // Not connected to iRacing
    Connected = 1      // Connected and receiving data
}

public class ConnectStateChangedEventArgs : EventArgs
{
    public ConnectState State { get; }
    public ConnectStateChangedEventArgs(ConnectState state)
    {
        State = state;
    }
}
```

**When it fires:**
- **Application Startup (iRacing already running)**: Fires immediately with `Connected`
- **iRacing Launches**: Fires with `Connected` within ~50ms
- **iRacing Closes**: Fires with `Disconnected` immediately
- **Session Change**: Does NOT fire (stays connected)

### Telemetry Updates

`OnTelemetryUpdate` fires on every telemetry update:

**Frequency:**
- **Live Mode**: 60 Hz (every ~16.67ms)
- **IBT Playback**: Variable speed based on playback multiplier

**Thread Safety:**
- Events fire on SDK's internal thread
- Use proper synchronization for UI updates
- WPF: Use `Dispatcher.Invoke()`
- Console: Safe to write directly

**Performance:**
- Minimize processing in event handler
- Queue data for background processing if needed
- Avoid blocking operations

### Error Events

`OnError` fires when SDK encounters errors:

```csharp
public class ExceptionEventArgs : EventArgs
{
    public Exception Exception { get; }
    public ExceptionEventArgs(Exception exception)
    {
        Exception = exception;
    }
}
```

**Common Error Scenarios:**
- Shared memory access failures
- SDK initialization errors
- IBT file read errors
- Unexpected data format issues

---

## Telemetry Variables

### Variable Categories

The SDK provides **400+ telemetry variables** across these categories:

1. **Vehicle Dynamics** - Speed, RPM, gear, pedals, steering, G-forces
2. **Track Position** - Lap distance, lap %, lap number, sector times
3. **Timing** - Current lap, last lap, best lap, delta times
4. **Multi-Car Data** - CarIdx arrays for all 64 cars in session
5. **Tire Data** - Temps, wear, pressure (carcass & surface)
6. **Engine & Fluids** - Coolant temp, oil temp, fuel level, pressures
7. **Environment** - Air temp, track temp, weather, wind
8. **Session Info** - Session time, laps remaining, flags
9. **Safety** - Incidents, warnings, engine lights
10. **Brake System** - Line pressure, ABS activity
11. **Pit Stop** - Pit status, service flags, repair times
12. **Car Setup** - In-car adjustments (ABS, TC, brake bias, etc.)
13. **Suspension** - Ride height, shock deflection/velocity
14. **Camera & Replay** - Camera state, replay controls
15. **Push-to-Pass** - P2P status, count remaining

### Complete Variable Reference

See **[iRacing_SDK_Variables_Reference.md](./iRacing_SDK_Variables_Reference.md)** for the complete list of 400+ variables with descriptions and data types.

### Currently Implemented (MRT-Mokkathon)

We currently use **72 variables** in `IRacingTelemetryService.cs`:

```csharp
[RequiredTelemetryVars([
    // MVP 1 - Core telemetry (14 variables)
    "Speed", "RPM", "Gear", "Throttle", "Brake", "Clutch",
    "SteeringWheelAngle", "Lap", "LapDistPct",
    "PlayerCarClassPosition", "PlayerCarIdx", "PlayerCarClass",

    // MVP 2 - Critical race data (7 variables)
    "FuelLevel", "FuelLevelPct", "WaterTemp", "OilTemp",
    "LapLastLapTime", "LapBestLapTime", "SessionTimeRemain",

    // MVP 3+ - Advanced telemetry (39 variables)
    "LFtempCL", "LFtempCM", "LFtempCR", // Tire carcass temps (12 total)
    "RFtempCL", "RFtempCM", "RFtempCR",
    "LRtempCL", "LRtempCM", "LRtempCR",
    "RRtempCL", "RRtempCM", "RRtempCR",

    "LFwearL", "LFwearM", "LFwearR", // Tire wear (12 total)
    "RFwearL", "RFwearM", "RFwearR",
    "LRwearL", "LRwearM", "LRwearR",
    "RRwearL", "RRwearM", "RRwearR",

    "LongAccel", "LatAccel", "VertAccel", // G-forces
    "SessionFlags", // Race flags
    "PlayerCarMyIncidentCount", // Incidents

    "LFbrakeLinePress", "RFbrakeLinePress", // Brake pressure (4 total)
    "LRbrakeLinePress", "RRbrakeLinePress",

    "AirTemp", "TrackTemp", "TrackTempCrew", // Environment
    "SessionTime", "SessionNum", // Session info

    // Phase 1: 4-Way Proximity Radar (14 variables)
    "CarLeftRight",    // Lateral spotter enum (0=Clear, 1=Left, 2=Right, 3=Both)

    "CarIdxLapDistPct",      // Track position % for each car [64]
    "CarIdxOnPitRoad",       // Pit road status [64]
    "CarIdxTrackSurface",    // Track surface type [64]
    "CarIdxClass",           // Car class ID [64]
    "CarIdxLap",             // Lap number [64]
    "CarIdxPosition",        // Overall race position [64]
    "CarIdxClassPosition",   // Class position [64]
    "CarIdxGear",            // Current gear [64]
    "CarIdxRPM",             // Engine RPM [64]
    "CarIdxEstTime",         // Est. time to reach position [64]
    "CarIdxF2Time",          // Time behind leader [64]
    "CarIdxLastLapTime",     // Last lap time [64]

    "Yaw",                   // Player heading angle (radians)
    "YawRate"                // Rate of heading change (rad/s)
])]
```

### How to Add New Variables

1. **Find the variable name** - Use SDK's DumpVariables sample or check [iRacing_SDK_Variables_Reference.md](./iRacing_SDK_Variables_Reference.md)
2. **Add to RequiredTelemetryVars** - Update the attribute in `IRacingTelemetryService.cs`
3. **Rebuild project** - Source generator creates updated struct
4. **Add to TelemetryData model** - Update `Core/Models/TelemetryData.cs` with matching property
5. **Map SDK data** - Add mapping in `OnTelemetryUpdate()` method
6. **Handle type conversions** - Cast enums to int, arrays properly

**Example - Adding tire pressure:**

```csharp
// 1. Add to RequiredTelemetryVars
[RequiredTelemetryVars([
    // ... existing variables
    "LFpressure", "RFpressure", "LRpressure", "RRpressure"
])]

// 2. Add to Models/TelemetryData.cs
public class TelemetryData
{
    // ... existing properties
    public float LFpressure { get; set; }
    public float RFpressure { get; set; }
    public float LRpressure { get; set; }
    public float RRpressure { get; set; }
}

// 3. Map in OnTelemetryUpdate()
private void OnTelemetryUpdate(object? sender, SVappsLAB.iRacingTelemetrySDK.TelemetryData sdkData)
{
    var data = new Models.TelemetryData
    {
        // ... existing mappings
        LFpressure = sdkData.LFpressure,
        RFpressure = sdkData.RFpressure,
        LRpressure = sdkData.LRpressure,
        RRpressure = sdkData.RRpressure,
        // ...
    };

    TelemetryUpdated?.Invoke(this, data);
}
```

---

## SessionInfo YAML

The SDK provides access to static/semi-static session data via YAML format.

### Accessing SessionInfo

```csharp
// After connection established, get session info via reflection
var clientType = _client.GetType();
var getSessionInfoMethod = clientType.GetMethod("GetRawTelemetrySessionInfoYaml");

if (getSessionInfoMethod != null)
{
    var sessionInfoYaml = getSessionInfoMethod.Invoke(_client, null) as string;
    if (!string.IsNullOrEmpty(sessionInfoYaml))
    {
        ParseSessionInfo(sessionInfoYaml);
    }
}
```

### SessionInfo Structure

```yaml
WeekendInfo:
  TrackDisplayName: "Spa-Francorchamps"
  TrackDisplayShortName: "Spa"
  TrackLength: "7.004 km"
  TrackCity: "Stavelot"
  TrackCountry: "Belgium"
  TrackType: "road course"
  TrackNumTurns: 20
  # ... more track info

DriverInfo:
  DriverUserName: "John Doe"
  DriverCarIdx: 0
  DriverUserID: 123456
  Drivers:
    - CarIdx: 0
      UserName: "John Doe"
      CarNumber: "42"
      CarPath: "fordmustangfr500s"
      TeamName: "Team MRT"
      # ... more driver info
    - CarIdx: 1
      UserName: "AI Driver 1"
      CarNumber: "23"
      # ...

SessionInfo:
  Sessions:
    - SessionNum: 0
      SessionName: "PRACTICE"
      SessionType: "Practice"
      SessionTime: "unlimited"
      SessionLaps: "unlimited"
      # ...
```

### Parsing Example (Our Implementation)

```csharp
private void ParseSessionInfo(string sessionInfoYaml)
{
    var lines = sessionInfoYaml.Split('\n');
    string? currentSection = null;
    int driverCarIdx = -1;
    bool inDriversArray = false;

    foreach (var line in lines)
    {
        var trimmed = line.Trim();

        // Track current section
        if (trimmed.EndsWith(':') && !trimmed.StartsWith('-'))
        {
            currentSection = trimmed.TrimEnd(':');
            continue;
        }

        // Parse based on section
        if (currentSection == "WeekendInfo")
        {
            if (trimmed.StartsWith("TrackDisplayName:"))
            {
                _trackName = ExtractYamlValue(trimmed);
            }
            else if (trimmed.StartsWith("TrackLength:"))
            {
                var lengthStr = ExtractYamlValue(trimmed);
                if (ParseTrackLength(lengthStr, out float lengthMeters))
                {
                    _trackLength = lengthMeters;
                }
            }
        }
        else if (currentSection == "DriverInfo")
        {
            if (trimmed.StartsWith("DriverUserName:"))
            {
                _driverName = ExtractYamlValue(trimmed);
            }
            else if (trimmed.StartsWith("DriverCarIdx:"))
            {
                int.TryParse(ExtractYamlValue(trimmed), out driverCarIdx);
            }
            else if (inDriversArray && trimmed.StartsWith("CarNumber:"))
            {
                _carNumber = ExtractYamlValue(trimmed).Trim('"', '\'');
            }
        }
    }
}

private static string ExtractYamlValue(string line)
{
    var colonIndex = line.IndexOf(':');
    if (colonIndex < 0 || colonIndex == line.Length - 1)
        return string.Empty;

    return line.Substring(colonIndex + 1).Trim();
}
```

### When SessionInfo Updates

SessionInfo is **semi-static** - it updates when:
- Session changes (practice → qualifying → race)
- Driver changes (team driver swap)
- Track conditions change significantly

**Performance Note:** Parsing SessionInfo YAML is expensive. Cache the results and only re-parse when needed.

---

## IBT File Playback

### What are IBT Files?

IBT (iRacing Binary Telemetry) files are recordings of telemetry sessions. They allow:
- **Offline Testing** - Test overlay without running iRacing
- **Reproducible Scenarios** - Debug specific race situations
- **Cross-Platform Development** - Mac/Linux development support
- **Performance Analysis** - Analyze recorded sessions

### Creating an IBT File

In iRacing:
1. Enter a session
2. Press `Ctrl + D` to start recording telemetry
3. Drive some laps
4. Press `Ctrl + D` again to stop recording
5. File saved to: `Documents\iRacing\telemetry\`

### Playback Implementation

```csharp
using SVappsLAB.iRacingTelemetrySDK;

// Create client with IBT options
var ibtOptions = new IBTOptions(
    filePath: @"C:\Users\YourName\Documents\iRacing\telemetry\mySession.ibt",
    speedMultiplier: 1.0 // 1.0 = real-time, 2.0 = 2x speed, 0.5 = half speed
);

var client = TelemetryClient<TelemetryData>.Create(_logger, ibtOptions);

// Subscribe to events (same as live mode)
client.OnConnectStateChanged += OnConnectStateChanged;
client.OnTelemetryUpdate += OnTelemetryUpdate;
client.OnError += OnError;

// Start playback
await client.Monitor(cancellationToken);
```

### Playback Features

**Speed Control:**
```csharp
var options = new IBTOptions(filePath, speedMultiplier: 2.0); // 2x speed
```

**Pause/Resume:**
```csharp
client.Pause();  // Pause playback
client.Resume(); // Resume playback
```

**Performance:**
- Processes 1 hour of telemetry in under 0.5 seconds
- ~300,000 records per second
- Minimal memory footprint

---

## Performance Characteristics

### Update Rates

**Live Telemetry:**
- **Target**: 60 Hz (60 updates per second)
- **Typical**: 48-60 Hz depending on iRacing framerate
- **Period**: ~16.67ms between updates
- **Latency**: <10ms typical, <20ms worst case

**IBT Playback:**
- **Configurable**: Controlled by speed multiplier
- **Real-time (1.0x)**: Matches original recording speed
- **Fast-forward (2.0x+)**: Higher update rate
- **Slow-motion (0.5x)**: Lower update rate

### Memory Usage

**Runtime Memory:**
- **SDK Client**: ~2MB telemetry buffer
- **Per Update**: Minimal allocations (ref structs)
- **Arrays**: CarIdx arrays [64] add ~50KB per update

**IBT File Size:**
- **Typical**: 2-5 MB per hour of racing
- **Compressed**: Binary format, very efficient

### Threading Model

**SDK Internal Threads:**
- Monitor loop runs on dedicated thread
- Events fire on SDK's thread (not UI thread)
- Background processing, non-blocking

**UI Integration:**
```csharp
// WPF - Dispatch to UI thread
private void OnTelemetryUpdate(object? sender, TelemetryData data)
{
    Dispatcher.Invoke(() =>
    {
        UpdateUI(data);
    });
}

// Console - Direct write (thread-safe)
private void OnTelemetryUpdate(object? sender, TelemetryData data)
{
    Console.WriteLine($"Speed: {data.Speed:F1} m/s");
}
```

---

## Best Practices

### 1. Client Lifecycle

**DO:**
```csharp
// Create once, reuse
_client = TelemetryClient<TelemetryData>.Create(_logger);
_client.OnTelemetryUpdate += OnTelemetryUpdate;

// Monitor in background
_ = Task.Run(() => _client.Monitor(cancellationToken));
```

**DON'T:**
```csharp
// Don't create multiple clients
for (int i = 0; i < 10; i++)
{
    var client = TelemetryClient<TelemetryData>.Create(_logger); // BAD!
}

// Don't block UI thread
_client.Monitor(cancellationToken).Wait(); // BAD! Blocks forever
```

### 2. Event Handler Performance

**DO:**
```csharp
private void OnTelemetryUpdate(object? sender, TelemetryData data)
{
    // Fast processing only
    _lastSpeed = data.Speed;
    _lastRPM = data.RPM;

    // Queue for background if needed
    _processingQueue.Enqueue(data);
}
```

**DON'T:**
```csharp
private void OnTelemetryUpdate(object? sender, TelemetryData data)
{
    // Don't do heavy processing
    var result = ComplexCalculation(data); // BAD!

    // Don't write to disk
    File.AppendAllText("telemetry.csv", data.ToString()); // BAD!

    // Don't make network calls
    await _apiClient.SendTelemetry(data); // BAD!
}
```

### 3. Error Handling

**DO:**
```csharp
try
{
    await _client.Monitor(cancellationToken);
}
catch (OperationCanceledException)
{
    _logger.LogInformation("Monitoring cancelled");
}
catch (Exception ex)
{
    _logger.LogError(ex, "SDK error: {Message}", ex.Message);
    // Handle gracefully, maybe retry
}
```

**DON'T:**
```csharp
// Don't swallow errors silently
try
{
    await _client.Monitor(cancellationToken);
}
catch { } // BAD! No logging
```

### 4. Variable Selection

**DO:**
```csharp
// Only request variables you need
[RequiredTelemetryVars([
    "Speed", "RPM", "Gear"  // Minimal set
])]
```

**DON'T:**
```csharp
// Don't request everything unnecessarily
[RequiredTelemetryVars([
    "Speed", "RPM", // ... 100+ variables you don't use
])]
```

### 5. Array Handling

**DO:**
```csharp
// Check for null arrays
if (sdkData.CarIdxLapDistPct != null)
{
    for (int i = 0; i < sdkData.CarIdxLapDistPct.Length; i++)
    {
        float pct = sdkData.CarIdxLapDistPct[i];
        if (pct >= 0 && pct <= 1) // Valid position
        {
            ProcessCar(i, pct);
        }
    }
}
```

**DON'T:**
```csharp
// Don't assume array is populated
for (int i = 0; i < 64; i++)
{
    ProcessCar(i, sdkData.CarIdxLapDistPct[i]); // BAD! Null reference!
}
```

---

## Troubleshooting

### Connection Issues

**Problem:** Client never connects to iRacing

**Solutions:**
1. Check iRacing is running and in a session
2. Verify shared memory is accessible:
   - Check Windows permissions
   - Disable antivirus temporarily
3. Enable debug logging:
   ```csharp
   services.AddLogging(builder =>
   {
       builder.SetMinimumLevel(LogLevel.Debug);
   });
   ```
4. Check console output for SDK errors

### Telemetry Not Updating

**Problem:** `OnTelemetryUpdate` fires but data seems stale

**Solutions:**
1. Verify connection status is `Connected`
2. Check update rate in logs (should be ~60 Hz)
3. Ensure you're in an active session (not menu/replay)
4. Verify variable names in `RequiredTelemetryVars` are correct

### Missing Variables

**Problem:** Variable always returns 0 or null

**Solutions:**
1. Check variable name spelling (case-insensitive but must match exactly)
2. Use DumpVariables sample to verify variable exists
3. Some variables only available in certain contexts:
   - Racing: All variables available
   - Practice: Most available
   - Replay: Limited set
   - Test Drive: Very limited
4. Check iRacing version compatibility

### Performance Issues

**Problem:** Application stutters or lags

**Solutions:**
1. Minimize event handler processing
2. Queue data for background processing
3. Reduce requested variable count
4. Check for memory leaks (dispose clients properly)
5. Profile with performance tools

### IBT Playback Errors

**Problem:** IBT file won't load

**Solutions:**
1. Verify file path is correct and file exists
2. Check file isn't corrupted (re-record if needed)
3. Ensure file is from compatible iRacing version
4. Try with smaller/simpler recording first

---

## Examples

### Example 1: Simple Speed Monitor

```csharp
[RequiredTelemetryVars(["Speed", "RPM", "Gear"])]
public class SpeedMonitor
{
    private readonly ILogger<SpeedMonitor> _logger;
    private ITelemetryClient<TelemetryData>? _client;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _client = TelemetryClient<TelemetryData>.Create(_logger);
        _client.OnTelemetryUpdate += (sender, data) =>
        {
            Console.WriteLine($"Speed: {data.Speed * 3.6f:F1} km/h, " +
                            $"RPM: {data.RPM:F0}, Gear: {data.Gear}");
        };

        await _client.Monitor(cancellationToken);
    }
}
```

### Example 2: Lap Time Tracker

```csharp
[RequiredTelemetryVars(["Lap", "LapCurrentLapTime", "LapBestLapTime"])]
public class LapTimeTracker
{
    private int _lastLap = -1;

    private void OnTelemetryUpdate(object? sender, TelemetryData data)
    {
        if (data.Lap != _lastLap)
        {
            _lastLap = data.Lap;
            Console.WriteLine($"Lap {data.Lap} complete! " +
                            $"Time: {data.LapCurrentLapTime:F3}s");

            if (data.LapCurrentLapTime < data.LapBestLapTime)
            {
                Console.WriteLine("*** NEW BEST LAP! ***");
            }
        }
    }
}
```

### Example 3: Multi-Car Proximity Detector

```csharp
[RequiredTelemetryVars([
    "PlayerCarIdx", "LapDistPct", "TrackLength",
    "CarIdxLapDistPct", "CarIdxOnPitRoad"
])]
public class ProximityDetector
{
    private void OnTelemetryUpdate(object? sender, TelemetryData data)
    {
        if (data.CarIdxLapDistPct == null) return;

        float playerPct = data.LapDistPct;
        int playerIdx = data.PlayerCarIdx;

        for (int i = 0; i < 64; i++)
        {
            if (i == playerIdx) continue;
            if (data.CarIdxOnPitRoad?[i] == true) continue;

            float carPct = data.CarIdxLapDistPct[i];
            if (carPct < 0 || carPct > 1) continue; // Invalid

            float diff = carPct - playerPct;

            // Handle lap wrap-around
            if (diff < -0.5f) diff += 1.0f;
            if (diff > 0.5f) diff -= 1.0f;

            float distanceMeters = Math.Abs(diff * data.TrackLength);

            if (distanceMeters < 20.0f) // Within 20 meters
            {
                string direction = diff > 0 ? "AHEAD" : "BEHIND";
                Console.WriteLine($"Car #{i} {direction} {distanceMeters:F1}m");
            }
        }
    }
}
```

### Example 4: Fuel Calculator

```csharp
[RequiredTelemetryVars([
    "FuelLevel", "FuelLevelPct", "SessionTimeRemain", "LapLastLapTime"
])]
public class FuelCalculator
{
    private float _startFuel = 0;
    private int _lapsCompleted = 0;

    private void OnTelemetryUpdate(object? sender, TelemetryData data)
    {
        if (_startFuel == 0)
        {
            _startFuel = data.FuelLevel;
            return;
        }

        float fuelUsed = _startFuel - data.FuelLevel;
        float avgPerLap = _lapsCompleted > 0 ? fuelUsed / _lapsCompleted : 0;

        float timeRemaining = data.SessionTimeRemain;
        float avgLapTime = data.LapLastLapTime > 0 ? data.LapLastLapTime : 90f;

        float estimatedLapsRemaining = timeRemaining / avgLapTime;
        float fuelNeeded = estimatedLapsRemaining * avgPerLap;

        Console.WriteLine($"Fuel: {data.FuelLevel:F1}L ({data.FuelLevelPct * 100:F0}%)");
        Console.WriteLine($"Avg/Lap: {avgPerLap:F2}L");
        Console.WriteLine($"Est. Laps Remaining: {estimatedLapsRemaining:F1}");
        Console.WriteLine($"Fuel Needed: {fuelNeeded:F1}L");

        if (fuelNeeded > data.FuelLevel)
        {
            Console.WriteLine("*** PIT FOR FUEL! ***");
        }
    }
}
```

---

## Additional Resources

### Official Documentation

- **GitHub Repository**: https://github.com/SVappsLAB/iRacingTelemetrySDK
- **NuGet Package**: https://www.nuget.org/packages/SVappsLAB.iRacingTelemetrySDK/
- **AI_CONTEXT.md**: Comprehensive developer guide in repository
- **Sample Projects**: ./Samples/ directory in repository

### Our Documentation

- **[iRacing_SDK_Variables_Reference.md](./iRacing_SDK_Variables_Reference.md)** - Complete 400+ variable list
- **[SDK_TELEMETRY_COMPLETE_ANALYSIS.md](./SDK_TELEMETRY_COMPLETE_ANALYSIS.md)** - Variable coverage analysis
- **[How-iRacing-Connection-Works.md](./reference/How-iRacing-Connection-Works.md)** - Connection flow explained
- **[PHASE1_RADAR_TELEMETRY_COMPLETE.md](./PHASE1_RADAR_TELEMETRY_COMPLETE.md)** - Radar system telemetry implementation

### Sample Projects (SDK Repository)

1. **DumpVariables_DumpSessionInfo** - Export telemetry to CSV, discover variables
2. **LocationAndWarnings** - Bitfield and enum handling examples
3. **SpeedRPMGear** - Simple telemetry display with pause/resume

---

## Version History

| Version | Release Date | Notes |
|---------|--------------|-------|
| **0.9.8.3** | Current | Latest stable release |
| 0.9.8.x | 2024 | Bug fixes, performance improvements |
| 0.9.0 | 2023 | Major refactor with source generation |
| Earlier | - | Legacy API (not recommended) |

**Current Version in Project**: 0.9.8.3 ✅

---

**Last Updated**: October 16, 2025
**Maintained By**: MRT-Mokkathon Development Team
**Questions?**: See [GitHub Issues](https://github.com/SVappsLAB/iRacingTelemetrySDK/issues)
