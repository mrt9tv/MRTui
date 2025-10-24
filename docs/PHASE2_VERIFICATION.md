# Phase 2 Fuel Calculator - Verification Guide

**Date**: October 19, 2025  
**Status**: ✅ Ready for Testing  
**Build**: Successful (No compilation errors)

---

## What Was Implemented

### Core Components
1. **FuelData Model** (`FuelData.cs`)
   - 40+ properties covering current state, averages, strategy
   - Averaging methods: Current, Last, Last5 (default), Last10, Session, Max
   - Safety car tracking (green/yellow flag separation)

2. **FuelCalculatorService** (`FuelCalculatorService.cs`)
   - Real-time fuel tracking with 60Hz telemetry updates
   - Lap-by-lap history with validation (filters pit laps, formation laps)
   - Refueling detection (fuel increase >0.5L while on pit road)
   - Strategy calculations (laps remaining, fuel needed, can finish)

3. **IRacingTelemetryService Integration**
   - FuelCalculatorService initialized in constructor
   - `Update()` called on every telemetry tick
   - `CurrentFuelData` property exposed via ITelemetryService interface

4. **MRT One Widget Display**
   - Simple 1-line TextBlock at bottom of circular gauge
   - Format: `"45.2L | Avg: 3.2L | Laps: 14"`
   - Color-coded warnings:
     - **Green**: >5 laps remaining (safe)
     - **Yellow**: 3-5 laps remaining (caution)
     - **Red**: <3 laps remaining (critical pit needed)

---

## How to Test Phase 2

### Step 1: Start the Overlay
```powershell
# From workspace root
dotnet run --project src\iRacingOverlay.WPF
```

Expected result:
- Overlay manager window opens
- "Create Widget" button available

### Step 2: Create MRT One Widget
1. Click "Create Widget" button
2. Select "MRT One" from widget list
3. Widget appears as circular gauge with radar spotter

### Step 3: Connect to iRacing
1. Launch iRacing
2. Join any session (Practice, Qualify, Race)
3. Overlay should auto-connect when entering car

### Step 4: Verify Fuel Display Appears

**Expected Behavior**:
- Fuel display **HIDDEN** initially (no data yet)
- After completing **1st lap**: Fuel display **APPEARS** at bottom of gauge
- Display shows: `"XX.XL | Avg: X.XL | Laps: X.X"`

**What You Should See**:
```
Current Fuel: Updates in real-time (decreases as you drive)
Avg (L5):     Shows "0.0" initially, stabilizes after 5 laps
Laps:         Calculated laps remaining (CurrentFuel / Avg)
```

### Step 5: Verify L5 Averaging Stabilization

| Lap | Expected Avg Behavior |
|-----|----------------------|
| 1   | AvgFuelPerLap_Last (single lap) |
| 2   | Average of 2 laps |
| 3   | Average of 3 laps |
| 4   | Average of 4 laps |
| 5+  | **Rolling 5-lap average** (stabilizes) |

**Example Data Flow**:
```
Lap 1: Used 3.2L → Avg shows 3.2L (only 1 lap)
Lap 2: Used 3.1L → Avg shows 3.15L (avg of 2)
Lap 3: Used 3.3L → Avg shows 3.2L (avg of 3)
Lap 4: Used 3.0L → Avg shows 3.15L (avg of 4)
Lap 5: Used 3.2L → Avg shows 3.16L (avg of 5)
Lap 6: Used 3.1L → Avg shows 3.14L (drops lap 1, uses laps 2-6)
```

### Step 6: Test Color Coding

**Scenario 1: Safe Fuel (Green)**
- Have >15L remaining (~5+ laps)
- Display should be **bright green**

**Scenario 2: Caution (Yellow)**
- Run down to ~10-12L (3-5 laps remaining)
- Display should turn **yellow**

**Scenario 3: Critical (Red)**
- Continue driving to <10L (<3 laps)
- Display should turn **red**
- This is your "PIT NOW" warning

### Step 7: Validate Calculations

**Manual Verification**:
1. Note your fuel level: e.g., `45.2L`
2. Note the L5 average: e.g., `3.2L/lap`
3. Calculate manually: `45.2 / 3.2 = 14.1 laps`
4. Verify display shows: `"45.2L | Avg: 3.2L | Laps: 14.1"`

**Edge Cases to Test**:
- ✅ **Pit stop**: Display should hide during refueling, reappear after
- ✅ **Formation lap**: Should be filtered out (not counted in average)
- ✅ **Incomplete lap** (crash/disconnect): Should be excluded
- ✅ **Yellow flag**: Should still calculate but may show different usage

---

## Troubleshooting

### Issue 1: Fuel Display Never Appears

