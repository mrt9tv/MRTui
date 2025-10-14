# iRacing Session Info YAML Reference

**Document Version**: 1.0  
**Date**: October 13, 2025  
**iRacing Version**: 2025.09.24.01  
**Source**: Live session data from iRacing SDK

---

## 📋 Overview

The iRacing SDK provides a **SessionInfo YAML string** (~14KB) containing comprehensive race weekend, session, driver, and setup data. This document catalogs all available data fields and their practical uses for telemetry overlays.

**Access Method**:
```csharp
// Via iRacing SDK reflection
var sessionInfo = _client.GetType()
    .GetMethod("GetRawTelemetrySessionInfoYaml")
    ?.Invoke(_client, null) as string;
```

**Update Frequency**: 
- **Static fields** (track, car specs): Parse ONCE at session start
- **Semi-dynamic fields** (weather): Check every 5-30 seconds in dynamic weather
- **Dynamic fields** (incidents, results): Check every 1-5 seconds when needed

**⚠️ PERFORMANCE WARNING**: Do NOT parse the full 14KB YAML at 60 Hz! Use caching strategy below.

---

## 🏁 Top-Level Sections

The YAML is organized into 6 main sections:

1. **WeekendInfo** - Track, event, and weather data
2. **SessionInfo** - Current and upcoming session details
3. **CameraInfo** - Available camera groups and positions
4. **RadioInfo** - Radio frequencies and channels
5. **DriverInfo** - Your car setup and all drivers in session
6. **SplitTimeInfo** - Track sectors for timing
7. **CarSetup** - Your current car setup configuration

---

## 📊 Section 1: WeekendInfo

**Purpose**: Track characteristics, event details, weather conditions, and weekend configuration.

### Track Information

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| `TrackName` | string | `"lagunaseca"` | Internal track identifier |
| `TrackDisplayName` | string | `"WeatherTech Raceway Laguna Seca"` | **✅ Display in UI status bar** |
| `TrackDisplayShortName` | string | `"Laguna Seca"` | Compact UI display |
| `TrackID` | int | `47` | Track database ID |
| `TrackLength` | string | `"3.5652 km"` | Official track length |
| `TrackCity` | string | `"Salinas"` | Geographic location |
| `TrackState` | string | `"CA"` | State/province |
| `TrackCountry` | string | `"USA"` | Country |
| `TrackType` | string | `"road course"` | Track category |
| `TrackNumTurns` | int | `11` | Corner count |
| `TrackPitSpeedLimit` | string | `"55.98 kph"` | **⚠️ Pit lane speed warning** |
| `TrackDirection` | string | `"neutral"` | Layout direction |

### Weather Conditions

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| `TrackWeatherType` | string | `"Static"` | Weather mode |
| `TrackSkies` | string | `"Overcast"` | Cloud coverage |
| `TrackSurfaceTemp` | string | `"21.33 C"` | **🌡️ Track temp for tire strategy** |
| `TrackAirTemp` | string | `"18.33 C"` | Ambient temperature |
| `TrackAirPressure` | string | `"29.14 Hg"` | Barometric pressure |
| `TrackAirDensity` | string | `"1.18 kg/m^3"` | Air density (affects aero) |
| `TrackWindVel` | string | `"0.45 m/s"` | Wind speed |
| `TrackWindDir` | string | `"0.00 rad"` | Wind direction (radians) |
| `TrackRelativeHumidity` | string | `"0 %"` | Humidity |
| `TrackFogLevel` | string | `"0 %"` | Fog density |
| `TrackPrecipitation` | string | `"0 %"` | Rain intensity |

### Event Information

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| `EventType` | string | `"Test"` | Session type |
| `Category` | string | `"Road"` | Road/Oval/Dirt |
| `SeriesID` | int | `0` | Series identifier |
| `SeasonID` | int | `0` | Season identifier |
| `SessionID` | int | `0` | Unique session ID |
| `Official` | int | `0` | Official race (1) or practice (0) |
| `RaceWeek` | int | `0` | Current race week |
| `SimMode` | string | `"full"` | Simulation mode |

