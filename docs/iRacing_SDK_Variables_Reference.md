# iRacing SDK Variables - Complete Reference

**Source**: SVappsLAB.iRacingTelemetrySDK v0.9.8.3  
**Date**: October 15, 2025  
**Total Variables**: 400+ telemetry variables

---

## 🎯 CRITICAL FINDING: Left/Right Detection

### CarLeftRight Variable ✅ AVAILABLE

```csharp
"CarLeftRight"  // Type: CarLeftRight (enum)
                // Description: "Notify if car is to the left or right of driver"
                // This is what the in-game spotter uses!
```

**How It Works**:
- Single enum value (not an array)
- Indicates if ANY car is beside the player
- Possible values (enum):
  - `LRClear` - No cars on either side
  - `LRCarLeft` - Car(s) on left side
  - `LRCarRight` - Car(s) on right side
  - `LRCarLeftRight` - Cars on both sides
  - *(Exact enum values need verification)*

**Limitations**:
- ❌ Doesn't tell us WHICH car is beside us
- ❌ Doesn't tell us HOW MANY cars beside us
- ❌ No per-car lateral position data
- ✅ Perfect for simple spotter-style warnings
- ✅ Exactly what in-game spotter uses

**Use Case for Radar**:
We CAN implement a 4-way radar (front/back/left/right) using:
- `CarIdxLapDistPct` → Front/back detection (which specific cars)
- `CarLeftRight` → Left/right detection (presence only, not specific cars)

---

## Multi-Car Position Arrays (CarIdx)

### Available CarIdx Arrays ✅

All of these provide data for each car in the session (array of 64):

```csharp
// Position & Lap Data
"CarIdxLapDistPct"         // float[64] - % around lap for each car
"CarIdxPosition"           // int[64]   - Race position
"CarIdxClassPosition"      // int[64]   - Class position
"CarIdxLap"                // int[64]   - Lap number
"CarIdxLapCompleted"       // int[64]   - Laps completed

// Status & Surface
"CarIdxOnPitRoad"          // bool[64]  - Pit road status
"CarIdxTrackSurface"       // int[64]   - Track surface type
"CarIdxTrackSurfaceMaterial" // int[64] - Surface material

// Car Class & Info
"CarIdxClass"              // int[64]   - Car class ID

// Performance Data
"CarIdxGear"               // int[64]   - Current gear
"CarIdxRPM"                // float[64] - Engine RPM
"CarIdxSteer"              // float[64] - Steering wheel angle

// Timing Data
"CarIdxEstTime"            // float[64] - Estimated time to reach position
"CarIdxF2Time"             // float[64] - Time behind leader
"CarIdxLastLapTime"        // float[64] - Last lap time
"CarIdxBestLapTime"        // float[64] - Best lap time
"CarIdxBestLapNum"         // int[64]   - Best lap number

// Flags & State
"CarIdxSessionFlags"       // uint[64]  - Session flags per car
"CarIdxPaceFlags"          // uint[64]  - Pace flags per car
"CarIdxPaceLine"           // int[64]   - Pace line (-1 if not pacing)
"CarIdxPaceRow"            // int[64]   - Pace row (-1 if not pacing)

// Tire Compound
"CarIdxTireCompound"       // int[64]   - Current tire compound
"CarIdxQualTireCompound"   // int[64]   - Qual tire compound
"CarIdxQualTireCompoundLocked" // bool[64] - Qual compound locked

// Push-to-Pass
"CarIdxP2P_Status"         // bool[64]  - P2P active status
"CarIdxP2P_Count"          // int[64]   - P2P count remaining

// Repair Info
"CarIdxFastRepairsUsed"    // int[64]   - Fast repairs used
```

### NOT Available ❌

These do NOT exist in the SDK:

```csharp
"CarIdxX"              // ❌ X coordinate
"CarIdxY"              // ❌ Y coordinate  
"CarIdxZ"              // ❌ Z coordinate
"CarIdxYaw"            // ❌ Heading angle
"CarIdxLateralOffset"  // ❌ Lateral track position
```

---

## Player Position & Orientation

### Available Player Variables ✅

