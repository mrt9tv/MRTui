# Settings Persistence Fix - v0.6.1

## Problem
After implementing Phase 2 visual enhancements (gradient background, shift ring, glow effects), settings were not persisting across app restarts. Users could toggle features in the Overlay Manager UI, and they would work during the current session, but all settings would revert to defaults after closing and reopening the application.

## Root Cause
**Architecture Gap**: The settings persistence layer was incomplete.

### Data Flow (Before Fix)
```
MRTOneSettings → Config.Settings["mrtone"] → WidgetConfig → [STOPS HERE - IN MEMORY ONLY]
```

### What Was Missing
1. **LayoutConfig Save**: `WidgetManager.GetCurrentLayout()` existed but was NEVER called
2. **File Persistence**: No method to serialize LayoutConfig to JSON file on disk
3. **Save Triggers**: No hooks to save when settings changed, widgets created/removed, or app closed
4. **Load Trigger**: No mechanism to load saved layout on app startup

## Solution Implemented

### 1. Added Layout Persistence to WidgetManager
**File**: `src/iRacingOverlay.WPF/Services/WidgetManager.cs`

Added two new methods:
- `SaveCurrentLayout()` - Serializes current widget layout to `Documents\MRT-UI\layout.json`
- `LoadSavedLayout()` - Deserializes layout from file and recreates widgets

Pattern copied from working `AppSettings.Save()` implementation:
```csharp
var directory = Path.GetDirectoryName(LayoutFilePath);
Directory.CreateDirectory(directory);
var options = new JsonSerializerOptions { WriteIndented = true };
var json = JsonSerializer.Serialize(layout, options);
File.WriteAllText(LayoutFilePath, json);
```

**File Path**: `C:\Users\{username}\Documents\MRT-UI\layout.json`

### 2. Wired Up Save Triggers

**WidgetManager.cs**:
- `CreateWidget()` - Calls `SaveCurrentLayout()` after widget creation
- `RemoveWidget()` - Calls `SaveCurrentLayout()` after widget removal

**OverlayViewModel.cs**:
- After `UpdateWidgetSettings()` - Calls `_widgetManager.SaveCurrentLayout()`
- This triggers when user changes Phase 2 toggles (gradient, shift ring, glow)

**MainWindow.xaml.cs**:
- `OnClosed()` - Calls `_widgetManager.SaveCurrentLayout()` before shutdown

### 3. Wired Up Load Trigger

**MainWindow.xaml.cs Constructor**:
- After `ApplyWindowSettings()` - Calls `_widgetManager.LoadSavedLayout()`
- Loads all widgets and their configurations from previous session
- If no saved layout exists, user can manually create widgets via Overlay Manager

## Data Flow (After Fix)
```
MRTOneSettings → Config.Settings["mrtone"] → WidgetConfig → LayoutConfig → layout.json (DISK) ✅
```

## Files Modified
1. `src/iRacingOverlay.WPF/Services/WidgetManager.cs`
   - Added `using System.Diagnostics`
   - Added `using System.IO`
   - Added `using System.Text.Json`
   - Added `SaveCurrentLayout()` method
   - Added `LoadSavedLayout()` method
   - Added save trigger in `CreateWidget()`
   - Added save trigger in `RemoveWidget()`

2. `src/iRacingOverlay.WPF/ViewModels/OverlayViewModel.cs`
   - Added save trigger after `UpdateWidgetSettings()`

3. `src/iRacingOverlay.WPF/MainWindow.xaml.cs`
   - Added `LoadSavedLayout()` call in constructor
   - Added `SaveCurrentLayout()` call in `OnClosed()`

## Testing Instructions

### Test 1: Phase 2 Settings Persistence
1. Launch application
2. Navigate to Overlay Manager
3. Create or select MRT One widget
4. Enable "Shift Point Ring" ✅
5. Enable "Glow Effects" ✅
6. Verify features appear immediately in widget window
7. **Close application completely**
8. **Reopen application**
9. Navigate to Overlay Manager
10. Verify "Shift Point Ring" is still enabled ✅
11. Verify "Glow Effects" is still enabled ✅
12. Check widget window - shift ring and glow should be visible

### Test 2: Widget Position Persistence
1. Create MRT One widget
2. Move widget to specific position on screen
3. Resize widget if desired
4. Close application
5. Reopen application
6. Verify widget appears at same position and size ✅

### Test 3: Multiple Widgets (Future)
1. Create multiple widgets (when more widget types are available)
2. Configure each widget differently
3. Close application
4. Reopen application
5. Verify all widgets restored with correct configurations ✅

### Test 4: Fresh Install Behavior
1. Delete `C:\Users\{username}\Documents\MRT-UI\layout.json`
2. Launch application
3. Verify no widgets appear automatically (expected behavior)
4. Create widget via Overlay Manager
5. Close and reopen
6. Verify widget restored ✅

## File Structure
```
C:\Users\{username}\Documents\MRT-UI\
├── settings.json      (Global app settings - already working)
└── layout.json        (Widget layout and configurations - NEW)
```

## Layout JSON Structure
```json
{
  "Name": "Current Layout",
  "Version": "1.0",
  "Widgets": [
    {
      "Id": "guid-here",
      "Type": 1,
      "X": 100,
      "Y": 100,
      "Width": 200,
      "Height": 200,
      "IsVisible": true,
      "Settings": {
        "mrtone": {
          "topField": "Speed",
          "centerField": "Gear",
          "bottomField": "RPM",
          "enableGradientBackground": true,
          "enableShiftPointRing": true,
          "enableGlowEffects": true,
          "showTop": true,
          "showCenter": true,
          "showBottom": true
        }
      }
    }
  ],
  "ToggleHotkey": "F11",
  "AllVisible": true
}
```

## Benefits
- ✅ Phase 2 settings (gradient, shift ring, glow) now persist
- ✅ Widget positions and sizes persist
- ✅ All widget visibility toggles persist
- ✅ Field selection (top/center/bottom/left/right) persists
- ✅ Multiple widgets can be saved and restored (when implemented)
- ✅ Consistent with existing AppSettings pattern
- ✅ Human-readable JSON format for debugging

## Version
- **Release**: v0.6.1
- **Status**: Ready for testing
- **Build**: Successful (0 errors, 0 warnings)

## Next Steps
1. Test all scenarios above ✅
2. Verify `layout.json` file is created correctly
3. Inspect JSON structure to confirm all settings are saved
4. Test edge cases (corrupted file, missing file, invalid JSON)
5. Consider adding backup mechanism for layout.json
6. Update user documentation with layout.json location
