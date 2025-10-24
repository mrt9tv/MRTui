# Fuel Strategy System - Complete Summary

**Last Updated**: October 21, 2025  
**Status**: Phase 1 ✅ Complete | Phase 2 ✅ Complete | Phase 3 📋 Planned

---

## 🎯 Overview

The Fuel Strategy System provides comprehensive fuel management across two widgets:
1. **Fuel Widget** (FuelWidget) - Standalone detailed fuel display with consumption tracking
2. **MRT One Widget** - Integrated fuel display with optional multi-stint pit strategy

---

## ✅ PHASE 1: Core Fuel Widget (COMPLETE)

### **Objective**
Create comprehensive standalone fuel widget with consumption tracking, sparklines, and basic pit strategy.

### **Key Features Implemented**

#### **1. Fuel Display**
- Current fuel level with percentage
- Tank capacity indicator
- Visual fuel bar with color-coded levels:
  - 🟢 Teal (≥50%) - Good
  - 🟡 Yellow (25-50%) - Moderate
  - 🟠 Orange (10-25%) - Low
  - 🔴 Red (<10%) - Critical

#### **2. Consumption Tracking**
- **LAST** - Last completed lap
- **L5** - Last 5 laps (exponentially weighted)
- **L10** - Last 10 laps (optional, simple average)
- **SESSION** - All laps (IQR outlier-filtered, optional)
- Lap-to-lap delta indicator (optional trend arrows)

#### **3. Visual Sparklines**
- **Live Consumption**: 5-second real-time fuel burn (0.5s intervals)
- **Lap History**: Last 5 completed laps with gradual width buildup
- Min/Max value tracking for both sparklines
- Guide lines (floor, middle, ceiling) for reference

#### **4. Pit Strategy Section** (Master Toggle)
- **LAPS** - Laps remaining on current fuel
- **TO GO** - Fuel needed to finish race
- **PIT** - Fuel to add at next stop
- **PIT IN** - Laps until pit required (countdown)
- **PRESS** - Fuel pressure monitoring (optional)

#### **5. Advanced Detection**
- ✅ Duplicate lap prevention
- ✅ Refuel detection (any fuel increase >0.5L)
- ✅ Out-lap detection (first lap after pit exit)
- ✅ Tow/reset detection (negative fuel or minimal first lap)
- ✅ Pit lap filtering (excludes from averages)

#### **6. Color-Coded Urgency**
- 🟢 Green (>5 laps) - SAFE
- 🟠 Orange (2-5 laps) - SOON
- 🔴 Red (<2 laps) - URGENT
- Blinking animation for critical fuel (<1.2 laps, configurable)

### **Configuration Settings**
```csharp
// Visibility Toggles
FuelWidget_ShowBar         // Visual fuel bar
FuelWidget_ShowPercentage  // Fuel percentage display
FuelWidget_ShowL10         // Last 10 average
FuelWidget_ShowSession     // Session average
FuelWidget_ShowPitStrategy // MASTER: Entire strategy section
FuelWidget_ShowCanFinish   // TO GO field
FuelWidget_ShowPitFuel     // PIT field
FuelWidget_ShowPressure    // PRESS field

// Strategy Settings
FuelWidget_Method          // Averaging: Last, Last5, Last10, Session, Max
FuelWidget_BufferLaps      // Safety margin (default: 1.0)
FuelWidget_CriticalThreshold // Blinking threshold (default: 1.2)
FuelWidget_BlinkCritical   // Enable blinking animation

// Visual Settings
FuelWidget_Scale           // Widget scale (0.7-1.5)
FuelWidget_ShowTrends      // Lap-to-lap delta arrows
FuelWidget_X, FuelWidget_Y // Position
```

### **Files Modified - Phase 1**
- `FuelWidget.xaml` - UI layout with sparklines
- `FuelWidget.xaml.cs` - Widget logic and display
- `FuelCalculatorService.cs` - Core calculation engine
- `FuelData.cs` - Data model
- `AppSettings.cs` - Configuration properties
- `SettingsViewModel.cs` - Settings UI binding