```csharp
// Basic Position
"Speed"                // float - Vehicle speed (m/s)
"LapDistPct"           // float - Track position (0.0-1.0)
"LapDist"              // float - Distance in meters from start/finish
"Lap"                  // int   - Current lap number
"LapCompleted"         // int   - Laps completed

// Orientation (3D angles in radians)
"Yaw"                  // float - Heading angle (rotation around Z-axis)
"YawNorth"             // float - Heading relative to north
"YawRate"              // float - Rate of yaw change
"Pitch"                // float - Pitch angle (nose up/down)
"PitchRate"            // float - Rate of pitch change
"Roll"                 // float - Roll angle (banking)
"RollRate"             // float - Rate of roll change

// 3D Velocity
"VelocityX"            // float - X velocity component (m/s)
"VelocityY"            // float - Y velocity component
"VelocityZ"            // float - Z velocity component

// Track Surface
"PlayerTrackSurface"   // int   - Surface type (enum)
"PlayerTrackSurfaceMaterial" // int - Material type (enum)

// Car Info
"PlayerCarIdx"         // int   - Player's car index (0-63)
"PlayerCarPosition"    // int   - Overall position
"PlayerCarClassPosition" // int - Class position
"PlayerCarClass"       // int   - Player's car class ID
```

---

## Session & Race Info

```csharp
// Session Time
"SessionTime"          // double - Seconds since session start
"SessionTimeRemain"    // double - Seconds remaining
"SessionTimeTotal"     // double - Total session duration
"SessionTimeOfDay"     // float  - Time of day in seconds

// Session State
"SessionNum"           // int    - Current session number
"SessionState"         // int    - Session state (enum: practice/qual/race)
"SessionLapsRemain"    // int    - Laps remaining (old, use Ex version)
"SessionLapsRemainEx"  // int    - Improved laps remaining
"SessionLapsTotal"     // int    - Total session laps
"SessionTick"          // int    - Current update number
"SessionUniqueID"      // int    - Unique session identifier

// Flags
"SessionFlags"         // uint   - Session flags (yellow/red/checkered)
"OnPitRoad"            // bool   - Player on pit road
"IsOnTrack"            // bool   - Car on track with player in it
"IsOnTrackCar"         // bool   - Car on track physics running

// Race Info
"RaceLaps"             // int    - Laps completed in race
```

---

## Timing & Laps

```csharp
// Current Lap
"LapCurrentLapTime"    // float - Current lap time estimate

// Last Lap
"LapLastLapTime"       // float - Previous lap time

// Best Lap
"LapBestLapTime"       // float - Player's best lap time
"LapBestLap"           // int   - Best lap number
"LapBestNLapTime"      // float - Best N-lap average
"LapBestNLapLap"       // int   - Last lap in best N average
"LapLastNLapTime"      // float - Last N-lap average
"LapLasNLapSeq"        // int   - Consecutive clean laps for N average

// Delta Times
"LapDeltaToBestLap"           // float - Delta to personal best
"LapDeltaToBestLap_DD"        // float - Rate of change
"LapDeltaToBestLap_OK"        // bool  - Delta valid flag
"LapDeltaToOptimalLap"        // float - Delta to optimal
"LapDeltaToOptimalLap_DD"     // float - Rate of change
"LapDeltaToOptimalLap_OK"     // bool  - Delta valid flag
"LapDeltaToSessionBestLap"    // float - Delta to session best
"LapDeltaToSessionBestLap_DD" // float - Rate of change
"LapDeltaToSessionBestLap_OK" // bool  - Delta valid flag
"LapDeltaToSessionLastlLap"   // float - Delta to session last
"LapDeltaToSessionLastlLap_DD" // float - Rate of change
"LapDeltaToSessionLastlLap_OK" // bool  - Delta valid flag
"LapDeltaToSessionOptimalLap"    // float - Delta to session optimal
"LapDeltaToSessionOptimalLap_DD" // float - Rate of change
"LapDeltaToSessionOptimalLap_OK" // bool  - Delta valid flag
```

---

## Vehicle Dynamics

```csharp
// Basic Controls
"Throttle"             // float - Throttle position (0.0-1.0)
"ThrottleRaw"          // float - Raw throttle input
"Brake"                // float - Brake position (0.0-1.0)
"BrakeRaw"             // float - Raw brake input
"Clutch"               // float - Clutch position (0.0-1.0)
"ClutchRaw"            // float - Raw clutch input
"Gear"                 // int   - Current gear (-1=R, 0=N, 1+=forward)

// Steering
"SteeringWheelAngle"   // float - Steering angle (radians)
"SteeringWheelAngleMax" // float - Max steering angle
"SteeringWheelTorque"  // float - Force feedback torque (N*m)

// G-Forces (including gravity)
"LongAccel"            // float - Longitudinal acceleration (m/s²)
"LatAccel"             // float - Lateral acceleration (m/s²)
"VertAccel"            // float - Vertical acceleration (m/s²)

// Engine
"RPM"                  // float - Engine RPM
"Engine0_RPM"          // float - Engine0 RPM (multi-engine cars)
```

