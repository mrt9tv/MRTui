# Radar System Debugging Guide

**Status**: Phase 1-3 complete, debugging phase active  
**Build**: ✅ Successful  
**Date**: 2025-01-21

## Changes Made

### 1. Visual Positioning Fix (Issue #1)
**Problem**: Radar squares were positioned inside the circle (overlapping) due to 5px margins being too small.

**Solution**: Repositioned squares to the very edges of the 200x200 grid:
- All margins set to `0` (at grid edges)
- Used proper `HorizontalAlignment` and `VerticalAlignment`
- Reduced size from 16x16 to 12x12 pixels
- Added subtle rounding (RadiusX/Y = 2)

**Visual Result**:
- Front square: Top center of grid
- Back square: Bottom center of grid
- Left square: Left center of grid
- Right square: Right center of grid
- All squares now clearly visible outside circle

---

### 2. Comprehensive Debug Logging Added

#### A. MRTOneWidget.UpdateRadarSquares()
Logs every 30Hz update with:
```
[RADAR DEBUG] CarLeftRight raw: {value}, Enum: {LateralPosition}
[RADAR DEBUG] HasLeft: {bool}, HasRight: {bool}
[RADAR DEBUG] FrontZone: {ProximityZone}, RearZone: {ProximityZone}
[RADAR DEBUG] FrontCar: #{carIdx} Dist:{distance} Zone:{zone} | NONE
[RADAR DEBUG] RearCar: #{carIdx} Dist:{distance} Zone:{zone} | NONE
[RADAR DEBUG] Player: Idx={idx}, Pct={lapPct}, Lap={lapNumber}
---
```

#### B. ProximityCalculator.GetNearbyCars()
Logs during car scanning:
```
[PROXIMITY DEBUG] Scanning cars... PlayerIdx={idx}, Pct={pct}, Lap={lap}
[PROXIMITY DEBUG] Car #{idx}: Pct={pct}, Lap={lap}, RelDist={dist}, AbsDist={dist}
[PROXIMITY DEBUG] Total scanned: {count}, Filtered: {count}, Detected: {count}
```

**What to Watch**: Console output will now show exactly what the radar system "sees" in real-time.

---

## Remaining Issues to Debug

### Issue #2: Left Square Always Red
**Symptom**: Left radar stays red 98% of time with brief blinks off  
**Possible Causes**:
1. `CarLeftRight` enum stuck at value `1` (CarLeft) or `3` (CarBothSides)
2. SDK enum values different than expected (0/1/2/3)
3. Test scenario has car permanently on left
4. Telemetry data caching/stuck values

**Debug Strategy**:
- Watch console for `CarLeftRight raw:` values
- Verify enum changes when driving alone vs beside cars
- Check if value is truly stuck or oscillating quickly
- Compare with in-game spotter audio ("car left" calls)

**Expected Behavior**:
- Alone on track: `CarLeftRight = 0` (Clear) → Green
- Car on left: `CarLeftRight = 1` (CarLeft) → Red
- Car on right: `CarLeftRight = 2` (CarRight) → Green
- Cars both sides: `CarLeftRight = 3` (CarBothSides) → Both Red

---

### Issue #3: Right Square Rarely Activates
**Symptom**: Right radar only blinks briefly when left blinks off  
**Possible Causes**:
1. Related to Issue #2 - asymmetry in lateral detection
2. Enum values swapped (left/right reversed?)
3. Test track configuration (left-hand turns bias?)
4. Cars preferring left side overtakes

**Debug Strategy**:
- Compare `HasLeft` vs `HasRight` console values
- Drive on right side of track with cars on right
- Verify `CarLeftRight = 2` appears in console
- Test with different cars/tracks

---

### Issue #4: Front Square Inaccurate
**Symptom**: Color fades in/out but doesn't match actual car positions  
**Possible Causes**:
1. Distance thresholds too tight/loose for track length
2. Lap wrap-around calculation wrong (0.5 boundary)
3. Car filtering too aggressive (pit road, invalid positions)
4. `RelativeDistance` calculation error

**Current Thresholds**:
- VeryClose: < 2% (Red)
- Close: 2-5% (OrangeRed)
- Near: 5-10% (Orange)
- Far: 10-15% (Yellow)
- Clear: > 15% (Green)

**Debug Strategy**:
- Watch `FrontCar: Dist:` values in console
- Note at what distance color changes occur
- Compare with actual gap to car ahead (F3 blackbox in iRacing)
- Check if `Total scanned` and `Detected` counts make sense
- Verify `RelDist` is positive for cars ahead

**Expected Behavior**:
- Following car closely: Red/OrangeRed
- Medium gap: Orange/Yellow
- Large gap or alone: Green

---

### Issue #5: Rear Square Never Activates
**Symptom**: Back radar always stays green despite cars behind  
**Possible Causes**:
1. `GetClosestCarBehind()` logic broken
2. `RelativeDistance` sign issue (negative for behind?)
3. No cars actually behind in test scenario
4. Cars behind filtered out (pit road, invalid data)

**Debug Strategy**:
- Watch `RearCar:` console output - should show NONE or negative distance
- Let AI cars pass you, verify rear activates
- Check `Total scanned` vs `Detected` - are cars being filtered?
- Look for cars with negative `RelDist` values
- Verify `IsBehind` property logic in ProximityInfo

**Expected Behavior**:
- Car close behind: Red/OrangeRed
- Car further back: Orange/Yellow
- No cars behind: Green

---

## Testing Procedure

