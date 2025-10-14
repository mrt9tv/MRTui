# WPF Overlay Testing Guide

## Quick Start

### Method 1: Using Batch File (Recommended)
```powershell
# From project root
.\START_OVERLAY.bat
```

### Method 2: Manual Run
```powershell
cd "f:\VSCode\Programming\MRTui - Copy\src\iRacingOverlay.WPF"
dotnet run
```

## Testing SpeedWidget

1. **Launch the application** using either method above
2. **Verify Main Window appears** - "iRacing Overlay Manager" window (400x500px)
3. **Click "Create Speed Widget"** button
4. **Expected behavior:**
   - New semi-transparent window appears (250x120px)
   - Shows "Speed: 0" in large green text
   - Has rounded corners and dark background
   - Border color indicates connection status:
     - **🟢 GREEN** = Connected to iRacing
     - **🟡 YELLOW** = Connecting...
     - **🔴 RED** = Disconnected
5. **Test dragging:**
   - Click and hold anywhere on the SpeedWidget
   - Drag it to different screen positions
   - Widget should move smoothly
6. **Test multiple widgets:**
   - Click "Create Speed Widget" again
   - Second widget should appear at same position (stack)
   - Each widget can be dragged independently
7. **Test toggle (F12):**
   - Press **F12** key
   - All widgets should hide
   - Press **F12** again to show
8. **Test remove all:**
   - Click "Remove All Widgets" button
   - All widgets should close immediately

## Connection Status Testing

### Without iRacing Running:
- SpeedWidget border = **RED**
- Speed displays "0"
- Status bar shows "Disconnected"

### With iRacing Running:
- SpeedWidget border = **YELLOW** (connecting)
- Then **GREEN** (connected)
- Speed updates every 100ms
- Shows actual car speed

### In a Race/Practice:
- Speed updates continuously
- Values change from 0-300+ (depending on car/track)
- Ultra-smooth updates (10 Hz refresh)

## Main Window Features

### Buttons:
- **Create Speed Widget** - Creates new speed display
- **Create Telemetry Table Widget** - ⚠️ Not yet implemented (disabled)
- **Toggle All Widgets (F12)** - Show/hide all widgets
- **Remove All Widgets** - Close all widgets

### Status Bar:
- **Left side** - Connection status and instructions
- **Right side** - Active widget count

### Keyboard Shortcuts:
- **F12** - Toggle all widgets (works globally)

## Expected Current State

✅ **Working:**
- Main window launches
- SpeedWidget creation
- Widget dragging
- Connection status colors
- F12 toggle functionality
- Remove all widgets
- Widget counting

⚠️ **Not Yet Implemented:**
- TelemetryTableWidget (next task)
- Layout save/load
- Widget configuration UI
- Other widgets (RPM, Fuel, etc.)

## Troubleshooting

### Application won't start:
```powershell
# Rebuild from scratch
cd "f:\VSCode\Programming\MRTui - Copy\src\iRacingOverlay.WPF"
dotnet clean
dotnet build
dotnet run
```

### SpeedWidget doesn't appear:
- Check for error message box
- Look for widget behind main window (try Alt+Tab)
- Check console output for errors

### Widget won't drag:
- Ensure you're clicking on the widget itself (not empty space)
- Widget should have mouse cursor change

### Connection stays RED:
- This is normal without iRacing running
- Widget will turn GREEN when iRacing is detected
- Ensure iRacing SDK is accessible (no admin rights needed)

### F12 not working:
- Main window must have focus initially
- After first toggle, should work globally
- Try clicking main window first

## Next Steps

After confirming SpeedWidget works:
1. ✅ Mark testing complete
2. 🔄 Build TelemetryTableWidget showing 10+ stats
3. 🔄 Test both widgets simultaneously
4. 🔄 Implement LayoutService for persistence

## Development Notes

### Widget Architecture:
- All widgets inherit from `WidgetBase`
- `WidgetManager` handles lifecycle
- Factory pattern for dynamic creation
- Thread-safe UI updates via Dispatcher

### Performance:
- Telemetry updates: 100ms (10 Hz)
- UI updates: Dispatcher.Invoke for thread safety
- Minimal overhead: ~1-2% CPU when idle

### Code-Only Widgets:
- SpeedWidget uses **code-only** approach (no XAML)
- Avoids XAML base class inheritance issues
- Simpler, more flexible
- Same approach for future widgets
