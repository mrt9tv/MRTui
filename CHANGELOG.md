# Changelog

All notable changes to MRT UI will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Planned
- Delta bar widget with optimal lap comparison
- Sector times widget with color-coded splits
- Multi-driver / team mode for endurance racing
- Hybrid power unit display (MGU-K/H, deploy mode) once a car with one is captured

---

## [0.3.2] - 2026-09-12

### Added
- **Race Start widget** — the start lights as five squares (red on SET, green on GO), then reaction time, launch time, technique and best on its own card; JUMP START in red. Hidden until the lights come up, clears with the session. Replaces the line in the Proximity Feed, which lost to the chaos of a race start for the feed's six slots
- **Proximity Feed event families** — every event type now has a switch in the widget (cars in trouble, overtaking, pit activity, flags on other cars, start lights, pace/safety car, red/white/blue flags, chequered, conditions and car warnings, my incidents). The global "Session alerts" toggle is gone

### Changed
- **Switch** — rounded rectangle with a square-cornered knob instead of a pill; matches the cards and inputs around it
- **Scrollbar** — 8 px rounded-rectangle thumb in a faint lane instead of a hairline
- No pill shapes anywhere in the config window: slider track and thumb, connection badge and status dot are rounded rectangles
- Action descriptions sit under their button rather than beside it
- Speed in mph shown to one decimal
- **Shift indicator** now derives from the car's own shift geometry (0% as the lights start, 100% at the optimal RPM); iRacing's `ShiftIndicatorPct` is deprecated and topped out near 90%

### Fixed
- **Proximity Feed switches did nothing for most event types** — they were only applied at display time, and the types without a switch were gated elsewhere. Switches are now enforced in the detector so a disabled type never takes a slot
- **"GO GO GO!" never showed on a rolling start** — it shares an event type with "PACE LAPS", whose fade-out blocked it
- **Flags never showed with no other car nearby** — session-level detection sat behind the relative-table guard
- **Nothing reset on a session advance** — the feed detector, and the race start result, now reset on session change; the start result also clears on a restart
- **Race start never reached the Proximity Feed** — the announcement hung on a one-frame flag the 30 Hz feed missed about half the time; it is keyed on a sequence number now
- Rolling start with the throttle already flat at the green reports "GREEN · already flat" instead of timing out silently
- Each measured start is written to the log

---

## [0.3.1] - 2026-09-12

### Added
- **Race start** toggle in the Proximity Feed event types, so the reaction timer can be switched off

### Changed
- **Settings rows** — every toggle and choice is a row with its description visible under the label (it was tooltip-only), the control on the right, and the whole row clickable
- **Switch** — larger (42×24), knob slides with an ease, on state has a teal halo, keyboard focus is an orange ring
- **Scrollbar** — overlay style: hairline thumb, no arrows or track, widens under the pointer and turns teal while dragged

### Fixed
- Fuel save-target readout formatted with the machine culture ("0,25 L/lap")

---

## [0.3.0] - 2026-09-12

### Added
- **Race start timer** — reaction time from lights-out to first input, launch time to first movement, and the launch technique (clutch, N→gear, throttle-only, rolling). Jump starts are called out. Announced once in the Proximity Feed; a `Reaction` field for MRT One holds it for the session
- **Profiles** — a profile is now a whole layout (positions, sizes, settings, visibility), bound to a session type and car class, applied automatically or from a new hotkey that cycles through them. Save/bind/apply/rename/delete from Settings → Sessions
- **Fuel** — consumption trend (▲/▼ beside L/LAP), full-tank stint length, and a three-state pit window: range ahead, PIT NOW with the last safe lap, LATE
- **Player alerts** — your own black flag, meatball and DSQ, incident gains with the new total, and PITS OPEN / CLOSED, through the Proximity Feed
- **In-car adjustments overlay** — brake bias fine/peak, TC 2–4, DRS and every other `dc*` control the car exposes, shown as they change
- **Pit Confirm** — auto-fill awareness (no more "NOTHING ARMED" in cars whose crew fills automatically), grille tape, weight jackers, charge to add
- **Settings window** — rebuilt around declared settings with search, Basic/Advanced tiers and progressive disclosure; every widget's settings are reachable, including Fuel, Standings and Turn Display which had none before
- **Overlay edit mode** and health diagnostics (dropped frames, frame time, update rate) in the status strip
- **Diagnostics** — rolling log file in `Documents/MRT-UI/logs` with repeat suppression, a UI-thread stall watchdog, and `available_variables.txt` listing every channel the live session publishes
- 48 new telemetry channels from the capability audit (track wetness, engine warnings, FFB clipping, connection quality, tire sets, and more)
- Test projects for Core (106 tests) and WPF (53 tests)

