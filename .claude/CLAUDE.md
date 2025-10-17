# iRacing Telemetry Overlay Development Guide

## Project Context
This is a sophisticated real-time telemetry overlay system for iRacing, built with WPF and .NET 8.0. The application provides customizable on-screen widgets displaying live telemetry data extracted from the iRacing SDK. Features include dynamic layouts, color-coded status indicators, unit conversion, and immediate-apply configuration UI.

## Development Environment

### Core Architecture
- **Platform**: WPF desktop application (.NET 8.0) with XAML UI
- **Language**: C# with modern language features (pattern matching, LINQ, async/await)
- **SDK Integration**: iRacing SDK (SVappsLAB.iRacingTelemetrySDK v0.9.8.3) for real-time telemetry
- **Widget System**: Modular overlay architecture with WidgetBase abstract class
- **Settings**: JSON-based persistence with real-time event propagation

### iRacing SDK Quick Reference

**Package**: `SVappsLAB.iRacingTelemetrySDK` v0.9.8.3
**Documentation**: See [docs/SDK_MASTER_REFERENCE.md](../docs/SDK_MASTER_REFERENCE.md) for complete details

#### Adding New Telemetry Variables

1. **Add to RequiredTelemetryVars** attribute in `IRacingTelemetryService.cs`:
   ```csharp
   [RequiredTelemetryVars([
       // ... existing variables
       "NewVariable1",  // Your new variable (case-insensitive)
       "NewVariable2"
   ])]
   ```

2. **Add property to TelemetryData model** (`Core/Models/TelemetryData.cs`):
   ```csharp
   public class TelemetryData
   {
       // ... existing properties
       public float NewVariable1 { get; set; }
       public float NewVariable2 { get; set; }
   }
   ```

3. **Map in OnTelemetryUpdate()** (`IRacingTelemetryService.cs`):
   ```csharp
   var data = new Models.TelemetryData
   {
       // ... existing mappings
       NewVariable1 = sdkData.NewVariable1,
       NewVariable2 = sdkData.NewVariable2,
   };
   ```

4. **Rebuild project** - Source generator updates the SDK's TelemetryData struct

#### Event System Pattern

```csharp
// Connection state changes
_telemetryService.StatusChanged += OnStatusChanged;

private void OnStatusChanged(object? sender, ConnectionStatusEventArgs e)
{
    if (e.Status == ConnectionStatus.Connected)
    {
        // iRacing connected - show widgets
    }
    else if (e.Status == ConnectionStatus.Disconnected)
    {
        // iRacing disconnected - hide widgets
    }
}

// Telemetry updates (60 Hz)
_telemetryService.TelemetryUpdated += OnTelemetryUpdated;

private void OnTelemetryUpdated(object? sender, TelemetryData data)
{
    // Update UI on UI thread
    Dispatcher.Invoke(() => UpdateUI(data));
}
```

#### Variable Discovery

- **Find all variables**: Check [docs/iRacing_SDK_Variables_Reference.md](../docs/iRacing_SDK_Variables_Reference.md) (400+ variables documented)
- **Currently using**: 72 variables (see `IRacingTelemetryService.cs` RequiredTelemetryVars attribute)
- **Test with IBT files**: Record telemetry in iRacing (Ctrl+D) for offline testing

#### Common Variable Categories

- **Vehicle**: Speed, RPM, Gear, Throttle, Brake, Clutch, SteeringWheelAngle
- **Position**: Lap, LapDistPct, PlayerCarClassPosition, PlayerCarIdx
- **Timing**: LapLastLapTime, LapBestLapTime, LapCurrentLapTime, SessionTimeRemain
- **Multi-Car**: CarIdxLapDistPct[64], CarIdxOnPitRoad[64], CarIdxPosition[64] (all cars)
- **Lateral Spotter**: CarLeftRight (0=Clear, 1=Left, 2=Right, 3=Both)
- **Tires**: LFtempCL/CM/CR, LFwearL/M/R, LFpressure (all 4 corners)
- **Fluids**: FuelLevel, FuelLevelPct, WaterTemp, OilTemp
- **Environment**: AirTemp, TrackTemp, SessionFlags

#### Performance Notes