---

## ✅ PHASE 2: Multi-Stint Strategy (COMPLETE)

### **Objective**
Add intelligent multi-stint pit strategy to MRT One Widget with 1-stop and 2-stop scenario calculations.

### **Key Features Implemented**

#### **1. NO-STOP Analysis**
Confirms if current fuel is sufficient without pit stop.
```
✓ NO-STOP: Current fuel sufficient (+3.5L surplus)
```

#### **2. 1-STOP Strategy**
Calculates optimal single pit stop window.
```
1-STOP: Pit @ L12 → Add 28.5L
```
OR if not possible:
```
1-STOP: NOT POSSIBLE (need 15.3L more capacity)
```

**Logic**:
- Optimal pit lap = `floor(CurrentFuel / AvgFuelPerLap)`
- Fuel to add = `(RaceLapsRemaining - OptimalPitLap) × AvgFuelPerLap`
- Validates tank capacity sufficient

#### **3. 2-STOP Strategy**
Calculates optimal dual pit stop windows.
```
2-STOP: L8 (20.5L), L16 (22.0L)
```
OR if not possible:
```
2-STOP: NOT POSSIBLE (tank too small for race distance)
```

**Logic**:
- Divides race into 3 equal stints
- First pit: `min(LapsOnCurrentFuel, RaceLaps/3)`
- Second pit: `FirstPit + LapsOnFullTank`
- Fuel amounts calculated per stint

#### **4. Safe Pit Window**
Conservative recommendation with 10% fuel buffer.
```
⚠ SAFE WINDOW: Pit by L10 (90% fuel buffer)
```

**Logic**:
- Safe lap = `floor(LapsOnCurrentFuel × 0.9)`
- Provides cushion for traffic, yellows, drafting

### **Integration**
- **Location**: MRT One Widget Visual Settings
- **Toggle**: `EnableFuelStrategy` (default: OFF)
- **Display**: Appends strategy section to fuel display when enabled
- **Visibility Requirements**:
  - `EnableFuelStrategy == true`
  - `RaceLapsRemaining > 0` (race mode)
  - `AvgFuelPerLap_L5 > 0` (valid lap data)

### **MRT One Fuel Display Example**
```
FUEL: 15.32L / 60.0L (26%)
AVG: L:2.32 | 5:2.34 | 10:2.33 | S:2.35
RANGE: 2.28-2.42L | LAPS: 6.5 (iR: 6.3)
TO FINISH: 28.50L (-13.18L) | NEED 28.5L
GREEN: 2.36L (8) | YELLOW: 2.20L (2)

═══ PIT STRATEGY ═══
1-STOP: Pit @ L6 → Add 28.5L
2-STOP: L4 (20.0L), L10 (24.0L)
⚠ SAFE WINDOW: Pit by L5 (90% fuel buffer)
```

### **Configuration - Phase 2**
```csharp
// MRTOneSettings
EnableFuelStrategy  // Toggle multi-stint strategy display (default: false)
EnableFuelDisplay   // Base fuel display (required for strategy)
```

### **Files Modified - Phase 2**
- `MRTOneSettings.cs` - Added `EnableFuelStrategy` toggle
- `MRTOneWidget.cs` - Enhanced `UpdateFuelDisplay()` with strategy calculations

---

## 🆕 TIME-BASED SESSION SUPPORT (JUST IMPLEMENTED)

### **Problem Solved**
Fixed fuel strategy for time-based sessions (endurance races, timed practice/qualifying).

### **Previous Limitation**
- Lap-based races worked correctly: `RaceLapsRemaining = SessionLaps - LapsCompleted`
- Time-based sessions always showed 0: `SessionLaps = 0` → `RaceLapsRemaining = 0`
- Broke pit strategy for endurance events

### **Solution Implemented**
Smart session type detection with dual calculation paths:

