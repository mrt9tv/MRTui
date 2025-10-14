# Implementation Plan: Connection Status System + Centralized SDK/Formula System

**Version**: 1.0  
**Date**: October 13, 2025  
**Target**: v0.5 Release  
**Estimated Time**: 4-6 hours total

---

## 🎯 Implementation Progress

### Part 1: Connection Status Monitoring System
- ✅ **Phase 1**: Create Connection Status Service (COMPLETE)
  - ✅ Step 1.1: IConnectionStatusService interface created
  - ✅ Step 1.2: ConnectionStatusService implementation with health monitoring
  - ✅ Step 1.3: Integration with IRacingTelemetryService
- ✅ **Phase 2**: Add UI Status Indicators (COMPLETE)
  - ✅ MainWindow.xaml updated with status bar (connection icon, rate, session info)
  - ✅ MainWindow.xaml.cs event handlers implemented
- ✅ **Phase 3**: Register Services in DI (COMPLETE)
  - ✅ ConnectionStatusService registered in App.xaml.cs
- ⏳ **Phase 4**: Testing Connection Status (PENDING)

### Part 2: Centralized SDK/Formula System
- ✅ **Phase 5**: Create TelemetryConstants (COMPLETE - 235 lines, 9 categories, refined twice)
- ✅ **Phase 6**: Create TelemetryExtensions (COMPLETE - TelemetryExtensions, TelemetryColorExtensions, TelemetryFormulas)
- ⏳ **Phase 7**: Migrate Widgets (PENDING)
- ⏳ **Phase 8**: Testing & Validation (PENDING)
- ⏳ **Phase 9**: Documentation (PENDING)

---

## 📋 Executive Summary

This document outlines the implementation of two critical architectural improvements:

1. **Connection Status Monitoring System** - Real-time iRacing connection health tracking with visual feedback
2. **Centralized SDK/Formula System** - Single source of truth for telemetry conversions, formulas, and thresholds

**Goals**:
- ✅ Eliminate "N/A" confusion with proper connection status indicators
- ✅ Reduce code duplication across widgets (400+ lines eliminated)
- ✅ Consistent unit conversions and color thresholds
- ✅ Better user experience with clear connection feedback
- ✅ Easier maintenance and tuning of telemetry logic

---

## 🎯 Part 1: Connection Status Monitoring System

### Overview
Build a robust connection status monitoring service that tracks iRacing telemetry health and provides real-time feedback to users and widgets.

### Architecture Components

```
┌─────────────────────────────────────────────────────┐
│         IConnectionStatusService (Interface)         │
├─────────────────────────────────────────────────────┤
│  + ConnectionStatus Status { get; }                  │
│  + DateTime LastDataReceived { get; }               │
│  + TimeSpan TimeSinceLastData { get; }              │
│  + bool IsHealthy { get; }                          │
│  + int TelemetryRate { get; }                       │
│  + event EventHandler<ConnectionStatus> StatusChanged│
│  + event EventHandler<bool> HealthChanged           │
│  + void OnTelemetryReceived()                       │
└─────────────────────────────────────────────────────┘
              ▲
              │ implements
              │
┌─────────────────────────────────────────────────────┐
│      ConnectionStatusService (Implementation)        │
├─────────────────────────────────────────────────────┤
│  - Timer _healthCheckTimer (1 second interval)       │
│  - DateTime _lastDataReceived                        │
│  - int _updatesThisSecond                           │
│  - int _telemetryRate                               │
│                                                      │
│  + void CheckHealth() // Timer callback             │
│  + void OnTelemetryReceived() // Call from service  │
└─────────────────────────────────────────────────────┘
```

---

## 📦 Phase 1: Create Connection Status Service (45 minutes)

### Step 1.1: Create Interface (10 minutes)

**File**: `src/iRacingOverlay.Core/Services/IConnectionStatusService.cs`

```csharp
using System;

namespace iRacingOverlay.Core.Services
{
    /// <summary>
    /// Monitors iRacing connection health and telemetry data flow
    /// </summary>
    public interface IConnectionStatusService
    {
        /// <summary>
        /// Current connection status (Disconnected, Connecting, Connected, Error)
        /// </summary>
        ConnectionStatus Status { get; }
        
        /// <summary>
        /// Timestamp of last telemetry data received
        /// </summary>
        DateTime LastDataReceived { get; }
        
        /// <summary>
        /// Time elapsed since last telemetry data received
        /// </summary>
        TimeSpan TimeSinceLastData { get; }
        
        /// <summary>
        /// True if telemetry data received within last 3 seconds
        /// </summary>
        bool IsHealthy { get; }
        
        /// <summary>
        /// Current telemetry update rate (updates per second)
        /// </summary>
        int TelemetryRate { get; }
        
        /// <summary>
        /// Fired when connection status changes (Disconnected/Connecting/Connected/Error)
        /// </summary>
        event EventHandler<ConnectionStatus> StatusChanged;
        
        /// <summary>
        /// Fired when health status changes (data flowing vs. stale)
        /// </summary>
        event EventHandler<bool> HealthChanged;
        
        /// <summary>
        /// Called by telemetry service when data received
        /// </summary>
        void OnTelemetryReceived();
    }
}
```

**Rationale**: Interface-first design allows for dependency injection and unit testing.

---

### Step 1.2: Implement Service (25 minutes)

**File**: `src/iRacingOverlay.Core/Services/ConnectionStatusService.cs`