### Changed
- Telemetry SDK moved from `1.0.0-beta.1` to `SVappsLAB.iRacingTelemetrySDK 2.3.0` (torn-read detection, 57 more channels). Builds with the .NET 10 SDK
- Shift points come from the car's own data (`DriverCarSLShiftRPM` and friends) with a −2% / +0.5% window; the learned estimate is the fallback only
- Session info parses once per change rather than once a second; roster changes are logged once
- Per-frame state (relatives, standings, lockups, shift zones) is computed once on the telemetry thread and shared, rather than per widget
- Atomic settings and layout writes with `.bak` recovery; debounced saves

### Fixed
- **UI freezes** — blocking `Dispatcher.Invoke` calls, unbounded render queues and per-tick settings writes removed; latest-wins rendering per widget
- **Frozen overlay after a session change** — the telemetry monitor now restarts itself if the SDK's read loop faults
- **Lap times on comma-decimal locales** rendered as `1:32,345` in MRT One; every formatted value is now culture-invariant
- Profile matching read the car after the match ran, so the first match of a session saw the previous car; it now matches on `CarClassShortName`
- Settings lost on crash; About page blocking the UI thread during update checks

---

## [0.2.0] - 2026-02-23

### Changed
- **DWM Hardware-Accelerated Transparency** — Replaced `AllowsTransparency` (software rendering) with `DwmExtendFrameIntoClientArea` for GPU-composited overlay windows, eliminating the primary FPS impact
- **Non-Blocking UI Dispatch** — Changed `Dispatcher.Invoke()` to `Dispatcher.BeginInvoke()` with 2Hz throttle for session status checks, preventing UI thread blocking
- **Zero-Allocation Telemetry Pipeline** — Pre-allocated array buffers for enum casting and version-stamped dictionary snapshots to eliminate ~960 heap allocations/sec
- **BrushCache Everywhere** — Replaced all `new SolidColorBrush()` calls in hot paths (MRTOne, ProximityFeed, Relative, Standings) with frozen cached brushes
- **Timer Demand Management** — Blink timers (fuel, radar, pit limiter) now start/stop on demand instead of running continuously
- **Per-Widget Update Throttling** — Configurable update rates: MRTOne 60Hz, ProximityFeed 30Hz, Relative/TurnDisplay 20Hz, Fuel 6Hz, Standings 4Hz

### Removed
- Duplicate fuel calculator subscription in App.xaml.cs (was already called internally by telemetry service)

### Performance
- Estimated 65-100% FPS recovery (from "halving iRacing FPS" to near-zero impact)
- 11 files modified across Core and WPF projects

---

## [0.1.001] - 2026-02-13

### Added
- **Relative Data Fields** — 4 new MRT One data fields:
  - Gap Ahead / Gap Behind (time gap in seconds to nearest car)
  - Dist Ahead / Dist Behind (distance in meters to nearest car)
  - Computed from `CarIdxLapDistPct` delta × reference lap time
  - Labels: "GAP ▲", "GAP ▼", "DIST ▲", "DIST ▼"

### Changed
- **MRT One Side Boxes** — Repositioned CUR/LAST/BEST lap displays
  - `SIDE_BOX_MARGIN_H` reduced from 30 → 15 for better centering between edge and gauge
- **Turn Display** — Simplified to show turn numbers only (removed turn names)
  - `FormatTurnInfo()` now returns `T{number}` instead of `T{number}: {name}`

### Fixed
- **ProxFeed disappearing on startup** — ProximityFeed widget now auto-created alongside MRT One on first run, and auto-injected if missing from saved layout
- **Decimal comma locale issue** — Global `InvariantCulture` set in `Program.cs` to ensure dot-decimal formatting everywhere

---

## [0.7.1] - 2026-02-08

### Added
- **Fuel Saving Service** — Lift & coast calculation engine with strategy analysis
- **Fuel Calculator Widget** — Dedicated overlay showing:
  - Fuel remaining, L/lap, laps left with splutter buffer accounting
  - L3/L5 average consumption (toggleable)
  - Projected fuel delta at race end
  - Fuel saving target (L/lap reduction needed)
  - Current saving rate and progress
  - Pit vs save strategy comparison with time delta
  - Strategic alert system with severity levels
- **Fuel Data Enhancements:**
  - `AvgFuelPerLap_L3` — Last 3 laps average for recent trend analysis
  - Splutter threshold now properly subtracted from laps remaining calculation
  - `FuelNeededToFinish` now includes splutter buffer in calculation
