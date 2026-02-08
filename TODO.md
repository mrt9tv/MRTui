# MRT UI — Feature Roadmap

> Single source of truth for planned features, architecture, and release strategy.
> Inspired by RaceLab, Edge Overlays, iOverlay, and Kapps — differentiated by AI/ML intelligence.
>
> **Design Philosophy:** Minimalist by default, adaptive by choice, smart by nature.
> Overlays should be glanceable at 200+ km/h — density adjusts to the situation automatically.

---

## 🏗️ Architecture Principles

| Principle | Detail |
|-----------|--------|
| **Runtime** | .NET 8.0, WPF overlays (transparent, always-on-top, click-through) |
| **Management UI** | Native WPF — evolved from current MainWindow into full management hub |
| **Data** | SVappsLAB iRacing SDK (live ~60Hz) + iRacing /data REST API (historical) |
| **Performance** | Target: < 2% CPU, < 80MB RAM, zero frame drops for iRacing |
| **Widget System** | Modular WidgetBase, hot-pluggable, independent lifecycle |
| **AI/ML** | Local inference only (ONNX Runtime) — no cloud dependency during races |
| **VR** | Not a priority — flatscreen and borderless window focus |
| **Monetization** | Undecided — architecture supports freemium if needed later |

---

## 📐 Management Window (MRT Control Center)

The management window is the cockpit for all overlay configuration. Native WPF, MRT theme, sidebar navigation.

### Phase 1 — Foundation
- [ ] **Sidebar navigation** replacing current flat scroll layout
  - Tabs: Dashboard, Widgets, Layouts, Settings, About
- [ ] **Dashboard page** — connection status, session info (track, car, session type), active widget summary
- [ ] **Widget gallery page** — grid of all available widgets with enable/disable toggles, preview thumbnails
- [ ] **Per-widget settings panels** — each widget gets a dedicated config page (expanding current approach)
- [ ] **System tray minimization** — minimize to tray, restore on click, tooltip shows connection status

### Phase 2 — Layout Management
- [ ] **Named profiles** — save/load complete overlay configurations (positions, sizes, fields, toggles)
- [ ] **Session-specific layouts** — different configs for Practice, Qualifying, Race
- [ ] **Quick-switch dropdown** — instant profile switching from title bar
- [ ] **Import/Export** — JSON-based layout sharing

### Phase 3 — Smart Layouts
- [ ] **Auto-detection** — detect car class (GT, open-wheel, prototype) + track type (road/oval) from session info
- [ ] **Auto-switch profiles** — automatically apply matching layout when entering a session
- [ ] **Car-specific overrides** — per-car layout tweaks (e.g., different shift ring settings for GT3 vs Formula)

### Phase 4 — Visual Editor
- [ ] **Drag-and-drop layout preview** — WYSIWYG widget placement on a resolution-scaled canvas
- [ ] **Grid snapping and alignment guides** — snap to edges, center lines, other widgets
- [ ] **Resolution presets** — 1080p, 1440p, 4K, triple monitor configurations
- [ ] **Live preview mode** — see widget positions update in real-time on overlay windows

---

## 🎛️ Phase 1 — Core Widgets (MVP Release)

The four essential widgets every iRacing driver needs. Ship these before anything else.

### W1. MRT One — Circular Gauge `[WIP]`
> Status: ~90% complete. Needs polish and contextual field enhancement.

Compact circular design: Gear (center), Speed (top), RPM (bottom), Side boxes (left/right).

**Remaining Work:**
- [ ] **Contextual center field** — auto-swap based on situation:
  - Green flag racing → gap to car ahead
  - Caution / yellow → fuel remaining
  - Out-lap / in-lap → lap delta to best
  - Critical fuel → "PIT NOW" countdown with laps remaining
  - Pit lane → pit limiter + pit service status
- [ ] **RPM shift ring refinement** — per-car shift point calibration from `PlayerCarSLFirstRPM` / `PlayerCarSLShiftRPM` / `PlayerCarSLBlinkRPM`
- [ ] **Enhanced proximity radar** — 3D position-aware dots using `CarIdxLapDistPct` + `CarLeftRight`
- [ ] **Metric/imperial toggle** — per-field unit selection persisted in settings
- [ ] **Opacity slider** — 0–100% widget transparency

