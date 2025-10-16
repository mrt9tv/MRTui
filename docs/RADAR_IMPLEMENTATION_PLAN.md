# Proximity Radar Implementation Plan

**Date**: October 15, 2025 (Updated)  
**SDK Version**: v0.9.8.3  
**Approach**: Track-Position Based + Lateral Detection  
**Estimated Time**: 10-13 hours total

---

## Overview

We will implement a **4-way proximity radar system** using **confirmed iRacing SDK telemetry variables**. This combines:
- **Track position percentages** (`CarIdxLapDistPct`) for front/back detection with specific car identification
- **Lateral spotter data** (`CarLeftRight`) for left/right presence detection (same as in-game spotter)

### What We Can Build

✅ **Front Proximity Detection** - Cars ahead on track (specific cars with position/gap)  
✅ **Back Proximity Detection** - Cars behind on track (specific cars with position/gap)  
✅ **Left/Right Spotter** - Car presence indicators beside player (spotter-style)  
✅ **Distance Zones** - Close/Near/Far color coding for front/back  
✅ **Multi-Car Tracking** - All 64 cars in session  
✅ **Class Filtering** - Show only same-class cars  
✅ **Time-to-Collision** - Estimated time until overlap  
✅ **Relative Speed** - Faster/slower indicators  
✅ **Lapped Traffic** - Identify cars +/-1 lap  
✅ **Toggleable Spotter** - Enable/disable left/right indicators per user preference

### What We Cannot Build

❌ **Precise Meter Distance** - Track % only (no 3D coordinates)  
❌ **Which Car is Left/Right** - Only presence detection, not identification  
❌ **Multiple Car Count L/R** - Only knows "car(s) present" not how many  
❌ **Off-Track Path** - Assumes cars follow racing line  

---

## Required Telemetry Variables

### Player Data (Already Have)
```csharp
"Speed",              // Player speed (m/s)
"LapDistPct",         // Player position on track (0-1)
"Lap",                // Current lap number
"PlayerCarClassPosition", // Player class position
```

### Lateral Spotter (NEW - Critical!)
```csharp
"CarLeftRight",       // int (enum) - Car presence beside player
                      // Values: 0=LRClear, 1=LRCarLeft, 2=LRCarRight, 3=LRCarLeftRight
                      // This is what the in-game spotter uses!
```

### New CarIdx Arrays (Need to Add)
```csharp
// Core proximity detection
"CarIdxLapDistPct",      // float[64] - Track position for each car
"CarIdxOnPitRoad",       // bool[64] - Pit road status

// Filtering & context
"CarIdxTrackSurface",    // int[64] - Track surface type
"CarIdxClass",           // int[64] - Car class ID
"CarIdxLap",             // int[64] - Lap number for each car
"CarIdxPosition",        // int[64] - Race position
"CarIdxClassPosition",   // int[64] - Class position

// Status indicators
"CarIdxGear",            // int[64] - Gear (-1=R, 0=N, 1+=forward)
"CarIdxRPM",             // float[64] - Engine RPM

// Advanced features
"CarIdxEstTime",         // float[64] - Est. time to reach position
"CarIdxF2Time",          // float[64] - Time behind leader
"CarIdxLastLapTime",     // float[64] - Last lap time
```

### Player Orientation (Need to Add)
```csharp
"Yaw",                   // float - Player heading angle (radians)
"YawRate",               // float - Rate of heading change
```

---

## Architecture

### Data Layer
- **IRacingTelemetryService**: Add CarIdx arrays + CarLeftRight to RequiredTelemetryVars
- **TelemetryData**: Add properties for all arrays + lateral enum (auto-generated)
- **TelemetryDataMapper**: Add array handling for proximity calculations

### Business Logic
- **ProximityCalculator** (NEW): Calculate relative positions and distances
  - Front detection algorithm (specific cars with CarIdxLapDistPct)
  - Back detection algorithm (specific cars with CarIdxLapDistPct)
  - Distance zone classification (close/near/far)
  - Time-to-collision estimation
- **LateralSpotter** (NEW): Simple enum passthrough for left/right presence

### UI Layer
- **RadarWidget** (NEW): Display 4-way proximity indicators
  - **MRT One Integration**: Simple color indicators outside the existing circular gauge
  - Front/Back zones: Full car info with position/gap/distance
  - Left/Right zones: Simple color presence indicators (green/yellow/red)
  - Toggleable spotter: User can enable/disable left/right indicators
  - Color-coded distance zones for front/back
  - Minimalist design to avoid clutter

