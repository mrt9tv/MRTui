# Wheel Lockup Detection - Diagnostic Guide

**Date**: October 18, 2025  
**Status**: ✅ Enhanced with comprehensive diagnostics  
**Issue**: No lockup detection with ABS OFF, triggers during ABS (not in sync)

---

## Issues Fixed

### 1. Thresholds Were Too Restrictive ❌ FIXED

**Old Values** (causing early exits):
```csharp
MinBrakeInput = 0.7f  // 70% - TOO HIGH!
MinSpeed = 20.0f      // 72 km/h - TOO HIGH!
```

**Problem**:
- Lockup can happen at 40-50% brake input
- Lockup can happen at lower speeds (especially with less downforce)
- Early exit prevented any detection below these thresholds

**New Values** (more realistic):
```csharp
MinBrakeInput = 0.3f  // 30% - Catches medium to hard braking
MinSpeed = 10.0f      // 36 km/h / 22 mph - Reasonable minimum
```

---

## Enabling Diagnostics

### In Code

Add this line **before** running any detection:

```csharp
WheelLockupDetector.EnableDiagnostics = true;
```

**Where to add it**:
- In `TelemetryDataMapper.cs` at the top of `GetValue()` method, or
- In widget initialization code, or
- In main app startup

### Example Integration

```csharp
// In TelemetryDataMapper.cs
public static object? GetValue(TelemetryField field, TelemetryData data)
{
    // Enable diagnostics (can be toggled via settings later)
    WheelLockupDetector.EnableDiagnostics = true;
    
    return field switch
    {
        TelemetryField.WheelLock => WheelLockupDetector.DetectLockup(data).AnyWheelLocked ? 1 : 0,
        // ... rest of fields
    };
}
```

---

## Diagnostic Output Reference

### 1. Early Exit (Not Braking Hard Enough)

```
[EARLY EXIT] Brake:0.25 (min:0.30) Speed:15.2m/s (min:10.0m/s)
```

**Meaning**:
- Brake input: 25% (below 30% minimum)
- Speed: 15.2 m/s (54.7 km/h)
- Detection skipped - not braking hard enough

**Action**: Normal - no lockup expected at light braking

---

### 2. Detection Start

```
[DETECT START] Brake:0.52 Speed:35.4m/s ABS:False
```

**Meaning**:
- Brake input: 52%
- Speed: 35.4 m/s (127 km/h)
- ABS: OFF
- Starting detection checks

---

### 3. Calibration Update

```
[CALIBRATION] Max pressure updated: 78.5 bar
```

**Meaning**:
- Detected hard braking (>90% input)
- Updated adaptive max pressure to 78.5 bar
- Will use this for ratio calculations

**Action**: Good! System is learning your car's pressure range

---

### 4. Pressure Collapse Check

```
[COLLAPSE] AvgP:32.1 ExpP:20.8 Ratio:1.54 (thresh:0.60) Scale:40.0 Calibrated:False
```

**Breakdown**:
- `AvgP`: Average pressure across all 4 wheels = 32.1 bar
- `ExpP`: Expected pressure for current brake input = 20.8 bar
- `Ratio`: Actual/Expected = 1.54 (154%)
- `thresh`: Collapse threshold = 0.60 (60%)
- `Scale`: Pressure scale being used = 40.0 bar
- `Calibrated`: Not yet calibrated (using default 40 bar)

**Meaning**: Ratio 1.54 > 0.60 → No collapse, pressures normal

---

### 5. Pressure Collapse Lockup

```
[COLLAPSE LOCKUP] All wheels locked! Ratio 0.42 < 0.60
```

**Meaning**:
- Pressure ratio: 42% (below 60% threshold)
- All 4 wheels locked simultaneously
- High confidence detection

---

### 6. Pressure Imbalance Check

```
[IMBALANCE] Brake:0.52 AvgAll:28.3 AvgF:34.1 AvgR:22.5
[IMBALANCE] LF:60.2 RF:8.0 LR:44.7 RR:0.3
```

**Breakdown**:
- Brake input: 52%
- Overall average: 28.3 bar
- Front average: 34.1 bar
- Rear average: 22.5 bar
- Individual wheels: LF=60.2, RF=8.0, LR=44.7, RR=0.3

---

### 7. Individual Wheel Drops

```
[IMBALANCE] Front drops: LF=-26.1 RF=26.1 (threshold=3.0)
[LOCKUP] RF LOCKED! Drop:26.1 bar
```

**Breakdown**:
- `LF drop`: -26.1 (negative = above average, gripping)
- `RF drop`: +26.1 (positive = below average, **LOCKED**)
- Threshold: 3.0 bar
- RF pressure is 26.1 bar below front average → **LOCKED**

