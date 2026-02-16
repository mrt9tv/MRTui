# Changelog

All notable changes to MRT UI will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Planned
- Relative timing board widget (±3 positions with gaps)
- Delta bar widget with optimal lap comparison
- Sector times widget with color-coded splits
- Profile system with auto-detection
- Multi-driver / team mode for endurance racing

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

[Unreleased]: https://github.com/mrt9tv/MRTui/compare/v0.7.1...HEAD
[0.7.1]: https://github.com/mrt9tv/MRTui/compare/v0.7.0...v0.7.1
[0.7.0]: https://github.com/mrt9tv/MRTui/compare/v0.6.0...v0.7.0
[0.6.0]: https://github.com/mrt9tv/MRTui/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/mrt9tv/MRTui/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/mrt9tv/MRTui/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/mrt9tv/MRTui/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/mrt9tv/MRTui/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/mrt9tv/MRTui/releases/tag/v0.1.0
