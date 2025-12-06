# Fuel Calculator Investigation & Analysis

**Date**: December 6, 2025  
**Status**: 🔍 Investigation Complete  
**Purpose**: Identify broken functionality, unused toggles, and values not working correctly

---

## 🚨 Critical Issues Found

### Issue #1: Dynamic Buffer Configuration Not Used ❌

**Location**: `FuelCalculatorService.cs` lines 80-81, 136-139

**Problem**:
```csharp
// DECLARED BUT NEVER USED:
private float _bufferLaps = 1.0f;  // User-configured base buffer
private bool _enableDynamicBuffer = true;  // Whether dynamic buffer is enabled

public void ConfigureBufferSettings(float bufferLaps, bool enableDynamicBuffer)
{
    _bufferLaps = bufferLaps;
    _enableDynamicBuffer = enableDynamicBuffer;
}
```

**Evidence**:
- Settings exist: `AppSettings.FuelWidget_BufferLaps` and `AppSettings.FuelWidget_EnableDynamicBuffer`
- UI bindings exist: `SettingsViewModel` and `OverlayViewModel` expose these properties
- `FuelWidget` calls `ConfigureBufferSettings()` on line 437
- **BUT**: `_bufferLaps` and `_enableDynamicBuffer` are NEVER REFERENCED in any calculations!

**Impact**: **HIGH** - Users can configure buffer settings, but they do nothing
- Settings UI shows buffer configuration
- Users expect 1.0 lap buffer to be applied
- Dynamic buffer toggle appears functional but is ignored
- All calculations use `DynamicBufferCalculator` with hardcoded `BaseBufferLaps = 0.5f`

**Root Cause**: Phase 6 refactoring extracted `DynamicBufferCalculator` service but didn't wire user settings

**Fix Required**:
```csharp
// In DynamicBufferCalculator.Calculate():
// CHANGE: const float BaseBufferLaps = 0.5f;
// TO: Pass userBufferLaps parameter and use it

// In FuelCalculatorService:
// Pass _bufferLaps to DynamicBufferCalculator
var bufferData = _bufferCalculator.Calculate(
    consistencyFactor,
    currentPosition,
    totalCars,
    isRaining,
    yellowFlagCount,
    currentLap,
    totalLaps,
    isTimedSession,
    _bufferLaps,          // ADD THIS
    _enableDynamicBuffer  // ADD THIS
);
```

---

### Issue #2: Fuel Averaging Method Toggle Not Working ⚠️

**Location**: `FuelWidget.xaml.cs` line 232, `FuelCalculatorService.cs`

**Problem**:
```csharp
// FuelWidget sets selected method on FuelCalculatorService:
var methodName = AppSettings.Instance.FuelWidget_Method;
_fuelCalculator.CurrentData.SelectedMethod = methodName switch
{
    "Current" => FuelAveragingMethod.Current,
    "Last" => FuelAveragingMethod.Last,
    "Last5" => FuelAveragingMethod.Last5,
    // ... etc
};
```

**BUT**: `SelectedMethod` is stored in `FuelData.SelectedMethod` but **NEVER USED** in actual calculations!

**Evidence**:
- `FuelAveragingService` calculates ALL 9 methods every update
- `FuelCalculatorService` uses hardcoded logic to pick which average to use
- No code checks `CurrentData.SelectedMethod` to determine display value
- User can change "Method" dropdown but sees no effect

**Current Behavior**:
```csharp
// FuelCalculatorService line ~950 (CalculateAverages method)
// Always uses this logic, ignoring user setting:
CurrentData.AvgFuelPerLap = greenFlagAverage > 0 ? greenFlagAverage : averages.L5;
```

**Impact**: **MEDIUM-HIGH** - Setting exists in UI but does nothing
- AppSettings has `FuelWidget_Method` property (default: "Session")
- SettingsViewModel exposes it to UI
- Users expect dropdown to change which average is displayed
- Display always shows Green Flag avg or L5, never other methods

