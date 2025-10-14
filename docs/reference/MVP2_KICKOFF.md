# 🚀 MVP 2 KICKOFF - Simple Overlay Window
**Date:** October 12, 2025  
**Status:** 🟢 Starting Now!  
**Previous Phase:** MVP 1 Complete with Fixes ✅

---

## ✅ MVP 1 Status: COMPLETE

### Final Changes Applied:
- ✅ Clutch display inverted (now shows 100% in gear)
- ✅ Steering direction fixed (turn left = bar left)
- ✅ Refresh rate set to **100ms (10 Hz display)** - Ultra smooth! ⚡
- ✅ 60+ telemetry variables captured (ready for overlay)
- ✅ Standalone executable built and ready

**Current Refresh Rate:** 100ms = 10 updates per second! 🔥

---

## 🎯 MVP 2 Goal: WPF Transparent Overlay

### What We're Building:
A transparent window that floats over iRacing showing:
1. **Speed** (km/h)
2. **RPM** (engine revs)
3. **Gear** (current gear)
4. **Lap** (current lap number)
5. **Position** (race position)

**PLUS MVP 2 Additions:**
6. **Fuel Level** (percentage)
7. **Water Temp** (°C)
8. **Oil Temp** (°C)
9. **Last Lap Time** (seconds)
10. **Best Lap Time** (seconds)

---

## 📋 Implementation Plan

### Phase 1: Project Setup (30 mins)
- [ ] Create new WPF .NET 8 project: `iRacingOverlay.WPF`
- [ ] Add reference to `iRacingOverlay.Core`
- [ ] Install NuGet: CommunityToolkit.Mvvm
- [ ] Set up basic project structure

### Phase 2: Transparent Window (1 hour)
- [ ] Create `OverlayWindow.xaml` with transparency
- [ ] Set WindowStyle="None", AllowsTransparency="True"
- [ ] Set Topmost="True" (always on top)
- [ ] Add semi-transparent dark background
- [ ] Test window appears over other apps

### Phase 3: Basic UI Layout (1 hour)
- [ ] Design layout for 10 telemetry values
- [ ] Add labels and value displays
- [ ] Style with colors (white labels, green values)
- [ ] Add rounded corners and padding
- [ ] Make window draggable

### Phase 4: ViewModel & Data Binding (1.5 hours)
- [ ] Create `OverlayViewModel.cs` with INotifyPropertyChanged
- [ ] Add properties for all 10 telemetry values
- [ ] Implement property change notifications
- [ ] Bind XAML to ViewModel
- [ ] Test with mock data first

### Phase 5: Telemetry Integration (1 hour)
- [ ] Reference TelemetryService from MVP 1
- [ ] Subscribe to TelemetryUpdated event
- [ ] Map telemetry data to ViewModel properties
- [ ] Use Dispatcher for thread-safe UI updates
- [ ] Add connection status indicator

### Phase 6: Polish & Testing (1 hour)
- [ ] Add smooth value transitions (optional)
- [ ] Test with real iRacing data
- [ ] Verify performance (CPU, FPS impact)
- [ ] Add keyboard shortcut to hide/show
- [ ] Final styling adjustments

**Total Estimated Time:** 6 hours

---

## 🏗️ New Project Structure

```
MRTui/
├── src/
│   ├── iRacingOverlay.Core/        ✅ Complete (MVP 1)
│   │   ├── Services/
│   │   ├── Models/
│   │   └── TelemetryWorker.cs
│   │
│   └── iRacingOverlay.WPF/         🆕 NEW (MVP 2)
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── MainWindow.xaml         # Overlay window
│       ├── MainWindow.xaml.cs
│       ├── ViewModels/
│       │   └── OverlayViewModel.cs
│       └── Resources/
│           └── Styles.xaml
│
├── build/
│   └── iRacingOverlay.Core.exe     ✅ MVP 1 executable
│
└── docs/
    ├── phases/MVP2_Simple_Overlay.md
    └── MVP2_KICKOFF.md ⭐ (this file)
```

---

## 🎨 Design Preview

### Overlay Layout:
```
┌─────────────────────────────┐
│  iRacing Telemetry Overlay  │
├─────────────────────────────┤
│  Speed:      145.3 km/h     │
│  RPM:        7,842          │
│  Gear:       4              │
│  Lap:        12             │
│  Position:   3rd            │
│                             │
│  Fuel:       67.5%          │
│  Water:      89.2°C         │
│  Oil:        105.8°C        │
│  Last Lap:   1:34.521       │
│  Best Lap:   1:33.012       │
└─────────────────────────────┘
```

**Window Properties:**
- Semi-transparent black background (#CC000000)
- Rounded corners (10px radius)
- White labels + Green values
- 300x400px size
- Draggable anywhere
- Always on top

---

## 🔧 Technical Stack

### WPF (.NET 8)
- **Framework:** WPF with XAML
- **Pattern:** MVVM (Model-View-ViewModel)
- **Data Binding:** Two-way binding with INotifyPropertyChanged
- **Thread Safety:** Dispatcher for UI thread updates
- **Performance:** Hardware acceleration enabled

### NuGet Packages:
```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
```

---

## 📊 Performance Targets

| Metric | Target | Why |
|--------|--------|-----|
| CPU Usage | <3% | Don't impact game FPS |
| Memory | <100MB | Keep it lightweight |
| Update Rate | 60 FPS | Match game refresh |
| Startup Time | <2 seconds | Quick launch |

---

## 🚦 Success Criteria

### Must Have:
- ✅ Transparent window visible over iRacing
- ✅ Shows 10 telemetry values in real-time
- ✅ Updates smoothly (no flicker)
- ✅ Stays on top of game
- ✅ Can be moved by dragging
- ✅ No game FPS impact

### Nice to Have:
- 🎯 Smooth value transitions
- 🎯 Fade-in animation on startup
- 🎯 Hotkey to hide/show (F12)
- 🎯 Color coding (red for high temps)
- 🎯 Progress bars for fuel

---

## 📝 Next Steps

### Immediate (Starting Now):
1. Create WPF project
2. Set up transparent window
3. Add basic layout
4. Connect to telemetry

### After MVP 2 Complete:
- MVP 3: First Widget (Customizable gauges)
- MVP 4: Multi-Widget (Multiple overlays)
- MVP 5: Basic Config (Save/load settings)
- MVP 6: Advanced Config (Full customization)

---

## 🎯 Let's Build This!

**Ready to start?** Say the word and we'll:
1. Create the WPF project
2. Set up the transparent overlay
3. Connect the telemetry data
4. Make it beautiful! ✨

**Current Status:** MVP 1 ✅ Complete (100ms refresh)  
**Next Up:** MVP 2 🚀 Starting Now!

---

**Last Updated:** October 12, 2025  
**MVP 1 Build:** Complete with 100ms refresh ⚡  
**MVP 2 Status:** Ready to begin! 🎉
