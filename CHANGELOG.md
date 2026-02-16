# Changelog

All notable changes to MRT UI will be documented in this file.

---

## [0.1.0] — Initial Release

### MRT One Widget
- Circular tachometer with real-time RPM arc and shift-light color bands
- 8 configurable data blocks: Speed, Gear, Fuel, Laps Remaining, Lap Delta, Brake Bias, Tire Wear, Steering Angle, Wind Speed/Direction, P2P/Push-to-Pass
- Fuel strategy — remaining laps, fuel-to-finish, per-lap consumption, fuel-save target
- Lap delta with color-coded gain/loss indicator
- Incident counter with flash-on-increment visual
- Session info strip: position, lap count, session time, session type

### Proximity Feed Widget
- Live event ticker for nearby car activity within configurable detection radius
- Event types: off-track, spin, slow car, stopped car, collision, approaching/incoming fast, pit entry/exit, local yellow, blue flag, black flag, disqualified
- APPROACHING warnings from both directions when player is off-track or stopped on track
- Pit-exit merge warning with grace period
- Pack density alert (CHAOS ZONE) with blinking indicator when 3+ nearby events fire simultaneously
- Severity-based color coding (info / warning / danger)
- Per-car event cooldowns and debounce to reduce false positives
- Instant-clear on car tow or disconnect

### Dashboard (MainWindow)
- Session-aware auto-presets: Practice, Qualifying, Race, Warmup
- System tray integration with context menu
- Global hotkeys: Ctrl+H toggle visibility, Ctrl+L lock/unlock positions
- Config import/export (JSON)
- Per-widget opacity control
- Multi-monitor support with per-display layout saving
- Auto-hide when player is in pit stall
- CPU and memory usage footer
- About page with version info and update checker
- Invariant culture formatting — decimal dots regardless of Windows locale
- Velopack packaging with single-file exe and auto-update

---

[0.1.0]: https://github.com/mrt9tv/MRTui/releases/tag/v0.1.0