```csharp
using System;
using System.Timers;
using Microsoft.Extensions.Logging;

namespace iRacingOverlay.Core.Services
{
    public class ConnectionStatusService : IConnectionStatusService, IDisposable
    {
        private readonly ILogger<ConnectionStatusService> _logger;
        private readonly Timer _healthCheckTimer;
        private DateTime _lastDataReceived;
        private int _updatesThisSecond;
        private int _telemetryRate;
        private ConnectionStatus _status = ConnectionStatus.Disconnected;
        private bool _isHealthy = false;

        public ConnectionStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    _logger.LogInformation("Connection status changed to: {Status}", value);
                    StatusChanged?.Invoke(this, value);
                }
            }
        }

        public DateTime LastDataReceived => _lastDataReceived;
        public TimeSpan TimeSinceLastData => DateTime.UtcNow - _lastDataReceived;
        public bool IsHealthy => _isHealthy;
        public int TelemetryRate => _telemetryRate;

        public event EventHandler<ConnectionStatus>? StatusChanged;
        public event EventHandler<bool>? HealthChanged;

        public ConnectionStatusService(ILogger<ConnectionStatusService> logger)
        {
            _logger = logger;
            _lastDataReceived = DateTime.MinValue;

            // Health check timer - runs every second
            _healthCheckTimer = new Timer(1000);
            _healthCheckTimer.Elapsed += OnHealthCheckTimer;
            _healthCheckTimer.AutoReset = true;
            _healthCheckTimer.Start();

            _logger.LogInformation("ConnectionStatusService initialized");
        }

        /// <summary>
        /// Called by IRacingTelemetryService when telemetry data received
        /// </summary>
        public void OnTelemetryReceived()
        {
            _lastDataReceived = DateTime.UtcNow;
            _updatesThisSecond++;

            // If we're receiving data, we're connected
            if (Status != ConnectionStatus.Connected)
            {
                Status = ConnectionStatus.Connected;
            }
        }

        private void OnHealthCheckTimer(object? sender, ElapsedEventArgs e)
        {
            // Update telemetry rate
            _telemetryRate = _updatesThisSecond;
            _updatesThisSecond = 0;

            // Check health: data received within last 3 seconds
            var timeSinceData = TimeSinceLastData.TotalSeconds;
            bool newHealthy = timeSinceData < 3.0 && _telemetryRate > 0;

            // Fire health changed event
            if (_isHealthy != newHealthy)
            {
                _isHealthy = newHealthy;
                _logger.LogInformation("Health status changed: Healthy={Healthy}, Rate={Rate}Hz, LastData={Seconds}s ago",
                    _isHealthy, _telemetryRate, timeSinceData);
                HealthChanged?.Invoke(this, _isHealthy);
            }

            // Update connection status based on health
            if (!_isHealthy && Status == ConnectionStatus.Connected)
            {
                // Lost connection (stale data)
                if (timeSinceData > 5.0)
                {
                    Status = ConnectionStatus.Disconnected;
                }
            }

            // Log telemetry rate periodically (every 10 seconds)
            if (e.SignalTime.Second % 10 == 0 && _isHealthy)
            {
                _logger.LogDebug("Telemetry rate: {Rate}Hz", _telemetryRate);
            }
        }

        public void Dispose()
        {
            _healthCheckTimer?.Stop();
            _healthCheckTimer?.Dispose();
            _logger.LogInformation("ConnectionStatusService disposed");
        }
    }
}
```

**Key Features**:
- ✅ Health check every 1 second
- ✅ Tracks telemetry rate (Hz)
- ✅ 3-second stale data threshold
- ✅ Automatic status updates
- ✅ Comprehensive logging

---

### Step 1.3: Integrate with Telemetry Service (10 minutes)

**File**: `src/iRacingOverlay.Core/Services/IRacingTelemetryService.cs`

**Modify constructor**:
```csharp
private readonly IConnectionStatusService? _connectionStatusService;

public IRacingTelemetryService(
    ILogger<IRacingTelemetryService> logger,
    IConnectionStatusService? connectionStatusService = null) // Optional for backward compatibility
{
    _logger = logger;
    _connectionStatusService = connectionStatusService;
}
```

**Modify OnTelemetryUpdate**:
```csharp
private void OnTelemetryUpdate(object? sender, SVappsLAB.iRacingTelemetrySDK.TelemetryData sdkData)
{
    try
    {
        // Notify connection status service (if available)
        _connectionStatusService?.OnTelemetryReceived();
        
        // ... rest of existing code ...
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing telemetry update");
    }
}
```

**Rationale**: Minimal invasive change, backward compatible.

---

## 🎨 Phase 2: Add UI Status Indicators (30 minutes)

### Step 2.1: Update MainWindow Status Bar (20 minutes)

**File**: `src/iRacingOverlay.WPF/MainWindow.xaml`

