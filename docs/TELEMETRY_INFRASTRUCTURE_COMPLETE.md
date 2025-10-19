# Telemetry Infrastructure - Implementation Complete

## ✅ What Was Created

### 1. **Comprehensive Documentation** (`docs/TELEMETRY_VARIABLE_REGISTRY.md`)
A complete reference guide containing:
- **324 variables** from iRacing SDK (cataloged from live session)
- **Organized by category**: Speed, Engine, Tires, Braking, Timing, Weather, etc.
- **Full metadata**: Type, Units, Range, Source (Telemetry vs YAML), Availability
- **Conversion formulas**: Metric/Imperial, derived calculations
- **Code examples**: Ready-to-use C# snippets for each category
- **Critical findings**: Documented missing variables (wheel speeds)

### 2. **Unit Conversion System** (`src/iRacingOverlay.Core/Telemetry/UnitConversions.cs`)
Static helper class with conversion methods:
- **Speed**: m/s ↔ km/h ↔ mph
- **Temperature**: Celsius ↔ Fahrenheit
- **Volume**: Liters ↔ Gallons
- **Angles**: Radians ↔ Degrees
- **Acceleration**: m/s² ↔ G-units
- **Pressure**: Bar ↔ PSI ↔ kPa
- **Distance**: Meters ↔ Feet ↔ Miles
- **Fuel**: kg/hr ↔ L/hr ↔ gal/hr

### 3. **Telemetry Calculations** (`src/iRacingOverlay.Core/Telemetry/TelemetryCalculations.cs`)
Derived value calculators:
- **Tire calculations**: Average temps, wear, imbalance detection
- **Fuel strategy**: Per-lap consumption, laps remaining, pit stop required
- **G-force**: Total G magnitude from lateral + longitudinal
- **Lap time formatting**: M:SS.mmm, delta time display
- **Track position**: Distance calculations, proximity detection
- **Brake analysis**: Pressure averages, bias verification, wheel lock detection
- **RPM zones**: Shift light logic, over-rev detection
- **Warning levels**: Temperature status, fuel status enums

---

## 📦 Ready-to-Use Examples

### Example 1: Display Speed in User's Preferred Units
```csharp
using iRacingOverlay.Core.Telemetry;

// Get speed from telemetry (always m/s)
float speed = telemetryData.Speed;

// Convert based on user setting
string displaySpeed = AppSettings.Instance.UseMetric 
    ? $"{UnitConversions.MpsToKmh(speed):F0} km/h"
    : $"{UnitConversions.MpsToMph(speed):F0} mph";
```

### Example 2: Calculate Fuel Strategy
```csharp
using iRacingOverlay.Core.Telemetry;

// Calculate fuel per lap
float fuelPerLap = TelemetryCalculations.FuelPerLap(
    totalFuelUsed: 45.5f, 
    lapsCompleted: 10
); // Result: 4.55 L/lap

// How many laps can we do?
float lapsRemaining = TelemetryCalculations.LapsRemainingOnFuel(
    currentFuel: telemetryData.FuelLevel,
    fuelPerLap: fuelPerLap
);

// Need a pit stop?
bool needPit = TelemetryCalculations.RequiresFuelPitStop(
    currentFuel: telemetryData.FuelLevel,
    fuelPerLap: fuelPerLap,
    lapsRemaining: 20
);
```

### Example 3: Tire Health Check
```csharp
using iRacingOverlay.Core.Telemetry;

// Calculate average LF tire wear
float avgWear = TelemetryCalculations.AverageTireWear(
    telemetryData.LFwearL,
    telemetryData.LFwearM,
    telemetryData.LFwearR
);

// Get remaining life
float lifeRemaining = TelemetryCalculations.TireLifeRemaining(avgWear);

// Check if critical
bool isCritical = TelemetryCalculations.IsTireCriticallyWorn(avgWear);

// Display
Console.WriteLine($"LF Tire: {lifeRemaining:F0}% life remaining");
if (isCritical) Console.WriteLine("⚠️ TIRE CRITICAL - PIT NOW!");
```

