# 🏗️ MVP 2 REVISED - Modular Architecture
**Date:** October 12, 2025  
**Status:** 🔄 Architecture Planning  
**Critical Decision:** Build modular from the ground up!

---

## 🎯 Why Modular Architecture?

### User's Vision (Racelabs-style):
- ✅ Multiple overlay windows simultaneously
- ✅ Each window independently moveable
- ✅ Each window independently resizable
- ✅ Each window shows different stats
- ✅ Save/load layouts
- ✅ Add/remove windows on the fly

### The Right Approach:
**Build the widget system FIRST, then create widgets!**

---

## 🏛️ Revised Architecture

### Core Components:

#### 1. **Widget Base Class** (Foundation)
```csharp
public abstract class WidgetBase : Window
{
    // Common overlay properties
    - Transparent background
    - Always on top
    - No window chrome
    - Draggable
    - Resizable (optional)
    
    // Widget identification
    - Unique ID (Guid)
    - Widget Type (enum)
    - Position & Size
    
    // Telemetry connection
    - Subscribe to TelemetryService
    - Auto-update UI
    
    // Save/Load
    - Serialize position/size
    - Serialize settings
}
```

#### 2. **Widget Manager** (Orchestration)
```csharp
public class WidgetManager
{
    - List<WidgetBase> ActiveWidgets
    - CreateWidget(WidgetType type)
    - RemoveWidget(Guid id)
    - SaveLayout()
    - LoadLayout()
    - ShowAll() / HideAll()
}
```

#### 3. **Individual Widget Types**
```csharp
// MVP 2 - Basic widgets
- SpeedWidget (just speed display)
- RPMWidget (just RPM gauge)
- GearWidget (just gear indicator)
- TelemetryTableWidget (multiple stats in table)

// MVP 3+ - Advanced widgets
- FuelWidget (fuel gauge + calculator)
- TemperatureWidget (gauges for water/oil)
- LapTimesWidget (current/best/delta)
- TireWidget (4 tires with temps/wear)
- InputWidget (throttle/brake/steering bars)
```

#### 4. **Configuration System**
```json
// layout.json
{
  "widgets": [
    {
      "id": "guid-1234",
      "type": "SpeedWidget",
      "position": { "x": 100, "y": 100 },
      "size": { "width": 200, "height": 80 },
      "settings": {
        "showKph": true,
        "showMph": false
      }
    },
    {
      "id": "guid-5678",
      "type": "TelemetryTableWidget",
      "position": { "x": 100, "y": 200 },
      "size": { "width": 300, "height": 400 },
      "settings": {
        "stats": ["Speed", "RPM", "Gear", "Fuel", "WaterTemp"]
      }
    }
  ]
}
```

---

## 📋 Revised MVP Breakdown

### **MVP 2: Widget Foundation + Simple Widgets**
**Goal:** Build the modular system with 2-3 basic widgets

#### Phase 1: Core Architecture (2 hours)
- [ ] Create `WidgetBase` abstract class
- [ ] Create `WidgetManager` service
- [ ] Create `WidgetType` enum
- [ ] Set up widget registration system
- [ ] Implement widget lifecycle (create/destroy)

#### Phase 2: Widget Base Features (2 hours)
- [ ] Transparent overlay window base
- [ ] Always-on-top behavior
- [ ] Draggable functionality (mouse down + move)
- [ ] Optional resize grips
- [ ] Telemetry service injection
- [ ] Thread-safe UI updates (Dispatcher)

#### Phase 3: First Simple Widget (1 hour)
- [ ] Create `SpeedWidget` class (inherit WidgetBase)
- [ ] Simple XAML: large speed display
- [ ] Bind to telemetry Speed property
- [ ] Test with iRacing data

#### Phase 4: Second Simple Widget (1 hour)
- [ ] Create `TelemetryTableWidget` class
- [ ] Display 5-10 stats in a grid/table
- [ ] Configurable which stats to show
- [ ] Test with iRacing data

