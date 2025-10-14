# iRacing Overlay - Manageable Development Phases
**Simplified Roadmap** | **Created:** October 12, 2025

---

## 🎯 Overview

This document breaks down the comprehensive development plan into **manageable, actionable phases** that can be realistically completed by a small team or solo developer.

**Core Philosophy:** Start small, deliver working software frequently, iterate based on feedback.

---

## 📊 Phase Comparison: Original vs Manageable

| Original Plan | Duration | Manageable Plan | Duration |
|---------------|----------|-----------------|----------|
| Phase 1: Foundation | 2-3 weeks | **MVP 1: Basic Connection** | 3-5 days |
| | | **MVP 2: Simple Overlay** | 3-5 days |
| Phase 2: Core Features | 3-4 weeks | **MVP 3: Essential Widget** | 1 week |
| | | **MVP 4: Multi-Widget System** | 1-2 weeks |
| Phase 3: Web Config | 4-5 weeks | **MVP 5: Basic Config UI** | 1-2 weeks |
| | | **MVP 6: Advanced Config** | 2-3 weeks |
| Phase 4: AI Features | 5-6 weeks | **Future: AI Features** | TBD |

---

# 🚀 MVP 1: Basic Connection & Telemetry
**Duration:** 3-5 days  
**Goal:** Connect to iRacing and receive telemetry data

## What You'll Build
A console application that:
- Connects to iRacing when the sim is running
- Receives telemetry data at 60Hz
- Logs basic data (speed, RPM, gear) to console
- Handles disconnections gracefully

## Success Criteria
✅ Application detects when iRacing is running  
✅ Receives telemetry updates consistently  
✅ Logs at least 5 data points to console  
✅ Reconnects automatically if connection drops  
✅ No crashes during 30-minute test session  

## Key Files to Create
```
iRacingOverlay/
├── Program.cs                    # Entry point
├── TelemetryService.cs          # SDK wrapper
└── appsettings.json             # Basic config
```

## Implementation Checklist
- [ ] Create new .NET 8 console project
- [ ] Install `SVappsLAB.iRacingTelemetrySDK` NuGet package
- [ ] Create basic `TelemetryService` class
- [ ] Implement connection detection
- [ ] Add telemetry data handler
- [ ] Test with live iRacing session
- [ ] Add basic error handling

## Time Breakdown
- **Day 1:** Project setup + SDK installation (2-3 hours)
- **Day 2:** Basic connection logic (4-5 hours)
- **Day 3:** Telemetry handling + logging (4-5 hours)
- **Day 4:** Error handling + reconnection (3-4 hours)
- **Day 5:** Testing + bug fixes (2-3 hours)

---

# 🎨 MVP 2: Simple Overlay Window
**Duration:** 3-5 days  
**Goal:** Display telemetry data in a transparent overlay

## What You'll Build
A WPF overlay that:
- Shows as transparent window on top of iRacing
- Displays 5 key metrics (Speed, RPM, Gear, Lap, Position)
- Updates in real-time (60Hz)
- Can be moved and resized

## Success Criteria
✅ Transparent overlay visible over iRacing  
✅ Shows at least 5 telemetry values  
✅ Updates smoothly at 60Hz  
✅ Window stays on top of game  
✅ Can be repositioned by dragging  

## Key Files to Create
```
iRacingOverlay.Overlay/
├── App.xaml / App.xaml.cs
├── OverlayWindow.xaml           # Main overlay
├── OverlayWindow.xaml.cs        # Code-behind
└── ViewModels/
    └── OverlayViewModel.cs      # Data binding
```

## Implementation Checklist
- [ ] Create WPF project
- [ ] Design transparent window (XAML)
- [ ] Implement click-through functionality
- [ ] Create ViewModel for data binding
- [ ] Wire telemetry service to UI
- [ ] Add basic styling (colors, fonts)
- [ ] Test overlay positioning
- [ ] Optimize rendering performance

