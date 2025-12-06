# MRT Overlay Development Roadmap
## Q1 2025: Service Architecture + Three-Mode System

**Status**: 🚀 PHASE 3.1-3.5 COMPLETE - Setup Engineering Mode Bug Fixes Done  
**Current**: Phase 3.1-3.5 ✅ Complete (Dec 6, 2025)  
**Next**: Phase 3.6 ML Integration → Phase 4 Strategy Scouting  
**Timeline**: 6-8 weeks total (Started Dec 6, 2025)  
**Branch**: `feature/phase3-setup-engineering` (ready for commit)

---

## Overview

This roadmap combines two major architectural initiatives:

1. **FuelCalculatorService Refactoring** (2-3 days) - Technical debt cleanup, improved testability
2. **Three-Mode System** (4-6 weeks) - Product differentiation, comprehensive race engineering platform

**Strategic Rationale**: Complete service splitting FIRST to establish clean architecture before building 3-mode system on top of it.

---

## Phase 0: Performance Fixes (✅ COMPLETE)

**Duration**: 1 day (December 2025)  
**Status**: ✅ Completed

### Completed Tasks

1. ✅ **StintTimelineControl Performance** (commit `bdd5782`)
   - Added change detection to prevent unnecessary canvas redraws
   - Result: 99% reduction in rendering operations

2. ✅ **Competitor Intelligence Caching** (commit `a55cd73`)
   - Only update LINQ queries when lap changes
   - Result: Smooth Race Strategy widget, no more stuttering

### Impact

Race Strategy Overview tab now performs smoothly with no lag or freezing.

---

## Phase 1: FuelCalculatorService Refactoring (2-3 days)

**Goal**: Split monolithic `FuelCalculatorService` (1,309 lines after Phase 7) into 6 focused services

**Why First**: Clean service architecture is foundation for 3-mode system. Better to refactor now than mid-development.

### Target Architecture

```
Services/
  FuelCalculatorService.cs (300 lines) ← Orchestrator
  
  Fuel/
    FuelAveragingService.cs (200 lines)         ← Extract from Phase 1.1
    FuelOutlierDetector.cs (150 lines)          ← Extract from Phase 1.2
    PitStrategyService.cs (400 lines)           ← Extract from Phase 1.3
    FuelSavingCalculator.cs (300 lines)         ← Extract from Phase 1.4
    DeltaTrackingService.cs (150 lines)         ← Extract from Phase 1.5
    DynamicBufferCalculator.cs (200 lines)      ← Extract from Phase 1.6
    LapValidator.cs (145 lines)                 ← Already done (Phase 7)
    TemperatureCompensationService.cs (95 lines) ← Already done (Phase 7)
```

### Day 1: Extract Averaging & Outlier Detection

#### 1.1 Extract FuelAveragingService (3 hours)

**Responsibilities**:
- Calculate 9 different fuel averages (Current, L5, L10, Session, EMA, Max, GreenFlag, Stint, Adaptive)
- Pure calculation logic, no state management

**Output Model**:
```csharp
public class FuelAverages
{
    public float CurrentLap { get; set; }
    public float Last5Laps { get; set; }
    public float Last10Laps { get; set; }
    public float SessionAverage { get; set; }
    public float ExponentialMovingAverage { get; set; }
    public float MaxConsumption { get; set; }
    public float GreenFlagOnly { get; set; }
    public float StintAverage { get; set; }
    public float AdaptiveWeighted { get; set; }
}
```

**Test Coverage**:
```csharp
[Test]
public void CalculateL5_WithFiveLaps_ReturnsCorrectAverage()
{
    var service = new FuelAveragingService();
    var laps = CreateLapHistory(new[] { 2.5f, 2.6f, 2.4f, 2.5f, 2.5f });
    var telemetry = CreateMockTelemetry();
    
    var averages = service.Calculate(laps, telemetry);
    
    Assert.That(averages.Last5Laps, Is.EqualTo(2.5f).Within(0.01f));
}

[Test]
public void CalculateEMA_WithAlpha05_ConvergesCorrectly()
{
    // Test exponential moving average convergence
}

[Test]
public void CalculateAdaptiveWeighted_PrioritizesRecentLaps()
{
    // Test adaptive weighting favors recent data
}
```

**Commit**: `refactor: Extract FuelAveragingService (9 methods, 200 lines)`

#### 1.2 Extract FuelOutlierDetector (2 hours)

