# Wheel Lockup Granular Detection - Complete Reference

**Date**: October 18, 2025  
**Status**: ✅ COMPLETE - Individual, Axle, Side, and Combined Detection

---

## Overview

The WheelLockup detection system provides **granular, multi-level** lockup information:

1. **Individual Wheel Level** - LF, RF, LR, RR (4 wheels independently)
2. **Axle Level** - Front vs Rear
3. **Side Level** - Left vs Right
4. **Combined Level** - Any wheel, all wheels, specific patterns

This allows widgets to display lockup information at any desired level of detail.

---

## Detection Levels

### 1. Individual Wheel Detection ⭐

**Properties**:
```csharp
bool LeftFrontLocked   // LF wheel locked
bool RightFrontLocked  // RF wheel locked
bool LeftRearLocked    // LR wheel locked
bool RightRearLocked   // RR wheel locked
```

**Usage**:
```csharp
var state = WheelLockupDetector.DetectLockup(data);

if (state.RightFrontLocked)
    Console.WriteLine("Right front wheel is locked!");

if (state.LeftRearLocked)
    Console.WriteLine("Left rear wheel is locked!");
```

**Example Scenarios**:
- Single wheel lockup (e.g., RF only)
- Two wheels on same side (e.g., RF + RR)
- Diagonal lockup (e.g., LF + RR)
- Any combination of 1-4 wheels

---

### 2. Axle Level Detection

**Properties**:
```csharp
bool FrontAxleLockup   // At least one front wheel locked (LF or RF)
bool RearAxleLockup    // At least one rear wheel locked (LR or RR)
```

**Helper Properties**:
```csharp
bool OnlyFrontLocked   // Front locked, rear not locked
bool OnlyRearLocked    // Rear locked, front not locked
bool BothAxlesLocked   // Both front AND rear have lockup
bool BothFrontWheelsLocked  // LF AND RF both locked
bool BothRearWheelsLocked   // LR AND RR both locked
```

**Usage**:
```csharp
if (state.OnlyFrontLocked)
    Console.WriteLine("Front brakes locking, adjust brake bias rearward");

if (state.OnlyRearLocked)
    Console.WriteLine("Rear brakes locking, adjust brake bias forward");

if (state.BothAxlesLocked)
    Console.WriteLine("All wheels locking, threshold braking!");
```

---

### 3. Side Level Detection ⭐ NEW

**Properties**:
```csharp
bool LeftSideLockup    // At least one left wheel locked (LF or LR)
bool RightSideLockup   // At least one right wheel locked (RF or RR)
```

**Usage**:
```csharp
if (state.RightSideLockup && !state.LeftSideLockup)
    Console.WriteLine("Right side locking - check suspension or uneven braking!");

if (state.LeftSideLockup && state.RightSideLockup)
    Console.WriteLine("Both sides locking - normal hard braking");
```

---

### 4. Combined/Overall Detection

**Properties**:
```csharp
bool AnyWheelLocked    // At least one wheel locked (primary indicator)
bool AllWheelsLocked   // All 4 wheels locked simultaneously
int  LockedWheelCount  // Count of locked wheels (0-4)
```

**Usage**:
```csharp
// Simple indicator (what MRT One Widget uses)
if (state.AnyWheelLocked)
{
    ShowRedWarning("WHEEL\nLOCKUP");
}

// Detailed analysis
Console.WriteLine($"{state.LockedWheelCount} wheels locked");

if (state.AllWheelsLocked)
    Console.WriteLine("FULL LOCKUP - All 4 wheels!");
```

---

## Real-World Usage Examples

### Example 1: MRT One Widget (Current Implementation)

```csharp
// Simple binary indicator
var lockupState = WheelLockupDetector.DetectLockup(data);
int displayValue = lockupState.AnyWheelLocked ? 1 : 0;

// Display
if (displayValue == 1)
{
    valueText.Text = "WHEEL\nLOCKUP";
    valueText.Foreground = Colors.Red;      // Bright red
    valueText.Opacity = 1.0;                // Full brightness
}
else
{
    valueText.Text = "WHEEL\nLOCKUP";
    valueText.Foreground = _primaryColor;   // Teal
    valueText.Opacity = 0.3;                // Dimmed
}
```

---

### Example 2: Detailed Widget (Individual Wheel Indicators)

```csharp
var state = WheelLockupDetector.DetectLockup(data);

// Update individual wheel indicators
lfIndicator.Fill = state.LeftFrontLocked ? Brushes.Red : Brushes.Green;
rfIndicator.Fill = state.RightFrontLocked ? Brushes.Red : Brushes.Green;
lrIndicator.Fill = state.LeftRearLocked ? Brushes.Red : Brushes.Green;
rrIndicator.Fill = state.RightRearLocked ? Brushes.Red : Brushes.Green;

// Display count
countText.Text = $"{state.LockedWheelCount}/4 Locked";
```

---

### Example 3: Brake Bias Advisor

```csharp
var state = WheelLockupDetector.DetectLockup(data);

if (state.OnlyFrontLocked)
{
    advice.Text = "Front locking first → Increase rear brake bias";
    advice.Foreground = Brushes.Yellow;
}
else if (state.OnlyRearLocked)
{
    advice.Text = "Rear locking first → Increase front brake bias";
    advice.Foreground = Brushes.Orange;
}
else if (state.BothAxlesLocked)
{
    advice.Text = "Balanced lockup → Brake bias OK";
    advice.Foreground = Brushes.Green;
}
```

---

### Example 4: Suspension/Alignment Diagnosis

