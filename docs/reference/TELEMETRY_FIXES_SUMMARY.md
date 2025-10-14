# Telemetry Data Fixes Summary

**Date:** 2025-01-XX  
**Status:** Phase 1 Complete - Testing Required

---

## ✅ **COMPLETED FIXES**

### 1. **RPM Zone Thresholds Adjusted**
**File:** `ShiftPointCalculator.cs`  
**Issue:** RPM zones were at 85%, optimal, 95%  
**Fix:** Updated to user-specified thresholds:
- **Teal (Safe):** 0-86% of redline
- **Yellow (Warning):** 87-92% of redline  
- **Orange (Optimal):** 93-95% of redline
- **Red (Danger):** 96-100%+ of redline

### 2. **Longitudinal G-Force Fixed**
**File:** `TelemetryDataMapper.cs`  
**Issue:** Returned 0f despite data.LongAccel being available  
**Fix:** Now returns `data.LongAccel` (acceleration/braking G-force)

### 3. **Vertical G-Force Corrected**
**File:** `TelemetryDataMapper.cs`  
**Issue:** Showed 9.8 at standstill (included gravity)  
**Fix:** Now returns `data.VertAccel - 9.8f` (subtracts gravity baseline)

### 4. **Tire Wear Format Fixed**
**File:** `TelemetryDataMapper.cs`  
**Issue:** Showed "1.0" which was unclear  
**Fix:** Now multiplies by 100 to show as percentage (1.0 → 100%)

---

## ⏳ **PENDING FIXES (Require SDK Research)**

### 5. **Air Temperature**
**Status:** TODO - SDK field unknown  
**Current:** Returns 0f  
**Needed:** Research if SDK provides `AirTemp` or similar field

### 6. **Track Temperature**
**Status:** TODO - SDK field unknown  
**Current:** Returns 0f  
**Needed:** Research if SDK provides `TrackTemp` or `TrackTempCrew` field

### 7. **Brake Temperatures**
**Status:** TODO - Incorrect field mapping  
**Current:** TelemetryData has brake LINE pressure (LFbrakeLinePress, etc.)  
**Issue:** User wants brake TEMPERATURES, not pressure  
**Needed:** Research SDK fields like `LFbrakeTemp`, `RFbrakeTemp`, etc.

### 8. **Tire Pressures**
**Status:** TODO - SDK field unknown  
**Current:** Returns 0f  
**Needed:** Research SDK fields like `LFtirePsi`, `RFtirePsi`, etc.

### 9. **Track Name**
**Status:** TODO - Session info not accessed  
**Current:** Returns "N/A"  
**Needed:** Access SDK session info for track name (may require different SDK call)

### 10. **In Pit Lane Detection**
**Status:** TODO - SessionFlags not decoded  
**Current:** Returns false (hardcoded)  
**Available:** `data.SessionFlags` is populated  
**Needed:** Research SessionFlags bit values for pit lane detection  
**Note:** iRacing SessionFlags likely has bit flags like:
- `0x0001` - Checkered flag
- `0x0002` - White flag  
- `0x0004` - Green flag
- etc.  
Need to find which bit indicates "OnPitRoad"

### 11. **Fuel Laps Remaining**
**Status:** TODO - Calculation not implemented  
**Current:** Field doesn't exist  
**Needed:** 
- Track average fuel usage per lap
- Calculate: `FuelLevel / AvgFuelPerLap`
- Requires fuel consumption tracking over multiple laps

### 12. **Fuel To End**
**Status:** TODO - Calculation not implemented  
**Current:** Field doesn't exist  
**Needed:**
- Know session laps remaining OR session time remaining
- Know fuel consumption rate
- Calculate: `FuelNeeded - FuelLevel`

---

## 🔍 **SDK RESEARCH NEEDED**

To complete the remaining fixes, we need to:

1. **Check SDK documentation** for available fields:
   - Browse iRacing SDK docs or source code
   - Look for telemetry field names
   - Check session info structure

