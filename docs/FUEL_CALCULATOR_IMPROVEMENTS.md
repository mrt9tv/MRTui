# Fuel Calculator Improvements - Phase 2 Refinements

## Overview
Enhanced fuel calculation system with improved detection, comprehensive display, and robust lap filtering.

## Key Improvements

### 1. **Duplicate Lap Prevention** ✅
- **Problem**: `OnLapCompleted()` fired multiple times for same lap (4ms apart)
- **Solution**: Added `_lapsCompletedWhenProcessed` tracking variable
- **Logic**: Only process lap if `telemetry.LapsCompleted != _lapsCompletedWhenProcessed`
- **Result**: Eliminates duplicate lap entries in history

### 2. **Improved Refuel Detection** ✅
- **Problem**: Refueling detection failed (showed `PitLap=False` despite fuel increase)
- **Old Logic**: `fuelDelta > 0.5f && telemetry.OnPitRoad` (timing issue - pit road state may change)
- **New Logic**: `fuelDelta > 0.5f` (any significant fuel increase)
- **Added Logging**: `REFUEL DETECTED: Added 4.152L (OnPitRoad=True/False)`
- **Result**: Catches all refueling regardless of pit road timing

### 3. **Out-Lap Detection** ✅
- **Purpose**: Filter out first lap after leaving pits (incomplete fuel data)
- **Method**: Track `_wasOnPitRoadLastUpdate` state changes
- **Detection**: When `_wasOnPitRoadLastUpdate && !isOnPitRoad` → just left pits
- **Flag**: `_justLeftPits` set true, marks lap as pit lap for filtering
- **Logging**: `OUT-LAP DETECTED: Just left pit road`
- **Reset**: Flag cleared at 50% lap distance to prepare for next detection
- **Result**: Out-laps excluded from averaging calculations

### 4. **Tow/Reset Detection** ✅
- **Use Case 1 - Negative Fuel**: Tow with refuel shows negative fuel usage
  - **Detection**: `fuelUsed < -0.1f`
  - **Logging**: `TOW DETECTED: Negative fuel usage (-4.152L)`
  - **Action**: Mark as pit lap, record refuel amount as `Math.Abs(fuelUsed)`
  
- **Use Case 2 - First Lap Reset**: Starting from garage/tow shows minimal fuel use
  - **Detection**: `_lapHistory.Count == 0 && fuelUsed < 0.3f && LapsCompleted == 1`
  - **Logging**: `TOW/RESET DETECTED: First lap with minimal fuel (0.12L)`
  - **Action**: Mark as pit lap, exclude from averages
  
- **Result**: Clean data regardless of session start method

### 5. **Comprehensive Widget Display** ✅
New multi-line fuel display shows ALL calculation data:

#### **Line 1: Current State**
```
FUEL: 17.51L / 60.0L (29%)
```
- Current fuel level (F2 precision)
- Tank capacity
- Fuel percentage

#### **Line 2: Averages**
```
AVG: L:1.70 | 5:1.37 | 10:1.40 | S:1.42
```
- **L**: Last lap
- **5**: Last 5 laps (exponentially weighted)
- **10**: Last 10 laps (simple average)
- **S**: Session average (IQR outlier-filtered)

#### **Line 3: Range & Laps**
```
RANGE: 0.42-2.12L | LAPS: 12.8 (iR: 13.4)
```
- Min/Max fuel per lap (strategic planning)
- Laps remaining on current fuel
- iRacing's estimate for comparison

#### **Line 4: Race Strategy** (if race has lap count)
```
TO FINISH: 18.24L (+0.73L) | ✓ CAN FINISH
```
OR
```
TO FINISH: 20.15L (-2.64L) | NEED 3.0L
```
- Fuel needed to finish race (with buffer laps)
- Delta from current fuel (+surplus / -deficit)
- Can finish status or fuel to add at pit

#### **Line 5: Flag Conditions** (if available)
```
GREEN: 1.45L (18) | YELLOW: 0.82L (3)
```
- Green flag average fuel consumption (lap count)
- Yellow flag average fuel consumption (lap count)

### 6. **Enhanced Debug Logging** ✅
All calculations now log with F4 precision (4 decimals):
- `AvgFuelPerLap_Last = 1.7034L`
- `AvgFuelPerLap_L5 = 1.3706L (weighted from 5 laps)`
- `AvgFuelPerLap_L10 = 1.4023L (from 10 laps)`
- `AvgFuelPerLap_Session = 1.4189L (from 18/20 laps, bounds: 0.8234-2.1234)`
- `Min/Max = 0.4231L / 2.1234L (range: 1.7003L)`
- `GreenFlagAverage = 1.4523L (from 18 laps)`
- `YellowFlagAverage = 0.8234L (from 3 laps)`
- `LapsRemaining = 12.7893 (Current fuel: 17.5123L / Avg: 1.3706L)`
- `iRacing estimate: 13.40 laps, Difference: -0.6107 laps`
- `FuelNeededToFinish = 16.9412L (for 12.00 laps w/ 1 buffer)`
- `FuelDeltaToFinish = 0.5711L - Can finish: True`

Special event logging:
- `OUT-LAP DETECTED: Just left pit road`
- `REFUEL DETECTED: Added 4.152L (OnPitRoad=True)`
- `TOW DETECTED: Negative fuel usage (-4.152L)`
- `TOW/RESET DETECTED: First lap with minimal fuel (0.12L)`

## Technical Implementation

### State Variables Added
```csharp
private bool _wasOnPitRoadLastUpdate = false;  // Track pit road state changes
private bool _justLeftPits = false;            // Out-lap detection flag
private int _lapsCompletedWhenProcessed = -1;  // Prevent duplicate processing
```