### Weekend Options

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| `NumStarters` | int | `0` | Grid size |
| `StartingGrid` | string | `"single file"` | Grid formation |
| `Restarts` | string | `"single file"` | Restart type |
| `StandingStart` | int | `0` | Rolling (0) or standing (1) |
| `TimeOfDay` | string | `"6:45 pm"` | Session time of day |
| `Date` | string | `"2025-05-15"` | Session date |
| `Unofficial` | int | `1` | Practice session flag |

---

## 🏎️ Section 2: SessionInfo

**Purpose**: Details about current and queued sessions.

### Current Session

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| `CurrentSessionNum` | int | `0` | Active session index |

### Session Array (Sessions[])

Each session in the weekend contains:

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| `SessionNum` | int | `0` | Session index |
| `SessionLaps` | string | `"unlimited"` | **🏁 Race length calculation** |
| `SessionTime` | string | `"unlimited"` | Time limit |
| `SessionType` | string | `"Offline Testing"` | Practice/Qualify/Race |
| `SessionTrackRubberState` | string | `"clean"` | Track grip level |
| `SessionNumLapsToAvg` | int | `0` | Laps for average calculation |

**Use Cases**:
- ✅ Determine if session is **lap-based or time-based** for fuel strategy
- ✅ Calculate total race laps for stint planning
- ✅ Show session type in UI (Practice, Qualifying, Race)

---

## 📷 Section 3: CameraInfo

**Purpose**: Available camera groups and positions.

### Camera Groups Array (Groups[])

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| `GroupNum` | int | `1` | Camera group ID |
| `GroupName` | string | `"Nose"` | Camera name |

### Cameras Array (Cameras[])

Each camera has:

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| `CameraNum` | int | `1` | Camera ID |
| `CameraName` | string | `"Cockpit"` | Display name |

**Use Cases**:
- 🎥 Automatic camera switching for replays
- 🎥 Camera position selection in overlay

---

## 📻 Section 4: RadioInfo

**Purpose**: Available radio channels and frequencies.

### Radios Array (Radios[])

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| `RadioNum` | int | `0` | Radio channel ID |
| `HopCount` | int | `1` | Network hops |
| `NumFrequencies` | int | `7` | Available frequencies |
| `TunedToFrequencyNum` | int | `0` | Active frequency |
| `ScanningIsOn` | int | `1` | Scan mode enabled |

**Use Cases**:
- 🔊 Voice comms integration
- 🔊 Team radio channel display

---

## 👤 Section 5: DriverInfo (MOST IMPORTANT)

**Purpose**: Your car setup specs and all drivers in the session.

### Your Driver Data

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| `DriverCarIdx` | int | `0` | **🔑 Your car index (player identification)** |
| `DriverUserID` | int | `914782` | Your user ID |
| `DriverIsAdmin` | int | `1` | Admin privileges |
| `DriverCarFuelMaxLtr` | float | `40.000` | **⛽ Tank capacity for fuel calculator** |
| `DriverCarMaxFuelPct` | float | `1.000` | Max fuel percentage |
| `DriverCarIdleRPM` | float | `1200.000` | Idle RPM |
| `DriverCarRedLine` | float | `7300.000` | **🔴 Shift light RPM** |
| `DriverCarSLFirstRPM` | float | `6300.000` | **🟡 Shift light start** |
| `DriverCarSLShiftRPM` | float | `6500.000` | **🟢 Optimal shift point** |
| `DriverCarSLLastRPM` | float | `6900.000` | Shift light end |
| `DriverCarSLBlinkRPM` | float | `7000.000` | **🔴 Blink shift light** |
| `DriverCarVersion` | string | `"2025.09.03.03"` | Car version |
| `DriverCarEstLapTime` | float | `79.4589` | **⏱️ Estimated lap time** |
| `DriverSetupName` | string | `"Garage 61 - MRT#9..."` | Current setup |
| `DriverSetupIsModified` | int | `1` | Setup changed flag |
| `DriverIncidentCount` | int | `31` | **⚠️ Incident points** |

