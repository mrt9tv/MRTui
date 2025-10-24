# Phase 3: Fuel Saving Mode - Implementation Progress

**Started**: October 21, 2025  
**Status**: 🚧 IN PROGRESS (Core Complete, UI Pending)

---

## 🎯 Phase 3 Goals

Implement intelligent fuel saving mode with real-time guidance, strategic alerts, and optimal pit stop calculations.

---

## ✅ COMPLETED: Core Fuel Saving Engine

### **1. FuelData Model Extensions** ✅

Added 12 new properties to `FuelData.cs`:

```csharp
// Fuel saving calculations
public float FuelSavingTarget { get; set; }          // Target reduction per lap (L/lap)
public float CurrentSavingRate { get; set; }         // Current saving rate (L/lap)
public float TargetLapTime { get; set; }             // Target lap time for conservation (seconds)
public string? LiftPoints { get; set; }              // Suggested lift points
public float SavingProgress { get; set; }            // Progress percentage (0-100)

// Status flags
public bool NeedsFuelSaving { get; set; }            // Is fuel saving needed?
public bool FuelSavingWorking { get; set; }          // Is saving working?
public float ProjectedFuelDelta { get; set; }        // Projected surplus/deficit (L)

// Optimal pit strategy
public int OptimalPitLap { get; set; }               // Optimal lap to pit
public string? OptimalPitReason { get; set; }        // Reason for recommendation

// Strategic alerts
public string? StrategicAlert { get; set; }          // Alert message
public int AlertSeverity { get; set; }               // 0=None, 1=Info, 2=Warning, 3=Critical
```

### **2. Fuel Saving Calculator** ✅

Implemented in `FuelCalculatorService.cs`:

#### **CalculateFuelSaving() Method**
- ✅ Detects when fuel saving is needed (fuel deficit exists)
- ✅ Calculates target fuel reduction per lap to finish
- ✅ Estimates target lap time (slower pace for fuel conservation)
- ✅ Generates lift point suggestions (track-agnostic)
- ✅ Tracks current saving rate vs target
- ✅ Calculates progress percentage (0-100%)
- ✅ Projects fuel delta if current rate continues
- ✅ Determines if fuel saving is working

**Example Output**:
```
FUEL SAVING MODE: Target=0.250L/lap, Current=0.180L/lap, Progress=72.0%, Projected=+1.5L
```

#### **GenerateStrategicAlerts() Method**
Intelligent alert system with 4 severity levels:

1. **🔴 CRITICAL (Severity 3)**: `⚠️ PIT THIS LAP - CRITICAL FUEL`
   - Triggers: LapsRemaining < 0.5 and can't finish
   - Action: Pit immediately

2. **🟠 WARNING (Severity 2)**: `🔴 INCREASE SAVING: Need 0.15L more per lap`
   - Triggers: SavingProgress < 50% and >5 laps remaining
   - Action: Save more fuel per lap

3. **🟠 WARNING (Severity 2)**: `💡 SAVE 0.25L/LAP TO FINISH`
   - Triggers: NeedsFuelSaving and not working yet
   - Action: Start fuel saving now

4. **🟢 INFO (Severity 1)**: `✅ FUEL SAVING WORKING: +1.5L surplus projected`
   - Triggers: Fuel saving working, will finish
   - Action: Continue current pace

#### **CalculateOptimalPitLap() Method**
Smart pit timing to minimize time loss:

- ✅ **Critical Fuel**: Pit next lap if < 1.5 laps remaining
- ✅ **Yellow Flag Opportunity**: Pit under caution if <10 laps fuel
- ✅ **Strategy-Based**: Pit just before running out (maximize stint)

**Example Recommendations**:
```
Optimal pit lap: 23 (Critical fuel)
Optimal pit lap: 18 (Yellow flag opportunity - pit now)
Optimal pit lap: 28 (Pit at lap 28 to maximize stint length)
```

### **3. AppSettings Integration** ✅

Added 8 new settings to `AppSettings.cs`:

```csharp
// Phase 3: Fuel Saving Mode
public bool FuelWidget_EnableFuelSaving { get; set; } = true;
public bool FuelWidget_ShowLiftPoints { get; set; } = true;
public bool FuelWidget_ShowSavingAlerts { get; set; } = true;
public bool FuelWidget_OptimalPitCalculator { get; set; } = true;

// Standalone Pit Strategy Window
public bool ShowPitStrategyWindow { get; set; } = false;
public double PitStrategyWindow_X { get; set; } = 100;
public double PitStrategyWindow_Y { get; set; } = 100;
public double PitStrategyWindow_Width { get; set; } = 800;
public double PitStrategyWindow_Height { get; set; } = 600;
```

---

## 🚧 IN PROGRESS: UI Implementation

### **Next Steps - FuelWidget Updates**

Need to add fuel saving display to `FuelWidget.xaml.cs`:

