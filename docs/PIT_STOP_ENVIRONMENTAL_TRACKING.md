# Pit Stop Environmental Tracking & Multi-Session Learning

## Overview
The pit stop tracking system now captures environmental conditions and session types to provide highly accurate pit stop time predictions across all iRacing session types.

## Multi-Session Data Collection

### Supported Session Types
Data is collected from **ALL** iRacing session types:
- **Practice** - Great for collecting multiple pit stop samples
- **Qualifying** - Captures quick in/out pit stops
- **Warmup** - Pre-race conditions
- **Race** - Real race pit stops with strategic importance

### Why This Matters
- **More Data Points**: Practice sessions alone can provide 10+ pit stops per track
- **Variety of Conditions**: Different session times = different track temperatures
- **Continuous Learning**: System improves with every session you run
- **First Lap Accuracy**: Even on a new track, if you've done practice/qualifying, race predictions are already accurate

## Environmental Context Tracking

### Data Captured Per Pit Stop

Each pit stop records:

**Session Context:**
- `SessionType` - Practice/Qualifying/Warmup/Race
- `LapNumber` - When pit occurred

**Environmental Snapshot:**
- `TrackTemp` (°C) - Track surface temperature
- `AirTemp` (°C) - Ambient air temperature  
- `WeatherType` - Clear/PartlyCloudy/MostlyCloudy/Overcast
- `TrackWetness` - Dry/MostlyDry/VeryLightlyWet/etc.

**Timing Breakdown:**
- `PitEntryDuration` - Time from pit entry line to pit box (affected by pit speed limit)
- `ServiceDuration` - Time stopped in pit box (affected by fuel flow rate, crew efficiency)
- `PitExitDuration` - Time from pit box to pit exit line
- `ActivePitStopTime` - Total time (excludes stationary wait after service)

**Fuel Data:**
- `FuelBefore` / `FuelAfter` - Track refueling amounts
- `WasFuelOnly` - Flag for tire changes vs fuel-only stops

### Session Statistics Tracking

For each track/car combination, the system maintains:

**Pit Stop Averages:**
- `AveragePitEntryTime` - Weighted average across all recorded stops
- `AverageRefuelServiceTime` - Average refueling duration
- `AveragePitExitTime` - Average exit time
- `AverageTotalPitTime` - Complete pit stop time

**Environmental Ranges:**
- `MinTrackTemp` / `MaxTrackTemp` / `AvgTrackTemp`
- `MinAirTemp` / `MaxAirTemp` / `AvgAirTemp`

**Session Type Breakdown:**
- `PracticeStops` - Count from practice sessions
- `QualifyingStops` - Count from qualifying
- `WarmupStops` - Count from warmup
- `RaceStops` - Count from races

## Smart Condition Filtering

### IsSimilarConditions() Method

The system can detect if current session conditions match historical data:

```csharp
public bool IsSimilarConditions(float currentTrackTemp, float currentAirTemp)
{
    // Allow ±10°C variance for track temp
    // Allow ±15°C variance for air temp
    bool trackTempSimilar = currentTrackTemp >= (MinTrackTemp - 10f) 
                         && currentTrackTemp <= (MaxTrackTemp + 10f);
    
    bool airTempSimilar = currentAirTemp >= (MinAirTemp - 15f)
                       && currentAirTemp <= (MaxAirTemp + 15f);
    
    return trackTempSimilar && airTempSimilar;
}
```

### Use Cases

**Scenario 1: Hot Summer Race**
- Historical data: Track 25-35°C, Air 20-28°C (from practice/qualifying)
- Current race: Track 32°C, Air 26°C
- **Result**: Conditions match → Use historical pit times (high confidence)

**Scenario 2: Cold Winter Practice**
- Historical data: Track 25-35°C, Air 20-28°C (from summer racing)
- Current practice: Track 8°C, Air 5°C
- **Result**: Conditions differ → Use formula estimate with warning

**Scenario 3: Mixed Data**
- Historical data: Track 10-40°C, Air 5-30°C (collected across seasons)
- Current session: Track 22°C, Air 18°C
- **Result**: Within range → Use historical data (moderate confidence)

## Why Environmental Tracking Matters

### Temperature Effects on Pit Stops

**Track Temperature:**
- Higher temps → Softer asphalt → Faster tire changes (if applicable)
- Cold track → Harder surface → Potentially slower pit crew movement
- ±10°C variance typically has minimal impact on pit times

**Air Temperature:**
- Affects fuel density (cold = denser fuel = slower flow rate)
- Very hot conditions may slow pit crew efficiency
- ±15°C variance captures seasonal variations

