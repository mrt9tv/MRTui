# Radar System Research - Car Proximity Detection

**Date:** October 15, 2025  
**Goal:** Implement RacelabsApp-style radar showing car positions (left, right, front, back) with color/animation  
**Status:** 🔬 Research Phase

---

## 🎯 Requirements

### Visual Design
- **4-way radar display**: Front, Back, Left, Right indicators
- **Color coding**: 
  - 🟢 Green: Safe distance (>3 car lengths)
  - 🟡 Yellow: Caution (1-3 car lengths)
  - 🔴 Red: Very close (<1 car length)
- **Animation**: Pulsing or flashing for very close cars
- **Distance indicators**: Show actual distance in meters or car lengths

### Functional Requirements
- Real-time update (25-60 Hz)
- Detect cars within ~20 meters (configurable)
- Calculate relative position (left/right/front/back)
- Account for player car orientation
- Work on all track types (road, oval, dirt)

---

## 📊 Available iRacing Telemetry Data

### 1. Player Car Data (Available ✅)

From our current telemetry implementation:

```csharp
[RequiredTelemetryVars([
    "Speed",           // m/s - Player speed
    "Lap",             // Current lap number
    "LapDistPct",      // 0-1 percentage around track
    "PlayerCarClassPosition", // Position in class
    // ... other fields
])]
```

**What We Have:**
- ✅ Player speed (m/s)
- ✅ Player lap distance (0-1 around track)
- ✅ Player position in race
- ❓ Player X, Y, Z coordinates (need to check)
- ❓ Player heading/yaw angle (need to check)

### 2. Other Cars Data (Need to Research 🔍)

**Critical telemetry arrays we need to find:**

| Telemetry Field | Purpose | Status |
|----------------|---------|--------|
| `CarIdxLap` | Array of lap numbers for all cars | 🔍 Need to verify |
| `CarIdxLapDistPct` | Array of track positions (0-1) for all cars | 🔍 Need to verify |
| `CarIdxTrackSurface` | Array indicating if car is on track/pit/off | 🔍 Need to verify |
| `CarIdxPosition` | Array of race positions | 🔍 Need to verify |
| `CarIdxEstTime` | Array of estimated lap times | 🔍 Need to verify |
| `CarIdxX`, `CarIdxY`, `CarIdxZ` | Arrays of world coordinates | 🔍 **CRITICAL** - Need to verify |
| `CarIdxHeading` / `CarIdxYaw` | Arrays of car headings | 🔍 **CRITICAL** - Need to verify |

### 3. Session Info (Available from YAML ✅)

From `DriverInfo` section:

```yaml
DriverInfo:
  DriverCarIdx: 0  # Our car index
  Drivers:
    - CarIdx: 0
      UserName: "Player Name"
      CarNumber: "9"
    - CarIdx: 1
      UserName: "Other Driver"
      CarNumber: "12"
    # ... all drivers in session
```

**What We Can Get:**
- ✅ Total number of cars in session (`Drivers.Length`)
- ✅ Car index for each driver (`CarIdx`)
- ✅ Driver names, car numbers
- ✅ Our player car index (`DriverCarIdx`)

---

## 🧮 Radar Calculation Methods

### Method 1: Track Position Based (Simple)

**If we only have `LapDistPct` for each car:**

```csharp
// Pseudo-code
float playerLapPct = telemetry.LapDistPct;  // 0.0 to 1.0

foreach (int carIdx in otherCars)
{
    float carLapPct = telemetry.CarIdxLapDistPct[carIdx];
    
    // Calculate track distance difference
    float deltaTrackPct = carLapPct - playerLapPct;
    
    // Handle wrap-around (e.g., player at 0.99, car at 0.01)
    if (deltaTrackPct > 0.5) deltaTrackPct -= 1.0f;
    if (deltaTrackPct < -0.5) deltaTrackPct += 1.0f;
    
    // Convert to meters (need track length)
    float trackLengthMeters = 3600; // Example: 3.6km track
    float distanceMeters = deltaTrackPct * trackLengthMeters;
    
    if (Math.Abs(distanceMeters) < 20) // Within 20 meters
    {
        if (distanceMeters > 0)
            radar.Front = true;  // Car ahead
        else
            radar.Back = true;   // Car behind
    }
}
```

**Limitations:**
- ❌ Can't detect left/right positioning
- ❌ Doesn't account for track width
- ❌ Poor for side-by-side racing
- ✅ Simple, reliable for oval racing

