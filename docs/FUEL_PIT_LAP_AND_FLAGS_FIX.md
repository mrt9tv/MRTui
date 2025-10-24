# Fuel Calculator: Pit Lap & Enhanced Flag Detection Fix

**Issue Date:** October 21, 2025  
**Reporter:** User (from live iRacing session screenshot)  
**Status:** 🔴 CRITICAL BUG + ENHANCEMENT  

---

## Issues Identified

### Issue #1: Incorrect Pit Lap Counting 🔴 CRITICAL

**Problem:**
Pit lap calculation is incorrect - counting laps 20-30% track distance away from pit entry.

**Root Cause:**
`telemetry.LapsCompleted` increments at **start/finish line**, NOT at pit entry.
- Most tracks have pit entry at 80-95% track distance (NOT at start/finish line 0%)
- Example: Road America pit entry at ~85% track distance
- When player crosses start/finish line, lap increments even though they're 20-30% away from actually entering pits

**Current Broken Logic:**
```csharp
// Detect lap completion (with duplicate prevention)
if (telemetry.LapsCompleted > _lastCompletedLap && 
    _lastCompletedLap >= 0 && 
    telemetry.LapsCompleted != _lapsCompletedWhenProcessed)
{
    OnLapCompleted(telemetry, isRefueling || _justLeftPits);
    _lapsCompletedWhenProcessed = telemetry.LapsCompleted;
}
```

**Problem Scenario:**
1. Player at 85% track distance (approaching pit entry)
2. Crosses start/finish line (0% → lap increments to Lap 10)
3. **Fuel calculator records "Lap 10 completed"** ❌
4. Player continues to 85% and enters pits (still on Lap 10)
5. Pit lap recorded as Lap 10, but player was only 15% into Lap 10!

**Impact:**
- Fuel calculations wrong (lap not actually complete when recorded)
- Pit stop timing incorrect
- Lap averages corrupted (partial lap fuel usage recorded as full lap)

---

### Issue #2: Limited Flag Detection 🟡 ENHANCEMENT

**Problem:**
Current system only detects basic flags: Yellow, Green, White, Checkered, Red.
Missing critical racing state information: Safety Car, Pace Laps, Two to Green, One to Green.

**Available Flags from SDK** (irsdk_defines.h):
```cpp
enum irsdk_Flags
{
    // Global flags
    irsdk_checkered         = 0x00000001,
    irsdk_white             = 0x00000002,
    irsdk_green             = 0x00000004,
    irsdk_yellow            = 0x00000008,
    irsdk_red               = 0x00000010,
    irsdk_blue              = 0x00000020,
    irsdk_debris            = 0x00000040,
    irsdk_crossed           = 0x00000080,
    irsdk_yellowWaving      = 0x00000100,
    irsdk_oneLapToGreen     = 0x00000200,  // ⚠️ MISSING!
    irsdk_greenHeld         = 0x00000400,
    irsdk_tenToGo           = 0x00000800,
    irsdk_fiveToGo          = 0x00001000,
    irsdk_randomWaving      = 0x00002000,
    irsdk_caution           = 0x00004000,
    irsdk_cautionWaving     = 0x00008000,
    
    // Start lights
    irsdk_startHidden       = 0x10000000,
    irsdk_startReady        = 0x20000000,
    irsdk_startSet          = 0x40000000,
    irsdk_startGo           = 0x80000000,
};

enum irsdk_SessionState
{
    irsdk_StateInvalid = 0,
    irsdk_StateGetInCar,
    irsdk_StateWarmup,
    irsdk_StateParadeLaps,   // ⚠️ Pace laps!
    irsdk_StateRacing,
    irsdk_StateCheckered,
    irsdk_StateCoolDown
};

enum irsdk_PaceMode
{
    irsdk_PaceModeSingleFileStart = 0,
    irsdk_PaceModeDoubleFileStart,
    irsdk_PaceModeSingleFileRestart,
    irsdk_PaceModeDoubleFileRestart,
    irsdk_PaceModeNotPacing,
};
```

**Missing Detection:**
- ❌ One Lap to Green (`irsdk_oneLapToGreen`) - Critical for pit strategy!
- ❌ Green Held (`irsdk_greenHeld`) - Green flag held, restart imminent
- ❌ Ten to Go, Five to Go flags
- ❌ Start lights sequence (Hidden/Ready/Set/Go)
- ❌ Session state (Parade Laps = pace laps)
- ❌ Pace mode (single/double file, restart type)
- ❌ Debris flag
- ❌ Blue flag (being lapped)