## Time Breakdown
- **Day 1:** WPF project setup + window design (3-4 hours)
- **Day 2:** Transparency + click-through (3-4 hours)
- **Day 3:** Data binding + ViewModel (4-5 hours)
- **Day 4:** Connect telemetry to UI (3-4 hours)
- **Day 5:** Styling + performance testing (3-4 hours)

---

# 📱 MVP 3: First Production Widget (Relative)
**Duration:** 1 week  
**Goal:** Build one fully-featured, production-quality widget

## What You'll Build
**Relative Position Widget** showing:
- Cars ahead and behind (±3 positions)
- Time gaps between cars
- Car numbers and driver names
- Position change indicators (↑↓)
- Color coding (same class, lapped cars)

## Success Criteria
✅ Shows 7 cars (3 ahead, you, 3 behind)  
✅ Time gaps accurate to 0.1 seconds  
✅ Updates smoothly without flicker  
✅ Handles position changes correctly  
✅ Works in practice and race sessions  

## Key Files to Create
```
iRacingOverlay.Widgets/
├── Base/
│   ├── IWidget.cs               # Widget interface
│   └── WidgetBase.cs            # Base class
└── Relative/
    ├── RelativeWidget.xaml      # UI
    ├── RelativeWidget.xaml.cs   # Code-behind
    ├── RelativeViewModel.cs     # Logic
    └── Models/
        └── DriverPosition.cs    # Data model
```

## Implementation Checklist
- [ ] Design widget interface (IWidget)
- [ ] Create base widget class
- [ ] Design Relative widget layout (XAML)
- [ ] Implement position tracking logic
- [ ] Calculate time gaps
- [ ] Add driver info display
- [ ] Implement color coding
- [ ] Add position change indicators
- [ ] Optimize for 60Hz updates
- [ ] Test with 40+ car field

## Time Breakdown
- **Days 1-2:** Widget architecture + base classes (8-10 hours)
- **Days 3-4:** Relative widget UI + logic (10-12 hours)
- **Days 5-6:** Gap calculation + styling (8-10 hours)
- **Day 7:** Testing + refinement (4-6 hours)

---

# 🏗️ MVP 4: Multi-Widget System
**Duration:** 1-2 weeks  
**Goal:** Support multiple widgets with basic layout management

## What You'll Build
- Widget management system
- 3-4 additional widgets (Fuel, Timing, Standings, Track Map)
- Basic positioning system (saved positions)
- Simple configuration file

## Success Criteria
✅ Can show 4+ widgets simultaneously  
✅ Each widget independently positioned  
✅ Widget positions saved between sessions  
✅ No performance degradation with multiple widgets  
✅ CPU usage still <8%  

## Widgets to Implement
1. **Fuel Calculator** (Basic)
   - Current fuel
   - Laps remaining
   - Fuel needed
   
2. **Timing Display** (Simple)
   - Current lap time
   - Last lap time
   - Best lap time
   - Delta to best

3. **Standings** (Essential)
   - Full field positions
   - Class positions
   - Lap down indicators

4. **Track Map** (Basic)
   - Simple track outline
   - Your position dot
   - Other cars (optional)

## Implementation Checklist
- [ ] Create widget manager service
- [ ] Implement widget lifecycle (load/unload)
- [ ] Add widget registration system
- [ ] Create JSON configuration for layouts
- [ ] Build Fuel Calculator widget
- [ ] Build Timing Display widget
- [ ] Build Standings widget
- [ ] Build basic Track Map widget
- [ ] Add widget show/hide functionality
- [ ] Implement position persistence
- [ ] Test with all widgets active

## Time Breakdown
- **Week 1:** Widget manager + 2 widgets (20-25 hours)
- **Week 2:** 2 more widgets + config system (15-20 hours)

---

# ⚙️ MVP 5: Basic Configuration UI
**Duration:** 1-2 weeks  
**Goal:** Simple web-based configuration interface

## What You'll Build
- Minimal web interface (React + TypeScript)
- Widget enable/disable controls
- Basic positioning adjustments
- Simple save/load functionality
- Local API for communication