---

## Implementation Phases

### Phase 1: Core Telemetry (2 hours)

**Goal**: Add CarIdx arrays + CarLeftRight and verify SDK provides data

**Tasks**:
1. Update `IRacingTelemetryService.cs` RequiredTelemetryVars:
   ```csharp
   // Add after existing variables:
   
   // Lateral Spotter (4-way radar)
   "CarLeftRight",         // ⭐ NEW: Enum for left/right car presence
   
   // Track Position Arrays
   "CarIdxLapDistPct",
   "CarIdxOnPitRoad",
   "CarIdxTrackSurface",
   "CarIdxClass",
   "CarIdxLap",
   "CarIdxPosition",
   "CarIdxClassPosition",
   "CarIdxGear",
   "CarIdxRPM",
   "CarIdxEstTime",
   "CarIdxF2Time",
   "CarIdxLastLapTime",
   "Yaw",
   "YawRate"
   ```

2. Build solution and verify code generation

3. Add logging to verify array data + CarLeftRight:
   ```csharp
   // In OnTelemetryUpdate:
   if (data.CarIdxLapDistPct != null)
   {
       var carsOnTrack = data.CarIdxLapDistPct
           .Where((pct, idx) => pct >= 0 && pct <= 1)
           .Count();
       _logger.LogInformation($"Cars on track: {carsOnTrack}/64");
   }
   
   // Verify CarLeftRight enum
   _logger.LogInformation($"CarLeftRight: {data.CarLeftRight} (0=Clear, 1=Left, 2=Right, 3=Both)");
   ```

4. Test with iRacing practice session (AI opponents to test left/right detection)

**Deliverables**:
- ✅ CarIdx arrays available in TelemetryData
- ✅ CarLeftRight enum value available
- ✅ Verification that SDK provides array data
- ✅ Log output showing car counts and lateral spotter status

---

### Phase 2: Proximity Calculator + Lateral Spotter (3.5 hours)

**Goal**: Core algorithm for front/back detection + lateral presence enum

**Tasks**:
1. Create `ProximityCalculator.cs`:
   ```csharp
   public class ProximityCalculator
   {
       public ProximityResult Calculate(
           float playerLapDistPct,
           int playerLap,
           int playerClass,
           float[] carIdxLapDistPct,
           int[] carIdxLap,
           int[] carIdxClass,
           bool[] carIdxOnPitRoad)
       {
           // 1. Filter valid cars (same class, on track, not in pits)
           // 2. Calculate distance ahead/behind (handle wrap-around)
           // 3. Find closest car ahead and behind
           // 4. Classify distance zones
           // 5. Return proximity data
       }
   }
   ```

1b. Create `LateralSpotter.cs` (simple enum wrapper):
   ```csharp
   public enum LateralPosition
   {
       Clear = 0,          // LRClear - No cars beside
       CarLeft = 1,        // LRCarLeft - Car(s) on left
       CarRight = 2,       // LRCarRight - Car(s) on right
       CarBothSides = 3    // LRCarLeftRight - Cars on both sides
   }
   
   public class LateralSpotter
   {
       public LateralPosition GetLateralStatus(int carLeftRight)
       {
           return (LateralPosition)carLeftRight;
       }
       
       public bool HasCarLeft(int carLeftRight) 
           => carLeftRight == 1 || carLeftRight == 3;
       
       public bool HasCarRight(int carLeftRight) 
           => carLeftRight == 2 || carLeftRight == 3;
   }
   ```

2. Implement distance calculation:
   ```csharp
   private float CalculateDistance(float playerPct, float carPct, int playerLap, int carLap)
   {
       // Same lap
       if (playerLap == carLap)
       {
           float diff = carPct - playerPct;
           
           // Handle wrap-around (car ahead crosses start/finish)
           if (diff < -0.5f) diff += 1.0f;
           if (diff > 0.5f) diff -= 1.0f;
           
           return diff; // Positive = ahead, negative = behind
       }
       // Car is lapped
       else if (carLap < playerLap)
       {
           return (carPct - playerPct) - 1.0f; // Behind by full lap
       }
       // Car is lapping us
       else
       {
           return (carPct - playerPct) + 1.0f; // Ahead by full lap
       }
   }
   ```

