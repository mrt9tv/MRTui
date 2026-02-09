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

> **Phase 1 Differentiators (things no competitor does):**
> - MRT One: contextual auto-swap center field with user-defined rules
> - Relative: per-row closing rate arrows + danger highlighting
> - Relative: smart row count that adapts to your actual position
> - Standings: live projected iRating delta during race
> - All widgets: unified MRT visual theme, smooth row animations, column presets with one-key cycling

### W1. MRT One — Circular Gauge `[WIP]`
> Status: ~90% complete. Needs polish and contextual field enhancement.

Compact circular design: Gear (center), Speed (top), RPM (bottom), Side boxes (left/right).

**Remaining Work:**

#### Contextual Center Field (Auto-Swap) — Enable/Disable Feature
> Must be an opt-in toggle. When disabled, center shows whatever the user selected.
> When enabled, a priority-based rule engine decides what to display.

- [ ] **Master toggle** — On/Off in MRT One settings, defaults to OFF
- [ ] **Priority cascade** (highest wins):
  1. `PIT NOW` — fuel critical (< configurable threshold, default 1.5 laps)
  2. Pit service — when `PlayerCarInPitStall == true`, show pit service status + fuel being added
  3. Yellow/Caution — when `SessionFlags` has yellow, show fuel remaining
  4. Close car alert — when `CarLeftRight != Clear`, show lateral indicator (LEFT / RIGHT / BOTH)
  5. User-selected default (gap to car ahead, lap delta, fuel %, etc.)
- [ ] **Per-rule enable/disable** — checkboxes for each trigger independently
- [ ] **Hold time** — configurable seconds before reverting to default (e.g., 3s after yellow clears)
- [ ] **Default field selector** — dropdown to pick what shows when no triggers are active

#### RPM Shift Ring — Visual Refinement
> The SDK values `PlayerCarSLFirstRPM`, `PlayerCarSLShiftRPM`, `PlayerCarSLLastRPM`, `PlayerCarSLBlinkRPM` are **already per-car** — iRacing sets them automatically when you enter any car. The current code already reads them. The work here is visual, not data.

- [ ] **Smooth gradient sweep** — interpolate ring color from green (FirstRPM) → yellow (ShiftRPM) → red (LastRPM) → blink (BlinkRPM)
- [ ] **Ring fill percentage** — arc fills proportionally based on RPM position between First and Blink thresholds
- [ ] **Blink animation** — ring flashes at BlinkRPM frequency (over-rev warning)
- [ ] **Shift flash** — brief full-ring white flash at exact ShiftRPM to signal optimal upshift
- [ ] **Note:** `ShiftIndicatorPct` is **DEPRECATED** — use the 4 RPM threshold vars directly. `ShiftPowerPct` (0.0–1.0) is available as alternative single-float driver.

#### Proximity Indicator
> **SDK Reality:** `Lat`, `Lon`, `Alt` are **Disk Only** (ibt files) — NOT available via live telemetry API. `CarIdxLapDistPct` gives 1D centerline position only — no cross-track offset. True 3D/2D positional radar is **not possible** with live SDK data.

- [ ] **1D proximity strip** — linear ahead/behind indicator using `CarIdxLapDistPct` + `ProximityCalculator` (existing)
- [ ] **CarLeftRight overlay** — left/right/both-sides indicator combining `CarLeftRight` enum (0=Off, 1=Clear, 2=Left, 3=Right, 4=Both, 5=TwoLeft, 6=TwoRight) with proximity distance
- [ ] **Side-specific coloring** — when `CarLeftRight` reports a car alongside AND `ProximityCalculator` shows < threshold, color the relevant side indicator on the gauge
- [ ] **Note on `CarLeftRight`:** It DOES specify which side (left/right/both). It also reports `TwoCarsLeft`/`TwoCarsRight`. On reversed ovals, left/right may be swapped — use `TrackDirection` from session YAML to handle this.

#### General Polish
- [ ] **Metric/imperial toggle** — per-field unit selection persisted in settings
- [ ] **Opacity slider** — 0–100% widget transparency
- [ ] **Widget lock** — prevent accidental drag during racing

**iRacing SDK channels used:** `Speed`, `RPM`, `Gear`, `Throttle`, `Brake`, `FuelLevel`, `FuelLevelPct`, `CarLeftRight`, `SessionFlags`, `PitSvFlags`, `PlayerCarSLFirstRPM`, `PlayerCarSLShiftRPM`, `PlayerCarSLLastRPM`, `PlayerCarSLBlinkRPM`, `ShiftPowerPct`, `PlayerCarInPitStall`, `CarIdxLapDistPct`

---

### W2. Relative Widget `[PLANNED]`
> Priority: **HIGHEST** — most requested overlay across all competitors.
> References: RaceLab Relative, Edge Relative Timing, iOverlay Relative
> **Design goal:** Clean, information-dense, zero visual clutter. Must look like it belongs with MRT One.

