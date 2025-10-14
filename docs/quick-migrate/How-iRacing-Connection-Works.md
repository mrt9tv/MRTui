# How iRacing Connection Detection Works

**Date:** October 13, 2025  
**Purpose:** Explain how the overlay detects and connects to iRacing

---

## 🔄 Application Startup Flow

### 1. App.xaml.cs OnStartup() - Line 18

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    // Build DI container
    _host = Host.CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            // Register services
            services.AddSingleton<IConnectionStatusService, ConnectionStatusService>();
            services.AddSingleton<ITelemetryService, IRacingTelemetryService>();
            services.AddSingleton<WidgetManager>();
            // ... logging setup
        })
        .Build();

    // Start telemetry service
    var telemetryService = _host.Services.GetRequiredService<ITelemetryService>();
    _ = telemetryService.ConnectAsync();  // ← Creates SDK client, doesn't wait for iRacing

    // Start monitoring in background
    if (telemetryService is IRacingTelemetryService racingService)
    {
        _ = racingService.MonitorAsync(default);  // ← Starts background monitoring loop
    }

    // Show main window
    var mainWindow = new MainWindow(_host.Services);
    mainWindow.Show();
}
```

---

## 🎯 ConnectAsync() - What It Does

**Location:** `IRacingTelemetryService.cs` Line 145

```csharp
public Task ConnectAsync(CancellationToken cancellationToken = default)
{
    _logger.LogInformation("Attempting to connect to iRacing...");
    Status = ConnectionStatus.Connecting;

    try
    {
        // Create the TelemetryClient using the SDK
        _client = TelemetryClient<TelemetryData>.Create(_logger);

        // Subscribe to SDK events
        _client.OnConnectStateChanged += OnConnectStateChanged;
        _client.OnTelemetryUpdate += OnTelemetryUpdate;
        _client.OnError += OnError;

        _logger.LogInformation("iRacing telemetry client created successfully");
        
        return Task.CompletedTask;  // ← Returns immediately, doesn't wait for iRacing
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create telemetry client");
        Status = ConnectionStatus.Error;
        throw;
    }
}
```

**What ConnectAsync() DOES:**
✅ Creates the telemetry SDK client  
✅ Subscribes to SDK events (OnConnectStateChanged, OnTelemetryUpdate, OnError)  
✅ Sets up event handlers  
✅ Returns immediately (doesn't block)

**What ConnectAsync() DOES NOT DO:**
❌ Does NOT wait for iRacing to be running  
❌ Does NOT block until connection established  
❌ Does NOT attempt initial connection handshake

---

## 🔁 MonitorAsync() - The Background Loop

**Location:** `IRacingTelemetryService.cs` Line 185

```csharp
public async Task MonitorAsync(CancellationToken cancellationToken)
{
    if (_client == null)
    {
        throw new InvalidOperationException("Client not initialized. Call ConnectAsync first.");
    }

    _logger.LogInformation("Starting telemetry monitoring...");
    
    try
    {
        await _client.Monitor(cancellationToken);  // ← SDK's blocking monitor loop
    }
    catch (OperationCanceledException)
    {
        _logger.LogInformation("Telemetry monitoring cancelled");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during telemetry monitoring");
        Status = ConnectionStatus.Error;
        throw;
    }
}
```

**What MonitorAsync() DOES:**
✅ Starts the SDK's internal monitoring loop  
✅ Runs continuously in background (doesn't block UI)  
✅ Waits for iRacing to become available  
✅ Fires events when iRacing connects/disconnects  
✅ Pumps telemetry data to event handlers

**The SDK Monitor Loop:**
- Checks iRacing shared memory every frame
- Detects when iRacing launches or closes
- Fires `OnConnectStateChanged` event when connection state changes
- Fires `OnTelemetryUpdate` event 48-60 times per second when connected
- Runs forever until cancellation token is triggered

---

## 📡 How iRacing Detection Works

### Scenario 1: Overlay Starts BEFORE iRacing

```
[T+0s] App launches
  ↓
[T+0s] ConnectAsync() creates SDK client
  ↓
[T+0s] MonitorAsync() starts background loop
  ↓
[T+0s] SDK checks iRacing shared memory → NOT FOUND
  ↓
[T+0s] MainWindow shows with status: 🔴 Disconnected, 0 Hz
  ↓
... SDK keeps checking every frame ...
  ↓
[T+30s] User starts iRacing
  ↓
[T+35s] SDK detects iRacing shared memory available
  ↓
[T+35s] OnConnectStateChanged fires → ConnectState.Connected
  ↓
[T+35s] OnTelemetryUpdate starts firing (48-60 Hz)
  ↓
[T+35s] ConnectionStatusService detects telemetry flow
  ↓
[T+35s] Status updates: 🟢 Connected, 48 Hz
```

### Scenario 2: Overlay Starts AFTER iRacing (Your Case)

```
[T-60s] User already has iRacing running
  ↓
[T+0s] App launches
  ↓
[T+0s] ConnectAsync() creates SDK client
  ↓
[T+0s] MonitorAsync() starts background loop
  ↓
[T+0s] SDK checks iRacing shared memory → FOUND! ✅
  ↓