**iRacing SDK channels used:** `Speed`, `RPM`, `Gear`, `Throttle`, `Brake`, `FuelLevel`, `FuelLevelPct`, `CarLeftRight`, `SessionFlags`, `PitSvFlags`, `ShiftIndicatorPct`, `PlayerCarSLFirstRPM`, `PlayerCarSLShiftRPM`, `PlayerCarSLLastRPM`, `PlayerCarSLBlinkRPM`

---

### W2. Relative Widget `[PLANNED]`
> Priority: **HIGHEST** — most requested overlay across all competitors.
> References: RaceLab Relative, Edge Relative Timing, iOverlay Relative

Compact table showing cars around you (configurable ±3 to ±10) with live intervals.

**Core Columns:**
- [ ] Position (overall + class position)
- [ ] Driver name (truncated, with optional iRating badge)
- [ ] Car number + class color stripe
- [ ] Interval (time gap, ±seconds, updates per tick)
- [ ] Last lap time
- [ ] Pit indicator (in pit lane / in pit stall / pit count)
- [ ] Off-track indicator (using `CarIdxTrackSurface`)

**Enhanced Columns (toggleable):**
- [ ] iRating (from session YAML)
- [ ] Safety Rating + license class color
- [ ] Delta to driver's best lap
- [ ] Tire compound indicator (using `CarIdxTireCompound`)
- [ ] Laps completed / laps down
- [ ] Fast repair status (using `CarIdxFastRepairsUsed`)
- [ ] Connection quality indicator (using `ChanPartnerQuality`)

**Layout Options:**
- [ ] Compact mode (position + name + interval only)
- [ ] Standard mode (+ last lap + pit + off-track)
- [ ] Detailed mode (all columns)
- [ ] User picks which columns to show + column order

**Multi-class Support:**
- [ ] Class color sidebar (left-edge stripe per iRacing class color)
- [ ] Class position vs overall position toggle
- [ ] Class filter — show only your class, all classes, or specific classes
- [ ] Class separator rows in standings

**Technical Implementation:**
- [ ] Build `RelativeCalculator` service in Core — compute sorted interval list from `CarIdxLapDistPct` + `CarIdxEstTime` + `CarIdxLap`
- [ ] Highlight your car row (distinct background)
- [ ] Row animation on position changes (fade/slide)
- [ ] Header row with session clock / SOF / remaining laps/time

**iRacing SDK channels:** `CarIdxLapDistPct`, `CarIdxEstTime`, `CarIdxLap`, `CarIdxLapCompleted`, `CarIdxLastLapTime`, `CarIdxBestLapTime`, `CarIdxOnPitRoad`, `CarIdxTrackSurface`, `CarIdxClass`, `CarIdxClassPosition`, `CarIdxPosition`, `CarIdxTireCompound`, `CarIdxFastRepairsUsed`, `CarIdxGear`, `CarIdxRPM`

---

### W3. Standings Widget `[PLANNED]`
> References: RaceLab Standings (flagship), Edge Leaderboard, iOverlay Standings
> Key difference from Relative: Shows full field; Relative shows cars around you.

Full-field leaderboard with class-aware multi-class rendering.

**Core Display:**
- [ ] Position (overall / class toggle)
- [ ] Driver name + car number
- [ ] Class color coding (left border stripe)
- [ ] Gap to leader / interval to car ahead (toggle)
- [ ] Last lap time + personal best lap time
- [ ] Laps completed

**Pro Columns (toggleable):**
- [ ] iRating + predicted iRating change (+/- after race)
- [ ] Safety Rating + license class (R, D, C, B, A, Pro)
- [ ] Positions gained/lost from grid
- [ ] Pit stop count + in-pit indicator
- [ ] Fastest lap indicator (purple time)
- [ ] Off-track / incident flash
- [ ] Car brand/model name
- [ ] Tire compound

**Multi-class Rendering:**
- [ ] Grouped by class with class headers (class name + color)
- [ ] Overall/class position dual display
- [ ] Show/hide specific classes
- [ ] Class leader highlight

**Session Awareness:**
- [ ] Practice: show best laps, laps completed
- [ ] Qualifying: show qualifying position, best lap, sector times
- [ ] Race: show gaps, intervals, pit stops, positions gained