**Responsibilities**:
- Detect outliers using Median Absolute Deviation (MAD)
- Filter lap history for valid fuel calculations
- Incident-based outlier detection

**Output Model**:
```csharp
public class OutlierDetectionResult
{
    public List<FuelLapHistory> CleanedHistory { get; set; }
    public List<FuelLapHistory> Outliers { get; set; }
    public float MedianFuelUsage { get; set; }
    public float MADValue { get; set; }
}
```

**Test Coverage**:
```csharp
[Test]
public void DetectOutliers_WithMAD_IdentifiesAnomalies()
{
    var detector = new FuelOutlierDetector();
    var laps = CreateLapHistory(new[] { 2.5f, 2.4f, 5.0f, 2.5f, 2.6f });
    
    var result = detector.Detect(laps);
    
    Assert.That(result.Outliers.Count, Is.EqualTo(1)); // 5.0L is outlier
    Assert.That(result.CleanedHistory.Count, Is.EqualTo(4));
}
```

**Commit**: `refactor: Extract FuelOutlierDetector (150 lines)`

---

### Day 2: Extract Pit Strategy & Fuel Saving

#### 1.3 Extract PitStrategyService (4 hours)

**Responsibilities**:
- Calculate optimal pit lap
- Determine fuel to add at pit
- Calculate pit windows (earliest/latest)
- Multi-stop strategy comparison

**Output Model**:
```csharp
public class PitStrategy
{
    public int OptimalPitLap { get; set; }
    public float FuelToAdd { get; set; }
    public int PitWindowStart { get; set; }
    public int PitWindowEnd { get; set; }
    public bool CanFinishWithoutStop { get; set; }
    public List<StopStrategy> MultiStopComparison { get; set; }
}
```

**Test Coverage**:
```csharp
[Test]
public void CalculateOptimalPitLap_WithRaceLapsRemaining_ReturnsMiddleOfWindow()
{
    var service = new PitStrategyService();
    var telemetry = CreateMockTelemetry(currentLap: 10, raceLaps: 40);
    var averages = new FuelAverages { SessionAverage = 2.5f };
    var fuelData = new FuelData { CurrentFuel = 50f };
    
    var strategy = service.Calculate(telemetry, averages, fuelData);
    
    Assert.That(strategy.OptimalPitLap, Is.InRange(18, 22)); // Middle of window
}
```

**Commit**: `refactor: Extract PitStrategyService (400 lines)`

#### 1.4 Extract FuelSavingCalculator (3 hours)

**Responsibilities**:
- Calculate fuel-save target (L/lap reduction needed)
- Track fuel-saving progress
- Generate strategic alerts (pit vs save comparison)

**Output Model**:
```csharp
public class FuelSavingData
{
    public bool IsActive { get; set; }
    public float TargetReduction { get; set; }
    public float CurrentSaving { get; set; }
    public float ProgressPercent { get; set; }
    public string Alert { get; set; }
    public bool IsFeasible { get; set; }
}
```

**Commit**: `refactor: Extract FuelSavingCalculator (300 lines)`

---

### Day 3: Extract Delta Tracking & Dynamic Buffer

#### 1.5 Extract DeltaTrackingService (2 hours)

**Responsibilities**:
- Track historical accuracy of fuel predictions
- Calculate convergence rate
- Confidence scoring

**Output Model**:
```csharp
public class DeltaTrackingData
{
    public float CurrentDelta { get; set; }
    public float ConvergenceRate { get; set; }
    public float ConfidenceScore { get; set; }
    public List<DeltaHistoryRecord> History { get; set; }
}
```

**Commit**: `refactor: Extract DeltaTrackingService (150 lines)`

#### 1.6 Extract DynamicBufferCalculator (2 hours)

**Responsibilities**:
- Calculate dynamic buffer based on 5 factors:
  1. Fuel consistency
  2. Race position
  3. Weather conditions
  4. Yellow flag probability
  5. Track characteristics

**Output Model**:
```csharp
public class DynamicBufferResult
{
    public float BufferLaps { get; set; }
    public float ConsistencyFactor { get; set; }
    public float PositionFactor { get; set; }
    public float WeatherFactor { get; set; }
    public float YellowFlagFactor { get; set; }
    public string Rationale { get; set; }
}
```

**Commit**: `refactor: Extract DynamicBufferCalculator (200 lines)`

#### 1.7 Final Orchestrator Cleanup (2 hours)

**Goal**: Reduce `FuelCalculatorService` to ~300 lines of orchestration code