### Example 4: Detect Possible Wheel Lock
```csharp
using iRacingOverlay.Core.Telemetry;

// Calculate average front brake pressure
float frontPress = TelemetryCalculations.AverageFrontBrakePressure(
    telemetryData.LFbrakeLinePress,
    telemetryData.RFbrakeLinePress
);

// Check for wheel lock (indirect method, wheel speeds unavailable)
bool wheelLock = TelemetryCalculations.PossibleWheelLock(
    brake: telemetryData.Brake,
    absActive: telemetryData.BrakeABSactive,
    speed: telemetryData.Speed,
    brakePressure: frontPress
);

if (wheelLock) 
{
    // Flash warning on display
    Console.WriteLine("🔴 POSSIBLE WHEEL LOCK DETECTED!");
}
```

### Example 5: Track Position & Proximity
```csharp
using iRacingOverlay.Core.Telemetry;

// Track length from YAML (Spa = 7004m)
float trackLength = 7004f;

// Distance to start/finish
float distToSF = TelemetryCalculations.DistanceToStartFinish(
    telemetryData.LapDistPct,
    trackLength
);

// Is car #5 nearby?
bool isCarNear = TelemetryCalculations.IsCarInProximity(
    playerPct: telemetryData.LapDistPct,
    carPct: telemetryData.CarIdxLapDistPct[5],
    proximityMeters: 50f,  // ±50m
    trackLengthMeters: trackLength
);
```

### Example 6: RPM Shift Light Logic
```csharp
using iRacingOverlay.Core.Telemetry;

// Check shift zones
bool inShiftZone = TelemetryCalculations.InShiftZone(
    telemetryData.RPM,
    telemetryData.PlayerCarSLFirstRPM,
    telemetryData.PlayerCarSLShiftRPM
);

bool shouldShift = TelemetryCalculations.ShouldShift(
    telemetryData.RPM,
    telemetryData.PlayerCarSLShiftRPM
);

bool overRev = TelemetryCalculations.IsOverRev(
    telemetryData.RPM,
    telemetryData.PlayerCarSLBlinkRPM
);

// Display logic
if (overRev)
    SetShiftLightColor(Colors.Red, blink: true);
else if (shouldShift)
    SetShiftLightColor(Colors.Red, blink: false);
else if (inShiftZone)
    SetShiftLightColor(Colors.Yellow, blink: false);
```

---

## 🔍 Key Findings from Research

### ✅ Available Variables
- **Core driving**: Speed, RPM, Gear, Throttle, Brake, Clutch ✅
- **Tire data**: Temps (3 zones), Wear (3 zones), Brake pressures ✅
- **Engine**: RPM, Temps (water, oil), Fuel level, Shift points ✅
- **Driver aids**: dcTractionControl (setting), BrakeABSactive ✅
- **Position**: Lap, LapDistPct, Position, all multi-car arrays ✅
- **Weather**: Air temp, track temp, air density, humidity ✅

### ❌ Missing Variables
- **Wheel speeds**: `LFspeed`, `RFspeed`, `LRspeed`, `RRspeed` ❌
  - **Not in live telemetry dumps** (as of Oct 2025)
  - **Documentation mentions them** but they don't exist in actual SDK
  - **Impact**: Cannot calculate wheel slip ratio for TC/wheel lock detection
  
### 🔧 Workarounds Implemented
1. **TC Activation**: Can't detect actual intervention, only if enabled (level > 0)
2. **Wheel Lock**: Use indirect method via brake pressure + ABS state + speed
3. **Brake Bias Verification**: Calculate actual bias from pressure readings

---

## 📋 Usage Recommendations

### For New Features
1. **Check TELEMETRY_VARIABLE_REGISTRY.md** first to see available variables
2. **Use UnitConversions** for all unit transformations
3. **Use TelemetryCalculations** for complex formulas
4. **Add new variables** to `RequiredTelemetryVars` if needed

### Best Practices
```csharp
// ✅ GOOD: Use helper functions
float speedKmh = UnitConversions.MpsToKmh(telemetryData.Speed);

// ❌ BAD: Manual conversion (error-prone, not maintainable)
float speedKmh = telemetryData.Speed * 3.6f;
```

