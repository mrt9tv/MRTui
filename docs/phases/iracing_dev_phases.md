# iRacing Overlay Application
## Complete Development Phases Overview
**Version 2.0 - Powered by Claude Sonnet 4.5 & .NET 8**

---

## 📋 Executive Summary

This document outlines the complete development roadmap for building a professional-grade iRacing telemetry overlay application. The project is structured into four major phases, progressing from foundation to advanced AI features, with Racelabs App serving as our feature reference model.

**Core Technologies:**
- **.NET 8.0** - Modern, high-performance framework
- **C# 12** - Latest language features
- **WPF with Hardware Acceleration** - Overlay rendering
- **SVappsLAB.iRacingTelemetrySDK** - Latest iRacing SDK wrapper
- **React TypeScript** - Web configuration interface (Phase 3)
- **ONNX Runtime** - Local AI/ML inference (Phase 4)

**Target Platform:** Windows 10/11 (x64)

---

## 📊 Current Progress Status

**Last Updated:** October 13, 2025

| MVP Phase | Status | Completion Date | Key Deliverables |
|-----------|--------|-----------------|------------------|
| **MVP 1: Basic Connection** | ✅ Complete | January 12, 2025 | Console telemetry app, SDK integration, 25-60Hz updates |
| **MVP 2: Simple Overlay** | ✅ Complete | January 2025 | WPF overlay windows, transparency, topmost behavior |
| **MVP 3: First Widget** | ✅ Complete | February 2025 | DrivingWidget (circular gauge), DataWidget (2x3 grid), WidgetBase architecture |
| **MVP 4: Multi-Widget System** | ✅ Complete | February 2025 | WidgetManager, position persistence, JSON settings |
| **MVP 5: Basic Config UI** | ✅ Complete | February 2025 | MainWindow config UI, immediate-apply pattern, real-time updates |
| **MVP 6: Advanced Config** | ⏳ In Progress | Phase 7 & 7.5 done | Field customization, color coding, unit conversion (themes pending) |

**Current Development Focus:** MVP 6 completion and stability improvements

**See Also:**
- Individual MVP details: `/docs/phases/MVP1_Basic_Connection.md` through `MVP6_Advanced_Config.md`
- Phase completion reports: `/docs/reference/PHASE_X_*.md`
- Project status summary: `/PROJECT_STATUS.md`

---

## 🎯 Project Vision

Create a free, open-source, high-performance iRacing overlay that rivals commercial solutions like Racelabs, iOverlay, and SDK Gaming, while adding unique AI-powered features that provide genuine competitive advantages to sim racers.

### Key Differentiators
1. **100% Free and Open Source** - No subscription fees
2. **AI-Powered Insights** - Local ML models for lap prediction and racing line optimization
3. **Modern Architecture** - Built with latest .NET 8 for maximum performance
4. **Extensive Customization** - Web-based drag-and-drop configuration
5. **Privacy-First** - All data processing happens locally

---

## 🏗️ Overall Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    User Experience Layer                     │
│  ┌────────────────┐              ┌─────────────────────┐   │
│  │  WPF Overlay   │              │   Web Config UI     │   │
│  │  (Transparent) │◄─────────────┤   (React TS)        │   │
│  └────────────────┘              └─────────────────────┘   │
└────────────┬────────────────────────────────┬───────────────┘
             │                                │
┌────────────▼────────────────────────────────▼───────────────┐
│                   Application Services                       │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐ │
│  │  Telemetry   │  │  Widget      │  │  Configuration   │ │
│  │  Service     │  │  Manager     │  │  Manager         │ │
│  └──────────────┘  └──────────────┘  └──────────────────┘ │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐ │
│  │  Data        │  │  Alert       │  │  ML Inference    │ │
│  │  Processing  │  │  Engine      │  │  (Phase 4)       │ │
│  └──────────────┘  └──────────────┘  └──────────────────┘ │
└────────────┬────────────────────────────────────────────────┘
             │
┌────────────▼────────────────────────────────────────────────┐
│              iRacing SDK Integration Layer                   │
│         (SVappsLAB.iRacingTelemetrySDK 0.5.0+)             │
└────────────┬────────────────────────────────────────────────┘
             │
┌────────────▼────────────────────────────────────────────────┐
│                    iRacing Simulator                         │
│              (60Hz Telemetry + Session Data)                 │
└──────────────────────────────────────────────────────────────┘
```

---

## 📊 Performance Targets Across All Phases

| Metric | Phase 1 | Phase 2 | Phase 3 | Phase 4 |
|--------|---------|---------|---------|---------|
| **CPU Usage** | <5% | <8% | <8% | <12% |
| **Memory Usage** | <100MB | <150MB | <180MB | <250MB |
| **Telemetry Rate** | 60Hz | 60Hz | 60Hz | 60Hz |
| **Overlay FPS** | 60 | 60 | 60 | 60 |
| **UI Response** | <50ms | <16ms | <16ms | <16ms |
| **ML Inference** | N/A | N/A | N/A | <50ms |

---

## 🎮 Feature Matrix - Inspired by Racelabs & Beyond

| Feature Category | Racelabs | iOverlay | Our App | Phase |
|------------------|----------|----------|---------|-------|
| **Basic Overlays** |
| Relative Position | ✅ | ✅ | ✅ | 2 |
| Standings | ✅ | ✅ | ✅ | 2 |
| Fuel Calculator | ✅ | ✅ | ✅ | 2 |
| Timing Display | ✅ | ✅ | ✅ | 2 |
| Track Map | ✅ Pro | ✅ | ✅ | 2 |
| **Advanced Overlays** |
| Input Telemetry | ✅ Pro | ✅ | ✅ | 2 |
| Spotter/Radar | ✅ | ✅ | ✅ | 2 |
| Tire Temperature | ✅ Pro | ✅ | ✅ | 2 |
| Boost Meter | ✅ | ✅ | ✅ | 2 |
| Head-to-Head | ✅ Pro | ✅ | ✅ | 2 |
| Delta/Sector Times | ✅ | ✅ | ✅ | 2 |
| **Configuration** |
| Web-Based Config | ❌ | ❌ | ✅ | 3 |
| Drag-Drop Layout | ❌ | ❌ | ✅ | 3 |
| Live Preview | ❌ | ❌ | ✅ | 3 |
| Theme System | ✅ Limited | ✅ | ✅ | 3 |
| Cloud Sync | ❌ | ❌ | ✅ | 3 |
| **AI Features** |
| Lap Time Prediction | ❌ | ❌ | ✅ | 4 |
| Racing Line AI | ❌ | ❌ | ✅ | 4 |
| Performance Analysis | ❌ | ❌ | ✅ | 4 |
| Anomaly Detection | ❌ | ❌ | ✅ | 4 |
| **Business Model** |
| Free Basic | ✅ | ✅ | ✅ | All |
| Subscription | €4.90/mo | ❌ | ❌ | N/A |
| Open Source | ❌ | ❌ | ✅ | All |

---

# PHASE 1: Foundation & SDK Integration
**Duration:** 2-3 weeks  
**Goal:** Establish rock-solid foundation with real iRacing SDK integration

## 1.1 Overview

Phase 1 transforms the current mock telemetry implementation into a production-ready foundation with real iRacing SDK integration. This phase focuses exclusively on reliability, performance, and proper architecture—no fancy features yet.

## 1.2 Technical Prerequisites

### Development Environment
- **Visual Studio Code** with C# Dev Kit
- **.NET 8.0 SDK** (latest patch)
- **Git** for version control
- **Windows 10/11** (x64)
- **iRacing subscription** for testing

### Required NuGet Packages
```xml
<!-- Core SDK -->
<PackageReference Include="SVappsLAB.iRacingTelemetrySDK" Version="0.5.0" />

<!-- Dependency Injection & Hosting -->
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />

<!-- Configuration -->
<PackageReference Include="System.Text.Json" Version="8.0.0" />