**Add status bar XAML**:
```xml
<StatusBar Grid.Row="2" Background="#1A1A1A">
    <StatusBarItem>
        <StackPanel Orientation="Horizontal">
            <!-- Connection Status Icon -->
            <TextBlock x:Name="ConnectionStatusIcon" 
                       Text="🔴" 
                       FontSize="14" 
                       Margin="5,0"/>
            
            <!-- Connection Text -->
            <TextBlock x:Name="ConnectionStatusText" 
                       Text="Disconnected" 
                       Foreground="#FF8000" 
                       FontSize="12" 
                       Margin="5,0"/>
            
            <!-- Separator -->
            <Separator Style="{StaticResource {x:Static ToolBar.SeparatorStyleKey}}" 
                       Margin="10,0"/>
            
            <!-- Telemetry Rate -->
            <TextBlock x:Name="TelemetryRateText" 
                       Text="0 Hz" 
                       Foreground="#808080" 
                       FontSize="12" 
                       Margin="5,0"/>
            
            <!-- Separator -->
            <Separator Style="{StaticResource {x:Static ToolBar.SeparatorStyleKey}}" 
                       Margin="10,0"/>
            
            <!-- Driver Name -->
            <TextBlock x:Name="DriverNameText" 
                       Text="No Driver" 
                       Foreground="#808080" 
                       FontSize="12" 
                       Margin="5,0"/>
            
            <!-- Separator -->
            <Separator Style="{StaticResource {x:Static ToolBar.SeparatorStyleKey}}" 
                       Margin="10,0"/>
            
            <!-- Track Name -->
            <TextBlock x:Name="TrackNameText" 
                       Text="No Track" 
                       Foreground="#808080" 
                       FontSize="12" 
                       Margin="5,0"/>
        </StackPanel>
    </StatusBarItem>
</StatusBar>
```

**File**: `src/iRacingOverlay.WPF/MainWindow.xaml.cs`

```csharp
private readonly IConnectionStatusService _connectionStatusService;

// In constructor after InitializeComponent():
_connectionStatusService = serviceProvider.GetRequiredService<IConnectionStatusService>();
_connectionStatusService.StatusChanged += OnConnectionStatusChanged;
_connectionStatusService.HealthChanged += OnConnectionHealthChanged;

// Subscribe to telemetry for driver/track updates
_telemetryService.TelemetryUpdated += OnTelemetryUpdatedForStatus;

private void OnConnectionStatusChanged(object? sender, ConnectionStatus status)
{
    Dispatcher.Invoke(() =>
    {
        switch (status)
        {
            case ConnectionStatus.Disconnected:
                ConnectionStatusIcon.Text = "🔴";
                ConnectionStatusText.Text = "Disconnected";
                ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 51, 51)); // Red
                break;
            
            case ConnectionStatus.Connecting:
                ConnectionStatusIcon.Text = "🟡";
                ConnectionStatusText.Text = "Connecting...";
                ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 0)); // Yellow
                break;
            
            case ConnectionStatus.Connected:
                ConnectionStatusIcon.Text = "🟢";
                ConnectionStatusText.Text = "Connected";
                ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)); // Green
                break;
            
            case ConnectionStatus.Error:
                ConnectionStatusIcon.Text = "🔴";
                ConnectionStatusText.Text = "Error";
                ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 51, 51)); // Red
                break;
        }
    });
}

private void OnConnectionHealthChanged(object? sender, bool isHealthy)
{
    Dispatcher.Invoke(() =>
    {
        var rate = _connectionStatusService.TelemetryRate;
        TelemetryRateText.Text = isHealthy ? $"{rate} Hz" : "Stale";
        TelemetryRateText.Foreground = isHealthy 
            ? new SolidColorBrush(Color.FromRgb(0, 255, 0)) 
            : new SolidColorBrush(Color.FromRgb(255, 128, 0)); // Orange
    });
}

private void OnTelemetryUpdatedForStatus(object? sender, TelemetryData data)
{
    Dispatcher.Invoke(() =>
    {
        // Update driver/track names from telemetry
        DriverNameText.Text = string.IsNullOrEmpty(data.DriverName) 
            ? "No Driver" 
            : data.DriverName;
        
        TrackNameText.Text = string.IsNullOrEmpty(data.TrackName) 
            ? "No Track" 
            : data.TrackName;
        
        // Color: Gray if empty, White if populated
        var driverColor = string.IsNullOrEmpty(data.DriverName)
            ? Color.FromRgb(128, 128, 128)
            : Color.FromRgb(255, 255, 255);
        DriverNameText.Foreground = new SolidColorBrush(driverColor);
        
        var trackColor = string.IsNullOrEmpty(data.TrackName)
            ? Color.FromRgb(128, 128, 128)
            : Color.FromRgb(255, 255, 255);
        TrackNameText.Foreground = new SolidColorBrush(trackColor);
    });
}
```

**Visual Result**:
```
🟢 Connected • 60 Hz • John Doe • Spa-Francorchamps
🟡 Connecting... • 0 Hz • No Driver • No Track
🔴 Disconnected • Stale • No Driver • No Track
```

---

### Step 2.2: Update Widget Base (10 minutes)

**File**: `src/iRacingOverlay.WPF/Core/WidgetBase.cs`

```csharp
protected IConnectionStatusService? ConnectionStatusService { get; private set; }

// Modify constructor to accept optional service
protected WidgetBase(
    ITelemetryService telemetryService, 
    WidgetConfig? config = null,
    IConnectionStatusService? connectionStatusService = null)
{
    _telemetryService = telemetryService;
    Config = config ?? new WidgetConfig();
    ConnectionStatusService = connectionStatusService;
    
    // Subscribe to connection status if available
    if (ConnectionStatusService != null)
    {
        ConnectionStatusService.HealthChanged += OnConnectionHealthChanged;
    }
    
    // ... rest of constructor
}

// Virtual method for widgets to override
protected virtual void OnConnectionHealthChanged(object? sender, bool isHealthy)
{
    // Default: do nothing, let widgets opt-in
}
```

