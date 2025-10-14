# Phase 1: MRT UI Manager - Modern Interface Redesign

**Duration:** 2 weeks (10 working days)  
**Status:** 🚧 In Progress  
**Started:** October 14, 2025  
**Target Completion:** October 28, 2025

---

## 🎯 Phase Goals

Transform the basic tab-based configuration window into a modern, full-screen MRT UI Manager with:
- Professional branding (Mokka Racing Team colors)
- Intuitive navigation (Dashboard, Overlay, Settings)
- Modular architecture for easy future redesigns
- Connection status monitoring
- Enhanced widget management interface

---

## 📋 Pre-Phase Checklist

### Design & Planning
- [x] Analyze current MainWindow.xaml structure
- [x] Create UI/UX requirements document
- [x] Define color scheme (Teal #008080 + Orange #FF8000)
- [x] Plan navigation structure (3 pages)
- [x] Identify modular components needed
- [ ] Create UI mockups/wireframes (optional)

### Technical Preparation
- [x] Review existing backend services (WidgetManager, AppSettings)
- [x] Verify current widget system functionality
- [x] Backup current MainWindow files
- [x] Plan file structure for Views/ViewModels
- [ ] Set up theme resource dictionary structure

---

## 📅 Week 1: Core Structure & Navigation

### Day 1-2: Foundation & Theme Setup
**Goal:** Create navigation shell and apply MRT branding

#### App-Level Theme Configuration
- [ ] Create `Resources/Themes/MRTTheme.xaml`
  - [ ] Define color resources (Teal, Orange, backgrounds)
  - [ ] Create button styles
  - [ ] Create toggle switch styles
  - [ ] Create slider styles
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

### Day 3: Dashboard View (Front Page)
**Goal:** Create professional dashboard with connection monitoring

#### Dashboard View Creation
- [ ] Create `Views/DashboardView.xaml`
  - [ ] Add welcome header "MRT UI - iRacing Telemetry Manager"
  - [ ] Create connection status card
    - [ ] Status indicator (🔴 Not Connected / 🟡 Connecting / 🟢 Connected / 🔴 Disconnected)
    - [ ] Connection uptime display
    - [ ] Last update timestamp
  - [ ] Add quick stats section
    - [ ] Active widgets count
    - [ ] Telemetry update rate (Hz)
    - [ ] Session info (if available)
  - [ ] Add MRT branding/logo area
  - [ ] Style with teal/orange theme

- [ ] Create `ViewModels/DashboardViewModel.cs`
  - [ ] Connection status property (bound to telemetry service)
  - [ ] Uptime calculation
  - [ ] Active widget count
  - [ ] Update rate tracking
  - [ ] INotifyPropertyChanged implementation

- [ ] Wire up Dashboard to MainWindow navigation
- [ ] Test connection status updates in real-time
- [ ] Build and verify dashboard displays correctly

**Deliverables:**
- ✅ Functional dashboard showing connection status
- ✅ Real-time status updates from telemetry service
- ✅ Clean, professional layout with MRT branding

---

### Day 4-5: Overlay Manager View (Widget Management)
**Goal:** Create widget management interface with toggles and configuration

#### Overlay View - Widget List
- [ ] Create `Views/OverlayView.xaml`
  - [ ] Create left panel for widget list
    - [ ] Add widget item template (name + toggle)
    - [ ] List all available widgets:
      - [ ] 🔘 The MRT Simplicity (formerly GearGauge)
      - [ ] 🔘 Speed Widget
      - [ ] 🔘 Data Widget
      - [ ] 🔘 Fuel Calculator
    - [ ] Style toggle switches (orange when active)
  
  - [ ] Create right panel for widget configuration
    - [ ] Opacity slider (0-100%)
    - [ ] Size slider (50-200%)
    - [ ] Position display (X, Y coordinates)
    - [ ] Widget-specific settings area
    - [ ] Apply/Reset buttons
  
  - [ ] Create center placeholder for future live preview
    - [ ] Gray box with "Live Preview - Coming Soon"
    - [ ] Or simple text: "Changes apply to widgets immediately"

#### Overlay View - ViewModel
- [ ] Create `ViewModels/OverlayViewModel.cs`
  - [ ] Widget list collection (ObservableCollection)
  - [ ] Selected widget property
  - [ ] Toggle command for each widget
  - [ ] Opacity/size change handlers
  - [ ] Connect to existing WidgetManager
  - [ ] Widget creation/destruction methods

#### Widget Renaming
- [ ] Update `WidgetType.cs` enum
  - [ ] Add new entry or rename: `MRTSimplicity` or keep `GearGauge`
- [ ] Update display names in UI
- [ ] Update `WidgetManager.cs` if needed for display names
- [ ] Test widget toggles work correctly

#### Integration & Testing
- [ ] Wire up OverlayView to MainWindow navigation
- [ ] Test widget on/off toggles
- [ ] Test opacity slider changes widget transparency
- [ ] Test size slider changes widget dimensions
- [ ] Verify changes persist in AppSettings
- [ ] Build and test all widget controls work

**Deliverables:**
- ✅ Widget list with working ON/OFF toggles
- ✅ Widget configuration panel (opacity, size)
- ✅ "The MRT Simplicity" widget renamed/displayed
- ✅ All 4 widgets can be managed from UI
- ✅ Settings persist across app restarts

---

## 📅 Week 2: Settings, Polish & Testing

### Day 6-7: Settings View
**Goal:** Global settings management with clean interface

#### Settings View Creation
- [ ] Create `Views/SettingsView.xaml`
  - [ ] Add "General Settings" section
    - [ ] Units selection
      - [ ] ⚪ Metric (km/h, L, °C)
      - [ ] ⚪ Imperial (mph, gal, °F)
    - [ ] Current unit display
  
  - [ ] Add "Widget Controls" section
    - [ ] Lock widgets toggle (☐ ON/OFF)
    - [ ] Hotkey configuration
      - [ ] Display current hotkey [F12]
      - [ ] Button to change hotkey (Phase 1.5)
      - [ ] Description: "Lock/unlock widget positions"
  
  - [ ] Add "Manager Window" section
    - [ ] Manager window opacity slider
    - [ ] Always on top toggle
    - [ ] Start minimized option
  
  - [ ] Add "About" section
    - [ ] MRT UI version
    - [ ] iRacing SDK version
    - [ ] GitHub link / documentation link

#### Settings ViewModel
- [ ] Create `ViewModels/SettingsViewModel.cs`
  - [ ] Bind to existing AppSettings properties
  - [ ] Unit selection command
  - [ ] Lock widgets toggle command
  - [ ] Hotkey display (read-only for now)
  - [ ] Manager opacity change handler
  - [ ] Save settings method
  - [ ] Reset to defaults method

#### Integration
- [ ] Wire up SettingsView to MainWindow navigation
- [ ] Test unit switching updates widgets
- [ ] Test lock widgets toggle
- [ ] Verify settings save correctly
- [ ] Build and test settings page

**Deliverables:**
- ✅ Functional settings page
- ✅ Unit switching (Metric/Imperial)
- ✅ Lock widgets toggle
- ✅ Settings persistence
- ✅ About section with version info

---

### Day 8: Window Management & Polish
**Goal:** Professional window behavior and final touches

#### Window Features
- [ ] Implement fullscreen mode
  - [ ] Set initial size to 1920x1080
  - [ ] Add maximize/restore functionality
  - [ ] Ensure minimum size constraint (1280x720)
- [ ] Implement minimize to taskbar
  - [ ] Test minimize/restore from taskbar
  - [ ] Optional: System tray icon
- [ ] Add window icon (MRT logo)
- [ ] Test multi-monitor support

#### UI Polish
- [ ] Review all spacing and alignment
  - [ ] Consistent margins (15px standard)
  - [ ] Button sizes uniform
  - [ ] Text sizes appropriate
- [ ] Add hover effects to buttons
- [ ] Add focus indicators
- [ ] Smooth page transitions (optional fade)
- [ ] Connection status color accuracy
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