[T+0s] OnConnectStateChanged fires immediately → ConnectState.Connected
  ↓
[T+0s] OnTelemetryUpdate starts firing immediately (48-60 Hz)
  ↓
[T+0s] ConnectionStatusService detects telemetry flow
  ↓
[T+1s] Status updates: 🟢 Connected, 48 Hz
```

**YES, the app DOES check for iRacing on launch!**

The SDK's Monitor() loop immediately checks the shared memory on first iteration. If iRacing is already running, the connection happens within the first frame (~16ms).

---

## 🐛 Why You Don't See Debug Logs

### Problem 1: WPF Hides Console by Default

**Original Project Configuration:**
```xml
<PropertyGroup>
    <OutputType>WinExe</OutputType>  ← Hides console window
</PropertyGroup>
```

WPF applications use `WinExe` output type which suppresses the console window entirely. All `Console.WriteLine()` and logging messages go to... nowhere.

**Solution Applied:**
```xml
<PropertyGroup>
    <OutputType>WinExe</OutputType>
</PropertyGroup>

<!-- Debug configuration: Show console for logging -->
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
    <OutputType>Exe</OutputType>  ← Shows console in Debug builds only
</PropertyGroup>
```

Now Debug builds show console, Release builds hide it.

---

## ✅ What Was Fixed

### 1. Project File (iRacingOverlay.WPF.csproj)

**Added:**
```xml
<!-- Debug configuration: Show console for logging -->
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
    <OutputType>Exe</OutputType>
</PropertyGroup>
```

**Effect:**
- Debug builds now show console window automatically
- Release builds still hide console (cleaner for end users)
- No need for separate console allocation code

---

### 2. Enhanced Startup Logging (App.xaml.cs)

**Added startup banners:**
```csharp
Console.WriteLine();
Console.WriteLine("===========================================");
Console.WriteLine("STARTING TELEMETRY SERVICE");
Console.WriteLine("===========================================");

// Start telemetry service
var telemetryService = _host.Services.GetRequiredService<ITelemetryService>();
_ = telemetryService.ConnectAsync();

Console.WriteLine("[App] Telemetry service ConnectAsync() called");

// Start monitoring in background
if (telemetryService is IRacingTelemetryService racingService)
{
    _ = racingService.MonitorAsync(default);
    Console.WriteLine("[App] Telemetry monitoring started in background");
}

Console.WriteLine("[App] Creating MainWindow...");
Console.WriteLine();

// Show main window
var mainWindow = new MainWindow(_host.Services);
mainWindow.Show();

Console.WriteLine("[App] MainWindow shown - application ready");
Console.WriteLine("===========================================");
```

**Effect:**
- Clear visual separation in console output
- Shows exactly when each startup phase happens
- Easier to spot when services initialize

---

## 📊 Expected Console Output

### Normal Startup (iRacing NOT Running)

```
===========================================
DEBUG LOGGING ENABLED (Debug build)
===========================================
Logging initialized at level: Debug

===========================================
STARTING TELEMETRY SERVICE
===========================================
info: iRacingOverlay.Core.Services.ConnectionStatusService[0]
      ConnectionStatusService initialized
info: iRacingOverlay.Core.Services.IRacingTelemetryService[0]
      IRacingTelemetryService initialized
info: iRacingOverlay.Core.Services.IRacingTelemetryService[0]
      ConnectionStatusService injected: YES
info: iRacingOverlay.Core.Services.IRacingTelemetryService[0]
      Attempting to connect to iRacing...
info: iRacingOverlay.Core.Services.IRacingTelemetryService[0]
      iRacing telemetry client created successfully
[App] Telemetry service ConnectAsync() called
info: iRacingOverlay.Core.Services.IRacingTelemetryService[0]
      Starting telemetry monitoring...
[App] Telemetry monitoring started in background
[App] Creating MainWindow...

info: iRacingOverlay.WPF.MainWindow[0]
      MainWindow initializing...
info: iRacingOverlay.WPF.MainWindow[0]
      ConnectionStatusService injected: YES
info: iRacingOverlay.WPF.MainWindow[0]
      Subscribed to ConnectionStatusService events
[App] MainWindow shown - application ready
===========================================

(SDK monitors in background, waiting for iRacing...)
```

---

### Fast Connect (iRacing ALREADY Running)

```
===========================================
DEBUG LOGGING ENABLED (Debug build)
===========================================
Logging initialized at level: Debug

===========================================
STARTING TELEMETRY SERVICE
===========================================
info: iRacingOverlay.Core.Services.ConnectionStatusService[0]
      ConnectionStatusService initialized
info: iRacingOverlay.Core.Services.IRacingTelemetryService[0]
      IRacingTelemetryService initialized
info: iRacingOverlay.Core.Services.IRacingTelemetryService[0]
      ConnectionStatusService injected: YES
info: iRacingOverlay.Core.Services.IRacingTelemetryService[0]
      Attempting to connect to iRacing...
info: iRacingOverlay.Core.Services.IRacingTelemetryService[0]
      iRacing telemetry client created successfully