<!-- Testing -->
<PackageReference Include="xUnit" Version="2.6.0" />
<PackageReference Include="Moq" Version="4.20.0" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
```

## 1.3 Project Structure

```
iRacingOverlay/
├── src/
│   ├── iRacingOverlay.Core/
│   │   ├── Telemetry/
│   │   │   ├── ITelemetryService.cs
│   │   │   ├── IRacingTelemetryService.cs
│   │   │   ├── TelemetryConnectionManager.cs
│   │   │   └── Models/
│   │   │       ├── TelemetryData.cs
│   │   │       └── ConnectionStatus.cs
│   │   ├── DataProcessing/
│   │   │   ├── CircularBuffer.cs
│   │   │   └── TelemetryProcessor.cs
│   │   └── Performance/
│   │       ├── PerformanceMonitor.cs
│   │       └── ObjectPool.cs
│   │
│   ├── iRacingOverlay.Configuration/
│   │   ├── Models/
│   │   │   ├── AppConfiguration.cs
│   │   │   ├── OverlaySettings.cs
│   │   │   └── TelemetrySettings.cs
│   │   ├── ConfigurationManager.cs
│   │   └── Validation/
│   │       └── ConfigurationValidator.cs
│   │
│   ├── iRacingOverlay.Overlay/
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── MainWindow.xaml / MainWindow.xaml.cs
│   │   ├── OverlayWindow.xaml / OverlayWindow.xaml.cs
│   │   └── Services/
│   │       └── OverlayService.cs
│   │
│   └── iRacingOverlay.Tests/
│       ├── Integration/
│       │   ├── TelemetryServiceTests.cs
│       │   └── ConnectionTests.cs
│       └── Unit/
│           ├── CircularBufferTests.cs
│           └── ConfigurationTests.cs
│
├── assets/
│   └── config/
│       └── default-settings.json
│
├── docs/
│   └── phase1/
│       ├── architecture.md
│       └── testing-guide.md
│
└── iRacingOverlay.sln
```

## 1.4 Implementation Roadmap

### Week 1: SDK Integration & Core Services

#### Days 1-2: SDK Setup & Basic Connection
**Objective:** Establish connection to iRacing

**Tasks:**
- [ ] Install SVappsLAB.iRacingTelemetrySDK
- [ ] Create `ITelemetryService` interface
- [ ] Implement `IRacingTelemetryService` class
- [ ] Add connection state management
- [ ] Implement basic error handling

**Key Code Pattern:**
```csharp
[RequiredTelemetryVars([
    "Speed", "RPM", "Gear", "Throttle", "Brake",
    "Clutch", "SteeringWheelAngle", "Lap", "LapDistPct"
])]
public class IRacingTelemetryService : ITelemetryService
{
    private TelemetryClient? _client;
    private readonly ILogger<IRacingTelemetryService> _logger;
    
    public event EventHandler<TelemetryData>? TelemetryUpdated;
    public bool IsConnected => _client?.IsRunning ?? false;
    
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _client = new TelemetryClient();
        _client.OnTelemetryUpdate += HandleTelemetryUpdate;
        await _client.Monitor(cancellationToken);
    }
}
```

#### Days 3-4: Connection Resilience
**Objective:** Handle disconnections gracefully

**Tasks:**
- [ ] Implement exponential backoff retry logic
- [ ] Add connection health monitoring
- [ ] Create reconnection strategies
- [ ] Add telemetry validation
- [ ] Implement timeout handling

**Features:**
- Auto-reconnect when iRacing starts
- Graceful handling when sim closes
- Connection status events
- Data validation and sanitization

#### Days 5-7: Data Processing Pipeline
**Objective:** Efficient 60Hz data handling

**Tasks:**
- [ ] Create `CircularBuffer<T>` for data history
- [ ] Implement `TelemetryProcessor` for data transformation
- [ ] Add performance monitoring
- [ ] Create object pooling for memory efficiency
- [ ] Implement data smoothing/filtering (optional)

**Performance Targets:**
- Process 60 telemetry updates/second
- Keep 5 minutes of data history
- <1ms processing latency per update
- <50MB memory for data buffer

### Week 2: Configuration & Basic Overlay

#### Days 8-10: Configuration System
**Objective:** Persistent, validated configuration

**Tasks:**
- [ ] Design configuration models
- [ ] Implement JSON-based persistence
- [ ] Add validation rules
- [ ] Create configuration migration system
- [ ] Add default configuration

**Configuration Structure:**
```json
{
  "version": "1.0",
  "telemetry": {
    "updateFrequency": 60,
    "autoConnect": true,
    "reconnectDelay": 5000
  },
  "overlay": {
    "enabled": true,
    "opacity": 0.85,
    "position": { "x": 100, "y": 100 },
    "alwaysOnTop": true
  },
  "performance": {
    "maxCpuPercent": 5,
    "maxMemoryMB": 100,
    "enableProfiling": false
  }
}
```

#### Days 11-12: Basic WPF Overlay
**Objective:** Transparent, hardware-accelerated window

**Tasks:**
- [ ] Create transparent WPF window
- [ ] Implement click-through functionality
- [ ] Add hardware acceleration
- [ ] Create basic data display (simple text)
- [ ] Wire telemetry service to UI

**Key WPF Features:**
```xaml
<Window x:Class="iRacingOverlay.Overlay.OverlayWindow"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        RenderOptions.ProcessRenderMode="Default">
</Window>
```

#### Days 13-14: Integration & Testing
**Objective:** All components working together

**Tasks:**
- [ ] Integration testing with live iRacing
- [ ] End-to-end telemetry flow validation
- [ ] Configuration load/save testing
- [ ] Memory leak detection
- [ ] Performance profiling

### Week 3: Polish & Stabilization

#### Days 15-17: Error Handling & Logging
**Objective:** Production-ready reliability

**Tasks:**
- [ ] Comprehensive error handling throughout
- [ ] Structured logging with Serilog
- [ ] Application crash reporting
- [ ] Diagnostic information collection
- [ ] Debug vs Release configurations

#### Days 18-19: Performance Optimization
**Objective:** Meet performance targets

**Tasks:**
- [ ] CPU usage profiling and optimization
- [ ] Memory allocation analysis
- [ ] Frame rate stabilization
- [ ] Async/await optimization
- [ ] Thread safety validation

#### Days 20-21: Documentation & Testing
**Objective:** Clear documentation and testing

**Tasks:**
- [ ] Unit test coverage >80%
- [ ] Integration test suite
- [ ] API documentation (XML comments)
- [ ] Architecture documentation
- [ ] Setup and installation guide

## 1.5 Success Criteria

### Functional Requirements
✅ **SDK Connection:** Successfully connects to iRacing  
✅ **Telemetry Capture:** 60Hz consistent data capture  
✅ **Auto-Reconnect:** Handles disconnections gracefully  
✅ **Configuration:** Persistent settings system working  
✅ **Basic Overlay:** Simple transparent overlay displays data  

### Performance Requirements
✅ **CPU Usage:** <5% during active telemetry  
✅ **Memory Usage:** <100MB total application memory  
✅ **Latency:** <1ms telemetry processing time  
✅ **Stability:** 4+ hour continuous operation without issues  
✅ **No Memory Leaks:** Memory usage stable over time  

### Quality Requirements
✅ **Test Coverage:** >80% unit test coverage  
✅ **Error Handling:** All I/O operations have error handling  
✅ **Logging:** Comprehensive logging infrastructure  
✅ **Documentation:** All public APIs documented  

## 1.6 Testing Strategy

### Unit Tests
- Telemetry service state management
- Configuration validation logic
- Circular buffer operations
- Connection retry logic

### Integration Tests
- SDK connection to live iRacing
- Configuration persistence
- Telemetry data flow
- Overlay window creation

### Performance Tests
- CPU usage under load
- Memory allocation patterns
- 60Hz processing consistency
- Long-running stability (4+ hours)

## 1.7 Known Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| SDK API changes | High | Pin SDK version, monitor releases |
| UAC/Permission issues | Medium | Document elevation requirements |
| Performance degradation | High | Continuous profiling, benchmarks |
| iRacing version compatibility | Medium | Test against multiple sim versions |

## 1.8 Phase 1 Deliverables

1. **Working Application** - Console app + simple overlay
2. **Core Libraries** - All Phase 1 projects compiled
3. **Test Suite** - Unit and integration tests
4. **Documentation** - Architecture and setup guides
5. **Performance Baseline** - Benchmark results documented

---

# PHASE 2: Core Features & Production Widgets
**Duration:** 3-4 weeks  
**Goal:** Feature-complete overlay matching Racelabs functionality

## 2.1 Overview

Phase 2 transforms the basic overlay into a feature-rich application with professional-grade widgets, matching and exceeding the free features of Racelabs. This phase focuses on building a comprehensive widget system and implementing all essential overlays.

## 2.2 Architecture Enhancements

### Enhanced Project Structure
```
iRacingOverlay/
├── src/
│   ├── iRacingOverlay.Widgets/          # NEW PROJECT
│   │   ├── Base/
│   │   │   ├── IWidget.cs
│   │   │   ├── WidgetBase.cs
│   │   │   └── WidgetConfiguration.cs
│   │   ├── Relative/
│   │   │   ├── RelativeWidget.cs
│   │   │   └── RelativeViewModel.cs
│   │   ├── Standings/
│   │   ├── Fuel/
│   │   ├── Timing/
│   │   ├── TrackMap/
│   │   ├── Spotter/
│   │   ├── Inputs/
│   │   └── TireTemp/
│   │
│   ├── iRacingOverlay.Visualization/     # NEW PROJECT
│   │   ├── Charts/
│   │   │   ├── RealtimeLineChart.cs
│   │   │   └── HistogramChart.cs
│   │   ├── Gauges/
│   │   │   ├── CircularGauge.cs
│   │   │   ├── LinearGauge.cs
│   │   │   └── RevLights.cs
│   │   └── Indicators/
│   │       ├── StatusIndicator.cs
│   │       └── ProgressBar.cs
│   │
│   └── iRacingOverlay.Analytics/         # NEW PROJECT
│       ├── LapAnalysis/
│       ├── SectorTiming/
│       ├── FuelCalculation/
│       └── DeltaCalculation/
```

## 2.3 Widget System Architecture

### Base Widget Interface
```csharp
public interface IWidget : INotifyPropertyChanged, IDisposable
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    
    FrameworkElement View { get; }
    WidgetConfiguration Configuration { get; set; }
    
    bool IsVisible { get; set; }
    bool IsEnabled { get; set; }
    
    void Initialize();
    void UpdateTelemetry(TelemetryData data);
    void UpdateConfiguration(WidgetConfiguration config);
    
    Task<bool> ValidateAsync();
}