---

### Method 2: 3D World Coordinates (Accurate)

**If we have `CarIdxX`, `CarIdxY`, `CarIdxZ` and heading:**

```csharp
// Pseudo-code
Vector3 playerPos = new Vector3(
    telemetry.PlayerCarX,
    telemetry.PlayerCarY,
    telemetry.PlayerCarZ
);
float playerHeading = telemetry.PlayerCarYaw; // Radians

foreach (int carIdx in otherCars)
{
    Vector3 carPos = new Vector3(
        telemetry.CarIdxX[carIdx],
        telemetry.CarIdxY[carIdx],
        telemetry.CarIdxZ[carIdx]
    );
    
    // Calculate distance
    float distance = Vector3.Distance(playerPos, carPos);
    
    if (distance < 20.0f) // Within 20 meters
    {
        // Calculate relative angle
        float dx = carPos.X - playerPos.X;
        float dy = carPos.Y - playerPos.Y;
        float angleToTarget = Math.Atan2(dy, dx);
        
        // Convert to player-relative angle
        float relativeAngle = angleToTarget - playerHeading;
        
        // Normalize to -π to π
        while (relativeAngle > Math.PI) relativeAngle -= 2 * Math.PI;
        while (relativeAngle < -Math.PI) relativeAngle += 2 * Math.PI;
        
        // Classify position
        if (Math.Abs(relativeAngle) < Math.PI / 4) // ±45°
        {
            radar.Front = GetProximityLevel(distance);
        }
        else if (Math.Abs(relativeAngle) > 3 * Math.PI / 4)
        {
            radar.Back = GetProximityLevel(distance);
        }
        else if (relativeAngle > 0)
        {
            radar.Left = GetProximityLevel(distance);
        }
        else
        {
            radar.Right = GetProximityLevel(distance);
        }
    }
}

ProximityLevel GetProximityLevel(float distance)
{
    if (distance < 2.0f) return ProximityLevel.VeryClose;  // Red
    if (distance < 6.0f) return ProximityLevel.Close;      // Yellow
    return ProximityLevel.Safe;                            // Green
}
```

**Advantages:**
- ✅ Accurate left/right/front/back detection
- ✅ Works for all track types
- ✅ Handles side-by-side racing
- ✅ Real 3D distance calculation

**Limitations:**
- ❓ Need to verify iRacing exposes these arrays
- ⚠️ More computationally expensive
- ⚠️ May need optimization for 60 Hz updates

---

## 🔍 Next Steps: SDK Investigation

### 1. Check Available Telemetry Arrays

We need to modify `IRacingTelemetryService.cs` to request these arrays:

```csharp
[RequiredTelemetryVars([
    // Existing fields...
    "Speed", "RPM", "Gear", // ... etc
    
    // NEW: Car position arrays
    "CarIdxLap",            // int[] - Lap number for each car
    "CarIdxLapDistPct",     // float[] - Track position for each car
    "CarIdxTrackSurface",   // int[] - Track surface (-1=NotInWorld, 0=OffTrack, 1=InPitStall, 2=AproachingPits, 3=OnTrack)
    "CarIdxPosition",       // int[] - Position in race for each car
    
    // CRITICAL: 3D coordinates
    "CarIdxX",              // float[] - X coordinate for each car (meters)
    "CarIdxY",              // float[] - Y coordinate for each car (meters)
    "CarIdxZ",              // float[] - Z coordinate for each car (meters)
    "CarIdxYaw",            // float[] - Heading angle for each car (radians)
    
    // Player coordinates
    "YawNorth",             // Player car yaw relative to north (radians)
    "Yaw",                  // Player car yaw (radians)
    "Pitch",                // Player car pitch (radians)
    "Roll",                 // Player car roll (radians)
])]
```

### 2. Test Data Availability

**Create test widget to display:**
- Number of cars in session
- CarIdx values for nearest cars
- Distance values (if available)
- Coordinate values (if available)

### 3. Prototype Simple Radar

**Start with track position method:**
1. Get `CarIdxLapDistPct` array
2. Calculate distance differences
3. Display front/back indicators only
4. Add color coding

**Then upgrade to 3D if available:**
1. Test coordinate arrays exist
2. Implement relative angle calculation
3. Add left/right detection
4. Refine proximity zones

---

## 📚 Reference Implementation (RacelabsApp Style)

### Visual Layout