### DriverTires Array

Tire compound information:

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| `TireIndex` | int | `0` | Tire set index |
| `TireCompoundType` | string | `"Hard"` | **🏁 Compound type for strategy** |

### Drivers Array (Drivers[]) - ALL DRIVERS IN SESSION

**⚠️ CRITICAL**: This array contains **every driver** in the session. Match `CarIdx` to `DriverCarIdx` to find **your data**.

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| `CarIdx` | int | `0` | **🔑 Car index (match to DriverCarIdx)** |
| `UserName` | string | `"Stefán Freyr Margrétarson"` | **✅ Driver name for status bar** |
| `UserID` | int | `914782` | User database ID |
| `TeamName` | string | `"Stefán Freyr..."` | Team name |
| `CarNumber` | string | `"9"` | **#️⃣ Car number for UI** |
| `CarNumberRaw` | int | `9` | Numeric car number |
| `CarPath` | string | `"formulair04"` | Car identifier |
| `CarScreenName` | string | `"FIA F4"` | **🏎️ Car name for display** |
| `CarScreenNameShort` | string | `"FIA F4"` | Compact car name |
| `CarClassShortName` | string | `""` | Class abbreviation |
| `IRating` | int | `1` | iRating |
| `LicLevel` | int | `1` | License class (1-20) |
| `LicSubLevel` | int | `1` | License safety rating |
| `LicString` | string | `"R 0.01"` | License display string |
| `IsSpectator` | int | `0` | Spectator flag |
| `CarDesignStr` | string | `"12,ffffff,..."` | Car paint scheme |
| `HelmetDesignStr` | string | `"64,ff8000,..."` | Helmet design |
| `SuitDesignStr` | string | `"22,008080,..."` | Suit design |

**How to Parse Driver Name**:
```csharp
// 1. Get your car index
int playerCarIdx = /* from DriverCarIdx field */;

// 2. Find matching driver in Drivers array
foreach (var driver in driversArray)
{
    if (driver.CarIdx == playerCarIdx)
    {
        string playerName = driver.UserName;  // ← YOUR NAME!
        string carNumber = driver.CarNumber;
        // ...
    }
}
```

---

## 🕐 Section 6: SplitTimeInfo

**Purpose**: Track sector definitions for timing.

### Sectors Array (Sectors[])

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| `SectorNum` | int | `0` | Sector number |
| `SectorStartPct` | float | `0.000000` | **⏱️ Sector timing boundaries** |

**Use Cases**:
- ⏱️ Sector timing displays
- ⏱️ Delta timing calculations
- ⏱️ Best sector tracking

---

## 🔧 Section 7: CarSetup

**Purpose**: Complete car setup configuration (suspension, aero, tires).

### TiresAero

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| `TireType → TireCompoundType` | string | `"Hard"` | Active compound |
| `LeftFront → ColdPressure` | string | `"138.0 kPa"` | **🔧 Tire pressure reference** |
| `LeftFront → TreadRemaining` | string | `"100 %"` | Tire wear baseline |

### Aero Settings

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| Various aero settings | | | Setup analysis tools |

### Chassis Settings

| Field | Type | Example | Use Case |
|-------|------|---------|----------|
| Front/Rear suspension | | | Setup comparison |

---

## 🎯 Common Use Cases

### 1. Driver Name in Status Bar ✅

**What You Need**:
- `DriverInfo → DriverCarIdx` (your car index)
- `DriverInfo → Drivers[CarIdx] → UserName`