public abstract class WidgetBase : IWidget
{
    protected readonly ILogger Logger;
    protected readonly Dispatcher UiDispatcher;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        UiDispatcher.Invoke(() => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName))
        );
    }
}
```

## 2.4 Core Widgets Implementation

### Widget 1: Relative Position Display
**Purpose:** Show nearby drivers (±3 positions)  
**Inspiration:** Racelabs Relative + iRacing native

**Features:**
- Dynamic driver list (scrolling)
- Position, name, gap, last lap, class
- Color coding by class
- License level indicators
- iRating display
- Pit status indicators

**Configuration Options:**
- Number of drivers shown (3-10)
- Column selection (customizable data)
- Update frequency
- Color schemes
- Font size and style

**Data Required:**
```csharp
public class RelativeDriverData
{
    public int Position { get; set; }
    public string DriverName { get; set; }
    public string CarNumber { get; set; }
    public CarClass Class { get; set; }
    public float GapAhead { get; set; }
    public float GapBehind { get; set; }
    public TimeSpan LastLapTime { get; set; }
    public int IRating { get; set; }
    public LicenseLevel License { get; set; }
    public bool IsInPit { get; set; }
}
```

### Widget 2: Standings Board
**Purpose:** Full field standings overview  
**Inspiration:** Racelabs Standings

**Features:**
- All drivers in session
- Multi-class support with filtering
- Position, name, laps, gap to leader
- Fast lap highlighting
- Pit stop tracking
- Incident points
- Grid/vertical layout options

**Layout Options:**
- Compact (condensed info)
- Standard (balanced)
- Detailed (all data)
- Horizontal bar
- Vertical list

### Widget 3: Fuel Calculator
**Purpose:** Fuel management and strategy  
**Inspiration:** Racelabs Fuel Calculator

**Features:**
- Current fuel level
- Laps remaining
- Average fuel consumption
- Fuel needed to finish
- Fuel needed for X laps
- Auto-refuel amount
- Low fuel warning
- Consumption trend graph

**Calculations:**
```csharp
public class FuelCalculator
{
    public float CurrentFuel { get; set; }
    public float AverageFuelPerLap { get; set; }
    public float EstimatedLapsRemaining => CurrentFuel / AverageFuelPerLap;
    
    public float CalculateFuelForLaps(int laps, float safetyMargin = 1.05f)
    {
        return laps * AverageFuelPerLap * safetyMargin;
    }
    
    public float CalculateRefuelAmount(int remainingLaps, float safetyMargin = 1.05f)
    {
        var needed = CalculateFuelForLaps(remainingLaps, safetyMargin);
        return Math.Max(0, needed - CurrentFuel);
    }
}
```

### Widget 4: Timing & Scoring
**Purpose:** Personal timing data  
**Inspiration:** iRacing native + enhancements

**Features:**
- Current lap time
- Best lap time
- Last lap time
- Sector times (S1, S2, S3)
- Delta to best lap
- Delta to optimal lap
- Delta to session best
- Position in class/overall

**Advanced Delta System:**
```csharp
public class DeltaCalculator
{
    private List<float> _bestLapDistances;
    private List<float> _bestLapTimes;
    
    public float CalculateDelta(float currentDistance, float currentLapTime)
    {
        // Interpolate from best lap data at current distance
        var referenceTime = InterpolateTime(currentDistance);
        return currentLapTime - referenceTime;
    }
    
    public DeltaInfo CalculateOptimalLap()
    {
        // Calculate theoretical best from best sector times
        // Combines best S1 + best S2 + best S3
    }
}
```

### Widget 5: Track Map
**Purpose:** Visual track position overview  
**Inspiration:** Racelabs Track Map (Pro), iOverlay

**Features:**
- 2D top-down track representation
- All car positions
- Player highlight
- Class color coding
- Incident locations
- Pit lane indication
- Zoom/pan controls
- Corner numbers (optional)

**Rendering Approach:**
```csharp
public class TrackMapRenderer
{
    private readonly SKCanvas _canvas;
    private readonly TrackGeometry _track;
    
    public void RenderMap(IEnumerable<CarPosition> cars)
    {
        // Render track outline
        DrawTrackPath(_track.Path);
        
        // Render pit lane
        DrawPitLane(_track.PitPath);
        
        // Render cars
        foreach (var car in cars)
        {
            DrawCar(car.Position, car.Class, car.IsPlayer);
        }
    }
}
```

### Widget 6: Input Telemetry Display
**Purpose:** Real-time input visualization  
**Inspiration:** Racelabs Input Telemetry (Pro)

**Features:**
- Steering wheel visual
- Throttle bar/graph
- Brake bar/graph
- Clutch bar/graph
- Gear indicator
- Rev lights
- Boost meter (for applicable cars)
- Historical trace option

**Display Modes:**
- Bars (vertical/horizontal)
- Gauges (circular)
- Graph (time series)
- Combined view

### Widget 7: Spotter/Radar
**Purpose:** Situational awareness  
**Inspiration:** Racelabs Spotter, Helicorsa

**Features:**
- 360° car proximity radar
- Left/right/behind indicators
- Distance to nearest car
- Visual proximity warnings
- Audio cues (optional)
- Customizable sensitivity
- Cone of vision highlighting

### Widget 8: Tire Temperature Monitor
**Purpose:** Tire management  
**Inspiration:** Professional telemetry systems

**Features:**
- 4 tire display (FL, FR, RL, RR)
- 3-zone temperature per tire (Inner, Middle, Outer)
- Pressure readings
- Wear percentage
- Color-coded temperature ranges
- Ideal temperature range overlay
- Temperature trend arrows

**Data Structure:**
```csharp
public class TireData
{
    public TireTemperature FrontLeft { get; set; }
    public TireTemperature FrontRight { get; set; }
    public TireTemperature RearLeft { get; set; }
    public TireTemperature RearRight { get; set; }
}