**iRacing SDK channels:** `CarIdxPosition`, `CarIdxClassPosition`, `CarIdxLap`, `CarIdxLapCompleted`, `CarIdxLastLapTime`, `CarIdxBestLapTime`, `CarIdxBestLapNum`, `CarIdxOnPitRoad`, `CarIdxClass`, `CarIdxTrackSurface`, `CarIdxTireCompound`, `CarIdxF2Time`, `CarIdxEstTime`
**iRacing YAML:** `DriverInfo` (iRating, license, car, team), `SessionInfo` (session type, laps/time), `SplitTimeInfo`

---

### W4. Fuel Calculator Widget `[WIP]`
> Status: ~70% complete. Core calculations working. Needs team fuel sharing + pit stop planning UI.

**Existing (done):**
- [x] Fuel remaining (L) + tank percentage
- [x] L/Lap averages (Last, L3, L5, L10, Session)
- [x] Laps remaining with splutter buffer
- [x] Projected delta at finish
- [x] Fuel saving target (L/lap reduction needed)
- [x] Pit vs save strategy comparison
- [x] Green/yellow flag consumption tracking
- [x] Strategic alert system

**Remaining Work:**
- [ ] **Pit stop planner** — calculate optimal fuel add amount, number of stops remaining, pit window
- [ ] **Fuel burn rate mini-graph** — sparkline showing L/lap over last N laps (identify lift-and-coast effectiveness)
- [ ] **Multiple strategies** — compare "splash & dash" vs "full fill" vs "fuel save to end"
- [ ] **Stint summary** — per-stint consumption, lap count, average pace
- [ ] **Required fuel to add** display — "Add X.X L at next stop" with +1 lap safety option

**Phase 2 additions:**
- [ ] **Team fuel sharing** — broadcast fuel data to teammates via local network (UDP/TCP)
- [ ] **Endurance stint plan** — driver swap scheduling, fuel per driver, fair-share time calculations

**iRacing SDK channels:** `FuelLevel`, `FuelLevelPct`, `FuelUsePerHour`, `SessionFlags`, `SessionLapsRemain`, `SessionLapsRemainEx`, `SessionTimeRemain`, `PitSvFuel`, `PitstopActive`, `PlayerCarInPitStall`

---

## 🎛️ Phase 2 — Essential Enhancements

Widgets and systems that round out the overlay suite to feature-parity with competitors.

### W5. Track Map Widget
> References: RaceLab Track Map, Edge Track Map, iOverlay Track Map

Minimap of the current circuit with live car positions.

- [ ] Circuit outline rendered from `CarIdxLapDistPct` path data (track centerline approximation)
- [ ] Colored dots per car (class color), your car highlighted
- [ ] Pit lane entry/exit markers
- [ ] Turn number labels (from TrackTurnDatabase)
- [ ] Proximity highlighting — cars near you glow or pulse
- [ ] North-up vs track-up orientation toggle
- [ ] Scalable widget size

**iRacing SDK channels:** `CarIdxLapDistPct`, `CarIdxClass`, `CarIdxOnPitRoad`, `CarIdxTrackSurface`, `Lat`, `Lon`, `Alt`

### W6. Delta Bar Widget
> References: RaceLab Delta, SimHub delta bars

Horizontal bar showing live lap delta with color gradient.

- [ ] Delta to personal best lap (`LapDeltaToBestLap`)
- [ ] Delta to optimal lap (`LapDeltaToOptimalLap`)
- [ ] Delta to session best (`LapDeltaToSessionBestLap`)
- [ ] Color gradient: green (gaining) → white (neutral) → red (losing)
- [ ] Numeric delta display (±X.XXX)
- [ ] Mini-bar + full-bar layout options
- [ ] Optional predictive lap time text ("Predicted: 1:48.xxx")

**iRacing SDK channels:** `LapDeltaToBestLap`, `LapDeltaToBestLap_DD`, `LapDeltaToBestLap_OK`, `LapDeltaToOptimalLap`, `LapDeltaToSessionBestLap`, `LapDeltaToSessionOptimalLap`

### W7. Input Trace Widget
> References: RaceLab Input Telemetry, Edge Input Graph, MoTeC traces

Real-time scrolling trace graph for driving inputs.

