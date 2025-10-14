# 🚀 iRacing Telemetry Overlay - Development Roadmap
**Created:** October 14, 2025  
**Version:** v0.4 → v1.0

---

## 📍 Current State (v0.4)

### ✅ What Works Now
- **4 Widget Types**: Speed, GearGauge, Data (2x3 grid), Fuel Calculator
- **Telemetry Service**: Real-time iRacing SDK integration (25-60 Hz)
- **Manager UI**: WPF configuration window with tabs for each widget
- **Settings System**: JSON persistence with metric/imperial units
- **Color Scheme**: Unified teal/orange theme with warning colors
- **Widget Architecture**: WidgetBase abstract class, WidgetManager lifecycle

### 🏗️ Architecture Strengths
- **Clean Separation**: Core (telemetry) ↔ WPF (UI)
- **MVVM Pattern**: ViewModels and data binding throughout
- **Event-Driven**: Real-time updates via event handlers
- **Extensible**: Easy to add new widgets via factory pattern
- **Well-Tested**: Builds cleanly, runs stably

### 🐛 Current Limitations
1. **Manager UI**: Basic tab-based config (functional but not polished)
2. **Widget Positioning**: Manual positioning only (no snap-to-grid)
3. **Limited Widgets**: Only 4 types (many telemetry fields unused)
4. **No Profiles**: Single layout only (no multi-car/track configs)
5. **Static Layout**: Widgets can't be added/removed at runtime easily
6. **Basic Visualization**: Simple text/gauge displays only

---

## 🎯 Strategic Decision: What to Build Next?

### Option A: Polish the UI First ⭐ **RECOMMENDED**
**Why:** The backend is solid, but the user experience needs work.

**Pros:**
- Makes the app immediately more usable and attractive
- Demonstrates value to users quickly
- UI improvements are visible and motivating
- Backend can continue to evolve independently

**Cons:**
- Doesn't add new telemetry features
- May need UI refactoring later for advanced features

**Time:** 2-3 weeks

---

### Option B: Expand the Widget System
**Why:** Add more widget types to leverage existing telemetry data.

**Pros:**
- Maximizes use of already-extracted telemetry
- Each new widget adds immediate functional value
- Builds on proven widget architecture
- Can be done incrementally

**Cons:**
- Doesn't improve core UX issues
- Config management becomes more complex
- May overwhelm users without better UI

**Time:** 1-2 weeks per widget type

---

### Option C: Enhance Backend/Telemetry
**Why:** Add more sophisticated data processing and features.

**Pros:**
- Enables advanced features (predictive fuel, AI coaching)
- More robust connection handling
- Better performance optimization
- Future-proofs the architecture

**Cons:**
- Benefits not immediately visible to users
- Current telemetry system is already working well
- Risk of over-engineering before validating needs

**Time:** 3-4 weeks

---

## ⭐ Recommended Path: UI-First Strategy

### Phase 1: Modern Manager UI (1-2 weeks) 🚧 IN PROGRESS
**Goal:** Replace tab-based config with intuitive manager window

**Status:** Started October 14, 2025 - Day 1 of 10  
**Progress:** 0% - Beginning foundation and theme setup

