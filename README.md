# MRT UI — iRacing Telemetry Overlay

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=.net)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Proprietary-red.svg)](LICENSE)
[![Version](https://img.shields.io/badge/Version-0.1.001-blue.svg)](CHANGELOG.md)

> **Professional-grade telemetry overlay system for iRacing featuring modular widgets, intelligent fuel strategy, and race-optimized UI design.**

![MRT UI Banner](docs/images/banner.png)

---

## 🎯 Overview

MRT UI is a next-generation iRacing overlay application that provides real-time telemetry visualization through a modular widget system. Built with WPF and .NET 8.0, it delivers race-critical information with minimal performance overhead and maximum customization flexibility.

### Key Features

- **🎛️ Modular Widget System** — Independent, draggable widgets for different telemetry aspects
- **⛽ Advanced Fuel Management** — Multi-method fuel calculations with lift & coast strategy analysis
- **🏁 Track Turn Tracking** — Real-time turn identification for supported circuits
- **🎨 MRT Theme Design** — Racelabs-inspired aesthetic with teal/orange accents
- **⚡ High Performance** — ~60Hz telemetry updates with negligible CPU overhead
- **💾 Profile System** — Save and load complete overlay configurations

---

## 📦 Installation

### Prerequisites

- **Windows 10/11** (64-bit)
- **.NET 8.0 Runtime** ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- **iRacing** subscription and installation

### Quick Start

1. **Download** the latest release from [Releases](https://github.com/mrt9tv/MRTui/releases)
2. **Extract** to your preferred location (e.g., `C:\MRT-UI\`)
3. **Run** `START_OVERLAY.bat` or launch `iRacingOverlay.WPF.exe`
4. **Start iRacing** and enter any session (Test Drive, Practice, Race, etc.)
5. **Configure** widgets through the control panel

> **Note:** The overlay auto-connects once you're on track. It won't display data in the iRacing main menu.

---

## 🎨 Widgets

### MRT One — Circular Gauge

The flagship widget featuring a compact circular design inspired by Racelabs:

**Core Display:**
- **Center:** Gear indicator (R, N, 1-6)
- **Top:** Customizable data field (speed, lap time, position, etc.)
- **Bottom:** Customizable data field (RPM, fuel, session time, etc.)
- **Sides:** Left/right customizable fields (compact mode)

**Visual Features:**
- Gradient background (toggleable)
- RPM-based shift ring with dynamic color progression
- Glow effects for shift point indication
- Pit limiter visual alert
- Fuel level alerts (yellow/red/critical with color-coded warnings)

**Fuel Intelligence:**
- Multiple calculation methods (Last lap, L3, L5, L10, Session, Min/Max)
- Green/yellow flag consumption tracking
- Laps remaining with splutter buffer accounting
- Fuel needed to finish with safety margin
- Configurable alert thresholds

**Supported Data Fields:**
- Speed (m/s, km/h, mph)
- Engine (RPM, gear, water temp, oil temp)
- Fuel (level, %, per lap, laps remaining, delta to finish)
- Timing (lap number, position, lap times, session info)
- Inputs (throttle, brake, clutch, steering)
- Temperatures (tires, ambient, track)
- Flags and session state

### Turn Display — Track Position

Compact vertical widget showing turn information:

**Display:**
- **NEXT** — Upcoming turn number and name (teal accent)
- **LAST** — Completed turn number and name (orange accent)

**Options:**
- Toggle turn names on/off
- Optional border animation based on turn progress (teal → orange interpolation)

**Supported Tracks:**
- Autodromo Enzo e Dino Ferrari (Imola)
- Autodromo Nazionale di Monza
- Streets of Long Beach
- Mount Panorama Circuit (Bathurst)
- Circuit de Spa-Francorchamps

### Fuel Calculator — Strategy Analysis

Advanced fuel saving and strategy widget featuring lift & coast calculations:

**Core Metrics:**
- **Fuel remaining** — Current fuel level (L) and tank percentage
- **L/Lap** — Average consumption using selected method
- **Laps Left** — Realistic laps remaining (accounts for 0.3L splutter buffer)

**Averages (toggleable):**
- **AVG L3** — Last 3 laps average (latest trend)
- **AVG L5** — Last 5 laps average (preferred for calculations)
- **BUFFER** — Splutter threshold display (unusable fuel at tank bottom)

**Strategy Analysis:**
- **PROJ DELTA** — Projected fuel surplus/deficit at race end
- **SAVE TGT** — L/lap reduction needed to finish without pitting
- **SAVING** — Current fuel saving rate (recent laps vs average)
- **STRATEGY** — Pit vs Save time comparison with recommendation

**Alert System:**
- Real-time strategic alerts with severity levels
- Fuel saving progress indicators
- Lift point suggestions (light/medium/heavy)
- Pit-versus-save time delta calculations

---

## ⚙️ Configuration

### Widget Controls

The control panel provides per-widget configuration:

**Size & Position:**
- Slider-based size control (120–600px)
- Quick centering buttons (horizontal, vertical, both)
- Free drag positioning when unlocked

**Data Fields:**
- Dropdown selectors for each display position
- Live preview updates
- Persistent between sessions

**Visual Effects (MRT One):**
- Gradient background
- Shift ring indicator
- Glow effects
- Pit limiter flash
- Enhanced radar (when available)

**Fuel Alerts (MRT One):**
- **Yellow** — Caution threshold (default: 5 laps)
- **Red** — Urgent threshold (default: 2 laps)
- **Critical** — Blinking alert (default: 1 lap)
- **Splutter** — Unusable fuel buffer (default: 0.3L)

### Global Settings

**Layout Management:**
- **Lock/Unlock** — Toggle widget drag capability
- **Show/Hide** — Per-widget visibility control
- **Save Layout** — Persistent configuration storage

**Hotkeys (Default):**
- `Ctrl+L` — Toggle lock state
- `Ctrl+H` — Toggle visibility for active widget

**Profile Location:**
```
%USERPROFILE%\Documents\MRT-UI\layout.json
```

---

## 🔧 Development

### Building from Source

```bash
# Clone repository
git clone https://github.com/mrt9tv/MRTui.git
cd MRTui

# Restore dependencies
dotnet restore iRacingOverlay.sln

# Build solution
dotnet build iRacingOverlay.sln --configuration Release

# Run WPF overlay
dotnet run --project src/iRacingOverlay.WPF
```

### Project Structure

```
MRTui/
├── src/
│   ├── iRacingOverlay.Core/        # Telemetry services & calculations
│   │   ├── Services/
│   │   │   ├── IRacingTelemetryService.cs
│   │   │   ├── FuelCalculatorService.cs
│   │   │   ├── FuelSavingService.cs
│   │   │   ├── TurnTrackingService.cs
│   │   │   └── ProximityCalculator.cs
│   │   ├── Models/
│   │   └── Data/
│   │       └── TrackTurnDatabase.json
│   │
│   └── iRacingOverlay.WPF/         # UI layer & widgets
│       ├── Widgets/
│       │   ├── MRTOneWidget/
│       │   ├── TurnDisplayWidget/
│       │   └── FuelWidget/
│       ├── Services/
│       │   ├── WidgetManager.cs
│       │   └── TelemetryFieldProvider.cs
│       ├── Models/
│       └── MainWindow.xaml
│
├── docs/                            # Documentation
├── ai-tune/                         # AI assistant integration
├── iRacingOverlay.sln              # Solution file
├── README.md                        # This file
├── CHANGELOG.md                     # Version history
└── TODO.md                          # Feature roadmap
```

### Tech Stack

- **Framework:** .NET 8.0 (C# 12)
- **UI:** WPF with XAML
- **Telemetry SDK:** [SVappsLAB.iRacingTelemetrySDK](https://www.nuget.org/packages/SVappsLAB.iRacingTelemetrySDK)
- **MVVM:** CommunityToolkit.Mvvm
- **DI:** Microsoft.Extensions.Hosting

---

## 🗺️ Roadmap

See [TODO.md](TODO.md) for the complete feature roadmap.

### Upcoming Features

**Widgets:**
- Relative timing board (±3 positions)
- Delta bar with optimal lap comparison
- Sector times display
- Inputs trace graph
- Track map with competitor positions

**Systems:**
- Pit strategy calculator with undercut/overcut analysis
- Weather & track condition monitoring
- Multi-driver / team mode for endurance racing
- Profile system with auto-detection (oval, road, weather)

### Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📄 License

MRT UI is proprietary software. Copyright (c) 2026 MRT9. All rights reserved.

The Software is provided for personal, non-commercial use only. Redistribution,
reverse engineering, and modification are prohibited. See [LICENSE](LICENSE) for full terms.

This project uses third-party open-source components. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for attributions and license details.

---

## 🙏 Acknowledgments

- **iRacing** — For providing the simulation platform and telemetry API
- **SVappsLAB** — For the excellent iRacing Telemetry SDK
- **Racelabs** — Design inspiration for MRT One widget
- **SimHub** — Inspiration for modular overlay architecture

---

## 📞 Support

- **Issues:** [GitHub Issues](https://github.com/mrt9tv/MRTui/issues)
- **Discussions:** [GitHub Discussions](https://github.com/mrt9tv/MRTui/discussions)
- **Documentation:** [Wiki](https://github.com/mrt9tv/MRTui/wiki)

---

## ⚠️ Disclaimer

MRT UI is not affiliated with, endorsed by, or in any way officially connected with iRacing.com Motorsport Simulations or any of its subsidiaries or affiliates. The official iRacing website can be found at https://www.iracing.com.

All product names, logos, and brands are property of their respective owners.

---

<div align="center">

**Built with ❤️ for the iRacing community**

[⬆ Back to Top](#mrt-ui--iracing-telemetry-overlay)

</div>