3. Implement distance zones:
   ```csharp
   private DistanceZone ClassifyZone(float distancePct)
   {
       float absDist = Math.Abs(distancePct);
       
       if (absDist < 0.02f) return DistanceZone.VeryClose;  // <2% track
       if (absDist < 0.05f) return DistanceZone.Close;      // 2-5%
       if (absDist < 0.10f) return DistanceZone.Near;       // 5-10%
       return DistanceZone.Far;                             // >10%
   }
   ```

4. Unit tests for edge cases:
   - Lap wrap-around (99% → 1%)
   - Lapped cars (player on lap 10, car on lap 9)
   - Lapping cars (player on lap 5, leader on lap 6)
   - Multiple cars in same zone

5. Unit tests for lateral spotter:
   - Enum value mapping (0-3)
   - HasCarLeft/HasCarRight logic
   - Both sides scenario (value 3)

**Deliverables**:
- ✅ ProximityCalculator class with full algorithm
- ✅ LateralSpotter class with enum mapping
- ✅ Distance zone classification
- ✅ Unit tests passing
- ✅ Handles all edge cases

---

### Phase 3: Radar Widget UI with MRT One Integration (3-4 hours)

**Goal**: Visual 4-way proximity display integrated with MRT One circular gauge

**Design Concept**: 
```
       [FRONT]
      P5 +2.1s
         ↑
    [L] 🟢 [R]  ← Simple color indicators outside circle
         ↓
       [BACK]
      P8 -1.3s
```

**Tasks**:
1. Create `RadarWidget.xaml` (MRT One style):
   ```xml
   <Window>
       <Grid>
           <!-- Front Indicator (Top) -->
           <StackPanel VerticalAlignment="Top" HorizontalAlignment="Center">
               <TextBlock Text="FRONT" FontWeight="Bold" FontSize="10"/>
               <Border Background="{Binding FrontColor}" Padding="4">
                   <StackPanel>
                       <TextBlock Text="{Binding FrontCarInfo}" FontSize="12"/>
                       <TextBlock Text="{Binding FrontDistance}" FontSize="10"/>
                   </StackPanel>
               </Border>
           </StackPanel>
           
           <!-- Left Spotter Indicator (Simple color box) -->
           <Border Width="20" Height="40" 
                   HorizontalAlignment="Left" VerticalAlignment="Center"
                   Background="{Binding LeftSpotterColor}"
                   Visibility="{Binding ShowSpotter}"
                   ToolTip="Car on Left">
               <TextBlock Text="L" VerticalAlignment="Center" 
                         HorizontalAlignment="Center" FontWeight="Bold"/>
           </Border>
           
           <!-- Right Spotter Indicator (Simple color box) -->
           <Border Width="20" Height="40" 
                   HorizontalAlignment="Right" VerticalAlignment="Center"
                   Background="{Binding RightSpotterColor}"
                   Visibility="{Binding ShowSpotter}"
                   ToolTip="Car on Right">
               <TextBlock Text="R" VerticalAlignment="Center" 
                         HorizontalAlignment="Center" FontWeight="Bold"/>
           </Border>
           
           <!-- Back Indicator (Bottom) -->
           <StackPanel VerticalAlignment="Bottom" HorizontalAlignment="Center">
               <Border Background="{Binding BackColor}" Padding="4">
                   <StackPanel>
                       <TextBlock Text="{Binding BackCarInfo}" FontSize="12"/>
                       <TextBlock Text="{Binding BackDistance}" FontSize="10"/>
                   </StackPanel>
               </Border>
               <TextBlock Text="BACK" FontWeight="Bold" FontSize="10"/>
           </StackPanel>
       </Grid>
   </Window>
   ```