#### Phase 5: Widget Manager Integration (1.5 hours)
- [ ] Create main app window (hidden, system tray?)
- [ ] Menu to add/remove widgets
- [ ] Show/hide all hotkey (F12)
- [ ] Multiple widgets at once

#### Phase 6: Save/Load Layout (1.5 hours)
- [ ] Serialize widget configs to JSON
- [ ] Save on app close
- [ ] Load on app start
- [ ] Restore widget positions/sizes

**Total MVP 2:** ~9 hours (was 6 hours)

---

### **MVP 3: Advanced Widgets**
**Goal:** Add specialized widget types

- [ ] RPM Gauge Widget (circular dial)
- [ ] Fuel Gauge Widget (bar + percentage + laps remaining)
- [ ] Temperature Widget (dual gauges for water/oil)
- [ ] Lap Times Widget (current/best/delta with color coding)
- [ ] Gear Indicator Widget (large centered gear display)

**Total MVP 3:** ~8 hours

---

### **MVP 4: Widget Customization**
**Goal:** Make widgets configurable

- [ ] Widget settings dialog
- [ ] Color themes (dark, light, custom)
- [ ] Font size adjustment
- [ ] Transparency/opacity control
- [ ] Border styles
- [ ] Background options

**Total MVP 4:** ~6 hours

---

### **MVP 5: Advanced Features**
**Goal:** Polish and advanced functionality

- [ ] Widget templates (pre-made layouts)
- [ ] Import/export layouts
- [ ] Widget locking (prevent accidental move)
- [ ] Snap-to-grid positioning
- [ ] Widget grouping (move multiple together)
- [ ] Conditional display (show only in race, qualify, etc.)

**Total MVP 5:** ~8 hours

---

### **MVP 6: Performance & Polish**
**Goal:** Optimize and finalize

- [ ] Performance profiling
- [ ] Memory optimization
- [ ] GPU acceleration
- [ ] Multi-monitor support
- [ ] Update checker
- [ ] Documentation

**Total MVP 6:** ~6 hours

---

## 🎨 Example Widget System in Action

### Scenario: User wants 3 overlays like Racelabs

#### **Overlay 1: Top-Left (Inputs)**
```
┌─────────────────┐
│ Throttle: ████░ │
│ Brake:    ██░░░ │
│ Clutch:   ░░░░░ │
│ Steering: ←──┼─→│
└─────────────────┘
```

#### **Overlay 2: Top-Center (Critical Info)**
```
┌────────────────────────┐
│      Speed: 187 km/h   │
│      RPM: 7,842        │
│      Gear: 4           │
│      Fuel: 67.5%       │
└────────────────────────┘
```

#### **Overlay 3: Bottom-Right (Temps & Times)**
```
┌──────────────────────┐
│ Water: 89°C  [████░] │
│ Oil:   105°C [█████░]│
│                      │
│ Last: 1:34.521       │
│ Best: 1:33.012       │
└──────────────────────┘
```

**All 3 windows:**
- Independently positioned
- Independently sized
- Show different stats
- Can be added/removed
- Saved in layout.json

---

## 🔧 Technical Implementation

### Project Structure:
```
iRacingOverlay.WPF/
├── App.xaml
├── App.xaml.cs
│
├── Core/
│   ├── WidgetBase.cs            ⭐ Abstract base class
│   ├── WidgetManager.cs         ⭐ Orchestration
│   ├── WidgetType.cs            ⭐ Enum of widget types
│   └── IWidgetService.cs        ⭐ Interface
│
├── Widgets/                     ⭐ Individual widgets
│   ├── SpeedWidget/
│   │   ├── SpeedWidget.xaml
│   │   ├── SpeedWidget.xaml.cs
│   │   └── SpeedWidgetViewModel.cs
│   │
│   ├── TelemetryTableWidget/
│   │   ├── TelemetryTableWidget.xaml
│   │   ├── TelemetryTableWidget.xaml.cs
│   │   └── TelemetryTableWidgetViewModel.cs
│   │
│   └── ... (more widgets)
│
├── ViewModels/
│   ├── MainViewModel.cs         # Main app (widget manager UI)
│   └── WidgetBaseViewModel.cs   # Base for widget VMs
│
├── Models/
│   ├── WidgetConfig.cs          # Serializable config
│   └── LayoutConfig.cs          # Layout file structure
│
├── Services/
│   └── LayoutService.cs         # Save/load layouts
│
└── MainWindow.xaml              # Hidden main window or system tray
```