**Fix Required**:
```csharp
// In FuelCalculatorService.CalculateAverages():
// Use SelectedMethod to pick which average to display
CurrentData.AvgFuelPerLap = CurrentData.SelectedMethod switch
{
    FuelAveragingMethod.Last => averages.Last,
    FuelAveragingMethod.Last5 => averages.L5,
    FuelAveragingMethod.Last10 => averages.L10,
    FuelAveragingMethod.Session => averages.Session,
    FuelAveragingMethod.Max => averages.Max,
    FuelAveragingMethod.EMA => averages.EMA,
    FuelAveragingMethod.GreenFlagOnly => averages.GreenOnly,
    FuelAveragingMethod.StintAverage => averages.Stint,
    FuelAveragingMethod.Adaptive => averages.Adaptive,
    _ => averages.L5  // Safe fallback
};
```

---

### Issue #3: Obsolete Settings Not Removed ⚠️

**Location**: `AppSettings.cs` lines 71-147

**Problem**: Multiple `[Obsolete]` settings still in codebase:
```csharp
[Obsolete("L10 display has been removed. Use FuelWidget_ShowRange instead.", false)]
public bool FuelWidget_ShowL10 { get; set; } = false;

[Obsolete("SESSION display has been removed. Use FuelWidget_ShowRange instead.", false)]
public bool FuelWidget_ShowSession { get; set; } = false;

[Obsolete("Individual field toggles are no longer used. Use FuelWidget_ShowPitStrategy instead.", false)]
public bool FuelWidget_ShowIRacingDelta { get; set; } = true;
// ... 4 more obsolete properties
```

**Impact**: **LOW** - Technical debt, no functional impact
- Properties kept for "backward compatibility with existing config files"
- Not referenced anywhere in code
- Clutter settings class (552 lines total)
- May confuse developers

**Fix Required**: Remove obsolete properties after sufficient deprecation period (e.g., 1-2 releases)

---

### Issue #4: Fuel Pressure Tracking Not Implemented ❌

**Location**: `FuelCalculatorService.cs` lines 77-79

**Problem**:
```csharp
// Sputtering threshold tracking (Enhanced Phase 2.1)
private bool _baselineFuelPressureEstablished = false;
private readonly List<float> _fuelPressureHistory = new();  // Track pressure for baseline calculation
private const int BASELINE_LAPS_NEEDED = 3;  // Laps needed to establish baseline pressure
```

**BUT**: These variables are NEVER USED anywhere in the service!

**Evidence**:
- `grep` search shows NO references to `_baselineFuelPressureEstablished`
- `_fuelPressureHistory` is never populated
- `BASELINE_LAPS_NEEDED` constant unused
- AppSettings has `FuelWidget_ShowFuelPressure` toggle (default: false)
- UI has PRESS field that can be enabled

**Current Behavior**: PRESS field shows "0.0 psi" always (no data)

**Impact**: **MEDIUM** - Feature advertised but not implemented
- Comment says "Enhanced Phase 2.1" but implementation missing
- Users can enable fuel pressure display but see no real data
- Sputtering detection not working

**Fix Required**: Either implement fuel pressure tracking or remove UI toggle and field

---

### Issue #5: Historical Data Not Applied Correctly ⚠️

**Location**: `FuelCalculatorService.cs` lines 49-50

**Problem**:
```csharp
private readonly History.TelemetryHistoryService? _historyService;
private bool _historicalDataApplied = false;
```

**Evidence**:
- `_historicalDataApplied` flag set once, never reset
- On session change, historical data won't be re-applied
- Flag prevents learning from multiple sessions in same run

**Current Behavior**:
```csharp
// In Update() method:
if (_historyService != null && !_historicalDataApplied)
{
    // Apply historical data ONCE
    _historicalDataApplied = true;  // Never reset!
}
```

**Impact**: **MEDIUM** - Limits usefulness of historical learning
- First session in app run gets historical data
- Subsequent sessions don't benefit from history
- Flag should reset on session restart