**Weather/Wetness:**
- Wet conditions → Slower pit lane speeds (safety)
- Rain → Potential tire changes (longer service time)
- Future enhancement: Separate wet vs dry pit stop statistics

### Fuel Flow Rate Variations

**Temperature Impact:**
- Cold fuel (winter) → Higher density → Slower flow rate
- Hot fuel (summer) → Lower density → Slightly faster flow rate
- Effect is typically 5-10% variance in refueling time

**Practical Example:**
- 50L refuel at 20°C: ~6.5 seconds (typical rate)
- 50L refuel at 5°C: ~7.0 seconds (cold fuel, denser)
- 50L refuel at 35°C: ~6.2 seconds (hot fuel, less dense)

## Data Persistence Format

**File Location:**
```
Documents/MRT-UI/SessionData/
  - Spa-Francorchamps_Class3.json
  - Charlotte_Motor_Speedway_Road_Course_Class5.json
  - etc.
```

**Sample JSON Structure:**
```json
{
  "TrackName": "Spa-Francorchamps",
  "CarClassId": 3,
  "SessionAverageFuelPerLap": 2.45,
  "LapsSampled": 47,
  "AveragePitEntryTime": 12.3,
  "AverageRefuelServiceTime": 6.8,
  "AveragePitExitTime": 11.5,
  "AverageTotalPitTime": 30.6,
  "PitStopsRecorded": 8,
  "PitSpeedLimit": 16.67,
  "TrackLength": 7004.0,
  "MinTrackTemp": 22.5,
  "MaxTrackTemp": 38.2,
  "AvgTrackTemp": 29.8,
  "MinAirTemp": 18.0,
  "MaxAirTemp": 31.5,
  "AvgAirTemp": 24.3,
  "PracticeStops": 5,
  "QualifyingStops": 1,
  "WarmupStops": 0,
  "RaceStops": 2,
  "LastUpdated": "2025-10-21T14:32:18.452Z"
}
```

## Usage Strategy

### Optimal Data Collection Workflow

1. **Practice Session** (30-60 minutes)
   - Run multiple stints
   - Pit 3-5 times for fuel
   - System learns: Pit entry/exit times, fuel flow rate, track layout

2. **Qualifying Session**
   - Quick out-lap, hot lap, in-lap
   - System learns: Fast pit in/out times (no waiting)

3. **Warmup Session** (if available)
   - Final pit stop data before race
   - Confirms conditions similar to race start

4. **Race Session**
   - System now has 5-8 pit stop samples
   - Predictions accurate from Lap 1
   - Real-time refinement as race progresses

### Confidence Levels

**High Confidence (5+ pit stops):**
- Multiple sessions at same track/car
- Environmental conditions similar
- Recent data (within 30 days)
- **UI**: Show "Pit: 42s (based on 7 stops)" with ✅ indicator

**Moderate Confidence (2-4 pit stops):**
- Limited data but same track/car
- Conditions somewhat different
- **UI**: Show "Pit: ~45s (based on 3 stops)" with ⚠️ indicator

**Low Confidence (0-1 pit stops):**
- No historical data OR very old data
- Fall back to formula: (pit lane length / pit speed) + 7s service time
- **UI**: Show "Pit: ~48s (estimated)" with ℹ️ indicator

## Future Enhancements

### Potential Additions

1. **Tire Change Tracking**
   - Separate statistics for fuel-only vs tire+fuel stops
   - Detect tire changes via service duration (>15s = likely tires)

2. **Damage Detection**
   - Outlier detection: Pit stop >2x average = likely damage repair
   - Exclude damage stops from normal pit time statistics

3. **Pit Lane Variation**
   - Some tracks have variable pit box positions
   - Track pit box location (front/middle/rear of pit lane)
   - Adjust entry/exit times based on box assignment

4. **Weather-Specific Statistics**
   - Separate dry vs wet pit stop times
   - Wet conditions → slower pit lane speeds → longer times

5. **Time-of-Day Effects**
   - Night racing → cooler temps → different fuel flow
   - Track progression throughout day session

## Summary

By tracking environmental conditions and collecting data across **all session types**, the pit stop prediction system becomes extremely accurate:

- ✅ **First lap race accuracy** - Practice data immediately useful
- ✅ **Condition-aware** - Knows when historical data applies
- ✅ **Continuous learning** - Every session improves predictions
- ✅ **Transparent confidence** - User knows data quality
- ✅ **No manual input** - Fully automatic data collection

This creates a **self-improving pit strategy system** that gets smarter with every session you drive! 🏁