**Implementation**:
```csharp
// Parse DriverInfo section
int playerCarIdx = /* from DriverCarIdx: 0 */;

// Parse Drivers array (indented under DriverInfo)
if (inDriversArray && currentDriverCarIdx == playerCarIdx)
{
    if (line.StartsWith("UserName:"))
    {
        string driverName = ExtractValue(line); // "Stefán Freyr Margrétarson"
    }
}
```

### 2. Track Name in Status Bar ✅

**What You Need**:
- `WeekendInfo → TrackDisplayName`

**Implementation**:
```csharp
// Parse WeekendInfo section
if (currentSection == "WeekendInfo")
{
    if (line.StartsWith("TrackDisplayName:"))
    {
        string trackName = ExtractValue(line); // "WeatherTech Raceway Laguna Seca"
    }
}
```

### 3. Fuel Tank Capacity ⛽

**What You Need**:
- `DriverInfo → DriverCarFuelMaxLtr`

**Implementation**:
```csharp
if (currentSection == "DriverInfo")
{
    if (line.StartsWith("DriverCarFuelMaxLtr:"))
    {
        double tankCapacity = double.Parse(ExtractValue(line)); // 40.0
    }
}
```

### 4. Shift Light Configuration 🚦

**What You Need**:
- `DriverCarRedLine` - Maximum RPM (7300)
- `DriverCarSLFirstRPM` - Start yellow (6300)
- `DriverCarSLShiftRPM` - Optimal shift (6500)
- `DriverCarSLBlinkRPM` - Blink red (7000)

### 5. Race Length (Fuel Strategy) 🏁

**What You Need**:
- `SessionInfo → Sessions[CurrentSessionNum] → SessionLaps`
- `SessionInfo → Sessions[CurrentSessionNum] → SessionTime`

**Logic**:
```csharp
if (sessionLaps == "unlimited")
{
    // Time-based race - calculate laps from session time
    totalLaps = sessionTimeSeconds / avgLapTime;
}
else
{
    // Lap-based race
    totalLaps = int.Parse(sessionLaps);
}
```

### 6. Car/Track Combo Display 🏎️

**What You Need**:
- `DriverInfo → Drivers[PlayerCarIdx] → CarScreenName` ("FIA F4")
- `WeekendInfo → TrackDisplayShortName` ("Laguna Seca")

**Display**: `"FIA F4 @ Laguna Seca"`

### 7. Tire Compound Strategy 🏁

**What You Need**:
- `DriverInfo → DriverTires[0] → TireCompoundType` ("Hard")
- Multiple tire types available for strategy planning

### 8. Estimated Lap Time ⏱️

**What You Need**:
- `DriverInfo → DriverCarEstLapTime` (79.4589 seconds)

**Use**: Initial lap time estimate before real laps completed

### 9. Incident Counter ⚠️

**What You Need**:
- `DriverInfo → DriverIncidentCount` (31)

**Display**: "Incidents: 31x" with color coding

### 10. Pit Speed Limit Warning ⚠️

**What You Need**:
- `WeekendInfo → TrackPitSpeedLimit` ("55.98 kph")

**Use**: Flash warning when approaching pit speed limit

---

## 🔍 YAML Structure & Parsing Tips

### Indentation Rules

```yaml
TopLevelSection:          ← No indent (currentSection)
 FieldName: value         ← 1 space (field of TopLevelSection)
 NestedSection:           ← 1 space (subsection, NOT a new currentSection!)
 - ArrayItem: value       ← Array entry (dash + space)
   ArrayField: value      ← Array item field
```

### Section Detection Logic

**✅ CORRECT** (only top-level sections):
```csharp
if (trimmed.EndsWith(':') && 
    !trimmed.StartsWith('-') && 
    !trimmed.Contains(' ') && 
    !line.StartsWith(' '))  // ← Must NOT be indented!
{
    currentSection = trimmed.TrimEnd(':');
}
```

