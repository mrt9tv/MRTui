# Wheel Lockup Detection - Emergency Diagnostic Update

**Date**: January 18, 2025  
**Status**: 🔴 **EMERGENCY DIAGNOSTICS ENABLED**  
**Issue**: Detection not working consistently - need empirical data

---

## Changes Made

### 1. ✅ Diagnostics Permanently Enabled

```csharp
public static bool EnableDiagnostics { get; set; } = true; // ENABLED FOR DEBUGGING
```

**Before**: `false` (disabled by default)  
**After**: `true` (always on)

---

### 2. ✅ Enhanced Raw Data Logging

Added comprehensive logging at detection start:

```csharp
Console.WriteLine($"[RAW PRESSURES] LF:{data.LFbrakeLinePress:F2} RF:{data.RFbrakeLinePress:F2} LR:{data.LRbrakeLinePress:F2} RR:{data.RRbrakeLinePress:F2}");
Console.WriteLine($"[RAW ACCEL] LongAccel:{data.LongAccel:F2} (deceleration, should be negative when braking)");
```

**Shows**: Exact brake pressure values for all 4 wheels + longitudinal acceleration

---

### 3. ✅ Dramatically Reduced Thresholds

Made detector **EXTREMELY SENSITIVE** for testing:

| Threshold | Old Value | New Value | Change |
|-----------|-----------|-----------|--------|
| **MinBrakeInput** | 0.30 (30%) | **0.15 (15%)** | ⬇️ 50% reduction |
| **MinSpeed** | 10.0 m/s (36 km/h) | **5.0 m/s (18 km/h)** | ⬇️ 50% reduction |
| **PressureDropThreshold** | 3.0 bar | **1.5 bar** | ⬇️ 50% reduction |
| **PressureCollapseRatio** | 0.60 (60%) | **0.75 (75%)** | ⬆️ 25% increase |

**Why**: Catch even slight pressure changes to see if detection is possible

---

## What This Means

### Detection Will Now Trigger On:

- **15% brake input** (instead of 30%)
- **18 km/h speed** (instead of 36 km/h)
- **1.5 bar pressure drop** (instead of 3.0 bar)
- **Any pressure drop below 75%** of expected (instead of 60%)

### Console Output Will Show:

**Every brake event >15% will log**:
1. Brake input value
2. Current speed
3. ABS status
4. **ALL 4 wheel pressures** (LF, RF, LR, RR)
5. Longitudinal acceleration
6. Pressure averages
7. Pressure drops per wheel
8. Lockup detections

---

## Critical Test Required

### What We Need to Determine:

**Do brake line pressures actually change when wheels lock?**

### Scenario A: Pressures DO Change ✅

```
[RAW PRESSURES] LF:65.20 RF:12.30 LR:58.40 RR:59.10
                          ^^^^^^ 
                          RF is WAY lower - wheel locked!
```

**Result**: Detection works, just needs threshold tuning

---

### Scenario B: Pressures DON'T Change ❌

```
[RAW PRESSURES] LF:72.50 RF:72.30 LR:71.80 RR:72.00
                ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                All ~72 bar - uniform despite lockup!
```

**Result**: Detection impossible without wheel speeds

**Reason**: Brake line pressure = hydraulic system pressure (constant when pedal held), not actual wheel friction/contact pressure

---

## Testing Instructions

### Step 1: Run Overlay

```powershell
dotnet run --project f:\VSCode\Programming\MRTui\src\iRacingOverlay.WPF
```

### Step 2: Enter iRacing

- **Car**: GT3 with ABS OFF
- **Track**: Any track
- **Session**: Practice

### Step 3: Brake Tests

1. **Light brake** (20-30%) at 50 km/h
2. **Medium brake** (50%) at 80 km/h
3. **Hard brake** (80%+) at 120 km/h
4. **Intentional lockup** (stomp brakes, no ABS)

### Step 4: Observe Console

**Look for**:
- `[RAW PRESSURES]` lines
- Do pressures vary between wheels during lockup?
- Or are they always uniform?

### Step 5: Share Results

Copy/paste console output showing:
- Brake tests with lockup
- Actual pressure values
- Whether detection triggered

---

## Why This Matters

### Without Wheel Speeds:

We **cannot directly measure** wheel lockup (slip ratio).

**Slip ratio**: `(vehicle_speed - wheel_speed) / vehicle_speed`
- iRacing SDK **does not provide** individual wheel speeds
- We're trying to **infer** lockup from brake pressures

### If Pressures Don't Change:

**Brake line pressure might represent**:
- Hydraulic system pressure (input from master cylinder)
- NOT actual pressure at wheel/caliper/pad contact

**When wheel locks**:
- Hydraulic pressure stays constant (pedal still pressed)
- Wheel speed drops to zero
- But pressure reading doesn't change

**In this case**: Detection without wheel speeds is **impossible**

---

## Possible Outcomes

### 1️⃣ Pressures Change (Detection Works)

- Fine-tune thresholds
- Document car-specific behaviors
- Enable feature

### 2️⃣ Pressures Don't Change (Detection Impossible)

**Option A**: ABS-only detection
```csharp
TelemetryField.WheelLock => data.BrakeABSactive ? 1 : 0
```

**Option B**: Remove feature
- Document limitation
- Explain why it's not possible
- Wait for iRacing to add wheel speeds

### 3️⃣ Pressures Change Sometimes

- Document which cars/setups work
- Make feature optional
- Warn about inconsistency

---

## Summary

**Current Status**:
- ✅ Build successful (0 errors)
- ✅ Diagnostics enabled permanently
- ✅ Enhanced raw data logging
- ✅ Thresholds reduced for maximum sensitivity
- ✅ Testing documentation created

**Required**:
- 🔴 **Empirical testing in iRacing**
- 🔴 **Console output from actual lockups**
- 🔴 **Confirmation if brake pressures change**

**This test determines if the feature is technically possible!**

---

## Files Modified

- `src/iRacingOverlay.WPF/Utils/WheelLockupDetector.cs`:
  - Line 17: `EnableDiagnostics = true`
  - Line 37: `MinSpeed = 5.0f`
  - Line 43: `MinBrakeInput = 0.15f`
  - Line 29: `PressureDropThreshold = 1.5f`
  - Line 57: `PressureCollapseRatio = 0.75f`
  - Lines 109-111: Added raw pressure/accel logging

## Files Created

- `docs/WHEEL_LOCKUP_CRITICAL_TESTING.md`: Comprehensive testing guide
- `docs/WHEEL_LOCKUP_EMERGENCY_DIAGNOSTIC_UPDATE.md`: This summary

---

**Ready for critical testing! 🚀**
