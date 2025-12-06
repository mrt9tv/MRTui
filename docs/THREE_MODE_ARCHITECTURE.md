# Three-Mode Architecture: Setup Engineering, Strategy Scouting, Driving

**Date**: December 2025  
**Status**: 🎯 DESIGN PHASE  
**Priority**: HIGH (Core Product Differentiation)  
**Estimated Effort**: 4-6 weeks (Phase 1: 2 weeks, Phase 2: 2 weeks, Phase 3: 2 weeks)

---

## Executive Summary

**Vision**: Transform MRT Overlay from a single-purpose driving aid into a **comprehensive race engineering platform** with three distinct operational modes:

1. **Setup Engineering Mode**: Develop optimal car setups through data-driven testing
2. **Strategy Scouting Mode**: Gather intelligence on fuel, tires, pit stops, and race scenarios
3. **Driving Mode**: Real-time overlay assistance during actual racing

**Key Innovation**: Each mode serves a distinct phase of the race preparation → execution workflow, with data flowing seamlessly between modes.

---

## Table of Contents

1. [Mode Definitions & Use Cases](#mode-definitions--use-cases)
2. [Architecture Overview](#architecture-overview)
3. [Data Flow & Storage](#data-flow--storage)
4. [ML Integration Strategy](#ml-integration-strategy)
5. [Setup Engineering Deep Dive](#setup-engineering-deep-dive)
6. [Strategy Scouting Deep Dive](#strategy-scouting-deep-dive)
7. [Mode Switching UX](#mode-switching-ux)
8. [Implementation Roadmap](#implementation-roadmap)
9. [Technical Considerations](#technical-considerations)

---

## Mode Definitions & Use Cases

### Mode 1: Setup Engineering 🔧

**When**: Practice sessions, test days, private testing  
**Purpose**: Optimize car setup through systematic data collection and analysis  
**Primary Users**: Engineers, serious sim racers, league teams

**Core Features**:
- **Lap-by-lap telemetry comparison** (speed traces, tire temps, fuel consumption)
- **Setup change tracking** (before/after comparison with statistical significance)
- **Sector analysis** (identify where time is gained/lost)
- **Tire temperature optimization** (ideal temp ranges, camber/pressure effects)
- **Balance analysis** (understeer/oversteer detection, brake bias optimization)
- **ML-powered recommendations** (predict lap time improvement from setup changes)
- **Session history** (track all setups tested, export CSV for external analysis)

**Output**: Optimal setup configurations for specific tracks/conditions, exported as iRacing setup files

**Example Workflow**:
```
1. Engineer loads baseline setup in iRacing
2. Setup Engineering Mode records 5 laps of telemetry
3. Engineer adjusts front wing (-2 clicks)
4. Mode records 5 more laps
5. ML model analyzes: "Front wing change → +0.15s/lap (95% confidence)"
6. Mode suggests: "Try rear ARB +1 click to balance oversteer in T3"
7. Repeat until optimal setup found
8. Export final setup as "Spa_GT3_Quali_Optimal_v2.sto"
```

---

### Mode 2: Strategy Scouting 📊

**When**: Practice/Qualifying before race, pre-race preparation  
**Purpose**: Gather strategic intelligence for race planning  
**Primary Users**: Race strategists, endurance teams, competitive racers

**Core Features**:
- **Fuel consumption profiling** (push vs fuel-save scenarios, stint length simulation)
- **Tire degradation tracking** (grip loss curves, stint predictions, compound comparison)
- **Pit stop timing optimization** (undercut/overcut windows, traffic analysis)
- **Weather impact analysis** (rain tire performance, temp-based fuel adjustments)
- **Traffic simulation** (how many positions lost during pit stop)
- **Multi-stint strategy comparison** (1-stop vs 2-stop vs 3-stop scenarios)
- **Competitor intelligence** (observe other drivers' pit strategies, lap times)
- **Historical session comparison** (compare practice → race conditions)

**Output**: Pre-race strategy document with pit windows, fuel targets, tire management plans

**Example Workflow**:
```
1. Strategist enters Strategy Scouting Mode during practice
2. Driver runs 20 laps at race pace → Mode calculates: "2.3L/lap avg"
3. Driver runs 10 laps in fuel-save mode → Mode calculates: "1.8L/lap (-22%)"
4. Mode analyzes tire deg: "Grip -8% after 15 laps, critical at lap 22"
5. Mode simulates: "1-stop at L18 = 1:42:30, 2-stop at L12+L24 = 1:43:15"
6. Strategist exports: "SpaRace_Strategy_1Stop_L18.pdf"
7. Driver enters race with strategy pre-loaded
```

---

### Mode 3: Driving (Current Overlay) 🏁

**When**: Qualifying, Race  
**Purpose**: Real-time assistance during competitive driving  
**Primary Users**: All drivers during actual racing

**Core Features** (Already Implemented):
- **Real-time fuel calculations** (laps remaining, optimal pit lap)
- **Delta tracking** (lap time comparison, sector splits)
- **Position awareness** (proximity radar, gap to leader/ahead/behind)
- **Tire monitoring** (temps, pressures, lockup detection)
- **Pit strategy execution** (countdown to pit window, fuel to add)
- **Flag status** (yellow, green, pit exit)
- **Lap timing** (best lap, last lap, session time remaining)

**Output**: On-screen overlays that don't distract from driving

**Example Workflow**:
```
1. Driver starts race in Driving Mode (overlays enabled)
2. Fuel widget shows: "18 laps on fuel, pit L14-16"
3. Radar widget alerts: "Car 0.5s behind, closing"
4. Lap 14: Pit strategy widget highlights: "PIT NOW (optimal)"
5. Driver pits, refuels 32L (pre-calculated from Strategy Scouting)
6. Exits pit, resumes race with updated calculations
```

---

## Architecture Overview

### High-Level System Design

```
┌─────────────────────────────────────────────────────────────┐
│                     MRT Overlay Platform                     │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌───────────────┐  ┌──────────────┐  ┌──────────────────┐ │
│  │   Setup       │  │  Strategy    │  │  Driving         │ │
│  │   Engineering │  │  Scouting    │  │  (Current)       │ │
│  │   Mode        │  │  Mode        │  │  Mode            │ │
│  └───────┬───────┘  └──────┬───────┘  └─────────┬────────┘ │
│          │                 │                     │           │
│          └─────────────────┴─────────────────────┘           │
│                            │                                 │
│                   ┌────────▼────────┐                        │
│                   │  Mode Controller │                       │
│                   │  (State Machine) │                       │
│                   └────────┬────────┘                        │
│                            │                                 │
│          ┌─────────────────┴─────────────────┐              │
│          │                                    │              │
│  ┌───────▼────────┐                 ┌────────▼────────┐    │
│  │  Telemetry     │                 │  Data Storage   │    │
│  │  Collection    │◄───────────────►│  & Export       │    │
│  │  Engine        │                 │  (SQL/Files)    │    │
│  └────────────────┘                 └─────────────────┘    │
│                                                               │
│  ┌────────────────────────────────────────────────────────┐ │
│  │            Service Layer (Shared)                      │ │
│  │  ┌──────────────────┐  ┌──────────────────────────┐   │ │
│  │  │ FuelCalculator   │  │ TireStrategyService      │   │ │
│  │  └──────────────────┘  └──────────────────────────┘   │ │
│  │  ┌──────────────────┐  ┌──────────────────────────┐   │ │
│  │  │ LapComparison    │  │ SetupAnalysisService     │   │ │
│  │  └──────────────────┘  └──────────────────────────┘   │ │
│  │  ┌──────────────────┐  ┌──────────────────────────┐   │ │
│  │  │ MLPrediction     │  │ WeatherTrackService      │   │ │
│  │  └──────────────────┘  └──────────────────────────┘   │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                               │
│  ┌────────────────────────────────────────────────────────┐ │
│  │         External ML Integration                        │ │
│  │  ┌────────────────┐  ┌──────────────────────────────┐ │ │
│  │  │ Setup NN/      │  │ Strategy CatBoost Model      │ │ │
│  │  │ CatBoost       │  │ (Fuel/Tire Predictions)      │ │ │
│  │  └────────────────┘  └──────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Mode Controller State Machine

```csharp
public enum OperationalMode
{
    SetupEngineering,  // Data collection + comparison + ML recommendations
    StrategyScouting,  // Strategic data gathering + scenario simulation
    Driving           // Real-time overlay (current implementation)
}

public class ModeController
{
    private OperationalMode _currentMode = OperationalMode.Driving;
    private readonly TelemetryCollectionEngine _telemetryEngine;
    private readonly DataStorageService _storage;
    
    // Switch modes with validation
    public void SwitchMode(OperationalMode newMode)
    {
        // Validate transition (e.g., can't switch to Driving during Setup Engineering session)
        if (!CanTransitionTo(newMode))
            throw new InvalidOperationException($"Cannot switch from {_currentMode} to {newMode}");
        
        // Save current mode state
        SaveModeState(_currentMode);
        
        // Load new mode state
        LoadModeState(newMode);
        
        // Update UI
        _currentMode = newMode;
        ModeChanged?.Invoke(this, new ModeChangedEventArgs(_currentMode));
    }
    
    // Each mode has different telemetry collection profiles
    public TelemetryProfile GetCollectionProfile(OperationalMode mode)
    {
        return mode switch
        {
            OperationalMode.SetupEngineering => new TelemetryProfile
            {
                SampleRate = 60,    // Hz - high freq for detailed analysis
                RecordFields = TelemetryFields.ALL, // Capture everything
                StorageMode = StorageMode.FullHistory, // Keep all laps
                EnableComparison = true
            },
            OperationalMode.StrategyScouting => new TelemetryProfile
            {
                SampleRate = 10,    // Hz - lower freq, just aggregates
                RecordFields = TelemetryFields.Strategy, // Fuel, tires, lap times
                StorageMode = StorageMode.AggregatesOnly,
                EnableProjection = true
            },
            OperationalMode.Driving => new TelemetryProfile
            {
                SampleRate = 60,    // Hz - real-time responsiveness
                RecordFields = TelemetryFields.Essential, // Just overlay data
                StorageMode = StorageMode.CurrentSession, // Discard after session
                EnableOverlays = true
            }
        };
    }
}
```

---

## Data Flow & Storage

### Storage Strategy for .exe Deployment

**Requirements**:
- Must work as standalone .exe (no external DB dependencies)
- Must persist data between sessions (track setup history, strategy profiles)
- Must be fast enough for real-time queries (< 10ms)
- Must support export to standard formats (CSV, JSON, iRacing setup files)

**Recommended Solution**: **SQLite Embedded Database**

**Why SQLite**:
✅ **Single-file database** (`MRTOverlay.db`) stored in `%APPDATA%/MRTOverlay/`  
✅ **Zero-configuration** (no server, no installation, ships with .exe)  
✅ **Fast queries** (< 5ms for indexed queries, perfect for real-time)  
✅ **ACID compliance** (no data corruption from crashes)  
✅ **Well-supported in .NET** (`Microsoft.Data.Sqlite` NuGet package)  
✅ **Portable** (users can backup/share `.db` file)  
✅ **SQL standard** (easy migration to PostgreSQL/MySQL if needed later)  

**Schema Design**:

```sql
-- Setup Engineering Tables
CREATE TABLE SetupSessions (
    SessionId TEXT PRIMARY KEY,
    TrackName TEXT NOT NULL,
    CarName TEXT NOT NULL,
    SessionType TEXT, -- 'Practice', 'TestDay'
    StartTime DATETIME,
    Weather TEXT,     -- JSON: {"AirTemp": 25, "TrackTemp": 35}
    Notes TEXT
);

CREATE TABLE SetupConfigurations (
    SetupId TEXT PRIMARY KEY,
    SessionId TEXT,
    SetupName TEXT,
    SetupData TEXT,   -- JSON: iRacing setup file content
    Timestamp DATETIME,
    FOREIGN KEY (SessionId) REFERENCES SetupSessions(SessionId)
);

CREATE TABLE LapTelemetry (
    LapId TEXT PRIMARY KEY,
    SetupId TEXT,
    LapNumber INTEGER,
    LapTime REAL,
    Sector1 REAL,
    Sector2 REAL,
    Sector3 REAL,
    FuelUsed REAL,
    AvgTireTemp_FL REAL,
    AvgTireTemp_FR REAL,
    AvgTireTemp_RL REAL,
    AvgTireTemp_RR REAL,
    TelemetryData BLOB, -- Full 60Hz telemetry (compressed)
    FOREIGN KEY (SetupId) REFERENCES SetupConfigurations(SetupId)
);

-- Strategy Scouting Tables
CREATE TABLE StrategyProfiles (
    ProfileId TEXT PRIMARY KEY,
    TrackName TEXT,
    CarName TEXT,
    SessionDate DATE,
    FuelPerLap_Push REAL,
    FuelPerLap_Save REAL,
    TireDegModel TEXT,  -- JSON: degradation curve coefficients
    PitStopTime REAL,
    OptimalStrategy TEXT -- JSON: stint plan
);

CREATE TABLE StintSimulations (
    SimulationId TEXT PRIMARY KEY,
    ProfileId TEXT,
    StintPlan TEXT,    -- JSON: [{"Stint": 1, "Laps": 18, "Fuel": 40}]
    TotalTime REAL,
    Feasibility TEXT,  -- 'Confirmed', 'Estimated', 'Risky'
    FOREIGN KEY (ProfileId) REFERENCES StrategyProfiles(ProfileId)
);

-- Driving Mode (Current Session Data)
CREATE TABLE ActiveSession (
    SessionId TEXT PRIMARY KEY,
    StartTime DATETIME,
    CurrentLap INTEGER,
    CurrentData TEXT  -- JSON: FuelData snapshot
);
```

**File Structure**:
```
%APPDATA%/MRTOverlay/
  ├── MRTOverlay.db          # SQLite database (all persistent data)
  ├── Exports/
  │   ├── Setup_Spa_2025_12_01.csv
  │   ├── Strategy_LeMans_1Stop.pdf
  │   └── Telemetry_Session_abc123.json
  ├── Logs/
  │   └── app_2025_12_01.log
  └── MLModels/
      ├── setup_catboost.cbm
      └── strategy_nn.onnx
```

---

## ML Integration Strategy

### Your Existing ML Models Integration

**Assumption**: You have:
1. **Setup NN/CatBoost**: Trained on setup parameters → lap time delta
2. **Strategy Model**: Trained on fuel/tire data → optimal strategy

**Integration Points**:

```csharp
public class MLModelService
{
    private readonly SetupPredictionModel _setupModel;
    private readonly StrategyPredictionModel _strategyModel;
    
    // Load models from %APPDATA%/MRTOverlay/MLModels/
    public MLModelService()
    {
        string modelPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MRTOverlay", "MLModels"
        );
        
        _setupModel = SetupPredictionModel.Load(
            Path.Combine(modelPath, "setup_catboost.cbm")
        );
        _strategyModel = StrategyPredictionModel.Load(
            Path.Combine(modelPath, "strategy_nn.onnx")
        );
    }
    
    // Setup Engineering: Predict lap time improvement
    public SetupPrediction PredictSetupChange(
        SetupConfiguration baselineSetup,
        SetupConfiguration proposedSetup,
        TrackConditions conditions)
    {
        var features = ExtractFeatures(baselineSetup, proposedSetup, conditions);
        float predictedDelta = _setupModel.Predict(features);
        float confidence = _setupModel.GetConfidence(features);
        
        return new SetupPrediction
        {
            LapTimeDelta = predictedDelta,  // e.g., -0.15s (improvement)
            Confidence = confidence,         // e.g., 0.95 (95% confidence)
            Recommendation = GenerateRecommendation(predictedDelta, confidence)
        };
    }
    
    // Strategy Scouting: Predict optimal strategy
    public StrategyPrediction PredictOptimalStrategy(
        float fuelPerLap,
        TireDegradationCurve tireDeg,
        int raceLaps,
        float pitStopTime)
    {
        var features = new[]
        {
            fuelPerLap,
            tireDeg.InitialGrip,
            tireDeg.DegradationRate,
            raceLaps,
            pitStopTime
        };
        
        var prediction = _strategyModel.Predict(features);
        
        return new StrategyPrediction
        {
            OptimalStops = prediction.NumStops,
            PitLaps = prediction.PitLaps,
            EstimatedRaceTime = prediction.TotalTime,
            Confidence = prediction.Confidence
        };
    }
}
```

**Model File Formats**:
- **CatBoost**: `.cbm` binary format (use `CatBoostSharp` NuGet)
- **Neural Network**: `.onnx` format (use `Microsoft.ML.OnnxRuntime` NuGet)
- **Fallback**: If models not found, disable ML features (graceful degradation)

---

## Setup Engineering Deep Dive

### What is Setup Engineering in Sim Racing?

**Definition**: Systematic process of optimizing car setup parameters (suspension, aero, tire pressures) to maximize lap time for specific track/conditions.

**Key Parameters**:
```
Aerodynamics:
  - Front wing angle (affects downforce, drag, understeer)
  - Rear wing angle (affects rear grip, drag, top speed)

Suspension:
  - Spring rates (stiffness, affects weight transfer)
  - Dampers (compression/rebound, affects tire contact)
  - Anti-roll bars (affects roll stiffness, under/oversteer)
  - Ride height (affects aero platform, ground clearance)

Tires:
  - Tire pressures (affects contact patch, tire temps)
  - Camber angle (affects tire wear, cornering grip)
  - Toe angle (affects stability, tire wear)

Brake Balance:
  - Front/rear brake bias (affects braking stability, rotation)
```

### How MRT Overlay Helps

**Problem**: iRacing provides no built-in tools to compare setups objectively. Drivers rely on "feel" which is subjective and inconsistent.

**Solution**: MRT Setup Engineering Mode provides:

1. **Objective Lap Comparison**
   ```
   Baseline Setup:
   - Best: 2:19.543 (Lap 3)
   - Avg: 2:19.782 (5 laps)
   
   Modified Setup (Front Wing -2):
   - Best: 2:19.387 (Lap 8)  ← 0.156s faster!
   - Avg: 2:19.621 (5 laps)  ← 0.161s faster!
   
   Statistical Confidence: 98% (improvement is real, not luck)
   ```

2. **Sector-Level Analysis**
   ```
   Sector 1 (High-speed corners):
   - Before: 42.123s
   - After:  42.089s (-0.034s, 68% confidence)
   
   Sector 2 (Technical section):
   - Before: 51.234s
   - After:  51.102s (-0.132s, 95% confidence) ← MAJOR GAIN
   
   Sector 3 (Long straight):
   - Before: 46.186s
   - After:  46.196s (+0.010s, drag penalty expected)
   
   Conclusion: Front wing change improved mid-corner grip (S2) 
                more than it hurt straight-line speed (S3)
   ```

3. **Tire Temperature Optimization**
   ```
   Ideal Range: 80-90°C (car-specific)
   
   Baseline Setup:
   - FL: 78°C (too cold, ↓ grip)
   - FR: 79°C (too cold, ↓ grip)
   - RL: 92°C (too hot, ↓ life)
   - RR: 91°C (too hot, ↓ life)
   
   Recommendation: 
   - Increase front tire pressure +1 PSI (raise FL/FR temps)
   - Decrease rear camber -0.2° (lower RL/RR temps)
   
   After Adjustment:
   - FL: 82°C ✓ (in range)
   - FR: 83°C ✓ (in range)
   - RL: 88°C ✓ (in range)
   - RR: 87°C ✓ (in range)
   
   Result: +0.2s/lap from optimal tire temps
   ```

4. **ML-Powered Predictions** (Your NN/CatBoost Integration)
   ```
   Current Setup Issues Detected:
   - Front ARB too soft (→ understeer in T3, T7)
   - Rear toe too aggressive (→ tire wear, straight-line instability)
   
   ML Model Recommendations:
   1. Front ARB +2 clicks → Predicted: -0.08s/lap (87% confidence)
   2. Rear toe -0.05° → Predicted: -0.05s/lap, +2 laps tire life (92% confidence)
   3. Combined effect → Predicted: -0.12s/lap (81% confidence)
   
   Suggested Test Order:
   1. Test Front ARB first (higher confidence, bigger gain)
   2. Then test rear toe (validate tire life improvement)
   3. Then test both together (check for interactions)
   ```

### Setup Engineering UI Mock

```
┌───────────────────────────────────────────────────────────────┐
│ SETUP ENGINEERING MODE - Spa-Francorchamps GT3               │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│ Current Session: "Baseline Setup Testing"                    │
│ Laps Recorded: 12 | Valid Laps: 10 | Outliers: 2            │
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ SETUP COMPARISON                                        │ │
│ │                                                         │ │
│ │ Baseline Setup (Laps 1-5):        2:19.782 avg         │ │
│ │ Modified Setup (Laps 6-10):       2:19.621 avg         │ │
│ │                                                         │ │
│ │ Improvement: -0.161s/lap (0.69%) ✅ 98% confidence     │ │
│ │                                                         │ │
│ │ Changes Made:                                           │ │
│ │  • Front Wing: -2 clicks                                │ │
│ │  • Front ARB: +1 click                                  │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ SECTOR ANALYSIS                                         │ │
│ │                                                         │ │
│ │ S1: 42.089s (-0.034s, 68%) → Minor gain                │ │
│ │ S2: 51.102s (-0.132s, 95%) → MAJOR gain ⭐              │ │
│ │ S3: 46.196s (+0.010s, 54%) → Slight loss (expected)    │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ ML RECOMMENDATIONS                                      │ │
│ │                                                         │ │
│ │ 🤖 Next Test Suggestion:                                │ │
│ │ Rear ARB +2 clicks → -0.11s/lap predicted (89% conf.)  │ │
│ │ Rationale: Balance oversteer from front wing change    │ │
│ │                                                         │ │
│ │ [Start Next Test Session]  [Export Results]            │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ [Switch to Strategy Scouting] [Switch to Driving Mode]      │
└───────────────────────────────────────────────────────────────┘
```

---

## Strategy Scouting Deep Dive

### Purpose

**Goal**: Answer the critical pre-race questions:
- How many pit stops do I need?
- When should I pit?
- Can I fuel-save to avoid an extra stop?
- How much does tire deg affect my pace?
- What strategy should I plan for different race scenarios?

### Key Features

1. **Fuel Profiling**
   ```
   Practice Session - 25 laps completed
   
   Push Pace (Laps 1-10):
   - Avg Fuel/Lap: 2.35L
   - Lap Time Avg: 2:19.5
   
   Fuel-Save Mode (Laps 15-25):
   - Avg Fuel/Lap: 1.88L (-20%)
   - Lap Time Avg: 2:21.2 (+1.7s/lap)
   
   Race: 44 laps, 100L tank
   
   Scenario 1: Push entire race
   - Fuel needed: 103.4L → REQUIRES 1 PIT STOP
   
   Scenario 2: Fuel-save final stint
   - Push 28 laps (65.8L) → Pit → Save 16 laps (30.1L)
   - Total fuel: 95.9L → FITS IN TANK! NO PIT NEEDED!
   - Time penalty: +27.2s slower, but saves 25s pit stop
   - Net benefit: +2.2s faster with fuel-save strategy!
   ```

2. **Tire Degradation Modeling**
   ```
   Stint 1 Data (20 laps on soft compound):
   
   Lap 1:  2:19.2 (100% grip)
   Lap 5:  2:19.8 (98% grip, -0.6s)
   Lap 10: 2:20.7 (95% grip, -1.5s)
   Lap 15: 2:22.1 (90% grip, -2.9s)
   Lap 20: 2:24.3 (83% grip, -5.1s)
   
   Model: Exponential decay, grip = 100 * e^(-0.01*lap)
   
   Prediction for Stint 2:
   - Lap 21-30: Lose 3.2s over 10 laps
   - Lap 31-40: Lose 5.8s over 10 laps (accelerating deg)
   
   Optimal Pit: Lap 35 (before critical deg phase)
   ```

3. **Multi-Stint Strategy Comparison**
   ```
   Race: 44 laps, 2:19 avg pace, 25s pit stop, 100L tank
   
   STRATEGY 1: No-Stop (Fuel Save)
   - Stint 1: 44 laps fuel-save (1.88L/lap)
   - Total Time: 1:42:45 (includes +27s from fuel-save)
   - Feasibility: 95% (tight on fuel)
   
   STRATEGY 2: 1-Stop @ L22
   - Stint 1: 22 laps push (2.35L/lap)
   - Pit: 25s
   - Stint 2: 22 laps push (2.35L/lap)
   - Total Time: 1:42:30 ✅ FASTEST
   - Feasibility: 100%
   
   STRATEGY 3: 1-Stop @ L30 (long first stint)
   - Stint 1: 30 laps push (2.35L/lap)
   - Pit: 25s + traffic penalty (+5s) = 30s
   - Stint 2: 14 laps push (2.35L/lap)
   - Total Time: 1:42:55
   - Feasibility: 85% (risky, tire deg unproven)
   
   RECOMMENDATION: Strategy 2 (1-Stop @ L22)
   - Balanced stints
   - Avoids traffic during pit cycle
   - Fresh tires for final stint
   ```

4. **Competitor Intelligence**
   ```
   Observing Car #23 (Same Class):
   
   Lap 12: Entered pits (early stop strategy)
   - Fuel added: ~45L (15 lap capacity)
   - Tire change: Yes (fresh soft compound)
   - Pit time: 28s (slower than optimal)
   
   Prediction: #23 will pit again at L27-29
   
   Your Strategy Impact:
   - If you pit L22, you'll be on same cycle as #23
   - Consider pit L20 for undercut (jump ahead in pit cycle)
   - Or pit L25 for overcut (stay out longer, pass during their pit)
   ```

### Strategy Scouting UI Mock

```
┌───────────────────────────────────────────────────────────────┐
│ STRATEGY SCOUTING MODE - Spa 2-Hour Endurance                │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ FUEL PROFILING                                          │ │
│ │                                                         │ │
│ │ Push Pace:      2.35 L/lap (25 laps tested)            │ │
│ │ Fuel-Save:      1.88 L/lap (15 laps tested) -20%       │ │
│ │                                                         │ │
│ │ Race Length:    44 laps (2:00:00 estimated)            │ │
│ │ Tank Capacity:  100L                                    │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ STRATEGY COMPARISON                                     │ │
│ │                                                         │ │
│ │ ┌─────────────────────────────────────────────────┐   │ │
│ │ │ 1-STOP @ L22 ⭐ RECOMMENDED                      │   │ │
│ │ │ Total Time: 1:42:30 (fastest)                   │   │ │
│ │ │ Feasibility: 100%                                │   │ │
│ │ │ Pit Window: L20-24 (optimal: L22)               │   │ │
│ │ └─────────────────────────────────────────────────┘   │ │
│ │                                                         │ │
│ │ ┌─────────────────────────────────────────────────┐   │ │
│ │ │ NO-STOP (Fuel-Save)                             │   │ │
│ │ │ Total Time: 1:42:45 (+15s slower)               │   │ │
│ │ │ Feasibility: 95% (risky, 2L margin)             │   │ │
│ │ │ Fuel-Save: L25-44 (20 laps at -20% pace)        │   │ │
│ │ └─────────────────────────────────────────────────┘   │ │
│ │                                                         │ │
│ │ ┌─────────────────────────────────────────────────┐   │ │
│ │ │ 2-STOP @ L15, L30                               │   │ │
│ │ │ Total Time: 1:43:25 (+55s slower)               │   │ │
│ │ │ Feasibility: 100%                                │   │ │
│ │ │ Not recommended: Extra pit stop too costly       │   │ │
│ │ └─────────────────────────────────────────────────┘   │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ TIRE DEGRADATION                                        │ │
│ │                                                         │ │
│ │ Lap 1-10:  98% grip (-0.5s)                             │ │
│ │ Lap 11-20: 93% grip (-1.8s cumulative)                  │ │
│ │ Lap 21-30: 85% grip (-3.9s cumulative) ⚠️               │ │
│ │                                                         │ │
│ │ Critical Degradation: Lap 25+ (avoid long first stint) │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ [Export Strategy PDF] [Load in Driving Mode]                │
│ [Switch to Setup Engineering] [Switch to Driving Mode]      │
└───────────────────────────────────────────────────────────────┘
```

---

## Mode Switching UX

### Master Control Panel

**Concept**: Floating widget (always on top) that shows current mode + quick switch

```
┌─────────────────────────────────────┐
│ MRT OVERLAY                         │
├─────────────────────────────────────┤
│ Mode: 🏁 DRIVING                    │ ← Current mode indicator
│                                     │
│ [🔧 Setup] [📊 Strategy] [🏁 Drive] │ ← Quick switch buttons
│                                     │
│ Session: Spa Race | Lap 12/44      │
└─────────────────────────────────────┘
```

### Mode Transition Rules

**Safe Transitions**:
```
Setup Engineering → Strategy Scouting: ✅ (save setup data)
Setup Engineering → Driving:          ✅ (save setup data, load strategy)
Strategy Scouting → Driving:          ✅ (load strategy into fuel calc)
Driving → Strategy Scouting:          ✅ (analyze mid-session)
Driving → Setup Engineering:          ⚠️ (warn: will stop overlay updates)
```

**Blocked Transitions**:
```
Setup Engineering during active comparison: ❌ "Finish current test session first"
Strategy Scouting during race:              ⚠️ "Switch to Driving Mode for race?"
```

### Mode-Specific Overlays

**Setup Engineering Mode**:
- Hide: Fuel widget, Pit strategy, Radar (not needed)
- Show: Lap comparison, Sector times, Tire temps, Setup change tracker

**Strategy Scouting Mode**:
- Hide: Radar, Position tracking (not racing yet)
- Show: Fuel usage tracker, Tire deg monitor, Lap counter

**Driving Mode** (current):
- Show: All overlays (fuel, radar, delta, pit strategy)
- Minimize distractions (compact layouts)

---

## Implementation Roadmap

### Phase 1: Foundation (2 weeks)

**Goal**: Infrastructure for mode switching, SQLite storage, telemetry collection profiles

**Tasks**:
1. **Mode Controller** (2 days)
   - `ModeController` class with state machine
   - `OperationalMode` enum
   - Mode transition validation
   - Mode settings persistence

2. **SQLite Integration** (3 days)
   - Add `Microsoft.Data.Sqlite` NuGet
   - Create schema (setup sessions, lap telemetry, strategy profiles)
   - `DataStorageService` for CRUD operations
   - Migration scripts for schema updates

3. **Telemetry Collection Engine** (3 days)
   - `TelemetryCollectionProfile` (mode-specific sampling)
   - High-frequency recording for Setup Engineering (60 Hz full data)
   - Aggregate-only recording for Strategy Scouting (10 Hz averages)
   - Current session recording for Driving (existing behavior)

4. **Mode UI Framework** (4 days)
   - Master control panel (floating widget)
   - Mode switch buttons with confirmation dialogs
   - Mode-specific window layouts
   - Settings: default mode on startup, auto-switch rules

**Deliverable**: Users can switch between 3 modes, data persists in SQLite, mode-specific telemetry collection works

---

### Phase 2: Setup Engineering Mode (2 weeks)

**Goal**: Full setup engineering workflow (comparison, ML recommendations, export)

**Tasks**:
1. **Setup Session Management** (2 days)
   - Create/load setup sessions
   - Track setup changes (diff before/after)
   - Save setup configurations to SQLite

2. **Lap Comparison Service** (3 days)
   - Compare baseline vs modified setup (t-test for statistical significance)
   - Sector-level analysis
   - Confidence scoring (95% threshold for "real" improvement)

3. **ML Integration** (3 days)
   - Load CatBoost/NN models from `%APPDATA%/MLModels/`
   - `MLModelService` for setup predictions
   - Feature extraction from telemetry
   - Recommendation generation

4. **Setup Engineering UI** (4 days)
   - Setup session window
   - Lap comparison charts (bar graphs, scatter plots)
   - Sector analysis table
   - ML recommendation panel
   - Export to CSV/JSON

**Deliverable**: Engineers can test setups, see objective comparisons, get ML recommendations, export data

---

### Phase 3: Strategy Scouting Mode (2 weeks)

**Goal**: Pre-race strategy planning with simulations

**Tasks**:
1. **Fuel/Tire Profiling** (3 days)
   - Track push vs fuel-save consumption
   - Tire degradation curve fitting (exponential model)
   - Store profiles in SQLite

2. **Multi-Stint Simulator** (4 days)
   - Calculate no-stop, 1-stop, 2-stop scenarios
   - Factor in tire deg, fuel requirements, pit stop time
   - Rank strategies by total time + feasibility

3. **Competitor Intelligence Integration** (2 days)
   - Observe other drivers' pit stops
   - Track competitor fuel/tire strategies
   - Predict opponent pit windows

4. **Strategy Scouting UI** (3 days)
   - Fuel profiling widget
   - Strategy comparison table
   - Tire deg chart
   - Export strategy PDF

**Deliverable**: Strategists can gather data, simulate strategies, export race plan

---

### Phase 4: Mode Integration & Polish (1 week)

**Goal**: Seamless workflow across all 3 modes

**Tasks**:
1. **Cross-Mode Data Flow** (2 days)
   - Load Setup Engineering results into Strategy Scouting
   - Load Strategy Scouting plan into Driving Mode
   - Pre-populate fuel targets, pit windows from scouting data

2. **UX Polish** (2 days)
   - Smooth mode transitions (fade animations)
   - Confirmation dialogs ("Unsaved data, continue?")
   - Keyboard shortcuts (Ctrl+1/2/3 for mode switch)

3. **Documentation & Help** (1 day)
   - In-app tooltips
   - Help documentation (how to use each mode)
   - Video tutorials (planned)

**Deliverable**: Complete 3-mode system with polished UX

---

### Phase 5: ML Model Integration & Testing (1 week)

**Goal**: Deploy your NN/CatBoost models into production

**Tasks**:
1. **Model Format Conversion** (1 day)
   - Convert your models to `.onnx` (NN) and `.cbm` (CatBoost)
   - Test inference speed (< 50ms per prediction)

2. **Feature Engineering** (2 days)
   - Map MRT telemetry to model features
   - Handle missing data gracefully
   - Normalize/scale inputs

3. **Prediction Quality Testing** (2 days)
   - Validate predictions against real session data
   - Calibrate confidence thresholds
   - A/B test with/without ML recommendations

**Deliverable**: ML models deployed, tested, providing accurate predictions

---

## Technical Considerations

### Performance Requirements

**Setup Engineering** (60 Hz telemetry):
- **Storage**: ~2 MB/minute per session (compressed)
- **Query Speed**: < 10ms for lap comparison (indexed by SetupId)
- **Memory**: < 500 MB for 1-hour session in RAM

**Strategy Scouting** (10 Hz aggregates):
- **Storage**: ~200 KB/minute per session
- **Simulation Speed**: < 100ms to calculate 3 strategies
- **Memory**: < 100 MB for profile data

**Driving Mode** (current):
- **No changes to existing performance** (keep current 60 Hz responsive)

### Telemetry Channel Definitions

#### Setup Engineering Mode Channels (48 total, 60Hz)

**Priority 1: Car Balance & Suspension (16 channels)**
```csharp
// Shock deflection/velocity (suspension travel analysis)
"LFshockDefl", "RFshockDefl", "LRshockDefl", "RRshockDefl"  // Meters
"LFshockVel", "RFshockVel", "LRshockVel", "RRshockVel"      // m/s

// Ride height (aero platform stability)
"LFrideHeight", "RFrideHeight", "LRrideHeight", "RRrideHeight" // Meters

// Roll/Pitch/Yaw (chassis dynamics)
"Roll", "RollRate"     // Banking angle + rate (radians, rad/s)
"Pitch", "PitchRate"   // Nose up/down + rate
"Yaw", "YawRate"       // Heading + rotation rate
```

**Priority 2: Tire Analysis (24 channels)**
```csharp
// Tire wear (% remaining across tire surface)
"LFwearL", "LFwearM", "LFwearR"  // Left/Middle/Right wear %
"RFwearL", "RFwearM", "RFwearR"
"LRwearL", "LRwearM", "LRwearR"
"RRwearL", "RRwearM", "RRwearR"

// Surface temps (3 points, immediate response to conditions)
"LFtempL", "LFtempM", "LFtempR"  // °C
"RFtempL", "RFtempM", "RFtempR"
"LRtempL", "LRtempM", "LRtempR"
"RRtempL", "RRtempM", "RRtempR"

// Tire pressure (current vs cold baseline)
"LFpressure", "LFcoldPressure"
"RFpressure", "RFcoldPressure"
"LRpressure", "LRcoldPressure"
"RRpressure", "RRcoldPressure"
```

**Priority 3: Forces & Dynamics (8 channels)**
```csharp
// G-forces (driver feel simulation)
"LongAccel"   // m/s² - Longitudinal (braking/accel, negative = braking)
"LatAccel"    // m/s² - Lateral (cornering)
"VertAccel"   // m/s² - Vertical (bumps/kerbs)

// Velocity components (for speed trace overlays)
"VelocityX", "VelocityY", "VelocityZ"  // m/s (3D velocity vector)

// Core telemetry
"Speed"       // m/s
"Throttle"    // 0.0-1.0 (normalized input)
```

**Sector Timing** (from SessionInfo YAML):
```yaml
# Parsed once at session start from SessionInfo YAML
SplitTimeInfo:
  Sectors:
   - SectorNum: 0
     SectorStartPct: 0.000000   # Start/Finish
   - SectorNum: 1
     SectorStartPct: 0.333333   # ~33% through lap
   - SectorNum: 2
     SectorStartPct: 0.666667   # ~66% through lap
```

#### Strategy Scouting Mode Channels (25 total, 10Hz)

**Priority 1: Fuel & Pit Strategy (11 channels)**
```csharp
// Fuel state
"FuelLevel"        // Liters remaining
"FuelLevelPct"     // % remaining (0.0-1.0)
"FuelUsePerHour"   // kg/h consumption rate

// Pit road detection
"OnPitRoad"        // bool - Player on pit road

// Pit service state (what's being serviced in pit box)
"PitSvFlags"       // uint - Pit service flags (tires, fuel, repairs)
"PitSvFuel"        // float - Fuel to add (L)
"PitSvLFP", "PitSvRFP", "PitSvLRP", "PitSvRRP"  // Tire pressure adjustments

// Pit repair times
"PitOptRepairLeft"  // float - Seconds remaining for optional repairs
"PitRepairLeft"     // float - Seconds remaining for mandatory repairs
```

**Priority 2: Tire Degradation (8 channels)**
```csharp
// Tire wear (for degradation modeling - middle point is average)
"LFwearM", "RFwearM", "LRwearM", "RRwearM"  // Middle wear % (0-100)

// Carcass temps (affects wear rate, more stable than surface temps)
"LFtempCM", "RFtempCM", "LRtempCM", "RRtempCM"  // °C (middle carcass)
```

**Priority 3: Lap Times & Position (6 channels)**
```csharp
// Timing
"LapCurrentLapTime"      // Current lap elapsed time (seconds)
"LapLastLapTime"         // Last completed lap time
"LapBestLapTime"         // Best lap time this session
"SessionTimeRemain"      // Session time remaining (seconds)

// Position (for traffic analysis during pit windows)
"LapDistPct"             // Track position (0.0-1.0)
"Lap"                    // Current lap number
```

**Competitor Arrays** (for pit strategy intelligence):
```csharp
"CarIdxLapDistPct"       // float[64] - All cars' track positions
"CarIdxOnPitRoad"        // bool[64] - Which cars are currently pitting
```

#### Storage Impact Analysis

**Setup Engineering** (60Hz × 48 channels × 5 min session):
- Data points: 60 Hz × 300s × 48 = 864,000 points
- Raw size: ~3.5 MB per 4-byte float
- Compressed: ~1.8 MB (gzip level 6)
- **5-lap comparison**: 1.8 MB × 2 setups = 3.6 MB total

**Strategy Scouting** (10Hz × 25 channels × 30 min session):
- Data points: 10 Hz × 1800s × 25 = 450,000 points
- Raw size: ~1.8 MB
- **Aggregates only**: 120 bytes/lap × 30 laps = 3.6 KB (**500× reduction!**)

**Why Aggregates Work for Strategy**:
- Don't need 60Hz tire temps, just average/min/max per lap
- Don't need 60Hz fuel level, just start/end per lap
- Don't need 60Hz speed, just average lap time
- Result: 2000 points/lap → 12 values/lap (tire temps avg×4, wear delta×4, fuel used, lap time, sector times×3)

### .exe Deployment Checklist

✅ **SQLite embedded** (zero external dependencies)  
✅ **ML models bundled** (ship `.cbm`/`.onnx` files with installer)  
✅ **%APPDATA% data directory** (user-writable, survives app updates)  
✅ **Graceful degradation** (if ML models missing, disable predictions)  
✅ **CSV/JSON export** (standard formats, no proprietary lock-in)  
✅ **Single .exe installer** (Inno Setup or WiX for Windows installer)  

### Testing Strategy

1. **Unit Tests**: Each service (fuel profiling, tire deg, ML predictions)
2. **Integration Tests**: Mode transitions, SQLite CRUD, telemetry collection
3. **End-to-End Tests**: Full workflow (Setup → Strategy → Driving)
4. **Performance Tests**: 60 Hz telemetry with no lag, SQLite query speed
5. **ML Model Tests**: Prediction accuracy, confidence calibration

---

## Next Steps

### Immediate Actions (This Week)

1. **Review this document** with team/stakeholders
2. **Prioritize Phase 1 tasks** (Mode Controller, SQLite)
3. **Set up development branch**: `feature/three-mode-architecture`
4. **Create GitHub issues** for Phase 1 tasks

### Decision Points

**Question 1**: Which mode to implement first after infrastructure?  
**Recommendation**: **Setup Engineering** (higher differentiation, easier to validate with A/B testing)

**Question 2**: How to handle ML model training/updates?  
**Recommendation**: Models ship with app, updates via separate installer or cloud download

**Question 3**: Should we support cloud sync for setup/strategy data?  
**Recommendation**: **Phase 6 feature** (after core 3 modes work standalone)

---

## Success Metrics

**Phase 1 Success** (Infrastructure):
- ✅ Mode switching works without crashes
- ✅ SQLite stores/retrieves data correctly
- ✅ Telemetry collection profiles work for all 3 modes

**Phase 2 Success** (Setup Engineering):
- ✅ Engineers can compare 2 setups with statistical confidence
- ✅ ML recommendations predict lap time delta within ±0.05s
- ✅ Export to CSV works for external analysis

**Phase 3 Success** (Strategy Scouting):
- ✅ Strategists can simulate 3+ strategies in < 2 seconds
- ✅ Fuel/tire profiling accurate within 5% of actual race data
- ✅ Export strategy PDF readable by team

**Overall Success** (3-Mode System):
- ✅ 80% of users try all 3 modes within first month
- ✅ Setup Engineering reduces setup tuning time by 30%
- ✅ Strategy Scouting improves race strategy confidence by 50%
- ✅ Zero performance regression in Driving Mode (existing users happy)

---

**End of Document**  
**Next Review**: After Phase 1 completion (estimate: 2 weeks)