public class TireTemperature
{
    public float InnerTemp { get; set; }
    public float MiddleTemp { get; set; }
    public float OuterTemp { get; set; }
    public float Pressure { get; set; }
    public float WearPercent { get; set; }
    
    public float AverageTemp => (InnerTemp + MiddleTemp + OuterTemp) / 3;
}
```

## 2.5 Implementation Roadmap

### Week 1: Widget System Foundation

#### Days 1-3: Widget Architecture
**Tasks:**
- [ ] Design and implement widget base classes
- [ ] Create widget lifecycle management
- [ ] Build widget registration system
- [ ] Implement widget configuration system
- [ ] Create widget factory pattern

#### Days 4-7: First Three Widgets
**Tasks:**
- [ ] Implement Relative Position widget
- [ ] Implement Standings widget
- [ ] Implement Fuel Calculator widget
- [ ] Add unit tests for each widget
- [ ] Performance testing

### Week 2: Advanced Widgets

#### Days 8-10: Timing & Track Map
**Tasks:**
- [ ] Implement Timing & Scoring widget
- [ ] Develop delta calculation system
- [ ] Create Track Map widget
- [ ] Implement track geometry parser
- [ ] Add car position rendering

#### Days 11-14: Input & Monitoring Widgets
**Tasks:**
- [ ] Implement Input Telemetry widget
- [ ] Create various visualization modes
- [ ] Implement Spotter/Radar widget
- [ ] Build Tire Temperature widget
- [ ] Add visual indicators and gauges

### Week 3: Widget Management & Layout

#### Days 15-17: Widget Manager System
**Tasks:**
- [ ] Build widget positioning system
- [ ] Implement layout save/load
- [ ] Create multi-layout support
- [ ] Add widget show/hide controls
- [ ] Implement widget locking

**Layout System:**
```csharp
public class LayoutManager
{
    private Dictionary<string, WidgetLayout> _layouts;
    private string _activeLayoutId;
    
    public void SaveLayout(string name, IEnumerable<WidgetPlacement> placements)
    {
        var layout = new WidgetLayout
        {
            Name = name,
            Widgets = placements.ToList()
        };
        _layouts[name] = layout;
        PersistLayout(layout);
    }
    
    public void LoadLayout(string name)
    {
        if (_layouts.TryGetValue(name, out var layout))
        {
            ApplyLayout(layout);
            _activeLayoutId = name;
        }
    }
}
```

#### Days 18-21: Configuration & Themes
**Tasks:**
- [ ] Per-widget configuration UI
- [ ] Global overlay settings
- [ ] Theme system implementation
- [ ] Custom color schemes
- [ ] Font customization

### Week 4: Polish & Testing

#### Days 22-24: Performance Optimization
**Tasks:**
- [ ] Widget rendering optimization
- [ ] Data update batching
- [ ] Memory usage optimization
- [ ] CPU profiling and optimization
- [ ] Frame rate stabilization

#### Days 25-28: Testing & Documentation
**Tasks:**
- [ ] Comprehensive widget testing
- [ ] Multi-session stability testing
- [ ] Performance regression testing
- [ ] Widget developer documentation
- [ ] User configuration guide

## 2.6 Advanced Features

### Alert System
```csharp
public class AlertRule
{
    public string Id { get; set; }
    public string Name { get; set; }
    public AlertCondition Condition { get; set; }
    public AlertAction Action { get; set; }
    public bool IsEnabled { get; set; }
}

public enum AlertCondition
{
    LowFuel,                    // Fuel < X laps
    TireTemperature,            // Temp outside ideal range
    FastestLap,                 // New session fastest lap
    PositionGained,             // Moved up positions
    DamageReceived,             // Car damage threshold
    SlowLap,                    // Lap X% slower than average
    PitWindowOpen               // Pit window opening soon
}
```

### Multi-Class Support
- Automatic class detection
- Per-class filtering
- Class-specific coloring
- Class standings toggle
- Overall vs. class position

### Session Type Adaptation
- Practice: Focus on lap times, setup data
- Qualifying: Delta times, position tracking
- Race: Position, fuel, tire management
- Auto-adapt widget visibility per session type

## 2.7 Success Criteria

### Functional Requirements
✅ **8+ Production Widgets:** All core widgets implemented  
✅ **Widget System:** Flexible, extensible architecture  
✅ **Layout Management:** Multiple saveable layouts  
✅ **Configuration:** Per-widget customization  
✅ **Theme System:** Multiple color schemes  

### Performance Requirements
✅ **CPU Usage:** <8% with all widgets active  
✅ **Memory Usage:** <150MB with full feature set  
✅ **60 FPS:** Smooth overlay rendering  
✅ **Widget Updates:** <16ms per update cycle  
✅ **Stability:** 6+ hour endurance race stable  

### Quality Requirements
✅ **Test Coverage:** >75% unit test coverage  
✅ **Widget Tests:** Each widget has dedicated tests  
✅ **Documentation:** Widget API fully documented  
✅ **User Guide:** Configuration documentation  

## 2.8 Phase 2 Deliverables

1. **Feature-Complete Overlay** - Production-ready app
2. **Widget Library** - 8+ professional widgets
3. **Configuration System** - Full customization support
4. **Documentation** - User and developer guides
5. **Test Suite** - Comprehensive test coverage
6. **Performance Report** - Benchmark results

---

# PHASE 3: Web Configuration Interface
**Duration:** 3-4 weeks  
**Goal:** Modern web-based configuration with drag-and-drop layout designer

## 3.1 Overview

Phase 3 replaces manual configuration editing with a beautiful, modern web interface featuring real-time overlay preview, drag-and-drop layout design, and cloud sync capabilities. This significantly improves user experience compared to competitors.

## 3.2 Technology Stack

### Backend
- **ASP.NET Core 8.0** Web API
- **SignalR** for real-time communication
- **Entity Framework Core** for local database
- **JWT Authentication** for cloud features
- **Swagger/OpenAPI** for API documentation

### Frontend
- **React 18** with TypeScript
- **Vite** for blazing fast development
- **TailwindCSS** for styling
- **React DnD** for drag-and-drop
- **Recharts** for data visualization
- **React Query** for state management

## 3.3 Architecture

```
┌──────────────────────────────────────────────────────────┐
│                    Web Browser                            │
│  ┌────────────────────────────────────────────────────┐  │
│  │         React TypeScript Frontend                  │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌─────────┐ │  │
│  │  │ Layout       │  │ Widget       │  │ Theme   │ │  │
│  │  │ Designer     │  │ Config       │  │ Editor  │ │  │
│  │  └──────────────┘  └──────────────┘  └─────────┘ │  │
│  │  ┌──────────────────────────────────────────────┐ │  │
│  │  │     Live Overlay Preview Component           │ │  │
│  │  └──────────────────────────────────────────────┘ │  │
│  └────────────────────────────────────────────────────┘  │
└────────────┬─────────────────────────────┬───────────────┘
             │ SignalR WebSocket           │ REST API
             │ (Live Updates)              │ (Config CRUD)
┌────────────▼─────────────────────────────▼───────────────┐
│              ASP.NET Core Web API                         │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │  SignalR     │  │  Config      │  │  Auth         │  │
│  │  Hub         │  │  Service     │  │  Service      │  │
│  └──────────────┘  └──────────────┘  └───────────────┘  │
└────────────┬──────────────────────────────────────────────┘
             │ IPC / Shared Memory