- **Update Rate**: 60 Hz (every ~16.67ms)
- **Thread Safety**: Events fire on SDK thread, use `Dispatcher.Invoke()` for UI updates
- **Array Handling**: Always null-check CarIdx arrays (64 elements, not all populated)
- **Enum Casting**: Cast SDK enums to int for storage: `(int)sdkData.CarLeftRight`

### Key Development Commands
```powershell
# Build & Run
dotnet build                                           # Build the solution
dotnet run --project src\iRacingOverlay.WPF           # Run the application
dotnet clean                                           # Clean build artifacts

# Testing (when implemented)
dotnet test                                            # Run test suite
dotnet test --logger "console;verbosity=detailed"     # Verbose test output

# Project Management
dotnet add package <PackageName>                       # Add NuGet package
dotnet restore                                         # Restore dependencies
dotnet publish -c Release                              # Create release build
```

### Development Mode
- **No Authentication**: Desktop application runs locally without auth requirements
- **Hot Reload**: Use Visual Studio 2022 or VS Code with C# Dev Kit for XAML hot reload
- **Debug Mode**: F5 in Visual Studio or `dotnet run` with debugger attached
- **Live Preview**: Changes to XAML reflect immediately with hot reload enabled

### Key File Patterns
```
src/
├── iRacingOverlay.WPF/
│   ├── MainWindow.xaml              # Configuration UI (394 lines)
│   ├── MainWindow.xaml.cs           # Event handlers (367 lines)
│   ├── App.xaml                     # Application entry point
│   └── App.xaml.cs                  # App initialization logic
│
├── Widgets/
│   ├── WidgetBase.cs                # Abstract base class for all widgets
│   ├── DrivingWidget.cs             # Speed/gear overlay widget
│   ├── DataWidget.cs                # 2x3 grid telemetry widget (675 lines)
│   └── WidgetManager.cs             # Widget lifecycle management
│
├── Services/
│   ├── IRacingTelemetryService.cs   # SDK data extraction service
│   ├── TelemetryDataMapper.cs       # Field name to data value mapping
│   └── AppSettings.cs               # Settings persistence singleton
│
├── Models/
│   ├── TelemetryData.cs             # Real-time telemetry data model
│   ├── TelemetryField.cs            # Enumeration of available fields
│   └── AppSettings.cs               # Settings data structure
│
└── Utils/
    └── ColorHelper.cs                # Color manipulation utilities

Docs/
├── iracing_dev_phases.md            # Development phase tracking
├── PHASE_7_UI_REDESIGN.md           # Phase 7 documentation
└── PHASE_7.5_DATAWIDGET_FIXES.md    # Phase 7.5 documentation
```

## Development Standards

### Language & Style
- **Language**: English only - all code, comments, docs, commits, configs
- **C# Conventions**: PascalCase for public/protected, camelCase for private, _camelCase for fields
- **Style**: Self-documenting code preferred over extensive comments
- **XAML**: Descriptive element names, consistent Grid/StackPanel usage, proper data binding

### Git Conventions
**Commit Format**: `<type>(<scope>): <subject>`
- **Types**: feat|fix|docs|style|refactor|test|chore|perf
- **Subject**: 50 chars max, imperative mood ("add" not "added"), no period
- **Body**: For complex changes, explain what/why (72-char lines)
- **Atomic**: One logical change per commit
- **Examples**: 
  - `feat(widgets): add tire temperature color coding`
  - `fix(data-widget): dynamic column sizing for empty cells`
  - `refactor(settings): migrate to event-driven updates`

### Code Quality Principles
1. **MAINTAIN COMPLEXITY**: Preserve sophisticated algorithms (color coding, dynamic sizing, field mapping)
2. **REUSE EXISTING**: Check WidgetBase, TelemetryDataMapper, AppSettings before creating new utilities
3. **COMPREHENSIVE TESTING**: Build and run application to verify changes work correctly
4. **SPECIFIC ERROR HANDLING**: Use try-catch with proper exception types (ArgumentException, InvalidOperationException)
5. **CONSISTENT NAMING**: Follow established patterns (OnXxxChanged for event handlers, UpdateXxx for UI updates)
6. **PROPER SEPARATION**: Keep UI logic in code-behind, domain logic in services/mappers
7. **RESOURCE MANAGEMENT**: Dispose widgets properly, unsubscribe from events, close timers
8. **PROGRESS TRACKING**: Update TODO/Phase checkboxes when completing tasks from lists
9. **CRITICAL - BUILD VERIFICATION**: After EVERY build command (dotnet build, run_task, etc.), immediately check the output to verify success or failure. If build failed, read ALL error messages carefully and fix every compilation error before proceeding. Never assume build success - always verify the result explicitly.

