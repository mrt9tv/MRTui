# Fuel Widget - Pit Strategy Enhancements (Phase 1 Complete)

## 🎯 Overview
Phase 1 pit strategy enhancements have been implemented for the Fuel Assist widget, providing comprehensive pit stop planning, color-coded urgency indicators, and configurable safety margins.

---

## ✅ Phase 1 Features Implemented

### 1. **Pit Window Countdown** (`PIT IN` field)
- **Display**: Shows laps remaining until pit stop required
- **Location**: Row 16 (Additional Info section, after PIT field)
- **Visibility**: Controlled by `FuelWidget_ShowPitWindow` setting (default: `true`)
- **Color Coding**:
  - 🟢 **Green** (>5 laps): SAFE - comfortable fuel window
  - 🟡 **Yellow** (3-5 laps): CAREFUL - pit window approaching
  - 🟠 **Orange** (2-3 laps): SOON - plan pit stop
  - 🔴 **Red** (<2 laps): URGENT - pit immediately
  - 🟢 **"OK"** (green): Can finish race without stop
  - ⚪ **"--"** (gray): Not applicable (qualifying/practice)

**Example Display**:
```
PIT IN: 12.3    (Green - safe, 12.3 laps remaining)
PIT IN: 3.5     (Yellow - careful, approaching pit window)
PIT IN: 1.8     (Red - urgent, pit NOW!)
PIT IN: OK      (Green - can finish without stop)
```

---

### 2. **Enhanced PIT Field with Color Coding**
- **Display**: Shows liters needed at next pit stop
- **Color Coding** (based on laps remaining):
  - 🔴 **Red** (<2 laps): PIT URGENT - critical fuel situation
  - 🟠 **Orange** (2-5 laps): PIT SOON - plan pit stop within 5 laps
  - 🟡 **Yellow** (>5 laps): COMFORTABLE - pit when convenient
  - 🟢 **"OK"** (green): Can finish without stop
  - ⚪ **"--"** (gray): Not applicable (qualifying/practice)

**Before** (Phase 0):
```
PIT: 35.50      (Orange, no color coding)
PIT: ---        (Can finish)
```

**After** (Phase 1):
```
PIT: 35.50      (Red - urgent, <2 laps left)
PIT: 42.30      (Orange - pit soon, 3 laps left)
PIT: 28.75      (Yellow - comfortable, 8 laps left)
PIT: OK         (Green - can finish without stop)
```

---

### 3. **Pit Stop Counter**
- **Display**: Shows total refuel count in PIT label
- **Format**: `PIT (X)` where X = number of pit stops
- **Examples**:
  - `PIT` → No pit stops yet
  - `PIT (1)` → 1 pit stop completed
  - `PIT (2)` → 2 pit stops completed

---

### 4. **User-Configurable Fuel Buffer**
- **Setting**: `FuelWidget_BufferLaps` (float, default: `1.0`)
- **Range**: 0.5 - 3.0 laps (recommended)
- **Purpose**: Safety margin for fuel calculations
- **Impact**: 
  - Higher buffer = more conservative fuel recommendations (earlier pit stops)
  - Lower buffer = more aggressive fuel strategy (later pit stops)
- **Examples**:
  - `0.5 laps`: Minimal safety margin (risky, experienced drivers only)
  - `1.0 laps`: Balanced (default, recommended for most drivers)
  - `2.0 laps`: Conservative (safe for endurance racing or unpredictable conditions)

**How it works**:
```csharp
// Fuel needed = (Race laps + Buffer laps) × Average fuel per lap + Sputtering threshold
FuelNeededToFinish = (RaceLapsRemaining + FuelBufferLaps) × AvgFuelPerLap + 0.3L
```

---

### 5. **Improved "Can Finish" Status**
- **Before**: Showed `---` (ambiguous)
- **After**: Shows `OK` in green (clear visual confirmation)
- **When Displayed**: `CanFinishWithoutStop == true` (current fuel sufficient for race)

---

## 🎮 New AppSettings Properties

### Added to `AppSettings.cs`:
```csharp
/// <summary>
/// Show pit window countdown (laps until pit required)
/// </summary>
public bool FuelWidget_ShowPitWindow { get; set; } = true;

/// <summary>
/// Buffer laps for fuel calculations (safety margin)
/// </summary>
public float FuelWidget_BufferLaps { get; set; } = 1.0f;  // Already existed!
```