┌────────────▼──────────────────────────────────────────────┐
│                  WPF Overlay Application                   │
│                (Receives Live Config Updates)              │
└────────────────────────────────────────────────────────────┘
```

## 3.4 Core Features

### 3.4.1 Layout Designer

**Drag-and-Drop Canvas:**
- Visual representation of overlay
- Real-time widget positioning
- Snap-to-grid functionality
- Alignment guides
- Multi-select and group operations
- Undo/redo support

**Widget Manipulation:**
```typescript
interface WidgetPlacement {
  widgetId: string;
  position: { x: number; y: number };
  size: { width: number; height: number };
  zIndex: number;
  opacity: number;
  isVisible: boolean;
  isLocked: boolean;
}

interface Layout {
  id: string;
  name: string;
  widgets: WidgetPlacement[];
  screenSize: { width: number; height: number };
  createdAt: Date;
  updatedAt: Date;
}
```

**Features:**
- Widget library sidebar
- Drag widgets onto canvas
- Resize handles with constraints
- Rotation support (for some widgets)
- Layer management (z-index)
- Widget cloning
- Precise positioning (numeric input)

### 3.4.2 Widget Configuration Panel

**Dynamic Form Generation:**
```typescript
interface WidgetConfig {
  widgetId: string;
  widgetType: string;
  properties: WidgetProperty[];
}

interface WidgetProperty {
  name: string;
  type: 'string' | 'number' | 'boolean' | 'color' | 'select' | 'multiselect';
  value: any;
  label: string;
  description?: string;
  validation?: ValidationRule[];
  options?: SelectOption[];
}

// Example: Relative Widget Config
const relativeConfig: WidgetConfig = {
  widgetId: 'relative-1',
  widgetType: 'relative',
  properties: [
    {
      name: 'driversShown',
      type: 'number',
      value: 5,
      label: 'Number of Drivers',
      description: 'How many drivers to show (3-10)',
      validation: [{ min: 3, max: 10 }]
    },
    {
      name: 'showIRating',
      type: 'boolean',
      value: true,
      label: 'Show iRating'
    },
    {
      name: 'colorScheme',
      type: 'select',
      value: 'class',
      label: 'Color Scheme',
      options: [
        { value: 'class', label: 'By Class' },
        { value: 'custom', label: 'Custom' }
      ]
    }
  ]
};
```

### 3.4.3 Theme System

**Theme Structure:**
```typescript
interface Theme {
  id: string;
  name: string;
  colors: {
    primary: string;
    secondary: string;
    background: string;
    foreground: string;
    accent: string;
    success: string;
    warning: string;
    danger: string;
    // Class-specific colors
    lmp1: string;
    lmp2: string;
    gte: string;
    gt3: string;
  };
  fonts: {
    primary: string;
    monospace: string;
  };
  borders: {
    radius: number;
    width: number;
    style: 'solid' | 'dashed' | 'dotted';
  };
  opacity: {
    widget: number;
    overlay: number;
  };
}
```

**Pre-built Themes:**
- Dark Mode (default)
- Light Mode
- High Contrast
- Racelabs-inspired
- iRacing Native
- Minimal
- Neon/Cyberpunk
- Custom (user-created)

### 3.4.4 Live Preview

**Real-time Preview System:**
- Accurate 1:1 scale preview
- Updates as you configure
- Simulated telemetry data in preview
- Toggle between mock/live data
- Multiple monitor preview
- Full-screen preview mode

```typescript
interface PreviewOptions {
  scale: number;           // 0.5 = 50% scale
  showGrid: boolean;
  showRulers: boolean;
  useLiveData: boolean;    // True = use actual telemetry
  simulateData: boolean;   // Generate fake data for preview
  screenSize: { width: number; height: number };
}
```

## 3.5 Implementation Roadmap

### Week 1: Backend Infrastructure

#### Days 1-3: ASP.NET Core Setup
**Tasks:**
- [ ] Create ASP.NET Core Web API project
- [ ] Configure dependency injection
- [ ] Set up Entity Framework Core
- [ ] Create configuration database schema
- [ ] Implement repository pattern

**API Endpoints:**
```csharp
// Layout Endpoints
GET    /api/layouts                    // Get all layouts
GET    /api/layouts/{id}               // Get specific layout
POST   /api/layouts                    // Create new layout
PUT    /api/layouts/{id}               // Update layout
DELETE /api/layouts/{id}               // Delete layout

// Widget Endpoints
GET    /api/widgets                    // Get available widgets
GET    /api/widgets/{id}/config        // Get widget schema
POST   /api/widgets/{id}/validate      // Validate widget config

// Theme Endpoints
GET    /api/themes                     // Get all themes
POST   /api/themes                     // Create custom theme
PUT    /api/themes/{id}                // Update theme

// Config Endpoints
GET    /api/config                     // Get full configuration
PUT    /api/config                     // Update configuration
POST   /api/config/export              // Export config file
POST   /api/config/import              // Import config file
```

#### Days 4-7: SignalR Real-time Communication
**Tasks:**
- [ ] Set up SignalR hub
- [ ] Implement bidirectional communication
- [ ] Create telemetry broadcast system
- [ ] Add configuration update notifications
- [ ] Connection state management

**SignalR Hub:**
```csharp
public class OverlayHub : Hub
{
    public async Task SendConfigUpdate(Configuration config)
    {
        // Broadcast to overlay app
        await Clients.All.SendAsync("ConfigUpdated", config);
    }
    
    public async Task RequestLiveTelemetry()
    {
        // Request overlay app to send telemetry
        await Clients.All.SendAsync("TelemetryRequested");
    }
    
    public async Task BroadcastTelemetry(TelemetryData data)
    {
        // Send to web clients for live preview
        await Clients.All.SendAsync("TelemetryUpdate", data);
    }
}
```

### Week 2: Frontend Foundation

#### Days 8-10: React Project Setup
**Tasks:**
- [ ] Initialize React + TypeScript project with Vite
- [ ] Set up TailwindCSS
- [ ] Configure React Router
- [ ] Set up React Query
- [ ] Create base component library

**Project Structure:**
```
web-ui/
├── src/
│   ├── components/
│   │   ├── common/           # Buttons, inputs, cards, etc.
│   │   ├── layout/           # App shell, navigation
│   │   └── overlay/          # Overlay-specific components
│   ├── features/
│   │   ├── designer/         # Layout designer
│   │   ├── widgets/          # Widget configuration
│   │   ├── themes/           # Theme editor
│   │   └── preview/          # Live preview
│   ├── hooks/                # Custom React hooks
│   ├── services/             # API clients
│   ├── store/                # State management
│   ├── types/                # TypeScript types
│   └── utils/                # Utilities
├── public/
└── package.json
```

#### Days 11-14: Core UI Components
**Tasks:**
- [ ] Design system implementation
- [ ] Navigation and routing
- [ ] Dashboard/home page
- [ ] Settings page
- [ ] API integration layer

### Week 3: Feature Implementation

#### Days 15-18: Layout Designer
**Tasks:**
- [ ] Canvas component with zoom/pan
- [ ] Drag-and-drop functionality (React DnD)
- [ ] Widget library sidebar
- [ ] Property panel
- [ ] Snap-to-grid and alignment
- [ ] Layer management
- [ ] Undo/redo system

**Example Designer Component:**
```typescript
const LayoutDesigner: React.FC = () => {
  const [widgets, setWidgets] = useState<WidgetPlacement[]>([]);
  const [selectedWidget, setSelectedWidget] = useState<string | null>(null);
  const [zoom, setZoom] = useState(1);
  
  const handleDrop = (item: DragItem, position: Position) => {
    const newWidget: WidgetPlacement = {
      widgetId: generateId(),
      position,
      size: getDefaultSize(item.widgetType),
      zIndex: widgets.length,
      opacity: 1,
      isVisible: true,
      isLocked: false
    };
    setWidgets([...widgets, newWidget]);
  };
  
  return (
    <div className="layout-designer">
      <WidgetLibrary />
      <Canvas
        widgets={widgets}
        zoom={zoom}
        onDrop={handleDrop}
        onSelect={setSelectedWidget}
      />
      {selectedWidget && (
        <PropertyPanel
          widget={widgets.find(w => w.widgetId === selectedWidget)}
          onChange={handleWidgetUpdate}
        />
      )}
    </div>
  );
};
```

#### Days 19-21: Widget Configuration
**Tasks:**
- [ ] Dynamic form generation
- [ ] Per-widget config panels
- [ ] Configuration validation
- [ ] Real-time config preview
- [ ] Configuration presets

### Week 4: Polish & Testing

#### Days 22-24: Live Preview System
**Tasks:**
- [ ] Preview component with telemetry simulation
- [ ] WebSocket integration for live data
- [ ] Multi-monitor preview
- [ ] Full-screen preview mode
- [ ] Preview controls (play/pause/step)

#### Days 25-28: Final Features
**Tasks:**
- [ ] Theme editor implementation
- [ ] Import/export functionality
- [ ] User preferences
- [ ] Help documentation
- [ ] Tutorial/onboarding

## 3.6 Integration with WPF Overlay

**IPC Communication:**
```csharp
// In WPF App
public class ConfigurationSynchronizer
{
    private readonly HubConnection _hubConnection;
    