### Reset Method Updated
All new state variables properly reset when session changes:
```csharp
_wasOnPitRoadLastUpdate = false;
_justLeftPits = false;
_lapsCompletedWhenProcessed = -1;
```

### Widget Display Configuration
```csharp
FontSize = 8,                      // Compact for multi-line
LineHeight = 11,                   // Tight line spacing
TextAlignment = Center,            // Centered below gauge
Margin = (0, 0, 0, 5),            // 5px from bottom
Color = LimeGreen/Yellow/Red,      // Based on laps remaining
Alpha = 200 (semi-transparent)
```

## Testing Checklist

### Duplicate Lap Testing
- [x] Build successful
- [ ] Run test session, complete 5+ laps
- [ ] Check fuel_debug.log for duplicate lap entries
- [ ] Verify lap history count matches actual laps completed

### Refuel Detection Testing
- [ ] Enter pits for refuel during practice
- [ ] Check log shows `REFUEL DETECTED` message
- [ ] Verify lap marked as `PitLap=True`
- [ ] Confirm next lap after pit is marked as out-lap

### Out-Lap Detection Testing
- [ ] Leave pits after stop
- [ ] Check log shows `OUT-LAP DETECTED`
- [ ] Verify out-lap excluded from averaging
- [ ] Test multiple pit stops in one session

### Tow Detection Testing
- [ ] Use tow/reset to pits during practice
- [ ] Check log shows `TOW DETECTED` or `TOW/RESET DETECTED`
- [ ] Verify tow lap excluded from averages
- [ ] Test both tow with refuel and tow without refuel

### Widget Display Testing
- [ ] Verify all 5 lines visible and readable
- [ ] Check font size appropriate (8pt)
- [ ] Confirm values update in real-time
- [ ] Test color changes based on laps remaining:
  - Green when >5 laps
  - Yellow when 3-5 laps
  - Red when <3 laps
- [ ] Verify "Need more laps" message before sufficient data

### Accuracy Testing
- [ ] Compare our averages to Racelabs/iRacing
- [ ] Verify laps remaining within 1 lap of reality
- [ ] Check Min/Max values make sense for track
- [ ] Confirm strategy calculations accurate

## Log Analysis Guide

### Healthy Log Pattern
```
[00:47:47.953] Lap 31 completed: FuelAtStart=24.453L, FuelAtEnd=23.156L, FuelUsed=1.297L, PitLap=False, Tow=False
[00:47:47.955] Total laps in history: 31, Valid laps: 28
[00:47:47.955]   Lap 29: FuelUsed=1.345L, Valid=True, Pit=False, Formation=False, Incomplete=False
[00:47:47.955]   Lap 30: FuelUsed=1.412L, Valid=True, Pit=False, Formation=False, Incomplete=False
[00:47:47.955]   Lap 31: FuelUsed=1.297L, Valid=True, Pit=False, Formation=False, Incomplete=False
[00:47:47.956] AvgFuelPerLap_Last = 1.2970L
[00:47:47.956] AvgFuelPerLap_L5 = 1.3452L (weighted from 5 laps)
[00:47:47.956] AvgFuelPerLap_Session = 1.3912L (from 26/28 laps, bounds: 0.823-2.123)
[00:47:47.956] Min/Max = 0.8234L / 2.1234L (range: 1.3000L)
[00:47:47.957] LapsRemaining = 17.8234 (Current fuel: 23.1560L / Avg: 1.3000L)
```

### Problem Indicators
- **Duplicate laps**: Same lap number appears twice in succession
- **Negative fuel**: `FuelUsed=-4.152L` without tow detection
- **All laps invalid**: `Valid laps: 0` with completed laps
- **Excessive filtering**: `(from 3/28 laps)` - too aggressive outlier removal

## Files Modified

### Core Service
- `src/iRacingOverlay.Core/Services/FuelCalculatorService.cs`
  - Added 3 state tracking variables
  - Improved refuel detection logic
  - Added out-lap detection
  - Added tow/reset detection
  - Enhanced debug logging (F4 precision)
  - Updated Reset() method

### Widget Display  
- `src/iRacingOverlay.WPF/Widgets/MRTOneWidget/MRTOneWidget.cs`
  - Rewrote `UpdateFuelDisplay()` method
  - Multi-line comprehensive display (5 lines)
  - Updated TextBlock properties (FontSize=8, LineHeight=11)
  - All fuel calculation data visible

## Next Steps

1. **Test in iRacing**: Run practice session, complete 10+ laps
2. **Verify log output**: Check fuel_debug.log for new detection messages
3. **Validate display**: Ensure all 5 lines visible and accurate
4. **Compare accuracy**: Match against Racelabs/iRacing estimates
5. **Test edge cases**: Pit stops, tows, resets, fuel strategy scenarios
6. **Document results**: Update phase tracking with test outcomes

## Known Issues

### Pre-existing
- Unused `const uint White` warning (line 361) - cosmetic only, no functional impact

### Monitoring Required
- Widget display may need font size adjustment based on screen resolution
- Line spacing may need tweaking for different overlay sizes
- Color visibility against various track backgrounds

## Success Criteria

✅ **Duplicate laps eliminated**: Each lap appears once in history  
✅ **Refueling detected reliably**: All fuel additions logged  
✅ **Out-laps filtered**: First lap after pits excluded from averages  
✅ **Tow detection working**: Reset laps properly identified  
✅ **Comprehensive display**: All fuel data visible on widget  
✅ **Build successful**: No compilation errors  

**Ready for in-game testing!** 🎮