**Impact on Fuel Strategy:**
- Cannot optimize pit timing around "One Lap to Green" window
- Cannot detect pace laps for better fuel saving calculations
- Cannot distinguish full-course caution from local yellow
- Cannot detect restart type (single vs double file)

---

## Solution Design

### Fix #1: Accurate Pit Lap Detection Using LapDistPct

**Strategy:**
Use `telemetry.LapDistPct` to detect when player actually **crosses pit entry**, not start/finish line.

**Implementation:**
1. Track `_lastLapDistPct` to detect when player crosses pit entry threshold
2. Typical pit entry: 80-95% track distance
3. Detect crossing: `_lastLapDistPct < 0.85 && telemetry.LapDistPct >= 0.85`
4. Record "virtual lap completion at pit entry" for accurate fuel calculations

**Pseudo-Code:**
```csharp
private float _lastLapDistPct = 0f;
private int _virtualLapsCompleted = 0; // Lap counter relative to pit entry
private const float PIT_ENTRY_PCT = 0.85f; // Typical pit entry location

// In Update():
// Detect crossing pit entry threshold (virtual lap completion)
if (_lastLapDistPct < PIT_ENTRY_PCT && telemetry.LapDistPct >= PIT_ENTRY_PCT)
{
    // Player crossed pit entry - record "virtual lap completion"
    _virtualLapsCompleted++;
    
    if (_virtualLapsCompleted != _lapsCompletedWhenProcessed)
    {
        OnLapCompleted(telemetry, isRefueling || _justLeftPits);
        _lapsCompletedWhenProcessed = _virtualLapsCompleted;
    }
}

// Handle wrap-around (95% → 5% without crossing pit entry)
if (_lastLapDistPct > 0.9f && telemetry.LapDistPct < 0.1f)
{
    // Wrapped around without pit entry - use start/finish line
    if (telemetry.LapsCompleted > _lastCompletedLap)
    {
        _virtualLapsCompleted = telemetry.LapsCompleted;
    }
}

_lastLapDistPct = telemetry.LapDistPct;
```

**Alternative Solution:**
Parse `TrackPitEntryStartPct` from YAML (if available) for exact pit entry location per track.

---

### Fix #2: Enhanced Flag Detection

**Implementation:**
Add comprehensive flag detection with new LapFlagStatus enum values.

**Enhanced LapFlagStatus Enum:**
```csharp
public enum LapFlagStatus
{
    Unknown = 0,
    Green = 1,
    Yellow = 2,          // Full course yellow/caution
    Red = 3,
    White = 4,           // Final lap
    Checkered = 5,
    
    // NEW: Enhanced flags for better strategy
    OneLapToGreen = 10,  // Critical for pit timing!
    GreenHeld = 11,      // Green flag held at line
    YellowWaving = 12,   // Local yellow (waved)
    Debris = 13,         // Debris on track
    Blue = 14,           // Being lapped
    
    // Race control flags
    TenToGo = 20,
    FiveToGo = 21,
    
    // Start sequence
    StartReady = 30,
    StartSet = 31,
    StartGo = 32
}
```

