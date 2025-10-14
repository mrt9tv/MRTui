# Phase 1 Quick Checklist - MRT UI Manager

**Quick reference for tracking Phase 1 progress**

---

## 🎯 Overall Progress: 20% Complete

### Pre-Phase Setup ✅
- [x] Requirements analysis complete
- [x] Color scheme defined
- [x] Architecture planned
- [x] Ready to start coding

---

## Week 1: Core Structure (Days 1-5)

### Day 1-2: Foundation ✅
- [x] Create MRTTheme.xaml with colors/styles
- [x] Redesign MainWindow navigation shell
- [x] Add status bar for connection
- [x] Test navigation works

### Day 3: Dashboard ⏳
- [ ] Create DashboardView.xaml
- [ ] Create DashboardViewModel.cs
- [ ] Show connection status
- [ ] Display quick stats
- [ ] Test real-time updates

### Day 4-5: Overlay Manager ⏳
- [ ] Create OverlayView.xaml
- [ ] Create OverlayViewModel.cs
- [ ] List all 4 widgets with toggles
- [ ] Add opacity/size sliders
- [ ] Rename GearGauge → "The MRT Simplicity"
- [ ] Test widget controls work

---

## Week 2: Settings & Polish (Days 6-10)

### Day 6-7: Settings Page ⏳
- [ ] Create SettingsView.xaml
- [ ] Create SettingsViewModel.cs
- [ ] Add unit selection (Metric/Imperial)
- [ ] Add lock widgets toggle
- [ ] Display hotkey info
- [ ] Test settings persist

### Day 8: Window Management ⏳
- [ ] Implement fullscreen (1920x1080)
- [ ] Add minimize to taskbar
- [ ] Polish UI spacing/alignment
- [ ] Add hover effects
- [ ] Code cleanup

### Day 9-10: Testing & Documentation ⏳
- [ ] Test all navigation
- [ ] Test connection status updates
- [ ] Test widget management
- [ ] Test settings changes
- [ ] Performance testing
- [ ] Update documentation
- [ ] Git commit & tag v0.5.0

---

## 📊 Feature Completion

### Must-Have Features
- [ ] 3-page navigation (Dashboard, Overlay, Settings)
- [ ] Connection status monitoring
- [ ] Widget ON/OFF toggles (all 4 widgets)
- [ ] Opacity control (per widget)
- [ ] Size control (per widget)
- [ ] Unit switching (Metric/Imperial)
- [ ] Lock widgets toggle
- [ ] Minimize to taskbar
- [ ] Settings persistence

### Widget List Status
- [ ] 🔘 The MRT Simplicity (GearGauge renamed)
- [ ] 🔘 Speed Widget
- [ ] 🔘 Data Widget
- [ ] 🔘 Fuel Calculator

---

## 🎨 Theme Application
- [x] Teal (#008080) primary color applied
- [x] Orange (#FF8000) accent color applied
- [x] Dark background (#1A1A1A)
- [x] White text on dark
- [x] Connection status colors (🔴🟡🟢)

---

## 📝 Documentation Status
- [ ] README_USER.md updated
- [ ] QUICKSTART.md updated
- [ ] PROJECT_STATUS.md updated
- [ ] New MRT_UI_GUIDE.md created

---

## ✅ Phase 1 Complete When:
- [ ] All checkboxes above are checked
- [ ] Build succeeds with 0 warnings
- [ ] All 4 widgets controllable from UI
- [ ] Connection status works in real-time
- [ ] Settings persist across app restarts
- [ ] Documentation complete
- [ ] Git tagged as v0.5.0

---

**Current Status:** 🚧 Day 1-2 Complete - Foundation & Theme Setup ✅

**Next Task:** Create DashboardView.xaml (Day 3)