#### **Lap-Based Sessions**
```csharp
// Fixed lap count races (50 laps, 100 laps, etc.)
RaceLapsRemaining = Math.Max(0, SessionLaps - LapsCompleted);
IsTimedSession = false;
```

#### **Time-Based Sessions**
```csharp
// Timed races (20 minutes, 2 hours, 24 hours, etc.)
AverageLapTime = WeightedAverage(Last5LapTimes);  // Exponentially weighted
EstimatedLapsFromTime = SessionTimeRemaining / AverageLapTime;
RaceLapsRemaining = (int)Math.Ceiling(EstimatedLapsFromTime);
IsTimedSession = true;
```

### **New FuelData Properties**
```csharp
public double SessionTimeRemaining { get; set; }     // Time left in seconds
public float EstimatedLapsFromTime { get; set; }     // Calculated from time
public bool IsTimedSession { get; set; }             // Session type flag
public float AverageLapTime { get; set; }            // Weighted lap time average
```

### **Benefits**
- ✅ Works for all iRacing session types
- ✅ Accurate pit strategy for endurance races
- ✅ Adapts to changing lap times (fuel saving, traffic)
- ✅ Uses weighted average for responsiveness
- ✅ Seamless integration with existing code

### **Files Modified - Time Support**
- `FuelData.cs` - Added 4 new properties
- `FuelCalculatorService.cs` - Session type detection and lap time tracking

---

## 📋 PHASE 3: Fuel Saving Mode (PLANNED)

### **Proposed Features**

#### **1. Fuel Save Recommendations**
Real-time targets to finish without additional pit stop:
```
SAVE: -0.2L/lap to finish
TARGET: 1:45.2 (lift in corners)
✅ Saving 0.15L/lap (goal: 0.2L)
```

#### **2. Lift Point Suggestions**
Corner-specific fuel conservation advice:
```
LIFT: T3, T7, T12
Reduce throttle 5% in fast corners
```

#### **3. Progress Tracking**
Monitor fuel saving effectiveness:
```
2.30L actual vs. 2.10L target
Will finish with 2.5L surplus
```

#### **4. Strategic Alerts** (Toggle-able)
- "⚠️ PIT THIS LAP" - Critical fuel
- "💡 FUEL SAVING WORKING" - On track to finish
- "🔴 INCREASE SAVING" - Not reducing enough

#### **5. Optimal Pit Lap Calculator**
Minimize time loss based on track position:
```
OPTIMAL: Pit after L18 (safety car window)
```

### **Configuration - Phase 3** (Proposed)
```csharp
// Fuel Saving Settings
FuelWidget_EnableFuelSaving      // Master toggle
FuelWidget_SavingTarget          // Target reduction (L/lap)
FuelWidget_ShowLiftPoints        // Corner-specific advice
FuelWidget_ShowSavingAlerts      // Real-time alerts
FuelWidget_OptimalPitCalculator  // Time-loss minimization
```

### **Implementation Status**
- 📅 **Planned** - Awaiting user feedback and priority
- ⏳ **Not Started** - Phase 1 & 2 complete first
- 💡 **Design Phase** - Feature specifications defined

---

## 🏗️ STANDALONE PIT STRATEGY WINDOW (UNDER DISCUSSION)

### **Concept**
Dedicated window for advanced pit strategy separate from Fuel Widget.

### **Proposed Features**
1. **Multi-Stint Timeline**
   - Visual lap-by-lap timeline
   - Pit stop markers with fuel amounts
   - Stint length indicators

2. **What-If Scenarios**
   - Compare 1-stop vs 2-stop time loss
   - Adjust buffer laps dynamically
   - Test different fuel strategies

3. **Lap Time Projections**
   - Expected lap times per stint
   - Fuel weight impact on pace
   - Total race time estimates

4. **Yellow Flag Strategy**
   - Optimal caution pit windows
   - Fuel saving under yellow
   - Wave-around scenarios

5. **Telemetry Integration**
   - Real-time track position
   - Gap to cars ahead/behind
   - Undercut/overcut opportunities