### Domain-Specific Guidelines

#### Telemetry Processing
- **SDK Integration**: YAML parsing from iRacing SDK with reflection-based property access
- **Field Mapping**: Use TelemetryDataMapper for consistent field name to value translation
- **Update Frequency**: Balance responsiveness with performance (typical: 60Hz telemetry updates)
- **Error Tolerance**: Handle missing/null telemetry fields gracefully without crashing

#### Widget Architecture
- **Base Class**: All widgets inherit from WidgetBase for consistent behavior
- **Lifecycle**: Create → Position → Show → Update (via timer) → Hide → Dispose
- **Thread Safety**: Use Dispatcher.Invoke for UI updates from background threads
- **Positioning**: Preserve relative positions when resizing, respect screen boundaries
- **Topmost**: Maintain overlay z-order with `Topmost = true`, handle focus properly

#### Configuration UI
- **Immediate Apply**: No "Apply" buttons - changes take effect on event triggers
- **Event Handlers**: Subscribe to ValueChanged, SelectionChanged, Checked/Unchecked
- **Settings Propagation**: Use AppSettings.Instance.SettingsChanged event for cross-widget updates
- **Validation**: Validate inputs before applying (size ranges, field selections, etc.)
- **State Management**: Persist settings to JSON on every change

#### Visual Design
- **Color Coding**: Use meaningful colors (Green=optimal, Orange=warning, Red=critical, Blue=cold)
- **Dynamic Sizing**: Collapse empty grid columns/rows by setting Width/Height to 0
- **Font Sizing**: Balance readability with space constraints (typically 16-20px for data values)
- **Transparency**: Support configurable opacity (0.5-1.0 range) for overlay visibility
- **Units**: Always display unit indicators (°C/°F, L/Gal, km/h / mph) for clarity

## Common Development Patterns

### Widget Implementation

```csharp
// 1. Create new widget class inheriting from WidgetBase
public class MyCustomWidget : WidgetBase
{
    public MyCustomWidget() : base()
    {
        // Initialize UI components
        InitializeComponent();
        
        // Subscribe to settings changes
        AppSettings.Instance.SettingsChanged += OnSettingsChanged;
    }
    
    protected override void UpdateTelemetryDisplay(TelemetryData data)
    {
        // Update UI with new telemetry data
        Dispatcher.Invoke(() => {
            MyValueLabel.Content = data.Speed.ToString("F1");
        });
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AppSettings.Instance.SettingsChanged -= OnSettingsChanged;
        }
        base.Dispose(disposing);
    }
}
```

### Event-Driven Configuration

```csharp
// In MainWindow.xaml.cs - Real-time updates without Apply buttons
private void MyConfigCheckBox_Checked(object sender, RoutedEventArgs e)
{
    // Immediate effect - no confirmation needed
    AppSettings.Instance.MyFeatureEnabled = true;
    AppSettings.Instance.Save();
    
    // Trigger widget update
    _widgetManager.UpdateWidgetConfiguration();
}

private void MyConfigSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    if (!_isInitialized) return; // Prevent spurious events during initialization
    
    AppSettings.Instance.MyValue = e.NewValue;
    AppSettings.Instance.Save();
}
```

### Dynamic Layout Management

```csharp
// Collapse empty grid columns/rows dynamically
private void UpdateGridLayout()
{
    // Check which cells have content
    bool[] hasColumnContent = new bool[3];
    bool[] hasRowContent = new bool[2];
    
    for (int row = 0; row < 2; row++)
    {
        for (int col = 0; col < 3; col++)
        {
            var cell = GetCellAt(row, col);
            if (cell != null && cell.IsVisible)
            {
                hasColumnContent[col] = true;
                hasRowContent[row] = true;
            }
        }
    }
    
    // Set column widths (0 for empty, * for active)
    for (int i = 0; i < 3; i++)
    {
        MainGrid.ColumnDefinitions[i].Width = hasColumnContent[i] 
            ? new GridLength(1, GridUnitType.Star) 
            : new GridLength(0);
    }
}
```

### Color-Coded Status Indicators

