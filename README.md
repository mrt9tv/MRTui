# MRT UI — iRacing Telemetry Overlay

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=.net)](https://dotnet.microsoft.com/)
[![Version](https://img.shields.io/badge/Version-0.1.0-blue.svg)](CHANGELOG.md)

> Real-time telemetry overlay for iRacing — modular, glanceable, and race‑optimized.

---

## What It Does

MRT UI renders transparent, always-on-top widgets over iRacing that update at ~60 Hz. It connects to the iRacing SDK through the SVappsLAB telemetry library and displays race-critical data you can read at 200+ km/h.

### Shipped Widgets

| Widget | Purpose |
|--------|---------|
| **MRT One** | Circular tachometer with configurable data blocks (speed, fuel, tires, delta, brake bias, wind, boost, etc.) |
| **Proximity Feed** | Live event ticker — approaching cars, off-track, spins, collisions, pit activity, caution flags, pack density |

### Highlights

- Drag-and-drop widget placement with position save/restore
- Session-aware auto-presets (Practice / Qualifying / Race / Warmup)
- System tray integration with global hotkeys (lock, show/hide)
- Config import/export, per-widget opacity, multi-monitor support
- Auto-hide in pit stall, instant-clear on car tow/disconnect
- CPU and memory usage footer in the dashboard

## Quick Start

1. Install [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows x64)
2. Run `MRT.MRTui.exe`
3. Enter any iRacing session — widgets appear when telemetry connects

**Typical footprint:** ~2% CPU, ~260 MB RAM (both widgets active at 60 Hz).

## Tech Stack

- **.NET 8.0 / WPF** — transparent click-through overlays
- **SVappsLAB.iRacingTelemetrySDK** — source-generated iRacing data binding
- **H.NotifyIcon.Wpf** — system tray
- **Velopack** — single-file packaging and auto-update
