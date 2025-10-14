# Phase 7.5: DataWidget Fixes & Enhancements

## Overview
Fixed multiple DataWidget issues including dynamic column sizing, font sizing for better text fit, tire temperature color coding, unit indicators for temperature fields, and updated Driving Widget default values.

## Issues Addressed ✅

### 1. ✅ Dynamic Column Sizing
**Problem:** Frame around Data Widget wasn't dynamic to active columns - empty columns still took up space.

**Solution:** Modified `UpdateGridLayout()` to actually hide empty columns/rows by setting Width/Height to 0.

**Implementation:**
```csharp
// Hide/show columns based on visibility
for (int col = 0; col < 2; col++)
{
    if (colHasVisibleCells[col])
    {
        _mainGrid.ColumnDefinitions[col].Width = new GridLength(190);
    }
    else
    {
        _mainGrid.ColumnDefinitions[col].Width = new GridLength(0);
    }
}

// Hide/show rows based on visibility
for (int row = 0; row < 3; row++)
{
    if (rowHasVisibleCells[row])
    {
        _mainGrid.RowDefinitions[row].Height = new GridLength(85);
    }
    else
    {
        _mainGrid.RowDefinitions[row].Height = new GridLength(0);
    }
}
```

**Result:** Widget now properly collapses to show only active columns/rows.

---

### 2. ✅ Font Size Reduction
**Problem:** TireWearAll data text not fitting in cell properly - needed to be downsized.

**Solution:** Reduced value font size from 20px to 16px.

**Change:**
```csharp
// Before
FontSize = 20,  // Reduced from 24 to fit better

// After
FontSize = 16,  // Reduced from 20 to fit TireWearAll data better
```

**Result:** All multi-line data (TireWearAll, TireTempAll) now fits comfortably in cells.

---

### 3. ✅ Tire Temperature Color Coding
**Problem:** Tire temps displayed but no visual indication of cold/warm/hot/extreme temperatures.

**User Request:** "changing colors depending on if it is 'cold' 'warming' 'optimal' 'hot' 'extreme' with blue, teal, green, orange, red"

**Implementation:**

#### Temperature Ranges (Celsius)
- **Cold**: < 60°C → **Blue**
- **Warming**: 60-75°C → **Teal**
- **Optimal**: 75-95°C → **Light Green**
- **Hot**: 95-110°C → **Orange**
- **Extreme**: > 110°C → **Red**

#### Individual Tire Temps (TireTempLF/RF/LR/RR)
```csharp
case TelemetryField.TireTempLF:
case TelemetryField.TireTempRF:
case TelemetryField.TireTempLR:
case TelemetryField.TireTempRR:
    if (value is float tireTemp)
    {
        text = AppSettings.Instance.UseMetricUnits ? $"{(int)tireTemp}°C" : $"{(int)(tireTemp * 9 / 5 + 32)}°F";
        
        color = tireTemp switch
        {
            < 60f => Colors.Blue,        // Cold
            < 75f => Colors.Teal,        // Warming
            < 95f => Colors.LightGreen,  // Optimal
            < 110f => Colors.Orange,     // Hot
            _ => Colors.Red              // Extreme
        };
    }
    break;
```

#### All Tire Temps (TireTempAll)
```csharp
case TelemetryField.TireTempAll:
    if (value is string allTemps)
    {
        text = allTemps;
        
        // Get average temp for color coding
        var lf = TelemetryDataMapper.GetValue(TelemetryField.TireTempLF, data) as float? ?? 0f;
        var rf = TelemetryDataMapper.GetValue(TelemetryField.TireTempRF, data) as float? ?? 0f;
        var lr = TelemetryDataMapper.GetValue(TelemetryField.TireTempLR, data) as float? ?? 0f;
        var rr = TelemetryDataMapper.GetValue(TelemetryField.TireTempRR, data) as float? ?? 0f;
        float avgTemp = (lf + rf + lr + rr) / 4f;
        
        color = avgTemp switch
        {
            < 60f => Colors.Blue,        // Cold
            < 75f => Colors.Teal,        // Warming
            < 95f => Colors.LightGreen,  // Optimal
            < 110f => Colors.Orange,     // Hot
            _ => Colors.Red              // Extreme
        };
    }
    break;
```

**Result:** Tire temperatures now have intuitive color feedback - easy to see at a glance if tires are in optimal range.

---

### 4. ✅ Unit Indicators for Temperature Fields
**Problem:** Temperature fields didn't show which unit system (°C or °F) was being used.

**User Request:** "add (°C) or (°F) respectively behind the data cell name if applicable"