```csharp
// Tire temperature color coding example
private Brush GetTireTemperatureColor(double temp)
{
    return temp switch
    {
        < 60 => new SolidColorBrush(Color.FromRgb(100, 150, 255)),  // Cold (Blue)
        < 75 => new SolidColorBrush(Color.FromRgb(100, 200, 200)),  // Warming (Teal)
        < 95 => new SolidColorBrush(Color.FromRgb(100, 255, 100)),  // Optimal (Green)
        < 110 => new SolidColorBrush(Color.FromRgb(255, 165, 0)),   // Hot (Orange)
        _ => new SolidColorBrush(Color.FromRgb(255, 50, 50))        // Extreme (Red)
    };
}
```

### Settings Persistence

```csharp
// AppSettings singleton pattern with event propagation
public class AppSettings
{
    private static AppSettings? _instance;
    public static AppSettings Instance => _instance ??= Load();
    
    public event EventHandler? SettingsChanged;
    
    public void Save()
    {
        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        File.WriteAllText(SettingsFilePath, json);
        
        // Notify all subscribers
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
    
    private static AppSettings Load()
    {
        if (File.Exists(SettingsFilePath))
        {
            string json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        return new AppSettings();
    }
}
```

## Troubleshooting Reference

### Common Issues

1. **Widgets not updating**: Check that telemetry service is running and connected to iRacing
2. **Layout not collapsing**: Verify UpdateGridLayout() is called after field changes
3. **Colors not changing**: Ensure OnSettingsChanged is subscribed and called
4. **Settings not persisting**: Check that AppSettings.Save() is called after changes
5. **Overlays not topmost**: Verify `Topmost = true` and handle window focus events

### Debugging Commands

```powershell
# Check build errors
dotnet build --no-incremental

# Run with verbose logging
dotnet run --project src\iRacingOverlay.WPF --verbosity detailed

# Clean and rebuild
dotnet clean
dotnet build

# Check dependencies
dotnet list package

# Inspect assembly
dotnet build /t:ShowAssemblyInfo
```

### Development Workflow

1. **Make Code Changes**: Edit .cs or .xaml files
2. **Build Application**: `dotnet build` or F5 in Visual Studio
3. **CRITICAL - Verify Build Result**: 
   - **ALWAYS check if build succeeded or failed**
   - If `run_task` was used, check the task output immediately
   - If `run_in_terminal` was used, read the terminal output
   - Look for "Build succeeded" or "Build FAILED"
   - If failed, read ALL error messages line-by-line
   - Identify the exact file, line number, and error type
   - Fix EVERY compilation error before proceeding
   - **NEVER assume build success** - verify explicitly
4. **Test in iRacing**: Launch iRacing, join session, verify overlay behavior (only after successful build)
5. **Iterate**: Fix issues, rebuild, test again
6. **Commit**: Use conventional commit format with clear scope

## Critical Policies

### Documentation Creation

**DO NOT create summary documents** unless explicitly requested by the user or critically important for project continuity.

- Prefer inline code comments and commit messages for documenting changes
- Only create markdown documentation files when:
  - User specifically requests documentation
  - Documenting major phase completions (Phase 7, Phase 8, etc.)
  - Creating architectural decision records (ADRs) for significant design choices

### Progress Tracking

**ALWAYS update checkboxes** in TODO lists and Phase documents when completing tasks from those lists.

- Mark items complete (✅) in real-time as work progresses, not in batches
- Use ⏳ for in-progress items
- Use ❌ for blocked or cancelled items
- Keep phase tracking documents (`iracing_dev_phases.md`) up-to-date with actual development state
- Update phase documentation files (e.g., `PHASE_7_UI_REDESIGN.md`) when completing major phases

### Claude Model Optimization (Sonnet 4.0/4.5)

Leverage advanced capabilities of Claude Sonnet 4.0 and 4.5:

- **Improved Code Understanding**: Better recognition of complex C# patterns and XAML bindings
- **Enhanced Multi-File Reasoning**: Understand interactions between widgets, services, and UI
- **Extended Context Window**: Analyze large files (600+ lines) without summarization
- **Parallel Tool Calls**: Use when operations are independent (e.g., reading multiple files)
- **Semantic Search**: Find patterns across codebase efficiently

Current status: Phase 7 and 7.5 complete. Overlay system operational with immediate-apply UI and enhanced data widgets.