```csharp
var state = WheelLockupDetector.DetectLockup(data);

if (state.LeftSideLockup && !state.RightSideLockup)
{
    warning.Text = "⚠ Left side locking asymmetrically - check suspension/alignment";
}
else if (state.RightSideLockup && !state.LeftSideLockup)
{
    warning.Text = "⚠ Right side locking asymmetrically - check suspension/alignment";
}
else if (state.LeftFrontLocked && !state.RightFrontLocked)
{
    warning.Text = "⚠ LF wheel locking alone - check brake caliper/pad";
}
```

---

## Helper Properties Summary

| Property | Description | Use Case |
|----------|-------------|----------|
| `LockedWheelCount` | Number of locked wheels (0-4) | Severity indicator |
| `LockedWheelsList` | "LF, RR" formatted string | Debug display |
| `StatusMessage` | "Locked: LF, RR" or "No Lockup" | Simple status display |
| `LeftSideLockup` | LF or LR locked | Asymmetry detection |
| `RightSideLockup` | RF or RR locked | Asymmetry detection |
| `BothFrontWheelsLocked` | LF AND RF locked | Front axle analysis |
| `BothRearWheelsLocked` | LR AND RR locked | Rear axle analysis |
| `AllWheelsLocked` | All 4 wheels locked | Maximum lockup |
| `OnlyFrontLocked` | Front yes, rear no | Brake bias tuning |
| `OnlyRearLocked` | Rear yes, front no | Brake bias tuning |
| `BothAxlesLocked` | Front AND rear locked | Both axles analysis |

---

## Diagnostic Output

Enable diagnostics to see real-time detection data:

```csharp
WheelLockupDetector.EnableDiagnostics = true;
```

**Console Output Example**:
```
[IMBALANCE] Brake:0.52 AvgAll:28.3 AvgF:34.1 AvgR:22.5
[IMBALANCE] LF:60.2 RF:8.0 LR:44.7 RR:0.3
[IMBALANCE] Front drops: LF=-26.1 RF=26.1 (threshold=3.0)
[LOCKUP] RF LOCKED! Drop:26.1 bar
[IMBALANCE] Rear drops: LR=-22.2 RR=22.2 (threshold=3.0)
[LOCKUP] RR LOCKED! Drop:22.2 bar
```

**Interpretation**:
- Brake input: 52%
- Front average: 34.1 bar
- RF pressure: 8.0 bar (26.1 bar below average) → **LOCKED**
- RR pressure: 0.3 bar (22.2 bar below average) → **LOCKED**
- Result: Right side lockup detected

---

## ToString() Debug Output

The state object has a comprehensive ToString() implementation:

```csharp
var state = WheelLockupDetector.DetectLockup(data);
Console.WriteLine(state.ToString());
```

**Output Examples**:

```
No Lockup
```

```
Wheels: RF, RR (2/4) | Both Sides | Method: BrakePressure | Confidence: Medium
```

```
Wheels: LF, RF, LR, RR (4/4) | Both Axles | Both Sides | Method: BrakePressure | Confidence: High
```

```
Wheels: RR (1/4) | Rear Axle Only | Right Side Only | Method: BrakePressure | Confidence: Medium
```

---

## Detection Method Priority

The detector checks methods in this order (first match wins):

1. **ABS Detection** (Highest Confidence)
   - If `BrakeABSactive == true`
   - Marks all wheels as "lock attempted"
   - Returns immediately

2. **Pressure Collapse** (High Confidence)
   - Checks if average pressure too low for brake input
   - Adaptive to car type (GT3, Formula, Road)
   - Marks all wheels locked if detected

3. **Pressure Imbalance** (Medium Confidence) ⭐ **Primary for Asymmetric**
   - Checks each wheel individually
   - Detects when wheel pressure drops below axle average
   - **This catches right-side-only lockups!**

4. **Deceleration Plateau** (Low Confidence)
   - Fallback method
   - General lockup indication only
   - No individual wheel detail

---

## Widget Integration Examples

### Simple Binary Indicator
```csharp
TelemetryField.WheelLock => WheelLockupDetector.DetectLockup(data).AnyWheelLocked ? 1 : 0
```

### Lockup Count Display
```csharp
TelemetryField.WheelLockCount => WheelLockupDetector.DetectLockup(data).LockedWheelCount
```

### Individual Wheel Status
```csharp
var state = WheelLockupDetector.DetectLockup(data);
DisplayWheel("LF", state.LeftFrontLocked);
DisplayWheel("RF", state.RightFrontLocked);
DisplayWheel("LR", state.LeftRearLocked);
DisplayWheel("RR", state.RightRearLocked);
```

### Axle Comparison
```csharp
var state = WheelLockupDetector.DetectLockup(data);
frontAxleIndicator.Fill = state.FrontAxleLockup ? Brushes.Red : Brushes.Green;
rearAxleIndicator.Fill = state.RearAxleLockup ? Brushes.Red : Brushes.Green;
```

---

## Summary of Capabilities

✅ **Individual Wheels**: LF, RF, LR, RR independently tracked  
✅ **Axle Level**: Front vs Rear lockup detection  
✅ **Side Level**: Left vs Right lockup detection  
✅ **Combined**: Any, all, count, patterns  
✅ **Helpers**: 15+ convenience properties for common scenarios  
✅ **Diagnostics**: Real-time console logging for debugging  
✅ **Debug String**: Comprehensive ToString() output  
✅ **Confidence**: Detection method and confidence level tracking  

The system provides **complete granularity** from individual wheel level all the way up to overall lockup state, with convenient helper properties for every common use case! 🎉