---

## 📐 XAML Structure Changes

### New Row Added (Row 16):
```xml
<!-- Row 16: Pit Window (optional) -->
<RowDefinition Height="Auto" x:Name="PitWindowRow"/>
```

### New Grid Element (PIT IN):
```xml
<Grid Grid.Row="16" x:Name="PitWindowGrid" Margin="0,2,0,0">
    <TextBlock Grid.Column="0" Text="PIT IN" ... />
    <TextBlock Grid.Column="1" x:Name="PitWindowText" ... />
</Grid>
```

### Modified PIT Label (for counter):
```xml
<TextBlock x:Name="PitFuelLabel" Text="PIT" ... />
<!-- Dynamically updates to "PIT (2)" when RefuelCount > 0 -->
```

---

## 🔧 Code Changes Summary

### **FuelWidget.xaml.cs** Updates:

1. **Buffer Sync** (line ~290):
```csharp
// Sync fuel buffer from settings (allows user-configurable safety margin)
data.FuelBufferLaps = settings.FuelWidget_BufferLaps;
```

2. **Pit Window Display** (line ~520):
```csharp
var pitWindowText = FindName("PitWindowText") as TextBlock;
// Color coding: Green (>5), Yellow (3-5), Orange (2-3), Red (<2)
pitWindowText.Foreground = data.LapsRemaining > 5.0f
    ? Green : data.LapsRemaining > 3.0f ? Yellow : Orange/Red;
```

3. **Pit Stop Counter** (line ~490):
```csharp
pitFuelLabel.Text = data.RefuelCount > 0 ? $"PIT ({data.RefuelCount})" : "PIT";
```

4. **Enhanced PIT Color Coding** (line ~495):
```csharp
// Red (<2 laps), Orange (2-5 laps), Yellow (>5 laps), Green (OK)
pitFuelText.Foreground = data.LapsRemaining < 2.0f ? Red 
    : data.LapsRemaining < 5.0f ? Orange : Yellow;
```

5. **Visibility Control** (line ~260):
```csharp
var pitWindowGrid = FindName("PitWindowGrid") as FrameworkElement;
pitWindowGrid.Visibility = settings.FuelWidget_ShowPitWindow ? Visible : Collapsed;
```

---

## 🎨 Visual Examples

### Pit Strategy Section (Rows 13-16):
```
┌─────────────────────────┐
│ iR Δ:        +2.3       │ (Teal - we have more fuel than iRacing estimates)
│ PRESS:       0.42 bar   │ (Green - fuel pressure OK)
│ PIT (2):     35.50      │ (Red - URGENT, 2 stops completed, 1.5 laps left)
│ PIT IN:      1.5        │ (Red - PIT NOW! <2 laps remaining)
└─────────────────────────┘
```

### Can Finish Scenario:
```
┌─────────────────────────┐
│ iR Δ:        +1.8       │
│ PRESS:       0.41 bar   │
│ PIT:         OK         │ (Green - can finish without stop)
│ PIT IN:      OK         │ (Green - sufficient fuel to finish)
└─────────────────────────┘
```

### Qualifying Mode:
```
┌─────────────────────────┐
│ iR Δ:        --         │ (Not applicable in qualifying)
│ PRESS:       0.43 bar   │
│ PIT:         --         │ (Not applicable in qualifying)
│ PIT IN:      --         │ (Not applicable in qualifying)
└─────────────────────────┘
```

---

## 🧪 Testing Checklist

- [x] ✅ Build successful (Phase 1 compiles without errors)
- [ ] 🔲 Test pit window countdown display (verify laps remaining shown correctly)
- [ ] 🔲 Test color coding transitions (5+ → 3-5 → 2-3 → <2 laps)
- [ ] 🔲 Test pit stop counter increments (verify RefuelCount displayed)
- [ ] 🔲 Test "OK" status when CanFinishWithoutStop
- [ ] 🔲 Test buffer setting changes (0.5, 1.0, 2.0 laps)
- [ ] 🔲 Test visibility toggle (FuelWidget_ShowPitWindow on/off)
- [ ] 🔲 Test qualifying mode (verify "--" displayed)
- [ ] 🔲 Test race mode with multiple pit stops
- [ ] 🔲 Test metric/imperial unit conversion compatibility