2. Create `RadarViewModel.cs` (with lateral spotter):
   ```csharp
   public class RadarViewModel : ViewModelBase
   {
       private ProximityCalculator _calculator;
       private LateralSpotter _lateralSpotter;
       
       // Front/Back (full car info)
       public string FrontCarInfo => FormatCarInfo(_frontCar);
       public string FrontDistance => FormatDistance(_frontDistance);
       public SolidColorBrush FrontColor => GetZoneColor(_frontZone);
       
       public string BackCarInfo => FormatCarInfo(_backCar);
       public string BackDistance => FormatDistance(_backDistance);
       public SolidColorBrush BackColor => GetZoneColor(_backZone);
       
       // Left/Right Spotter (simple presence indicators)
       public SolidColorBrush LeftSpotterColor => GetSpotterColor(_hasCarLeft);
       public SolidColorBrush RightSpotterColor => GetSpotterColor(_hasCarRight);
       public Visibility ShowSpotter => _settings.EnableLateralSpotter ? Visibility.Visible : Visibility.Collapsed;
       
       private void OnTelemetryUpdate(TelemetryData data)
       {
           // Front/Back detection (which specific cars)
           var result = _calculator.Calculate(
               data.LapDistPct,
               data.Lap,
               data.PlayerCarClass,
               data.CarIdxLapDistPct,
               data.CarIdxLap,
               data.CarIdxClass,
               data.CarIdxOnPitRoad);
           
           UpdateFront(result.ClosestAhead);
           UpdateBack(result.ClosestBehind);
           
           // Left/Right spotter (presence only)
           _hasCarLeft = _lateralSpotter.HasCarLeft(data.CarLeftRight);
           _hasCarRight = _lateralSpotter.HasCarRight(data.CarLeftRight);
           OnPropertyChanged(nameof(LeftSpotterColor));
           OnPropertyChanged(nameof(RightSpotterColor));
       }
       
       private SolidColorBrush GetSpotterColor(bool hasCar)
       {
           // Simple green/red indicator
           return hasCar 
               ? new SolidColorBrush(Colors.Red)      // Car present = RED warning
               : new SolidColorBrush(Colors.Green);   // Clear = GREEN safe
       }
   }
   ```

3. Color scheme:
   ```csharp
   // Front/Back zones (distance-based)
   private SolidColorBrush GetZoneColor(DistanceZone zone)
   {
       return zone switch
       {
           DistanceZone.VeryClose => new SolidColorBrush(Colors.Red),    // <2%
           DistanceZone.Close     => new SolidColorBrush(Colors.Orange),  // 2-5%
           DistanceZone.Near      => new SolidColorBrush(Colors.Yellow),  // 5-10%
           DistanceZone.Far       => new SolidColorBrush(Colors.Green),   // >10%
           _ => new SolidColorBrush(Colors.Gray)
       };
   }
   
   // Left/Right spotter (presence-based, simple 2-color)
   private SolidColorBrush GetSpotterColor(bool hasCar)
   {
       return hasCar 
           ? new SolidColorBrush(Colors.Red)      // Car present = RED warning
           : new SolidColorBrush(Colors.Green);   // Clear = GREEN safe
   }
   ```

4. Display format (4-way radar):
   ```
         ┌──────────────┐
         │    FRONT     │
         │  🟡 P12 +1.2s│
         │   ~80m       │
         └──────────────┘
              ↑
   [🔴L]    [🏎️]    [🟢R]  ← Simple left/right indicators
              ↓
         ┌──────────────┐
         │  🔴 P14 -0.3s│
         │   ~20m       │
         │    BACK      │
         └──────────────┘
   ```

5. Add to `AppSettings.cs`:
   ```csharp
   public class RadarSettings
   {
       public bool EnableLateralSpotter { get; set; } = true;  // ⭐ NEW: Toggle L/R indicators
       public bool EnableClassFilter { get; set; } = true;
       public bool ShowLappedCars { get; set; } = true;
       public bool ShowTimeGaps { get; set; } = true;
       public bool ShowRelativeSpeed { get; set; } = false;
       public int MaxCarsToDisplay { get; set; } = 3;
   }
   ```

**Deliverables**:
- ✅ RadarWidget XAML layout with 4-way indicators
- ✅ RadarViewModel with data binding for front/back/left/right
- ✅ Color-coded distance zones (front/back)
- ✅ Simple color indicators for left/right spotter
- ✅ Toggleable lateral spotter in settings
- ✅ Clean, minimalist MRT One integration

---

### Phase 4: Advanced Features + Settings UI (2.5-3 hours)

**Goal**: Enhanced functionality, filtering, and user configuration

**Tasks**:
1. **Lateral Spotter Toggle UI**:
   ```csharp
   // In MainWindow or settings panel
   <CheckBox Content="Enable Lateral Spotter (L/R indicators)" 
             IsChecked="{Binding Settings.EnableLateralSpotter}"
             ToolTip="Show simple left/right car presence indicators (spotter-style)"/>
   ```

2. **Class Filtering**:
   ```csharp
   // Only show cars in same class
   private bool IsSameClass(int carClass, int playerClass)
   {
       return carClass == playerClass;
   }
   ```