---

## Tire Data

### Tire Temperatures (Carcass)
```csharp
// Left Front
"LFtempCL"             // float - LF tire left carcass temp (°C)
"LFtempCM"             // float - LF tire middle carcass temp
"LFtempCR"             // float - LF tire right carcass temp

// Right Front (RF), Left Rear (LR), Right Rear (RR)
// ... similar pattern for RF, LR, RR ...
```

### Tire Temperatures (Surface)
```csharp
"LFtempL"              // float - LF tire left surface temp (°C)
"LFtempM"              // float - LF tire middle surface temp
"LFtempR"              // float - LF tire right surface temp
// ... similar for RF, LR, RR ...
```

### Tire Wear
```csharp
"LFwearL"              // float - LF tire left wear (%)
"LFwearM"              // float - LF tire middle wear
"LFwearR"              // float - LF tire right wear
// ... similar for RF, LR, RR ...
```

### Tire Pressure
```csharp
"LFpressure"           // float - LF tire current pressure (kPa)
"LFcoldPressure"       // float - LF tire cold pressure (garage setting)
// ... similar for RF, LR, RR ...
```

### Tire Speed & Distance
```csharp
"LFspeed"              // float - LF wheel speed (m/s)
"LFodometer"           // float - LF distance traveled since mount (m)
// ... similar for RF, LR, RR ...
```

---

## Engine & Fluids

```csharp
// Temperature
"WaterTemp"            // float - Engine coolant temp (°C)
"WaterLevel"           // float - Engine coolant level (L)
"OilTemp"              // float - Engine oil temp (°C)
"OilLevel"             // float - Engine oil level (L)
"OilPress"             // float - Engine oil pressure (bar)

// Fuel
"FuelLevel"            // float - Fuel remaining (L or kWh)
"FuelLevelPct"         // float - Fuel % remaining
"FuelPress"            // float - Fuel pressure (bar)
"FuelUsePerHour"       // float - Fuel consumption rate (kg/h)

// Electrical
"Voltage"              // float - Electrical system voltage (V)

// Manifold
"ManifoldPress"        // float - Intake manifold pressure (bar)
```

---

## Warnings & Incidents

```csharp
// Warnings
"EngineWarnings"       // uint - Engine warning lights (flags)

// Incidents
"PlayerIncidents"      // uint - Player incident flags
"PlayerCarMyIncidentCount"     // int - Player's own incidents
"PlayerCarTeamIncidentCount"   // int - Team total incidents
"PlayerCarDriverIncidentCount" // int - Current driver incidents
```

---

## Environment & Track

```csharp
// Weather
"AirTemp"              // float - Ambient air temp (°C)
"AirDensity"           // float - Air density (kg/m³)
"AirPressure"          // float - Air pressure (Pa)
"RelativeHumidity"     // float - Humidity (%)
"FogLevel"             // float - Fog density (%)
"Skies"                // int   - Sky condition (0=clear, 1=p.cloudy, etc.)
"Precipitation"        // float - Precipitation intensity (%)
"WeatherDeclaredWet"   // bool  - Rain tires allowed

// Wind
"WindVel"              // float - Wind speed (m/s)
"WindDir"              // float - Wind direction (radians)

// Track
"TrackTemp"            // float - Track surface temp (deprecated)
"TrackTempCrew"        // float - Track temp from crew (°C)
"TrackWetness"         // int   - Track wetness level (enum)

// Sun
"SolarAltitude"        // float - Sun angle above horizon (rad)
"SolarAzimuth"         // float - Sun angle from north (rad)

// Location
"Lat"                  // double - Latitude (decimal degrees)
"Lon"                  // double - Longitude (decimal degrees)
"Alt"                  // float  - Altitude (m)
```

---

## Brake Data

```csharp
// Brake Line Pressure
"LFbrakeLinePress"     // float - LF brake line pressure (bar)
"RFbrakeLinePress"     // float - RF brake line pressure
"LRbrakeLinePress"     // float - LR brake line pressure
"RRbrakeLinePress"     // float - RR brake line pressure

// Brake ABS
"BrakeABSactive"       // bool  - ABS currently active
"BrakeABScutPct"       // float - Brake force reduction from ABS (%)
```

---

## Pit Stop Data