**Features:**
- **3-Page Navigation**: Dashboard (connection status), Overlay (widget management), Settings
- **MRT Branding**: Teal (#008080) + Orange (#FF8000) color theme
- **Widget Management**: ON/OFF toggles, opacity/size controls for 4 widgets
- **Connection Monitoring**: Real-time status display (🔴🟡🟢)
- **Settings Persistence**: Units, lock widgets, window preferences

**Technical:**
- Keep existing WidgetManager backend
- Add WPF drag-drop handlers
- Implement visual widget templates
- Add preview layer system

**Files to Create:**
```
src/iRacingOverlay.WPF/
├── Views/
│   ├── WidgetGalleryView.xaml          # Visual widget picker
│   ├── LivePreviewPanel.xaml           # Real-time preview
│   └── StatusBarView.xaml              # Bottom status bar
├── ViewModels/
│   ├── WidgetGalleryViewModel.cs       # Gallery logic
│   └── ManagerViewModel.cs             # Main manager state
└── Controls/
    └── WidgetTemplateControl.cs        # Reusable widget preview
```

---

### Phase 2: Widget Enhancements (1 week)
**Goal:** Improve existing widgets with better visuals

**Improvements:**
- **Data Widget**: Add sparklines for trending values
- **Fuel Widget**: Add lap-by-lap fuel graph
- **Gear Gauge**: Add shift point indicator LEDs
- **Speed Widget**: Add acceleration/deceleration arrows

**Technical:**
- Keep existing widget structure
- Add custom WPF controls for visualizations
- Leverage existing telemetry data
- No new backend changes needed

---

### Phase 3: Smart Layout System (1 week)
**Goal:** Make positioning and sizing easier

**Features:**
- **Snap-to-Grid**: Widgets align to invisible grid
- **Smart Anchors**: Widgets can attach to screen edges
- **Layout Templates**: Predefined layouts (oval, road, endurance)
- **Multi-Monitor**: Detect and support multiple displays

**Technical:**
- Add GridHelper utility class
- Implement anchor point system
- Create LayoutTemplate model
- Add monitor detection logic

---

### Phase 4: Profile System (1 week)
**Goal:** Support multiple layouts for different scenarios

**Features:**
- **Named Profiles**: "GT3 Sprint", "Oval Racing", "Endurance"
- **Quick Switch**: Hotkey to change profiles
- **Auto-Load**: Detect car/track and load appropriate profile
- **Import/Export**: Share profiles with other users

**Technical:**
- Extend AppSettings to support profiles
- Add ProfileManager service
- Implement car/track detection
- Create JSON import/export

---

### Phase 5: Polish & Release (1 week)
**Goal:** Production-ready v1.0 release

**Tasks:**
- Comprehensive testing (all widgets, all layouts)
- Performance optimization (target <1% CPU)
- User documentation (quick start guide, video)
- Installer creation (MSI package)
- GitHub release with binaries

---

## 🔮 Future Phases (Post v1.0)

### Phase 6: New Widget Types
- **Timing Widget**: Sector times, delta to best
- **Relative Widget**: Nearby cars (like iRacing's F3)
- **Track Map Widget**: Position on track visualization
- **Input Widget**: Brake/throttle/steering traces

### Phase 7: Advanced Features
- **Predictive Fuel**: Machine learning for fuel estimates
- **AI Coach**: Suggest braking points, racing line
- **Telemetry Recording**: Save sessions for analysis
- **Web Dashboard**: Browser-based configuration

### Phase 8: Community Features
- **Widget Marketplace**: Download community widgets
- **Theme System**: Custom color schemes
- **Plugin API**: Third-party widget development
- **Cloud Sync**: Sync profiles across machines

---

## 📊 Development Metrics

### Current Codebase:
- **Total Lines:** ~8,500 (estimated)
- **Projects:** 2 (Core, WPF)
- **Widgets:** 4 types
- **Models:** 10+ data models
- **Build Time:** 1-2 seconds ✅

### v1.0 Target:
- **Total Lines:** ~12,000
- **Widgets:** 6-8 types
- **Features:** Manager UI, Profiles, Smart Layout
- **Performance:** <1% CPU, <50MB RAM
- **Stability:** 0 crashes in 4-hour race session

---

## 🛠️ Technical Priorities

### Must-Have for v1.0:
1. **Modern Manager UI** - Users need better config experience
2. **Profile System** - Essential for multi-car/track usage
3. **Stability** - Zero crashes, graceful error handling
4. **Performance** - Minimal resource usage
5. **Documentation** - Clear user guide and examples

### Nice-to-Have:
1. **More Widgets** - Timing, Relative, Track Map
2. **Themes** - Custom color schemes
3. **Telemetry Recording** - Save sessions for review
4. **Advanced Visuals** - Graphs, animations, effects

### Post-v1.0:
1. **AI Features** - Predictive fuel, coaching
2. **Plugin System** - Community widget development
3. **Web Dashboard** - Browser-based config
4. **Mobile Companion** - Phone app for remote control

---

## 💡 Key Insights

### Why UI First?
1. **User Value**: Immediate improvement in usability
2. **Motivation**: Visible progress keeps development momentum
3. **Foundation**: Better UI enables future feature discovery
4. **Marketing**: Polished UI attracts more users/contributors

### Architecture Philosophy:
- **Backend is Solid**: Don't fix what isn't broken
- **UI Needs Work**: Current manager is functional but basic
- **Iterative**: Small, working increments
- **User-Focused**: Build what users will actually use

### Success Criteria:
- ✅ **Usable**: Non-technical users can configure widgets
- ✅ **Stable**: Runs for hours without issues
- ✅ **Performant**: <1% CPU, <50MB RAM
- ✅ **Attractive**: Modern, polished appearance
- ✅ **Documented**: Clear guides and examples

---

## 🎬 Next Steps

1. **Review this roadmap** - Does UI-first make sense?
2. **Start Phase 1** - Begin Modern Manager UI
3. **Create mockups** - Sketch new UI layout
4. **Prototype** - Build basic drag-drop widget gallery
5. **Iterate** - Test, refine, improve

**Ready to start Phase 1?** Let's build a modern, intuitive manager UI that makes MRTui a joy to use! 🚀
