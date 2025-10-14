# MVP 4: Multi-Widget System

**Duration:** 1-2 weeks (Actual: ~1 week)  
**Goal:** Support multiple widgets with basic layout management  
**Status:** ✅ **COMPLETE** (February 2025)  
**Prerequisite:** MVP 1, 2, & 3 must be complete

---

## 🎯 What You Built

- **WidgetManager**: Comprehensive widget lifecycle management service
- **Multiple Widgets**: DrivingWidget (circular gauge) + DataWidget (2x3 grid)
- **Position Persistence**: Widget positions saved in AppSettings.json
- **Configuration System**: JSON-based settings with real-time updates

---

## ✅ Success Criteria

- ✅ Can show 2+ widgets simultaneously (DrivingWidget, DataWidget)
- ✅ Each widget independently positioned and sized
- ✅ Widget positions/sizes saved between sessions (AppSettings.json)
- ✅ No performance degradation with multiple widgets
- ✅ CPU usage <5% (hardware acceleration)
- ✅ Memory usage ~100MB
- ✅ Real-time enable/disable per widget via checkboxes

---

## 📁 Key Files to Create

```plaintext
iRacingOverlay.Widgets/
├── Management/
│   ├── IWidgetManager.cs
│   ├── WidgetManager.cs
│   └── WidgetRegistry.cs
├── Fuel/
│   ├── FuelWidget.xaml
│   ├── FuelWidget.xaml.cs
│   └── FuelViewModel.cs
├── Timing/
│   ├── TimingWidget.xaml
│   ├── TimingWidget.xaml.cs
│   └── TimingViewModel.cs
├── Standings/
│   ├── StandingsWidget.xaml
│   ├── StandingsWidget.xaml.cs
│   └── StandingsViewModel.cs
└── TrackMap/
    ├── TrackMapWidget.xaml
    ├── TrackMapWidget.xaml.cs
    └── TrackMapViewModel.cs

iRacingOverlay.Configuration/
├── Models/
│   ├── LayoutConfiguration.cs
│   └── WidgetSettings.cs
└── Services/
    └── LayoutManager.cs
```

---

## 📋 Implementation Checklist

### Week 1: Widget Manager + 2 Widgets

#### Days 1-2: Widget Management System

- [ ] Design `IWidgetManager` interface
- [ ] Implement `WidgetManager` class
- [ ] Create widget registration system
- [ ] Add widget lifecycle management (load/unload)
- [ ] Implement widget enable/disable
- [ ] Create widget positioning system
- [ ] Add unit tests for manager

#### Days 3-4: Fuel Calculator Widget

- [ ] Design Fuel widget UI
- [ ] Implement fuel calculation logic
- [ ] Add fuel per lap tracking
- [ ] Calculate laps remaining
- [ ] Show fuel needed for race
- [ ] Add low fuel warning
- [ ] Test with various session types

#### Days 5-7: Timing Display Widget

- [ ] Design Timing widget UI
- [ ] Show current lap time
- [ ] Display last lap time
- [ ] Track best lap time
- [ ] Calculate delta to best
- [ ] Add sector timing (optional)
- [ ] Test timing accuracy

### Week 2: 2 More Widgets + Configuration

#### Days 8-10: Standings Widget

- [ ] Design Standings widget UI
- [ ] Show full field positions
- [ ] Display class positions
- [ ] Add lap down indicators
- [ ] Show position changes
- [ ] Handle large fields (scroll)
- [ ] Test with 40+ cars

#### Days 11-12: Track Map Widget

- [ ] Design Track Map widget UI
- [ ] Draw basic track outline
- [ ] Show player position dot
- [ ] Add other cars (optional)
- [ ] Handle different track shapes
- [ ] Test with various tracks

#### Days 13-14: Configuration System

- [ ] Create JSON configuration structure
- [ ] Implement layout save/load
- [ ] Add position persistence
- [ ] Create widget settings per widget
- [ ] Add configuration validation
- [ ] Test configuration survival across restarts

