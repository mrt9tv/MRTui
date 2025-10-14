# Widget Architecture Proposal v2.0
**Based on Racelabs-style modular, customizable widget system**

## 🎯 Current vs. Proposed Architecture

### **Current Architecture (MVP 2 - Basic)**
```
Widget = Single Purpose
- SpeedWidget → Shows only speed
- TelemetryTableWidget → Shows only table of stats
- RPMGaugeWidget → Shows only RPM gauge
```

**Limitations:**
- ❌ Not customizable - user can't swap data sources
- ❌ Redundant code - each widget has its own layout logic
- ❌ Not composable - can't combine elements
- ❌ Limited flexibility - can't create custom layouts

---

### **Proposed Architecture (Racelabs-Style)**

```
Widget = Container + Configurable Components

Widget (Container)
├── Layout Type (defines structure)
│   ├── SingleValue (like your Speed example)
│   ├── GaugeWithValue (like your Gear/RPM circular)
│   ├── FuelCalculator (like your fuel table)
│   └── MultiRow (like your session info)
│
└── Data Bindings (what data to show)
    ├── Primary Data Source
    ├── Secondary Data Source
    └── Display Options
```

---

## 📊 Analysis of Your Racelabs Examples

### **Image 1: Gear Widget**
```yaml
Layout: GaugeWithValue
Structure:
  - Circular gauge (center)
  - Large value display (center text)
  - Status indicators (bottom row: IBC, OLCK, time)
  
Customizable Elements:
  - Center value: Gear (could be Speed, RPM, Position, etc.)
  - Gauge range: 0-max gear
  - Status row items: Any 3 telemetry values
  
Data Bindings:
  - Primary: CurrentGear
  - Secondary: InPitLane, OilLockout, RaceTime
```

### **Image 2: Fuel Calculator Widget**
```yaml
Layout: FuelCalculator
Structure:
  - Header with current fuel (77.75 L / 40L)
  - Stats table (AVG, MAX, MIN, LS usage/laps/pits)
  - Alert row (LAPS UNTIL EMPTY)
  
Customizable Elements:
  - Fuel units (L, gal)
  - Calculation mode (average/max/conservative)
  - Alert threshold
  
Data Bindings:
  - Primary: FuelLevel, FuelCapacity
  - Calculations: Usage per lap, laps remaining
  - Race info: Total laps, position
```

### **Image 3: Session Info Widget**
```yaml
Layout: MultiRow
Structure:
  - Row 1: Track name + lap count indicator
  - Row 2: Driver name + position + delta
  - Row 3: Session type + current lap + time
  
Customizable Elements:
  - Each row can show different data
  - Text alignment and sizing
  - Color coding (green progress bar)
  
Data Bindings:
  - Track: TrackName, TotalLaps
  - Driver: DriverName, Position, Delta
  - Session: SessionType, CurrentLap, SessionTime
```

---

## 🏗️ Proposed Component System

### **1. Widget Layouts (Templates)**

```csharp
public enum WidgetLayout
{
    // Simple displays
    SingleValue,        // Large value with label (Speed, RPM, etc.)
    DoubleValue,        // Two values side-by-side
    MultiValue,         // Multiple values in grid
    
    // Gauges
    CircularGauge,      // Circular with center value (Gear, RPM)
    LinearGauge,        // Horizontal/vertical bar (Fuel, Brake)
    
    // Complex
    FuelCalculator,     // Specialized fuel management
    LapTimesTable,      // Lap time history
    TireMonitor,        // 4-tire display with temps/wear
    InputDisplay,       // Steering/throttle/brake visual
    
    // Composite
    MultiRow,           // Flexible rows of data
    Dashboard,          // Customizable grid layout
}
```

### **2. Data Bindings (What to Display)**