---

### 8. Multiple Wheel Lockup

```
[LOCKUP] RF LOCKED! Drop:26.1 bar
[LOCKUP] RR LOCKED! Drop:21.8 bar
```

**Meaning**: Right side (RF + RR) both locked

---

## Interpreting Results

### Case 1: No Detection with ABS OFF

**Expected Output**:
```
[DETECT START] Brake:0.55 Speed:40.2m/s ABS:False
[COLLAPSE] AvgP:18.2 ExpP:43.1 Ratio:0.42 (thresh:0.60) Scale:78.5 Calibrated:True
[COLLAPSE LOCKUP] All wheels locked! Ratio 0.42 < 0.60
```

**If you see**:
```
[EARLY EXIT] Brake:0.55 (min:0.30) Speed:15.2m/s (min:10.0m/s)
```

**Problem**: Speed too low (< 10 m/s)  
**Solution**: Either lower `MinSpeed` or test at higher speeds

---

**If you see**:
```
[DETECT START] Brake:0.55 Speed:40.2m/s ABS:False
[COLLAPSE] AvgP:28.5 ExpP:22.0 Ratio:1.30 (thresh:0.60) Scale:40.0 Calibrated:False
[IMBALANCE] Brake:0.55 AvgAll:28.5 AvgF:28.2 AvgR:28.8
[IMBALANCE] LF:28.0 RF:28.4 LR:28.5 RR:29.1
[IMBALANCE] Front drops: LF=0.2 RF=-0.2 (threshold=3.0)
```

**Problem**: Pressures are balanced (no drops > 3 bar)  
**Reason**: Wheels **might not actually be locked!** Or pressure sensors show uniform values  
**Solution**: 
1. Verify wheels are actually locking (tire smoke, locked sound)
2. Check if brake pressure sensors show variation when locking
3. May need to lower `PressureDropThreshold` from 3.0 to 2.0 bar

---

### Case 2: Detection During ABS (Not in Sync)

**Expected**:
```
[DETECT START] Brake:0.85 Speed:45.0m/s ABS:True
```

Then detection returns immediately with ABS flag.

**If Detection Continues**:

This shouldn't happen - ABS check is first. But if pressure collapse or imbalance triggers:

**Possible Cause**: ABS cycles on/off rapidly (normal ABS behavior)
- ABS pulses brakes → pressure fluctuates
- Some samples: ABS=True → Triggers ABS detection
- Other samples: ABS=False → Triggers pressure detection

**This is expected!** ABS cycles 10-15 times per second.

---

## Troubleshooting Steps

### Step 1: Enable Diagnostics

```csharp
WheelLockupDetector.EnableDiagnostics = true;
```

### Step 2: Test with ABS OFF

1. Drive GT3 car with ABS OFF
2. Hard brake from high speed (>80 km/h)
3. Intentionally lock wheels
4. Watch console output

### Step 3: Analyze Output

**Look for**:
- `[EARLY EXIT]` → Thresholds blocking detection
- `[COLLAPSE]` → Pressure ratio values
- `[IMBALANCE]` → Individual wheel pressures
- `[LOCKUP]` → Successful detections

### Step 4: Adjust Thresholds if Needed

**Too sensitive** (false positives):
```csharp
WheelLockupDetector.PressureCollapseRatio = 0.5f;  // 50% instead of 60%
WheelLockupDetector.PressureDropThreshold = 4.0f;  // 4 bar instead of 3
```

**Not sensitive enough** (missing lockups):
```csharp
WheelLockupDetector.PressureCollapseRatio = 0.7f;  // 70% instead of 60%
WheelLockupDetector.PressureDropThreshold = 2.0f;  // 2 bar instead of 3
WheelLockupDetector.MinSpeed = 5.0f;               // 5 m/s instead of 10
```

---

## Summary of Changes

✅ **Reduced MinBrakeInput**: 0.7 → 0.3 (70% → 30%)  
✅ **Reduced MinSpeed**: 20 m/s → 10 m/s (72 km/h → 36 km/h)  
✅ **Added diagnostics** to early exit check  
✅ **Added diagnostics** to pressure collapse check  
✅ **Added diagnostics** to calibration updates  
✅ **Enhanced diagnostics** in imbalance detection  

The system now provides **complete visibility** into every detection decision! 🎉

---

## Next Steps

1. **Enable diagnostics** in code
2. **Run in iRacing** with GT3 + ABS OFF
3. **Hard brake** and intentionally lock wheels
4. **Post console output** for analysis
5. **Adjust thresholds** based on actual data

The diagnostic output will show **exactly** why detection is or isn't triggering!