**❌ WRONG** (treats nested subsections as top-level):
```csharp
if (trimmed.EndsWith(':'))  // ← Breaks on nested sections!
{
    currentSection = trimmed.TrimEnd(':');
}
```

### Array Detection

```csharp
// Detect array start
if (trimmed.StartsWith("Drivers:"))
{
    inDriversArray = true;
}

// Process array items
if (inDriversArray)
{
    if (trimmed.StartsWith("- CarIdx:"))
    {
        // New driver entry
    }
    else if (trimmed.StartsWith("UserName:"))
    {
        // Driver field
    }
}
```

### Value Extraction

```csharp
private string ExtractYamlValue(string line)
{
    var colonIndex = line.IndexOf(':');
    if (colonIndex < 0 || colonIndex == line.Length - 1)
        return string.Empty;
    
    return line.Substring(colonIndex + 1).Trim().Trim('"', '\'');
}
```

---

---

## ⚡ YAML Update Frequency & Caching Strategy

### Field Update Behaviors

#### 🟢 **Static Fields** (Read ONCE, Cache Forever)

**Update Frequency:** Parse at session connection only  
**Re-parse Trigger:** Session change, reconnection to iRacing  
**Performance Impact:** Zero (cached in memory)

**Fields:**
- **Track Info**: `TrackName`, `TrackDisplayName`, `TrackLength`, `TrackPitSpeedLimit`, `TrackNumTurns`
- **Car Specs**: `DriverCarFuelMaxLtr`, `DriverCarRedLine`, `DriverCarSL*RPM`, `DriverCarGearNumForward`
- **Driver Data**: `DriverCarIdx`, `Drivers[]` array (UserName, CarNumber, IRating)
- **Tire Compounds**: `DriverTires[]` array (available compounds)
- **Event Info**: `EventType`, `SeriesID`, `SessionID`, `RaceWeek`

**Implementation:**
```csharp
// Parse ONCE at connection
private void OnConnected()
{
    var yaml = GetRawTelemetrySessionInfoYaml();
    
    // Cache static fields forever
    _trackName = ParseField(yaml, "TrackDisplayName");
    _driverName = ParseField(yaml, "UserName");
    _tankCapacity = ParseField(yaml, "DriverCarFuelMaxLtr");
    _shiftRPM = ParseField(yaml, "DriverCarSLShiftRPM");
    // ... etc
    
    _staticDataCached = true;
}
```

---

#### 🟡 **Semi-Dynamic Fields** (Check Every 5-30 Seconds)

**Update Frequency:** Poll periodically based on session type  
**Re-parse Trigger:** Timer-based (only if needed)  
**Performance Impact:** Low (occasional small parses)

**Fields:**
- **Weather** (dynamic weather sessions ONLY):
  - `TrackSurfaceTemp` - Track temperature evolution
  - `TrackAirTemp` - Ambient temperature changes
  - `TrackWindVel`, `TrackWindDir` - Wind conditions
  - `TrackPrecipitation` - Rain intensity
- **Track State**: `SessionTrackRubberState` (clean → moderate → heavy)
- **Setup**: `DriverSetupIsModified` (if user changes setup mid-session)

**Implementation:**
```csharp
private Timer _weatherUpdateTimer;

private void InitializeWeatherMonitoring()
{
    // Only poll in dynamic weather sessions
    if (_weatherType == "Dynamic")
    {
        _weatherUpdateTimer = new Timer(30000); // 30 seconds
        _weatherUpdateTimer.Elapsed += (s, e) => UpdateWeatherData();
        _weatherUpdateTimer.Start();
    }
}

private void UpdateWeatherData()
{
    var yaml = GetRawTelemetrySessionInfoYaml();
    
    // Parse ONLY weather fields (fast)
    _trackSurfaceTemp = ParseField(yaml, "TrackSurfaceTemp");
    _trackAirTemp = ParseField(yaml, "TrackAirTemp");
    _windSpeed = ParseField(yaml, "TrackWindVel");
}
```

