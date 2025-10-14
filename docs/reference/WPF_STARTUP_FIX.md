# WPF Application Startup Fix

## Problem
Application crashed immediately with error:
```
System.Windows.Markup.XamlParseException: 'No matching constructor found on type 'iRacingOverlay.WPF.MainWindow'
System.MissingMethodException: No default constructor found for type 'iRacingOverlay.WPF.MainWindow'
```

## Root Cause
**XAML Limitation:** When using `StartupUri="MainWindow.xaml"` in App.xaml, WPF attempts to instantiate the window using XAML, which **requires a parameterless constructor**.

Our `MainWindow` has a constructor that requires `IServiceProvider`:
```csharp
public MainWindow(IServiceProvider services)
{
    InitializeComponent();
    _widgetManager = services.GetRequiredService<WidgetManager>();
    // ...
}
```

XAML cannot inject dependencies - it can only call parameterless constructors.

## Solution
**Remove StartupUri and create window programmatically:**

### 1. Modified App.xaml
**BEFORE:**
```xml
<Application x:Class="iRacingOverlay.WPF.App"
             StartupUri="MainWindow.xaml">
```

**AFTER:**
```xml
<Application x:Class="iRacingOverlay.WPF.App">
```

Removed `StartupUri="MainWindow.xaml"` to prevent automatic XAML instantiation.

### 2. App.xaml.cs Already Correct
The `OnStartup` method was already creating the window correctly:
```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    
    // Build DI container
    _host = Host.CreateDefaultBuilder()
        .ConfigureServices(...)
        .Build();
    
    // Manually create and show window with DI
    var mainWindow = new MainWindow(_host.Services);
    mainWindow.Show();
}
```

## Result
✅ Application now starts successfully
✅ MainWindow receives IServiceProvider via constructor
✅ Dependency injection works correctly
✅ WidgetManager and telemetry service properly injected

## Key Takeaway
**When using dependency injection with WPF windows:**
- **NEVER** use `StartupUri` in App.xaml
- **ALWAYS** create windows programmatically in `OnStartup()`
- This allows passing constructor parameters from DI container

## Verification
```powershell
cd "f:\VSCode\Programming\MRTui - Copy\src\iRacingOverlay.WPF"
dotnet run
# Application should start without errors
# Main window should appear
# Create Speed Widget button should work
```