**Usage in Widgets** (Example: FuelWidget):
```csharp
protected override void OnConnectionHealthChanged(object? sender, bool isHealthy)
{
    Dispatcher.Invoke(() =>
    {
        if (!isHealthy)
        {
            // Show "Waiting for iRacing..." instead of "Calculating..."
            // or hide advanced sections
        }
    });
}
```

---

## ⚙️ Phase 3: Register Services in DI (10 minutes)

**File**: `src/iRacingOverlay.WPF/App.xaml.cs` (or wherever services are registered)

```csharp
// In ConfigureServices():
services.AddSingleton<IConnectionStatusService, ConnectionStatusService>();

// Ensure IRacingTelemetryService receives it:
services.AddSingleton<ITelemetryService>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<IRacingTelemetryService>>();
    var connectionStatus = provider.GetRequiredService<IConnectionStatusService>();
    return new IRacingTelemetryService(logger, connectionStatus);
});
```

---

## 🧪 Phase 4: Testing Connection Status (15 minutes)

### Test Scenarios

1. **Cold Start** → Should show 🔴 Disconnected, 0 Hz
2. **iRacing Launch** → Should show 🟡 Connecting, then 🟢 Connected @ 60Hz
3. **iRacing Running** → Should show driver name, track name
4. **iRacing Close** → Should show 🔴 Disconnected after 3-5 seconds
5. **iRacing Pause** → Should show "Stale" telemetry rate

---

# 🎯 Part 2: Centralized SDK/Formula System

## Overview
Create a single source of truth for all telemetry-related conversions, formulas, color thresholds, and formatting logic.

---

## 📦 Phase 5: Create Telemetry Constants (30 minutes)

### Step 5.1: Constants Class (15 minutes)

**File**: `src/iRacingOverlay.Core/Telemetry/TelemetryConstants.cs`

```csharp
namespace iRacingOverlay.Core.Telemetry
{
    /// <summary>
    /// Centralized constants for telemetry data processing
    /// </summary>
    public static class TelemetryConstants
    {
        // ===== UNIT CONVERSION FACTORS =====
        
        /// <summary>Liters to US Gallons (1 L = 0.264172 gal)</summary>
        public const double LITERS_TO_GALLONS = 0.264172;
        
        /// <summary>US Gallons to Liters (1 gal = 3.78541 L)</summary>
        public const double GALLONS_TO_LITERS = 3.78541;
        
        /// <summary>Meters per second to Miles per hour (1 m/s = 2.23694 mph)</summary>
        public const double MPS_TO_MPH = 2.23694;
        
        /// <summary>Meters per second to Kilometers per hour (1 m/s = 3.6 km/h)</summary>
        public const double MPS_TO_KPH = 3.6;
        
        /// <summary>Miles per hour to Kilometers per hour (1 mph = 1.60934 km/h)</summary>
        public const double MPH_TO_KPH = 1.60934;
        
        /// <summary>Celsius to Fahrenheit multiplier</summary>
        public const double CELSIUS_TO_FAHRENHEIT_MULT = 9.0 / 5.0;
        
        /// <summary>Celsius to Fahrenheit offset</summary>
        public const double CELSIUS_TO_FAHRENHEIT_OFFSET = 32.0;

        // ===== TIRE TEMPERATURE THRESHOLDS (Celsius) =====
        
        public static class TireTemp
        {
            /// <summary>Below 60°C - Cold (Blue)</summary>
            public const float Cold = 60f;
            
            /// <summary>60-75°C - Warming up (Cyan)</summary>
            public const float Warming = 75f;
            
            /// <summary>75-95°C - Optimal operating range (Green)</summary>
            public const float Optimal = 95f;
            
            /// <summary>95-110°C - Hot (Orange)</summary>
            public const float Hot = 110f;
            
            /// <summary>Above 110°C - Overheating (Red)</summary>
            public const float Overheating = 110f;
        }

        // ===== ENGINE TEMPERATURE THRESHOLDS (Celsius) =====
        
        public static class EngineTemp
        {
            /// <summary>Below 85°C - Cold (Cyan)</summary>
            public const float Cold = 85f;
            
            /// <summary>85-100°C - Normal operating range (Green)</summary>
            public const float Normal = 100f;
            
            /// <summary>100-115°C - Running warm (Orange)</summary>
            public const float Warm = 115f;
            
            /// <summary>Above 115°C - Overheating (Red)</summary>
            public const float Hot = 115f;
        }

        // ===== FUEL PRESSURE THRESHOLDS (Ratio of baseline) =====
        
        public static class FuelPressure
        {
            /// <summary>≥95% of baseline - Normal (Green)</summary>
            public const double Normal = 0.95;
            
            /// <summary>90-95% of baseline - Warning (Orange)</summary>
            public const double Warning = 0.90;
            
            /// <summary>80-90% of baseline - Critical (Red)</summary>
            public const double Critical = 0.80;
            
            /// <summary>Below 80% - Emergency (Dark Red)</summary>
            public const double Emergency = 0.80;
        }

        // ===== TIRE WEAR THRESHOLDS (Percentage remaining) =====
        
        public static class TireWear
        {
            /// <summary>Above 70% - Good (Green)</summary>
            public const float Good = 70f;
            
            /// <summary>50-70% - Moderate (Yellow)</summary>
            public const float Moderate = 50f;
            
            /// <summary>30-50% - Worn (Orange)</summary>
            public const float Worn = 30f;
            
            /// <summary>Below 30% - Critical (Red)</summary>
            public const float Critical = 30f;
        }

        // ===== FUEL LAPS REMAINING THRESHOLDS =====
        
        public static class FuelLaps
        {
            /// <summary>5+ laps remaining - Normal (Yellow)</summary>
            public const double Normal = 5.0;
            
            /// <summary>2-5 laps remaining - Warning (Orange)</summary>
            public const double Warning = 2.0;
            
            /// <summary>Below 2 laps - Critical (Red)</summary>
            public const double Critical = 2.0;
        }

        // ===== PIT WINDOW THRESHOLDS =====
        
        public static class PitWindow
        {
            /// <summary>Pit window opens at 5 laps fuel remaining</summary>
            public const double EarlyPitLaps = 5.0;
            
            /// <summary>Pit window closes at 2 laps fuel remaining (emergency)</summary>
            public const double LatePitLaps = 2.0;
        }

        // ===== FUEL SAVE THRESHOLDS =====
        
        public static class FuelSave
        {
            /// <summary>Above 15% fuel save needed - Critical (Red)</summary>
            public const double Critical = 15.0;
            
            /// <summary>8-15% fuel save needed - Warning (Orange)</summary>
            public const double Warning = 8.0;
            
            /// <summary>Below 8% fuel save needed - Moderate (Yellow)</summary>
            public const double Moderate = 8.0;
        }
    }
}
```