- **Turn Display Improvements:**
  - Redesigned vertical layout (NEXT on top, LAST on bottom)
  - Compact 170×110px footprint
  - Optional border animation (teal → orange) based on turn progress
  - Refined Long Beach turn names (Shoreline Dr., Pine Ave., Hairpin, Fountain)
- **MainWindow UI Overhaul:**
  - Two-column layout (left: widget controls, right: alerts & visuals)
  - Cleaner spacing and improved visual hierarchy
  - Per-widget panel visibility system
  - Enhanced fuel alert controls with splutter threshold explanation
- **Data Field System:**
  - Added `FuelSavingTarget`, `FuelProjectedDelta`, `FuelIsPittingFaster` fields
  - Immediate label updates when changing MRT One data fields
  - Proper validation and debug logging for field assignments

### Changed
- **FuelCalculatorService:**
  - Laps remaining now accounts for splutter threshold (usable fuel only)
  - Fuel needed to finish includes splutter buffer in calculation
  - More accurate doable laps computation
- **IRacingTelemetryService:**
  - Integrated `FuelSavingService` into telemetry update loop
  - Saving calculations run after fuel calculator update
- **MRTOne Widget:**
  - Fixed field update issue (labels now update immediately on selection)
  - Better validation for null widget states
  - Enhanced debug logging for troubleshooting
- **Widget Manager:**
  - `HasWidgetType()` now checks for widget existence (not just visibility)
  - Support for Fuel Calculator widget factory
- **Turn Display Widget:**
  - Changed from horizontal 3-column to vertical 2-row layout
  - Removed "CURRENT" turn in favor of cleaner NEXT/LAST design
  - Border animation now optional (off by default)

### Fixed
- MRT One data field changes not persisting between sessions
- Field combo selection events being suppressed incorrectly
- Widget status warnings appearing when widget exists but not yet initialized
- L3 average fuel field missing from calculations

---

## [0.7.0] - 2026-02-07

### Added
- **Turn Display Widget** — Real-time turn tracking overlay
  - Vertical bar design showing last, current, and next turns
  - Turn number and name display
  - Track progress-based border animation (teal → orange)
  - Toggleable turn names
  - Support for 5 tracks: Imola, Monza, Long Beach, Bathurst, Spa
- **Turn Tracking Service** — Backend turn detection system
  - JSON-based track database with turn definitions
  - Per-turn metadata (number, name, type, position range)
  - Track length and total turns information
- **Track Turn Database** — Comprehensive turn data for supported circuits
  - Imola: 17 turns (Tamburello, Villeneuve, Tosa, Acque Minerali, etc.)
  - Monza: 11 turns (Rettifilo, Roggia, Lesmo, Parabolica, etc.)
  - Long Beach: 11 turns (street circuit corners)
  - Bathurst: 23 turns (The Cutting, Hell Corner, The Dipper, etc.)
  - Spa: 19 turns (La Source, Eau Rouge, Raidillon, Pouhon, etc.)

### Changed
- Widget selector dropdown now includes Turn Display option
- MainWindow panel system expanded for widget-specific controls
- Turn Display settings panel with toggle controls

---

## [0.6.0] - 2026-02-05

### Added
- **Fuel Alert System** — Configurable thresholds with visual warnings
  - Yellow caution alert (default: 5 laps remaining)
  - Red urgent alert (default: 2 laps remaining)
  - Critical blinking alert (default: 1 lap remaining)
  - Splutter threshold setting (unusable fuel buffer, default: 0.3L)
- **Advanced Fuel Calculations:**
  - Multiple averaging methods (Last lap, L5, L10, Session, Min/Max)
  - Green/yellow flag consumption tracking
  - Doable laps accounting for splutter buffer
  - Fuel delta to finish calculation
  - Pit strategy: fuel needed vs current fuel
- **Visual Enhancement Toggles:**
  - Gradient background (on by default)
  - Shift ring indicator
  - Glow effects
  - Pit limiter flash alert (on by default)
  - Enhanced radar (future feature)

### Changed
- MRT One fuel display now uses intelligent laps remaining calculation
- Fuel section UI colors: yellow → red → blinking red progression
- MainWindow layout reorganized for better UX
- Cleaner separation of widget controls and visual settings

---

## [0.5.0] - 2026-02-03

### Added
- **Widget Configuration Panel:**
  - Dropdown selector for active widget
  - Show/Hide toggle per widget
  - Lock/Unlock all widgets button
  - Status indicator text
- **MRT One Size Control:**
  - Slider-based sizing (120–600px)
  - Real-time size preview
  - Persistent size configuration
- **MRT One Position Controls:**
  - Center Horizontal button
  - Center Vertical button
  - Center Both (quick alignment)