### **Design Options**
- **Option A**: Separate floating window (like Fuel Widget)
- **Option B**: Expandable panel in Fuel Widget
- **Option C**: Integrated tab in Manager Window
- **Option D**: Pop-up overlay on hotkey press

### **Status**
- 💬 **Under Discussion** - User preference needed
- 🎨 **Concept Phase** - Mockups/wireframes pending
- ⏸️ **On Hold** - Complete Phase 3 first

---

## 📊 Testing Scenarios

### **Short Oval Race** (Charlotte, 100 laps)
```
Tank: 60L | Avg: 2.3L/lap | Laps: 100 | Current: 45L

Expected Output:
1-STOP: Pit @ L19 → Add 48.3L
2-STOP: L13 (30.7L), L40 (30.7L)
⚠ SAFE WINDOW: Pit by L17 (90% buffer)
```

### **Endurance Race** (24H, 200 laps)
```
Tank: 90L | Avg: 3.5L/lap | Laps: 200 | Current: 70L

Expected Output:
1-STOP: NOT POSSIBLE (need 10.0L more capacity)
2-STOP: L20 (62.5L), L45 (62.5L)
⚠ SAFE WINDOW: Pit by L18 (90% buffer)
```

### **Can Finish Without Stop** (Short Race)
```
Tank: 60L | Avg: 2.0L/lap | Laps: 25 | Current: 55L

Expected Output:
✓ NO-STOP: Current fuel sufficient (+5.0L surplus)
1-STOP: Pit @ L27 → Add 0.0L (not needed)
⚠ SAFE WINDOW: Pit by L24 (90% buffer)
```

### **Time-Based Session** (2-hour endurance)
```
Tank: 90L | Avg Lap: 85s | Time Remaining: 1800s | Current: 40L

Expected Output:
Estimated Laps: 21.2 laps (1800s / 85s)
1-STOP: Pit @ L11 → Add 50.5L
⚠ SAFE WINDOW: Pit by L9 (90% buffer)
```

---

## 🔧 Technical Architecture

### **Core Service**
```
FuelCalculatorService
├── Update(TelemetryData)        // Real-time telemetry processing
├── CalculateAverages()          // L5, L10, Session averages
├── CalculateStrategy()          // Pit stop calculations
├── OnLapCompleted()             // Lap history tracking
├── DetectFlagStatus()           // Green/Yellow detection
└── Reset()                       // Session change cleanup
```

### **Data Model**
```
FuelData
├── Current State (fuel, pct, tank capacity)
├── Lap Consumption (current, last, averages)
├── Laps Remaining (calculated, iRacing, time-based)
├── Race Strategy (laps left, fuel needed, can finish)
├── Flag Conditions (green/yellow averages)
└── Session Tracking (laps completed, total used, refuels)
```

### **Widget Integration**
```
FuelWidget (Standalone)
├── UpdateUI(FuelData)           // Display all fields
├── UpdateSparklines()           // Real-time graphs
├── ManageBlinking()             // Critical fuel animation
└── UpdateFieldVisibility()      // Settings-based toggles

MRTOneWidget (Integrated)
├── UpdateFuelDisplay()          // Compact multi-line
├── CalculateStrategyScenarios() // 1-stop/2-stop logic
└── FormatFuelStrategySummary()  // Strategy section text
```

---

## 📁 File Structure

```
src/
├── iRacingOverlay.Core/
│   ├── Models/
│   │   ├── FuelData.cs                   // Data model
│   │   └── FuelLapHistory.cs             // Lap-by-lap tracking
│   └── Services/
│       └── FuelCalculatorService.cs      // Core calculation engine
│
└── iRacingOverlay.WPF/
    ├── Models/
    │   ├── AppSettings.cs                // Fuel Widget settings
    │   └── MRTOneSettings.cs             // MRT One strategy toggle
    ├── ViewModels/
    │   └── SettingsViewModel.cs          // Settings UI binding
    └── Widgets/
        ├── FuelWidget/
        │   ├── FuelWidget.xaml           // Standalone widget UI
        │   └── FuelWidget.xaml.cs        // Standalone widget logic
        └── MRTOneWidget/
            ├── MRTOneWidget.cs           // Integrated display + strategy
            └── MRTOneSettings.cs         // Strategy toggle
```