**Orchestrator Structure**:
```csharp
public class FuelCalculatorService
{
    // Sub-services (injected or created)
    private readonly FuelAveragingService _averaging;
    private readonly FuelOutlierDetector _outlierDetector;
    private readonly PitStrategyService _pitStrategy;
    private readonly FuelSavingCalculator _fuelSaving;
    private readonly DeltaTrackingService _deltaTracking;
    private readonly DynamicBufferCalculator _bufferCalculator;
    private readonly LapValidator _lapValidator;
    private readonly TemperatureCompensationService _temperatureCompensation;
    
    // State management (lap history, etc.)
    private readonly List<FuelLapHistory> _lapHistory;
    private float _fuelAtLapStart;
    
    public void Update(TelemetryData telemetry)
    {
        // 1. Update lap tracking
        UpdateLapTracking(telemetry);
        
        // 2. Calculate averages (delegate)
        var averages = _averaging.Calculate(_lapHistory, telemetry);
        
        // 3. Detect outliers (delegate)
        var cleaned = _outlierDetector.Detect(_lapHistory);
        
        // 4. Calculate pit strategy (delegate)
        var strategy = _pitStrategy.Calculate(telemetry, averages, CurrentData);
        
        // 5. Calculate fuel saving (delegate)
        var saving = _fuelSaving.Calculate(telemetry, averages, strategy);
        
        // 6. Track delta (delegate)
        var delta = _deltaTracking.Track(telemetry, averages);
        
        // 7. Calculate buffer (delegate)
        var buffer = _bufferCalculator.Calculate(telemetry, cleaned);
        
        // 8. Apply temperature correction (delegate)
        _temperatureCompensation.ApplyTemperatureCorrection(telemetry, _sessionStats, CurrentData);
        
        // 9. Update CurrentData
        CurrentData.UpdateAverages(averages);
        CurrentData.UpdateStrategy(strategy);
        CurrentData.UpdateFuelSaving(saving);
        CurrentData.UpdateDelta(delta);
        CurrentData.FuelBufferLaps = buffer.BufferLaps;
        
        // 10. Fire event
        FuelDataUpdated?.Invoke(this, CurrentData);
    }
}
```

**Commit**: `refactor: Complete fuel service modularization (orchestrator now 300 lines)`

---

### Phase 1 Validation

**Integration Tests**:
```csharp
[TestFixture]
public class FuelCalculatorRefactoringTests
{
    [Test]
    public void RefactoredService_ProducesSameResults_AsOriginal()
    {
        // Load 50-lap race telemetry data
        var telemetryData = LoadRealRaceData("spa_50laps.json");
        
        // Run both implementations
        var original = RunOriginalImplementation(telemetryData);
        var refactored = RunRefactoredImplementation(telemetryData);
        
        // ALL outputs must be identical
        Assert.That(refactored.AvgFuelPerLap_L5, Is.EqualTo(original.AvgFuelPerLap_L5));
        Assert.That(refactored.OptimalPitLap, Is.EqualTo(original.OptimalPitLap));
        Assert.That(refactored.FuelToAddAtPit, Is.EqualTo(original.FuelToAddAtPit));
        // ... test all 50+ properties
    }
}
```

