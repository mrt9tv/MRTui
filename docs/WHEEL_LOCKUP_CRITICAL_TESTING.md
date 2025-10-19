# Wheel Lockup Detection - CRITICAL TESTING REQUIRED

**Date**: January 18, 2025  
**Status**: 🔴 **UNCERTAIN IF DETECTION IS POSSIBLE**  
**Priority**: **CRITICAL - NEED EMPIRICAL DATA**

---

## ⚠️ CRITICAL ISSUE: Detection Still Not Working

User reports: **Wheel lock does not trigger during any speed or braking, only very inconsistently.**

## 🔬 Hypothesis: Brake Line Pressures May Not Change

**Core Problem**: We're assuming brake line pressures DROP when wheels lock, but this might not be true!

### What Brake Line Pressure Might Represent

**Option 1** (Our Assumption):
- Brake line pressure = effective braking force at wheel
- When wheel locks → pressure drops (lost traction)
- **This is what we're detecting**

**Option 2** (Possible Reality):
- Brake line pressure = hydraulic system pressure
- Pressure stays constant when brake pedal held
- Wheel locks but pressure doesn't change
- **Our detection WON'T WORK**

### Why This Matters

Without **individual wheel speeds**, we **cannot directly measure wheel lockup** (slip ratio).

Slip ratio formula: `(vehicle_speed - wheel_speed) / vehicle_speed`
- 0% = no slip, wheel rolling normally
- 100% = wheel completely locked

**iRacing SDK DOES NOT provide wheel speeds!**

---

## 🎯 Testing Configuration

I've made the detector **EXTREMELY SENSITIVE** for testing:

### New Threshold Values

```csharp
MinBrakeInput = 0.15f           // 15% brake (was 30%)
MinSpeed = 5.0f                 // 5 m/s = 18 km/h = 11 mph (was 10 m/s)
PressureDropThreshold = 1.5f    // 1.5 bar drop (was 3.0 bar)
PressureCollapseRatio = 0.75f   // 75% threshold (was 60%)
EnableDiagnostics = TRUE        // Permanently enabled
```

### Enhanced Diagnostics

Every detection attempt now logs:

1. **Raw Pressures**: `LF`, `RF`, `LR`, `RR` in bars
2. **Brake Input**: Current brake pedal position (0-1)
3. **Speed**: Current vehicle speed (m/s)
4. **LongAccel**: Longitudinal acceleration (negative = braking)
5. **ABS Status**: Whether ABS is active
6. **Early Exits**: When thresholds prevent detection
7. **Pressure Analysis**: Averages, drops, ratios

---

## 📋 Testing Protocol

### Step 1: Run the Overlay

```powershell
dotnet run --project f:\VSCode\Programming\MRTui\src\iRacingOverlay.WPF
```

### Step 2: Configure MRT One Widget

1. Enable **"WheelLock"** in left or right side box
2. Should show **"WHEEL LOCKUP"** label (teal at 0.3 opacity when OK)

### Step 3: Enter iRacing

**Car**: GT3 car with ABS OFF  
**Track**: Any track  
**Session**: Practice or test session

### Step 4: Perform Brake Tests

**Test A: Light Braking (15-30%)**
- Speed: 50+ km/h
- Brake: Gently (15-30% pedal)
- **Expected**: Diagnostic output, but no lockup

**Test B: Medium Braking (40-60%)**
- Speed: 80+ km/h
- Brake: Medium (40-60% pedal)
- **Expected**: Diagnostic output, possibly lockup detection

**Test C: Hard Braking (70-100%)**
- Speed: 120+ km/h
- Brake: Full/hard braking
- **Expected**: Strong diagnostic output, lockup detection if pressures change

**Test D: Intentional Lockup**
- Speed: 100+ km/h
- Brake: **HARD STOMP** on brakes (no ABS)
- **Observe**: Tire smoke, skid sounds, loss of steering
- **Expected**: Lockup detection if pressures change

**Test E: ABS ON (Control Test)**
- Same car with ABS enabled
- Hard braking
- **Expected**: `BrakeABSactive` detection (should work)

---

## 📊 What to Look For in Console Output

### Example Output (Successful Detection)

```
[DETECT START] Brake:0.85 Speed:45.2m/s ABS:False
[RAW PRESSURES] LF:68.23 RF:12.45 LR:54.11 RR:55.88
[RAW ACCEL] LongAccel:-9.23 (deceleration, should be negative when braking)

[COLLAPSE] AvgP:47.67 ExpP:66.3 Ratio:0.72 (thresh:0.75) Scale:78.0 Calibrated:True

[IMBALANCE] Brake:0.85 AvgAll:47.67 AvgF:40.34 AvgR:55.00
[IMBALANCE] LF:68.2 RF:12.5 LR:54.1 RR:55.9

[IMBALANCE] Front drops: LF=-27.9 RF=27.9 (threshold=1.5)
[LOCKUP] RF LOCKED! Drop:27.9 bar
```