    public async Task ConnectAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/hubs/overlay")
            .Build();
            
        _hubConnection.On<Configuration>("ConfigUpdated", 
            async config => await ApplyConfigurationAsync(config));
            
        await _hubConnection.StartAsync();
    }
    
    private async Task ApplyConfigurationAsync(Configuration config)
    {
        // Update overlay in real-time
        await Application.Current.Dispatcher.InvokeAsync(() => 
        {
            _overlayManager.ApplyConfiguration(config);
        });
    }
}
```

## 3.7 Advanced Features

### Cloud Sync (Optional)
- User authentication
- Cloud configuration storage
- Multi-device synchronization
- Backup and restore
- Share layouts with community

### Analytics Dashboard
- Session statistics
- Performance trends
- Lap time analysis
- Visual charts and graphs

### Community Features
- Layout sharing
- Theme marketplace
- Widget templates
- User ratings and reviews

## 3.8 Success Criteria

### Functional Requirements
✅ **Layout Designer:** Drag-and-drop interface working  
✅ **Live Preview:** Real-time preview with mock/live data  
✅ **Config Sync:** Instant overlay updates (<100ms)  
✅ **Theme System:** Multiple themes, custom creation  
✅ **Import/Export:** Configuration backup/restore  

### Performance Requirements
✅ **Load Time:** <2 seconds initial load  
✅ **Config Update:** <100ms update propagation  
✅ **Preview FPS:** 60 FPS smooth preview  
✅ **Memory:** <50MB web app memory  

### Quality Requirements
✅ **Responsive Design:** Works on 1024px+ screens  
✅ **Cross-browser:** Chrome, Edge, Firefox support  
✅ **Accessibility:** WCAG 2.1 AA compliance  
✅ **Documentation:** User manual and video tutorials  

## 3.9 Phase 3 Deliverables

1. **Web Configuration App** - Full React TypeScript UI
2. **ASP.NET Core API** - Backend with SignalR
3. **Live Preview System** - Real-time preview engine
4. **Documentation** - User guides and API docs
5. **Deployment Package** - Installer with web server

---

# PHASE 4: AI & Advanced Analytics
**Duration:** 6-8 weeks  
**Goal:** Intelligent racing assistant with machine learning

## 4.1 Overview

Phase 4 introduces cutting-edge AI features that provide genuine competitive advantages. Using local ONNX models, the overlay becomes an intelligent racing coach offering predictions, insights, and optimization suggestions—features no commercial competitor currently offers.

## 4.2 Technology Stack

### Machine Learning
- **ONNX Runtime** for local inference
- **ML.NET** for model training (.NET integration)
- **Python** for initial model development
- **NumPy/Pandas** for data processing
- **Scikit-learn** for traditional ML
- **PyTorch** for deep learning models

### Data Storage
- **SQLite** for telemetry history
- **Parquet** for efficient data storage
- **Time-series database** (optional)

## 4.3 AI Features Overview

### Feature 1: Lap Time Prediction
**Goal:** Predict final lap time before lap completion

**Inputs:**
- Current sector times
- Tire temperature trends
- Fuel load
- Track conditions
- Weather data
- Driver performance history

**Output:**
- Predicted final lap time
- Confidence interval
- Comparison to best lap

**Use Cases:**
- Know if current lap will be PB before finishing
- Adjust strategy mid-lap
- Qualifying session optimization

**Model Architecture:**
```python
# Gradient Boosting Regressor or LSTM
# Input features: ~20-30 telemetry parameters
# Output: Predicted lap time (seconds)

import lightgbm as lgb

class LapTimePredictor:
    def __init__(self):
        self.model = lgb.LGBMRegressor(
            n_estimators=100,
            learning_rate=0.05,
            max_depth=8
        )
        
    def prepare_features(self, telemetry_data):
        return {
            'sector1_time': telemetry_data.sector1,
            'sector2_time': telemetry_data.sector2,
            'current_sector_progress': telemetry_data.sector3_progress,
            'avg_speed_s3': telemetry_data.avg_speed_sector3,
            'tire_temp_avg': telemetry_data.tire_temp_average,
            'fuel_level': telemetry_data.fuel,
            'track_temp': telemetry_data.track_temperature,
            # ... 20 more features
        }
        
    def predict(self, features):
        prediction = self.model.predict([features])
        confidence = self.calculate_confidence(features)
        return prediction[0], confidence
```

### Feature 2: Racing Line Optimization
**Goal:** Suggest optimal racing line and braking points

**Inputs:**
- Track geometry
- Car characteristics
- Current lap telemetry
- Historical best laps
- Tire condition
- Fuel load

**Output:**
- Optimal racing line overlay
- Braking point suggestions
- Apex speed targets
- Potential time gains per corner

**Visualization:**
- Color-coded track map
- Green = optimal line
- Yellow/Red = suboptimal
- Time gain/loss indicators

**Algorithm:**
```python
class RacingLineOptimizer:
    def __init__(self):
        self.track_model = None
        self.physics_model = None
        
    def calculate_optimal_line(self, track, car_data):
        # Use optimization algorithms (e.g., genetic algorithms)
        # Minimize lap time while respecting physics constraints
        
        segments = self.discretize_track(track)
        optimal_path = []
        
        for segment in segments:
            # Calculate optimal speed and line for segment
            speed = self.calculate_corner_speed(
                segment.radius, 
                car_data.grip_level, 
                car_data.downforce
            )
            line = self.calculate_racing_line(segment, speed)
            optimal_path.append((line, speed))
            
        return optimal_path
        
    def calculate_time_gain(self, current_line, optimal_line):
        # Compare current path to optimal
        current_time = self.calculate_segment_time(current_line)
        optimal_time = self.calculate_segment_time(optimal_line)
        return current_time - optimal_time
```

### Feature 3: Performance Pattern Recognition
**Goal:** Identify driving patterns and areas for improvement

**Inputs:**
- Multi-session telemetry
- Lap times over time
- Consistency metrics
- Incident data

**Output:**
- Driving style analysis
- Consistency score
- Weak corner identification
- Improvement suggestions
- Fatigue detection

**Metrics:**
```csharp
public class PerformanceAnalyzer
{
    public ConsistencyScore CalculateConsistency(List<float> lapTimes)
    {
        var avg = lapTimes.Average();
        var stdDev = CalculateStandardDeviation(lapTimes);
        var coefficient = stdDev / avg;
        
        return new ConsistencyScore
        {
            Average = avg,
            StandardDeviation = stdDev,
            CoefficientOfVariation = coefficient,
            Grade = GradeConsistency(coefficient),
            ConsistentLaps = CountLapsWithinRange(lapTimes, avg, stdDev)
        };
    }
    