2. **Common SDK field patterns** (educated guesses):
   ```
   AirTemp, TrackTemp, TrackTempCrew
   LFbrakeTemp, RFbrakeTemp, LRbrakeTemp, RRbrakeTemp
   LFtirePsi, RFtirePsi, LRtirePsi, RRtirePsi
   SessionFlags (bit field)
   TrackName (from session info string)
   ```

3. **SessionFlags bit values:**
   - Need to find iRacing SessionFlags enum/constants
   - Look for "OnPitRoad" or similar flag
   - Implement bit checking: `(SessionFlags & PIT_LANE_BIT) != 0`

4. **Session Info:**
   - Track name may require parsing session info YAML string
   - SDK may provide `GetSessionInfoStr()` method
   - Need to parse YAML for track details

---

## 🧪 **TESTING RECOMMENDATIONS**

### Test the Completed Fixes:
1. **RPM Zones:**
   - Rev engine through all RPM ranges
   - Verify colors change at 87%, 93%, 96% of redline
   - Check that zones show correctly in Data Widget

2. **G-Forces:**
   - **Vertical G:** Should show ~0.0 at standstill (not 9.8)
   - **Longitudinal G:** Should show positive when accelerating, negative when braking
   - **Lateral G:** Will show slight offset on banked tracks (expected)

3. **Tire Wear:**
   - Should now show as percentage (e.g., "98.5%" instead of "0.985")
   - Should decrease as tires wear

### Test the Pending Fixes:
After SDK research is complete:
- Verify air/track temperatures display
- Verify brake temperatures (not pressures)
- Verify tire pressures
- Verify track name shows correctly
- Test pit lane detection by entering/exiting pits
- Test fuel calculations over multiple laps

---

## 📝 **IMPLEMENTATION NOTES**

### Adding New SDK Fields:

**Step 1:** Add properties to `TelemetryData.cs`
```csharp
public float AirTemp { get; set; }
public float TrackTemp { get; set; }
public float LFbrakeTemp { get; set; }
// etc.
```

**Step 2:** Map in `IRacingTelemetryService.cs`
```csharp
AirTemp = sdkData.AirTemp,
TrackTemp = sdkData.TrackTemp,
// etc.
```

**Step 3:** Update `TelemetryDataMapper.cs`
```csharp
TelemetryField.AirTemp => data.AirTemp,
TelemetryField.TrackTemp => data.TrackTemp,
// etc.
```

### Implementing Fuel Calculations:

Will require adding fuel tracking logic:
```csharp
// Track fuel consumption
private Queue<float> _fuelPerLap = new Queue<float>();
private float _lastFuelLevel;
private int _lastLap;

// On lap complete:
float fuelUsed = _lastFuelLevel - currentFuelLevel;
_fuelPerLap.Enqueue(fuelUsed);
if (_fuelPerLap.Count > 5) _fuelPerLap.Dequeue(); // Keep last 5 laps

// Calculate average
float avgFuelPerLap = _fuelPerLap.Average();
float lapsRemaining = currentFuelLevel / avgFuelPerLap;
```

---

## 🎯 **PRIORITY ORDER**

**HIGH PRIORITY (Testing Phase 1):**
- ✅ RPM zones
- ✅ Longitudinal G
- ✅ Vertical G  
- ✅ Tire wear format

**MEDIUM PRIORITY (Phase 2):**
- ⏳ In pit lane detection (SessionFlags)
- ⏳ Brake temperatures
- ⏳ Tire pressures

**LOW PRIORITY (Phase 3):**
- ⏳ Air/Track temperatures
- ⏳ Track name
- ⏳ Fuel calculations

---

## 🚀 **NEXT STEPS**

1. **User Testing:** Test Phase 1 fixes in iRacing
2. **SDK Research:** Find field names for pending items
3. **Phase 2 Implementation:** Add SDK fields to code
4. **Phase 3 Implementation:** Add fuel calculations
5. **Final Testing:** Comprehensive test of all telemetry

---

**Build Status:** ✅ Succeeded (4.4s)  
**Files Modified:** 2 (ShiftPointCalculator.cs, TelemetryDataMapper.cs)