**Benefits**:
- ✅ Single source of truth
- ✅ Easy to tune thresholds
- ✅ Self-documenting with XML comments
- ✅ Organized by category

---

## 📦 Phase 6: Create Extension Methods (45 minutes)

### Step 6.1: Core Extensions (30 minutes)

**File**: `src/iRacingOverlay.Core/Telemetry/TelemetryExtensions.cs`

```csharp
using System;
using System.Windows.Media;

namespace iRacingOverlay.Core.Telemetry
{
    /// <summary>
    /// Extension methods for telemetry data formatting and conversions
    /// </summary>
    public static class TelemetryExtensions
    {
        // ===== TEMPERATURE CONVERSIONS =====
        
        /// <summary>Convert Celsius to Fahrenheit</summary>
        public static float CelsiusToFahrenheit(this float celsius)
        {
            return (float)(celsius * TelemetryConstants.CELSIUS_TO_FAHRENHEIT_MULT + 
                          TelemetryConstants.CELSIUS_TO_FAHRENHEIT_OFFSET);
        }
        
        /// <summary>Convert Fahrenheit to Celsius</summary>
        public static float FahrenheitToCelsius(this float fahrenheit)
        {
            return (float)((fahrenheit - TelemetryConstants.CELSIUS_TO_FAHRENHEIT_OFFSET) / 
                          TelemetryConstants.CELSIUS_TO_FAHRENHEIT_MULT);
        }
        
        /// <summary>Format temperature with automatic unit conversion</summary>
        public static string FormatTemperature(this float celsius, bool useMetric = true)
        {
            if (useMetric)
                return $"{(int)celsius}°C";
            else
                return $"{(int)celsius.CelsiusToFahrenheit()}°F";
        }

        // ===== FUEL CONVERSIONS =====
        
        /// <summary>Convert liters to gallons</summary>
        public static double LitersToGallons(this double liters)
        {
            return liters * TelemetryConstants.LITERS_TO_GALLONS;
        }
        
        /// <summary>Convert gallons to liters</summary>
        public static double GallonsToLiters(this double gallons)
        {
            return gallons * TelemetryConstants.GALLONS_TO_LITERS;
        }
        
        /// <summary>Format fuel with automatic unit conversion</summary>
        public static string FormatFuel(this double liters, bool useMetric = true)
        {
            if (useMetric)
                return $"{liters:F2} L";
            else
                return $"{liters.LitersToGallons():F2} gal";
        }

        // ===== SPEED CONVERSIONS =====
        
        /// <summary>Convert m/s to mph</summary>
        public static float MetersPerSecondToMph(this float mps)
        {
            return (float)(mps * TelemetryConstants.MPS_TO_MPH);
        }
        
        /// <summary>Convert m/s to km/h</summary>
        public static float MetersPerSecondToKph(this float mps)
        {
            return (float)(mps * TelemetryConstants.MPS_TO_KPH);
        }
        
        /// <summary>Format speed with automatic unit conversion</summary>
        public static string FormatSpeed(this float metersPerSecond, bool useMetric = true)
        {
            if (useMetric)
                return $"{(int)metersPerSecond.MetersPerSecondToKph()} km/h";
            else
                return $"{(int)metersPerSecond.MetersPerSecondToMph()} mph";
        }

        // ===== COLOR CODING =====
        
        /// <summary>Get color for tire temperature based on thresholds</summary>
        public static Color GetTireTemperatureColor(this float tempCelsius)
        {
            return tempCelsius switch
            {
                < TelemetryConstants.TireTemp.Cold => Color.FromRgb(51, 153, 255),    // Blue
                < TelemetryConstants.TireTemp.Warming => Color.FromRgb(0, 255, 255),  // Cyan
                < TelemetryConstants.TireTemp.Optimal => Color.FromRgb(0, 255, 0),    // Green
                < TelemetryConstants.TireTemp.Hot => Color.FromRgb(255, 128, 0),      // Orange
                _ => Color.FromRgb(255, 51, 51)                                        // Red
            };
        }
        
        /// <summary>Get color for engine temperature based on thresholds</summary>
        public static Color GetEngineTemperatureColor(this float tempCelsius)
        {
            return tempCelsius switch
            {
                < TelemetryConstants.EngineTemp.Cold => Color.FromRgb(0, 255, 255),   // Cyan
                < TelemetryConstants.EngineTemp.Normal => Color.FromRgb(0, 255, 0),   // Green
                < TelemetryConstants.EngineTemp.Warm => Color.FromRgb(255, 128, 0),   // Orange
                _ => Color.FromRgb(255, 51, 51)                                        // Red
            };
        }
        
        /// <summary>Get color for fuel pressure based on baseline ratio</summary>
        public static Color GetFuelPressureColor(this double currentPressure, double baselinePressure)
        {
            if (baselinePressure == 0) 
                return Color.FromRgb(128, 128, 128); // Gray - no baseline
            
            double ratio = currentPressure / baselinePressure;
            
            return ratio switch
            {
                >= TelemetryConstants.FuelPressure.Normal => Color.FromRgb(0, 255, 0),      // Green
                >= TelemetryConstants.FuelPressure.Warning => Color.FromRgb(255, 128, 0),   // Orange
                >= TelemetryConstants.FuelPressure.Critical => Color.FromRgb(255, 51, 51),  // Red
                _ => Color.FromRgb(139, 0, 0)                                                // Dark Red
            };
        }
        
        /// <summary>Get color for tire wear percentage remaining</summary>
        public static Color GetTireWearColor(this float wearPercentage)
        {
            return wearPercentage switch
            {
                >= TelemetryConstants.TireWear.Good => Color.FromRgb(0, 255, 0),       // Green
                >= TelemetryConstants.TireWear.Moderate => Color.FromRgb(255, 255, 0), // Yellow
                >= TelemetryConstants.TireWear.Worn => Color.FromRgb(255, 128, 0),     // Orange
                _ => Color.FromRgb(255, 51, 51)                                         // Red
            };
        }
        
        /// <summary>Get color for fuel laps remaining</summary>
        public static Color GetFuelLapsRemainingColor(this double lapsRemaining)
        {
            return lapsRemaining switch
            {
                >= TelemetryConstants.FuelLaps.Normal => Color.FromRgb(255, 255, 0),   // Yellow
                >= TelemetryConstants.FuelLaps.Warning => Color.FromRgb(255, 128, 0),  // Orange
                _ => Color.FromRgb(255, 51, 51)                                         // Red
            };
        }
    }
}
```

