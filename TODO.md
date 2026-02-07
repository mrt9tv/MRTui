# MRT UI — Feature Roadmap

> Single source of truth for planned features and enhancements.
> Items are numbered for reference; order is not strict priority.

---

## Widgets — Planned

### 1. Contextual Center Field Enhancement (MRT One)
Auto-swap the center display based on situation: show gap-to-car-ahead during racing, fuel remaining under caution, lap delta on out-laps, and "PIT NOW" countdown when fuel is critical. One smart field replaces needing to manually toggle.

### 2. Relative / Timing Board Widget
A compact leaderboard showing cars around you (3 ahead, 3 behind) with intervals, driver names, and pit status — essential for race strategy.

### 11. Delta Bar Widget
A dedicated horizontal widget showing live lap delta (ahead/behind best/optimal) with colour-coded gradient, similar to SimHub delta bars.

### 13. Track Map Widget
A minimap of the current circuit with coloured dots for all cars, highlighting your position, cars within proximity, and pit lane entries.

### 14. Fuel Strategy Standalone Widget
A larger dedicated fuel strategy panel: required fuel to finish, optimal add amount, number of pit stops remaining, under/over-fuel buffer, and stint plan summary. More detail than the in-gauge fields.

### 15. Fuel Burn Rate Graph Widget
A mini line chart showing fuel consumption per lap over the session (or last N laps). Highlights stints, averages, and outlier laps. Useful for spotting lift-and-coast efficiency.

### 16. Overtake / Traffic Alert Widget
Warns when higher-class cars (or significantly faster cars) are approaching from behind for multi-class traffic anticipation. Shows closing rate, class colour, estimated time-to-reach, and which side they're likely to pass. Audio chime option.

### 17. Inputs Trace Widget
A real-time throttle/brake/steering trace similar to MoTeC or iSpeed. Scrolling line graph that helps identify driving consistency and technique improvements post-session.

### 18. Penalty & Incident Monitor Widget
Shows current incident count vs. limit, active penalties (drive-through, stop-and-go), time remaining on penalties, and incident history with lap references.

### 19. Sector Times Widget
Live sector splits comparing current lap to personal best, session best, and overall best. Colour-coded (purple/green/yellow/red) per sector like F1 timing screens.

---

## Systems — Planned
### 20. Turn Number + Name Widget
A small widget displaying the current turn number and turn name (e.g., "T3: Casino Square"), updating dynamically as you navigate the track. Useful for referencing setup notes, coaching instructions, or track guide videos.


### 3. Pit Strategy Calculator
Auto-calculate mandatory pit windows, optimal pit lap based on remaining fuel laps, tyre degradation rate, and undercut/overcut gap analysis.

### 7. Session Stint History
Graph showing fuel consumption, lap times, and tyre wear per stint across the session, helping identify degradation curve trends for strategy decisions.

> **Note:** Tyre temperature and wear data is only available when the car is in pit lane. Stint history must capture pit-entry snapshots and interpolate between them.

### 8. Weather & Track Condition Widget
Display current/forecast weather, track temperature, wind direction, and grip level — critical for setup adjustments and tyre strategy in dynamic weather races.

### 9. Multi-Driver / Team Mode
Support for endurance team racing: driver swap tracking, stint assignments, shared fuel strategy, fair-share driving time calculations.

> **Note:** This may need to be its own separate window within the MRT UI application rather than an in-overlay widget, due to the amount of data and controls required.

### 10. Profile System
Save/load complete overlay configurations (widget positions, sizes, field assignments, alert thresholds, visual toggles) as named profiles — e.g., "Oval", "Road Course", "Night Rain".

Auto-detection capabilities:
- Detect vehicle class (open-wheel, GT, prototype, oval stock car) and suggest or auto-switch profiles.
- Detect track type (oval vs road course) from session info.
- Allow user override and manual profile selection.

---

## Legend

| Status | Meaning |
|--------|---------|
| (unmarked) | Planned — not yet started |
| `[WIP]` | Work in progress |
| `[DONE]` | Completed and merged |