- [ ] Throttle trace (green line, 0-100%)
- [ ] Brake trace (red line, 0-100%)
- [ ] Steering trace (blue/white line, ±degrees)
- [ ] Scrolling mode (time-based) or lap-based (reset per lap)
- [ ] Overlay comparison: current lap vs best lap ghost trace
- [ ] ABS active indicator on brake trace (`BrakeABSactive`)
- [ ] TC active indicator on throttle trace
- [ ] Configurable time window (5s, 10s, 30s, full lap)

**iRacing SDK channels:** `Throttle`, `ThrottleRaw`, `Brake`, `BrakeRaw`, `BrakeABSactive`, `Clutch`, `SteeringWheelAngle`, `SteeringWheelAngleMax`

### W8. Flag Display Widget
> References: Edge DigiFlags, RaceLab Flags

Visual flag indicators with sector-specific awareness.

- [ ] Green, yellow (full course + local), blue, black, white, checkered flags
- [ ] Sector-specific yellow flag display (which sectors have incidents)
- [ ] Flash/pulse animation for urgent flags (black, meatball)
- [ ] Compact mode (color bar) vs full mode (flag icon + text)
- [ ] Audio alert option for safety car / local yellow

**iRacing SDK channels:** `SessionFlags`, `CarIdxPaceFlags`, `CarIdxPaceLine`, `CarIdxPaceRow`, `PaceMode`

### W9. Incident Tracker Widget
> References: Edge Incident Tracker, RaceLab (within standings)

- [ ] Current incident count vs session limit
- [ ] Team incident count (in team sessions, `PlayerCarTeamIncidentCount`)
- [ ] Driver incident count (`PlayerCarDriverIncidentCount`, `PlayerCarMyIncidentCount`)
- [ ] Visual alert at configurable thresholds (e.g., 12x of 17x limit)
- [ ] Fast repair available / used count
- [ ] Incident flash on new incident (brief widget highlight)

**iRacing SDK channels:** `PlayerCarDriverIncidentCount`, `PlayerCarMyIncidentCount`, `PlayerCarTeamIncidentCount`, `FastRepairAvailable`, `FastRepairUsed`, `PlayerFastRepairsUsed`

### W10. Weather Monitor Widget
> References: Edge Live Weather, RaceLab Weather Monitor

- [ ] Air temperature + track temperature
- [ ] Wind speed + direction (compass arrow)
- [ ] Sky conditions (clear, partly cloudy, overcast, etc. from `Skies`)
- [ ] Precipitation level + track wetness (`Precipitation`, `TrackWetness`, `WeatherDeclaredWet`)
- [ ] Fog level
- [ ] Humidity
- [ ] Time of day + solar position (for endurance day/night transitions)
- [ ] Temperature trend (warming/cooling over session)

**iRacing SDK channels:** `AirTemp`, `TrackTemp`, `TrackTempCrew`, `WindDir`, `WindVel`, `Skies`, `RelativeHumidity`, `Precipitation`, `FogLevel`, `TrackWetness`, `WeatherDeclaredWet`, `SolarAltitude`, `SolarAzimuth`, `SessionTimeOfDay`

### W11. Sector Times Widget

- [ ] Live sector splits: current lap vs personal best, session best, overall best
- [ ] Color-coded per sector: purple (overall best), green (PB), yellow (slower), red (much slower)
- [ ] Sector time table (optional: last N laps of sector times)
- [ ] Predicted lap time based on current sectors

**iRacing SDK sources:** `SplitTimeInfo` from session YAML, `LapDeltaToSessionBestLap`, lap time analysis

---

## 🧠 Phase 3 — AI/ML Intelligence Layer

> **THE DIFFERENTIATOR.** No competitor offers AI-driven race intelligence.
> All inference runs locally via ONNX Runtime — zero cloud latency, works offline.

### System: AI Race Engineer `[PLANNED]`

A conversational/alert-based AI system that acts as your in-car race engineer.

**3.1 Real-Time Strategic Alerts**
- [ ] "Lift & coast next 3 corners — saving 0.15 L/lap to make it on fuel"
- [ ] "Car ahead on old tires — closing 0.3s/lap — attempt pass T4 chicane"
- [ ] "Safety car probability high — consider staying out for track position"
- [ ] "Pit window opens in 3 laps — fuel OK to lap 28, add 42L"
- [ ] Context-aware alert priority (suppress low-priority alerts during overtaking)
- [ ] Text overlay widget + optional TTS audio output

