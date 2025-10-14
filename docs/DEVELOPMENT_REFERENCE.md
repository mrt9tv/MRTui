# Development Reference Guide - MRT UI Manager

**Purpose:** Critical knowledge and consistency guidelines for MRTui development  
**Created:** October 14, 2025  
**Last Updated:** October 14, 2025

---

## 🎯 Core Principles

1. **MVVM Architecture** - Strict separation: View (XAML) → ViewModel (C#) → Model/Services
2. **Event-Driven Updates** - Subscribe to service events, don't poll
3. **MRT Theme Consistency** - Always use defined theme resources (see below)
4. **No Breaking Changes** - Preserve existing APIs unless absolutely necessary
5. **Build Validation** - Every change must build with 0 errors, 0 warnings

---

## 🎨 MRT Theme Resources (MRTTheme.xaml)

### ⚠️ CRITICAL: Always Use Full Resource Names

**Common Crash Cause:** Incorrect resource names cause silent runtime crashes!

### Color Resources

#### Simplified Aliases (Available)
```xml
<!-- Backgrounds -->
<StaticResource ResourceKey="DarkBackground"/>  <!-- #1A1A1A -->
<StaticResource ResourceKey="DarkSurface"/>     <!-- #2F2F2F -->
<StaticResource ResourceKey="DarkBorder"/>      <!-- #444444 -->

<!-- Primary Colors -->
<StaticResource ResourceKey="TealPrimary"/>     <!-- #008080 -->
<StaticResource ResourceKey="OrangePrimary"/>   <!-- #FF8000 -->

<!-- Text Colors -->
<StaticResource ResourceKey="LightText"/>       <!-- #FFFFFF -->
<StaticResource ResourceKey="MutedText"/>       <!-- #999999 -->
```

#### Full MRT.* Names (Also Available)
```xml
<StaticResource ResourceKey="MRT.Background"/>      <!-- #1A1A1A -->
<StaticResource ResourceKey="MRT.Background.Gray"/> <!-- #2F2F2F -->
<StaticResource ResourceKey="MRT.Teal"/>            <!-- #008080 -->
<StaticResource ResourceKey="MRT.Orange"/>          <!-- #FF8000 -->
<StaticResource ResourceKey="MRT.White"/>           <!-- #FFFFFF -->
<!-- ... etc -->
```

### Control Styles

#### ✅ Correct Style Names
```xml
<!-- Buttons -->
<Style="{StaticResource MRT.Button.Primary}"/>
<Style="{StaticResource MRT.Button.Secondary}"/>
<Style="{StaticResource NavigationButton}"/>

<!-- Toggle Switch -->
<Style="{StaticResource MRT.ToggleButton}"/>

<!-- Sliders -->
<Style="{StaticResource MRT.Slider}"/>

<!-- Radio Buttons -->
<Style="{StaticResource MRT.RadioButton}"/>
```

#### ❌ Common Mistakes (DO NOT USE)
```xml
<!-- WRONG - These don't exist! -->
<Style="{StaticResource ToggleSwitchStyle}"/>  <!-- ❌ Crashes -->
<Style="{StaticResource MRTSlider}"/>          <!-- ❌ Crashes -->
<Style="{StaticResource SecondaryButton}"/>    <!-- ❌ Crashes -->
<Style="{StaticResource PrimaryButton}"/>      <!-- ❌ Crashes -->
```

### Quick Reference Cheat Sheet
| Component | Correct Style Name | ❌ Common Mistake |
|-----------|-------------------|-------------------|
| Toggle Switch | `MRT.ToggleButton` | `ToggleSwitchStyle` |
| Slider | `MRT.Slider` | `MRTSlider` |
| Primary Button | `MRT.Button.Primary` | `PrimaryButton` |
| Secondary Button | `MRT.Button.Secondary` | `SecondaryButton` |
| Navigation Button | `NavigationButton` | `NavButton` |

---

## 🏗️ MVVM Architecture Patterns

### ViewModel Constructor Pattern

**Standard Pattern:**
```csharp
public class MyViewModel : INotifyPropertyChanged
{
    private readonly ServiceType _service;
    private readonly ManagerType _manager;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public MyViewModel(ServiceType service, ManagerType manager)
    {
        _service = service;
        _manager = manager;
        
        // Subscribe to events
        _service.SomeEvent += OnSomeEvent;
        _manager.SomeEvent += OnSomeEvent;
        
        // Initialize state
        InitializeState();
    }
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

### Property Pattern with Change Notification

```csharp
private string _myProperty;
public string MyProperty
{
    get => _myProperty;
    set
    {
        if (_myProperty != value)
        {
            _myProperty = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RelatedProperty)); // If needed
        }
    }
}
```

### View Constructor Pattern

```csharp
public partial class MyView : UserControl
{
    public MyView(MyViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

### Navigation Pattern (MainWindow)

```csharp
private void NavigateToMyView()
{
    var viewModel = new MyViewModel(_service, _manager);
    var view = new MyView(viewModel);
    ContentFrame.Content = view;
    SetActiveButton(BtnMyView);
}
```

---

## 🔧 Widget System API

### WidgetManager Service

#### Key Methods
```csharp
// Create widget
WidgetBase CreateWidget(WidgetType type, WidgetConfig? config = null)

// Remove widget
void RemoveWidget(Guid widgetId)
void RemoveAllWidgets()

// Query widgets
int GetWidgetCount()
bool HasWidgetType(WidgetType type)
IEnumerable<WidgetBase> GetWidgetsByType(WidgetType type)

// Visibility control
void ShowAllWidgets()
void HideAllWidgets()
void ToggleAllWidgets()

// Lock/unlock (prevents dragging, enables click-through)
void LockAllWidgets(bool lockState)

// Layout management
LayoutConfig GetCurrentLayout()
void LoadLayout(LayoutConfig layout)
```

#### Events
```csharp
event EventHandler<WidgetBase>? WidgetCreated;
event EventHandler<Guid>? WidgetRemoved;
```

#### Properties
```csharp
IReadOnlyDictionary<Guid, WidgetBase> ActiveWidgets { get; }
```

### WidgetBase (Base Class)

#### Key Properties
```csharp
Guid WidgetId { get; }                    // Unique identifier
abstract WidgetType WidgetType { get; }   // Type enum
WidgetConfig Config { get; }              // Configuration
double Opacity { get; set; }              // Window opacity (0.0-1.0)
double Width { get; set; }                // Window width
double Height { get; set; }               // Window height
bool IsVisible { get; }                   // Visibility state
```

#### Key Methods
```csharp
WidgetConfig GetConfiguration()           // Get current config
void UpdateConfiguration(WidgetConfig)    // Apply config changes
void ToggleVisibility()                   // Show/hide widget
void SetLocked(bool locked)               // Lock/unlock position
```

#### ⚠️ Critical: Opacity Property Location
```csharp
// ✅ CORRECT - Opacity is on Window (WidgetBase extends Window)
widget.Opacity = 0.5;

// ❌ WRONG - WidgetConfig does NOT have Opacity property
widget.Config.Opacity = 0.5;  // DOES NOT EXIST!
```

### WidgetConfig Model

```csharp
public class WidgetConfig
{
    Guid Id { get; set; }                              // Widget instance ID
    WidgetType Type { get; set; }                      // Widget type
    double X { get; set; }                             // Position X
    double Y { get; set; }                             // Position Y
    double Width { get; set; }                         // Dimension width
    double Height { get; set; }                        // Dimension height
    Dictionary<string, object> Settings { get; set; }  // Widget-specific settings
    bool IsVisible { get; set; }                       // Visibility flag
    
    // ⚠️ NOTE: Opacity is NOT in WidgetConfig! It's on Window class.
}
```

---

## 📂 File Organization

### Directory Structure
```
src/iRacingOverlay.WPF/
├── App.xaml / App.xaml.cs           # Application entry point
├── MainWindow.xaml / .cs            # Main navigation shell
├── Resources/
│   └── Themes/
│       └── MRTTheme.xaml            # Theme resources
├── Views/                           # XAML views (UserControls)
│   ├── DashboardView.xaml / .cs
│   ├── OverlayView.xaml / .cs
│   └── SettingsView.xaml / .cs
├── ViewModels/                      # View logic and data binding
│   ├── DashboardViewModel.cs
│   ├── OverlayViewModel.cs
│   └── SettingsViewModel.cs
├── Models/                          # Data models
│   ├── AppSettings.cs
│   ├── WidgetConfig.cs
│   ├── WidgetType.cs
│   └── LayoutConfig.cs
├── Services/                        # Business logic
│   └── WidgetManager.cs
├── Core/                            # Base classes
│   └── WidgetBase.cs
└── Widgets/                         # Specific widget implementations
    ├── SpeedWidget/
    ├── GearGaugeWidget/
    ├── DataWidget/
    └── FuelWidget/
```

### Naming Conventions

#### Files
- **Views:** `[Name]View.xaml` + `[Name]View.xaml.cs`
- **ViewModels:** `[Name]ViewModel.cs`
- **Models:** `[Name].cs` (singular, PascalCase)
- **Services:** `[Name]Manager.cs` or `[Name]Service.cs`

#### Code
- **Classes/Properties/Methods:** `PascalCase`
- **Private fields:** `_camelCase` with underscore prefix
- **Local variables:** `camelCase`
- **Constants:** `PascalCase` or `UPPER_CASE`

---

## 🧪 Testing & Debugging

### Build Commands
```powershell
# Debug build
dotnet build iRacingOverlay.sln

# Verbose build (for troubleshooting)
dotnet build iRacingOverlay.sln --verbosity normal

# Release build
dotnet build iRacingOverlay.sln --configuration Release

# Clean build
dotnet clean iRacingOverlay.sln
dotnet build iRacingOverlay.sln
```

### Run Commands
```powershell
# Run WPF app
dotnet run --project src\iRacingOverlay.WPF

# Run with debug logging (recommended)
START_OVERLAY_DEBUG.bat
```

### Debug Logging
- **Script:** `START_OVERLAY_DEBUG.bat`
- **Logs:** `debug_*.log` (3-log rotation)
- **Features:** Timestamps, log rotation, error tracking

---

## 📋 Common Patterns & Recipes

### Pattern 1: Add New View/ViewModel

1. **Create ViewModel:**
   ```csharp
   // src/iRacingOverlay.WPF/ViewModels/MyViewModel.cs
   public class MyViewModel : INotifyPropertyChanged
   {
       private readonly MyService _service;
       
       public event PropertyChangedEventHandler? PropertyChanged;
       
       public MyViewModel(MyService service)
       {
           _service = service;
           _service.SomeEvent += OnSomeEvent;
       }
       
       protected void OnPropertyChanged([CallerMemberName] string? name = null)
       {
           PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
       }
   }
   ```

2. **Create View XAML:**
   ```xml
   <!-- src/iRacingOverlay.WPF/Views/MyView.xaml -->
   <UserControl x:Class="iRacingOverlay.WPF.Views.MyView"
                Background="{StaticResource DarkBackground}">
       <!-- Content here -->
   </UserControl>
   ```

3. **Create View Code-Behind:**
   ```csharp
   // src/iRacingOverlay.WPF/Views/MyView.xaml.cs
   public partial class MyView : UserControl
   {
       public MyView(MyViewModel viewModel)
       {
           InitializeComponent();
           DataContext = viewModel;
       }
   }
   ```

4. **Wire Up Navigation in MainWindow.xaml.cs:**
   ```csharp
   private void NavigateToMy()
   {
       var viewModel = new MyViewModel(_myService);
       var view = new MyView(viewModel);
       ContentFrame.Content = view;
       SetActiveButton(BtnMy);
   }
   ```

### Pattern 2: Subscribe to WidgetManager Events

```csharp
public MyViewModel(WidgetManager widgetManager)
{
    _widgetManager = widgetManager;
    
    // Subscribe to events
    _widgetManager.WidgetCreated += OnWidgetCreated;
    _widgetManager.WidgetRemoved += OnWidgetRemoved;
    
    // Initialize state
    UpdateWidgetInfo();
}

private void OnWidgetCreated(object? sender, WidgetBase widget)
{
    // Update UI with new widget
    UpdateWidgetInfo();
}

private void OnWidgetRemoved(object? sender, Guid widgetId)
{
    // Update UI after widget removed
    UpdateWidgetInfo();
}
```

### Pattern 3: Control Widget Opacity/Size

```csharp
// ✅ CORRECT - Access Opacity on Window
var widgets = _widgetManager.GetWidgetsByType(WidgetType.Speed);
foreach (var widget in widgets)
{
    widget.Opacity = 0.8;        // Window property
    widget.Width = 300;          // Window property
    widget.Height = 200;         // Window property
}

// ❌ WRONG - Opacity is NOT in WidgetConfig
widget.Config.Opacity = 0.8;  // Does not exist!
```

---

## 🚨 Common Pitfalls & Solutions

### Pitfall 1: XAML Resource Not Found (Silent Crash)
**Symptom:** App crashes when navigating to a page  
**Cause:** Incorrect `StaticResource` name in XAML  
**Solution:** Use exact names from MRTTheme.xaml (see cheat sheet above)

```xml
<!-- ❌ WRONG -->
<CheckBox Style="{StaticResource ToggleSwitchStyle}"/>
<Button Style="{StaticResource SecondaryButton}"/>
<TextBlock Foreground="{StaticResource MRT.Foreground}"/>  <!-- Doesn't exist! -->

<!-- ✅ CORRECT -->
<CheckBox Style="{StaticResource MRT.ToggleButton}"/>
<Button Style="{StaticResource MRT.Button.Secondary}"/>
<TextBlock Foreground="{StaticResource MRT.White}"/>  <!-- Use MRT.White or LightText -->
```

**Common Mistakes:**
- `MRT.Foreground` → Use `MRT.White` or `LightText`
- `ToggleSwitchStyle` → Use `MRT.ToggleButton`
- `MRTSlider` → Use `MRT.Slider`
- `SecondaryButton` → Use `MRT.Button.Secondary`

### Pitfall 2: Widget Opacity Not Working
**Symptom:** Setting opacity doesn't change widget transparency  
**Cause:** Trying to set `widget.Config.Opacity` (doesn't exist)  
**Solution:** Set `widget.Opacity` directly (Window property)

```csharp
// ❌ WRONG
widget.Config.Opacity = 0.5;  // Property doesn't exist!

// ✅ CORRECT
widget.Opacity = 0.5;  // Window property
```

### Pitfall 3: ViewModel Not Updating UI
**Symptom:** Property changes don't reflect in UI  
**Cause:** Forgot to call `OnPropertyChanged()`  
**Solution:** Always call `OnPropertyChanged()` in property setters

```csharp
// ❌ WRONG
public string MyProperty { get; set; }  // Auto-property doesn't notify

// ✅ CORRECT
private string _myProperty;
public string MyProperty
{
    get => _myProperty;
    set
    {
        _myProperty = value;
        OnPropertyChanged();  // Critical!
    }
}
```

### Pitfall 4: Event Memory Leaks
**Symptom:** ViewModels not garbage collected  
**Cause:** Not unsubscribing from events  
**Solution:** Unsubscribe in Dispose or when ViewModel is discarded

```csharp
// ⚠️ Better: Implement cleanup if ViewModel is long-lived
public void Dispose()
{
    _service.SomeEvent -= OnSomeEvent;
    _widgetManager.WidgetCreated -= OnWidgetCreated;
}
```

---

## 🎯 Git Commit Patterns

### Commit Message Format
```
<Type>: <Short description>

<Detailed description>
<List of changes>
<Build status>
<Progress update>
```

### Types
- `Phase 1 Day X COMPLETE:` - Completing a phase day
- `HOTFIX:` - Critical bug fix
- `FEATURE:` - New feature
- `REFACTOR:` - Code refactoring
- `DOCS:` - Documentation update
- `TEST:` - Test updates

### Example
```
Phase 1 Day 4-5 COMPLETE: Overlay Manager View

Created professional widget management interface with 3-panel layout,
toggle switches, opacity/size sliders, and real-time updates.

Changes:
- Created OverlayView.xaml with 3-panel layout
- Created OverlayViewModel.cs with WidgetItemViewModel
- Wired up MainWindow navigation
- All 4 widgets manageable from UI

Build Status: ✅ Success (0 errors, 0 warnings)
Progress: 60% complete (Day 4-5 of 10)
```

---

## 📚 Quick Reference Links

- **Theme Resources:** `src/iRacingOverlay.WPF/Resources/Themes/MRTTheme.xaml`
- **Widget API:** `src/iRacingOverlay.WPF/Services/WidgetManager.cs`
- **Widget Base:** `src/iRacingOverlay.WPF/Core/WidgetBase.cs`
- **App Settings:** `src/iRacingOverlay.WPF/Models/AppSettings.cs`
- **Phase Tracking:** `docs/phases/PHASE1_MRT_UI_MANAGER.md`
- **Quick Checklist:** `docs/phases/PHASE1_QUICK_CHECKLIST.md`

---

## 🔄 Document Maintenance

**When to Update This Document:**
- Adding new theme resources
- Changing architecture patterns
- Discovering new common pitfalls
- Adding new services or APIs
- Updating file organization

**Last Updated:** October 14, 2025 (Phase 1 Day 4-5)
