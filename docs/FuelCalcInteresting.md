# Advanced Fuel Calculation & Race Strategy Guide

**Document Generated**: October 19, 2025
**Purpose**: Comprehensive fuel calculation strategies, pit stop analysis, and race position predictions
**Status**: Implementation Guide with Available SDK Variables

---

## Table of Contents

1. [Overview](#overview)
2. [Available SDK Variables](#available-sdk-variables)
3. [Fuel Calculation Summaries](#fuel-calculation-summaries)
4. [Pit Stop Time Estimation](#pit-stop-time-estimation)
5. [Position Prediction on Pit Stop](#position-prediction-on-pit-stop)
6. [Advanced Strategies](#advanced-strategies)
7. [Implementation Recommendations](#implementation-recommendations)

---

## Overview

### Current Implementation Status
- ✅ Lap-by-lap fuel consumption tracking
- ✅ Multiple averaging methods (Last, Last 5, Last 10, Session)
- ✅ Green flag vs. yellow flag fuel usage separation
- ✅ Basic pit detection
- ❌ Pit stop time prediction
- ❌ Position prediction during pit stop
- ❌ Comparison vs. iRacing fuel calculator
- ❌ Advanced anomaly detection

### Key Metrics Available
From `available_variables.txt` (326 total SDK variables):
- **4 core fuel variables**: FuelLevel, FuelLevelPct, FuelPress, FuelUsePerHour
- **6 pit service variables**: PitSvFuel, PitSvLFP, PitSvRFP, PitSvLRP, PitSvRRP, PitSvTireCompound
- **7 pit status variables**: PitsOpen, PitstopActive, PlayerCarInPitStall, PitRepairLeft, PitOptRepairLeft, PlayerCarPitSvStatus, PitSvFlags
- **Multi-car arrays (64 cars)**: CarIdxLapDistPct, CarIdxPosition, CarIdxLastLapTime, CarIdxF2Time, etc.

---

## Available SDK Variables

### Fuel-Related Variables (7 total)
```
✅ FuelLevel              - Current fuel in tank (L or kWh)
✅ FuelLevelPct           - Fuel percentage (0-1)
✅ FuelPress              - Fuel line pressure (bar)
✅ FuelUsePerHour         - Consumption rate (kg/h) - iRacing's estimate!
❌ SessionLapsRemainEx    - iRacing's laps remaining estimate (NOT CAPTURED)
```

### Pit Stop Service Variables (8 total)
```
✅ PitSvFuel              - Fuel amount set to add (L or kWh)
✅ PitSvLFP               - Left Front tire pressure setting (kPa)
✅ PitSvRFP               - Right Front tire pressure setting
✅ PitSvLRP               - Left Rear tire pressure setting
✅ PitSvRRP               - Right Rear tire pressure setting
✅ PitSvTireCompound      - Pending tire compound enum
✅ PitSvFlags             - Service flags bitfield (what's being done)
```

### Pit Stop Status Variables (6 total)
```
✅ PitsOpen               - Pit stops allowed (bool)
✅ PitstopActive          - Currently receiving service (bool)
✅ PlayerCarInPitStall    - Car in pit stall (bool) - MORE PRECISE than OnPitRoad
✅ PitRepairLeft          - Mandatory repair time remaining (seconds)
✅ PitOptRepairLeft       - Optional repair time remaining (seconds)
✅ PlayerCarPitSvStatus   - Pit service status bitfield (flags)
```

### Session/Race Variables (15 total) - CRITICAL FOR PREDICTIONS
```
✅ SessionLapsRemain      - Laps remaining (old version)
✅ SessionLapsRemainEx    - Improved laps remaining (NEW - use this!)
✅ SessionLapsTotal       - Total laps in session
✅ RaceLaps               - Laps completed by player in race
✅ Lap                    - Current lap number
✅ LapCompleted           - Laps completed (another tracking method)
✅ SessionTime            - Elapsed time (seconds)
✅ SessionTimeRemain      - Time remaining (seconds)
✅ SessionTimeTotal       - Total session duration
✅ SessionFlags           - Yellow, red, checkered, etc.
✅ SessionState           - Racing, checkered, cooldown, etc.
✅ SessionNum             - Current session number
✅ SessionUniqueID        - Unique identifier (detect session changes!)
```

### Multi-Car Position Arrays (14 total) - FOR POSITION PREDICTION
```
✅ CarIdxLapDistPct       - Track position % for all 64 cars [64]
✅ CarIdxPosition         - Overall position for all 64 cars [64]
✅ CarIdxClassPosition    - Class position for all 64 cars [64]
✅ CarIdxLap              - Lap number for all 64 cars [64]
✅ CarIdxLapCompleted     - Laps completed for all 64 cars [64]
✅ CarIdxLastLapTime      - Last lap time for all 64 cars [64]
✅ CarIdxBestLapTime      - Best lap time for all 64 cars [64]
✅ CarIdxEstTime          - Estimated time to reach position [64]
✅ CarIdxF2Time           - Time behind leader for all 64 cars [64]
✅ CarIdxOnPitRoad        - Pit status for all 64 cars [64]
✅ CarIdxClass            - Car class for all 64 cars [64]
✅ CarIdxGear             - Current gear for all 64 cars [64]
✅ CarIdxRPM              - Engine RPM for all 64 cars [64]
```

### Timing Variables (12 total) - FOR LAP TIME DELTAS
```
✅ LapLastLapTime         - Player's last lap time
✅ LapBestLapTime         - Player's best lap time
✅ LapCurrentLapTime      - Player's current lap time (running)
✅ LapDeltaToBestLap      - Delta to personal best
✅ LapDeltaToBestLap_DD   - Delta-delta (rate of change)
✅ LapDeltaToSessionBestLap     - Delta to session best
✅ LapDeltaToSessionBestLap_DD  - Delta-delta to session best
✅ LapBestLap             - Lap number of best lap
✅ LapBestNLapTime        - Best N-lap average
✅ LapBestNLapLap         - Last lap in best N-lap average
✅ LapLastNLapTime        - Last N-lap average
```

### Environment & Tire Variables - FOR FUEL IMPACT ANALYSIS
```
✅ AirTemp                - Air temperature (°C)
✅ AirDensity             - Air density (kg/m³)
✅ AirPressure            - Atmospheric pressure (hPa)
✅ TrackTemp              - Track temperature (°C)
✅ TrackTempCrew          - Track temp from crew (°C)
✅ PlayerTireCompound     - Current tire compound
✅ CarIdxTireCompound     - Tire compound for each car [64]
✅ WeatherDeclaredWet     - Rain tires allowed (bool)
```

---

## Fuel Calculation Summaries

### Strategy 1: Compare Your Calculator vs. iRacing's Calculator

**Objective**: Validate your fuel calculations against iRacing's built-in estimate.

**SDK Variables Needed**:
- `FuelUsePerHour` - iRacing's estimated consumption rate
- `SessionLapsRemainEx` - iRacing's laps remaining estimate
- `LapLastLapTime` - Latest lap time for conversion

**Implementation**:
```csharp
// Convert iRacing's kg/h to liters per lap
float fuelUsePerHourKg = telemetry.FuelUsePerHour; // kg/h
float lapTimeSeconds = telemetry.LapLastLapTime;
float lapTimeHours = lapTimeSeconds / 3600.0f;
float iRacingFuelPerLap = (fuelUsePerHourKg * lapTimeHours) / carSpecificDensity;

// Compare with our calculation
float ourAvgFuel = CurrentData.AvgFuelPerLap_L5;
float calculationDifference = ourAvgFuel - iRacingFuelPerLap;
float percentDifference = Math.Abs(calculationDifference / iRacingFuelPerLap) * 100;

// Alert if significant discrepancy
if (percentDifference > 10.0f)
{
    // "⚠️ Fuel calculation mismatch: We calc {ourAvgFuel:F2}L/lap,
    //   iRacing estimates {iRacingFuelPerLap:F2}L/lap ({percentDifference:F1}% difference)"
}

// Compare laps remaining
float ourLapsRemaining = CurrentData.CurrentFuel / ourAvgFuel;
float iRacingLapsRemaining = telemetry.SessionLapsRemainEx;
float lapsDifference = ourLapsRemaining - iRacingLapsRemaining;

if (Math.Abs(lapsDifference) > 2.0f)
{
    // Major discrepancy - investigate cause
    // Possible reasons:
    // - Different averaging method (session vs. recent laps)
    // - Fuel mixture adjustment affecting consumption
    // - Environmental factors (air density, temp)
    // - Traffic/drafting impacts
}
```

**Use Cases**:
- ✅ Validate calculation accuracy
- ✅ Detect when iRacing's estimate is unreliable
- ✅ Flag anomalies: "Our calc says 15 laps, iRacing says 12"
- ✅ Learn which averaging method iRacing uses

---

### Strategy 2: Detect Fuel Usage that "Does Not Make Sense"

**Objective**: Identify anomalous fuel consumption patterns that indicate problems.

#### A. Statistical Outlier Detection
```csharp
public class AnomalyDetector
{
    public bool DetectOutlierLap(FuelLapHistory lap, List<FuelLapHistory> history)
    {
        var validLaps = history.Where(l => l.IsValidForAveraging).ToList();

        if (validLaps.Count < 3) return false; // Need baseline

        float mean = validLaps.Average(l => l.FuelUsed);
        float variance = validLaps.Average(l => Math.Pow(l.FuelUsed - mean, 2));
        float stdDev = (float)Math.Sqrt(variance);

        float deviation = Math.Abs(lap.FuelUsed - mean);
        bool isOutlier = deviation > 2.0f * stdDev; // 2σ rule

        if (isOutlier)
        {
            float percentDev = (deviation / mean) * 100;
            Console.WriteLine($"⚠️ Lap {lap.LapNumber} outlier: {lap.FuelUsed:F2}L " +
                            $"({percentDev:F1}% from mean)");
        }

        return isOutlier;
    }
}
```

#### B. Context-Aware Validation
```csharp
public bool IsUsageImpossible(FuelLapHistory lap, TelemetryData telemetry)
{
    // 1. Used more fuel than physically possible
    if (lap.FuelUsed > telemetry.FuelLevelMax)
        return true; // "Used more than tank capacity!"

    // 2. Negative fuel usage (refuel detected mid-lap)
    if (lap.FuelUsed < 0)
        return true; // "Refueled during lap - exclude"

    // 3. Used >50% more than worst case recorded
    float maxEverSeen = _lapHistory.Max(l => l.FuelUsed);
    if (lap.FuelUsed > maxEverSeen * 1.5f)
        return true; // "Unprecedented fuel usage"

    // 4. Lap time suggests crash/off-track
    float avgLapTime = _lapHistory.Average(l => l.LapTime);
    if (lap.LapTime > avgLapTime * 1.5f && lap.FuelUsed > avgLapTime * 1.2f)
        return true; // "Likely crashed - high time + high fuel"

    // 5. Fuel used inconsistent with throttle input
    // (requires tracking throttle percentage during lap)
    float expectedFuel = CalculateExpectedFuel(lap.AvgThrottle);
    if (Math.Abs(lap.FuelUsed - expectedFuel) > 0.5f)
        return true; // "Unusual fuel/throttle relationship"

    return false;
}
```

#### C. Driving Style Correlation
```csharp
public string AnalyzeFuelAnomalyReason(FuelLapHistory lap, LapDrivingData driving)
{
    // Correlate high fuel usage with driving inputs
    string reason = "Normal";

    if (lap.FuelUsed > mean + 1.5f * stdDev)
    {
        if (driving.TimeFullThrottle > avgTimeFullThrottle * 1.3f)
            reason = "Aggressive driving (full throttle)";
        else if (driving.AvgRPM > avgRPM * 1.1f)
            reason = "High RPM running";
        else if (driving.MaxSpeed > maxSpeed * 1.05f)
            reason = "Slipstreaming competitor";
        else if (lap.LapTime < avgLapTime * 0.95f)
            reason = "Track evolution (going faster)";
        else if (driving.ShiftCount > avgShiftCount * 1.2f)
            reason = "Excessive gear changes";
        else
            reason = "Unknown - investigate";
    }

    return reason;
}
```

---

### Strategy 3: Safety Car / Yellow Flag Fuel Usage

**Objective**: Track fuel usage separately under caution vs. green flag racing.

**Already Implemented** in `FuelCalculatorService.cs` (lines 175-183):
```csharp
// Green flag average
var greenLaps = validLaps.Where(l => l.IsGreenFlagLap).ToList();
CurrentData.GreenFlagLapCount = greenLaps.Count;
CurrentData.GreenFlagAverage = greenLaps.Count > 0 ? greenLaps.Average(l => l.FuelUsed) : 0;

// Yellow flag average
var yellowLaps = validLaps.Where(l => l.IsYellowFlagLap).ToList();
CurrentData.YellowFlagLapCount = yellowLaps.Count;
CurrentData.YellowFlagAverage = yellowLaps.Count > 0 ? yellowLaps.Average(l => l.FuelUsed) : 0;
```

**Enhanced Strategy**:
```csharp
public class SafetyCarStrategy
{
    public void PredictFuelUnderGreen(TelemetryData telemetry)
    {
        if (!CurrentData.IsUnderYellow)
            return;

        // Current situation (under yellow)
        float yellowLapsRemaining = CurrentData.CurrentFuel / CurrentData.YellowFlagAverage;

        // Projected under green flag racing
        float greenLapsRemaining = CurrentData.CurrentFuel / CurrentData.GreenFlagAverage;

        // Strategy alert
        string alert = $"Under YELLOW: {yellowLapsRemaining:F1} laps possible | " +
                      $"Under GREEN: {greenLapsRemaining:F1} laps only";

        float lapsToGo = CurrentData.RaceLapsRemaining;

        // Predict if caution ends on lap X
        int assumeYellowLaps = 3; // Conservative: assume 3 more yellow laps
        float fuelAfterYellow = CurrentData.CurrentFuel - (assumeYellowLaps * CurrentData.YellowFlagAverage);
        float greenLapsAfterYellow = fuelAfterYellow / CurrentData.GreenFlagAverage;

        bool canFinish = (assumeYellowLaps + greenLapsAfterYellow) >= lapsToGo;

        if (!canFinish)
        {
            // Need to pit before green resumes
            Console.WriteLine("⚠️ CAUTION: Will need fuel stop if caution ends on lap {Lap}");
        }

        return alert;
    }
}
```

---

### Strategy 4: Fuel Load Effect on Lap Time

**Objective**: Quantify performance impact of carrying fuel vs. running light.

**SDK Variables**:
- `Speed` - Detect correlation between fuel load and lap pace
- `LapCurrentLapTime`, `LapLastLapTime` - Lap time tracking
- Track fuel level at lap start

**Implementation**:
```csharp
public class FuelLoadAnalysis
{
    public float LapTimePerLiter { get; set; } // How much time each liter costs

    public void Analyze(List<FuelLapHistory> history)
    {
        // Group laps by fuel load
        var fullFuelLaps = history
            .Where(l => l.FuelAtStart > tankCapacity * 0.8f)
            .ToList();

        var lowFuelLaps = history
            .Where(l => l.FuelAtStart < tankCapacity * 0.3f)
            .ToList();

        if (fullFuelLaps.Count < 2 || lowFuelLaps.Count < 2)
            return; // Insufficient data

        float fullFuelAvgTime = fullFuelLaps.Average(l => l.LapTime);
        float lowFuelAvgTime = lowFuelLaps.Average(l => l.LapTime);
        float fuelDifference = fullFuelLaps.Average(l => l.FuelAtStart)
                             - lowFuelLaps.Average(l => l.FuelAtStart);

        LapTimePerLiter = (fullFuelAvgTime - lowFuelAvgTime) / fuelDifference;

        // Example: "Each liter of fuel costs 0.03 seconds per lap"
        Console.WriteLine($"Performance impact: {LapTimePerLiter:F4}s per liter");

        // Strategic insight: Pit earlier for pit stop advantage?
        float timeSavedPerStop = LapTimePerLiter * tankCapacity;
        Console.WriteLine($"Pit stop advantage: ~{timeSavedPerStop:F2}s lighter");
    }
}
```

---

## Pit Stop Time Estimation

### Pit Stop Duration Model

**Objective**: Estimate total pit stop time based on services requested.

**SDK Variables Available**:
- `PitRepairLeft` - Mandatory repair time remaining
- `PitOptRepairLeft` - Optional repair time remaining
- `PitSvFuel` - Fuel amount to add
- `PitSvLFP`, `PitSvRFP`, `PitSvLRP`, `PitSvRRP` - Tire pressure settings (indicates tire change)
- `PitSvTireCompound` - Tire compound (0 = no change)
- `PlayerCarInPitStall` - Precise pit detection
- `PitstopActive` - Currently being serviced

### Pit Stop Time Components

```csharp
public class PitStopTimeEstimate
{
    // Base time (car enters/exits pit)
    public float EntryExitTime { get; set; } = 3.5f; // seconds (typical)

    // Fuel service time
    public float FuelServiceTime { get; set; } // Dynamic based on amount

    // Tire change time
    public float TireChangeTime { get; set; } // 12-18 seconds per tire set

    // Brake pad change (if applicable)
    public float BrakePadTime { get; set; } = 0.0f; // Usually done during tire change

    // Damage repair time
    public float RepairTime { get; set; } = 0.0f; // From PitRepairLeft

    // Optional repairs
    public float OptionalRepairTime { get; set; } = 0.0f; // From PitOptRepairLeft

    public float TotalEstimatedTime =>
        EntryExitTime + FuelServiceTime + TireChangeTime +
        BrakePadTime + RepairTime + OptionalRepairTime;

    /// <summary>
    /// Calculate based on what pit service flags indicate
    /// </summary>
    public void Calculate(TelemetryData telemetry)
    {
        // Fuel service
        float fuelToAdd = telemetry.PitSvFuel;
        FuelServiceTime = fuelToAdd > 0
            ? 0.5f + (fuelToAdd / 40.0f) * 5.0f  // 0.5s base + ~5s per 40L
            : 0.0f;

        // Tire change detection
        bool changingTires = telemetry.PitSvTireCompound > 0
                          || telemetry.PitSvLFP > 0; // Any tire pressure change = tire work

        TireChangeTime = changingTires ? 15.0f : 0.0f; // 15s typical

        // Damage repair (pre-calculated by iRacing)
        RepairTime = telemetry.PitRepairLeft > 0
            ? telemetry.PitRepairLeft
            : 0.0f;

        OptionalRepairTime = telemetry.PitOptRepairLeft > 0
            ? telemetry.PitOptRepairLeft
            : 0.0f;
    }
}
```

### Real-World Pit Stop Times

```csharp
// Typical pit stop durations by service type:
// Fuel only:           5-8 seconds
// Fuel + tire change:  18-25 seconds
// Fuel + 2 tires:      12-18 seconds
// Damage repair:       Varies (5-60+ seconds)
// Emergency stop:      3-5 seconds

// iRacing Physics Correlation:
// - 1L of fuel ≈ 0.1-0.15 seconds
// - 1 tire ≈ 3-4 seconds per wheel
// - Full tire set ≈ 12-15 seconds
```

---

## Position Prediction on Pit Stop

### Strategy: "If I Pit Now, Where Will I Finish?"

**Objective**: Predict finishing position based on pit strategy choice.

**SDK Variables Needed**:
- `CarIdxLapDistPct` - All cars' track positions
- `CarIdxPosition` - All cars' current positions
- `CarIdxLastLapTime` - All cars' pace
- `CarIdxF2Time` - Time gaps to leader
- `CarIdxOnPitRoad` - Which cars are pitting
- `PlayerCarIdx` - Your car index
- `SessionLapsRemain` - Remaining laps
- Your pit stop duration estimate (from above)

### Position Delta Calculation

```csharp
public class PitPositionPredictor
{
    /// <summary>
    /// Predict position change from pit stop
    /// </summary>
    public class PitStopResult
    {
        public int PositionBefore { get; set; }
        public int EstimatedPositionAfter { get; set; }
        public int PositionLost { get; set; }
        public string Strategy { get; set; } // "Gain fuel + save on tires" etc
        public List<string> CarsAhead { get; set; } // Which competitors ahead after pit
        public List<string> CarsBehind { get; set; } // Which competitors behind after pit
    }

    public PitStopResult PredictPitStop(TelemetryData telemetry, float pitStopDuration)
    {
        int playerIdx = telemetry.PlayerCarIdx;
        int playerCurrentLap = telemetry.Lap;
        int playerCurrentPos = telemetry.Position;
        int lapsRemaining = telemetry.SessionLapsRemain;

        // Calculate time lost during pit stop
        float avgLapTime = telemetry.LapLastLapTime; // Use recent lap time
        float lapsLostToPit = pitStopDuration / avgLapTime;

        // Scan all other cars' positions
        var competitorAnalysis = new List<CompetitorGap>();

        for (int i = 0; i < telemetry.CarIdxLapDistPct?.Length; i++)
        {
            if (i == playerIdx) continue;
            if (telemetry.CarIdxLapDistPct[i] < 0 || telemetry.CarIdxLapDistPct[i] > 1)
                continue; // Car not on track

            float competitorLapDist = telemetry.CarIdxLapDistPct[i];
            float playerLapDist = telemetry.LapDistPct;

            // Calculate gap in lap percentage
            float gapPct = competitorLapDist - playerLapDist;
            if (gapPct < -0.5f) gapPct += 1.0f;
            if (gapPct > 0.5f) gapPct -= 1.0f;

            // Convert to time gap
            float competitorLapTime = telemetry.CarIdxLastLapTime?[i] ?? avgLapTime;
            float gapTime = gapPct * competitorLapTime;

            competitorAnalysis.Add(new CompetitorGap
            {
                CarIndex = i,
                Position = telemetry.CarIdxPosition?[i] ?? 0,
                GapSeconds = gapTime,
                LapTime = competitorLapTime,
                OnPitRoad = telemetry.CarIdxOnPitRoad?[i] ?? false
            });
        }

        // Predict positions after pit stop
        int positionsGained = 0;
        int positionsLost = 0;

        foreach (var competitor in competitorAnalysis)
        {
            // If competitor is ahead
            if (competitor.GapSeconds > 0)
            {
                // Will we pass them while pitting?
                // They travel: (lapsRemaining * competitorLapTime)
                // We travel: (lapsRemaining * avgLapTime) - pitStopDuration

                float competitorDistance = (lapsRemaining * competitor.LapTime);
                float ourDistance = (lapsRemaining * avgLapTime) - pitStopDuration;

                // Account for fuel load advantage (lighter car = faster)
                // If we gain advantage from fresh tires + less fuel:
                float ourSpeedAdvantage = 1.02f; // Conservative: 2% faster with fresh tires
                ourDistance *= ourSpeedAdvantage;

                if (ourDistance > competitorDistance + competitor.GapSeconds)
                {
                    positionsGained++;
                }
            }
            // If competitor is behind
            else if (competitor.GapSeconds < 0)
            {
                // Will they pass us while pitting?
                float ourDistance = lapsRemaining * avgLapTime - pitStopDuration;
                float competitorDistance = lapsRemaining * competitor.LapTime;

                if (competitorDistance > ourDistance)
                {
                    positionsLost++;
                }
            }
        }

        return new PitStopResult
        {
            PositionBefore = playerCurrentPos,
            EstimatedPositionAfter = playerCurrentPos + positionsLost - positionsGained,
            PositionLost = positionsLost,
            Strategy = $"Pit stop: -{pitStopDuration:F1}s, " +
                      $"gain {positionsGained} positions, lose {positionsLost} positions"
        };
    }
}

public class CompetitorGap
{
    public int CarIndex { get; set; }
    public int Position { get; set; }
    public float GapSeconds { get; set; }
    public float LapTime { get; set; }
    public bool OnPitRoad { get; set; }
}
```

### Advanced Position Prediction: Multi-Pit Scenarios

```csharp
public class MultiPitStrategy
{
    /// <summary>
    /// Compare scenarios: pit now vs. pit later
    /// </summary>
    public void CompareStrategies(TelemetryData telemetry)
    {
        int currentLap = telemetry.Lap;
        int totalLaps = telemetry.SessionLapsTotal;

        // Scenario 1: Pit now
        var pitNow = CalculatePitScenario(telemetry, currentLap,
            currentLap + 1, "Now");

        // Scenario 2: Pit in 2 laps
        var pitIn2 = CalculatePitScenario(telemetry, currentLap,
            currentLap + 3, "Lap +3");

        // Scenario 3: Pit at end (if possible)
        var pitLate = CalculatePitScenario(telemetry, currentLap,
            totalLaps - 1, "Late");

        // Compare which is best
        Console.WriteLine("=== PIT STRATEGY COMPARISON ===");
        Console.WriteLine($"Pit NOW:     Pos {pitNow.EstimatedPositionAfter}, " +
                        $"Gap to leader {pitNow.GapToLeader:F2}s");
        Console.WriteLine($"Pit LATER:   Pos {pitIn2.EstimatedPositionAfter}, " +
                        $"Gap to leader {pitIn2.GapToLeader:F2}s");
        Console.WriteLine($"Pit END:     Pos {pitLate.EstimatedPositionAfter}, " +
                        $"Gap to leader {pitLate.GapToLeader:F2}s");

        // Recommend best option
        var best = new[] { pitNow, pitIn2, pitLate }
            .OrderBy(s => s.EstimatedPositionAfter)
            .ThenBy(s => s.GapToLeader)
            .First();

        Console.WriteLine($"\n✅ RECOMMENDED: {best.Strategy}");
    }

    private PitScenarioResult CalculatePitScenario(TelemetryData telemetry,
        int currentLap, int pitLap, string scenarioName)
    {
        // Simplified: pit on specified lap, calculate final position
        // In production: account for fuel consumption, other cars' pit strategies, etc.

        float avgLapTime = telemetry.LapLastLapTime;
        float fuelAtLap = CalculateFuelAtLap(telemetry, pitLap);
        float pitDuration = EstimatePitDuration(telemetry, fuelAtLap);

        // Time lost vs. not pitting
        float timeLost = pitDuration;

        // Time gained from fresh tires
        float timeGained = avgLapTime * 0.02f; // 2% faster per lap with fresh tires

        return new PitScenarioResult
        {
            Strategy = scenarioName,
            PitLap = pitLap,
            EstimatedPositionAfter = telemetry.Position - 1, // Placeholder
            GapToLeader = 5.0f, // Placeholder
            NetTimeChange = timeLost - timeGained
        };
    }
}

public class PitScenarioResult
{
    public string Strategy { get; set; }
    public int PitLap { get; set; }
    public int EstimatedPositionAfter { get; set; }
    public float GapToLeader { get; set; }
    public float NetTimeChange { get; set; }
}
```

---

## Advanced Strategies

### Strategy 5: Traffic Impact on Fuel Consumption

**Detection**: Identify fuel usage differences when in traffic vs. clean air.

```csharp
public class TrafficFuelAnalysis
{
    public float CleanAirAverage { get; set; }
    public float TrafficAverage { get; set; }
    public float TrafficPenalty { get; set; } // Extra fuel in traffic

    public void AnalyzeTrafficImpact(List<FuelLapHistory> history, TelemetryData telemetry)
    {
        var cleanAirLaps = new List<FuelLapHistory>();
        var trafficLaps = new List<FuelLapHistory>();

        for (int i = 0; i < history.Count; i++)
        {
            var lap = history[i];

            // Detect if car was in traffic (following another car)
            // Use CarIdxF2Time or CarIdxLapDistPct to detect cars ahead
            bool wasInTraffic = DetectTraffic(telemetry, i);

            if (wasInTraffic)
                trafficLaps.Add(lap);
            else
                cleanAirLaps.Add(lap);
        }

        CleanAirAverage = cleanAirLaps.Count > 0
            ? cleanAirLaps.Average(l => l.FuelUsed)
            : 0;

        TrafficAverage = trafficLaps.Count > 0
            ? trafficLaps.Average(l => l.FuelUsed)
            : 0;

        TrafficPenalty = TrafficAverage - CleanAirAverage;

        // Strategy: "Following costs 0.15L per lap - avoid if possible"
        Console.WriteLine($"Traffic penalty: {TrafficPenalty:F3}L per lap " +
                        $"({(TrafficPenalty/CleanAirAverage*100):F1}% more)");
    }
}
```

### Strategy 6: Fuel Mixture & Advanced Settings

**Note**: Currently NOT captured - need to add `dcFuelMixture` to SDK variables.

```csharp
// When added, will allow detection of fuel-saving modes:
//
// Lean mixture = less fuel, less power
// Rich mixture = more fuel, more power
//
// Strategy: Detect when driver activates lean mode for fuel saving
// vs. race pace mode

if (telemetry.dcFuelMixture < -0.5f)
{
    // Lean mixture active
    expectedFuelSavings = 0.1f; // ~10% fuel savings estimate
    powerLoss = 0.05f; // ~5% power loss
}
```

### Strategy 7: Weather Impact on Fuel

```csharp
public class WeatherFuelImpact
{
    public void AnalyzeWeatherCorrelation(List<FuelLapHistory> history, TelemetryData telemetry)
    {
        // Higher air density (cold, high humidity) = more drag = more fuel
        // High track temperature = less engine power needed? = less fuel?

        // Group laps by conditions
        var hotLaps = history.Where(l => l.AirTemp > 30).ToList();
        var coldLaps = history.Where(l => l.AirTemp < 15).ToList();
        var normalLaps = history.Where(l => l.AirTemp >= 15 && l.AirTemp <= 30).ToList();

        // Analyze: which condition used most fuel?
        float hotAvg = hotLaps.Count > 0 ? hotLaps.Average(l => l.FuelUsed) : 0;
        float coldAvg = coldLaps.Count > 0 ? coldLaps.Average(l => l.FuelUsed) : 0;
        float normalAvg = normalLaps.Count > 0 ? normalLaps.Average(l => l.FuelUsed) : 0;

        // Strategy: "Cold weather uses 8% more fuel"
    }
}
```

---

## Implementation Recommendations

### Priority 1: Critical Missing Variables (DO FIRST!)

Add to `IRacingTelemetryService.cs` RequiredTelemetryVars:
```csharp
"SessionLapsRemainEx",      // ⭐ iRacing's laps remaining estimate
"RaceLaps",                 // Total race laps
"SessionLapsTotal",         // Session total laps
"PlayerCarInPitStall",      // More precise pit detection
"PitSvFuel",                // Fuel amount being added
"PitRepairLeft",            // Mandatory repair time
"PitOptRepairLeft",         // Optional repair time
"CarDistAhead",             // Distance to car ahead (meters)
"CarDistBehind"             // Distance to car behind (meters)
```

Add to `TelemetryData.cs`:
```csharp
public float CarDistAhead { get; set; }
public float CarDistBehind { get; set; }
public int SessionLapsRemainEx { get; set; }
public int RaceLaps { get; set; }
public int SessionLapsTotal { get; set; }
public bool PlayerCarInPitStall { get; set; }
public float PitSvFuel { get; set; }
public float PitRepairLeft { get; set; }
public float PitOptRepairLeft { get; set; }
```

### Priority 2: Implement Pit Stop Time Estimation

Create `PitStopAnalyzer.cs`:
- Calculate pit duration based on services requested
- Model fuel addition time
- Model tire change time
- Account for damage repair
- Combine for total pit stop estimate

### Priority 3: Implement Position Prediction

Create `PositionPredictor.cs`:
- Scan all cars' positions relative to player
- Calculate time gaps
- Predict movement during pit stop
- Estimate position after pit
- Compare pit now vs. pit later scenarios

### Priority 4: Calculator Comparison

Extend `FuelCalculatorService.cs`:
- Track `FuelUsePerHour` from telemetry
- Convert iRacing's estimate to L/lap
- Compare with your calculation
- Alert on significant discrepancies

### Priority 5: Anomaly Detection

Create `AnomalyDetector.cs`:
- Statistical outlier detection (mean ± 2σ)
- Context-aware validation
- Driving style correlation
- Generate anomaly reports

### Priority 6: Advanced Analytics

Create `AdvancedFuelAnalytics.cs`:
- Traffic impact analysis
- Weather correlation
- Fuel load vs. lap time
- Sector-by-sector fuel mapping
- Multi-pit strategy comparison

---

## Summary

### What's Already Working ✅
- Lap-by-lap fuel consumption tracking
- Multiple averaging methods
- Green vs. yellow flag separation
- Basic pit detection
- Fuel strategy calculations

### What Needs Implementation ❌
1. **Pit stop time estimation** - Predict duration based on services
2. **Position prediction** - "If I pit now, where do I finish?"
3. **Calculator comparison** - Validate vs. iRacing's estimate
4. **Anomaly detection** - Identify unusual fuel usage
5. **Advanced analytics** - Traffic, weather, multi-pit strategies

### Available SDK Variables Summary
- **326 total variables** in iRacing SDK
- **7 fuel variables** (4 currently captured, 3 new ones)
- **14 pit service variables** (all available)
- **14 multi-car position arrays** (all available)
- **12 timing variables** (for position prediction)

### Next Steps
1. Rebuild `IRacingTelemetryService.cs` with critical missing variables
2. Implement pit stop time model
3. Implement position prediction algorithm
4. Add calculator comparison features
5. Build anomaly detection system

---

**Document Created**: 2025-10-19
**Last Updated**: 2025-10-19
**Status**: Ready for Implementation
