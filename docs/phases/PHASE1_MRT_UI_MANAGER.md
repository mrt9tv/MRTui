# Phase 1: MRT UI Manager - Modern Interface Redesign

**Duration:** 2 weeks (10 working days)  
**Status:** 🚧 In Progress (80% Complete)  
**Started:** October 14, 2025  
**Target Completion:** October 28, 2025

---

## 🎯 Phase Goals

Transform the basic tab-based configuration window into a modern, full-screen MRT UI Manager with:
- Professional branding (Mokka Racing Team colors) ✅
- Intuitive navigation (Home, Dashboard, Overlay, Settings) ✅
- Modular architecture for easy future redesigns ✅
- Connection status monitoring ✅
- Enhanced widget management interface ⏳

---

## 📋 Pre-Phase Checklist

### Design & Planning
- [x] Analyze current MainWindow.xaml structure
- [x] Create UI/UX requirements document
- [x] Define color scheme (Teal #008080 + Orange #FF8000)
- [x] Plan navigation structure (4 pages: Home, Dashboard, Overlay, Settings)
- [x] Identify modular components needed
- [x] Create initial theme system

### Technical Preparation
- [x] Review existing backend services (WidgetManager, AppSettings)
- [x] Verify current widget system functionality
- [x] Backup current MainWindow files
- [x] Plan file structure for Views/ViewModels
- [x] Set up theme resource dictionary structure

---

## 📅 Week 1: Core Structure & Navigation

### Day 1-2: Foundation & Theme Setup ✅
**Goal:** Create navigation shell and apply MRT branding

#### App-Level Theme Configuration
- [x] Create `Resources/Themes/MRTTheme.xaml`
  - [x] Define color resources (Teal, Orange, backgrounds)
  - [x] Create button styles
  - [x] Create toggle switch styles
  - [x] Create slider styles
  - [ ] Create card/panel styles
- [ ] Update `App.xaml` to reference MRTTheme
- [ ] Test theme resources load correctly

#### Main Window Redesign
- [ ] Backup current `MainWindow.xaml` → `MainWindow.xaml.backup`
- [ ] Create new `MainWindow.xaml` structure
  - [ ] Set window properties (1920x1080 minimum)
  - [ ] Add title bar with "MRT UI" branding
  - [ ] Create navigation buttons (Dashboard, Overlay, Settings)
  - [ ] Add content frame for page switching
  - [ ] Add status bar for connection indicator
- [ ] Update `MainWindow.xaml.cs` navigation logic
  - [ ] Add page switching methods
  - [ ] Keep existing telemetry service connection
  - [ ] Add minimize to taskbar functionality
- [ ] Build and test navigation shell works

**Deliverables:**
- ✅ MRT-themed resource dictionary
- ✅ New navigation shell with 3 buttons
- ✅ Empty pages that can be switched between
- ✅ Connection status bar at bottom

---

### Day 3: Dashboard View (Front Page) ✅
**Goal:** Create professional dashboard with connection monitoring

#### Dashboard View Creation
- [x] Create `Views/DashboardView.xaml`
  - [x] Add welcome header "Dashboard"
  - [x] Create connection status card
    - [x] Status indicator (🤌 Not Running / 🤞 Connecting / 🏁 Connected)
    - [x] Connection status text with real-time updates
    - [x] Last connection timestamp (military time format)
    - [x] Update rate display (Hz)
  - [x] Add quick stats section
    - [x] Active widgets count
    - [x] Active widgets list
    - [x] "Manage Widgets" navigation button
  - [x] Add system stats card
    - [x] Application uptime (live updates every second)
    - [x] Version info display
  - [x] Style with teal/orange theme

- [x] Create `ViewModels/DashboardViewModel.cs`
  - [x] Connection status property (bound to telemetry service)
  - [x] Uptime calculation with timer
  - [x] Active widget count tracking
  - [x] Update rate tracking
  - [x] INotifyPropertyChanged implementation
  - [x] Initial connection status detection
  - [x] Widget change event handling

- [x] Wire up Dashboard to MainWindow navigation
- [x] Test connection status updates in real-time
- [x] Build and verify dashboard displays correctly
- [x] Fix icon updates when connection status changes
- [x] Add military time format (24-hour)

**Deliverables:**
- ✅ Functional dashboard showing connection status
- ✅ Real-time status updates from telemetry service
- ✅ Clean, professional layout with MRT branding
- ✅ Live uptime counter
- ✅ Widget tracking integration

---

### Day 4-5: Overlay Manager View (Widget Management) ✅
**Goal:** Create widget management interface with toggles and configuration

#### Overlay View - Widget List
- [x] Create `Views/OverlayView.xaml`
  - [x] Create left panel for widget list
    - [x] Add widget item template (name + toggle)
    - [x] List all available widgets:
      - [x] 🎯 The MRT Simplicity (formerly GearGauge)
      - [x] 🏎️ Speed Widget
      - [x] � Data Widget
      - [x] ⛽ Fuel Calculator
    - [x] Style toggle switches (orange when active)
  
  - [x] Create right panel for widget configuration
    - [x] Opacity slider (0-100%)
    - [x] Size slider (50-200%)
    - [x] Position display (X, Y coordinates)
    - [x] Widget-specific settings area (placeholder)
    - [x] Apply/Reset buttons (placeholder)
  
  - [x] Create center placeholder for future live preview
    - [x] "Live Preview - Coming Soon" message
    - [x] "Changes apply to widgets immediately" subtitle

#### Overlay View - ViewModel
- [x] Create `ViewModels/OverlayViewModel.cs`
  - [x] Widget list collection (ObservableCollection)
  - [x] Selected widget property
  - [x] Toggle command for each widget
  - [x] Opacity/size change handlers
  - [x] Connect to existing WidgetManager
  - [x] Widget creation/destruction methods
  - [x] Real-time state updates via WidgetManager events

#### Widget Renaming
- [x] Update display names in UI ("The MRT Simplicity" for GearGauge)
- [x] Keep WidgetType.GearGauge enum (no breaking changes)
- [x] Test widget toggles work correctly

#### Integration & Testing
- [x] Wire up OverlayView to MainWindow navigation
- [x] Test widget on/off toggles
- [x] Test opacity slider changes widget transparency
- [x] Test size slider changes widget dimensions
- [x] Build successful (0 errors, 0 warnings)

**Deliverables:**
- ✅ Widget list with working ON/OFF toggles
- ✅ Widget configuration panel (opacity, size)
- ✅ "The MRT Simplicity" widget displayed correctly
- ✅ All 4 widgets can be managed from UI
- ✅ Real-time position tracking
- ✅ Professional 3-panel layout (list, preview placeholder, config)

---

## 📅 Week 2: Settings, Polish & Testing

### Day 6-7: Settings View ✅
**Goal:** Global settings management with clean interface

#### Settings View Creation
- [x] Create `Views/SettingsView.xaml`
  - [x] Add "General Settings" section
    - [x] Units selection
      - [x] ⚪ Metric (km/h, L, °C)
      - [x] ⚪ Imperial (mph, gal, °F)
    - [x] Current unit display
  
  - [x] Add "Widget Controls" section
    - [x] Lock widgets toggle (☐ ON/OFF)
    - [x] Hotkey configuration
      - [x] Display current hotkey [F12]
      - [x] Button to change hotkey (Phase 1.5)
      - [x] Description: "Lock/unlock widget positions"
  
  - [x] Add "Manager Window" section
    - [x] Manager window opacity slider
    - [x] Always on top toggle
    - [x] Start minimized option
  
  - [x] Add "About" section
    - [x] MRT UI version
    - [x] iRacing SDK version
    - [x] GitHub link / documentation link

#### Settings ViewModel
- [x] Create `ViewModels/SettingsViewModel.cs`
  - [x] Bind to existing AppSettings properties
  - [x] Unit selection command
  - [x] Lock widgets toggle command
  - [x] Hotkey display (read-only for now)
  - [x] Manager opacity change handler
  - [x] Save settings method
  - [x] Reset to defaults method

#### Integration
- [x] Wire up SettingsView to MainWindow navigation
- [x] Test unit switching updates widgets
- [x] Test lock widgets toggle
- [x] Verify settings save correctly
- [x] Build and test settings page

**Deliverables:**
- ✅ Functional settings page with 2-panel layout (tab navigation + content)
- ✅ Unit switching (Metric/Imperial) with immediate refresh
- ✅ Lock widgets toggle with 🔓/🔒 visual indicator
- ✅ Manager opacity control (20-100% enforced minimum)
- ✅ Always on top and start minimized options
- ✅ Settings persistence with immediate save
- ✅ About section with version info
- ✅ Reset all settings feature with confirmation dialog
- ✅ Professional Discord-style tab layout

---

### Day 8: Window Management & Polish ✅
**Goal:** Professional window behavior and final touches

#### Window Features
- [x] Implement window state persistence
  - [x] Save/restore window size (defaults to 1280x720)
  - [x] Save/restore window position
  - [x] Save/restore maximized state
  - [x] Handle monitor disconnection gracefully
  - [x] Center on first launch
  - [x] Settings stored in Documents\MRT-UI\settings.json
- [x] Implement StartMinimized feature
  - [x] Apply WindowState.Minimized when setting enabled
  - [x] Test minimize/restore from taskbar
- [x] Window state management
  - [x] ApplyWindowSettings() on startup
  - [x] SaveWindowState() on close, move, resize, state change
  - [x] IsPositionOnScreen() validation for multi-monitor
- ⏸️ Add window icon (MRT logo) - Skipped per user request

#### Widget Management UI Redesign
- [x] Remove individual widget toggles from widget list
- [x] Make widget items selectable (button-style with selection highlight)
- [x] Add "ACTIVE" badge to active widgets
- [x] Add Activate/Deactivate button to configuration panel
  - [x] Single toggle button with dynamic text/icon
  - [x] 🟢 "Activate Widget" when inactive
  - [x] 🔴 "Deactivate Widget" when active
  - [x] Prominent placement at top of config panel
- [x] Implement ToggleActiveCommand in WidgetItemViewModel
- [x] Add IsSelected property for visual feedback

#### UI Compactness
- [x] Reduce padding 20px → 15px across all views
  - [x] SettingsView.xaml (12 locations)
  - [x] DashboardView.xaml (all cards)
  - [x] OverlayView.xaml (all panels)
- [x] Reduced button padding 20,10 → 15,8 where applicable
- [x] Test at 1280x720 resolution for improved layout

#### Code Cleanup
- ⏸️ XML documentation comments (deferred to next phase)
- [x] Verify no compiler warnings
- [x] Format code consistently

**Deliverables:**
- ✅ Window state save/restore system with JSON persistence
- ✅ StartMinimized feature functional
- ✅ Widget activation via single button (no more toggles in list)
- ✅ Cleaner, more compact UI (15px padding standard)
- ✅ Professional widget selection UI with visual feedback
- ✅ Clean build with 0 errors, 0 warnings

---

### Day 9-10: Testing & Documentation
**Goal:** Comprehensive testing and user documentation

#### Functional Testing
- [ ] Test all navigation flows
  - [ ] Dashboard → Overlay → Settings → Dashboard
  - [ ] All buttons respond correctly
- [ ] Test connection status updates
  - [ ] Start without iRacing (🔴 Not Connected)
  - [ ] Launch iRacing (🟡 Connecting → 🟢 Connected)
  - [ ] Close iRacing (🔴 Disconnected)
- [ ] Test widget management
  - [ ] Select widget in list
  - [ ] Click Activate → widget appears
  - [ ] Change opacity for active widget
  - [ ] Change size for active widget
  - [ ] Click Deactivate → widget closes
  - [ ] Verify widgets persist positions
- [ ] Test settings persistence
  - [ ] Change settings, close app, reopen → settings restored
  - [ ] Test window size/position restore
  - [ ] Test StartMinimized feature
  - [ ] Test on multi-monitor setup
- [ ] Test settings
  - [ ] Switch metric/imperial
  - [ ] Lock/unlock widgets
  - [ ] Change manager opacity
- [ ] Test window management
  - [ ] 🟢 Green for connected
  - [ ] 🟡 Yellow for connecting
  - [ ] 🔴 Red for not connected/disconnected

#### Code Cleanup
- [ ] Remove old MainWindow code (commented sections)
- [ ] Add XML documentation comments
- [ ] Verify no compiler warnings
- [ ] Clean up unused using statements
- [ ] Format code consistently

**Deliverables:**
- ✅ Professional window management
- ✅ Polished UI with consistent styling
- ✅ Clean, documented code
- ✅ No build warnings

---

### Day 9-10: Testing & Documentation
**Goal:** Comprehensive testing and user documentation

#### Functional Testing
- [ ] Test all navigation flows
  - [ ] Dashboard → Overlay → Settings → Dashboard
  - [ ] All buttons respond correctly
- [ ] Test connection status updates
  - [ ] Start without iRacing (🔴 Not Connected)
  - [ ] Launch iRacing (🟡 Connecting → 🟢 Connected)
  - [ ] Close iRacing (🔴 Disconnected)
- [ ] Test widget management
  - [ ] Toggle each widget on/off
  - [ ] Change opacity for each widget
  - [ ] Change size for each widget
  - [ ] Verify widgets persist positions
- [ ] Test settings
  - [ ] Switch metric/imperial
  - [ ] Lock/unlock widgets
  - [ ] Change manager opacity
- [ ] Test window management
  - [ ] Minimize to taskbar
  - [ ] Restore from taskbar
  - [ ] Maximize window
  - [ ] Close and restart (settings persist)

#### Edge Case Testing
- [ ] Start app before iRacing launches
- [ ] Start iRacing before app launches
- [ ] Disconnect during active session
- [ ] Multiple app restarts
- [ ] Fast widget toggle on/off
- [ ] Extreme opacity values (0%, 100%)
- [ ] Extreme size values (50%, 200%)

#### Performance Testing
- [ ] CPU usage < 2% when idle
- [ ] Memory usage < 100MB
- [ ] Smooth page transitions
- [ ] No UI lag during telemetry updates
- [ ] Widget toggles respond instantly

#### Documentation Updates
- [ ] Update `README_USER.md`
  - [ ] Add MRT UI Manager section
  - [ ] Document new navigation
  - [ ] Add screenshots (optional)
- [ ] Update `QUICKSTART.md`
  - [ ] New UI walkthrough
  - [ ] Widget management guide
  - [ ] Settings explanation
- [ ] Update `PROJECT_STATUS.md`
  - [ ] Mark Phase 1 complete
  - [ ] Document new features
  - [ ] Update metrics
- [ ] Create `docs/MRT_UI_GUIDE.md`
  - [ ] Detailed manager UI guide
  - [ ] Navigation explanation
  - [ ] Widget configuration tutorial
  - [ ] Settings reference

#### Build & Release Preparation
- [ ] Clean build (no warnings)
- [ ] Test Release configuration
- [ ] Verify all files included
- [ ] Update version number (v0.5.0)
- [ ] Update launcher scripts if needed

**Deliverables:**
- ✅ Fully tested MRT UI Manager
- ✅ All features working correctly
- ✅ Updated documentation
- ✅ Ready for Phase 1 completion

---

## 🎨 Design Specifications

### Color Palette (MRT Theme)
```
Primary:
- Teal:           #008080
- Orange:         #FF8000
- White:          #FFFFFF
- Black:          #000000

Backgrounds:
- Dark:           #1A1A1A
- Dark Gray:      #2F2F2F
- Charcoal:       #2F2F2F

Accents:
- Deep Teal:      #006666
- Bright Orange:  #FF9933
- Silver Gray:    #C0C0C0
```

### Typography
```
Headers:   18-24px, Bold
Body Text: 14-16px, Regular
Labels:    12-14px, Medium
Status:    16px, Bold (with emojis)
```

### Spacing Standards
```
Card Padding:      15px
Section Margins:   20px
Button Padding:    10px 20px
Minimum Spacing:   10px between elements
```

---

## 🚀 Success Criteria

### Must-Have (Phase 1 Complete)
- ✅ MRT UI Manager launches in fullscreen (1920x1080)
- ✅ Navigation works (Dashboard, Overlay, Settings)
- ✅ Connection status displays correctly with real-time updates
- ✅ All 4 widgets can be toggled on/off from UI
- ✅ Widget opacity and size controls work
- ✅ Settings page functional (units, lock widgets)
- ✅ Application minimizes to taskbar
- ✅ Settings persist across restarts
- ✅ Build completes with no warnings
- ✅ Documentation updated

### Nice-to-Have (Phase 1.5)
- ⏳ Live preview visualization in Overlay page
- ⏳ Hotkey customization UI
- ⏳ Smooth page transition animations
- ⏳ System tray icon integration
- ⏳ Advanced widget positioning controls

### Performance Targets
- ✅ CPU usage < 2% when idle
- ✅ Memory usage < 100MB
- ✅ UI response time < 50ms
- ✅ Zero crashes during 4-hour session

---

## 📁 Files Created/Modified

### New Files
```
src/iRacingOverlay.WPF/
├── Resources/
│   └── Themes/
│       └── MRTTheme.xaml                    [NEW]
├── Views/
│   ├── DashboardView.xaml                   [NEW]
│   ├── DashboardView.xaml.cs                [NEW]
│   ├── OverlayView.xaml                     [NEW]
│   ├── OverlayView.xaml.cs                  [NEW]
│   ├── SettingsView.xaml                    [NEW]
│   └── SettingsView.xaml.cs                 [NEW]
└── ViewModels/
    ├── NavigationViewModel.cs               [NEW]
    ├── DashboardViewModel.cs                [NEW]
    ├── OverlayViewModel.cs                  [NEW]
    └── SettingsViewModel.cs                 [NEW]
```

### Modified Files
```
src/iRacingOverlay.WPF/
├── App.xaml                                 [MODIFIED - Add theme]
├── MainWindow.xaml                          [REDESIGN]
├── MainWindow.xaml.cs                       [MODIFIED - Navigation]
└── Models/
    └── WidgetType.cs                        [MODIFIED - Display names]
```

### Backup Files
```
MainWindow.xaml.backup                       [BACKUP]
MainWindow.xaml.cs.backup                    [BACKUP]
```

---

## 🐛 Known Issues / Technical Debt

### To Address in Phase 1
- [ ] None yet (will track as discovered)

### Deferred to Phase 1.5
- [ ] Live preview visualization (complex)
- [ ] Hotkey customization UI
- [ ] Drag-and-drop widget positioning
- [ ] Advanced layout templates

---

## 📝 Notes

### Design Decisions
- **Navigation Style:** Button-based (Dashboard/Overlay/Settings) instead of sidebar for simplicity
- **Theme:** Dark background (#1A1A1A) with teal/orange accents for racing aesthetic
- **Modularity:** Each view is a separate UserControl for easy replacement
- **Backend:** No changes to WidgetManager or AppSettings - keeps existing architecture

### User Feedback Integration
- Keep interface minimal and compact
- Use emojis for quick visual indicators
- Focus on functionality over decoration
- Easy to navigate without training

---

## ✅ Phase 1 Completion Checklist

- [ ] All Day 1-10 tasks completed
- [ ] All widgets working correctly
- [ ] Connection status accurate
- [ ] Settings persist
- [ ] Documentation updated
- [ ] Build successful (no warnings)
- [ ] Testing complete (functional + performance)
- [ ] Git commit with detailed message
- [ ] Tag as v0.5.0
- [ ] Update DEVELOPMENT_ROADMAP.md progress

**Phase 1 Status:** 🚧 In Progress (0/10 days complete)

---

**Next Phase:** Phase 1.5 - Live Preview & Widget Positioning Enhancements