**3.2 Predictive Lap Time**
- [ ] ML model trained on your inputs (throttle, brake, steering) vs lap time
- [ ] Per-sector prediction: "Sector 2 projected +0.4s vs best"
- [ ] Real-time "ghost" pace (what lap time are you on track for?)
- [ ] Factor in tire degradation, fuel weight, track temperature

**3.3 Corner-by-Corner Analysis (Live)**
- [ ] Segment each lap by turn (using TrackTurnDatabase)
- [ ] Compare current turn execution vs personal best: braking point, apex speed, exit speed
- [ ] Highlight the 3 turns where you lose the most time
- [ ] Mini turn-performance dashboard: "T3: -0.2s (late apex), T7: -0.15s (early brake)"

### System: Multiclass Traffic Predictor `[PLANNED]`

AI-powered traffic awareness for multi-class racing.

- [ ] Detect faster-class cars approaching from behind using `CarIdxEstTime` + `CarIdxClass` + `CarIdxLapDistPct`
- [ ] Calculate closing rate (seconds per lap)
- [ ] Predict which side they'll pass based on track position + historical passing data
- [ ] Estimated time-to-reach ("LMP2 behind in ~12 seconds")
- [ ] Visual alert: class color + closing rate + approach direction
- [ ] Audio chime option for imminent pass

### System: Smart Pit Strategy (ML-Powered) `[PLANNED]`

ML-based pit window optimizer replacing simple fuel calculations.

- [ ] Factor inputs: fuel remaining, consumption rate, tire degradation curve, track position, gaps, weather forecast
- [ ] Undercut/overcut gap analysis — "Pit now: undercut P4 by 1.2s" vs "Stay out: overcut P3"
- [ ] Safety car probability model (based on incident rate, session type, lap count)
- [ ] Optimal pit lap suggestion with confidence level
- [ ] Multi-stop strategy comparison
- [ ] Live strategy update as race conditions change

### System: Driving Style Analysis

- [ ] Classify driving patterns from telemetry: braking aggression, throttle smoothness, cornering consistency
- [ ] Identify improving/degrading stint performance trends
- [ ] Tire management scoring (are you overdriving the rears?)
- [ ] Fuel efficiency scoring per lap
- [ ] Post-session summary with improvement suggestions

### AI/ML Technical Stack

- [ ] Model format: **ONNX** (cross-platform, .NET native via ONNX Runtime)
- [ ] Training pipeline: Python (scikit-learn/PyTorch → ONNX export)
- [ ] Data collection: ibt telemetry file parsing + live session recording
- [ ] Model storage: `%AppData%/MRT-UI/models/` with versioned model files
- [ ] Feature extraction service: converts raw telemetry to model input tensors
- [ ] Inference service: async model evaluation, debounced to avoid CPU spikes

---

## 🏁 Phase 4 — Endurance & Team Features