### 1. Launch with Debug Console
```powershell
dotnet run --project src\iRacingOverlay.WPF
```
- Console window will show all debug output
- Keep console visible alongside iRacing

### 2. iRacing Test Scenarios

**A. Solo Baseline Test**
- Join practice session alone
- Drive 2-3 laps
- **Expected**: All 4 squares green, `CarLeftRight = 0`, no cars detected

**B. Lateral Spotter Test**
- Join with AI or other players
- Drive alongside cars (side-by-side)
- Move left/right relative to other cars
- **Expected**: Left/right squares turn red when car beside

**C. Front Proximity Test**
- Follow AI car at varying distances
- Close up slowly from far back
- **Expected**: Front square changes Green→Yellow→Orange→OrangeRed→Red

**D. Rear Proximity Test**
- Let AI car catch up from behind
- Let them follow closely
- **Expected**: Rear square changes Green→Yellow→Orange→OrangeRed→Red

**E. Full Multicar Test**
- Join crowded session (practice/race)
- Drive in traffic with cars all around
- **Expected**: Multiple squares active simultaneously

### 3. Console Data Collection

Create log file of console output:
```powershell
dotnet run --project src\iRacingOverlay.WPF > radar_debug_log.txt 2>&1
```

### 4. Compare with iRacing Data
- Use F3 (relative blackbox) to see actual car positions
- Use in-game spotter audio ("car left/right/inside/outside")
- Verify radar matches reality

---

## Known Technical Details

### Distance Calculation Algorithm
```csharp
// Positive = ahead, Negative = behind
float diff = carPct - playerPct;

// Wrap-around handling at 0%/100% boundary
if (diff > 0.5f) diff -= 1.0f;   // Car "behind" via wrap
if (diff < -0.5f) diff += 1.0f;  // Car "ahead" via wrap
```

### Enum Mappings
```csharp
// LateralPosition
Clear = 0
CarLeft = 1
CarRight = 2
CarBothSides = 3

// ProximityZone
Clear = 0
Far = 1        // 10-15% away
Near = 2       // 5-10% away
Close = 3      // 2-5% away
VeryClose = 4  // <2% away
```

### Update Frequency
- Telemetry SDK: ~60Hz
- Widget UI: 30Hz via `UpdateUI()`
- Radar squares: 30Hz via `UpdateRadarSquares()`

---

## Next Steps After Testing

1. **Analyze Console Logs**
   - Look for patterns in CarLeftRight values
   - Check if proximity zones match expected behavior
   - Verify RelativeDistance calculations

2. **Fix Identified Issues**
   - Adjust thresholds if needed (track length dependent?)
   - Fix enum mapping if swapped
   - Correct distance calculation if sign wrong
   - Add data validation if SDK values invalid

3. **Refinement Phase** (after bugs fixed)
   - Fine-tune distance thresholds per track type
   - Add smoothing/debouncing for rapid changes
   - Implement visual polish (animations, gradients)
   - Add advanced features (TTC, class filtering)

4. **Unit Testing**
   - Test lap wrap-around edge cases
   - Test multi-lap scenarios (lapped traffic)
   - Test all ProximityZone transitions
   - Test all LateralPosition states

---

## File Locations

**Modified Files**:
- `src/iRacingOverlay.WPF/Widgets/MRTOneWidget/MRTOneWidget.cs`
  - Lines ~136-193: Radar square creation (positioning fixed)
  - Lines ~968-1010: UpdateRadarSquares() with debug logging

- `src/iRacingOverlay.Core/Services/ProximityCalculator.cs`
  - Lines ~84-180: GetNearbyCars() with comprehensive logging

**Related Files**:
- `src/iRacingOverlay.Core/Services/LateralSpotter.cs` - Left/right detection
- `src/iRacingOverlay.Core/Models/TelemetryData.cs` - CarLeftRight property
- `src/iRacingOverlay.WPF/Models/AppSettings.cs` - EnableLateralSpotter toggle

---

## Quick Reference: Console Output Meanings

### Lateral Spotter
```
CarLeftRight raw: 0  → No cars beside (Clear)
CarLeftRight raw: 1  → Car on left (Red left square)
CarLeftRight raw: 2  → Car on right (Red right square)
CarLeftRight raw: 3  → Cars both sides (Red both squares)
```

### Proximity Zones
```
FrontZone: Clear      → Green (no car ahead)
FrontZone: Far        → Yellow (10-15% ahead)
FrontZone: Near       → Orange (5-10% ahead)
FrontZone: Close      → OrangeRed (2-5% ahead)
FrontZone: VeryClose  → Red (<2% ahead)
```

### Distance Values
```
RelDist: +0.0500  → Car 5% ahead (positive = ahead)
RelDist: -0.0300  → Car 3% behind (negative = behind)
AbsDist: 0.0300   → Absolute gap 3% (always positive)
```

---

## Success Criteria

Radar system working correctly when:
1. ✅ Squares positioned outside circle (not overlapping)
2. ❓ Left square: Red only when car beside on left
3. ❓ Right square: Red only when car beside on right
4. ❓ Front square: Color gradient matches distance to car ahead
5. ❓ Rear square: Color gradient matches distance to car behind
6. ✅ Console logs provide clear debugging data
7. ❓ All zones transitions smooth and accurate
8. ❓ No false positives/negatives during racing

**Status**: 1/8 confirmed, 6 pending validation, 1 visual fix complete

---

*Last Updated: 2025-01-21 - Positioning fixed, debug logging added, ready for iRacing testing*