**Fix Required**:
```csharp
// In Reset() method, add:
_historicalDataApplied = false;

// Or better: Apply historical data at START of each lap, not just once
```

---

## 🔧 Minor Issues & Code Smells

### Issue #6: Excessive Debug Logging

**Location**: Throughout `FuelCalculatorService.cs`

**Problem**: 50+ debug log statements create 10MB+ log files
- `LogDebug()` called every update (60 Hz)
- File I/O on every telemetry frame
- Logs to `Documents/MRT-UI/fuel_debug.log`

**Impact**: **LOW** - Performance hit during development, disk space usage

**Recommendation**: Add `#if DEBUG` or setting toggle to disable in production

---

### Issue #7: Commented-Out Code Not Removed

**Location**: Lines 60-62

```csharp
// EMA (Exponential Moving Average) tracking - REMOVED: No longer used, replaced by DeltaTrackingService
// private float _emaValue = 0f;  // Current EMA value
// private bool _emaInitialized = false;  // Whether EMA has been initialized with first lap
// REMOVED: Fixed EMA_ALPHA constant - now calculated adaptively based on fuel consistency
```

**Impact**: **NONE** - Just clutter

**Recommendation**: Delete commented code after successful Phase 6 refactoring validation

---

### Issue #8: Magic Numbers Throughout Code

**Examples**:
- `telemetry.LapDistPct > 0.05f` (line 625) - Why 5%?
- `telemetry.LapDistPct > 0.3f && telemetry.LapDistPct < 0.9f` (line 1450) - Why 30-90%?
- `LIVE_UPDATE_THROTTLE = 10` (line 98) - Why 10 frames?

**Impact**: **LOW** - Maintainability issue

**Recommendation**: Extract to named constants with explanatory comments

---

## 📊 Feature Status Matrix

| Feature | Setting Exists | UI Exists | Functional | Notes |
|---------|---------------|-----------|------------|-------|
| **Buffer Laps** | ✅ Yes | ✅ Yes | ❌ **NO** | Configured but ignored |
| **Dynamic Buffer** | ✅ Yes | ✅ Yes | ⚠️ **PARTIAL** | Always on, toggle ignored |
| **Fuel Method** | ✅ Yes | ✅ Yes | ❌ **NO** | Dropdown does nothing |
| **Fuel Pressure** | ✅ Yes | ✅ Yes | ❌ **NO** | Shows 0.0 always |
| **Live Sparkline** | ✅ Yes | ✅ Yes | ✅ Yes | Works correctly |
| **Lap Sparkline** | ✅ Yes | ✅ Yes | ✅ Yes | Works correctly |
| **Strategy Recommendation** | ✅ Yes | ✅ Yes | ✅ Yes | Works correctly |
| **Fuel Saving Badge** | ✅ Yes | ✅ Yes | ✅ Yes | Works correctly |
| **Pit Strategy Section** | ✅ Yes | ✅ Yes | ✅ Yes | Master toggle works |
| **Historical Learning** | ✅ Yes | N/A | ⚠️ **PARTIAL** | Only applies once per app run |

---

## 🎯 Prioritized Fix List

### Priority 1: Critical User-Facing Bugs ⚠️

1. **Fix Buffer Settings Not Working**
   - Modify `DynamicBufferCalculator` to accept user buffer
   - Wire `_bufferLaps` and `_enableDynamicBuffer` into calculations
   - Test: Change buffer from 1.0 to 2.0 laps, verify fuel calculations adjust
   - **Estimated Effort**: 2 hours

2. **Fix Fuel Method Dropdown Not Working**
   - Use `CurrentData.SelectedMethod` in `CalculateAverages()`
   - Apply user-selected averaging method to `AvgFuelPerLap`
   - Test: Switch between Last5, Session, EMA, verify display changes
   - **Estimated Effort**: 1 hour

### Priority 2: Missing Features 🔧