### Performance Notes
- **UnitConversions**: All methods are `static` - zero overhead
- **TelemetryCalculations**: Pure functions, no state, thread-safe
- **Can be called 60 times/second** (every telemetry update)

---

## 🚀 Next Steps

### Immediate Use Cases
1. ✅ **Fuel calculator widget**: Use fuel strategy calculations
2. ✅ **Tire health display**: Use tire wear/temp calculations
3. ✅ **Shift light enhancement**: Use RPM zone calculations
4. ✅ **Wheel lock indicator**: Use indirect detection method

### Future Enhancements
When/if iRacing adds wheel speed variables:
```csharp
// Add these to RequiredTelemetryVars
TelemetryVar.LFspeed,
TelemetryVar.RFspeed,
TelemetryVar.LRspeed,
TelemetryVar.RRspeed,

// Then implement in TelemetryCalculations.cs:
public static bool DetectWheelLock(float wheelSpeed, float vehicleSpeed, float brake)
{
    return wheelSpeed < 1.39f &&  // < 5 km/h
           vehicleSpeed > 5.56f && // > 20 km/h
           brake > 0.5f;           // Heavy braking
}

public static bool DetectTCActivation(float rearWheelSpeed, float vehicleSpeed, int tcLevel)
{
    float slipRatio = (rearWheelSpeed - vehicleSpeed) / vehicleSpeed;
    return slipRatio > 0.05f && tcLevel > 0;  // 5% slip threshold
}
```

---

## 📚 Files Created

1. **Documentation**: `docs/TELEMETRY_VARIABLE_REGISTRY.md` (1000+ lines)
2. **Unit Conversions**: `src/iRacingOverlay.Core/Telemetry/UnitConversions.cs`
3. **Calculations**: `src/iRacingOverlay.Core/Telemetry/TelemetryCalculations.cs`
4. **This Summary**: `docs/TELEMETRY_INFRASTRUCTURE_COMPLETE.md`

**Build Status**: ✅ All code compiles successfully with no errors

---

## 💡 Example: Complete Feature Using New Infrastructure

### Fuel Warning Display (Full Implementation)
```csharp
using iRacingOverlay.Core.Telemetry;
using iRacingOverlay.Core.Models;

public class FuelWarningWidget
{
    private float _totalFuelUsed = 0f;
    private float _lastFuelLevel;
    
    public void OnTelemetryUpdate(TelemetryData data)
    {
        // Track total fuel consumed
        if (_lastFuelLevel > 0)
        {
            float consumed = _lastFuelLevel - data.FuelLevel;
            if (consumed > 0) _totalFuelUsed += consumed;
        }
        _lastFuelLevel = data.FuelLevel;
        
        // Calculate strategy
        float fuelPerLap = TelemetryCalculations.FuelPerLap(_totalFuelUsed, data.Lap);
        float lapsRemaining = TelemetryCalculations.LapsRemainingOnFuel(
            data.FuelLevel, 
            fuelPerLap
        );
        
        // Get fuel status
        var status = TelemetryCalculations.GetFuelStatus(data.FuelLevel);
        
        // Display in user's units
        bool useMetric = AppSettings.Instance.UseMetric;
        string fuelDisplay = useMetric
            ? $"{data.FuelLevel:F1} L"
            : $"{UnitConversions.LitersToGallons(data.FuelLevel):F1} gal";
        
        // Update UI
        FuelText.Text = fuelDisplay;
        LapsRemainingText.Text = $"{lapsRemaining:F1} laps";
        
        // Warning colors
        FuelText.Foreground = status switch
        {
            FuelStatus.Critical => Brushes.Red,
            FuelStatus.Low => Brushes.Yellow,
            _ => Brushes.White
        };
        
        // Flash on critical
        if (status == FuelStatus.Critical)
        {
            BlinkTimer.Start();
        }
    }
}
```

This infrastructure is now ready to power **all future telemetry-based features**! 🎯