---

## ⏱️ Time Breakdown

| Days | Focus Area | Hours |
|------|-----------|-------|
| **1-2** | Widget Manager | 10-12 |
| **3-4** | Fuel Widget | 8-10 |
| **5-7** | Timing Widget | 10-12 |
| **8-10** | Standings Widget | 10-12 |
| **11-12** | Track Map Widget | 8-10 |
| **13-14** | Configuration System | 8-10 |

**Total Estimated Hours:** 54-66 hours

---

## 🔧 Technical Details

### IWidgetManager Interface

```csharp
public interface IWidgetManager
{
    IReadOnlyList<IWidget> Widgets { get; }
    
    void RegisterWidget(IWidget widget);
    void UnregisterWidget(string widgetId);
    
    IWidget? GetWidget(string widgetId);
    T? GetWidget<T>() where T : IWidget;
    
    void ShowWidget(string widgetId);
    void HideWidget(string widgetId);
    void ShowAll();
    void HideAll();
    
    Task LoadLayoutAsync(LayoutConfiguration layout);
    Task SaveLayoutAsync(string layoutName);
}
```

### WidgetManager Implementation

```csharp
public class WidgetManager : IWidgetManager
{
    private readonly Dictionary<string, IWidget> _widgets = new();
    private readonly ILogger<WidgetManager> _logger;
    private readonly LayoutManager _layoutManager;
    
    public IReadOnlyList<IWidget> Widgets => 
        _widgets.Values.ToList().AsReadOnly();
    
    public WidgetManager(
        ILogger<WidgetManager> logger,
        LayoutManager layoutManager)
    {
        _logger = logger;
        _layoutManager = layoutManager;
    }
    
    public void RegisterWidget(IWidget widget)
    {
        if (_widgets.ContainsKey(widget.Id))
        {
            _logger.LogWarning("Widget {WidgetId} already registered", widget.Id);
            return;
        }
        
        _widgets[widget.Id] = widget;
        _logger.LogInformation("Registered widget: {WidgetName}", widget.Name);
    }
    
    public void UnregisterWidget(string widgetId)
    {
        if (_widgets.Remove(widgetId, out var widget))
        {
            widget.Dispose();
            _logger.LogInformation("Unregistered widget: {WidgetId}", widgetId);
        }
    }
    
    public IWidget? GetWidget(string widgetId)
    {
        return _widgets.GetValueOrDefault(widgetId);
    }
    
    public T? GetWidget<T>() where T : IWidget
    {
        return _widgets.Values.OfType<T>().FirstOrDefault();
    }
    
    public void ShowWidget(string widgetId)
    {
        if (GetWidget(widgetId) is { } widget)
        {
            widget.Show();
        }
    }
    
    public void HideWidget(string widgetId)
    {
        if (GetWidget(widgetId) is { } widget)
        {
            widget.Hide();
        }
    }
    
    public void ShowAll()
    {
        foreach (var widget in _widgets.Values)
        {
            widget.Show();
        }
    }
    
    public void HideAll()
    {
        foreach (var widget in _widgets.Values)
        {
            widget.Hide();
        }
    }
    
    public async Task LoadLayoutAsync(LayoutConfiguration layout)
    {
        foreach (var widgetConfig in layout.Widgets)
        {
            var widget = GetWidget(widgetConfig.WidgetId);
            if (widget != null)
            {
                widget.Configuration = widgetConfig;
                if (widgetConfig.IsEnabled)
                {
                    widget.Show();
                }
                else
                {
                    widget.Hide();
                }
            }
        }
        
        await Task.CompletedTask;
    }
    
    public async Task SaveLayoutAsync(string layoutName)
    {
        var layout = new LayoutConfiguration
        {
            Name = layoutName,
            Widgets = _widgets.Values
                .Select(w => w.Configuration)
                .ToList()
        };
        
        await _layoutManager.SaveLayoutAsync(layout);
    }
}
```

