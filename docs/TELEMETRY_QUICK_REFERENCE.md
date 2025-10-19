# Telemetry Quick Reference Card

## 🚀 Common Tasks

### Get Speed in User's Units
```csharp
using iRacingOverlay.Core.Telemetry;

bool useMetric = AppSettings.Instance.UseMetric;
float speed = telemetryData.Speed;  // Always m/s from SDK

string display = useMetric 
    ? $"{UnitConversions.MpsToKmh(speed):F0} km/h"
    : $"{UnitConversions.MpsToMph(speed):F0} mph";
```

### Check Fuel Status
```csharp
var status = TelemetryCalculations.GetFuelStatus(telemetryData.FuelLevel);
// Returns: Normal | Low (<10L) | Critical (<5L)
```

### Calculate Average Tire Wear
```csharp
float avgWear = TelemetryCalculations.AverageTireWear(
    telemetryData.LFwearL, 
    telemetryData.LFwearM, 
    telemetryData.LFwearR
);
float lifeRemaining = TelemetryCalculations.TireLifeRemaining(avgWear);
```

### Format Lap Time
```csharp
string formatted = TelemetryCalculations.FormatLapTime(123.456f);
// Result: "2:03.456"
```

### Detect Wheel Lock (Indirect)
```csharp
float frontPress = TelemetryCalculations.AverageFrontBrakePressure(
    telemetryData.LFbrakeLinePress, 
    telemetryData.RFbrakeLinePress
);

bool wheelLock = TelemetryCalculations.PossibleWheelLock(
    telemetryData.Brake,
    telemetryData.BrakeABSactive,
    telemetryData.Speed,
    frontPress
);
```

---

## 📋 Variable Cheat Sheet

### Always Available
- `Speed` (m/s), `RPM`, `Gear`, `Lap`, `LapDistPct`
- `Throttle`, `Brake`, `Clutch` (0-1)
- `FuelLevel` (L), `WaterTemp`, `OilTemp` (°C)
- Tire temps: `LFtempCL/CM/CR`, `RFtemp...`, `LRtemp...`, `RRtemp...`
- Tire wear: `LFwearL/M/R`, `RFwear...`, `LRwear...`, `RRwear...`

### Car-Specific
- `dcTractionControl` (GT3, LMP2, F1 only)
- `BrakeABSactive` (GT3, LMP2, F1 only)
- `CarIdxP2P_Status` (IndyCar only)

### NOT Available
- ❌ `LFspeed`, `RFspeed`, `LRspeed`, `RRspeed` (wheel speeds)
- ❌ `dcTCactive` (TC activation state)

---

## 🔧 Common Conversions

| From | To | Method |
|------|----|----|------|
| m/s | km/h | `UnitConversions.MpsToKmh(value)` |
| m/s | mph | `UnitConversions.MpsToMph(value)` |
| °C | °F | `UnitConversions.CelsiusToFahrenheit(value)` |
| Liters | Gallons | `UnitConversions.LitersToGallons(value)` |
| Radians | Degrees | `UnitConversions.RadiansToDegrees(value)` |
| m/s² | Gs | `UnitConversions.MpsSquaredToGs(value)` |
| Bar | PSI | `UnitConversions.BarToPsi(value)` |

---

## ⚠️ Important Notes

1. **Wheel speeds DO NOT EXIST** in current SDK builds
2. **TC activation cannot be detected** (no dcTCactive variable)
3. **Use indirect methods** for wheel lock detection
4. **All temperatures from SDK are Celsius**
5. **All speeds from SDK are m/s**
6. **All distances from SDK are meters**
7. **Fuel is always in liters** (not kg or gallons)

---

## 📚 Full Documentation

- **Complete Variable List**: `docs/TELEMETRY_VARIABLE_REGISTRY.md`
- **Implementation Guide**: `docs/TELEMETRY_INFRASTRUCTURE_COMPLETE.md`
- **Unit Conversions**: `src/iRacingOverlay.Core/Telemetry/UnitConversions.cs`
- **Calculations**: `src/iRacingOverlay.Core/Telemetry/TelemetryCalculations.cs`