Compact table showing cars around you (configurable ±3 to ±10) with live intervals.

**Core Columns:**
- [ ] Position (overall + class position)
- [ ] Driver name (truncated, with optional iRating badge)
- [ ] Car number + class color stripe (left-edge, 3px)
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

**🧠 MRT Differentiators (no competitor does these):**
- [ ] **Closing rate arrow** — tiny ▲/▼ icon per row showing if gap is shrinking or growing, computed from interval delta over last N ticks
- [ ] **Danger row highlighting** — when a car's closing rate exceeds threshold (e.g., >0.5s/lap faster), row gets a subtle warning glow. You see a threat before it arrives.
- [ ] **Smart row count** — if you're P3, don't show 5 empty rows ahead. Dynamically reallocate to show more cars behind. Configurable: auto vs fixed.
- [ ] **Class-aware dual gap** — in multi-class, show both "gap to class car" and "gap to overall" in a compact dual-column. Not one-or-the-other.

**Column Presets (one-key cycling):**
- [ ] **Minimal** — position + name + interval (3 columns)
- [ ] **Standard** — + last lap + pit + off-track (6 columns)
- [ ] **Full** — + iRating + tire + laps (all columns)
- [ ] Hotkey to cycle through presets without opening settings
- [ ] Custom: user picks which columns to show + column order via drag-drop in management window

**Multi-class Support:**
- [ ] Class color sidebar (left-edge stripe per iRacing class color)
- [ ] Class position vs overall position toggle
- [ ] Class filter — show only your class, all classes, or specific classes

**Visual Design:**
- [ ] MRT theme — dark background, teal/orange accents matching MRT One
- [ ] Smooth row slide animation on position changes (not snap)
- [ ] Your car row: distinct background color, always centered
- [ ] Font size scaling — single slider scales all text proportionally
- [ ] Smart column reflow — widget auto-narrows when showing fewer columns (no wasted space)
- [ ] Row right-click → "Set as H2H target" (hook for future Head-to-Head widget)

**Technical Implementation:**
- [ ] Build `RelativeCalculator` service in Core — compute sorted interval list from `CarIdxLapDistPct` + `CarIdxEstTime` + `CarIdxLap`
- [ ] `ClosingRateTracker` — rolling window of interval deltas per car, smoothed over N ticks
- [ ] Header row with session clock / SOF / remaining laps/time

**iRacing SDK channels:** `CarIdxLapDistPct`, `CarIdxEstTime`, `CarIdxLap`, `CarIdxLapCompleted`, `CarIdxLastLapTime`, `CarIdxBestLapTime`, `CarIdxOnPitRoad`, `CarIdxTrackSurface`, `CarIdxClass`, `CarIdxClassPosition`, `CarIdxPosition`, `CarIdxTireCompound`, `CarIdxFastRepairsUsed`

#### 🧠 Rival Tracker — Persistent Cross-Session Driver Intel
> No competitor offers this. A persistent local database of drivers you've raced against.

- [ ] **Auto-log encounters** — every session, record driver name, iRating, finish positions, incidents, average gap
- [ ] **Rival tagging** — right-click any row → "Tag as Rival / Friendly / Aggressive" with color coding
- [ ] **Encounter history** — tooltip/popup showing "Raced 12 times, you beat them 7x, avg gap: +1.3s"
- [ ] **Threat scoring** — ML-based: combine iRating, incident rate, closing tendencies into a 1-5 star threat score per driver
- [ ] **Race prep** — before green flag, scan entry list and highlight known rivals + show their stats
- [ ] **Storage** — Local SQLite database, no cloud. Indexed by iRacing customer ID.
- [ ] **Export** — CSV/JSON dump of your rival database for analysis

---

### W3. Standings Widget `[PLANNED]`
> References: RaceLab Standings (flagship), Edge Leaderboard, iOverlay Standings
> Key difference from Relative: Shows full field; Relative shows cars around you.
> **Design goal:** Full-field but never overwhelming. Clean class separation, live data storytelling.

Full-field leaderboard with class-aware multi-class rendering.

**Core Display:**
- [ ] Position (overall / class toggle)
- [ ] Driver name + car number
- [ ] Class color coding (left border stripe, 3px)
- [ ] Gap to leader / interval to car ahead (toggle)
- [ ] Last lap time + personal best lap time
- [ ] Laps completed

**Pro Columns (toggleable):**
- [ ] iRating (from YAML)
- [ ] Safety Rating + license class color (R, D, C, B, A, Pro)
- [ ] Pit stop count + in-pit indicator
- [ ] Fastest lap indicator (purple time)
- [ ] Off-track / incident flash
- [ ] Car brand/model name (from YAML `CarScreenName`)
- [ ] Tire compound