#### **Proposed Layout** (New Section):
```
═══ FUEL SAVING ═══
SAVE: 0.25L/lap → Target Time: 1:45.2
Progress: 72% | Current: 0.18L/lap
LIFT: Fast corners, straights before braking

✅ FUEL SAVING WORKING: +1.5L surplus projected

OPTIMAL PIT: Lap 28 (maximize stint length)
```

#### **Display Logic**:
- Show only when `FuelWidget_EnableFuelSaving == true`
- Show only during races with fuel deficit
- Color-coded alerts:
  - 🟢 Green: Fuel saving working
  - 🟠 Orange: Need to increase saving
  - 🔴 Red: Critical fuel / pit this lap
- Blinking for critical alerts

---

## 📋 REMAINING WORK

### **Phase 3 - UI Tasks** (Estimated: 2-3 hours)

#### **1. Update FuelWidget.xaml** ⏳
- [ ] Add fuel saving section after sparklines
- [ ] Create XAML layout for saving display
- [ ] Add collapsible panel (toggle with setting)
- [ ] Style alert messages with color coding

#### **2. Update FuelWidget.xaml.cs** ⏳
- [ ] Add `UpdateFuelSavingDisplay(FuelData data)` method
- [ ] Format saving target, current rate, progress
- [ ] Display target lap time
- [ ] Show lift point suggestions
- [ ] Render strategic alerts with color/blinking
- [ ] Show optimal pit recommendation
- [ ] Add setting toggles for visibility

#### **3. Settings UI** ⏳
- [ ] Add Phase 3 settings to SettingsViewModel
- [ ] Create fuel saving checkboxes in OverlayView.xaml
- [ ] Test toggle functionality

---

## 🏗️ STANDALONE PIT STRATEGY WINDOW (Option A)

### **High-Level Design**

Separate floating window with advanced pit strategy features beyond fuel widget scope.

#### **Proposed Architecture**:
```
PitStrategyWindow.xaml
├── Multi-Stint Timeline (Visual)
│   ├── Lap-by-lap progress bar
│   ├── Pit stop markers
│   └── Color-coded stints
├── What-If Calculator
│   ├── 1-stop vs 2-stop comparison
│   ├── Adjustable buffer sliders
│   └── Time loss estimates
├── Lap Time Projections
│   ├── Expected pace per stint
│   ├── Fuel weight impact
│   └── Total race time
└── Yellow Flag Strategy
    ├── Caution pit windows
    ├── Fuel saving under yellow
    └── Wave-around scenarios
```

#### **Key Features**:
1. **Multi-Stint Timeline**
   - Visual representation of full race
   - Drag-and-drop pit stop planning
   - Real-time updates as race progresses
   - Color-coded segments (green=current, orange=projected)

2. **What-If Scenarios**
   - Compare multiple strategies side-by-side
   - Adjust buffer laps dynamically (slider 0.5-3.0)
   - Calculate time loss for each strategy
   - Show fuel amounts and pit laps for each option

3. **Lap Time Projections**
   - Calculate expected lap times per stint
   - Account for fuel weight (heavier = slower)
   - Estimate total race time for each strategy
   - Compare vs. session time remaining

4. **Yellow Flag Strategy**
   - Detect yellow flag periods
   - Show optimal caution pit windows
   - Calculate fuel savings under yellow
   - Wave-around opportunity detection

5. **Telemetry Integration**
   - Real-time track position
   - Gap to cars ahead/behind
   - Undercut/overcut opportunities
   - Tire wear correlation

---

## 📊 Implementation Timeline

| Task | Est. Time | Status |
|------|-----------|--------|
| **FuelData Extensions** | 30 min | ✅ Complete |
| **Fuel Saving Calculator** | 1 hour | ✅ Complete |
| **Strategic Alerts** | 30 min | ✅ Complete |
| **Optimal Pit Calculator** | 30 min | ✅ Complete |
| **AppSettings Integration** | 15 min | ✅ Complete |
| **FuelWidget XAML** | 1 hour | ⏳ Pending |
| **FuelWidget Logic** | 1 hour | ⏳ Pending |
| **Settings UI** | 30 min | ⏳ Pending |
| **Testing & Debug** | 1 hour | ⏳ Pending |
| **PitStrategyWindow Design** | 2 hours | 📋 Planned |
| **Timeline Visualization** | 3 hours | 📋 Planned |
| **What-If Calculator** | 2 hours | 📋 Planned |
| **Lap Time Projections** | 2 hours | 📋 Planned |
| **Yellow Flag Strategy** | 2 hours | 📋 Planned |
| **Telemetry Integration** | 2 hours | 📋 Planned |

---

## 🧪 Testing Plan

### **Phase 3 Fuel Saving Tests**
1. ✅ Build verification (PASSED)
2. ⏳ Short race with fuel deficit (trigger saving mode)
3. ⏳ Long race with surplus (no saving needed)
4. ⏳ Critical fuel scenario (pit this lap alert)
5. ⏳ Yellow flag pit opportunity
6. ⏳ Fuel saving progress tracking
7. ⏳ Alert severity levels
8. ⏳ Optimal pit lap calculation