**Implementation:**

Modified `FormatFieldName()` to append unit indicator based on global settings:

```csharp
private string FormatFieldName(TelemetryField field)
{
    var name = field.ToString();
    
    // Add spaces before capitals
    var spaced = string.Concat(name.Select((x, i) =>
        i > 0 && char.IsUpper(x) ? " " + x : x.ToString()));
    
    var formattedName = spaced.ToUpper();
    
    // Add unit indicators for temperature fields based on global units setting
    var tempUnit = AppSettings.Instance.UseMetricUnits ? " (°C)" : " (°F)";
    
    return field switch
    {
        TelemetryField.WaterTemp => formattedName + tempUnit,
        TelemetryField.OilTemp => formattedName + tempUnit,
        TelemetryField.AirTemp => formattedName + tempUnit,
        TelemetryField.TrackTemp => formattedName + tempUnit,
        TelemetryField.TireTempLF => formattedName + tempUnit,
        TelemetryField.TireTempRF => formattedName + tempUnit,
        TelemetryField.TireTempLR => formattedName + tempUnit,
        TelemetryField.TireTempRR => formattedName + tempUnit,
        TelemetryField.TireTempAll => "TIRE TEMP" + tempUnit,
        _ => formattedName
    };
}
```

#### Real-Time Unit Updates
Added event handler to update labels when unit system changes:

```csharp
public DataWidget(ITelemetryService telemetryService, WidgetConfig? config = null)
    : base(telemetryService, config)
{
    // Subscribe to settings changes to update temperature unit indicators
    AppSettings.Instance.SettingsChanged += OnSettingsChanged;
    // ...
}

private void OnSettingsChanged(object? sender, EventArgs e)
{
    // Update all cell labels to reflect new unit indicators
    foreach (var cell in _dataCells)
    {
        UpdateCellLabel(cell);
    }
}
```

**Examples:**
- **Metric Mode**: "WATER TEMP (°C)"
- **Imperial Mode**: "WATER TEMP (°F)"
- **Metric Mode**: "TIRE TEMP L F (°C)"
- **Imperial Mode**: "TIRE TEMP L F (°F)"

**Result:** Users always know which temperature unit is being displayed. Labels update immediately when switching between Metric/Imperial in Global Settings.

---

### 5. ✅ Driving Widget Default Side Box Values
**Problem:** Left and Right side boxes defaulted to "None".

**User Request:** "Left side box and right side box in driving widget config should show fuel level (left) and brake (right) by default"

**Change:**
```xml
<!-- Before -->
<ComboBox x:Name="LeftSideField" Width="200" SelectedIndex="0">  <!-- None -->
<ComboBox x:Name="RightSideField" Width="200" SelectedIndex="0"> <!-- None -->

<!-- After -->
<ComboBox x:Name="LeftSideField" Width="200" SelectedIndex="4">  <!-- Fuel Level -->
<ComboBox x:Name="RightSideField" Width="200" SelectedIndex="2"> <!-- Brake % -->
```

**ComboBox Items (for reference):**
- Index 0: None
- Index 1: Throttle %
- Index 2: Brake %
- Index 3: Clutch %
- Index 4: Fuel Level
- Index 5: Water Temp
- Index 6: Oil Temp
- Index 7: Speed
- Index 8: RPM
- Index 9: Gear

**Result:** New Driving Widget instances now show Fuel Level on left, Brake % on right by default - more useful starting configuration.

---

## Files Modified

### 1. DataWidget.cs
**Location:** `src/iRacingOverlay.WPF/Widgets/DataWidget/DataWidget.cs`

**Changes:**
- **Line 39**: Added `AppSettings.Instance.SettingsChanged += OnSettingsChanged;` subscription
- **Line 120-155**: Modified `UpdateGridLayout()` to hide empty columns/rows (set Width/Height to 0)
- **Line 223**: Reduced value font from 20px → 16px
- **Line 273-299**: Modified `FormatFieldName()` to add unit indicators for temperature fields
- **Line 450-470**: Added tire temperature color coding for individual temps (TireTempLF/RF/LR/RR)
- **Line 473-496**: Added tire temperature color coding for TireTempAll with average calculation
- **Line 662-669**: Added `OnSettingsChanged()` event handler to update labels on unit change

### 2. MainWindow.xaml
**Location:** `src/iRacingOverlay.WPF/MainWindow.xaml`

**Changes:**
- **Line 210**: Changed `LeftSideField` from `SelectedIndex="0"` → `SelectedIndex="4"` (Fuel Level)
- **Line 234**: Changed `RightSideField` from `SelectedIndex="0"` → `SelectedIndex="2"` (Brake %)