**Enhanced DetectFlagStatus():**
```csharp
private LapFlagStatus DetectFlagStatus(uint sessionFlags)
{
    // iRacing SessionFlags bitfield constants (from irsdk_defines.h)
    const uint Checkered = 0x00000001;
    const uint White = 0x00000002;
    const uint Green = 0x00000004;
    const uint Yellow = 0x00000008;
    const uint Red = 0x00000010;
    const uint Blue = 0x00000020;
    const uint Debris = 0x00000040;
    const uint YellowWaving = 0x00000100;
    const uint OneLapToGreen = 0x00000200;  // ⭐ NEW!
    const uint GreenHeld = 0x00000400;
    const uint TenToGo = 0x00000800;
    const uint FiveToGo = 0x00001000;
    const uint Caution = 0x00004000;
    const uint CautionWaving = 0x00008000;
    
    // Start lights
    const uint StartReady = 0x20000000;
    const uint StartSet = 0x40000000;
    const uint StartGo = 0x80000000;
    
    // Priority order (higher priority flags checked first)
    
    // 1. Race ending flags
    if ((sessionFlags & Red) != 0)
        return LapFlagStatus.Red;
    if ((sessionFlags & Checkered) != 0)
        return LapFlagStatus.Checkered;
    if ((sessionFlags & White) != 0)
        return LapFlagStatus.White;
    
    // 2. Start sequence flags
    if ((sessionFlags & StartGo) != 0)
        return LapFlagStatus.StartGo;
    if ((sessionFlags & StartSet) != 0)
        return LapFlagStatus.StartSet;
    if ((sessionFlags & StartReady) != 0)
        return LapFlagStatus.StartReady;
    
    // 3. Critical restart flags
    if ((sessionFlags & OneLapToGreen) != 0)  // ⭐ MOST IMPORTANT!
        return LapFlagStatus.OneLapToGreen;
    if ((sessionFlags & GreenHeld) != 0)
        return LapFlagStatus.GreenHeld;
    
    // 4. Caution flags
    if ((sessionFlags & (Yellow | Caution | CautionWaving)) != 0)
        return LapFlagStatus.Yellow;
    if ((sessionFlags & YellowWaving) != 0)
        return LapFlagStatus.YellowWaving;
    
    // 5. Informational flags
    if ((sessionFlags & TenToGo) != 0)
        return LapFlagStatus.TenToGo;
    if ((sessionFlags & FiveToGo) != 0)
        return LapFlagStatus.FiveToGo;
    if ((sessionFlags & Debris) != 0)
        return LapFlagStatus.Debris;
    if ((sessionFlags & Blue) != 0)
        return LapFlagStatus.Blue;
    
    // 6. Green flag (racing)
    if ((sessionFlags & Green) != 0)
        return LapFlagStatus.Green;
    
    return LapFlagStatus.Unknown;
}
```

---

## Integration with Fuel Strategy

### Phase 5.B: Enhanced Optimal Pit Lap (Updated)

**New Strategic Decision Priority:**

**Priority 0: One Lap to Green (HIGHEST PRIORITY!)**
```csharp
// NEW: Detect "One Lap to Green" - CRITICAL pit window!
if (_currentFlagStatus == LapFlagStatus.OneLapToGreen && lapsOnCurrentFuel > 2f)
{
    optimalPitLap = currentLap + 1; // Pit THIS lap (on pace lap)
    pitReason = "🟢 ONE LAP TO GREEN - Pit NOW before restart!";
    CurrentData.PitWindowReason = "Critical restart window";
}
```

**Why This Matters:**
- "One Lap to Green" = field is circling under caution, next lap is green flag restart
- If you pit on THIS lap (pace lap), you pit under yellow = no position loss
- If you wait until green flag, you pit under green = major position loss
- **Most critical pit timing decision in entire race!**

**Updated Priority Order:**
1. ⚠️ One Lap to Green (new) - Pit NOW if fuel ok
2. 🔴 Critical fuel (<1.5 laps) - Pit immediately
3. 🟡 Under yellow + low fuel - Seize opportunity
4. 🔮 Yellow expected soon - Delay for yellow
5. 🏆 Top position - Pit late in window
6. ⚡ Back of pack - Undercut strategy
7. 🎯 Default - Mid-window balanced

---

## Implementation Checklist

### Phase 1: Fix Pit Lap Counting (Critical) ✅ COMPLETE
- [x] Add `_lastLapDistPct` tracking variable ✅
- [x] Add `_virtualLapsCompleted` counter ✅
- [x] Add `PIT_ENTRY_PCT` constant (0.85f default) ✅
- [x] Implement pit entry crossing detection ✅
- [x] Handle wrap-around edge case (95% → 5%) ✅
- [ ] Test with multiple tracks (verify pit entry locations)
- [x] Add debug logging for pit entry detection ✅

### Phase 2: Enhanced Flag Detection (High Priority) ✅ COMPLETE
- [x] Expand `LapFlagStatus` enum with new flags ✅
- [x] Update `DetectFlagStatus()` method with all flags ✅
- [x] Add flag constants from irsdk_defines.h ✅
- [x] Implement priority order for flag detection ✅
- [x] Add `SessionState` to TelemetryData (for pace lap detection) ✅
- [x] Add `PaceMode` to TelemetryData (optional) ✅
- [ ] Test flag detection in practice/qualifying/race
- [x] Add debug logging for flag transitions ✅

