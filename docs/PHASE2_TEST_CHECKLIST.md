# Quick Verification Test - Phase 2 Fuel Calculator

## ✅ Pre-Flight Checklist

Run this checklist before starting iRacing:

### 1. Build Status
```powershell
dotnet build src/iRacingOverlay.sln
```
Expected: ✅ Build succeeded with 0 errors

### 2. Code Verification

**FuelCalculatorService Integration**:
- ✅ `_fuelCalculatorService` field added to IRacingTelemetryService
- ✅ Service initialized in constructor
- ✅ `Update(data)` called in OnTelemetryUpdate
- ✅ `CurrentFuelData` property exposed via ITelemetryService interface

**MRT One Widget Integration**:
- ✅ `_fuelDisplay` TextBlock field added
- ✅ Display created in constructor (positioned at bottom, 8px margin)
- ✅ `UpdateFuelDisplay()` called in UpdateUI method
- ✅ Method accesses `_telemetryService.CurrentFuelData`

**Data Flow Path**:
```
iRacing SDK
    ↓
IRacingTelemetryService.OnTelemetryUpdate()
    ↓ (line ~658)
_fuelCalculatorService.Update(data)
    ↓
FuelData.CurrentData updated
    ↓ (exposed via)
ITelemetryService.CurrentFuelData property
    ↓ (accessed by)
MRTOneWidget.UpdateFuelDisplay()
    ↓
_fuelDisplay.Text = formatted string
```

### 3. Expected Data Values (After 5 Laps)

**FuelData Properties Displayed**:
- `CurrentFuel`: Real-time fuel level (e.g., 45.2 liters)
- `AvgFuelPerLap_L5`: 5-lap rolling average (e.g., 3.2 L/lap)
- `LapsRemaining`: Calculated value (e.g., 14.1 laps)

**Format String**:
```csharp
$"{fuelData.CurrentFuel:F1}L | Avg: {fuelData.AvgFuelPerLap_L5:F1}L | Laps: {fuelData.LapsRemaining:F1}"
```

Example output: `"45.2L | Avg: 3.2L | Laps: 14.1"`

---

## 🧪 In-Game Test Procedure

### Lap 0 (Formation/Warmup)
- [ ] Fuel display is **HIDDEN** (no valid data yet)
- [ ] Widget shows normal gauge elements (RPM, gear, etc.)

### Lap 1 (First Completion)
- [ ] Cross start/finish line
- [ ] Fuel display **APPEARS** at bottom of gauge
- [ ] Shows current fuel value
- [ ] Avg shows first lap's consumption
- [ ] Laps remaining calculated

### Laps 2-4 (Building Average)
- [ ] Avg value updates each lap
- [ ] Average of 2, then 3, then 4 laps shown
- [ ] Laps remaining recalculates

### Lap 5+ (Stabilized L5 Average)
- [ ] Rolling 5-lap average now active
- [ ] Oldest lap drops from calculation
- [ ] Average stabilizes (less volatile)

### Fuel Consumption Test
Drive 10 laps and record data:

| Lap | Fuel After | Fuel Used | L5 Avg | Laps Rem | Color |
|-----|-----------|-----------|--------|----------|-------|
| 1   | _____L    | _____L    | ___L   | ___      | ___   |
| 2   | _____L    | _____L    | ___L   | ___      | ___   |
| 3   | _____L    | _____L    | ___L   | ___      | ___   |
| 4   | _____L    | _____L    | ___L   | ___      | ___   |
| 5   | _____L    | _____L    | ___L   | ___      | ___   |
| 6   | _____L    | _____L    | ___L   | ___      | ___   |
| 7   | _____L    | _____L    | ___L   | ___      | ___   |
| 8   | _____L    | _____L    | ___L   | ___      | ___   |
| 9   | _____L    | _____L    | ___L   | ___      | ___   |
| 10  | _____L    | _____L    | ___L   | ___      | ___   |

**Manual Verification** (Lap 10):
- Calculate: Fuel After Lap 10 ÷ L5 Avg = Expected Laps
- Compare with displayed "Laps Rem" value
- Difference should be <0.5 laps

### Color Coding Test
- [ ] **Green** when laps remaining >5
- [ ] **Yellow** when laps remaining 3-5
- [ ] **Red** when laps remaining <3

### Edge Case Tests
- [ ] **Pit Stop**: Display hides during refuel, reappears after
- [ ] **Yellow Flag**: Fuel still tracked, consumption may differ
- [ ] **Formation Lap**: Excluded from averages (WasPitLap or IsFormationLap)
- [ ] **Session Change**: Data resets properly

---

## 📊 Expected Behavior Reference

### Visibility Logic
```csharp
if (fuelData.CurrentFuel > 0 && fuelData.AvgFuelPerLap_L5 > 0)
    → Display VISIBLE
else
    → Display HIDDEN
```

### Color Logic
```csharp
LapsRemaining > 5  → Color.FromArgb(180, 0, 255, 0)      // Green
LapsRemaining 3-5  → Color.FromArgb(180, 255, 255, 0)    // Yellow
LapsRemaining < 3  → Color.FromArgb(180, 255, 0, 0)      // Red
```

### Averaging Logic (L5)
```csharp
// Laps 1-4: Average of all completed laps
// Lap 5+:   Rolling 5-lap average (last 5 completed)

Example:
Lap 6 avg = Average of laps [2, 3, 4, 5, 6]  // Drops lap 1
Lap 7 avg = Average of laps [3, 4, 5, 6, 7]  // Drops lap 2
```

---

## 🐛 Known Issues to Watch For

### Issue: Fuel Level Never Updates
**Symptom**: Display stuck at 0.0L or initial value
**Cause**: TelemetryData.FuelLevel not being populated
**Check**: Verify car has fuel system (not all cars support it)

### Issue: Average Stuck at 0.0
**Symptom**: "Avg: 0.0L" never changes
**Cause**: Laps not being recorded in history
**Check**: 
1. Verify lap completion detection (OnLapCompleted called)
2. Check `_lapHistory` list has entries
3. Verify laps pass `IsValidForAveraging` check

### Issue: Display Never Appears
**Symptom**: Fuel display always hidden
**Cause**: Visibility condition never met
**Debug**:
```csharp
// Check these conditions:
fuelData.CurrentFuel > 0        // Should be true when car has fuel
fuelData.AvgFuelPerLap_L5 > 0   // Should be true after lap 1
```

### Issue: Incorrect Laps Remaining
**Symptom**: Laps remaining doesn't match manual calculation
**Formula**: `LapsRemaining = CurrentFuel / AvgFuelPerLap_L5`
**Check**: Verify both values are correct individually

---

## ✅ Success Criteria

Phase 2 is **verified successful** when:

1. ✅ Fuel display appears after lap 1
2. ✅ Current fuel decreases in real-time
3. ✅ L5 average stabilizes after 5 laps
4. ✅ Laps remaining = CurrentFuel / Avg (±0.5 laps)
5. ✅ Color changes: Green → Yellow → Red as fuel depletes
6. ✅ Display hides/shows correctly (pit stops, session start)
7. ✅ No exceptions or crashes during 10+ lap test

---

## 📝 Test Results

**Date**: _______________  
**Track**: _______________  
**Car**: _______________  
**Session Type**: _______________

**Overall Result**: ⬜ Pass  ⬜ Fail  ⬜ Partial

**Issues Found**:
- 
- 
- 

**Comments**:


---

**Ready to test!** Start the overlay and get on track! 🏁