### Team Fuel Sharing
- [ ] Local network fuel data broadcast (UDP multicast or TCP)
- [ ] Driver fuel status dashboard (all team drivers' fuel state)
- [ ] Shared pit strategy with coordinator role
- [ ] Invite-only team sessions (session code)

### Multi-Driver / Team Mode
- [ ] Driver swap tracking (who's driving, how long, when next swap)
- [ ] Stint assignments with time scheduling
- [ ] Fair-share driving time calculations with alerts
- [ ] Team incident total + per-driver breakdown
- [ ] Dedicated team management window (separate from overlay config)

### Stint History System
- [ ] Per-stint data: fuel consumption, average lap time, tire wear snapshot (captured at pit entry via `CarIdxTireCompound`)
- [ ] Visual stint graph (timeline bar showing stints, pit stops, driver swaps)
- [ ] Degradation curve identification
- [ ] Compare stints across drivers (team mode)

> **Note:** Tire temperature and wear data from iRacing SDK is only available when the car is in pit lane. Stint history must capture pit-entry snapshots and interpolate between them.

---

## ⚡ Phase 5 — Pro Features & Polish

### Visual Layout Editor
- [ ] WYSIWYG drag-and-drop widget placement on resolution-scaled canvas
- [ ] Grid snapping + alignment guides
- [ ] Resolution presets (1080p, 1440p, 4K, triple monitor)
- [ ] Live preview: widget positions update on overlay windows in real-time

### Data Blocks (Custom Widgets)
> Reference: RaceLab Data Blocks (80+ blocks), Edge Data Frames

- [ ] Single-value display widgets: pick any telemetry field, display with configurable formatting
- [ ] Header/footer customizable text areas
- [ ] User creates their own display from the full telemetry field library
- [ ] Template library: pre-built data block sets for common use cases

### Pit Box Helper
> Reference: RaceLab Pitbox Helper

- [ ] Visual guide for pit stall positioning
- [ ] Pit entry speed limit indicator
- [ ] Pit service status display (fuel amount, tire selection, repairs)
- [ ] Pit exit timing (gap to traffic on pit exit)

### Head-to-Head Widget
> Reference: RaceLab Head to Head

- [ ] Compare yourself to any single driver: live gap, sector deltas, lap times, fuel
- [ ] Selection: click driver in Relative/Standings to set H2H target
- [ ] Battle tracker: how the gap has evolved over last N laps

### Overtake / Traffic Alert Widget
- [ ] Proximity-based warnings for approaching faster cars
- [ ] Closing rate calculation + estimated time-to-reach
- [ ] Which side they're likely to pass (based on track position + upcoming corners)
- [ ] Class color + car number overlay
- [ ] Audio chime option

### Laptime Graph Widget
- [ ] Rolling graph of lap times over the session
- [ ] Highlight best lap, stints, outlier laps
- [ ] Stint boundaries marked with pit stop indicators
- [ ] Lap time spread analysis (consistency metric)

### Blind Spot Indicator
> Reference: RaceLab Blind Spot Indicator

- [ ] Visual indicators on screen edges (left/right) when car is alongside
- [ ] Three states: overlap, side-by-side, car behind closing
- [ ] Uses `CarLeftRight` SDK variable + own proximity calculations
- [ ] Configurable visual style (arrows, bars, color pulsing)

---

## 🔧 Core Systems & Infrastructure

### Telemetry Service Enhancements
- [ ] **iRacing /data REST API integration** — historical results, iRating trends, series info
- [ ] **Session info parser** — extract full driver list, car models, team info, split info from YAML
- [ ] **Telemetry recording** — save session data for post-race analysis + AI training
- [ ] **ibt file parser** — import iRacing telemetry files for replay/analysis

### Performance & Reliability
- [ ] **Widget render budget** — each widget gets a CPU time limit, throttles if exceeded
- [ ] **Priority-based updates** — critical widgets (Relative, Fuel) update at full rate, cosmetic widgets at reduced rate
- [ ] **Memory pooling** — pre-allocate data structures to avoid GC pressure during races
- [ ] **Crash resilience** — auto-save layout on widget crash, restart individual widgets without full app restart
- [ ] **Telemetry diagnostics page** — show update rate, latency, dropped frames, CPU usage

### Hotkey System
- [ ] Global hotkey manager (customizable bindings)
- [ ] Default keys: `Ctrl+L` lock/unlock, `Ctrl+H` show/hide active widget
- [ ] Per-widget show/hide hotkeys
- [ ] Hotkey to cycle data fields on MRT One
- [ ] Hotkey to cycle between profiles/layouts

### Click-Through & Interaction
- [ ] Click-through mode: overlays pass all mouse events to iRacing (essential for borderless window)
- [ ] Lock mode: prevent accidental widget drag during racing
- [ ] Unlock mode: allow repositioning and resizing
- [ ] Always-on-top management per widget

### Update & Distribution
- [ ] Auto-update system (check for new versions, download, apply)
- [ ] Release channels: stable, beta (opt-in)
- [ ] First-run setup wizard (connection check, resolution detection, default layout)
- [ ] Crash reporting (opt-in, anonymized)

---

## 📊 Competitive Feature Matrix

| Feature | RaceLab | Edge | iOverlay | Kapps | MRT UI |
|---------|---------|------|----------|-------|--------|
| Relative | ✅ | ✅ | ✅ | ✅ | Phase 1 |
| Standings | ✅ | ✅ | ✅ | ✅ | Phase 1 |
| Fuel Calculator | ✅ | ✅ | ✅ | ✅ | ✅ WIP |
| Track Map | ✅ | ✅ | ✅ | ❌ | Phase 2 |
| Input Trace | ✅ | ✅ | ✅ | ❌ | Phase 2 |
| Delta Bar | ✅ | ❌ | ❌ | ❌ | Phase 2 |
| Weather | ❌ | ✅ | ❌ | ❌ | Phase 2 |
| Flag Display | ✅ | ✅ | ❌ | ❌ | Phase 2 |
| Incidents | ❌ | ✅ | ❌ | ❌ | Phase 2 |
| Circular Gauge | ❌ | ✅ | ❌ | ❌ | ✅ WIP |
| Turn Tracking | ❌ | ❌ | ❌ | ❌ | ✅ Done |
| Smart Layouts | ✅ | ✅ | Partial | ❌ | Phase 2-3 |
| Team Fuel Sharing | ✅ Pro | ✅ | ✅ Pro | ❌ | Phase 4 |
| Visual Editor | ✅ | ✅ | ❌ | ❌ | Phase 5 |
| VR Native | ✅ | ✅ | ❌ | ❌ | Not planned |
| Multi-sim | ✅ (8) | ❌ | ❌ | ❌ | iRacing only |
| **AI Race Engineer** | ❌ | ❌ | ❌ | ❌ | **Phase 3** 🧠 |
| **Traffic Predictor** | ❌ | ❌ | ❌ | ❌ | **Phase 3** 🧠 |
| **ML Pit Strategy** | ❌ | ❌ | ❌ | ❌ | **Phase 3** 🧠 |
| **Driving Analysis** | ❌ | ❌ | ❌ | ❌ | **Phase 3** 🧠 |

---

## 🗓️ Release Milestones

| Milestone | Target | Deliverables |
|-----------|--------|-------------|
| **v0.8** | Phase 1A | MRT One complete, Management Window sidebar, Profile system (basic) |
| **v0.9** | Phase 1B | Relative Widget, Standings Widget |
| **v1.0** | Phase 1 Release | Fuel Calculator complete, all Phase 1 widgets polished, public release |
| **v1.1** | Phase 2A | Track Map, Delta Bar, Flag Display |
| **v1.2** | Phase 2B | Input Trace, Weather, Incidents, Sector Times |
| **v1.3** | Phase 2C | Smart layouts (auto-switch), session-specific configs |
| **v2.0** | Phase 3 | AI Race Engineer (alerts), Traffic Predictor, ML Pit Strategy |
| **v2.5** | Phase 4 | Team mode, Fuel sharing, Stint history |
| **v3.0** | Phase 5 | Visual editor, Data blocks, Head-to-head, full pro suite |

---

## ✅ Completed

### Turn Display Widget (v0.7.0)
Compact vertical bar showing NEXT turn (teal) and LAST turn (orange). Turn number + name display with optional border animation. 5 tracks supported: Imola, Monza, Long Beach, Bathurst, Spa.

### Fuel Saving Service (v0.7.1)
Lift & coast calculation engine: saving target, current saving rate, pit-vs-save strategy, strategic alerts, projected fuel delta. Integrated into Fuel Calculator Widget.

### Fuel Calculator Widget (v0.7.1)
Dedicated overlay: fuel remaining, L/lap, laps left (splutter-aware), L3/L5 averages, projected delta, saving target, strategy recommendation.

### Core Telemetry Service
60Hz live telemetry via SVappsLAB SDK. Connection management, session parsing, per-car data indexing.

### Fuel Calculator Service
Multi-method averaging (Last, L3, L5, L10, Session, Min/Max). Green/yellow flag tracking. Doable laps with splutter buffer. Fuel-needed-to-finish with pit strategy.

### Proximity System
Multi-zone car detection (ahead/behind/left/right). Lateral spotter integration. CarLeftRight SDK validation.

### Widget Architecture
WidgetBase class, WidgetManager with factories, layout save/load (JSON), per-widget lifecycle, dirty field tracking.

---

## 📝 Legend

| Symbol | Meaning |
|--------|---------|
| `[PLANNED]` | Designed, not yet started |
| `[WIP]` | Work in progress |
| `✅` | Completed and merged |
| 🧠 | AI/ML differentiator — no competitor has this |