**Interpretation**: Right Front wheel pressure is 27.9 bar BELOW average → **LOCKED**

---

### Example Output (No Detection - Pressures Don't Change)

```
[DETECT START] Brake:0.95 Speed:42.0m/s ABS:False
[RAW PRESSURES] LF:72.50 RF:72.30 LR:71.80 RR:72.00
[RAW ACCEL] LongAccel:-8.50

[IMBALANCE] Brake:0.95 AvgAll:72.15 AvgF:72.40 AvgR:71.90
[IMBALANCE] LF:72.5 RF:72.3 LR:71.8 RR:72.0

[IMBALANCE] Front drops: LF=-0.1 RF=0.1 (threshold=1.5)
```

**Interpretation**: All pressures uniform (~72 bar) despite wheel lockup → **DETECTION IMPOSSIBLE**

---

## 🔍 Critical Questions to Answer

### Question 1: Do Pressures Change At All?

When you lock wheels:
- Do you see pressure **variations** between wheels?
- Do any wheels show **pressure drops** > 1.5 bar?
- Or do all pressures stay **uniform**?

### Question 2: How Much Do They Change?

If pressures do change:
- What's the typical drop? (1 bar? 5 bar? 20 bar?)
- Is it consistent across multiple lockups?
- Does it vary by car/setup/brake bias?

### Question 3: Is Timing a Factor?

- Do pressures change **instantly** when wheels lock?
- Or is there a **delay** (100ms? 500ms?)
- Does pressure recover when wheel regains grip?

### Question 4: What About Different Scenarios?

**Single Wheel Lockup** (e.g., right front only):
- Does RF pressure drop while others stay high?

**Both Front Wheels Locked**:
- Do LF + RF pressures drop while LR + RR stay high?

**All Four Wheels Locked**:
- Do all pressures drop uniformly?
- Or does one axle drop more than the other?

---

## 🚨 Possible Outcomes

### Outcome A: Pressures DO Change (Detection Works)

**Evidence**:
- Console shows pressure drops > 1.5 bar during lockup
- Drops correlate with wheel lockup events
- Different wheels show different pressures

**Action**: Fine-tune thresholds based on actual values

---

### Outcome B: Pressures DON'T Change (Detection Impossible)

**Evidence**:
- Console shows uniform pressures (~70-80 bar) regardless of lockup
- Pressure variations < 1 bar even during obvious lockup
- No correlation between lockup and pressure changes

**Implications**:
- **Brake line pressure = hydraulic system pressure** (not wheel pressure)
- **Detection without wheel speeds is IMPOSSIBLE**
- Must use **ABS detection only** (BrakeABSactive flag)

**Alternative Approach**:
```csharp
// Simple fallback: Only detect via ABS flag
TelemetryField.WheelLock => data.BrakeABSactive ? 1 : 0
```

---

### Outcome C: Pressures Change But Inconsistently

**Evidence**:
- Some lockups show pressure drops
- Others don't
- Depends on car/setup/brake bias

**Action**: Document which cars work, which don't

---

## 📝 What to Share

Please copy/paste the **console output** from multiple brake tests, including:

1. **Light braking** (no lockup expected)
2. **Hard braking with ABS OFF** (intentional lockup)
3. **Hard braking with ABS ON** (control test)

Include observations:
- Did you see/hear wheel lockup? (tire smoke, skid sound)
- Did the detector trigger?
- What were the actual pressure values?
- Any patterns you notice?

---

## 🎓 Background: Why This Is Hard

### Ideal Detection (If We Had Wheel Speeds)

```csharp
float slipRatio = (vehicleSpeed - wheelSpeed) / vehicleSpeed;
if (slipRatio > 0.2f) // 20% slip = locked
{
    wheelLocked = true;
}
```

**Simple, reliable, accurate.**

### Our Approach (Without Wheel Speeds)

We're trying to **infer** wheel lockup from:
- Brake line pressures (might not change)
- Longitudinal acceleration (affected by many factors)
- ABS flag (only works with ABS ON)

**This is essentially "educated guessing" without the core data we need.**

---

## ✅ Summary

**Current State**:
- Diagnostics: ✅ ENABLED
- Thresholds: ✅ EXTREMELY SENSITIVE
- Raw data logging: ✅ COMPREHENSIVE
- Build: ✅ SUCCESS

**Required**:
- 🔴 **Empirical testing data**
- 🔴 **Console output from actual lockups**
- 🔴 **Confirmation if pressures change at all**

**This will determine if pressure-based detection is even possible!**

---

## 🎯 Next Steps

1. **Run the overlay** with diagnostics enabled
2. **Test in iRacing** with intentional wheel lockups
3. **Copy console output** and share it
4. **We'll analyze together** if detection is feasible

If pressures don't change, we'll switch to **ABS-only detection** or explore alternative approaches.

**This is the critical test that determines if the feature is possible! 🚀**