---

#### 🔴 **Dynamic Fields** (Check Every 1-5 Seconds)

**Update Frequency:** Poll when feature needs data  
**Re-parse Trigger:** User-triggered (e.g., incident counter display)  
**Performance Impact:** Medium (frequent small parses)

**Fields:**
- **Incidents**: `DriverIncidentCount` - Increases with each incident
- **Results**: `ResultsPositions[]`, `ResultsFastestLap`, `ResultsAverageLapTime`
- **Session Time**: `SessionTime` (remaining time in time-based races)
- **Session Flags**: Race state, caution flags

**Implementation:**
```csharp
private Timer _incidentCheckTimer;

private void InitializeIncidentMonitoring()
{
    _incidentCheckTimer = new Timer(5000); // 5 seconds
    _incidentCheckTimer.Elapsed += (s, e) => UpdateIncidentCount();
    _incidentCheckTimer.Start();
}

private void UpdateIncidentCount()
{
    var yaml = GetRawTelemetrySessionInfoYaml();
    
    // Parse ONLY incident field
    var newCount = ParseField(yaml, "DriverIncidentCount");
    
    if (newCount != _lastIncidentCount)
    {
        _lastIncidentCount = newCount;
        // Trigger UI update
    }
}
```

---

### 🚀 Optimal Caching Strategy

#### **Anti-Pattern ❌ (Current Implementation)**
```csharp
// BAD: Parsing 14KB YAML at 60 Hz = 840 KB/sec wasted!
private void UpdateTelemetry()
{
    // Called 60 times per second
    var sessionInfo = GetRawTelemetrySessionInfoYaml(); // 14,428 bytes
    ParseSessionInfo(sessionInfo); // Full YAML parse every frame
    
    // Re-extracts static fields like track name, car specs
    // even though they NEVER change!
}
```