3. **Remove or Implement Fuel Pressure**
   - Option A: Remove UI toggle and PRESS field (1 hour)
   - Option B: Implement fuel pressure tracking from SDK (8 hours)
   - **Recommendation**: Remove (not in iRacing SDK YAML)

4. **Fix Historical Data Application**
   - Reset `_historicalDataApplied` flag on session change
   - Allow historical learning across multiple sessions
   - **Estimated Effort**: 30 minutes

### Priority 3: Code Cleanup 🧹

5. **Remove Obsolete Settings** (after 1-2 release grace period)
6. **Extract Magic Numbers to Constants**
7. **Add Debug Logging Toggle** (#if DEBUG or setting)
8. **Remove Commented-Out Code**

---

## 🔬 Testing Plan

### Test Case 1: Buffer Settings
```
GIVEN: User sets FuelWidget_BufferLaps = 2.0
WHEN: Race session starts
THEN: Fuel calculations use 2.0 lap buffer (not 0.5 default)
VERIFY: FuelNeededToFinish = (RaceLapsRemaining * AvgFuelPerLap) + (2.0 * AvgFuelPerLap)
```

### Test Case 2: Dynamic Buffer Toggle
```
GIVEN: User disables FuelWidget_EnableDynamicBuffer
WHEN: Race conditions change (inconsistent laps, position battles)
THEN: Buffer stays constant at user setting (no dynamic adjustments)
VERIFY: BufferData.TotalBuffer = _bufferLaps (fixed)
```

### Test Case 3: Fuel Method Selection
```
GIVEN: User selects "Last5" from dropdown
WHEN: Lap completes
THEN: Display shows Last5 average (not GreenOnly or L5)
VERIFY: FuelData.AvgFuelPerLap == FuelAverages.L5
```

### Test Case 4: Historical Learning
```
GIVEN: User completes Race 1, then starts Race 2 (same track/car)
WHEN: Race 2 lap 1 starts
THEN: Historical fuel average from Race 1 is applied
VERIFY: CurrentData.HistoricalAverage > 0 on lap 1 of Race 2
```

---

## 📝 Implementation Recommendations

### Fix #1: Buffer Settings Integration

**File**: `src/iRacingOverlay.Core/Services/Fuel/DynamicBufferCalculator.cs`

**Change 1**: Add parameters to `Calculate()`:
```csharp
public BufferData Calculate(
    float consistencyFactor,
    int currentPosition,
    int totalCars,
    bool isRaining,
    int yellowFlagCount,
    int currentLap,
    int totalLaps,
    bool isTimedSession,
    float userBufferLaps,        // NEW: User-configured base buffer
    bool enableDynamicBuffer)    // NEW: Whether to apply dynamic adjustments
{
    var bufferData = new BufferData
    {
        TotalBuffer = userBufferLaps  // Use user setting instead of hardcoded 0.5f
    };
    
    // If dynamic buffer disabled, return user setting immediately
    if (!enableDynamicBuffer)
    {
        bufferData.Reason = $"Fixed: {userBufferLaps:F2} laps (dynamic buffer disabled)";
        return bufferData;
    }
    
    // Otherwise, continue with dynamic calculations...
```

**File**: `src/iRacingOverlay.Core/Services/FuelCalculatorService.cs`

**Change 2**: Pass user settings to calculator:
```csharp
// Around line 500 (in CalculateStrategy method)
var bufferData = _bufferCalculator.Calculate(
    fuelConsistencyVariance,
    CurrentData.RacePosition,
    CurrentData.TotalCars,
    telemetry.WeatherType == 1,  // 1 = Rain
    yellowFlagCount,
    telemetry.LapsCompleted,
    totalLaps,
    isTimedSession,
    _bufferLaps,          // ADD: Pass user buffer setting
    _enableDynamicBuffer  // ADD: Pass dynamic buffer toggle
);
```

---

### Fix #2: Fuel Method Selection

**File**: `src/iRacingOverlay.Core/Services/FuelCalculatorService.cs`

**Change**: In `CalculateAverages()` method (~line 950):
```csharp
// BEFORE (hardcoded logic):
CurrentData.AvgFuelPerLap = greenFlagAverage > 0 ? greenFlagAverage : averages.L5;

// AFTER (use user selection):
CurrentData.AvgFuelPerLap = CurrentData.SelectedMethod switch
{
    FuelAveragingMethod.Last => averages.Last,
    FuelAveragingMethod.Last5 => averages.L5,
    FuelAveragingMethod.Last10 => averages.L10,
    FuelAveragingMethod.Session => averages.Session,
    FuelAveragingMethod.Max => averages.Max,
    FuelAveragingMethod.EMA => averages.EMA,
    FuelAveragingMethod.GreenFlagOnly => averages.GreenOnly,
    FuelAveragingMethod.StintAverage => averages.Stint,
    FuelAveragingMethod.Adaptive => averages.Adaptive,
    FuelAveragingMethod.Current => telemetry.LapDistPct > 0.1f ? 
        CurrentData.CurrentLapFuelRate : averages.Last,
    _ => greenFlagAverage > 0 ? greenFlagAverage : averages.L5  // Safe fallback
};

// Log selected method for debugging
LogDebug($"[AVERAGING] Using {CurrentData.SelectedMethod}: {CurrentData.AvgFuelPerLap:F3}L/lap");
```

---

### Fix #3: Remove Fuel Pressure (Not in SDK)

**File**: `src/iRacingOverlay.WPF/Models/AppSettings.cs`

**Change**: Remove or comment out:
```csharp
// REMOVE THIS:
// public bool FuelWidget_ShowFuelPressure { get; set; } = false;
```

**File**: `src/iRacingOverlay.Core/Services/FuelCalculatorService.cs`

**Change**: Remove unused variables:
```csharp
// REMOVE LINES 77-79:
// private bool _baselineFuelPressureEstablished = false;
// private readonly List<float> _fuelPressureHistory = new();
// private const int BASELINE_LAPS_NEEDED = 3;
```

**File**: `src/iRacingOverlay.WPF/Widgets/FuelWidget/*.xaml`

**Change**: Remove PRESS field from UI (if exists)

---

### Fix #4: Historical Learning Reset

**File**: `src/iRacingOverlay.Core/Services/FuelCalculatorService.cs`

**Change**: In `Reset()` method:
```csharp
public void Reset()
{
    // ... existing reset logic ...
    
    // FIX: Reset historical data flag to allow re-application on new session
    _historicalDataApplied = false;
    
    LogDebug("FuelCalculatorService reset - ready for new session");
}
```

---

## ✅ Success Criteria

Fixes complete when:
1. ✅ Changing `FuelWidget_BufferLaps` from 1.0 to 2.0 increases fuel buffer in calculations
2. ✅ Disabling `FuelWidget_EnableDynamicBuffer` prevents dynamic adjustments
3. ✅ Changing `FuelWidget_Method` dropdown updates displayed average method
4. ✅ Fuel pressure field removed OR implemented (decision needed)
5. ✅ Historical data re-applies when starting new session after previous session
6. ✅ All fixes validated with build and runtime testing
7. ✅ Documentation updated to reflect working features

---

## 📚 Related Documentation

- `DEVELOPMENT_ROADMAP.md` - Phase 6 refactoring status
- `FUEL_CALCULATOR_IMPROVEMENTS.md` - Planned enhancements
- `REFACTORING_ROADMAP.md` - Service extraction details
- `docs/iRacing_SDK_Variables_Reference.md` - Available telemetry fields

---

**Status**: ✅ **Investigation Complete - Ready for Implementation**

**Next Steps**:
1. Review findings with team
2. Prioritize fixes (recommend Priority 1 + 2 first)
3. Create implementation branch
4. Test fixes thoroughly before merge
5. Update user documentation with corrected feature list
