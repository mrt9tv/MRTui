# iRacing Telemetry Variable Registry
**Complete reference for all available telemetry variables**

**Last Updated**: October 18, 2025  
**SDK Version**: SVappsLAB.iRacingTelemetrySDK v1.0.0-beta.1  
**iRacing SDK**: irsdk_1_19  
**Total Variables**: 324 (from live session dump)

---

## Table of Contents
- [Usage Guide](#usage-guide)
- [Core Driving Variables](#core-driving-variables)
- [Engine & Power](#engine--power)
- [Tires & Wheels](#tires--wheels)
- [Braking System](#braking-system)
- [Driver Aids](#driver-aids)
- [Timing & Position](#timing--position)
- [Weather & Track](#weather--track)
- [Multi-Car Arrays](#multi-car-arrays)
- [Pit & Strategy](#pit--strategy)
- [Session Info (YAML)](#session-info-yaml)
- [Derived Calculations](#derived-calculations)

---

## Usage Guide

### Variable Entry Format
```
VariableName
├─ Type: float | int | bool | string | array
├─ Units: m/s, celsius, liters, radians, etc.
├─ Range: Min-Max or enum values
├─ Source: Telemetry | YAML | Both
├─ Availability: Always | Car-specific | Session-specific
└─ Conversions: Formula for imperial/derived values
```

### **⚠️ Important Notes:**
- **Wheel Speeds**: `LFspeed`, `RFspeed`, `LRspeed`, `RRspeed` are **NOT available** in current SDK builds (as of Oct 2025)
- **Car-Specific Variables**: Some variables (TC, ABS, P2P) only available in cars that have these features
- **YAML vs Telemetry**: Session info is parsed once at connection, telemetry streams at 60Hz

---

## Core Driving Variables

### Speed & Motion
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `Speed` | float | m/s | 0-150+ | Vehicle speed (NOT wheel speed) |
| `VelocityX` | float | m/s | ±100 | World-space X velocity |
| `VelocityY` | float | m/s | ±100 | World-space Y velocity (vertical) |
| `VelocityZ` | float | m/s | ±100 | World-space Z velocity |
| `Yaw` | float | radians | 0-2π | Vehicle heading angle |
| `YawRate` | float | rad/s | ±5 | Rate of heading change |
| `Pitch` | float | radians | ±π/2 | Vehicle pitch angle |
| `PitchRate` | float | rad/s | ±5 | Rate of pitch change |
| `Roll` | float | radians | ±π | Vehicle roll angle |
| `RollRate` | float | rad/s | ±5 | Rate of roll change |

**Conversions:**
```csharp
// Speed conversions
float speedKmh = Speed * 3.6f;           // m/s → km/h
float speedMph = Speed * 2.23694f;       // m/s → mph

// Angular conversions
float yawDegrees = Yaw * (180f / MathF.PI);  // radians → degrees
```

### Acceleration (G-Forces)
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `LatAccel` | float | m/s² | ±50 | Lateral G-force (left/right) |
| `LongAccel` | float | m/s² | ±50 | Longitudinal G-force (forward/brake) |
| `VertAccel` | float | m/s² | ±50 | Vertical G-force (bumps) |

**Conversions:**
```csharp
// Convert to G-units (1G = 9.81 m/s²)
float latG = LatAccel / 9.81f;
float longG = LongAccel / 9.81f;
```

### Driver Inputs
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `Throttle` | float | 0-1 | 0.0-1.0 | Throttle input (0% - 100%) |
| `Brake` | float | 0-1 | 0.0-1.0 | Brake input (0% - 100%) |
| `Clutch` | float | 0-1 | 0.0-1.0 | Clutch input (0% - 100%) |
| `SteeringWheelAngle` | float | radians | ±π | Steering wheel rotation |
| `BrakeRaw` | float | 0-1 | 0.0-1.0 | Raw brake pedal (pre-ABS) |
| `ThrottleRaw` | float | 0-1 | 0.0-1.0 | Raw throttle (pre-TC) |
| `ClutchRaw` | float | 0-1 | 0.0-1.0 | Raw clutch pedal |
| `HandbrakeRaw` | float | 0-1 | 0.0-1.0 | Handbrake input (rally cars) |

**Conversions:**
```csharp
// Percentage display
int throttlePct = (int)(Throttle * 100);  // 0-100%
int brakePct = (int)(Brake * 100);        // 0-100%

// Steering angle in degrees
float steerDeg = SteeringWheelAngle * (180f / MathF.PI);
```

---

## Engine & Power

### RPM & Gearing
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `RPM` | float | rev/min | 0-20000 | Engine RPM |
| `Gear` | int | gear | -1 to 8 | Current gear (-1=R, 0=N, 1-8=forward) |
| `PlayerCarSLFirstRPM` | float | rev/min | 0-20000 | **Shift light start** RPM |
| `PlayerCarSLShiftRPM` | float | rev/min | 0-20000 | **OPTIMAL SHIFT POINT** (key value!) |
| `PlayerCarSLLastRPM` | float | rev/min | 0-20000 | Shift lights fully lit RPM |
| `PlayerCarSLBlinkRPM` | float | rev/min | 0-20000 | Over-rev warning blink RPM |
| `Engine0_RPM` | float | rev/min | 0-20000 | Engine RPM (alt name) |

**Key Formulas:**
```csharp
// RPM percentage to redline
float rpmPct = (RPM / PlayerCarSLBlinkRPM) * 100f;

// Shift light zones
bool inShiftZone = RPM >= PlayerCarSLFirstRPM && RPM < PlayerCarSLShiftRPM;
bool shouldShift = RPM >= PlayerCarSLShiftRPM;
bool overRev = RPM >= PlayerCarSLBlinkRPM;

// Gear ratio calculation (requires YAML car info)
// gear_ratio = (wheel_circumference * RPM) / (speed * gear_final_ratio * 60)
```

### Temperature & Fluids
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `WaterTemp` | float | °C | 0-150 | Engine coolant temp |
| `WaterLevel` | float | liters | 0-20 | Coolant level |
| `OilTemp` | float | °C | 0-180 | Engine oil temp |
| `OilLevel` | float | liters | 0-20 | Oil level |
| `OilPress` | float | bar | 0-10 | Oil pressure |
| `FuelLevel` | float | liters | 0-120 | Fuel remaining |
| `FuelLevelPct` | float | 0-1 | 0.0-1.0 | Fuel % of tank capacity |
| `FuelPress` | float | bar | 0-10 | Fuel line pressure |
| `FuelUsePerHour` | float | kg/hr | 0-200 | Fuel consumption rate |

**Conversions:**
```csharp
// Temperature: Celsius → Fahrenheit
float waterTempF = (WaterTemp * 9f / 5f) + 32f;
float oilTempF = (OilTemp * 9f / 5f) + 32f;

// Fuel: Liters → Gallons
float fuelGal = FuelLevel * 0.264172f;

// Fuel consumption: kg/hr → L/hr (assumes fuel density ~0.75 kg/L for gasoline)
float fuelLitersPerHour = FuelUsePerHour / 0.75f;
```

**Warning Thresholds:**
```csharp
// Conservative warning levels
bool waterTempCritical = WaterTemp > 100f;  // Red alert
bool waterTempWarn = WaterTemp > 90f;       // Yellow alert

bool oilTempCritical = OilTemp > 120f;      // Red alert
bool oilTempWarn = OilTemp > 110f;          // Yellow alert

bool fuelCritical = FuelLevel < 5f;         // Red alert (<5L)
bool fuelLow = FuelLevel < 10f;             // Yellow alert (<10L)
```

---

## Tires & Wheels

### ⚠️ WHEEL SPEEDS NOT AVAILABLE
**Critical Finding**: `LFspeed`, `RFspeed`, `LRspeed`, `RRspeed` are **NOT in current SDK builds**.
- These variables are mentioned in documentation but not present in actual telemetry dumps
- Wheel lock / TC activation detection via slip ratio is **NOT POSSIBLE** with current SDK
- Alternative: Use brake line pressure + ABS active state for indirect wheel lock detection

### Tire Temperatures (3 zones per tire)
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `LFtempCL` | float | °C | 0-150 | LF Center-Left |
| `LFtempCM` | float | °C | 0-150 | LF Center-Middle |
| `LFtempCR` | float | °C | 0-150 | LF Center-Right |
| `RFtempCL` / `CM` / `CR` | float | °C | 0-150 | RF temps |
| `LRtempCL` / `CM` / `CR` | float | °C | 0-150 | LR temps |
| `RRtempCL` / `CM` / `CR` | float | °C | 0-150 | RR temps |

**Formulas:**
```csharp
// Average tire temperature
float avgLF = (LFtempCL + LFtempCM + LFtempCR) / 3f;

// Temperature imbalance (indicates setup issues)
float lfImbalance = Math.Abs(LFtempCL - LFtempCR);  // Camber/pressure issue if > 10°C

// Temperature spread (left vs right side)
float frontSpread = avgLF - avgRF;  // Should be near zero for balanced setup
```

### Tire Wear (3 zones per tire)
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `LFwearL` / `M` / `R` | float | 0-1 | 0.0-1.0 | LF wear (Left/Middle/Right) |
| `RFwearL` / `M` / `R` | float | 0-1 | 0.0-1.0 | RF wear |
| `LRwearL` / `M` / `R` | float | 0-1 | 0.0-1.0 | LR wear |
| `RRwearL` / `M` / `R` | float | 0-1 | 0.0-1.0 | RR wear |

**Formulas:**
```csharp
// Average tire wear (0=new, 1=worn out)
float avgLFWear = (LFwearL + LFwearM + LFwearR) / 3f;

// Wear percentage remaining
float wearRemainingPct = (1f - avgLFWear) * 100f;

// Critical wear check
bool tireCritical = avgLFWear > 0.9f;  // < 10% remaining
```

---

## Braking System

### Brake Variables
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `Brake` | float | 0-1 | 0.0-1.0 | Effective brake input (post-ABS) |
| `BrakeRaw` | float | 0-1 | 0.0-1.0 | Raw pedal input (pre-ABS) |
| `BrakeABSactive` | bool | - | 0/1 | **ABS actively preventing lock** |
| `dcBrakeBias` | float | % | 0-100 | Brake bias (% front bias) |
| `LFbrakeLinePress` | float | bar | 0-20 | LF brake line pressure |
| `RFbrakeLinePress` | float | bar | 0-20 | RF brake line pressure |
| `LRbrakeLinePress` | float | bar | 0-20 | LR brake line pressure |
| `RRbrakeLinePress` | float | bar | 0-20 | RR brake line pressure |

**Key Insights:**
```csharp
// ABS activation detection (works well!)
bool absWorking = BrakeABSactive;  // True = ABS cutting brake pressure

// Brake pressure imbalance (setup check)
float frontAvgPressure = (LFbrakeLinePress + RFbrakeLinePress) / 2f;
float rearAvgPressure = (LRbrakeLinePress + RRbrakeLinePress) / 2f;
float biasPressureRatio = frontAvgPressure / rearAvgPressure;

// Indirect wheel lock detection (since wheel speeds unavailable)
// If brake pressure high + ABS NOT active + vehicle speed high = possible wheel lock
bool possibleWheelLock = Brake > 0.8f && !BrakeABSactive && Speed > 20f && 
                         (LFbrakeLinePress > 15f || RFbrakeLinePress > 15f);
```

---

## Driver Aids

### Traction Control & ABS
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `dcTractionControl` | float | level | 0-20 | TC strength level (car-specific) |
| `BrakeABSactive` | bool | - | 0/1 | ABS active indicator |

**Availability**: Car-specific (GT3, LMP2, F1 have TC/ABS; most road cars don't)

**⚠️ TC Activation Detection NOT POSSIBLE**:
- No `dcTCactive` variable exists (unlike `BrakeABSactive`)
- Wheel speed variables unavailable for slip ratio calculation
- Can only detect if TC is **enabled** (level > 0), not **actively working**

**Display Logic:**
```csharp
// TC display
if (dcTractionControl < 0) 
    return "N/A";  // Car doesn't have TC
else if (dcTractionControl == 0)
    return "OFF";  // TC disabled
else
    return $"{(int)dcTractionControl}";  // TC level (1-20)

// ABS display
string absStatus = BrakeABSactive ? "ACTIVE" : "READY";
```

### Other Aids
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `dcPitSpeedLimiterToggle` | bool | - | 0/1 | Pit speed limiter active |
| `CarIdxP2P_Status` | bool[] | - | 0/1 | Push-to-pass active (per car) |
| `CarIdxP2P_Count` | int[] | - | 0-20 | P2P uses remaining (per car) |

---

## Timing & Position

### Lap Data
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `Lap` | int | lap# | 0-500 | Current lap number |
| `LapDistPct` | float | 0-1 | 0.0-1.0 | Distance around lap (0%=S/F, 100%=S/F) |
| `LapBestLap` | int | lap# | 0-500 | Lap number of personal best |
| `LapBestLapTime` | float | seconds | 0-600 | Personal best lap time |
| `LapLastLapTime` | float | seconds | 0-600 | Last completed lap time |
| `LapCurrentLapTime` | float | seconds | 0-600 | Current lap time (running) |
| `LapDeltaToBestLap` | float | seconds | ±60 | Delta to personal best |
| `LapDeltaToBestLap_DD` | float | seconds | ±60 | Delta-delta (rate of change) |
| `LapDeltaToSessionBestLap` | float | seconds | ±60 | Delta to session best |

**Conversions:**
```csharp
// Lap time formatting: M:SS.mmm
string FormatLapTime(float seconds) {
    int min = (int)(seconds / 60);
    float sec = seconds % 60;
    return $"{min}:{sec:00.000}";
}

// Track position in meters (requires YAML track length)
float trackPosition = LapDistPct * trackLengthMeters;
```

### Position & Classification
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `PlayerCarClassPosition` | int | pos | 1-64 | Position in class |
| `PlayerCarPosition` | int | pos | 1-64 | Overall position |
| `PlayerCarIdx` | int | index | 0-63 | Player's car index |
| `PlayerCarClass` | int | classID | 0-100 | Player's car class ID |

---

## Weather & Track

### Atmospheric Conditions
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `AirTemp` | float | °C | -20-50 | Ambient air temperature |
| `TrackTemp` | float | °C | 0-80 | Track surface temperature |
| `TrackTempCrew` | float | °C | 0-80 | Track temp from crew chief |
| `AirDensity` | float | kg/m³ | 0.8-1.4 | Air density (affects downforce) |
| `AirPressure` | float | hPa | 900-1100 | Atmospheric pressure |
| `RelativeHumidity` | float | % | 0-100 | Relative humidity |
| `FogLevel` | float | % | 0-100 | Fog density |
| `Skies` | int | enum | 0-3 | Sky condition (0=clear, 3=overcast) |
| `WeatherType` | int | enum | 0-6 | Weather type |

**Conversions:**
```csharp
// Temperature: Celsius → Fahrenheit
float airTempF = (AirTemp * 9f / 5f) + 32f;
float trackTempF = (TrackTemp * 9f / 5f) + 32f;
```

### Track Surface
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `OnPitRoad` | bool | - | 0/1 | Player on pit road |
| `CarIdxTrackSurface` | int[] | enum | 0-5 | Track surface type per car |
| `CarIdxTrackSurfaceMaterial` | int[] | enum | 0-10 | Surface material per car |

**Surface Types** (CarIdxTrackSurface):
- 0 = Not in world
- 1 = Off track
- 2 = In pit stall
- 3 = Approaching pits
- 4 = On track

---

## Multi-Car Arrays
**All arrays are [64] elements (0-63 car indices)**

### Position & Timing Arrays
| Variable | Type | Notes |
|----------|------|-------|
| `CarIdxLap` | int[64] | Current lap for each car |
| `CarIdxLapDistPct` | float[64] | Track position % for each car |
| `CarIdxPosition` | int[64] | Overall position |
| `CarIdxClassPosition` | int[64] | Class position |
| `CarIdxBestLapTime` | float[64] | Best lap time (seconds) |
| `CarIdxLastLapTime` | float[64] | Last lap time (seconds) |
| `CarIdxEstTime` | float[64] | Estimated time to reach start/finish |
| `CarIdxF2Time` | float[64] | Time behind leader |

### Car State Arrays
| Variable | Type | Notes |
|----------|------|-------|
| `CarIdxGear` | int[64] | Current gear |
| `CarIdxRPM` | float[64] | Engine RPM |
| `CarIdxClass` | int[64] | Car class ID |
| `CarIdxOnPitRoad` | bool[64] | On pit road status |
| `CarIdxTrackSurface` | int[64] | Track surface type |
| `CarIdxSessionFlags` | int[64] | Session flags per car |

**Usage Example:**
```csharp
// Get data for car at index 5
int carIndex = 5;
int carLap = CarIdxLap[carIndex];
float carPosition = CarIdxLapDistPct[carIndex];
float carBestLap = CarIdxBestLapTime[carIndex];

// Find closest car ahead
float playerPos = LapDistPct;
float closestDist = float.MaxValue;
int closestIdx = -1;
for (int i = 0; i < 64; i++) {
    if (i == PlayerCarIdx) continue;
    float dist = CarIdxLapDistPct[i] - playerPos;
    if (dist > 0 && dist < closestDist) {
        closestDist = dist;
        closestIdx = i;
    }
}
```

---

## Pit & Strategy

### Pit Service
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `dpFuelFill` | float | liters | 0-120 | Fuel to add in pit stop |
| `dpFuelAddKg` | float | kg | 0-100 | Fuel to add (weight) |
| `dpFuelAutoFillEnabled` | bool | - | 0/1 | Auto-fill fuel enabled |
| `dpFuelAutoFillActive` | bool | - | 0/1 | Auto-fill currently active |
| `dpLFTireColdPress` | float | kPa | 100-300 | LF tire cold pressure |
| `dpRFTireColdPress` | float | kPa | 100-300 | RF tire cold pressure |
| `dpLRTireColdPress` | float | kPa | 100-300 | LR tire cold pressure |
| `dpRRTireColdPress` | float | kPa | 100-300 | RR tire cold pressure |
| `dpTireChange` | bool | - | 0/1 | Change tires in pit stop |
| `dpFastRepair` | float | seconds | 0-60 | Fast repair time |
| `FastRepairAvailable` | int | count | 0-3 | Fast repairs remaining |
| `FastRepairUsed` | int | count | 0-3 | Fast repairs used |

### Tire Sets
| Variable | Type | Units | Range | Notes |
|----------|------|-------|-------|-------|
| `FrontTireSetsAvailable` | int | sets | 0-20 | Front tire sets available |
| `FrontTireSetsUsed` | int | sets | 0-20 | Front tire sets used |
| `RearTireSetsAvailable` | int | sets | 0-20 | Rear tire sets available |
| `RearTireSetsUsed` | int | sets | 0-20 | Rear tire sets used |
| `LeftTireSetsAvailable` | int | sets | 0-20 | Left tire sets available |
| `LeftTireSetsUsed` | int | sets | 0-20 | Left tire sets used |
| `RightTireSetsAvailable` | int | sets | 0-20 | Right tire sets available |
| `RightTireSetsUsed` | int | sets | 0-20 | Right tire sets used |

---

## Session Info (YAML)
**Parsed once at connection, not real-time telemetry**

### Session Structure
```yaml
SessionInfo:
  Sessions:
    - SessionNum: 0
      SessionType: Practice | Qualify | Race
      SessionTime: "unlimited sec" | "30 min"
      SessionLaps: "unlimited" | "50 laps"
      
DriverInfo:
  DriverCarIdx: 0
  Drivers:
    - CarIdx: 0
      UserName: "Player Name"
      CarNumber: "1"
      CarClassShortName: "GT3"
      
WeekendInfo:
  TrackName: "Circuit de Spa-Francorchamps"
  TrackLength: "7.004 km"
  TrackDisplayName: "Spa"
  
SplitTimeInfo:
  Sectors:
    - SectorNum: 0
      SectorStartPct: 0.0
```

### Key YAML Values to Parse
- `TrackLength`: Track length in km (for distance calculations)
- `SessionTime`: Session duration
- `SessionLaps`: Race lap count
- `CarNumber`: Player's car number
- `CarClassShortName`: Car class name
- `DriverName`: Player username

---

## Derived Calculations

### 1. Fuel Strategy
```csharp
// Average fuel per lap
float fuelPerLap = TotalFuelUsed / LapsCompleted;

// Laps remaining on current fuel
float lapsRemaining = FuelLevel / fuelPerLap;

// Fuel needed for race
float fuelNeeded = (RaceLaps - CurrentLap) * fuelPerLap - FuelLevel;

// Pit stop required
bool needPitStop = fuelNeeded > 0;
```

### 2. Gap to Cars
```csharp
// Gap to car ahead (seconds)
float gapAhead = CarIdxF2Time[carAheadIdx] - CarIdxF2Time[PlayerCarIdx];

// Gap to leader (seconds)
float gapToLeader = CarIdxF2Time[PlayerCarIdx];

// Distance gap (meters)
float trackLength = 7004f;  // Spa example
float distGap = Math.Abs(CarIdxLapDistPct[carIdx] - LapDistPct) * trackLength;
```

### 3. Tire Degradation Rate
```csharp
// Wear per lap
float wearPerLap = CurrentTireWear / LapsOnTires;

// Laps until critical wear (90%)
float lapsUntilWorn = (0.9f - CurrentTireWear) / wearPerLap;
```

### 4. Track Position Relative
```csharp
// Distance to start/finish (meters)
float distToSF = (1f - LapDistPct) * trackLength;

// Is car in proximity zone?
float proximityMeters = 50f;  // ±50m
float carDist = Math.Abs(CarIdxLapDistPct[carIdx] - LapDistPct) * trackLength;
bool inProximity = carDist < proximityMeters;
```

### 5. Cornering G-Force (Total)
```csharp
// Total lateral G (magnitude)
float totalG = MathF.Sqrt((LatAccel * LatAccel) + (LongAccel * LongAccel)) / 9.81f;
```

### 6. Brake Bias Pressure Verification
```csharp
// Actual front/rear pressure ratio
float frontPressure = (LFbrakeLinePress + RFbrakeLinePress) / 2f;
float rearPressure = (LRbrakeLinePress + RRbrakeLinePress) / 2f;
float actualBiasRatio = frontPressure / (frontPressure + rearPressure);

// Compare to dcBrakeBias setting
float biasDifference = Math.Abs(actualBiasRatio - (dcBrakeBias / 100f));
bool biasMatchesSetup = biasDifference < 0.05f;  // Within 5%
```

---

## Variable Availability Matrix

| Variable | Always Available | Car-Specific | Session-Specific |
|----------|------------------|--------------|------------------|
| Speed, RPM, Gear | ✅ | - | - |
| Throttle, Brake, Clutch | ✅ | - | - |
| Tire temps, wear | ✅ | - | - |
| dcTractionControl | - | ✅ GT3/LMP/F1 | - |
| BrakeABSactive | - | ✅ GT3/LMP/F1 | - |
| P2P (Push-to-Pass) | - | ✅ IndyCar | - |
| Pit speed limiter | ✅ | - | - |
| Fast repair | - | - | ✅ Official races |
| LFspeed (wheel speed) | ❌ **NOT AVAILABLE** | - | - |

---

## Future Enhancements

### When Wheel Speeds Become Available
```csharp
// Wheel lock detection
bool wheelLocked = (LFspeed < 1.39f || RFspeed < 1.39f) &&  // < 5 km/h
                   Speed > 5.56f &&                          // > 20 km/h
                   Brake > 0.5f;                             // Heavy braking

// TC activation detection
float avgRearSpeed = (LRspeed + RRspeed) / 2f;
float slipRatio = (avgRearSpeed - Speed) / Speed;
bool tcActive = slipRatio > 0.05f && dcTractionControl > 0;

// Wheel spin detection (acceleration)
bool rearWheelSpin = avgRearSpeed > Speed * 1.05f;  // Rear 5% faster
```

### Recommended Additional Variables to Subscribe
```csharp
// When implementing new features, consider adding:
TelemetryVar.LFshockDefl,        // Suspension travel
TelemetryVar.RFshockDefl,
TelemetryVar.LRshockDefl,
TelemetryVar.RRshockDefl,
TelemetryVar.RaceDistance,       // Total race distance
TelemetryVar.SessionTimeRemain,  // Time remaining
TelemetryVar.VidCapEnabled,      // Is recording active
TelemetryVar.ReplayPlaySpeed,    // Replay speed multiplier
```

---

## References
- **Official SDK**: `irsdk_1_19/irsdk_defines.h`
- **NuGet Package**: `SVappsLAB.iRacingTelemetrySDK v1.0.0-beta.1`
- **Live Variable Dump**: `docs/iRacing_SDK_Variables_from-live-race.md`
- **SDK Documentation**: `docs/iRacing_SDK_Variables_Reference.md`
- **iRacing Forums**: https://forums.iracing.com/

---

**Next Steps:**
1. Implement `TelemetryVariableInfo.cs` class for runtime metadata access
2. Create unit conversion helpers in `UnitConversions.cs`
3. Build derived value calculators in `TelemetryCalculations.cs`
4. Add missing variables to `RequiredTelemetryVars` as needed