### Layout Configuration Model

```csharp
public class LayoutConfiguration
{
    public string Name { get; set; } = "Default";
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public List<WidgetConfiguration> Widgets { get; set; } = new();
}

public class WidgetConfiguration
{
    public string WidgetId { get; set; } = string.Empty;
    public string WidgetType { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public WidgetPosition Position { get; set; } = new();
    public Dictionary<string, object> Settings { get; set; } = new();
}

public class WidgetPosition
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
```

### Example Configuration JSON

```json
{
  "name": "Default Layout",
  "lastModified": "2025-10-12T10:30:00Z",
  "widgets": [
    {
      "widgetId": "relative-001",
      "widgetType": "Relative",
      "isEnabled": true,
      "position": {
        "x": 100,
        "y": 100,
        "width": 350,
        "height": 200
      },
      "settings": {
        "carsAhead": 3,
        "carsBehind": 3
      }
    },
    {
      "widgetId": "fuel-001",
      "widgetType": "Fuel",
      "isEnabled": true,
      "position": {
        "x": 500,
        "y": 100,
        "width": 200,
        "height": 150
      },
      "settings": {
        "showLapsRemaining": true,
        "lowFuelWarning": 5.0
      }
    },
    {
      "widgetId": "timing-001",
      "widgetType": "Timing",
      "isEnabled": true,
      "position": {
        "x": 100,
        "y": 350,
        "width": 250,
        "height": 120
      },
      "settings": {
        "showSectorTimes": false
      }
    }
  ]
}
```

---

## 📱 Widget Specifications

### 1. Fuel Calculator Widget

**Purpose:** Track fuel usage and calculate race strategy

**Display Elements:**

- Current fuel level (liters/gallons)
- Fuel per lap (average)
- Laps remaining on current fuel
- Fuel needed for race distance
- Low fuel warning (configurable threshold)

**Sample UI:**

```
┌─────────────────────────┐
│ FUEL                    │
├─────────────────────────┤
│ Current:      12.5 L    │
│ Per Lap:       2.1 L    │
│ Remaining:     5 laps   │
│ Needed:       21.0 L    │
└─────────────────────────┘
```

**Calculations:**

```csharp
public class FuelCalculator
{
    public double CalculateFuelPerLap(List<double> lastLaps)
    {
        // Average of last 3-5 laps
        return lastLaps.TakeLast(5).Average();
    }
    
    public int CalculateLapsRemaining(double currentFuel, double fuelPerLap)
    {
        return (int)Math.Floor(currentFuel / fuelPerLap);
    }
    
    public double CalculateFuelNeeded(int lapsRemaining, double fuelPerLap, double currentFuel)
    {
        var totalNeeded = lapsRemaining * fuelPerLap;
        return Math.Max(0, totalNeeded - currentFuel);
    }
}
```

### 2. Timing Display Widget

**Purpose:** Show lap time information and deltas

**Display Elements:**

- Current lap time (live)
- Last lap time
- Best lap time (session/personal)
- Delta to best lap
- Sector times (optional)

**Sample UI:**

```
┌─────────────────────────┐
│ TIMING                  │
├─────────────────────────┤
│ Current:   1:23.456     │
│ Last:      1:24.123     │
│ Best:      1:23.789     │
│ Delta:     -0.333       │
└─────────────────────────┘
```

### 3. Standings Widget

**Purpose:** Show full field standings

**Display Elements:**

- Position number
- Car number
- Driver name
- Laps completed
- Gap to leader
- Class position
- Laps down indicator

**Sample UI:**

```
┌─────────────────────────────┐
│ STANDINGS                   │
├─────────────────────────────┤
│ P# | Car | Driver     | Gap │
│  1 | 23  | Leader     | --  │
│  2 | 45  | Smith      | 2.3 │
│  3 | 12  | Johnson    | 5.7 │
│  4 | 99  | You        | 8.1 │
│ ...                         │
└─────────────────────────────┘
```