3. **Time-to-Collision**:
   ```csharp
   private float CalculateTTC(float distancePct, float relativeSpeed, float trackLength)
   {
       if (relativeSpeed <= 0) return float.PositiveInfinity;
       
       float distanceMeters = distancePct * trackLength;
       return distanceMeters / relativeSpeed; // seconds
   }
   ```

4. **Relative Speed Indicator**:
   ```csharp
   private string GetSpeedIndicator(float playerSpeed, float carSpeed)
   {
       float diff = carSpeed - playerSpeed;
       if (diff > 5) return "↑↑ Much Faster";
       if (diff > 1) return "↑ Faster";
       if (diff < -5) return "↓↓ Much Slower";
       if (diff < -1) return "↓ Slower";
       return "≈ Similar";
   }
   ```

5. **Lapped Traffic Warning**:
   ```csharp
   if (carLap < playerLap)
   {
       info += " [LAPPED]";
       color = Colors.Blue; // Different color for lapped cars
   }
   else if (carLap > playerLap)
   {
       info += " [LAPPING]";
       color = Colors.Purple; // Leader lapping us
   }
   ```

6. **Multi-Car Display**:
   ```csharp
   // Show count of cars in each zone
   public string FrontCount => $"{_frontCarsCount} cars ahead";
   
   // List top 3 closest
   public string FrontCarsList => string.Join("\n", 
       _frontCars.Take(3).Select(c => $"P{c.Position} +{c.Gap:F1}s"));
   ```

7. **Settings Configuration** (already added in Phase 3):
   ```csharp
   public class RadarSettings
   {
       public bool EnableLateralSpotter { get; set; } = true;  // ⭐ Toggle L/R
       public bool EnableClassFilter { get; set; } = true;
       public bool ShowLappedCars { get; set; } = true;
       public bool ShowTimeGaps { get; set; } = true;
       public bool ShowRelativeSpeed { get; set; } = false;
       public int MaxCarsToDisplay { get; set; } = 3;
   }
   ```

**Deliverables**:
- ✅ Lateral spotter toggle in UI
- ✅ Class filtering toggle
- ✅ Time-to-collision calculation
- ✅ Relative speed indicators
- ✅ Lapped traffic detection
- ✅ Multi-car display (top 3)
- ✅ User-configurable settings with persistence

---

## Testing Strategy

### Unit Tests
- Distance calculation accuracy
- Lap wrap-around handling
- Lapped car detection
- Distance zone classification
- Edge cases (0%, 100%, negative values)

### Integration Tests
- Telemetry data parsing
- ProximityCalculator with real data
- ViewModel updates with live telemetry
- Performance with 64 cars at 60 Hz

### Manual Testing
- Solo practice session (AI opponents)
- **Side-by-side racing** (test left/right spotter accuracy)
- **Passing maneuvers** (verify spotter activates/deactivates correctly)
- Multiclass race (class filtering)
- Close racing (proximity accuracy)
- Lapping/being lapped scenarios
- Track position edge cases (start/finish line)
- **Spotter toggle** (enable/disable left/right indicators in settings)

---

## Performance Considerations

### Update Rate
- Telemetry: 60 Hz (16.67ms)
- Radar Update: 30 Hz (33ms) - Update every 2nd telemetry frame
- UI Refresh: 10 Hz (100ms) - Display updates 10 times per second

### Array Processing
- 64 cars × 12 properties = 768 data points per frame
- Filter to relevant cars first (same class, on track)
- Typical race: 20-40 cars → ~240-480 data points
- Processing time: <1ms per frame (negligible overhead)

### Memory Footprint
- CarIdx arrays: ~3 KB per telemetry update
- Proximity results: ~1 KB
- UI state: ~1 KB
- Total: <5 KB additional memory

---

## Future Enhancements (Post-MVP)

### Phase 5: Visual Improvements
- Animated pulsing for very close cars
- Directional arrows (up/down for ahead/behind)
- Mini car icons with position numbers
- Track map integration (if track coordinates available)

### Phase 6: Audio Alerts
- "Car ahead" voice warning
- Beep/tone for very close proximity (<2%)
- Different tones for front vs back
- Volume based on distance

### Phase 7: Data Logging
- Proximity event recording
- Close call statistics
- Incident correlation (proximity + incidents)
- Session replay with proximity data