```csharp
public class WidgetDataBinding
{
    public TelemetryField PrimaryField { get; set; }     // Main data source
    public TelemetryField? SecondaryField { get; set; }  // Optional secondary
    public List<TelemetryField> StatusFields { get; set; } // Status indicators
    public DisplayOptions DisplayOptions { get; set; }    // Formatting
}

public enum TelemetryField
{
    // Speed & Motion
    Speed, SpeedKmh, SpeedMph, Velocity,
    
    // Engine
    RPM, Gear, Throttle, Brake, Clutch,
    
    // Temperatures
    WaterTemp, OilTemp, TireTemp, AirTemp,
    
    // Fuel
    FuelLevel, FuelPercent, FuelUsedLastLap, FuelRemaining,
    
    // Lap & Timing
    LapNumber, Position, LastLapTime, BestLapTime, CurrentLapTime,
    DeltaToSessionBest, DeltaToBestLap,
    
    // Session
    SessionTime, RaceTime, TimeRemaining, Laps, LapsRemaining,
    
    // Tires
    TirePressure_LF, TirePressure_RF, TirePressure_LR, TirePressure_RR,
    TireTemp_LF, TireTemp_RF, TireTemp_LR, TireTemp_RR,
    TireWear_LF, TireWear_RF, TireWear_LR, TireWear_RR,
    
    // Flags & Status
    Flags, InPitLane, OnTrack, SessionState,
    
    // Driver
    DriverName, CarNumber, TrackName,
}
```

### **3. Display Options**

```csharp
public class DisplayOptions
{
    // Units
    public string Unit { get; set; } = "";  // "km/h", "L", "°C", etc.
    public int DecimalPlaces { get; set; } = 0;
    
    // Formatting
    public string Format { get; set; } = "{0}";  // "{0:F1} L", "P{0}", etc.
    public double FontSize { get; set; } = 24;
    public string FontFamily { get; set; } = "Consolas";
    
    // Colors
    public string NormalColor { get; set; } = "#00FF00";
    public string WarningColor { get; set; } = "#FFFF00";
    public string DangerColor { get; set; } = "#FF0000";
    
    // Thresholds for color changes
    public double? WarningThreshold { get; set; }
    public double? DangerThreshold { get; set; }
    public bool InvertThresholds { get; set; } = false;
}
```

---

## 🎨 Example Widget Configurations

### **Example 1: Speed Widget (Current)**
```csharp
var speedWidget = new Widget
{
    Layout = WidgetLayout.SingleValue,
    DataBinding = new WidgetDataBinding
    {
        PrimaryField = TelemetryField.Speed,
        DisplayOptions = new DisplayOptions
        {
            Unit = "km/h",
            DecimalPlaces = 0,
            FontSize = 48,
            NormalColor = "#00FF00"
        }
    }
};
```

### **Example 2: Gear Gauge (Like Racelabs)**
```csharp
var gearWidget = new Widget
{
    Layout = WidgetLayout.CircularGauge,
    DataBinding = new WidgetDataBinding
    {
        PrimaryField = TelemetryField.Gear,
        StatusFields = new List<TelemetryField>
        {
            TelemetryField.InPitLane,  // IBC indicator
            TelemetryField.Clutch,     // OLCK indicator
            TelemetryField.RaceTime    // Time display
        },
        DisplayOptions = new DisplayOptions
        {
            FontSize = 64,
            NormalColor = "#00FF00"
        }
    }
};
```

### **Example 3: Fuel Calculator (Complex)**
```csharp
var fuelWidget = new Widget
{
    Layout = WidgetLayout.FuelCalculator,
    DataBinding = new WidgetDataBinding
    {
        PrimaryField = TelemetryField.FuelLevel,
        SecondaryField = TelemetryField.FuelRemaining,
        DisplayOptions = new DisplayOptions
        {
            Unit = "L",
            DecimalPlaces = 2,
            WarningThreshold = 5.0,  // Laps remaining
            DangerThreshold = 2.0
        }
    }
};
```

### **Example 4: Customizable Speed → RPM**
```csharp
// User can change PrimaryField from Speed to RPM via UI
var flexWidget = new Widget
{
    Layout = WidgetLayout.SingleValue,
    DataBinding = new WidgetDataBinding
    {
        PrimaryField = TelemetryField.Speed, // User selectable dropdown
        DisplayOptions = new DisplayOptions
        {
            Unit = "km/h",  // Auto-changes to "RPM" if field changed
            DecimalPlaces = 0,
            FontSize = 48
        }
    }
};
```

---

## 🔧 Implementation Strategy

### **Phase 1: Widget Base Refactoring** (2-3 hours)
1. Create `WidgetLayout` enum
2. Create `TelemetryField` enum (comprehensive)
3. Create `WidgetDataBinding` class
4. Create `DisplayOptions` class
5. Refactor `WidgetBase` to use new system
6. Create `WidgetConfigurationDialog` for user customization

### **Phase 2: Layout Renderers** (3-4 hours)
Create layout renderers:
- `SingleValueRenderer` (refactor SpeedWidget to use this)
- `CircularGaugeRenderer` (for Gear/RPM gauges)
- `FuelCalculatorRenderer` (specialized fuel layout)
- `MultiRowRenderer` (flexible row-based layout)