**🧠 MRT Differentiators:**
- [ ] **Positions gained/lost column** — green ▲ / red ▼ arrow + number showing spots gained/lost since race start. Instant visual race narrative.
- [ ] **Live iRating delta** — projected iRating change (+/-) calculated live from current position + SOF. Nobody else shows this during a race.
- [ ] **Condensed class headers** — class name + color in a thin separator bar (not a full-height row). Maximum car density.
- [ ] **Dim disconnected/spectating** — de-emphasize (gray out) rather than remove. Keeps the field count honest.

**Multi-class Rendering:**
- [ ] Grouped by class with condensed class headers
- [ ] Overall/class position dual display
- [ ] Show/hide specific classes
- [ ] Class leader row highlight

**Session Awareness:**
- [ ] Practice: show best laps, laps completed, gap to fastest
- [ ] Qualifying: show qualifying position, best lap
- [ ] Race: show gaps, intervals, pit stops, positions gained, iRating delta

**Visual Design (shared with Relative):**
- [ ] MRT theme — consistent dark background, teal/orange accents
- [ ] Smooth row slide animation on position changes
- [ ] Font size scaling — single slider
- [ ] Column presets (Minimal / Standard / Full) with hotkey cycling
- [ ] Row right-click → "Set as H2H target"

**iRacing SDK channels:** `CarIdxPosition`, `CarIdxClassPosition`, `CarIdxLap`, `CarIdxLapCompleted`, `CarIdxLastLapTime`, `CarIdxBestLapTime`, `CarIdxBestLapNum`, `CarIdxOnPitRoad`, `CarIdxClass`, `CarIdxTrackSurface`, `CarIdxTireCompound`, `CarIdxF2Time`, `CarIdxEstTime`
**iRacing YAML:** `DriverInfo` (iRating, license, car, team, `CarScreenName`), `SessionInfo` (session type, laps/time), `SplitTimeInfo`

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

## 💡 Future Ideas — Noted for Consideration

> Ideas discussed during development that deserve future exploration.

### Analysis & Strategy
- [ ] **Predictive Pit Window** — Calculate optimal pit lap range based on current fuel burn, remaining fuel, and race distance. Highlight when in the window vs. too early/late.
- [ ] **Delta-to-Best Sector Breakdown** — Per-sector time deltas using `LapDeltaToSessionBestLap` and sector split data. Red/green badges per corner.
- [ ] **Weather/Track Temp Overlay** — Small ambient conditions bar showing `TrackTempCrew`, `AirTemp`, `WindSpeed`, `WindDir`, `Skies` for tire strategy decisions.

### Driving Intelligence
- [ ] **Braking Heatmap** — Using `Brake` (0-1) + `LapDistPct`, build per-corner braking point comparison vs. your fastest lap. Show where you're braking early/late.
- [ ] **Draft Detection** — Using `CarIdxLapDistPct` proximity + `Speed` comparison, highlight when in a draft (within ~0.5s of car ahead on a straight).
- [ ] **Time Lost in Corners** — Compare speed traces through turn zones against best lap. Show per-corner time delta as red/green badges.

### UI/UX Enhancements
- [ ] **Drag-to-Reorder Columns/Rows** — Let users drag column headers or row order in any widget, persisted in settings.
- [ ] **Quick-Glance Mode** — Relative widget compact mode: only 2 cars ahead + 2 behind, larger text for peripheral vision at speed.

### 🧠 Unique MRT-Only Features (No Competitor Has These)

- [ ] **Ghost Gap Projection** — Extrapolate closing/opening rates to show *when* a car will catch/pass you. Display a countdown timer: "P3 catches you in ~4 laps" or "You catch P2 in ~7 laps". Uses rolling interval deltas + pace trend, visible as a subtle timer next to the REL column.

- [ ] **Incident Heatmap Zones** — Track which corners produce the most incidents across all cars in the session. Overlay a small corner danger indicator (🔴🟡🟢) on the Turn Display widget or as floating badges. Data comes from correlating `CarIdxTrackSurface=OffTrack` events with `LapDistPct` per car per lap.

- [ ] **Pit Window Optimizer** — Real-time undercut/overcut analysis. Tracks when nearby competitors pit, calculates their expected rejoin position, and tells you if pitting NOW would gain or lose positions. Shows "+2P if pit now" or "wait 3 laps: -1P risk". Uses fuel burn + pit time + interval data from the relative table.

- [ ] **Adaptive Widget Density** — ML model that learns your glance patterns (which widgets you look at in corners vs straights) and automatically reduces information density during high-workload sections. E.g., relative table shrinks to 3 rows in a chicane, expands to full in a straight. Uses `LapDistPct` + turn data + steering input as proxies for workload.

- [ ] **Race Narrative Log** — Auto-generates a post-race text summary of your race: "Started P12, gained 4 positions in first 5 laps. Battled with J.Smith for P7 from lap 8-14 (avg gap 0.4s). Lost P6 to R.Johnson after incident on lap 22 (+2x). Finished P8, gained 3 positions overall." Exportable as Markdown for forums/Discord.

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