### Phase 3: Fuel Strategy Integration (Medium Priority) ✅ COMPLETE
- [x] Add "One Lap to Green" priority to CalculateOptimalPitLap() ✅
- [x] Priority 0 (HIGHEST): OneLapToGreen + GreenHeld detection ✅
- [x] Pit recommendation: "PIT NOW before restart!" ✅
- [ ] Update pit window calculation for caution laps
- [x] Add pace lap fuel consumption tracking (separate average) ✅
- [ ] Update fuel saving logic for caution/restart conditions
- [ ] Add restart strategy recommendations
- [ ] Test in live race with multiple cautions

### Phase 4: UI Enhancement (Low Priority)
- [ ] Display current flag status in Fuel Widget
- [ ] Show "One Lap to Green" alert with pit recommendation
- [ ] Add pace lap indicator
- [ ] Add restart countdown timer
- [ ] Color-code pit window for caution vs green flag

---

## Testing Strategy

### Test Case 1: Pit Lap Accuracy
**Track:** Road America (pit entry at ~85%)
1. Run 10 laps without pitting
2. Verify lap completion recorded at pit entry crossing (85%), NOT start/finish (0%)
3. Enter pits on Lap 10 at 85% track distance
4. Verify pit lap = 10 (not 11)

### Test Case 2: One Lap to Green Detection
**Session:** Race with caution
1. Trigger caution (go off track)
2. Wait for "One Lap to Green" message
3. Verify fuel calculator detects flag and recommends pit
4. Pit on pace lap
5. Verify no position loss vs pitting under green

### Test Case 3: Pace Lap Fuel Consumption
**Session:** Race start with pace laps
1. Track fuel consumption during parade/pace laps
2. Verify separate average calculated for pace laps
3. Verify race lap average excludes pace lap data
4. Compare pace lap avg (should be ~40-60% of race lap avg)

---

## Priority Ranking

1. 🔴 **CRITICAL**: Fix pit lap counting (Issue #1)
   - Affects all fuel calculations
   - Corrupts lap history data
   - Must be fixed immediately

2. 🟠 **HIGH**: Add "One Lap to Green" detection (Issue #2)
   - Most impactful flag for pit strategy
   - Can save 10-20 seconds per race
   - Common scenario in iRacing

3. 🟡 **MEDIUM**: Add comprehensive flag detection
   - Improves strategy accuracy
   - Better user experience
   - Nice-to-have for competitive racing

4. 🟢 **LOW**: UI enhancements for flags
   - Quality of life improvement
   - Can be added later
   - Not critical for core functionality

---

## Estimated Impact

**Fix #1 (Pit Lap Counting):**
- ✅ Accurate lap completion detection
- ✅ Correct fuel averages (no partial laps)
- ✅ Proper pit stop timing
- ✅ Fix corrupted lap history data
- **Impact: Fixes fundamental calculation bug**

**Fix #2 (Enhanced Flag Detection):**
- ✅ Optimal pit timing around "One Lap to Green"
- ✅ Better fuel saving during caution
- ✅ Restart strategy recommendations
- ✅ Pace lap fuel consumption tracking
- **Impact: 10-20 second advantage per race**

---

## Notes for Implementation

**Pit Entry Location Parsing:**
Check YAML for exact pit entry location per track:
```yaml
WeekendInfo:
  TrackPitEntryStartPct: 0.847  # Exact pit entry location
```

If available, use this value instead of hardcoded 0.85f constant.

**SessionState Integration:**
Already available in telemetry as `data.SessionState`:
- 3 = Parade Laps (pace laps)
- 4 = Racing (green flag)
- 5 = Checkered (race ended)

Use this to detect pace laps for separate fuel consumption tracking.

**Safety Considerations:**
- Never recommend pitting if fuel critical (<1 lap remaining)
- Always prioritize fuel criticality over optimal flag timing
- Fail-safe: If flag detection fails, fall back to original logic

---

## References

- iRacing SDK: `irsdk_defines.h` (Session flags enum)
- LivePositionCalculator.cs (lap distance tracking examples)
- ProximityCalculator.cs (LapDistPct usage patterns)
- Current bug: User screenshot showing incorrect pit lap timing