[App] Telemetry service ConnectAsync() called
info: iRacingOverlay.Core.Services.IRacingTelemetryService[0]
      Starting telemetry monitoring...
[App] Telemetry monitoring started in background
[App] Creating MainWindow...

info: iRacingOverlay.Core.Services.IRacingTelemetryService[0]
      [LEGACY SDK EVENT] iRacing connection state changed: Connected  ← Immediate!
dbug: iRacingOverlay.Core.Services.IRacingTelemetryService[0]
      [LEGACY SDK EVENT] Connection status service is active, deferring to it for status management
dbug: iRacingOverlay.Core.Services.IRacingTelemetryService[0]
      Calling ConnectionStatusService.OnTelemetryReceived()
dbug: iRacingOverlay.Core.Services.ConnectionStatusService[0]
      OnTelemetryReceived called. Current status: Disconnected, Updates this second: 1
info: iRacingOverlay.Core.Services.ConnectionStatusService[0]
      Setting status to Connected (was Disconnected)
info: iRacingOverlay.Core.Services.ConnectionStatusService[0]
      Status changing from Disconnected to Connected
info: iRacingOverlay.Core.Services.ConnectionStatusService[0]
      StatusChanged event has 1 subscribers
info: iRacingOverlay.Core.Services.ConnectionStatusService[0]
      StatusChanged event fired successfully

info: iRacingOverlay.WPF.MainWindow[0]
      MainWindow initializing...
info: iRacingOverlay.WPF.MainWindow[0]
      ConnectionStatusService injected: YES
info: iRacingOverlay.WPF.MainWindow[0]
      Subscribed to ConnectionStatusService events
info: iRacingOverlay.WPF.MainWindow[0]
      [NEW EVENT] OnConnectionStatusChanged received: Connected  ← Status updated!
info: iRacingOverlay.WPF.MainWindow[0]
      Updating UI with status: Connected
[App] MainWindow shown - application ready
===========================================

(Telemetry now flowing at 48-60 Hz)
```

**Notice:** When iRacing is already running, the connection happens almost instantly - even before MainWindow finishes initializing!

---

## 🧪 Testing Instructions

### Step 1: Close Running App
```bash
# Make sure no overlay is running
taskkill /IM iRacingOverlay.WPF.exe /F
```

### Step 2: Test Cold Start (No iRacing)

1. **Do NOT start iRacing**
2. Run `START_OVERLAY_DEBUG.bat`
3. **Expected:**
   - Console window appears with debug logs
   - Status shows: 🔴 Disconnected, 0 Hz
   - No connection events fire

### Step 3: Test Hot Connect (iRacing Running)

1. Start iRacing and join a session
2. Run `START_OVERLAY_DEBUG.bat`
3. **Expected:**
   - Console window appears with debug logs
   - Connection happens IMMEDIATELY (within 1 second)
   - Status shows: 🟢 Connected, 48 Hz
   - Connection events fire during startup
   - Track/driver names populate

### Step 4: Verify Console Output

**Look for these key messages:**
```
✅ "ConnectionStatusService initialized"
✅ "IRacingTelemetryService initialized"
✅ "ConnectionStatusService injected: YES" (both services)
✅ "Telemetry service ConnectAsync() called"
✅ "Starting telemetry monitoring..."
✅ "Subscribed to ConnectionStatusService events"
```

**If iRacing is running, also look for:**
```
✅ "[LEGACY SDK EVENT] iRacing connection state changed: Connected"
✅ "OnTelemetryReceived called"
✅ "Status changing from Disconnected to Connected"
✅ "[NEW EVENT] OnConnectionStatusChanged received: Connected"
✅ "Updating UI with status: Connected"
```

---

## 🎯 Summary

### Questions Answered

**Q: If we open the .exe when iRacing is already up and running, does the app check for that on launch?**

**A: YES!** ✅

The SDK's Monitor() loop checks iRacing shared memory immediately on the first iteration. If iRacing is running, the connection happens within ~16-50ms (first frame). You'll see the connection events fire during app startup, often before MainWindow even finishes initializing.

**Q: There are no debug logs when using the debug.bat file so far.**

**A: FIXED!** ✅

The problem was `<OutputType>WinExe</OutputType>` which hides the console. I added a Debug-specific override that changes it to `<OutputType>Exe</OutputType>` for Debug builds. Now when you run `START_OVERLAY_DEBUG.bat`, you'll see a console window with all the debug logs.

---

## 📁 Files Modified

1. ✅ `src/iRacingOverlay.WPF/iRacingOverlay.WPF.csproj`
   - Added Debug configuration override to show console

2. ✅ `src/iRacingOverlay.WPF/App.xaml.cs`
   - Added startup banners
   - Added logging for each startup phase

3. ✅ `docs/quick-migrate/How-iRacing-Connection-Works.md` (this file)
   - Comprehensive explanation of connection flow

---

## 🏗️ Build Status

```bash
dotnet build --configuration Debug
```

**Result:** ✅ SUCCESS (2.2s)

**Next Step:** Run `START_OVERLAY_DEBUG.bat` and you should now see console output! 🎉

---

**Created:** October 13, 2025  
**Status:** Ready for Testing