### **Phase 3: Data Binding System** (2-3 hours)
1. Create `TelemetryDataMapper` to map fields to TelemetryData properties
2. Create `DataBindingEngine` to update widget values
3. Add unit conversion (km/h ↔ mph, L ↔ gal, °C ↔ °F)
4. Add threshold-based color changes

### **Phase 4: Widget Configuration UI** (3-4 hours)
1. Right-click widget → "Configure"
2. Dialog showing:
   - Layout type dropdown
   - Primary data field dropdown
   - Display options (unit, decimals, colors)
   - Status fields selection
3. Save configuration to WidgetConfig.Settings

---

## 📋 Revised Todo List

### **Immediate (Next Steps)**
- [ ] Refactor WidgetBase to use new architecture
- [ ] Create TelemetryField enum with all 60+ fields
- [ ] Create WidgetDataBinding and DisplayOptions classes
- [ ] Create TelemetryDataMapper for field → value mapping

### **Short Term (MVP 2 Complete)**
- [ ] Create SingleValueRenderer (generic)
- [ ] Refactor SpeedWidget to use SingleValueRenderer + DataBinding
- [ ] Create CircularGaugeRenderer (for Gear widget)
- [ ] Create FuelCalculatorRenderer
- [ ] Create MultiRowRenderer

### **Medium Term (MVP 3)**
- [ ] Build WidgetConfigurationDialog
- [ ] Add right-click menu to widgets
- [ ] Implement data binding engine
- [ ] Add unit conversion system
- [ ] Create widget templates/presets

### **Long Term (MVP 4+)**
- [ ] Advanced layouts (TireMonitor, InputDisplay, Dashboard)
- [ ] User-saved widget presets
- [ ] Layout import/export
- [ ] Conditional formatting rules
- [ ] Custom themes

---

## 💡 Key Benefits

### **For Users:**
✅ **Flexibility** - Change what any widget displays without creating new widgets
✅ **Customization** - Adjust colors, units, thresholds per widget
✅ **Templates** - Save favorite configurations as presets
✅ **Consistency** - Same look and feel across all widgets

### **For Development:**
✅ **DRY Principle** - Layout logic separate from data logic
✅ **Maintainability** - Add new telemetry fields without new widgets
✅ **Testability** - Data binding and rendering are separate concerns
✅ **Extensibility** - Easy to add new layouts and data sources

---

## 🤔 Questions for You

1. **Complexity vs. Speed:**
   - **Option A:** Implement full architecture now (8-10 hours total)
   - **Option B:** Hybrid approach - keep SpeedWidget simple, new widgets use system
   - **Option C:** Continue simple approach, refactor later when needed

2. **Priority Widgets:**
   Which layouts are most important to you?
   - Circular Gauge (Gear/RPM with status indicators)?
   - Fuel Calculator (comprehensive fuel management)?
   - Multi-row (session info, driver details)?
   - Tire Monitor (4-tire display)?

3. **Configuration UI:**
   - Build configuration dialog now or later?
   - Manual JSON editing acceptable initially?

4. **Units and Localization:**
   - Support metric/imperial switching?
   - Temperature units (°C/°F)?
   - Auto-detect or user preference?

---

## 🎯 My Recommendation

**Hybrid Approach (Option B):**

1. **Keep SpeedWidget as-is** (proof of concept, already works)
2. **Build new architecture** for next widgets (2-3 hours)
3. **Create CircularGaugeWidget** using new system (Gear with status)
4. **Create FuelCalculatorWidget** using new system
5. **Test both systems** side-by-side
6. **Refactor SpeedWidget** once new system is proven (1 hour)

This gives us:
- ✅ Working widgets quickly
- ✅ Modern architecture validated
- ✅ Easy comparison of approaches
- ✅ Refactor path when ready

**Next Immediate Step:**
Create the data binding infrastructure (TelemetryField enum, DataBinding classes) so we can build the CircularGaugeWidget (Gear display like your first image).

---

## 🚀 What Would You Like to Do?

**A)** Implement full architecture now (best long-term, 8-10 hours)
**B)** Hybrid approach - new architecture for new widgets (recommended, 5-6 hours)
**C)** Simple approach - build a few simple widgets first, refactor later (fastest, 2-3 hours)

**D)** Something else? Share your thoughts!

Which option aligns with your goals and timeline?