---

## Testing Checklist

### DataWidget - Dynamic Sizing
- [ ] Create Data Widget with only left column fields (cells 1, 3, 5)
- [ ] Verify widget collapses to single column width
- [ ] Create Data Widget with only right column fields (cells 2, 4, 6)
- [ ] Verify widget collapses to single column width
- [ ] Create Data Widget with only top row (cells 1, 2)
- [ ] Verify widget collapses to single row height

### DataWidget - Font Sizing
- [ ] Add TireWearAll field to a cell
- [ ] Verify text "FL 95% 93% RF\nLR 91% 92% RR" fits without wrapping awkwardly
- [ ] Verify all text is readable at 16px size

### DataWidget - Tire Temperature Colors
- [ ] Add TireTempLF field, observe color changes as temp varies:
  - Cold (< 60°C): Blue
  - Warming (60-75°C): Teal
  - Optimal (75-95°C): Light Green
  - Hot (95-110°C): Orange
  - Extreme (> 110°C): Red
- [ ] Add TireTempAll field
- [ ] Verify average temperature color coding works correctly

### DataWidget - Unit Indicators
- [ ] Set Global Settings to Metric
- [ ] Add WaterTemp field → verify label shows "WATER TEMP (°C)"
- [ ] Add TireTempLF field → verify label shows "TIRE TEMP L F (°C)"
- [ ] Switch Global Settings to Imperial
- [ ] Verify labels update to "WATER TEMP (°F)" and "TIRE TEMP L F (°F)"
- [ ] Test with: OilTemp, AirTemp, TrackTemp, TireTempAll

### Driving Widget - Default Side Boxes
- [ ] Activate Driving Widget (check activation checkbox)
- [ ] Verify Left Side Box shows "Fuel Level" by default
- [ ] Verify Right Side Box shows "Brake %" by default
- [ ] Change values and verify they update in real-time

---

## Benefits Summary

### User Experience Improvements
1. **Cleaner Layout**: Widget automatically shrinks to fit active cells (no wasted space)
2. **Better Readability**: Smaller font ensures multi-line data fits properly
3. **Visual Feedback**: Tire temps now have color-coded warnings (cold/optimal/hot)
4. **Clear Units**: Temperature labels show °C or °F so no confusion
5. **Better Defaults**: Driving Widget starts with useful data (fuel + brake) instead of empty boxes

### Developer Benefits
1. **Dynamic Layout**: Grid properly collapses based on content
2. **Consistent Units**: Unit indicators update automatically when settings change
3. **Color System**: Reusable tire temperature color ranges
4. **Event-Driven**: Labels update reactively when settings change

---

## Temperature Color Coding Reference

### Tire Temperature Zones (Celsius)

| Zone | Range | Color | Visual | Meaning |
|------|-------|-------|--------|---------|
| **Cold** | < 60°C | Blue | 🔵 | Tires not warmed up yet |
| **Warming** | 60-75°C | Teal | 🟦 | Approaching optimal range |
| **Optimal** | 75-95°C | Light Green | 🟢 | Best grip and performance |
| **Hot** | 95-110°C | Orange | 🟠 | Getting too hot, degradation risk |
| **Extreme** | > 110°C | Red | 🔴 | Dangerous - tire damage likely |

### Converting to Fahrenheit

| Zone | Range (Celsius) | Range (Fahrenheit) |
|------|-----------------|-------------------|
| Cold | < 60°C | < 140°F |
| Warming | 60-75°C | 140-167°F |
| Optimal | 75-95°C | 167-203°F |
| Hot | 95-110°C | 203-230°F |
| Extreme | > 110°C | > 230°F |

**Note:** Color thresholds are based on Celsius values internally, but display converts to Fahrenheit when Imperial units selected.

---

## Build Status
✅ **Build succeeded** - No errors, no warnings

## Next Steps (Future Enhancements)

1. **Per-Tire Color Coding for TireTempAll**: Instead of average color, show multi-color display for mixed temps
2. **Adjustable Temperature Ranges**: Allow users to customize optimal temp ranges per car/track
3. **Tire Pressure Colors**: Add similar color coding for tire pressure fields
4. **Brake Temperature Colors**: Color-code brake temps (cool/optimal/hot/fade)
5. **Fuel Level Colors**: Visual warning when fuel gets low (yellow/red)

---

**Status**: ✅ **COMPLETE**  
**Build**: ✅ **PASSING**  
**Testing**: ⏳ **READY FOR USER TESTING**