---

## 📊 Backend Data Available (Already Implemented)

The following data is already calculated by `FuelCalculatorService` and available for Phase 2/3:

- ✅ `RefuelCount` - Total pit stops completed
- ✅ `LastRefuelAmount` - Liters added at last pit stop
- ✅ `FuelToAddAtPit` - Liters needed at next pit (rounded to 0.5L)
- ✅ `FuelNeededToFinish` - Total fuel needed to complete race
- ✅ `FuelDeltaToFinish` - Surplus (+) or deficit (-) vs. finish requirement
- ✅ `CanFinishWithoutStop` - Boolean flag
- ✅ `LapsRemaining` - Laps possible with current fuel
- ✅ `GreenFlagAverage` / `YellowFlagAverage` - Flag-specific consumption
- ✅ `MinFuelPerLap` / `MaxFuelPerLap` - Efficiency extremes
- ✅ Lap history with pit stop flags and refuel amounts

---

## 🚀 Next Steps: Phase 2 (Multi-Stint Planning)

### Proposed Phase 2 Features:
1. **Multi-Stop Strategy Calculator**
   - "1-STOP @ L25 (35L)" - optimal pit window
   - "2-STOP @ L15,L30 (30L ea.)" - multiple stop scenarios
   - "NO-STOP" - confirm can finish on current fuel

2. **Strategic Comparison**
   - Side-by-side scenarios:
     - "PIT NOW: +18s"
     - "PIT L+5: +12s" (optimal)

3. **Refuel History Display**
   - Last 3 pit stops:
     - "P1: 45L @ L12"
     - "P2: 38L @ L24"

4. **Layout Suggestions**:
   - **Option A**: Expandable section (click to expand/collapse strategy details)
   - **Option B**: Separate "Strategy" tab/panel
   - **Option C**: Dedicated strategy widget (separate window)
   - **Option D**: Tooltip/hover overlay (minimal UI space)

**User Decision Needed**: Which layout approach do you prefer for Phase 2 multi-stint planning?

---

## 🎯 Next Steps: Phase 3 (Fuel Saving Mode)

### Proposed Phase 3 Features:
1. **Fuel Save Recommendations**
   - "SAVE: -0.2L/lap" - target reduction to finish without pit
   - "TARGET: 1:45.2" - lap time needed for fuel conservation
   - "LIFT at: T3, T7" - corner-specific fuel saving advice

2. **Real-time Alerts** (Toggle-able):
   - "⚠️ PIT THIS LAP" - critical fuel warning
   - "💡 SAVE 0.3L/LAP TO FINISH" - fuel conservation target
   - "✅ FUEL SAVING SUCCESSFUL" - confirmation feedback

3. **Optimal Pit Lap Calculator**
   - Calculate minimal time loss based on track position
   - "OPTIMAL: Pit after L18 (safety car window)"

---

## 📝 Configuration Example

```json
// AppSettings.json
{
  "FuelWidget_ShowPitWindow": true,        // Show "PIT IN" field
  "FuelWidget_ShowPitFuel": true,          // Show "PIT" field
  "FuelWidget_BufferLaps": 1.0,            // Safety margin (0.5-3.0)
  "FuelWidget_ShowPressure": false,        // Hide fuel pressure (optional)
  "FuelWidget_ShowIRacingDelta": true,     // Show iRacing comparison
  "FuelWidget_ShowCanFinish": true,        // Show "TO GO" field
  "FuelWidget_Method": "Session"           // Averaging method
}
```

---

## 🎖️ Phase 1 Completion Status

| Feature | Status | Notes |
|---------|--------|-------|
| Pit Window Countdown | ✅ Complete | Color-coded, toggle-able |
| PIT Field Color Coding | ✅ Complete | 4-tier urgency system |
| Pit Stop Counter | ✅ Complete | Displays in PIT label |
| Configurable Buffer | ✅ Complete | User-adjustable safety margin |
| "Can Finish" Status | ✅ Complete | Shows "OK" in green |
| Build Verification | ✅ Complete | No compilation errors |
| Runtime Testing | ⏳ Pending | User testing required |

---

**Phase 1 Implementation Date**: October 20, 2025  
**Next Phase Planning**: Awaiting user feedback and layout preference for Phase 2