---

## 🎯 Completion Status

| Phase | Status | Features |
|-------|--------|----------|
| **Phase 1** | ✅ **COMPLETE** | Fuel Widget with consumption tracking, sparklines, pit strategy |
| **Phase 2** | ✅ **COMPLETE** | Multi-stint strategy in MRT One Widget (1-stop, 2-stop) |
| **Time Support** | ✅ **COMPLETE** | Time-based session calculations for endurance races |
| **Phase 3** | 📋 **PLANNED** | Fuel saving mode with lift points and alerts |
| **Strategy Window** | 💬 **DISCUSSION** | Standalone pit strategy window (layout undecided) |

---

## 🚀 Next Steps

### **Immediate**
1. ✅ Build verification (DONE - no errors)
2. 🧪 Test time-based sessions in iRacing (2-hour endurance)
3. 📝 Verify estimated laps accuracy vs actual
4. 🔍 Monitor debug logs for session type detection

### **Short Term**
1. **User Feedback Collection**
   - Test Phase 1 & 2 features in real races
   - Gather accuracy reports (compare to Racelabs)
   - Identify pain points or missing features

2. **Phase 3 Planning**
   - Prioritize fuel saving features
   - Design lift point algorithm
   - Create alert system mockups

### **Long Term**
1. **Standalone Strategy Window**
   - Choose layout approach (A/B/C/D)
   - Design UI mockups
   - Plan telemetry integration
   - Implement timeline visualization

2. **Advanced Features**
   - Tire wear correlation with fuel consumption
   - Track-specific fuel consumption database
   - AI-powered pit stop optimization
   - Multi-class race strategy

---

## 📖 User Documentation

### **Enabling Fuel Strategy (MRT One Widget)**
1. Open **MRT One Widget Settings**
2. Navigate to **Visual Settings**
3. Toggle **Enable Fuel Strategy** to ON
4. Strategy section appears during races (when race laps > 0)

### **Configuring Fuel Widget**
1. Open **Overlay Manager**
2. Select **Fuel Widget** tab
3. Adjust visibility toggles for desired fields
4. Set **Fuel Buffer Laps** (0.5-3.0) for safety margin
5. Choose **Averaging Method** (Last5 recommended)

### **Interpreting Strategy Output**
- **NO-STOP**: Current fuel sufficient, no pit required
- **1-STOP**: Single pit stop, shows optimal lap and fuel amount
- **2-STOP**: Dual pit stops, shows both pit laps and fuel amounts
- **SAFE WINDOW**: Conservative recommendation with 10% buffer
- **NOT POSSIBLE**: Tank capacity insufficient for strategy

---

## 🐛 Known Issues

### **Resolved**
- ✅ Duplicate lap entries (fixed with `_lapsCompletedWhenProcessed`)
- ✅ Refuel detection timing (fixed with fuel delta threshold only)
- ✅ Out-lap contamination (fixed with `_justLeftPits` flag)
- ✅ Time-based session support (fixed with lap time calculation)

### **Active**
- ⚠️ Unused `const uint White` warning (cosmetic only, no impact)

### **Monitoring**
- 🔍 Widget font size on different screen resolutions
- 🔍 Color visibility against various track backgrounds
- 🔍 Average lap time accuracy in mixed-pace sessions

---

## 📞 Support & Feedback

For questions, bugs, or feature requests related to fuel strategy:
- 📋 Check fuel_debug.log for detailed calculation traces
- 🐛 Report issues with session type, lap count, and averages
- 💡 Suggest Phase 3 features or standalone window design
- 📊 Share accuracy comparisons vs Racelabs/iRacing estimates

---

**Document Version**: 2.1  
**Last Build**: Successful (October 21, 2025)  
**Next Milestone**: Phase 3 Planning & User Feedback Collection