- **Data Field Customization:**
  - Top field selector (speed, lap time, position, etc.)
  - Center field selector (gear, RPM, fuel, etc.)
  - Bottom field selector (lap number, session time, etc.)
  - Left/right side box selectors for compact metrics
- **Field Categories:**
  - Speed & Motion (8 fields)
  - Engine (12 fields)
  - Fuel (20+ fields with advanced calculations)
  - Timing (13 fields)
  - Inputs (6 fields)
  - Temperatures (8 fields including tire temps)
  - Session (6 fields)
  - Track & Flags (4 fields)

### Changed
- MainWindow redesigned with card-based layout
- MRT theme styling applied throughout control panel
- Improved visual hierarchy with labels and structured grids

---

## [0.4.0] - 2026-01-30

### Added
- **MRT One Widget Foundation:**
  - Circular gauge design with gear center
  - Racelabs-inspired aesthetic
  - MRT theme colors (teal #008080, orange #FF8000)
  - Draggable widget with window state management
  - Always-on-top window configuration
- **Live Position Calculator** — Race/qual aware position tracking
  - Frozen position on checkered flag
  - Class position calculation
  - Dynamic position updates
- **Telemetry Data Mapping System:**
  - Centralized field provider service
  - Type-safe enum-based field identifiers
  - Category organization (Speed, Engine, Fuel, Timing, etc.)
  - Extensible architecture for new fields

### Changed
- Switched from console output to WPF overlay window
- Telemetry service now provides structured data updates
- Widget base class for future multi-widget support

---

## [0.3.0] - 2026-01-25

### Added
- **Fuel Calculator Service** — Comprehensive fuel strategy calculations
  - Per-lap fuel tracking with history
  - Rolling window averages (Last, L5, L10, Session)
  - Min/max lap consumption tracking
  - Green flag vs yellow flag consumption separation
  - Laps remaining with multiple calculation methods
  - Fuel needed to finish with buffer
  - Current lap fuel projection
- **Proximity Calculator** — Multi-zone car detection system
  - Ahead/behind detection
  - Left/right lateral detection
  - Multi-class handling with lap differential logic
  - ProximityInfo data structure
- **Lateral Spotter** — Side-by-side racing awareness
  - Left/right lateral position tracking
  - Three-state system (Clear, Alongside, Overlap)
  - iRacing CarLeftRight validation integration

### Changed
- Telemetry service architecture improved for service composition
- Core services now use dependency injection pattern

---

## [0.2.0] - 2026-01-20

### Added
- **iRacing Telemetry Service** — Core telemetry integration
  - SVappsLAB SDK integration for iRacing data
  - ~60Hz telemetry update rate
  - Connection status management (Disconnected, Connecting, Connected, Error)
  - Session info parsing (track name, session type, etc.)
  - Comprehensive telemetry channels (speed, RPM, gear, inputs, temps, etc.)
- **Unit Conversions** — Multi-unit support
  - Speed: m/s ↔ km/h ↔ mph
  - Temperature: Celsius ↔ Fahrenheit
  - Fuel: Liters ↔ Gallons
  - Distance: Meters ↔ Feet
- **Telemetry Calculations** — Derived metrics
  - G-force calculations (lateral, longitudinal, combined)
  - Steering angle conversions
  - Percentage-based metrics (throttle, brake, clutch)

### Changed
- Solution structure: Core library + WPF application
- Separated telemetry logic from presentation layer

---

## [0.1.0] - 2026-01-15

### Added
- **Initial Project Setup**
  - .NET 8.0 solution structure
  - Core library project (telemetry models and services)
  - WPF application project (UI layer)
  - Console telemetry output (pre-overlay phase)
- **Basic Models:**
  - `TelemetryData` — Core telemetry data structure
  - `ConnectionStatus` — Telemetry connection states
  - `TelemetryChannels` — iRacing SDK channel definitions
- **Proof of Concept:**
  - Console-based telemetry display
  - iRacing connection detection
  - Live data streaming

---

## Release Types

- **Major (X.0.0):** Breaking changes, major architectural shifts
- **Minor (0.X.0):** New features, widgets, or significant enhancements
- **Patch (0.0.X):** Bug fixes, minor improvements, documentation updates

---

## Legend

- `Added` — New features, widgets, or capabilities
- `Changed` — Modifications to existing functionality
- `Deprecated` — Features marked for removal in future versions
- `Removed` — Deleted features or code
- `Fixed` — Bug fixes and error corrections
- `Security` — Vulnerability patches and security improvements

---

[0.1.001]: https://github.com/mrt9tv/MRTui/compare/v0.7.1...v0.1.001