```
        [FRONT]
           🟢
           
   [LEFT]       [RIGHT]
     🔴           🟡
     
        [BACK]
         (empty)
```

### Distance Zones

| Zone | Distance | Color | Visual Effect |
|------|----------|-------|---------------|
| **Very Close** | 0-2m (< 1 car length) | 🔴 Red | Pulsing animation |
| **Close** | 2-6m (1-3 car lengths) | 🟡 Yellow | Solid indicator |
| **Near** | 6-20m (3-10 car lengths) | 🟢 Green | Dim indicator |
| **Far** | >20m | (hidden) | No display |

### Update Frequency

- **Radar scan:** 60 Hz (every frame)
- **Visual update:** 30 Hz (every other frame for smooth animation)
- **Color transitions:** Smooth interpolation over 100ms

---

## 🎨 UI Design Mockup

```
┌─────────────────────┐
│      RADAR          │
│                     │
│         ▲           │
│        🟢🟢         │
│      (2 cars)       │
│                     │
│   ◄ 🔴      🟡 ►   │
│  (1 car)  (1 car)  │
│                     │
│         ▼           │
│        ---          │
│      (clear)        │
└─────────────────────┘
```

**Features:**
- Minimalist design (small footprint)
- Clear directional arrows
- Car count per direction
- Optional: Show closest car's number

---

## ⚠️ Known Challenges

### 1. Array Size & Performance
- **Problem:** Processing 40-60 cars every frame
- **Solution:** 
  - Only process cars within max radar range
  - Use spatial partitioning
  - Update every 2-3 frames (30 Hz)

### 2. Track Coordinates
- **Problem:** Need to convert iRacing world coords to player-relative
- **Solution:**
  - Use player yaw for rotation matrix
  - Pre-calculate trigonometry values
  - Cache player position

### 3. Multiclass Racing
- **Problem:** Should we show all cars or only same class?
- **Solution:**
  - Configuration option: "Show all cars" vs "Same class only"
  - Use `CarIdxClass` to filter
  - Different colors for different classes

### 4. Lapped Cars
- **Problem:** Lap-based distance calculation may confuse lapped cars
- **Solution:**
  - Use `CarIdxLap` to account for lap difference
  - Prioritize cars on same lap
  - Show "1 lap ahead/behind" indicator

---

## 📝 Implementation Plan

### Phase 1: Research & Prototype (3-4 hours)
1. ✅ Document requirements and calculations
2. ⏳ Test telemetry array availability
3. ⏳ Create test widget to display raw data
4. ⏳ Verify coordinate systems work

### Phase 2: Basic Radar (4-6 hours)
1. ⏳ Implement track-position-based radar (front/back only)
2. ⏳ Add color coding (green/yellow/red)
3. ⏳ Create simple circular radar widget
4. ⏳ Test with multiple cars

### Phase 3: 3D Radar (6-8 hours)
1. ⏳ Implement 3D coordinate-based positioning
2. ⏳ Add left/right detection
3. ⏳ Refine proximity zones
4. ⏳ Add car count per direction

### Phase 4: Polish (4-6 hours)
1. ⏳ Add animations (pulsing for very close)
2. ⏳ Add configuration options
3. ⏳ Optimize performance
4. ⏳ Add car number/name display (optional)

**Total Estimate:** 17-24 hours

---

## 🔗 Resources

### iRacing SDK Documentation
- **TelemetryVariables**: Need to check official iRacing SDK docs for array fields
- **Coordinate System**: iRacing uses right-handed coordinate system (verify)
- **Units**: Distances in meters, angles in radians

### Similar Implementations
- **RacelabsApp**: Reference for visual design
- **CrewChief**: May have radar implementation
- **iOverlay**: Check for radar features

---

## 🚀 Next Action

**IMMEDIATE: Test telemetry array availability**

Run this test in telemetry service:

```csharp
// Add to RequiredTelemetryVars
"CarIdxLapDistPct",
"CarIdxX",
"CarIdxY", 
"CarIdxZ",
"CarIdxYaw",
"Yaw"

// Then in OnTelemetryUpdate, log the data:
_logger.LogInformation("CarIdxLapDistPct array length: {Length}", 
    sdkData.CarIdxLapDistPct?.Length ?? 0);
```

If arrays are available → **Proceed with implementation**  
If arrays are NOT available → **Need to research alternative approach**

---

**Status:** Awaiting telemetry array verification 🔍