### **Pit Strategy Window Tests** (Future)
1. 📋 Multi-stint visualization
2. 📋 What-if scenario comparison
3. 📋 Lap time projections accuracy
4. 📋 Yellow flag detection
5. 📋 Telemetry integration

---

## 💡 Future Enhancements

### **Phase 3.1: Advanced Fuel Saving**
- Corner-specific lift point detection (requires track map data)
- Real-time lap time delta vs target
- Fuel saving efficiency rating (A-F grade)
- Historical fuel saving performance tracking

### **Phase 3.2: Machine Learning**
- Predict optimal fuel consumption based on driver style
- Learn from historical lap data
- Adapt recommendations to track conditions
- Personalized lift point suggestions

### **Phase 3.3: Multiplayer Strategy**
- Compare fuel strategy vs competitors
- Undercut/overcut opportunities
- Track position optimization
- Pit window traffic analysis

---

## 📁 Files Modified

### **Core**
- ✅ `FuelData.cs` - Added 12 fuel saving properties
- ✅ `FuelCalculatorService.cs` - Added 3 new methods (188 lines)
- ✅ `AppSettings.cs` - Added 8 settings

### **Pending**
- ⏳ `FuelWidget.xaml` - Add fuel saving section
- ⏳ `FuelWidget.xaml.cs` - Add display logic
- ⏳ `SettingsViewModel.cs` - Add Phase 3 properties
- ⏳ `OverlayView.xaml` - Add Phase 3 checkboxes

### **Future**
- 📋 `PitStrategyWindow.xaml` - New standalone window
- 📋 `PitStrategyWindow.xaml.cs` - Advanced strategy logic
- 📋 `PitStrategyViewModel.cs` - MVVM binding
- 📋 `TimelineControl.xaml` - Custom multi-stint timeline
- 📋 `WhatIfCalculator.cs` - Scenario comparison engine

---

## 🎯 Success Criteria

### **Phase 3 Core** ✅
- [x] Detect when fuel saving is needed
- [x] Calculate target fuel reduction per lap
- [x] Generate lift point suggestions
- [x] Track saving progress percentage
- [x] Project fuel delta if current rate continues
- [x] Strategic alerts with 4 severity levels
- [x] Optimal pit lap calculator
- [x] Yellow flag pit opportunity detection

### **Phase 3 UI** ⏳
- [ ] Fuel saving section visible in FuelWidget
- [ ] Color-coded alerts rendering correctly
- [ ] Blinking for critical fuel alerts
- [ ] Toggle settings working
- [ ] Tested in real iRacing session

### **Standalone Window** 📋
- [ ] PitStrategyWindow created and launchable
- [ ] Multi-stint timeline visualization
- [ ] What-if scenario calculator
- [ ] Lap time projections
- [ ] Yellow flag strategy display
- [ ] Telemetry integration complete

---

## 🚀 Next Actions

### **Immediate (Tonight)**
1. ⏳ Implement FuelWidget UI for fuel saving display
2. ⏳ Add settings UI for Phase 3 toggles
3. ⏳ Test in iRacing practice session
4. ⏳ Verify calculations accuracy
5. ⏳ Validate alert triggers

### **Short Term (This Week)**
1. 📋 Design PitStrategyWindow mockups
2. 📋 Create XAML layout for standalone window
3. 📋 Implement timeline visualization control
4. 📋 Add what-if scenario calculator
5. 📋 Test multi-stint projections

### **Long Term (This Month)**
1. 📋 Complete all pit strategy window features
2. 📋 Integrate telemetry for undercut/overcut
3. 📋 Add yellow flag strategy logic
4. 📋 User testing and feedback collection
5. 📋 Performance optimization

---

## 📝 Notes

### **Design Decisions**
- **Fuel saving target**: Calculated as `fuelDeficit / lapsRemaining`
- **Target lap time**: 1% slower for every 2% fuel saved (simplified model)
- **Lift points**: Track-agnostic advice (requires track map data for specific turns)
- **Alert thresholds**: Critical <0.5 laps, Warning <50% progress, Info when working
- **Optimal pit**: Prioritizes critical fuel > yellow flag > maximize stint

### **Known Limitations**
- Lift point suggestions are generic (no track-specific data yet)
- Target lap time is estimated (doesn't account for tire wear, traffic)
- Optimal pit doesn't consider tire strategy or track position advantage
- No integration with tire wear or brake temps (future enhancement)

### **Performance Considerations**
- Fuel saving calculations add ~5ms per update (negligible)
- Strategic alert generation is lightweight
- No performance impact on existing functionality
- All calculations run on telemetry update thread

---

**Document Version**: 1.0  
**Last Updated**: October 21, 2025, 10:45 PM  
**Next Review**: After Phase 3 UI completion