### Phase 8: Advanced Analytics
- Optimal racing line suggestions
- Overtaking opportunity detection
- Defensive positioning recommendations
- Traffic management coaching

---

## Success Criteria

### MVP (v0.7.0) - 4-Way Radar
- ✅ Front/back proximity detection working (specific cars with position/gap)
- ✅ Left/right spotter indicators working (presence detection)
- ✅ Color-coded distance zones accurate (front/back)
- ✅ Simple green/red spotter colors (left/right)
- ✅ Toggleable lateral spotter in settings
- ✅ Updates at 30 Hz without lag
- ✅ Works with 40+ cars in session
- ✅ No false positives/negatives

### Complete Feature (v0.7.1)
- ✅ Class filtering functional
- ✅ Lapped traffic detection
- ✅ Time-to-collision estimates
- ✅ User-configurable settings with persistence
- ✅ MRT One visual integration (clean, minimalist)
- ✅ Unit test coverage >80%

---

## Risk Mitigation

### Risk: Array Data Not Populated
**Mitigation**: Log array values on connect, verify non-zero/non-null data

### Risk: Performance Issues with 64 Cars
**Mitigation**: Profile with 60-car grids, optimize filtering, reduce update rate if needed

### Risk: Inaccurate Distance Calculation
**Mitigation**: Test on multiple track types (oval, road, street), compare with video footage

### Risk: Lap Wrap-Around Bugs
**Mitigation**: Comprehensive unit tests for 0%/100% transitions, manual testing at start/finish

---

## Timeline Summary

| Phase | Task | Hours | Cumulative |
|-------|------|-------|------------|
| 1 | Core Telemetry (+ CarLeftRight) | 2 | 2 |
| 2 | Proximity Calculator + Lateral Spotter | 3.5 | 5.5 |
| 3 | 4-Way Radar Widget UI (MRT One) | 3-4 | 8.5-9.5 |
| 4 | Advanced Features + Settings UI | 2.5-3 | 11-12.5 |
| **TOTAL** | **4-Way Radar MVP + Features** | **11-12.5** | **11-12.5 hours** |

**Target Completion**: 1-2 days of focused development  
**Testing Buffer**: +2 hours for lateral spotter edge cases  
**Total Project Time**: **13-14.5 hours**

**Time Increase Breakdown**:
- CarLeftRight telemetry setup: +0 hours (same as before)
- LateralSpotter class: +0.5 hours (simple enum wrapper)
- Left/Right UI indicators: +1 hour (XAML + binding)
- Settings toggle UI: +0.5 hours (checkbox + persistence)
- Testing lateral spotter: +0.5 hours (side-by-side scenarios)
- **Total Added**: ~2.5 hours for full 4-way radar

---

## Next Actions

1. ✅ Review and approve 4-way radar implementation plan
2. ⏳ Update IRacingTelemetryService with CarIdx arrays + CarLeftRight
3. ⏳ Build and verify SDK provides data (including lateral spotter enum)
4. ⏳ Create ProximityCalculator + LateralSpotter classes
5. ⏳ Implement 4-way Radar Widget UI (MRT One integration)
6. ⏳ Add lateral spotter toggle to settings UI
7. ⏳ Test with real iRacing sessions (focus on side-by-side racing)
8. ⏳ Deploy as v0.7.0 (4-Way Proximity Radar)

---

## Key Design Decisions

### Why Simple Color Indicators for Left/Right?

1. **SDK Limitation**: `CarLeftRight` only provides presence detection, NOT which car or how many
2. **Spotter-Style Warning**: Matches the in-game spotter behavior (simple awareness)
3. **Visual Clarity**: Red/green boxes are instantly recognizable without clutter
4. **MRT One Integration**: Small indicators outside circle won't interfere with driving data
5. **User Choice**: Toggleable feature allows users to disable if not wanted

### Front/Back vs Left/Right Data Quality

| Direction | Data Source | Information Available |
|-----------|-------------|----------------------|
| **Front/Back** | `CarIdxLapDistPct` array | ✅ Which car, position #, gap time, distance, speed |
| **Left/Right** | `CarLeftRight` enum | ⚠️ Only presence (car exists), no identification |

This explains why front/back zones show detailed car info while left/right are simple color warnings.

---

**Status**: Ready to Begin  
**Last Updated**: October 15, 2025 (Updated for 4-way radar)  
**Author**: AI Development Assistant