---

### Step 6.2: Formula Helpers (15 minutes)

**File**: `src/iRacingOverlay.Core/Telemetry/TelemetryFormulas.cs`

```csharp
using System;

namespace iRacingOverlay.Core.Telemetry
{
    /// <summary>
    /// Common telemetry calculation formulas
    /// </summary>
    public static class TelemetryFormulas
    {
        // ===== FUEL CALCULATIONS =====
        
        /// <summary>Calculate laps remaining based on current fuel and average usage</summary>
        public static double CalculateLapsRemaining(double currentFuel, double avgFuelPerLap)
        {
            if (avgFuelPerLap <= 0) return 0;
            return currentFuel / avgFuelPerLap;
        }
        
        /// <summary>Calculate fuel needed to complete race with safety margin</summary>
        public static double CalculateFuelNeeded(
            double lapsRemaining, 
            double avgFuelPerLap, 
            int safetyMarginLaps = 1)
        {
            return (lapsRemaining + safetyMarginLaps) * avgFuelPerLap;
        }
        
        /// <summary>Calculate fuel to add at pit stop</summary>
        public static double CalculateFuelToAdd(
            double currentFuel, 
            double lapsRemaining, 
            double avgFuelPerLap,
            double tankCapacity,
            int safetyMarginLaps = 1)
        {
            double fuelNeeded = CalculateFuelNeeded(lapsRemaining, avgFuelPerLap, safetyMarginLaps);
            double fuelToAdd = fuelNeeded - currentFuel;
            
            // Cap at tank capacity
            if (tankCapacity > 0 && fuelToAdd > tankCapacity)
                return tankCapacity;
            
            return Math.Max(0, fuelToAdd);
        }
        
        /// <summary>Calculate optimal pit window (early and late lap numbers)</summary>
        public static (int earlyLap, int lateLap) CalculatePitWindow(
            double currentFuel,
            double avgFuelPerLap,
            int currentLap)
        {
            double fuelLapsRemaining = CalculateLapsRemaining(currentFuel, avgFuelPerLap);
            
            int earlyLap = currentLap + (int)(fuelLapsRemaining - TelemetryConstants.PitWindow.EarlyPitLaps);
            int lateLap = currentLap + (int)(fuelLapsRemaining - TelemetryConstants.PitWindow.LatePitLaps);
            
            // Ensure realistic values
            earlyLap = Math.Max(currentLap + 1, earlyLap);
            lateLap = Math.Max(earlyLap, lateLap);
            
            return (earlyLap, lateLap);
        }
        
        /// <summary>Calculate required fuel save percentage</summary>
        public static double CalculateFuelSavePercentage(
            double currentFuel,
            double avgFuelPerLap,
            double lapsRemaining)
        {
            if (lapsRemaining <= 0 || avgFuelPerLap <= 0) return 0;
            
            double fuelNeeded = lapsRemaining * avgFuelPerLap;
            double shortfall = fuelNeeded - currentFuel;
            
            if (shortfall <= 0) return 0; // No save needed
            
            double targetConsumption = currentFuel / lapsRemaining;
            double savingsNeeded = avgFuelPerLap - targetConsumption;
            
            return (savingsNeeded / avgFuelPerLap) * 100.0;
        }

        // ===== RACE STRATEGY CALCULATIONS =====
        
        /// <summary>Calculate number of pit stops needed</summary>
        public static int CalculatePitStopsNeeded(
            double totalRaceLaps,
            double maxLapsPerStint)
        {
            if (maxLapsPerStint <= 0) return 0;
            return Math.Max(0, (int)Math.Ceiling(totalRaceLaps / maxLapsPerStint) - 1);
        }
        
        /// <summary>Calculate maximum laps per stint with safety margin</summary>
        public static double CalculateMaxLapsPerStint(
            double tankCapacity,
            double avgFuelPerLap,
            int safetyMarginLaps = 1)
        {
            if (avgFuelPerLap <= 0) return 0;
            return (tankCapacity / avgFuelPerLap) - safetyMarginLaps;
        }

        // ===== TIME-BASED CALCULATIONS =====
        
        /// <summary>Estimate laps remaining in time-based session</summary>
        public static double EstimateLapsRemaining(
            double sessionTimeRemaining, 
            double lastLapTime)
        {
            if (lastLapTime <= 0) return 0;
            return sessionTimeRemaining / lastLapTime;
        }
        
        /// <summary>Estimate total race laps for time-based session</summary>
        public static double EstimateTotalRaceLaps(
            double totalSessionTime,
            double sessionTimeRemaining,
            double lastLapTime)
        {
            if (lastLapTime <= 0) return 0;
            return (totalSessionTime + sessionTimeRemaining) / lastLapTime;
        }
    }
}
```