## Success Criteria
✅ Web interface accessible at localhost  
✅ Can enable/disable each widget  
✅ Can adjust widget positions numerically  
✅ Changes apply in real-time  
✅ Settings persist after restart  

## Key Components
```
iRacingOverlay.Web/
├── ClientApp/                   # React frontend
│   ├── src/
│   │   ├── App.tsx
│   │   ├── components/
│   │   │   ├── WidgetList.tsx
│   │   │   └── WidgetSettings.tsx
│   │   └── services/
│   │       └── api.ts
└── Controllers/
    └── ConfigController.cs      # .NET API
```

## Implementation Checklist
- [ ] Create ASP.NET Core Web API project
- [ ] Set up React TypeScript frontend
- [ ] Create configuration API endpoints
- [ ] Build widget list component
- [ ] Add enable/disable toggles
- [ ] Create position adjustment inputs
- [ ] Implement save/load functionality
- [ ] Add real-time preview
- [ ] Test configuration changes

## Time Breakdown
- **Week 1:** API + basic React setup (12-15 hours)
- **Week 2:** UI components + integration (12-15 hours)

---

# 🎯 MVP 6: Advanced Configuration (Optional)
**Duration:** 2-3 weeks  
**Goal:** Drag-and-drop layout editor with themes

## What You'll Build
- Visual layout editor
- Drag-and-drop positioning
- Live preview
- Theme system (3-5 themes)
- Layout presets

## Success Criteria
✅ Can drag widgets to reposition  
✅ Live preview shows changes instantly  
✅ At least 3 themes available  
✅ 2-3 pre-built layouts  
✅ Export/import configuration  

## Implementation Checklist
- [ ] Implement drag-and-drop in React
- [ ] Create live preview system
- [ ] Design theme structure
- [ ] Build 3-5 themes
- [ ] Create layout presets
- [ ] Add export/import functionality
- [ ] Implement theme switcher
- [ ] Test across different resolutions

## Time Breakdown
- **Week 1:** Drag-and-drop + preview (15-18 hours)
- **Week 2:** Theme system (12-15 hours)
- **Week 3:** Presets + polish (10-12 hours)

---

# 🤖 Future: AI Features (Phase 4)
**When:** After MVP 6 is stable and adopted  
**Duration:** 5-6 weeks (when started)

## Planned Features
1. **Lap Time Prediction**
   - Real-time lap time estimates
   - Based on current pace

2. **Racing Line Optimization**
   - AI-suggested ideal line
   - Corner-by-corner analysis

3. **Performance Analysis**
   - Automated telemetry analysis
   - Identify improvement areas

4. **Anomaly Detection**
   - Unusual patterns detection
   - Setup problem identification

**Note:** This is a future enhancement. Focus on MVPs 1-6 first to build a solid foundation.

---

# 📋 Priority Matrix

## Must Have (MVP 1-3)
- ✅ SDK Connection
- ✅ Basic Overlay
- ✅ One Production Widget (Relative)

## Should Have (MVP 4-5)
- ✅ Multi-Widget System
- ✅ Basic Configuration UI
- ✅ 3-4 Essential Widgets

## Could Have (MVP 6)
- ✅ Advanced Config UI
- ✅ Drag-and-Drop Layout
- ✅ Theme System

## Won't Have (Initially)
- ❌ AI Features
- ❌ Cloud Sync
- ❌ Mobile App
- ❌ VR Support

---

# 🎯 Recommended Development Path

## Path 1: Solo Developer (Part-Time)
**Timeline:** 8-12 weeks total

1. **Weeks 1-2:** MVP 1-2 (Connection + Overlay)
2. **Weeks 3-4:** MVP 3 (First Widget)
3. **Weeks 5-7:** MVP 4 (Multi-Widget)
4. **Weeks 8-10:** MVP 5 (Basic Config)
5. **Weeks 11-12:** Testing + Bug Fixes

**Release Strategy:** Release after MVP 4 as "Alpha", MVP 5 as "Beta"

## Path 2: Small Team (2-3 Developers)
**Timeline:** 4-6 weeks total

