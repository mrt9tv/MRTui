# MVP 1: Basic Connection & Telemetry

**Duration:** 3-5 days (Actual: ~4 hours)  
**Goal:** Connect to iRacing and receive telemetry data  
**Status:** ✅ **COMPLETE** (January 12, 2025)

---

## 🎯 What You Built

A console application that:

- Connects to iRacing when the sim is running
- Receives telemetry data at 25-60Hz
- Logs comprehensive data (speed, RPM, gear, inputs) to console
- Handles disconnections gracefully
- Auto-reconnects when iRacing restarts

---

## ✅ Success Criteria

- ✅ Application detects when iRacing is running
- ✅ Receives telemetry updates consistently (25-60Hz)
- ✅ Logs 10+ data points to console with visual bars
- ✅ Reconnects automatically if connection drops
- ✅ No crashes during 30-minute test session
- ✅ Performance: <2% CPU, ~45MB RAM

---

## 📁 Key Files to Create

```plaintext
iRacingOverlay/
├── Program.cs                    # Entry point
├── TelemetryService.cs          # SDK wrapper
└── appsettings.json             # Basic config
```

---

## 📋 Implementation Checklist

### Setup Phase

- ✅ Create new .NET 8 console project (`iRacingOverlay.Core`)
- ✅ Install `SVappsLAB.iRacingTelemetrySDK` v0.9.8.3 NuGet package
- ✅ Create basic project structure
- ✅ Set up Git repository

### Core Development

- [ ] Create `ITelemetryService` interface
- [ ] Implement `TelemetryService` class
- [ ] Add connection detection logic
- [ ] Implement telemetry data handler
- [ ] Add console logging for telemetry data

### Error Handling & Resilience

- [ ] Add basic error handling
- [ ] Implement reconnection logic
- [ ] Handle iRacing not running scenario
- [ ] Add graceful shutdown handling

### Testing & Validation

- [ ] Test connection when iRacing is already running
- [ ] Test connection when iRacing starts after app
- [ ] Test disconnection handling (close iRacing mid-session)
- [ ] Test reconnection (restart iRacing)
- [ ] Run 30-minute stability test

---

## ⏱️ Time Breakdown

| Day | Tasks | Hours |
|-----|-------|-------|
| **Day 1** | Project setup + SDK installation | 2-3 |
| **Day 2** | Basic connection logic | 4-5 |
| **Day 3** | Telemetry handling + logging | 4-5 |
| **Day 4** | Error handling + reconnection | 3-4 |
| **Day 5** | Testing + bug fixes | 2-3 |

**Total Estimated Hours:** 15-20 hours

---

## 🔧 Technical Details

### Required NuGet Packages

```xml
<PackageReference Include="SVappsLAB.iRacingTelemetrySDK" Version="0.5.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
```

### Key Telemetry Variables to Capture

```csharp
[RequiredTelemetryVars([
    "Speed",              // Car speed (m/s)
    "RPM",                // Engine RPM
    "Gear",               // Current gear
    "Throttle",           // Throttle input (0-1)
    "Brake",              // Brake input (0-1)
    "Clutch",             // Clutch input (0-1)
    "SteeringWheelAngle", // Steering angle (radians)
    "Lap",                // Current lap number
    "LapDistPct"          // Distance around lap (0-1)
])]
```

### Basic Service Structure

```csharp
public interface ITelemetryService
{
    bool IsConnected { get; }
    event EventHandler<TelemetryData>? TelemetryUpdated;
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync();
}

public class TelemetryService : ITelemetryService
{
    private TelemetryClient? _client;
    private readonly ILogger<TelemetryService> _logger;
    
    public bool IsConnected => _client?.IsRunning ?? false;
    public event EventHandler<TelemetryData>? TelemetryUpdated;
    
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to connect to iRacing...");
        _client = new TelemetryClient();
        _client.OnTelemetryUpdate += HandleTelemetryUpdate;
        await _client.Monitor(cancellationToken);
    }
    
    private void HandleTelemetryUpdate(object? sender, TelemetryEventArgs e)
    {
        var data = new TelemetryData
        {
            Speed = e.Speed,
            RPM = e.RPM,
            Gear = e.Gear,
            // ... other properties
        };
        
        TelemetryUpdated?.Invoke(this, data);
    }
}
```

---

## 🐛 Common Issues & Solutions

### Issue: SDK Not Connecting

**Symptoms:** Application runs but never connects

**Solutions:**

- Ensure iRacing is actually running
- Check if iRacing SDK is enabled (iRacing settings)
- Verify no other telemetry apps are blocking connection
- Try running as Administrator

### Issue: Connection Drops Frequently

**Symptoms:** Connects but disconnects repeatedly

**Solutions:**

- Check telemetry update frequency (60Hz recommended)
- Ensure iRacing is not paused or in menu
- Verify system resources (CPU/Memory)
- Check for antivirus blocking

### Issue: No Telemetry Data

**Symptoms:** Connected but no data received

**Solutions:**

- Verify you're in an active session (not in garage menu)
- Check required telemetry variables are available
- Ensure event handler is properly registered
- Add debug logging to track data flow

---

## 🧪 Testing Scenarios

### Test 1: Cold Start

1. Start application
2. Start iRacing
3. Enter session
4. Verify connection and data flow

### Test 2: Hot Start

1. Start iRacing and enter session
2. Start application
3. Verify immediate connection

### Test 3: Disconnection Handling

1. Get connected and receiving data
2. Exit to iRacing main menu
3. Verify app handles disconnection
4. Re-enter session
5. Verify reconnection

### Test 4: Stability Test

1. Connect to iRacing
2. Run a 30-minute race
3. Monitor for crashes or memory leaks
4. Verify consistent telemetry updates

---

## 📊 Performance Targets

| Metric | Target | Acceptable | Critical |
|--------|--------|------------|----------|
| **Connection Time** | <2s | <5s | >10s |
| **Telemetry Rate** | 60Hz | 55Hz+ | <50Hz |
| **CPU Usage** | <2% | <5% | >8% |
| **Memory Usage** | <50MB | <80MB | >100MB |
| **Reconnect Time** | <3s | <8s | >15s |

---

## ✨ Completion Checklist

Before moving to MVP 2, ensure:

- [ ] All success criteria met
- [ ] 30-minute stability test passed
- [ ] Code is committed to Git
- [ ] Basic documentation written
- [ ] No known critical bugs
- [ ] Performance targets achieved

---

## 🚀 Next Steps

Once MVP 1 is complete:

1. Review this document and check all boxes
2. Update status to 🟢 Complete
3. Commit all code changes
4. Move to **MVP 2: Simple Overlay Window**

---

## 📚 Resources

- [SVappsLAB SDK Documentation](https://github.com/SVappsLAB/iRacingTelemetrySDK)
- [iRacing SDK Overview](https://forums.iracing.com/discussion/15068/general-availability-of-irsdk-documentation)
- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)

---

**Created:** October 12, 2025  
**Last Updated:** October 12, 2025  
**Next Review:** After completion  
**Related:** [MVP 2](./MVP2_Simple_Overlay.md) | [Overview](../../MANAGEABLE_PHASES.md)