**Benefits**:
- ✅ Reusable formulas across widgets
- ✅ Unit testable (no UI dependencies)
- ✅ Consistent calculation logic
- ✅ Well-documented with XML comments

---

## 📦 Phase 7: Migrate Widgets to Use Centralized System (90 minutes)

### Step 7.1: Migrate FuelWidget (30 minutes)

**Before** (Current code with duplication):
```csharp
// Line 760 in FuelWidget.xaml.cs
private string FormatFuelValue(double value)
{
    if (AppSettings.Instance.UseMetricUnits)
    {
        return $"{value:F2} L";
    }
    else
    {
        // Convert liters to gallons (1 L = 0.264172 gal)
        double gallons = value * 0.264172;
        return $"{gallons:F2} gal";
    }
}
```

**After** (Using centralized extension):
```csharp
using iRacingOverlay.Core.Telemetry;

private string FormatFuelValue(double value)
{
    return value.FormatFuel(AppSettings.Instance.UseMetricUnits);
}
```

**Color coding replacement** (Line 408+):
```csharp
// BEFORE:
if (lapsRemaining > 0 && lapsRemaining < 2.0)
{
    LapsRemainingText.Foreground = new SolidColorBrush(
        Color.FromRgb(255, 51, 51)); // Red
}
else if (lapsRemaining > 0 && lapsRemaining < 5.0)
{
    LapsRemainingText.Foreground = new SolidColorBrush(
        Color.FromRgb(255, 128, 0)); // Orange
}

// AFTER:
LapsRemainingText.Foreground = new SolidColorBrush(
    lapsRemaining.GetFuelLapsRemainingColor());
```

**Formula replacement** (Line 408-471):
```csharp
// BEFORE: Manual calculation
double fuelNeededToFinish = lapsRemainingInRace * avgFuelPerLap;
double fuelToAdd = fuelNeededToFinish - currentFuel;
fuelToAdd += avgFuelPerLap; // Safety margin
if (_fuelMaxCapacity > 0 && fuelToAdd > _fuelMaxCapacity)
    fuelToAdd = _fuelMaxCapacity;

// AFTER: Use centralized formula
double fuelToAdd = TelemetryFormulas.CalculateFuelToAdd(
    currentFuel, 
    lapsRemainingInRace, 
    avgFuelPerLap, 
    _fuelMaxCapacity,
    safetyMarginLaps: 1);
```

**Estimated changes**: ~20 replacements, 50+ lines eliminated

---

### Step 7.2: Migrate DataWidget (30 minutes)

**File**: `src/iRacingOverlay.WPF/Widgets/DataWidget/DataWidget.cs`

**Temperature conversion** (Lines 483, 502):
```csharp
// BEFORE:
text = AppSettings.Instance.UseMetricUnits 
    ? $"{(int)temp}°C" 
    : $"{(int)(temp * 9 / 5 + 32)}°F";

// AFTER:
text = temp.FormatTemperature(AppSettings.Instance.UseMetricUnits);
```

**Tire temp color coding** (Lines 500-520):
```csharp
// BEFORE:
if (tireTemp < 60) color = Colors.Blue;
else if (tireTemp < 75) color = Colors.Cyan;
else if (tireTemp < 95) color = Colors.LightGreen;
else if (tireTemp < 110) color = Colors.Orange;
else color = Colors.Red;

// AFTER:
color = tireTemp.GetTireTemperatureColor();
```

**Speed formatting** (Lines 399-410):
```csharp
// BEFORE:
if (AppSettings.Instance.UseMetricUnits)
    text = $"{(int)(speedMs * 3.6f)} km/h";
else
    text = $"{(int)(speedMs * 2.23694f)} mph";

// AFTER:
text = speedMs.FormatSpeed(AppSettings.Instance.UseMetricUnits);
```

**Estimated changes**: ~15 replacements, 40+ lines eliminated

---

### Step 7.3: Migrate GearGaugeWidget (30 minutes)