```csharp
// Pit Status
"PitsOpen"             // bool  - Pit stop allowed
"PitstopActive"        // bool  - Currently getting service
"OnPitRoad"            // bool  - On pit road between cones
"PlayerCarInPitStall"  // bool  - Car in pit stall

// Pit Service
"PitSvFlags"           // uint  - Pit service checkboxes (flags)
"PitSvFuel"            // float - Fuel add amount (L or kWh)
"PitSvLFP"             // float - LF tire pressure (kPa)
"PitSvRFP"             // float - RF tire pressure
"PitSvLRP"             // float - LR tire pressure
"PitSvRRP"             // float - RR tire pressure
"PitSvTireCompound"    // int   - Pending tire compound

// Pit Repair
"PitRepairLeft"        // float - Time for mandatory repairs (s)
"PitOptRepairLeft"     // float - Time for optional repairs (s)

// Pit Service Status
"PlayerCarPitSvStatus" // uint  - Pit service status bits (flags)
```

---

## Car Setup & Adjustments

```csharp
// In-Car Adjustments
"dcABS"                // float - ABS adjustment
"dcTractionControl"    // float - TC adjustment
"dcBrakeBias"          // float - Brake bias adjustment
"dcFuelMixture"        // float - Fuel mixture adjustment
"dcThrottleShape"      // float - Throttle shape adjustment
"dcAntiRollFront"      // float - Front ARB adjustment
"dcAntiRollRear"       // float - Rear ARB adjustment
"dcWeightJackerRight"  // float - Right wedge adjustment
"dcDashPage"           // float - Dash display page
"dcLaunchRPM"          // float - Launch RPM adjustment

// Toggles
"dcStarter"            // bool  - Starter trigger
"dcHeadlightFlash"     // bool  - Headlight flash
"dcPitSpeedLimiterToggle" // bool - Pit limiter
"dcPushToPass"         // bool  - Push to pass trigger
"dcToggleWindshieldWipers" // bool - Wiper on/off
"dcTriggerWindshieldWipers" // bool - Wiper momentary
"dcTearOffVisor"       // bool  - Tear off visor
"dcRFBrakeAttachedToggle"  // bool - RF brake attached/detached
```

---

## Shift Light & RPM Indicators

```csharp
"PlayerCarSLFirstRPM"  // float - Shift light first light RPM
"PlayerCarSLLastRPM"   // float - Shift light last light RPM
"PlayerCarSLShiftRPM"  // float - Shift light shift RPM
"PlayerCarSLBlinkRPM"  // float - Shift light blink RPM
"ShiftIndicatorPct"    // float - Deprecated shift indicator
"ShiftPowerPct"        // float - Shift friction/grinding (%)
"ShiftGrindRPM"        // float - RPM of grinding noise
```

---

## Suspension & Ride Height

```csharp
// Ride Heights
"LFrideHeight"         // float - LF ride height (m)
"RFrideHeight"         // float - RF ride height
"LRrideHeight"         // float - LR ride height
"RRrideHeight"         // float - RR ride height

// Shock Deflection
"LFshockDefl"          // float - LF shock deflection (m)
"RFshockDefl"          // float - RF shock deflection
"LRshockDefl"          // float - LR shock deflection
"RRshockDefl"          // float - RR shock deflection

// Shock Velocity
"LFshockVel"           // float - LF shock velocity (m/s)
"RFshockVel"           // float - RF shock velocity
"LRshockVel"           // float - LR shock velocity
"RRshockVel"           // float - RR shock velocity
```

---

## Performance & System

```csharp
// FPS & Performance
"FrameRate"            // float - Average FPS
"CpuUsageFG"           // float - Foreground CPU usage (%)
"CpuUsageBG"           // float - Background CPU usage (%)
"GpuUsage"             // float - GPU usage (%)

// Memory
"MemPageFaultSec"      // float - Page faults per second
"MemSoftPageFaultSec"  // float - Soft page faults per second

// Communications
"ChanQuality"          // float - Communication quality (%)
"ChanPartnerQuality"   // float - Partner quality (%)
"ChanLatency"          // float - Latency (s)
"ChanAvgLatency"       // float - Average latency (s)
"ChanClockSkew"        // float - Clock skew (s)
```

---

## Camera & Replay