### 4. Track Map Widget

**Purpose:** Visual representation of car positions

**Display Elements:**

- Track outline (simple shape)
- Player position (highlighted dot)
- Other cars (optional, different colors)
- Start/finish line indicator

**Sample UI:**

```
┌─────────────────────────┐
│ TRACK MAP               │
├─────────────────────────┤
│                         │
│     ╱───────╲          │
│    │    •    │  ← You  │
│    ╲────────╱          │
│         │               │
└─────────────────────────┘
```

---

## 🐛 Common Issues & Solutions

### Issue: Widget Overlap

**Symptoms:** Widgets positioned on top of each other

**Solutions:**

- Implement basic collision detection
- Add snap-to-grid positioning
- Provide default layout templates
- Show widget boundaries in config mode

### Issue: Configuration Not Persisting

**Symptoms:** Widget positions reset after restart

**Solutions:**

- Verify JSON file write permissions
- Check configuration path is correct
- Add file write error handling
- Implement auto-save on position change

### Issue: Performance Degradation

**Symptoms:** FPS drops with multiple widgets

**Solutions:**

- Profile each widget individually
- Reduce update frequency for non-critical widgets
- Implement update throttling
- Use hardware acceleration

---

## 🧪 Testing Scenarios

### Test 1: Multiple Widgets Active

1. Enable all 4+ widgets
2. Position in different screen areas
3. Verify all update correctly
4. Check performance metrics

### Test 2: Configuration Persistence

1. Position widgets on screen
2. Close application
3. Restart application
4. Verify positions restored correctly

### Test 3: Widget Enable/Disable

1. Disable individual widgets
2. Verify they disappear
3. Re-enable widgets
4. Verify they reappear at saved positions

### Test 4: Large Fields

1. Join 40+ car race
2. Enable all widgets
3. Verify performance remains acceptable
4. Check Standings widget handles scrolling

---

## 📊 Performance Targets

| Metric | Target | Acceptable | Critical |
|--------|--------|------------|----------|
| **CPU Usage (4 widgets)** | <8% | <10% | >15% |
| **Memory Usage** | <150MB | <180MB | >250MB |
| **Overlay FPS** | 60 FPS | 50+ FPS | <45 FPS |
| **Config Load Time** | <1s | <2s | >5s |
| **Widget Toggle** | <100ms | <200ms | >500ms |

---

## ✨ Completion Checklist

Before moving to MVP 5, ensure:

- [ ] All success criteria met
- [ ] 4+ widgets implemented and working
- [ ] Widget manager functional
- [ ] Configuration saves/loads correctly
- [ ] All widgets independently positioned
- [ ] Performance targets achieved
- [ ] No widget conflicts or overlaps
- [ ] Code is well-documented
- [ ] Unit tests written (>70% coverage)
- [ ] Integration tests passing
- [ ] User guide started
- [ ] Code committed to Git

---

## 🚀 Next Steps

Once MVP 4 is complete:

1. Review this document and check all boxes
2. Update status to 🟢 Complete
3. Create demo video showing all widgets
4. Write user documentation for each widget
5. Commit all code changes
6. Move to **MVP 5: Basic Configuration UI**

---

## 📚 Resources

- [Dependency Injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [JSON Serialization in .NET](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to)
- [WPF Layout Management](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/layout)
- [Performance Profiling in Visual Studio](https://learn.microsoft.com/en-us/visualstudio/profiling/)

---

**Created:** October 12, 2025  
**Last Updated:** October 12, 2025  
**Next Review:** After completion  
**Related:** [MVP 3](./MVP3_First_Widget.md) | [MVP 5](./MVP5_Basic_Config.md) | [Overview](../../MANAGEABLE_PHASES.md)