**Problems:**
- ❌ Wastes CPU on redundant parsing (track name doesn't change!)
- ❌ Parses 14KB string 60 times/sec = 840 KB/sec throughput
- ❌ Re-creates same strings/objects every frame (GC pressure)
- ❌ Blocks telemetry thread with unnecessary work

---

#### **Best Practice ✅ (Three-Tier Caching)**

```csharp
public class IRacingTelemetryService
{
    // TIER 1: Static data (parse once, cache forever)
    private string _trackName;
    private string _driverName;
    private double _tankCapacity;
    private double _shiftRPM;
    private bool _staticDataCached = false;
    
    // TIER 2: Semi-dynamic data (check every 30 seconds)
    private double _trackSurfaceTemp;
    private double _trackAirTemp;
    private Timer _weatherTimer;
    
    // TIER 3: Dynamic data (check every 5 seconds)
    private int _incidentCount;
    private Timer _incidentTimer;
    
    private void OnConnected()
    {
        // Parse static fields ONCE
        ParseStaticFields();
        
        // Start semi-dynamic polling (if needed)
        if (_isDynamicWeather)
            StartWeatherMonitoring();
        
        // Start dynamic polling (if needed)
        if (_showIncidents)
            StartIncidentMonitoring();
    }
    
    private void ParseStaticFields()
    {
        if (_staticDataCached) return; // Already cached!
        
        var yaml = GetRawTelemetrySessionInfoYaml();
        
        // Extract WeekendInfo section once
        var weekendSection = ExtractSection(yaml, "WeekendInfo");
        _trackName = ParseField(weekendSection, "TrackDisplayName");
        _trackPitSpeed = ParseField(weekendSection, "TrackPitSpeedLimit");
        
        // Extract DriverInfo section once
        var driverSection = ExtractSection(yaml, "DriverInfo");
        _driverName = ParseDriverName(driverSection);
        _tankCapacity = ParseField(driverSection, "DriverCarFuelMaxLtr");
        _shiftRPM = ParseField(driverSection, "DriverCarSLShiftRPM");
        
        _staticDataCached = true;
        _logger.LogInformation("Static YAML fields cached (track: {Track}, driver: {Driver})", 
            _trackName, _driverName);
    }
    
    private void StartWeatherMonitoring()
    {
        _weatherTimer = new Timer(30000); // 30 seconds
        _weatherTimer.Elapsed += (s, e) => 
        {
            var yaml = GetRawTelemetrySessionInfoYaml();
            var weekendSection = ExtractSection(yaml, "WeekendInfo");
            
            _trackSurfaceTemp = ParseField(weekendSection, "TrackSurfaceTemp");
            _trackAirTemp = ParseField(weekendSection, "TrackAirTemp");
            
            // Notify UI if significant change
            if (Math.Abs(_trackSurfaceTemp - _lastSurfaceTemp) > 1.0)
            {
                WeatherChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        _weatherTimer.Start();
    }
    
    private void StartIncidentMonitoring()
    {
        _incidentTimer = new Timer(5000); // 5 seconds
        _incidentTimer.Elapsed += (s, e) =>
        {
            var yaml = GetRawTelemetrySessionInfoYaml();
            var driverSection = ExtractSection(yaml, "DriverInfo");
            
            var newCount = ParseField(driverSection, "DriverIncidentCount");
            if (newCount != _incidentCount)
            {
                _incidentCount = newCount;
                IncidentCountChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        _incidentTimer.Start();
    }
    
    // Telemetry updates run at 60 Hz - NO YAML PARSING HERE!
    private void UpdateTelemetry()
    {
        // Only update real-time telemetry (Speed, RPM, Fuel, etc.)
        // Use CACHED YAML fields (_trackName, _driverName, etc.)
        
        Speed = _sdk.GetValue<double>("Speed");
        RPM = _sdk.GetValue<double>("RPM");
        FuelLevel = _sdk.GetValue<double>("FuelLevel");
        
        // NO YAML PARSING - uses cached values
        TrackName = _trackName; // Already parsed once!
        DriverName = _driverName; // Already parsed once!
    }
}
```

---

### 📊 Performance Comparison

| Strategy | YAML Parses/Second | CPU Impact | Use Case |
|----------|-------------------|------------|----------|
| ❌ **No Caching** (current) | 60 (every frame) | **HIGH** 🔴 | Never use |
| ⚠️ **Partial Caching** | 1-5 (dynamic only) | **Medium** 🟡 | Acceptable |
| ✅ **Three-Tier Caching** | 0.03-0.2 (30s weather) | **LOW** 🟢 | Recommended |

**Example Savings:**
- No caching: 60 parses/sec × 14KB = **840 KB/sec**
- Three-tier: 0.2 parses/sec × 14KB = **2.8 KB/sec** (99.7% reduction!)

---

### 🎯 Implementation Checklist

- [ ] **Parse static fields ONCE** at connection (track, car, driver)
- [ ] **Cache all static values** in memory (no re-parsing)
- [ ] **Only poll weather** if session has dynamic weather enabled
- [ ] **Only poll incidents** if displaying incident counter
- [ ] **Never parse YAML at 60 Hz** in UpdateTelemetry()
- [ ] **Use timers** for semi-dynamic and dynamic checks
- [ ] **Add logging** to verify cache hits vs YAML reads

---

### 📊 Quick Reference: When to Parse Each Field

| Field Category | Parse Frequency | Trigger | Cache Strategy |
|----------------|----------------|---------|----------------|
| **Track Info** (TrackName, TrackLength, TrackPitSpeedLimit) | Once | Session start | Cache forever |
| **Car Specs** (FuelMaxLtr, RedLine, ShiftRPM) | Once | Session start | Cache forever |
| **Driver Data** (UserName, CarNumber, IRating) | Once | Session start | Cache forever |
| **Tire Compounds** (DriverTires array) | Once | Session start | Cache forever |
| **Weather** (TrackSurfaceTemp, TrackAirTemp) | Every 30s | Timer | Update cache |
| **Track State** (TrackRubberState) | Every 30s | Timer | Update cache |
| **Incidents** (DriverIncidentCount) | Every 5s | Timer | Update cache |
| **Results** (Positions, FastestLap) | Every 5s | Timer | Update cache |

**Key Insight**: ~95% of YAML fields are **static** and only need to be parsed ONCE! 🚀

---

## 📝 Best Practices

1. **✅ Cache Static YAML Data**: Parse static fields (track, car, driver) ONCE at session start - they NEVER change
2. **✅ Use Three-Tier Caching**: Static (parse once), Semi-dynamic (30s timer), Dynamic (5s timer)
3. **❌ Never Parse at 60 Hz**: Do NOT call `GetRawTelemetrySessionInfoYaml()` in your telemetry update loop
4. **✅ Conditional Polling**: Only poll weather if dynamic weather enabled, only poll incidents if displaying them
5. **✅ Use Fallbacks**: Check multiple field names (`TrackDisplayName` OR `TrackName`)
6. **✅ Handle Missing Fields**: Not all fields exist in all sessions
7. **✅ Match Player Data**: Always use `DriverCarIdx` to identify your car in arrays
8. **✅ Type Conversion**: Many fields are strings with units ("40.000", "3.5652 km") - use `TryParse`
9. **✅ Array Safety**: Check array indices before accessing
10. **✅ Indentation Matters**: Only unindented sections are top-level (use `!line.StartsWith(' ')`)
11. **✅ Unit Parsing**: Strip units from values ("55.98 kph" → 55.98)
12. **✅ Log Cache Hits**: Add logging to verify you're hitting cache, not re-parsing YAML

---

## 🚀 Future Enhancements

### Additional Fields to Parse

- **Lap-specific data**: Best lap times, sector times
- **Standings data**: Current position, gap to leader
- **Pit data**: Pit stop count, pit service time
- **Weather changes**: Dynamic weather progression
- **Multi-class data**: Class positions, class lap times

### Advanced Features

- **Opponent tracking**: Parse all drivers for relative display
- **Setup comparison**: Compare your setup to others
- **Track conditions**: Monitor rubber buildup, temperature changes
- **Strategy simulation**: Use session length + fuel data for pit strategy

---

## 📊 Quick Reference Table

| Data Category | Primary Fields | Use Case |
|--------------|----------------|----------|
| **Driver Identity** | `DriverInfo.Drivers[].UserName` | Status bar display |
| **Track Info** | `WeekendInfo.TrackDisplayName` | Status bar, session info |
| **Car Specs** | `DriverInfo.DriverCarFuelMaxLtr` | Fuel calculator |
| **Shift Points** | `DriverInfo.DriverCarSL*RPM` | Shift light logic |
| **Session Type** | `SessionInfo.Sessions[].SessionType` | UI mode selection |
| **Race Length** | `SessionInfo.Sessions[].SessionLaps` | Fuel strategy |
| **Tire Compound** | `DriverInfo.DriverTires[].TireCompoundType` | Strategy display |
| **Car Number** | `DriverInfo.Drivers[].CarNumber` | Position display |
| **Weather** | `WeekendInfo.Track*Temp/Wind*` | Strategy planning |
| **Pit Speed** | `WeekendInfo.TrackPitSpeedLimit` | Speed warnings |

---

## 🔗 Related Documentation

- **iRacing SDK Documentation**: Official API reference
- **YAML Parsing Implementation**: `src/iRacingOverlay.Core/Services/IRacingTelemetryService.cs`
- **TelemetryData Model**: `src/iRacingOverlay.Core/Models/TelemetryData.cs`

---

**Document Status**: ✅ Complete  
**Last Updated**: October 13, 2025  
**Author**: MRT Overlay Development Team