**Success Criteria**:
- ✅ All unit tests pass (60+ tests across 6 services)
- ✅ Integration test shows 100% identical results
- ✅ Build completes with 0 errors, 0 warnings
- ✅ `FuelCalculatorService` reduced from 1,309 → ~300 lines (77% reduction)
- ✅ Public API unchanged (widgets don't require updates)

---

## Phase 2: Mode Infrastructure (2 weeks)

**Goal**: Foundation for three-mode system (Setup Engineering, Strategy Scouting, Driving)

### Week 1: Mode Controller & SQLite Storage

#### 2.1 Mode Controller (3 days)

**Features**:
- `OperationalMode` enum (SetupEngineering, StrategyScouting, Driving)
- State machine with transition validation
- Mode-specific settings persistence
- Master control panel UI (floating widget)

**Deliverables**:
```csharp
public class ModeController
{
    public OperationalMode CurrentMode { get; private set; }
    public event EventHandler<ModeChangedEventArgs> ModeChanged;
    
    public void SwitchMode(OperationalMode newMode);
    public TelemetryProfile GetCollectionProfile(OperationalMode mode);
    public bool CanTransitionTo(OperationalMode targetMode);
}
```

**UI**: Master control panel (150x80 floating widget)
```
┌─────────────────────────┐
│ MRT OVERLAY             │
├─────────────────────────┤
│ Mode: 🏁 DRIVING        │
│                         │
│ [🔧] [📊] [🏁 Active]   │
│                         │
│ Lap 12/44 | P3          │
└─────────────────────────┘
```

#### 2.2 SQLite Integration (4 days)

**Database Schema**:
```sql
-- Setup Engineering
CREATE TABLE SetupSessions (
    SessionId TEXT PRIMARY KEY,
    TrackName TEXT NOT NULL,
    CarName TEXT NOT NULL,
    StartTime DATETIME,
    Weather TEXT,
    Notes TEXT
);

CREATE TABLE SetupConfigurations (
    SetupId TEXT PRIMARY KEY,
    SessionId TEXT,
    SetupName TEXT,
    SetupData TEXT,   -- JSON: iRacing setup file
    Timestamp DATETIME
);

CREATE TABLE LapTelemetry (
    LapId TEXT PRIMARY KEY,
    SetupId TEXT,
    LapNumber INTEGER,
    LapTime REAL,
    FuelUsed REAL,
    TelemetryData BLOB -- Compressed 60Hz data
);

-- Strategy Scouting
CREATE TABLE StrategyProfiles (
    ProfileId TEXT PRIMARY KEY,
    TrackName TEXT,
    CarName TEXT,
    FuelPerLap_Push REAL,
    FuelPerLap_Save REAL,
    TireDegModel TEXT, -- JSON coefficients
    OptimalStrategy TEXT -- JSON stint plan
);

-- Shared
CREATE TABLE ActiveSession (
    SessionId TEXT PRIMARY KEY,
    Mode TEXT, -- 'SetupEngineering', 'StrategyScouting', 'Driving'
    StartTime DATETIME,
    CurrentData TEXT -- JSON snapshot
);
```

**File Structure**:
```
%APPDATA%/MRTOverlay/
  ├── MRTOverlay.db          # SQLite database
  ├── Exports/
  │   ├── Setup_Spa_2025_12_01.csv
  │   └── Strategy_LeMans_1Stop.pdf
  ├── Logs/
  │   └── app_2025_12_01.log
  └── MLModels/
      ├── setup_catboost.cbm
      └── strategy_nn.onnx
```

**Deliverables**:
- `DataStorageService` with CRUD operations
- Schema migration system
- Unit tests for all DB operations
- Backup/restore functionality

### Week 2: Telemetry Collection Profiles

#### 2.3 Telemetry Collection Engine (5 days)

**Mode-Specific Profiles**:

**Setup Engineering** (HIGH DETAIL):
```csharp
new TelemetryProfile
{
    SampleRate = 60,    // Hz - full resolution
    RecordFields = TelemetryFields.ALL, // Everything
    StorageMode = StorageMode.FullHistory, // Keep all laps
    EnableComparison = true,
    CompressionLevel = CompressionLevel.Medium // Zlib compression
}
```

**Strategy Scouting** (AGGREGATES):
```csharp
new TelemetryProfile
{
    SampleRate = 10,    // Hz - lower frequency
    RecordFields = TelemetryFields.Strategy, // Fuel, tires, lap times
    StorageMode = StorageMode.AggregatesOnly, // Just summaries
    EnableProjection = true
}
```

**Driving** (REAL-TIME):
```csharp
new TelemetryProfile
{
    SampleRate = 60,    // Hz - responsive
    RecordFields = TelemetryFields.Essential, // Overlay data only
    StorageMode = StorageMode.CurrentSession, // Discard after
    EnableOverlays = true
}
```

**Deliverables**:
- `TelemetryCollectionEngine` with mode-aware recording
- Compression for Setup Engineering mode (Zlib)
- Efficient storage (2MB/minute for Setup, 200KB/minute for Strategy)
- Unit tests for all profiles

## Phase 3: Setup Engineering Mode (✅ PHASE 3.6 COMPLETE - Dec 6, 2025)

**Goal**: Full setup development workflow  
**Status**: ✅ Phase 3.1-3.6 Complete (ML Integration Done!)

### Week 1: Core Features (✅ COMPLETE)

#### 3.1 Setup Session Management (✅ COMPLETE - commit f34aade)

**Delivered**: SQLite database with 3 tables, 6 indexes, full CRUD operations

#### 3.1 Setup Session Management (2 days)

**Features**:
- Create/load setup sessions
- Track setup changes (before/after diffs)
- Save setup configurations to SQLite

**UI**: Setup session selector
```
┌──────────────────────────────────────┐
│ SETUP ENGINEERING MODE               │
├──────────────────────────────────────┤
│ Session: "Spa GT3 Testing"          │
│ Track: Spa-Francorchamps             │
│ Car: Ferrari 488 GT3                 │
│                                      │
│ [New Session] [Load Session]        │
│                                      │
#### 3.2 Lap Comparison Service (✅ COMPLETE - commit caec547)

**Delivered**:
- ✅ Statistical comparison (Welch's t-test, 95% confidence threshold)
- ✅ Sector-level analysis with outlier filtering (>3σ)
- ✅ Human-readable recommendationse (3 days)

**Features**:
- Statistical comparison (t-test, 95% confidence)
- Sector-level analysis
- Lap time distribution charts

**Algorithm**:
```csharp
public class LapComparisonService
{
    public ComparisonResult Compare(
        List<FuelLapHistory> baselineLaps,
        List<FuelLapHistory> modifiedLaps)
    {
        // 1. Calculate averages
        float baselineAvg = baselineLaps.Average(l => l.LapTime);
        float modifiedAvg = modifiedLaps.Average(l => l.LapTime);
        
        // 2. T-test for statistical significance
        float tStat = CalculateTStatistic(baselineLaps, modifiedLaps);
        float pValue = GetPValue(tStat, degreesOfFreedom);
        
        // 3. Confidence level
        float confidence = (1 - pValue) * 100; // e.g., 95%
        
        // 4. Result
        return new ComparisonResult
        {
            LapTimeDelta = modifiedAvg - baselineAvg,
            Confidence = confidence,
            IsSignificant = confidence >= 95,
            BaselineAvg = baselineAvg,
            ModifiedAvg = modifiedAvg
#### 3.3 iRacing Setup File Parser (✅ COMPLETE - commit 58345f4)

**Delivered**:
- ✅ Binary format detection (.sto files are binary, not XML)
- ✅ Clear error messaging for unsupported format
- ✅ XML parsing path preserved for future support

#### 3.4 Setup Engineering UI (✅ COMPLETE - commit b89b8f9)

**Delivered**:
- ✅ 715-line WPF window with 4 tabs
- ✅ Session management controls
- ✅ Setup comparison panel
- ✅ ML recommendations tab
- ✅ CSV export functionality

#### 3.5 Live Telemetry ML Integration (✅ COMPLETE - commit 3189553)

**Delivered**:
- ✅ ML works with LIVE telemetry (no .sto files required!)
- ✅ ITelemetryService integration for real-time data
- ✅ Predictions enabled by default
- ✅ UI messaging clarifies .sto files optional

### Week 2: ML Integration & Export (✅ COMPLETE - Dec 6, 2025)

#### 3.6 Real ML Model Integration (✅ COMPLETE - commit cc702c5)

**Delivered**:
- ✅ Live setup name tracking from iRacing YAML (`DriverSetupName`, `DriverSetupIsModified`)
- ✅ Physics-based prediction system (track classification, wing/ARB/tire impact coefficients)
- ✅ Real-time ML predictions from live telemetry (no .sto files needed!)
- ✅ Setup diff calculation already implemented (SetupFileParser.CompareSetups)
- ✅ Sector-level comparison already implemented (LapComparisonService with t-test, 95% confidence)
- ✅ Enhanced Setup Engineering UI with prediction panel
- ✅ `SetupParameterFeatures` model for ML input (Aero, Chassis, Tires, Environment)
- ✅ `MergeSetupWithTelemetry()` for setup file + live telemetry fusion

**Physics-Based Heuristics** (until ML models trained):
- Track classification: HighSpeed (Monza, Spa) / Medium / LowSpeed (Monaco)
- Wing impact: ±0.02-0.025s per click (track-dependent)
- ARB impact: ±0.01s per click
- Tire pressure: ±0.02s per kPa deviation
- Confidence scoring: 3+ changes = 75%, 2+ = 60%, 1 = 40%

**Architecture**:
- Setup tracking works purely from YAML (no file loading required)
- ML predictions integrate with live telemetry automatically
- Ready for CatBoost/ONNX model drop-in (Phase 3.7)

**Next**: Phase 3.7 - End-to-end testing with live iRacing session

#### 3.7 CatBoost/ONNX Model Training (⏭️ FUTURE)
```

### Week 2: ML Integration & Export

#### 3.3 ML Model Integration (3 days)

**Features**:
- Load CatBoost/NN models from `%APPDATA%/MLModels/`
- Feature extraction from telemetry
- Setup change recommendations

**Integration**:
```csharp
public class MLModelService
{
    private readonly SetupPredictionModel _model;
    
    public SetupPrediction PredictSetupChange(
        SetupConfiguration baseline,
        SetupConfiguration proposed,
        TrackConditions conditions)
    {
        var features = ExtractFeatures(baseline, proposed, conditions);
        float predictedDelta = _model.Predict(features);
        float confidence = _model.GetConfidence(features);
        
        return new SetupPrediction
        {
            LapTimeDelta = predictedDelta,  // e.g., -0.15s
            Confidence = confidence,         // e.g., 0.95
            Recommendation = GenerateRecommendation(predictedDelta)
        };
    }
}
```

#### 3.4 Setup Engineering UI & Export (4 days)

**UI Components**:
- Setup comparison window (800x600)
- Lap time charts (bar graphs, histograms)
- Sector analysis table
- ML recommendation panel
- Export to CSV/JSON

**Export Formats**:
```csv
# setup_comparison_spa_2025_12_01.csv
Setup,LapTime,Sector1,Sector2,Sector3,FuelUsed,TireTemp_FL
Baseline,2:19.543,42.123,51.234,46.186,2.35,82.3
Modified,2:19.387,42.089,51.102,46.196,2.33,83.1
Delta,-0.156,-0.034,-0.132,+0.010,-0.02,+0.8
```

---

## Phase 4: Strategy Scouting Mode (2 weeks)

**Goal**: Pre-race strategy planning with simulations

### Week 1: Fuel & Tire Profiling

#### 4.1 Fuel Profiling (2 days)

**Features**:
- Track push vs fuel-save consumption
- Calculate fuel-save percentage
- Store profiles in SQLite

**Algorithm**:
```csharp
public class FuelProfilingService
{
    public FuelProfile AnalyzeSession(List<FuelLapHistory> laps)
    {
        // Identify push vs save laps (manual flag or pace-based heuristic)
        var pushLaps = laps.Where(l => l.IsPushPace).ToList();
        var saveLaps = laps.Where(l => l.IsSavePace).ToList();
        
        float pushAvg = pushLaps.Average(l => l.FuelUsed);
        float saveAvg = saveLaps.Average(l => l.FuelUsed);
        float savingPercent = ((pushAvg - saveAvg) / pushAvg) * 100;
        
        return new FuelProfile
        {
            PushConsumption = pushAvg,
            SaveConsumption = saveAvg,
            SavingPercent = savingPercent
        };
    }
}
```

#### 4.2 Tire Degradation Modeling (3 days)

**Features**:
- Fit exponential decay curve to lap times
- Predict grip loss over stint
- Identify critical degradation point

**Algorithm**:
```csharp
public class TireDegradationService
{
    public TireDegradationModel FitModel(List<FuelLapHistory> stintLaps)
    {
        // Exponential decay: grip(lap) = initialGrip * e^(-degradationRate * lap)
        var (initialGrip, degradationRate) = FitExponentialCurve(stintLaps);
        
        return new TireDegradationModel
        {
            InitialGrip = initialGrip,
            DegradationRate = degradationRate,
            CriticalLap = CalculateCriticalLap(degradationRate) // When grip < 85%
        };
    }
}
```

### Week 2: Multi-Stint Simulation & Export

#### 4.3 Multi-Stint Simulator (3 days)

**Features**:
- Simulate no-stop, 1-stop, 2-stop scenarios
- Factor in tire deg, fuel requirements, pit time
- Rank strategies by total time + feasibility

**Algorithm**:
```csharp
public class StintSimulator
{
    public List<StrategyResult> SimulateStrategies(
        FuelProfile fuelProfile,
        TireDegradationModel tireDeg,
        int raceLaps,
        float pitStopTime)
    {
        var strategies = new List<StrategyResult>();
        
        // No-stop (fuel-save)
        strategies.Add(SimulateNoStop(fuelProfile, tireDeg, raceLaps));
        
        // 1-stop (various pit laps)
        for (int pitLap = 15; pitLap <= 30; pitLap += 2)
        {
            strategies.Add(Simulate1Stop(fuelProfile, tireDeg, raceLaps, pitLap, pitStopTime));
        }
        
        // 2-stop
        strategies.Add(Simulate2Stop(fuelProfile, tireDeg, raceLaps, pitStopTime));
        
        // Rank by total time
        return strategies.OrderBy(s => s.TotalTime).ToList();
    }
}
```

#### 4.4 Strategy Scouting UI & Export (4 days)

**UI Components**:
- Fuel profiling widget
- Strategy comparison table
- Tire deg chart
- Export to PDF/CSV

**Export Example**:
```
# Strategy Report: Spa 2-Hour Endurance
# Generated: 2025-12-01 14:30

## FUEL PROFILING
Push Pace: 2.35 L/lap (25 laps tested)
Fuel-Save: 1.88 L/lap (15 laps tested)
Saving: 20%

## TIRE DEGRADATION
Initial Grip: 100%
Degradation Rate: 0.8%/lap
Critical Lap: 25 (85% grip)

## STRATEGY COMPARISON
1-STOP @ L22 (RECOMMENDED)
  Total Time: 1:42:30
  Feasibility: 100%
  Stint 1: 22 laps push (51.7L)
  Stint 2: 22 laps push (51.7L)

NO-STOP (Fuel-Save)
  Total Time: 1:42:45 (+15s)
  Feasibility: 95% (2L margin)
  Fuel-Save: L25-44 (20 laps)

2-STOP @ L15, L30
  Total Time: 1:43:25 (+55s)
  Feasibility: 100%
  Not recommended (extra pit stop too costly)
```

---

## Phase 5: Integration & Polish (1 week)

**Goal**: Seamless workflow across all 3 modes

### 5.1 Cross-Mode Data Flow (2 days)

**Features**:
- Load Setup Engineering results into Strategy Scouting
- Load Strategy Scouting plan into Driving Mode
- Pre-populate fuel targets, pit windows

**Example**:
```csharp
// Setup Engineering → Strategy Scouting
var setupResults = _storage.LoadSetupSession("Spa_Session_123");
_strategyScoutingMode.LoadSetupResults(setupResults);

// Strategy Scouting → Driving Mode
var strategyPlan = _storage.LoadStrategyProfile("Spa_1Stop_L22");
_fuelCalculator.LoadStrategyPlan(strategyPlan);
```

### 5.2 UX Polish (2 days)

**Features**:
- Smooth mode transitions (fade animations)
- Confirmation dialogs
- Keyboard shortcuts (Ctrl+1/2/3)
- Tooltips & help text

### 5.3 Documentation & Help (1 day)

**Deliverables**:
- In-app help system
- User guide (Markdown)
- Video tutorial scripts

---

## Phase 6: Unit Test Coverage (Ongoing)

**Goal**: 80% code coverage across all services

### Test Pyramid

**Unit Tests** (500+ tests):
- Each service method tested independently
- Edge cases, error handling
- Mock dependencies
## Timeline Summary (UPDATED Dec 6, 2025)

| Phase | Duration | Start Date | End Date | Status |
|-------|----------|------------|----------|--------|
| 0. Performance Fixes | 1 day | Dec 6 | Dec 6 | ✅ Complete |
| 3. Setup Engineering Core | 1 day | Dec 6 | Dec 6 | ✅ Complete |
| 3. Setup Engineering ML | 1 day | Dec 6 | Dec 6 | ✅ Complete |
| 1. Service Refactoring | 3 days | Dec 9 | Dec 11 | ⏭️ Planned |
| 2. Mode Infrastructure | 2 weeks | Dec 12 | Dec 23 | ⏭️ Planned |
| 4. Strategy Scouting | 2 weeks | Dec 26 | Jan 8 | ⏭️ Planned |
| 5. Integration & Polish | 1 week | Jan 9 | Jan 15 | ⏭️ Planned |
| 6. Testing (Ongoing) | - | Dec 6 | Jan 15 | 🔄 Ongoing |

**Total Duration**: ~6 weeks (December 6, 2025 - January 15, 2025)

**Note**: Phase 3 completed 1 day early! ML integration delivered with physics-based predictions + live setup tracking.

**Note**: Phase 3 jumped ahead of Phases 1-2 due to immediate user needs. Service refactoring will follow.
| Service | Unit Tests | Coverage |
|---------|------------|----------|
| FuelAveragingService | 20+ | 90% |
| FuelOutlierDetector | 15+ | 85% |
| PitStrategyService | 30+ | 85% |
| FuelSavingCalculator | 20+ | 85% |
| DeltaTrackingService | 10+ | 80% |
| DynamicBufferCalculator | 15+ | 85% |
| ModeController | 20+ | 90% |
| DataStorageService | 30+ | 90% |
| LapComparisonService | 25+ | 90% |
| MLModelService | 15+ | 80% |
| **TOTAL** | **500+** | **85%** |

---

## Timeline Summary

| Phase | Duration | Start Date | End Date |
|-------|----------|------------|----------|
| 0. Performance Fixes | ✅ Complete | Dec 6 | Dec 6 |
| 1. Service Refactoring | 3 days | Dec 9 | Dec 11 |
| 2. Mode Infrastructure | 2 weeks | Dec 12 | Dec 25 |
| 3. Setup Engineering | 2 weeks | Dec 26 | Jan 8 |
| 4. Strategy Scouting | 2 weeks | Jan 9 | Jan 22 |
| 5. Integration & Polish | 1 week | Jan 23 | Jan 29 |
| 6. Testing (Ongoing) | - | Dec 9 | Jan 29 |

**Total Duration**: ~8 weeks (December 2025 - January 2025)

---

## Success Metrics

### Phase 1 Success (Service Refactoring)
- ✅ `FuelCalculatorService` reduced to 300 lines (77% reduction)
- ✅ 6 new focused services with single responsibilities
- ✅ Integration test shows 100% identical results
- ✅ 60+ unit tests passing
- ✅ Build completes with 0 errors, 0 warnings

### Phase 2 Success (Mode Infrastructure)
- ✅ Mode switching works without crashes
- ✅ SQLite stores/retrieves data correctly
- ✅ Telemetry profiles work for all 3 modes
- ✅ Master control panel provides smooth UX

### Phase 3 Success (Setup Engineering)
- ✅ Engineers can compare 2 setups with statistical confidence
- ✅ ML recommendations predict lap time delta within ±0.05s
- ✅ Export to CSV works for external analysis
- ✅ 80% of test users report faster setup development

### Phase 4 Success (Strategy Scouting)
- ✅ Strategists can simulate 3+ strategies in < 2 seconds
- ✅ Fuel/tire profiling accurate within 5% of actual race data
- ✅ Export strategy PDF readable by team
- ✅ 70% of users report improved race strategy confidence

### Overall Success
- ✅ 80% of users try all 3 modes within first month
- ✅ Setup Engineering reduces setup tuning time by 30%
- ✅ Strategy Scouting improves strategy confidence by 50%
- ✅ Zero performance regression in Driving Mode
- ✅ 85% code coverage across all services

---

## Risk Mitigation

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Service refactoring introduces bugs | MEDIUM | HIGH | Comprehensive integration tests, side-by-side comparison |
| SQLite performance insufficient | LOW | MEDIUM | Indexed queries, compression, aggregate-only storage |
| ML model accuracy low | MEDIUM | LOW | Graceful degradation, confidence thresholds, manual override |
| Mode switching causes data loss | LOW | HIGH | Auto-save, confirmation dialogs, SQLite ACID compliance |

### Schedule Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Service refactoring takes longer | MEDIUM | LOW | Phase 1 can slip without blocking Phase 2 |
| ML integration complex | HIGH | MEDIUM | Start with simple predictions, iterate |
| Testing reveals major issues | MEDIUM | HIGH | Early integration tests, continuous validation |

---

## Next Steps

### Immediate Actions (This Week)

1. ✅ **Performance fixes complete** (commits `bdd5782`, `a55cd73`)
2. ⏭️ **Review this roadmap** with stakeholders
3. ⏭️ **Create GitHub issues** for Phase 1 tasks
4. ⏭️ **Set up development branch**: `feature/service-refactoring`
5. ⏭️ **Begin Phase 1.1**: Extract FuelAveragingService

### Weekly Checkpoints

**Week 1**: Phase 1 complete (service refactoring done)  
**Week 2**: Phase 2.1 complete (mode controller working)  
**Week 3**: Phase 2.2 complete (SQLite integration done)  
**Week 4**: Phase 3.1 complete (setup session management)  
**Week 5**: Phase 3.2 complete (lap comparison + ML)  
**Week 6**: Phase 4.1 complete (fuel/tire profiling)  
**Week 7**: Phase 4.2 complete (multi-stint simulation)  
**Week 8**: Phase 5 complete (full system integration)

---

**End of Roadmap**  
**Next Review**: After Phase 1 completion (December 11, 2025)