    public IEnumerable<Corner> IdentifyWeakCorners(TrackData track, TelemetryHistory history)
    {
        var corners = track.Corners;
        var weakCorners = new List<Corner>();
        
        foreach (var corner in corners)
        {
            var timeLoss = CalculateTimeLossAtCorner(corner, history);
            if (timeLoss > THRESHOLD)
            {
                weakCorners.Add(new
                {
                    Corner = corner,
                    AverageTimeLoss = timeLoss,
                    Frequency = CalculateInconsistency(corner, history),
                    Suggestions = GenerateSuggestions(corner, history)
                });
            }
        }
        
        return weakCorners.OrderByDescending(c => c.AverageTimeLoss);
    }
}
```

### Feature 4: Anomaly Detection
**Goal:** Detect unusual patterns indicating issues

**Targets:**
- Tire degradation anomalies
- Engine performance drops
- Unusual fuel consumption
- Damage detection
- Setup problems

**Approach:**
```python
from sklearn.ensemble import IsolationForest

class AnomalyDetector:
    def __init__(self):
        self.models = {
            'tire': IsolationForest(contamination=0.1),
            'engine': IsolationForest(contamination=0.05),
            'fuel': IsolationForest(contamination=0.08)
        }
        
    def detect_tire_anomaly(self, tire_data):
        features = self.extract_tire_features(tire_data)
        score = self.models['tire'].score_samples([features])
        
        if score < ANOMALY_THRESHOLD:
            return AnomalyAlert(
                type='tire_degradation',
                severity='warning',
                message='Unusual tire wear detected',
                recommendation='Consider pit stop'
            )
            
    def detect_engine_anomaly(self, engine_data):
        # Monitor RPM patterns, power delivery, etc.
        pass
```

### Feature 5: Predictive Pit Strategy
**Goal:** Optimize pit stop timing using ML

**Inputs:**
- Fuel consumption rate
- Tire degradation rate
- Gap to competitors
- Historical pit window data
- Race time remaining
- Safety car probability

**Output:**
- Optimal pit window
- Fuel load recommendation
- Tire strategy
- Expected track position after pit

**Algorithm:**
```csharp
public class PitStrategyOptimizer
{
    private readonly IMLPredictor _mlPredictor;
    
    public PitStrategy CalculateOptimalStrategy(RaceState state)
    {
        var predictions = new
        {
            FuelLapsRemaining = PredictFuelLaps(state.FuelLevel, state.ConsumptionRate),
            TireLapsRemaining = PredictTireLaps(state.TireWear, state.TrackConditions),
            SafetyCarProbability = PredictSafetyCar(state.LapsRemaining),
            TrafficWindow = PredictTrafficWindows(state.Standings)
        };
        
        var strategies = GeneratePitScenarios(predictions);
        var scored = strategies.Select(s => new
        {
            Strategy = s,
            Score = ScoreStrategy(s, state, predictions)
        });
        
        return scored.OrderByDescending(s => s.Score).First().Strategy;
    }
}
```

## 4.4 Implementation Roadmap

### Week 1-2: Data Collection Infrastructure

#### Days 1-5: Telemetry Database
**Tasks:**
- [ ] Design telemetry database schema
- [ ] Implement data collection service
- [ ] Create efficient storage (Parquet/SQLite)
- [ ] Build data export functionality
- [ ] Implement data validation and cleaning

**Database Schema:**
```sql
CREATE TABLE Sessions (
    SessionId UUID PRIMARY KEY,
    TrackId INT,
    CarId INT,
    StartTime DATETIME,
    EndTime DATETIME,
    SessionType VARCHAR(20),
    WeatherConditions JSON
);

CREATE TABLE Laps (
    LapId UUID PRIMARY KEY,
    SessionId UUID FOREIGN KEY,
    LapNumber INT,
    LapTime FLOAT,
    Sector1 FLOAT,
    Sector2 FLOAT,
    Sector3 FLOAT,
    IsValid BOOLEAN,
    TelemetryData BLOB
);

CREATE TABLE TelemetryPoints (
    PointId UUID PRIMARY KEY,
    LapId UUID FOREIGN KEY,
    Timestamp BIGINT,
    Speed FLOAT,
    RPM FLOAT,
    Throttle FLOAT,
    Brake FLOAT,
    Gear INT,
    TireTemp JSON,
    -- ... 50+ more columns
);
```

#### Days 6-10: Feature Engineering
**Tasks:**
- [ ] Define feature extraction pipeline
- [ ] Implement statistical features
- [ ] Create domain-specific features
- [ ] Build feature validation
- [ ] Implement feature normalization

### Week 3-4: Model Development

#### Days 11-15: Lap Time Prediction Model
**Tasks:**
- [ ] Collect training data (100+ sessions)
- [ ] Feature selection and engineering
- [ ] Train multiple model architectures
- [ ] Model evaluation and comparison
- [ ] Export to ONNX format

**Training Pipeline:**
```python
# train_lap_predictor.py
import pandas as pd
from sklearn.model_selection import train_test_split
import lightgbm as lgb
import onnxmltools

# Load data
data = pd.read_parquet('telemetry_data.parquet')
X = extract_features(data)
y = data['lap_time']

# Split
X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2)

# Train
model = lgb.LGBMRegressor(n_estimators=200, max_depth=10)
model.fit(X_train, y_train)

# Evaluate
predictions = model.predict(X_test)
mae = mean_absolute_error(y_test, predictions)
print(f"MAE: {mae:.3f} seconds")

# Export to ONNX
onnx_model = onnxmltools.convert_lightgbm(model)
onnxmltools.utils.save_model(onnx_model, 'lap_predictor.onnx')
```

#### Days 16-21: Racing Line & Performance Models
**Tasks:**
- [ ] Track geometry data collection
- [ ] Train racing line optimization model
- [ ] Develop performance pattern classifier
- [ ] Create anomaly detection models
- [ ] Export all models to ONNX

### Week 5: Integration & Inference

#### Days 22-26: ONNX Runtime Integration
**Tasks:**
- [ ] Add ONNX Runtime to C# project
- [ ] Create inference service
- [ ] Implement model loading and caching
- [ ] Build real-time prediction pipeline
- [ ] Add error handling and fallbacks

**Inference Service:**
```csharp
public class MLInferenceService
{
    private readonly InferenceSession _lapPredictorSession;
    private readonly InferenceSession _racingLineSession;
    
    public MLInferenceService()
    {
        _lapPredictorSession = new InferenceSession("models/lap_predictor.onnx");
        _racingLineSession = new InferenceSession("models/racing_line.onnx");
    }
    
    public async Task<PredictionResult> PredictLapTime(TelemetrySnapshot snapshot)
    {
        var features = ExtractFeatures(snapshot);
        var input = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", features)
        };
        
        using var results = await Task.Run(() => 
            _lapPredictorSession.Run(input));
            
        var output = results.First().AsEnumerable<float>().First();
        
        return new PredictionResult
        {
            PredictedTime = output,
            Confidence = CalculateConfidence(features),
            DeltaToBest = output - snapshot.BestLapTime
        };
    }
}
```

#### Days 27-28: ML-Enhanced Widgets
**Tasks:**
- [ ] Create Lap Prediction widget
- [ ] Build Racing Line overlay widget
- [ ] Implement Performance Analysis widget
- [ ] Add Anomaly Alert widget

### Week 6: Advanced Features

#### Days 29-32: Performance Analytics Dashboard
**Tasks:**
- [ ] Multi-session analysis UI
- [ ] Trend visualization
- [ ] Corner-by-corner analysis
- [ ] Driver comparison tools
- [ ] Progress tracking

#### Days 33-35: Model Improvement System
**Tasks:**
- [ ] Continuous learning pipeline
- [ ] User feedback collection
- [ ] A/B testing infrastructure
- [ ] Model versioning
- [ ] Automatic retraining triggers

### Week 7-8: Testing & Optimization

#### Days 36-42: Comprehensive Testing
**Tasks:**
- [ ] Model accuracy validation
- [ ] Performance testing (<50ms inference)
- [ ] Memory usage optimization
- [ ] Multi-track testing
- [ ] Edge case handling

#### Days 43-49: Documentation & Polish
**Tasks:**
- [ ] ML feature documentation
- [ ] Model training guide
- [ ] User guide for AI features
- [ ] Performance tuning guide
- [ ] Example scenarios

## 4.5 ML Widget Specifications

### Lap Predictor Widget
```csharp
public class LapPredictorWidget : WidgetBase
{
    private readonly IMLInferenceService _mlService;
    