**Possible Causes**:
- `FuelLevel > 0` check failing → Verify car has fuel
- `AvgFuelPerLap_L5 > 0` check failing → Need at least 1 completed lap

**Debug Steps**:
1. Check telemetry connection: Is RPM/Speed/Gear updating?
2. Complete at least 1 full lap (cross start/finish line)
3. Check `FuelCalculatorService.CurrentData.AvgFuelPerLap_L5` property

### Issue 2: Display Shows "0.0L | Avg: 0.0L | Laps: 0.0"

**Cause**: FuelCalculatorService not receiving updates

**Fix**:
1. Verify `_fuelCalculatorService.Update(data)` is called in `OnTelemetryUpdate`
2. Check that telemetry data has valid fuel values
3. Ensure `CurrentFuelData` property returns non-empty data

### Issue 3: Average Doesn't Stabilize After 5 Laps

**Possible Causes**:
- Pit laps being counted (should be filtered)
- Formation laps being counted (should be filtered)
- Lap validation failing

**Debug Steps**:
1. Check `FuelLapHistory.IsValidForAveraging` property
2. Verify `WasPitLap`, `IsFormationLap`, `IsIncompleteLap` flags
3. Inspect `_lapHistory` list contents

### Issue 4: Color Coding Not Working

**Expected Colors**:
```csharp
LapsRemaining > 5  → Green  (safe)
LapsRemaining 3-5  → Yellow (caution)
LapsRemaining < 3  → Red    (critical)
```

**Verify**:
- Check `fuelData.LapsRemaining` value
- Ensure `Color.FromArgb(180, R, G, B)` is creating semi-transparent color
- Verify `_fuelDisplay.Foreground` is being set

---

## Success Criteria

Phase 2 is **successfully verified** when:

- ✅ Fuel display appears after 1st lap
- ✅ Current fuel updates in real-time (every ~16ms at 60Hz)
- ✅ L5 average stabilizes after 5 laps
- ✅ Laps remaining calculation is accurate
- ✅ Color coding works (green → yellow → red)
- ✅ Display hides/shows correctly (pit stops, session changes)
- ✅ No crashes or exceptions during normal operation

---

## Data Flow Summary

```
iRacing SDK (60Hz)
    ↓
IRacingTelemetryService.OnTelemetryUpdate()
    ↓
TelemetryData object created (FuelLevel, FuelPct, etc.)
    ↓
FuelCalculatorService.Update(telemetry)
    ├─ Detect lap completion
    ├─ Record lap history
    ├─ Calculate averages (L5, L10, Session, etc.)
    ├─ Calculate strategy (laps remaining, fuel needed)
    └─ Update CurrentData property
    ↓
ITelemetryService.CurrentFuelData property
    ↓
MRTOneWidget.UpdateFuelDisplay()
    ├─ Get fuelData from _telemetryService.CurrentFuelData
    ├─ Format display string
    ├─ Apply color coding
    └─ Update _fuelDisplay.Text and Visibility
    ↓
Visual display on screen (bottom of circular gauge)
```

---

## Next Steps After Verification

Once Phase 2 is verified working:

1. **Mark TODO as complete**: Update task #7 in todo list
2. **Document any issues**: Note any bugs or unexpected behavior
3. **Gather feedback**: How does it perform in real racing?
4. **Plan Phase 3**: Standalone fuel widget with advanced features
   - Fuel bar (empty → full)
   - Multiple averaging methods toggle
   - Pit stop predictions
   - Position change estimates
   - Fuel mixture tracking
   - Weather impact analysis

---

## Reference Files

**Core Implementation**:
- `src/iRacingOverlay.Core/Models/FuelData.cs` (184 lines)
- `src/iRacingOverlay.Core/Models/FuelLapHistory.cs` (67 lines)
- `src/iRacingOverlay.Core/Services/FuelCalculatorService.cs` (304 lines)
- `src/iRacingOverlay.Core/Services/ITelemetryService.cs` (interface with CurrentFuelData)
- `src/iRacingOverlay.Core/Services/IRacingTelemetryService.cs` (integration point)

**Widget Display**:
- `src/iRacingOverlay.WPF/Widgets/MRTOneWidget/MRTOneWidget.cs`
  - Line 54: `_fuelDisplay` field declaration
  - Line 368-386: Fuel display creation
  - Line 1106: `UpdateFuelDisplay()` call in `UpdateUI()`
  - Line 1476-1503: `UpdateFuelDisplay()` method implementation

**Documentation**:
- `docs/FuelCalcInteresting.md` (Advanced strategies and future features)
- `docs/PHASE2_VERIFICATION.md` (This file)

---

**Good luck with testing!** 🚀⛽
