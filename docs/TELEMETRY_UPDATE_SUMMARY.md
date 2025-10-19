# Telemetry Infrastructure Update - October 18, 2025

## ✅ Updates Completed

### 1. **Widget Code Modernization**
All hardcoded unit conversions replaced with `UnitConversions` utility methods:

#### MRTOneWidget.cs
- ✅ Temperature conversions: `temp * 9 / 5 + 32` → `UnitConversions.CelsiusToFahrenheit(temp)`
- ✅ Speed conversions: `speedMs * 3.6f` → `UnitConversions.MpsToKmh(speedMs)`
- ✅ Speed conversions: `speedMs * 2.23694f` → `UnitConversions.MpsToMph(speedMs)`
- ✅ Added `using iRacingOverlay.Core.Telemetry;` namespace

#### DataWidget.cs
- ✅ Temperature conversions updated for `WaterTemp`, `OilTemp`, tire temps
- ✅ Speed conversions updated for all speed fields
- ✅ Added `using iRacingOverlay.Core.Telemetry;` namespace

#### IRacingTelemetryService.cs
- ✅ Diagnostic logging updated to use `UnitConversions.MpsToKmh()`

#### TelemetryData.cs
- ✅ Calculated properties `SpeedKmh` and `SpeedMph` now use `UnitConversions` methods

---

### 2. **Expanded Telemetry Variable Coverage**
Added **22 new telemetry variables** to `RequiredTelemetryVars`:

#### Fluids & Monitoring
- ✅ `FuelUsePerHour` - Fuel consumption rate (kg/hr)
- ✅ `FuelPress` - Fuel line pressure (bar)
- ✅ `WaterLevel` - Coolant level (liters)
- ✅ `OilLevel` - Oil level (liters)
- ✅ `OilPress` - Oil pressure (bar)

#### Lap Timing (Enhanced)
- ✅ `LapCurrentLapTime` - Current lap time from SDK
- ✅ `LapDeltaToBestLap` - Delta to personal best from SDK
- ✅ `LapDeltaToBestLap_DD` - Delta-delta (rate of change)
- ✅ `LapDeltaToSessionBestLap` - Delta to session best from SDK

#### Environmental Conditions
- ✅ `AirDensity` - Air density (kg/m³, affects downforce)
- ✅ `AirPressure` - Atmospheric pressure (hPa)
- ✅ `RelativeHumidity` - Relative humidity (%)
- ✅ `OnPitRoad` - Pit road detection (bool)
- ✅ `Skies` - Sky condition enum
- ✅ `WeatherType` - Weather type enum
- ✅ `FogLevel` - Fog density (%)

#### Motion & Orientation
- ✅ `VelocityX`, `VelocityY`, `VelocityZ` - World-space velocities (m/s)
- ✅ `Pitch`, `PitchRate` - Vehicle pitch (radians, rad/s)
- ✅ `Roll`, `RollRate` - Vehicle roll (radians, rad/s)

#### Driver Inputs (Raw)
- ✅ `BrakeRaw` - Raw brake pedal (pre-ABS)
- ✅ `ThrottleRaw` - Raw throttle (pre-TC)
- ✅ `ClutchRaw` - Raw clutch pedal
- ✅ `HandbrakeRaw` - Handbrake input (rally cars)

---

### 3. **TelemetryData Model Enhancements**
Updated `TelemetryData.cs` with **22 new properties** matching the SDK variables:

#### Added Properties
All new properties include XML documentation with:
- Clear descriptions
- Units of measurement
- Source context (SDK vs calculated)

#### Backward Compatibility
- ✅ Kept existing calculated properties (`CurrentLapTime`, `DeltaToBestLap`, `DeltaToSessionBest`)
- ✅ Added SDK-sourced equivalents with `Lap` prefix for clarity
- ✅ All existing code continues to work without changes

---

### 4. **Telemetry Service Mapping**
Updated `IRacingTelemetryService.cs` to map all 22 new variables:
- ✅ Proper `GetValueOrDefault()` handling for nullable SDK values
- ✅ Enum casting where needed (Skies, WeatherType)
- ✅ Organized by category matching the documentation

---

## 📊 Current Telemetry Coverage

### Total Variables in RequiredTelemetryVars
**Before Update**: ~98 variables  
**After Update**: **120+ variables**

### Categories Covered
- ✅ **Core Driving**: Speed, RPM, Gear, Inputs, G-forces
- ✅ **Engine & Fluids**: All temperatures, pressures, levels
- ✅ **Tires**: All 3-zone temps and wear (12 temps, 12 wear values)
- ✅ **Braking**: All 4 brake line pressures, ABS status, bias
- ✅ **Timing**: All lap times, deltas, session info
- ✅ **Position**: Live positions, multi-car arrays (64 cars)
- ✅ **Weather**: Air, track temps, humidity, pressure, density
- ✅ **Motion**: All orientation angles and rates
- ✅ **Radar/Proximity**: Full multi-car position tracking
- ✅ **Driver Aids**: TC, ABS, pit limiter
- ✅ **Professional Shift Lights**: All 4 SDK shift point values

---

## 🔍 Variables Still Not Available (Per SDK Research)

### Confirmed Missing from iRacing SDK
- ❌ **Wheel Speeds**: `LFspeed`, `RFspeed`, `LRspeed`, `RRspeed`
  - Documented but not in actual SDK builds (as of Oct 2025)
  - Cannot calculate wheel slip ratios directly
  - Workaround: Use indirect detection via brake pressure + ABS state