    // Displayed Data
    public float PredictedTime { get; set; }
    public float Confidence { get; set; }
    public float DeltaToBest { get; set; }
    public string PredictionQuality { get; set; } // "High", "Medium", "Low"
    
    public override void UpdateTelemetry(TelemetryData data)
    {
        if (data.LapDistPct > 0.5f) // Only predict after 50% lap completion
        {
            var prediction = await _mlService.PredictLapTime(data);
            
            PredictedTime = prediction.PredictedTime;
            Confidence = prediction.Confidence;
            DeltaToBest = prediction.DeltaToBest;
            PredictionQuality = GetQualityLabel(Confidence);
            
            NotifyPropertyChanged();
        }
    }
}
```

### Performance Insights Widget
```csharp
public class PerformanceInsightsWidget : WidgetBase
{
    public ConsistencyScore Consistency { get; set; }
    public List<WeakCorner> WeakCorners { get; set; }
    public List<Suggestion> ImprovementSuggestions { get; set; }
    
    public override void UpdateTelemetry(TelemetryData data)
    {
        if (IsLapComplete(data))
        {
            var analysis = await _analyticsService.AnalyzeSession();
            
            Consistency = analysis.ConsistencyScore;
            WeakCorners = analysis.WeakCorners.Take(3).ToList();
            ImprovementSuggestions = GenerateSuggestions(analysis);
            
            NotifyPropertyChanged();
        }
    }
}
```

## 4.6 Success Criteria

### ML Model Performance
✅ **Lap Prediction Accuracy:** >85% within 0.5s  
✅ **Racing Line Optimization:** >90% alignment with pro drivers  
✅ **Anomaly Detection:** >90% detection rate, <5% false positives  
✅ **Inference Latency:** <50ms per prediction  
✅ **Model Size:** <50MB total for all models  

### System Performance
✅ **CPU Impact:** <3% additional CPU for ML  
✅ **Memory Impact:** <100MB additional memory  
✅ **Total System Load:** <12% CPU, <250MB RAM  
✅ **Responsiveness:** UI remains 60 FPS  

### Feature Completeness
✅ **5+ ML Features:** All planned features implemented  
✅ **4+ ML Widgets:** Dedicated UI for ML features  
✅ **Training Pipeline:** Automated model training  
✅ **User Feedback:** System for model improvement  
✅ **Documentation:** Complete ML feature docs  

## 4.7 Phase 4 Deliverables

1. **ML-Enhanced Application** - Full AI features integrated
2. **ONNX Models** - 5+ production-ready models
3. **Training Pipeline** - Automated model training system
4. **ML Widget Library** - 4+ intelligent widgets
5. **Analytics Dashboard** - Performance insights UI
6. **Documentation** - ML architecture and user guides
7. **Model Repository** - Version-controlled model storage

---

# CROSS-PHASE CONSIDERATIONS

## Development Principles

### Code Quality Standards
- **Test Coverage:** Maintain >75% across all phases
- **Performance:** Profile after every major feature
- **Documentation:** XML docs for all public APIs
- **Code Review:** All PRs require review
- **Static Analysis:** Use Roslyn analyzers

### Performance Monitoring
```csharp
public class PerformanceMonitor
{
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memCounter;
    
    public PerformanceMetrics GetCurrentMetrics()
    {
        return new PerformanceMetrics
        {
            CpuPercent = _cpuCounter.NextValue(),
            MemoryMB = _memCounter.NextValue() / 1024 / 1024,
            TelemetryHz = CalculateTelemetryFrequency(),
            OverlayFps = CalculateOverlayFps(),
            Timestamp = DateTime.UtcNow
        };
    }
    
    public void Alert(PerformanceMetrics metrics)
    {
        if (metrics.CpuPercent > MAX_CPU_PERCENT)
            Logger.Warn($"CPU usage high: {metrics.CpuPercent:F1}%");
            
        if (metrics.MemoryMB > MAX_MEMORY_MB)
            Logger.Warn($"Memory usage high: {metrics.MemoryMB:F0}MB");
    }
}
```

### Error Handling Strategy
```csharp
public abstract class ServiceBase
{
    protected readonly ILogger Logger;
    
    protected async Task<T> ExecuteWithErrorHandling<T>(
        Func<Task<T>> operation,
        string operationName)
    {
        try
        {
            Logger.LogDebug($"Starting {operationName}");
            var result = await operation();
            Logger.LogDebug($"Completed {operationName}");
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error in {operationName}");
            
            // Telemetry for diagnostics
            TelemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Operation"] = operationName,
                ["User"] = Environment.UserName
            });
            
            throw;
        }
    }
}
```

## Security Considerations

### Data Privacy
- All telemetry stays local by default
- Optional cloud sync with encryption
- No personal data collection without consent
- GDPR compliance for EU users

### Application Security
- Code signing for releases
- Secure configuration storage
- Input validation throughout
- SQL injection prevention
- XSS protection in web UI

## Deployment Strategy

### Release Process
1. **Alpha Testing** (internal)
2. **Beta Testing** (limited users)
3. **Release Candidate** (public beta)
4. **Production Release**
5. **Post-release monitoring**

### Versioning
- Semantic versioning (MAJOR.MINOR.PATCH)
- Clear changelog
- Breaking change notices
- Migration guides

### Distribution
- GitHub Releases
- Installer (MSI/MSIX)
- Auto-update system
- Portable version

## Community & Open Source

### Contribution Guidelines
- Code of conduct
- Contribution guide
- Issue templates
- PR templates
- Development setup guide

### Documentation
- User documentation
- Developer documentation
- API reference
- Video tutorials
- Example projects

### Community Engagement
- Discord server
- GitHub Discussions
- Regular updates
- Feature requests
- Bug reports

---

# SUCCESS METRICS

## Quantitative Goals

### Performance Targets (End of Phase 4)
| Metric | Target | Acceptable | Critical |
|--------|--------|------------|----------|
| CPU Usage | <12% | <15% | >20% |
| Memory Usage | <250MB | <300MB | >400MB |
| Telemetry Rate | 60Hz | 55Hz+ | <50Hz |
| Overlay FPS | 60 FPS | 50+ FPS | <45 FPS |
| ML Inference | <50ms | <75ms | >100ms |
| Startup Time | <3s | <5s | >8s |

### Feature Completeness
- **Widgets:** 10+ production-ready widgets
- **Layouts:** 5+ pre-built layouts
- **Themes:** 8+ themes included
- **ML Models:** 5+ trained models
- **Test Coverage:** >75% code coverage

### User Experience
- **Setup Time:** <10 minutes to first overlay
- **Configuration:** Intuitive web interface
- **Stability:** >95% session completion rate
- **Responsiveness:** All UI interactions <100ms

## Qualitative Goals

### User Feedback
- Positive reviews on GitHub
- Active community participation
- Feature requests showing engagement
- Bug reports with good repro steps

### Code Quality
- Clean architecture maintained
- Well-documented codebase
- Active development
- Regular releases

---

# CONCLUSION

This development plan provides a comprehensive roadmap for building a world-class iRacing overlay application. By following this phased approach:

1. **Phase 1** establishes a rock-solid foundation
2. **Phase 2** delivers feature parity with commercial solutions
3. **Phase 3** adds modern web-based configuration
4. **Phase 4** introduces unique AI-powered features

The result will be an application that not only competes with but exceeds commercial offerings like Racelabs, while remaining 100% free and open source.

---

**Document Version:** 2.0  
**Last Updated:** October 12, 2025  
**Maintained By:** Development Team  
**License:** Apache 2.0 (planned)