---

## 🎯 Key Design Decisions

### 1. **Widget Base Class**
- All widgets inherit from `WidgetBase`
- Common functionality: transparency, dragging, telemetry
- Each widget implements its own UI and ViewModel

### 2. **Widget Manager**
- Single source of truth for active widgets
- Handles lifecycle management
- Coordinates save/load operations

### 3. **Configuration**
- JSON-based layout files
- Each widget has its own settings
- Easy import/export for sharing

### 4. **Dependency Injection**
- TelemetryService injected into widgets
- WidgetManager registered as singleton
- Easy testing and mocking

### 5. **MVVM Pattern**
- Each widget has its own ViewModel
- Data binding for real-time updates
- Separation of concerns

---

## 🚀 Advantages of This Approach

### ✅ **Scalability**
- Add new widget types easily (just inherit WidgetBase)
- No limit on number of widgets
- Each widget is self-contained

### ✅ **Flexibility**
- Users create custom layouts
- Mix and match widgets
- Each widget independently configurable

### ✅ **Maintainability**
- Clean separation of concerns
- Reusable base class
- Easy to test individual widgets

### ✅ **User Experience**
- Racelabs-style flexibility
- Save/load layouts
- Drag-and-drop positioning
- Resize to preferences

### ✅ **Future-Proof**
- Easy to add new widget types
- Plugin architecture possible later
- Community widgets feasible

---

## 📊 Comparison: Old vs New Plan

| Aspect | Old Plan (Simple) | New Plan (Modular) |
|--------|-------------------|-------------------|
| Architecture | Single overlay window | Widget-based system |
| Flexibility | Fixed layout | Unlimited layouts |
| Widgets | 1 window, all stats | Multiple windows, each widget |
| Positioning | Single position | Each widget moveable |
| Resizing | Single size | Each widget resizable |
| Customization | Limited | Full control |
| Racelabs-like? | ❌ No | ✅ Yes |
| Development time | 6 hours | 9 hours (+50%) |
| Long-term value | Limited | High |

**Verdict:** +3 hours investment now = 10x more powerful system!

---

## 🎯 Recommendation

### **Build Modular from Day 1!**

**Why:**
1. ✅ Matches your Racelabs vision
2. ✅ Only +3 hours extra work
3. ✅ Infinitely more flexible
4. ✅ Easier to maintain long-term
5. ✅ Better user experience
6. ✅ Future-proof architecture

**Trade-off:**
- ⏱️ +3 hours initial development
- 🧠 Slightly more complex architecture
- 📚 More code to write upfront

**Payoff:**
- 🚀 10x more powerful
- 😊 Better UX from day 1
- 🔮 Ready for advanced features
- 🎨 True customization

---

## ✅ Decision Time!

### **Option A: Modular Widget System** (Recommended)
- 9 hours MVP 2
- Racelabs-style flexibility
- Multiple independent overlays
- Each moveable/resizable
- Save/load layouts
- **Future-proof** ⭐

### **Option B: Simple Single Overlay**
- 6 hours MVP 2
- One fixed overlay
- All stats in one window
- Basic functionality
- Will need refactor later

---

## 🎤 Your Call!

**Which approach do you prefer?**

**I recommend Option A (Modular)** because:
1. It matches your Racelabs vision
2. Only 50% more time (+3 hours)
3. Infinitely more valuable
4. Won't need refactoring later
5. Better UX from day 1

**Should we build the modular widget system?** 🚀

---

**Last Updated:** October 12, 2025  
**Status:** Awaiting architecture decision  
**Next:** Build WidgetBase + WidgetManager + First Widget