- ❌ **TC Activation State**: No `dcTCactive` variable
  - Can detect TC **level** (`dcTractionControl`), but not **active intervention**
  - Similar to ABS, but ABS has `BrakeABSactive` while TC doesn't

---

## 🚀 Usage Examples

### Using New Fluid Monitoring
```csharp
// Check oil pressure warning
if (data.OilPress < 2.0f)  // Low pressure threshold
{
    ShowWarning("⚠️ LOW OIL PRESSURE");
}

// Fuel consumption rate
string fuelRate = AppSettings.Instance.UseMetricUnits
    ? $"{UnitConversions.FuelKgPerHourToLitersPerHour(data.FuelUsePerHour):F1} L/hr"
    : $"{UnitConversions.LitersToGallons(
        UnitConversions.FuelKgPerHourToLitersPerHour(data.FuelUsePerHour)):F1} gal/hr";
```

### Using Environmental Data
```csharp
// Air density affects downforce
float densityFactor = data.AirDensity / 1.225f;  // 1.225 = sea level standard
string downforceInfo = $"Downforce: {densityFactor * 100:F0}% of standard";

// Weather display
string weather = data.WeatherType switch
{
    0 => "Clear",
    1 => "Partly Cloudy",
    2 => "Mostly Cloudy",
    3 => "Overcast",
    _ => "Unknown"
};
```

### Using Motion Data
```csharp
// Calculate slip angle (requires velocity components)
float lateralVel = data.VelocityX;
float longitudinalVel = data.VelocityZ;
float slipAngle = MathF.Atan2(lateralVel, longitudinalVel);
float slipAngleDegrees = UnitConversions.RadiansToDegrees(slipAngle);

// Vehicle orientation
float pitchDeg = UnitConversions.RadiansToDegrees(data.Pitch);
float rollDeg = UnitConversions.RadiansToDegrees(data.Roll);
```

### Using Raw Inputs (TC/ABS Detection)
```csharp
// Detect TC intervention (indirect)
float inputDifference = data.ThrottleRaw - data.Throttle;
bool tcLikelyActive = data.TractionControl > 0 && inputDifference > 0.1f;

// Detect ABS intervention (direct)
bool absActive = data.BrakeABSactive;  // Direct from SDK
float brakeDifference = data.BrakeRaw - data.Brake;  // Can also check difference
```

---

## 🎯 Benefits of This Update

### Code Quality
- ✅ **Zero hardcoded conversions** - All use centralized utilities
- ✅ **Maintainability** - Single source of truth for unit conversions
- ✅ **Consistency** - Same conversion logic everywhere
- ✅ **Type safety** - Static methods prevent runtime errors

### Data Richness
- ✅ **120+ telemetry variables** available for widgets
- ✅ **Comprehensive monitoring** - All fluid levels, pressures, temperatures
- ✅ **Advanced physics** - Full motion/orientation data
- ✅ **Environmental awareness** - Weather, density, humidity

### Future-Proof
- ✅ **Documentation** - Complete registry of all 324 SDK variables
- ✅ **Easy expansion** - Add new variables by updating 3 files:
  1. `RequiredTelemetryVars` in `IRacingTelemetryService.cs`
  2. Property in `TelemetryData.cs`
  3. Mapping in `OnTelemetryDataReceived()`

---

## 📝 Files Modified

### Core Infrastructure
- ✅ `src/iRacingOverlay.Core/Services/IRacingTelemetryService.cs`
- ✅ `src/iRacingOverlay.Core/Models/TelemetryData.cs`

### Widgets
- ✅ `src/iRacingOverlay.WPF/Widgets/MRTOneWidget/MRTOneWidget.cs`
- ✅ `src/iRacingOverlay.WPF/Widgets/DataWidget/DataWidget.cs`

### Documentation
- ✅ `docs/TELEMETRY_UPDATE_SUMMARY.md` (this file)

---

## ✅ Build Status
- **Build Result**: ✅ SUCCESS
- **Errors**: 0
- **Warnings**: 0
- **Test Status**: All existing functionality preserved

---

## 🔄 Next Steps (Optional Enhancements)

### Potential Widget Features Using New Data
1. **Fluid Monitor Widget**: Display oil/water/fuel pressures and levels with warnings
2. **Weather Widget**: Show air density, humidity, track temp delta to air temp
3. **Physics Debug Widget**: Display pitch/roll angles, velocity vectors
4. **Input Monitor Widget**: Show raw vs processed inputs (TC/ABS intervention visualization)
5. **Fuel Economy Widget**: Real-time fuel consumption rate (L/hr or gal/hr)

### Documentation Enhancements
- Consider adding widget examples using new variables
- Create "Quick Start" guide for adding new telemetry variables
- Document best practices for unit conversions in widget code

---

## 📚 Reference Documentation
- **Variable Registry**: `docs/TELEMETRY_VARIABLE_REGISTRY.md` (324 variables cataloged)
- **Infrastructure Guide**: `docs/TELEMETRY_INFRASTRUCTURE_COMPLETE.md`
- **Quick Reference**: `docs/TELEMETRY_QUICK_REFERENCE.md`
- **Unit Conversions**: `src/iRacingOverlay.Core/Telemetry/UnitConversions.cs`
- **Calculations**: `src/iRacingOverlay.Core/Telemetry/TelemetryCalculations.cs`

---

**Last Updated**: October 18, 2025  
**SDK Version**: SVappsLAB.iRacingTelemetrySDK v1.0.0-beta.1  
**iRacing SDK**: irsdk_1_19  
**Total Telemetry Variables**: 120+ (from 324 available in SDK)