1. **Week 1:** MVP 1-2 (parallel: connection + overlay)
2. **Week 2:** MVP 3 (pair on widget system)
3. **Week 3-4:** MVP 4 (divide widgets among team)
4. **Week 5:** MVP 5 (frontend + backend split)
5. **Week 6:** Testing + Polish

**Release Strategy:** Beta release after 5 weeks

## Path 3: Full-Time Development
**Timeline:** 2-3 weeks total

1. **Days 1-3:** MVP 1-2
2. **Days 4-8:** MVP 3-4
3. **Days 9-12:** MVP 5
4. **Days 13-15:** Testing + Release

**Release Strategy:** Beta after 2 weeks, v1.0 after 3 weeks

---

# ✅ Success Metrics (Simplified)

## MVP 1-2 Success
- [ ] Connects to iRacing reliably
- [ ] Overlay visible and responsive
- [ ] No crashes in 1-hour test session

## MVP 3-4 Success
- [ ] 4+ widgets working
- [ ] CPU usage <8%
- [ ] Memory usage <150MB
- [ ] Smooth 60Hz updates

## MVP 5-6 Success
- [ ] Configuration UI functional
- [ ] Changes apply in real-time
- [ ] Settings persist correctly
- [ ] User-friendly interface

---

# 🚦 Getting Started

## Immediate Next Steps
1. **Review this document** - Understand the MVP approach
2. **Read the phase documents** - Each MVP has detailed documentation in `docs/phases/`
3. **Choose your path** - Solo, team, or full-time?
4. **Set up environment** - Install .NET 8, VS Code, iRacing
5. **Start MVP 1** - Follow the detailed guide in `docs/phases/MVP1_Basic_Connection.md`
6. **Test frequently** - Validate each step with live iRacing

## Tips for Success
- **Start Small:** Don't skip MVPs 1-2
- **Test Often:** Use real iRacing sessions for validation
- **Keep it Simple:** Resist feature creep in early MVPs
- **Iterate Fast:** Release working software frequently
- **Get Feedback:** Share early versions with sim racing friends
- **Stay Focused:** Complete each MVP before moving to next
- **Use the Checklists:** Each phase document has detailed checklists

---

# 📚 Detailed Phase Documentation

Each MVP has its own detailed markdown file with:
- Complete implementation checklists
- Code samples and architecture details
- Testing scenarios
- Common issues and solutions
- Performance targets
- Time breakdowns

**📁 Location:** `docs/phases/`

### Phase Documents

1. **[MVP 1: Basic Connection](./docs/phases/MVP1_Basic_Connection.md)** - SDK integration and telemetry
2. **[MVP 2: Simple Overlay](./docs/phases/MVP2_Simple_Overlay.md)** - Transparent window with data display
3. **[MVP 3: First Widget](./docs/phases/MVP3_First_Widget.md)** - Production-quality Relative widget
4. **[MVP 4: Multi-Widget System](./docs/phases/MVP4_Multi_Widget.md)** - Multiple widgets and configuration
5. **[MVP 5: Basic Config UI](./docs/phases/MVP5_Basic_Config.md)** - Web-based configuration interface
6. **[MVP 6: Advanced Config](./docs/phases/MVP6_Advanced_Config.md)** - Drag-and-drop and themes (optional)

**📖 Index:** See [docs/phases/README.md](./docs/phases/README.md) for navigation guide

---

# 📚 Related Documentation

- **Detailed Phases:** See `docs/phases/` for individual MVP documentation
- **Full Development Plan:** See `iracing_dev_phases.md` for comprehensive technical details
- **Phase Index:** See `docs/phases/README.md` for navigation and progress tracking
- **API Reference:** SDK documentation at SVappsLAB GitHub

---

**Document Status:** ✅ Ready for Development  
**Recommended Starting Point:** [MVP 1: Basic Connection](./docs/phases/MVP1_Basic_Connection.md)  
**Next Review:** After MVP 2 completion  

---

*This simplified roadmap transforms the ambitious 14-18 week plan into manageable 3-5 day increments, allowing for rapid iteration and early feedback.*