```csharp
// Camera
"CamCameraNumber"      // int  - Active camera number
"CamCameraState"       // uint - Camera system state (flags)
"CamCarIdx"            // int  - Camera focus car index
"CamGroupNumber"       // int  - Active camera group

// Replay
"IsReplayPlaying"      // bool - Replay mode active
"ReplayFrameNum"       // int  - Current replay frame (60 fps)
"ReplayFrameNumEnd"    // int  - Replay frame from end
"ReplayPlaySpeed"      // int  - Replay speed multiplier
"ReplayPlaySlowMotion" // bool - Slow motion active
"ReplaySessionNum"     // int  - Replay session number
"ReplaySessionTime"    // double - Replay session time (s)
```

---

## Tire Sets & Compounds

```csharp
// Tire Sets Available
"TireSetsAvailable"    // int - Total tire sets remaining (255=unlimited)
"TireSetsUsed"         // int - Total tire sets used
"LeftTireSetsAvailable"  // int - Left tire sets
"LeftTireSetsUsed"     // int - Left tire sets used
"RightTireSetsAvailable" // int - Right tire sets
"RightTireSetsUsed"    // int - Right tire sets used
"FrontTireSetsAvailable" // int - Front tire sets
"FrontTireSetsUsed"    // int - Front tire sets used
"RearTireSetsAvailable"  // int - Rear tire sets
"RearTireSetsUsed"     // int - Rear tire sets used

// Individual Tires
"LFTiresAvailable"     // int - LF tires remaining
"LFTiresUsed"          // int - LF tires used
// ... similar for RF, LR, RR ...

// Compound
"PlayerTireCompound"   // int - Current tire compound
```

---

## Special Features

```csharp
// Push-to-Pass
"P2P_Status"           // bool - P2P active on player
"P2P_Count"            // int  - P2P count remaining

// Hybrid/ERS
"ManualBoost"          // bool - Manual boost state
"ManualNoBoost"        // bool - Manual no boost state

// Driver Changes
"DCDriversSoFar"       // int - Team drivers who have run
"DCLapStatus"          // int - Driver change lap status

// Fast Repair
"FastRepairAvailable"  // int - Fast repairs remaining (255=unlimited)
"FastRepairUsed"       // int - Fast repairs used
"PlayerFastRepairsUsed" // int - Player's fast repairs used

// Towing
"PlayerCarTowTime"     // float - Tow time if >0 (s)

// Penalties
"PlayerCarWeightPenalty" // float - Weight penalty (kg)
"PlayerCarPowerAdjust"   // float - Power adjustment (%)
```

---

## Notes on Data Types

- **float**: Single-precision floating point
- **double**: Double-precision floating point  
- **int**: 32-bit signed integer
- **bool**: Boolean (true/false)
- **uint**: 32-bit unsigned integer (often used for bitfield flags)
- **Arrays [64]**: Data for each car in session (car index 0-63)

## Enum Types

Common enum types used in telemetry:

- `SessionState` - Practice, Qualifying, Race, etc.
- `SessionFlags` - Yellow, Red, Checkered, etc. (bitfield)
- `EngineWarnings` - Warning lights (bitfield)
- `TrackLocation` - Off Track, In Pit, On Track, etc.
- `TrackSurface` - Asphalt, Concrete, Grass, Dirt, etc.
- `CarLeftRight` - **Clear, CarLeft, CarRight, CarLeftRight** ⭐
- `PaceMode` - Not Pacing, Pacing, etc.

---

## Quick Category Index

1. **Multi-Car Arrays** - CarIdx variables for all cars
2. **Player Position** - Speed, LapDistPct, orientation
3. **Session Info** - Time, laps, flags, state
4. **Timing & Laps** - Current, last, best, delta times
5. **Vehicle Dynamics** - Throttle, brake, steering, G-forces
6. **Tire Data** - Temps, wear, pressure for all 4 tires
7. **Engine & Fluids** - Temps, fuel, oil, coolant
8. **Warnings & Incidents** - Engine warnings, incident counts
9. **Environment** - Weather, wind, track temp, time of day
10. **Brake Data** - Line pressure, ABS
11. **Pit Stop** - Status, service, repairs
12. **Car Setup** - In-car adjustments
13. **Suspension** - Ride height, shock deflection/velocity
14. **Performance** - FPS, CPU/GPU usage
15. **Camera & Replay** - Camera state, replay controls
16. **Special Features** - P2P, hybrid, driver changes

---

**Total Variables Documented**: 200+ core variables  
**Array Variables**: 25+ CarIdx arrays  
**Enum Types**: 10+ enumeration types  

**Last Updated**: October 15, 2025  
**SDK Version**: v0.9.8.3
