# Wheel Lockup Detection - Deep Research & New Approach

**Date**: January 18, 2025  
**Status**: 🔬 **RESEARCHING ALTERNATIVE DETECTION METHODS**  
**Priority**: **HIGH - Non-ABS cars need lockup detection**

---

## Console Output Fixed! ✅

Added `AllocConsole()` in `App.xaml.cs` - you'll now see a console window with diagnostic output!

```csharp
[DllImport("kernel32.dll")]
static extern bool AllocConsole();

// In OnStartup():
AllocConsole();
Console.WriteLine("=== iRacing Overlay Debug Console ===");
```

**Now you'll see all `[RAW PRESSURES]` and `[LOCKUP]` messages!** 🎉

---

## Critical Finding: Wheel Speeds DON'T Exist

### Searched ALL SDK Variables

**From live iRacing session** (324 total variables):
- ✅ `LFodometer` - Distance traveled (not speed!)
- ✅ `RFodometer`, `LRodometer`, `RRodometer`
- ❌ `LFspeed` - **DOES NOT EXIST**
- ❌ `RFspeed`, `LRspeed`, `RRspeed` - **NONE EXIST**

**The reference documentation mentioning wheel speeds is WRONG or OUTDATED.**

### What We DO Have

```csharp
// Per-Wheel Data Available:
"LFbrakeLinePress"     // Brake line pressure (bar)
"LFodometer"           // Distance traveled since mount (m)
"LFshockDefl"          // Shock deflection (m)
"LFshockVel"           // Shock velocity (m/s)
"LFtempCL/CM/CR"       // Tire temps (°C)
"LFwearL/M/R"          // Tire wear (%)
"LFcoldPressure"       // Cold tire pressure (kPa)

// Overall Vehicle:
"Speed"                // Vehicle speed (m/s)
"LongAccel"            // Longitudinal accel (m/s²)
"LatAccel"             // Lateral accel (m/s²)
"BrakeABSactive"       // ABS flag (bool)
```

---

## Why Brake Pressure Detection Failed

### Hypothesis Confirmed

From your testing: **"Barely any activation besides when with ABS on"**

**Brake line pressures represent hydraulic system pressure, NOT wheel friction/contact pressure.**

When you lock a wheel:
1. ✅ Hydraulic pressure stays constant (pedal held)
2. ❌ Pressure reading doesn't change
3. ❌ No pressure drop to detect

**Our pressure-based detection CAN'T WORK without wheel speeds!**

---

## NEW Approach: Shock Deflection Analysis

### Theory

When a wheel locks under braking:

1. **Normal Braking** (wheel rolling):
   - Weight transfers forward
   - Front shocks compress
   - Rear shocks extend
   - Smooth, predictable deflection

2. **Locked Wheel** (wheel skidding):
   - Sudden loss of tire grip
   - Rapid change in shock deflection
   - **High-frequency oscillation** in shock velocity
   - **Asymmetric deflection** between left/right

### Available Variables

```csharp
"LFshockDefl"     // Shock deflection (meters)
"LFshockVel"      // Shock velocity (m/s)
"LFshockDefl_ST"  // Short-term deflection (?)
"LFshockVel_ST"   // Short-term velocity (?)
```

### Detection Strategy

#### Method 1: Shock Velocity Spikes

```csharp
// Track shock velocity changes during braking
float lfVelDelta = currentLFshockVel - previousLFshockVel;
float rfVelDelta = currentRFshockVel - previousRFshockVel;

// Locked wheel causes SUDDEN shock velocity changes
if (Math.Abs(lfVelDelta) > ShockVelocitySpike Threshold && braking)
{
    // Possible LF lockup
}
```

**Logic**: Locked wheel loses grip → sudden change in suspension loading → shock velocity spike

#### Method 2: Asymmetric Deflection

```csharp
// Compare left vs right shock deflection
float frontDeflDiff = Math.Abs(LFshockDefl - RFshockDefl);

// Normal: Left/right similar deflection
// Locked: One side deflects differently
if (frontDeflDiff > AsymmetricThreshold && braking)
{
    // One front wheel locked
    if (LFshockDefl < RFshockDefl)
        leftFrontLocked = true;
    else
        rightFrontLocked = true;
}
```

**Logic**: Locked wheel skids → different tire grip → asymmetric weight distribution → different shock deflection

#### Method 3: Longitudinal Accel + Shock Pattern

```csharp
// Combine deceleration with shock behavior
bool hardBraking = data.Brake > 0.5f && data.LongAccel < -5.0f;
bool shockOscillation = DetectShockOscillation(shockVelHistory);

if (hardBraking && shockOscillation)
{
    // Inefficient braking with suspension oscillation = lockup
}
```

**Logic**: Locked wheels cause inefficient braking + suspension instability

---

## Alternative: Odometer-Based Detection

### Concept

Calculate **estimated wheel speed** from odometer delta:

```csharp
// Track odometer changes over time
float lfDistance = currentLFodometer - previousLFodometer;
float timeDelta = 1.0f / 60.0f; // 60Hz updates

float estimatedLFspeed = lfDistance / timeDelta;

// Compare to vehicle speed
float slipRatio = (data.Speed - estimatedLFspeed) / data.Speed;

if (slipRatio > 0.15f) // 15% slip
{
    leftFrontLocked = true;
}
```

**Caveat**: Odometer might not update during wheel lock (wheel not rotating), making this unreliable.

---

## Implementation Plan

### Phase 1: Add Shock Telemetry ✅ TODO

1. Add to `TelemetryData.cs`:
   ```csharp
   public float LFshockDefl { get; set; }
   public float LFshockVel { get; set; }
   public float RFshockDefl { get; set; }
   public float RFshockVel { get; set; }
   public float LRshockDefl { get; set; }
   public float LRshockVel { get; set; }
   public float RRshockDefl { get; set; }
   public float RRshockVel { get; set; }
   ```

2. Add to `RequiredTelemetryVars` in `IRacingTelemetryService.cs`

3. Map in `TelemetryService` extraction

### Phase 2: Test Shock Data ✅ TODO

Run diagnostics to see shock behavior during lockup:

```csharp
if (data.Brake > 0.5f)
{
    Console.WriteLine($"[SHOCK] LF_Defl:{data.LFshockDefl:F4} LF_Vel:{data.LFshockVel:F4}");
    Console.WriteLine($"[SHOCK] RF_Defl:{data.RFshockDefl:F4} RF_Vel:{data.RFshockVel:F4}");
}
```

### Phase 3: Implement Shock-Based Detection ✅ TODO

Create `DetectViaShockAnalysis()` method in `WheelLockupDetector.cs`

### Phase 4: Combine Methods ✅ TODO

```csharp
public static WheelLockupState DetectLockup(TelemetryData data)
{
    // METHOD 1: ABS (highest confidence)
    if (data.BrakeABSactive)
        return DetectViaABS(data);
    
    // METHOD 2: Shock analysis (experimental)
    var shockResult = DetectViaShockAnalysis(data);
    if (shockResult.AnyWheelLocked)
        return shockResult;
    
    // METHOD 3: Deceleration plateau (fallback)
    return DetectViaDeceleration(data);
}
```

---

## Research Questions

### Question 1: Do Shock Values Change During Lockup?

**Test**: Hard brake with wheels locked, watch console for:
- `LFshockDefl` and `RFshockDefl` values
- `LFshockVel` and `RFshockVel` values
- Do they differ left vs right?
- Do they spike or oscillate?

### Question 2: What's the Baseline Behavior?

**Test**: Normal hard braking (no lockup), record:
- Typical shock deflection range
- Typical shock velocity range
- Left/right symmetry tolerance

### Question 3: Can We Distinguish Lockup from Bumps?

**Challenge**: Track bumps also cause shock oscillation

**Solution**: Combine with brake input + speed:
- Only check during braking
- Ignore below minimum speed
- Look for **sustained** oscillation (not single spike)

---

## Expected Outcomes

### Best Case: Shock Analysis Works ✅

- Detect lockup via shock velocity spikes
- Asymmetric deflection indicates which wheel
- Works for non-ABS cars
- **Feature fully functional!**

### Moderate Case: Partial Detection

- Works for severe lockups
- Misses slight wheel slip
- High false positive rate on bumps
- **Limited but useful**

### Worst Case: No Correlation

- Shock data doesn't reflect lockup
- Must use ABS-only detection
- **Document limitation, disable for non-ABS**

---

## Immediate Next Steps

1. ✅ **Run overlay with console** - see diagnostic output
2. ✅ **Test in iRacing** - lock wheels with ABS OFF
3. ✅ **Share console output** - especially `[RAW PRESSURES]` lines
4. ❌ **Add shock telemetry** - if pressure detection fails
5. ❌ **Test shock-based detection** - experimental approach

---

## Summary

**Console Output**: ✅ FIXED (you'll see debug window now)  
**Pressure Detection**: ❌ FAILED (pressures don't change during lockup)  
**Wheel Speeds**: ❌ DON'T EXIST in current SDK  
**Next Approach**: 🔬 SHOCK ANALYSIS (experimental)

**The console window will now show you exactly what's happening during braking!**

**Please run the overlay again and share the console output from brake tests!** 🚀

---

## Technical Notes

### Why Pressure Detection Seemed Promising

In real hydraulic brake systems with ABS:
- Wheel locks → Friction drops → Back-pressure builds
- ABS modulates to release pressure
- Pressure sensors **might** show this

But in iRacing:
- SDK likely reports master cylinder pressure (input)
- Not actual wheel-end pressure (output/friction)
- Lockup doesn't affect the reading

### Why Shock Analysis Might Work

Physics-based reasoning:
1. Locked wheel skids (kinetic friction < static friction)
2. Lower friction → Less braking force on that wheel
3. Less force → Different weight transfer
4. Different transfer → Asymmetric suspension loading
5. Asymmetric loading → Detectable in shock data

**This is indirect but might be measurable!**