**File**: `src/iRacingOverlay.WPF/Widgets/GearGaugeWidget/GearGaugeWidget.cs`

Similar replacements as DataWidget for:
- Temperature formatting (Lines 377, 379)
- Speed formatting (Line 381)
- Engine temp color coding

**Estimated changes**: ~10 replacements, 30+ lines eliminated

---

## 🧪 Phase 8: Testing & Validation (45 minutes)

### Unit Tests

**File**: `tests/iRacingOverlay.Core.Tests/Telemetry/TelemetryExtensionsTests.cs`

```csharp
using Xunit;
using iRacingOverlay.Core.Telemetry;

public class TelemetryExtensionsTests
{
    [Theory]
    [InlineData(0, 32)]
    [InlineData(100, 212)]
    [InlineData(20, 68)]
    public void CelsiusToFahrenheit_ConvertsCorrectly(float celsius, float expectedFahrenheit)
    {
        var result = celsius.CelsiusToFahrenheit();
        Assert.Equal(expectedFahrenheit, result, precision: 0);
    }
    
    [Theory]
    [InlineData(10.0, 2.64172)] // 10L = ~2.64 gal
    [InlineData(50.0, 13.2086)] // 50L = ~13.2 gal
    public void LitersToGallons_ConvertsCorrectly(double liters, double expectedGallons)
    {
        var result = liters.LitersToGallons();
        Assert.Equal(expectedGallons, result, precision: 2);
    }
    
    [Theory]
    [InlineData(50f, "Blue")]    // Cold
    [InlineData(70f, "Cyan")]    // Warming
    [InlineData(85f, "Green")]   // Optimal
    [InlineData(105f, "Orange")] // Hot
    [InlineData(120f, "Red")]    // Overheating
    public void GetTireTemperatureColor_ReturnsCorrectColor(float temp, string expectedColorName)
    {
        var color = temp.GetTireTemperatureColor();
        // Assert color matches expected range
    }
}
```

### Integration Tests

1. **Fuel Widget Test**:
   - Verify FormatFuel() returns correct units
   - Verify laps remaining color changes at thresholds
   - Verify pit window calculations match formula

2. **Data Widget Test**:
   - Verify temperature conversions match
   - Verify tire color coding consistent
   - Verify speed formatting correct

3. **Cross-Widget Consistency**:
   - Same tire temp shows same color in all widgets
   - Same temperature value formats identically
   - Fuel values match across widgets

---

## 📊 Phase 9: Documentation & Cleanup (30 minutes)

### Step 9.1: Update Widget Documentation

Add comments to each widget:
```csharp
// Uses centralized TelemetryExtensions for:
//  - Fuel formatting: value.FormatFuel()
//  - Temperature formatting: temp.FormatTemperature()
//  - Color coding: temp.GetTireTemperatureColor()
```

### Step 9.2: Create Migration Guide

**File**: `docs/Migration-Guide-Centralized-Telemetry.md`

Document:
- What changed
- How to use new extensions
- How to add new thresholds
- Breaking changes (if any)

---

## 📋 Summary Checklist

### Connection Status System
- [ ] Create IConnectionStatusService interface
- [ ] Implement ConnectionStatusService
- [ ] Integrate with IRacingTelemetryService
- [ ] Add MainWindow status bar UI
- [ ] Update WidgetBase with connection awareness
- [ ] Register services in DI container
- [ ] Test connection status changes
- [ ] Verify telemetry rate display

### Centralized SDK/Formula System
- [ ] Create TelemetryConstants.cs
- [ ] Create TelemetryExtensions.cs
- [ ] Create TelemetryFormulas.cs
- [ ] Migrate FuelWidget to use extensions
- [ ] Migrate DataWidget to use extensions
- [ ] Migrate GearGaugeWidget to use extensions
- [ ] Write unit tests for extensions
- [ ] Write unit tests for formulas
- [ ] Run integration tests
- [ ] Update documentation
- [ ] Remove old duplicated code
- [ ] Verify consistent behavior across widgets

---

## 🎉 Success Metrics

### Before
- ❌ 400+ lines of duplicated conversion code
- ❌ Inconsistent color thresholds between widgets
- ❌ No connection status feedback
- ❌ "N/A" confusion when disconnected
- ❌ Hard-coded magic numbers everywhere

### After
- ✅ Single source of truth for all conversions
- ✅ Consistent color coding across all widgets
- ✅ Real-time connection status (🔴/🟡/🟢) @ 60Hz
- ✅ Clear "Waiting for iRacing..." messaging
- ✅ Named constants instead of magic numbers
- ✅ 400+ lines eliminated
- ✅ Unit testable formula logic
- ✅ Easy to tune thresholds globally

---

## 🚀 Next Steps After Implementation

1. **Performance Profiling**: Verify no performance regression
2. **User Testing**: Gather feedback on status indicators
3. **Tuning**: Adjust color thresholds based on real-world data
4. **Expansion**: Add more centralized formulas (lap time deltas, wear predictions, etc.)
5. **Documentation**: Create video tutorial on using new systems

---

## 📝 Notes

- All changes are **backward compatible**
- Services are **optional** in constructors (gradual migration)
- Extension methods **don't break existing code**
- Constants can be **overridden per car/track** in future
- Connection status can be **extended** for network latency tracking

---

**Total Estimated Time**: 4-6 hours  
**Recommended Order**: Connection Status → Constants → Extensions → Migration → Testing  
**Risk Level**: Low (non-breaking changes)  
**Priority**: High (improves UX and code quality significantly)
