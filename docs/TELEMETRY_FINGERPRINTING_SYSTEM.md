# Telemetry Fingerprinting & Historical Data System

**Document Version**: 1.0  
**Date**: October 24, 2025  
**Status**: 🔬 Research & Design  
**Purpose**: System to identify racing situations and use historical data for better initial predictions

---

## 📋 Executive Summary

**Goal**: Create a "fingerprinting" system that:
1. Uniquely identifies racing situations (car + track + weather + setup)
2. Stores aggregated performance data (fuel consumption, lap times, tire wear)
3. Uses historical data to provide accurate predictions from lap 0-3 (before live data is sufficient)

**Benefits**:
- ✅ **Instant accurate fuel predictions** at race start (no more "waiting for 3 laps")
- ✅ **Track-specific pit strategy** from past sessions
- ✅ **Weather-adjusted calculations** (hot vs cold track)
- ✅ **Setup-aware predictions** (different fuel maps, downforce levels)
- ✅ **Learning system** that gets smarter with every session

**Estimated Implementation**: 2-3 days (database + service + UI)

---

## 🔑 Fingerprint Components

### Tier 1: Session Fingerprint (Exact Match)

A unique combination of factors that define "the same situation":

| Component | Source | Example | Match Tolerance |
|-----------|--------|---------|-----------------|
| **Car Name** | SessionInfo YAML | `"dallara il15"` | Exact match required |
| **Track Name** | SessionInfo YAML | `"lagunaseca"` | Exact match required |
| **Track Config** | SessionInfo YAML | `"full"` or `"short"` | Exact match required |
| **Session Type** | SessionInfo YAML | `"Race"`, `"Practice"`, `"Qualify"` | Exact match required |
| **Track Temp** | WeekendInfo YAML | `21.3°C` | ±5°C tolerance |
| **Air Temp** | WeekendInfo YAML | `18.3°C` | ±5°C tolerance |
| **Track Surface** | SessionInfo YAML | `"moderately low"` | String match |
| **Weather Type** | WeekendInfo YAML | `"Static"` vs `"Dynamic"` | Exact match |

**Fingerprint Hash Example**:
```
SHA256("dallara_il15|lagunaseca|full|Race|21.3|18.3|mod_low|static")
= "a3f5e9d2..."
```

### Tier 2: Fuzzy Match (Fallback)

If no exact match found, fallback to:
1. **Same car + track** (any weather)
2. **Same car + similar track length** (±10%)
3. **Same car class** (any track)

### Tier 3: User Setup Factors (Optional - Advanced)

These are harder to extract but provide even better accuracy:

| Factor | Source | Impact | Feasibility |
|--------|--------|--------|-------------|
| **Fuel Map Setting** | CarSetup YAML | High (±5% fuel/lap) | ✅ Easy |
| **Downforce Level** | CarSetup YAML | Medium (±2% fuel/lap) | ✅ Easy |
| **Gear Ratios** | CarSetup YAML | Low (±1% fuel/lap) | ✅ Easy |
| **Brake Bias** | CarSetup YAML | Negligible | ❌ Skip |
| **Weight Distribution** | CarSetup YAML | Negligible | ❌ Skip |

**Recommendation**: Start with Tier 1 only, add Tier 3 in Phase 2.

---

## 💾 Data Storage Strategy

### Option A: SQLite Database (✅ **RECOMMENDED**)

**Pros**:
- Fast indexed queries for fingerprint matching
- Efficient aggregation (AVG, MIN, MAX, COUNT)
- Small file size (~1-10 MB for 1000 sessions)
- Transaction support for data integrity
- No external dependencies (.NET includes SQLite)

**Cons**:
- Slightly more complex than JSON
- Requires SQL knowledge for queries

**Schema Design**:

```sql
-- Table 1: Session Fingerprints (unique combos)
CREATE TABLE SessionFingerprints (
    FingerprintID INTEGER PRIMARY KEY AUTOINCREMENT,
    FingerprintHash TEXT UNIQUE NOT NULL,  -- SHA256 hash of combo
    CarName TEXT NOT NULL,
    TrackName TEXT NOT NULL,
    TrackConfig TEXT NOT NULL,
    SessionType TEXT NOT NULL,
    TrackTempC REAL NOT NULL,
    AirTempC REAL NOT NULL,
    TrackSurface TEXT,
    WeatherType TEXT,
    CreatedDate TEXT NOT NULL,
    LastUsedDate TEXT NOT NULL,
    SessionCount INTEGER DEFAULT 1,
    INDEX idx_fingerprint (FingerprintHash),
    INDEX idx_car_track (CarName, TrackName)
);

-- Table 2: Session History (individual sessions)
CREATE TABLE SessionHistory (
    SessionID INTEGER PRIMARY KEY AUTOINCREMENT,
    FingerprintID INTEGER NOT NULL,
    SessionDate TEXT NOT NULL,
    SessionDuration REAL,  -- minutes
    TotalLaps INTEGER,
    ValidLaps INTEGER,     -- non-pit, non-formation laps
    
    -- Fuel Data
    AvgFuelPerLap REAL,
    MinFuelPerLap REAL,
    MaxFuelPerLap REAL,
    StdDevFuelPerLap REAL,
    TotalFuelUsed REAL,
    
    -- Lap Times
    AvgLapTime REAL,
    FastestLapTime REAL,
    ConsistencyPct REAL,   -- std dev / avg
    
    -- Pit Strategy
    PitStops INTEGER,
    AvgPitLossTime REAL,   -- time lost in pit (entry to exit)
    OptimalPitLap INTEGER, -- when did user actually pit
    
    -- Track Learning
    PitEntryLapDistPct REAL,  -- learned pit entry point
    PitExitLapDistPct REAL,   -- learned pit exit point
    
    -- Tire Wear (optional - Phase 2)
    AvgTireWearPerLap REAL,
    
    -- Weather (for dynamic sessions)
    WeatherChanged INTEGER DEFAULT 0,  -- bool: did weather change during session
    
    FOREIGN KEY (FingerprintID) REFERENCES SessionFingerprints(FingerprintID)
);

-- Table 3: Aggregated Statistics (pre-computed for fast access)
CREATE TABLE FingerprintStats (
    FingerprintID INTEGER PRIMARY KEY,
    
    -- Fuel Statistics (weighted average across all sessions)
    WeightedAvgFuelPerLap REAL,
    ConfidenceScore REAL,  -- 0-100%, based on sample size and consistency
    RecommendedBufferLaps REAL,
    
    -- Lap Time Statistics
    WeightedAvgLapTime REAL,
    TypicalFastestLap REAL,
    
    -- Pit Strategy
    RecommendedPitLap INTEGER,
    AvgPitLossTime REAL,
    
    -- Data Quality
    TotalSessions INTEGER,
    TotalLaps INTEGER,
    LastUpdated TEXT,
    
    FOREIGN KEY (FingerprintID) REFERENCES SessionFingerprints(FingerprintID)
);
```

**File Location**: `%USERPROFILE%\Documents\MRT-UI\telemetry.db`

**Size Estimate**:
- 1 fingerprint + 10 sessions + stats = ~2 KB
- 500 unique combos × 2 KB = **1 MB total**
- Very manageable!

### Option B: JSON Files (Simpler but slower)

**Pros**:
- Easy to read/debug (human-readable)
- No SQL knowledge required
- Easy backups (just copy folder)

**Cons**:
- Slow for large datasets (need to load entire file to search)
- No indexing (O(n) search for matches)
- Manual aggregation calculations

**File Structure**:
```
Documents\MRT-UI\history\
    dallara_il15\
        lagunaseca_full\
            race_21C_18C_modlow_static.json
            race_25C_22C_modlow_static.json
        watkinsglen_classic\
            race_20C_17C_modlow_static.json
```

**JSON Format**:
```json
{
  "fingerprint": {
    "car": "dallara il15",
    "track": "lagunaseca",
    "config": "full",
    "sessionType": "Race",
    "trackTempC": 21.3,
    "airTempC": 18.3,
    "trackSurface": "moderately low",
    "weatherType": "Static"
  },
  "sessions": [
    {
      "date": "2025-10-24T14:30:00Z",
      "laps": 25,
      "avgFuelPerLap": 2.45,
      "avgLapTime": 75.3,
      "pitLap": 13
    }
  ],
  "aggregated": {
    "weightedAvgFuel": 2.42,
    "confidence": 85.0,
    "totalSessions": 5,
    "totalLaps": 127
  }
}
```

**Recommendation**: Use JSON for MVP, migrate to SQLite if dataset grows >100 unique fingerprints.

---

## 🔄 Data Collection Workflow

### Phase 1: Session Start (Fingerprint Creation)

```csharp
// When telemetry connects
public void OnTelemetryConnected()
{
    // 1. Parse SessionInfo YAML (one-time)
    var sessionInfo = ParseSessionInfo();
    
    // 2. Extract fingerprint components
    var fingerprint = new SessionFingerprint
    {
        CarName = sessionInfo.CarName,
        TrackName = sessionInfo.TrackName,
        TrackConfig = sessionInfo.TrackConfig,
        SessionType = sessionInfo.SessionType,
        TrackTempC = sessionInfo.TrackTempC,
        AirTempC = sessionInfo.AirTempC,
        TrackSurface = sessionInfo.TrackSurface,
        WeatherType = sessionInfo.WeatherType
    };
    
    // 3. Generate hash
    fingerprint.Hash = GenerateFingerprint(fingerprint);
    
    // 4. Query historical data
    var history = _historyService.GetHistory(fingerprint);
    
    // 5. If found, initialize fuel calculator with historical average
    if (history != null && history.Confidence > 70)
    {
        _fuelCalculator.InitializeFromHistory(
            avgFuelPerLap: history.WeightedAvgFuel,
            confidence: history.Confidence,
            recommendedBuffer: history.RecommendedBuffer
        );
        
        _logger.LogInformation(
            "Initialized from history: {AvgFuel:F2}L/lap ({Sessions} sessions, {Confidence}% confidence)",
            history.WeightedAvgFuel, history.TotalSessions, history.Confidence
        );
    }
}
```

### Phase 2: Live Session (Data Collection)

```csharp
// Every lap completion
public void OnLapCompleted(LapData lap)
{
    // Store lap in temporary buffer
    _currentSessionLaps.Add(lap);
    
    // Update fuel calculator with live data (overrides historical after 3 laps)
    _fuelCalculator.Update(lap);
}
```

### Phase 3: Session End (Data Persistence)

```csharp
// When telemetry disconnects or session ends
public void OnSessionEnded()
{
    // 1. Filter valid laps (exclude pit laps, formation laps)
    var validLaps = _currentSessionLaps
        .Where(l => !l.IsPitLap && !l.IsFormationLap && l.FuelUsed > 0)
        .ToList();
    
    if (validLaps.Count < 3)
    {
        _logger.LogWarning("Not enough valid laps to save history ({Count})", validLaps.Count);
        return;
    }
    
    // 2. Calculate session statistics
    var stats = new SessionStats
    {
        TotalLaps = validLaps.Count,
        AvgFuelPerLap = validLaps.Average(l => l.FuelUsed),
        MinFuelPerLap = validLaps.Min(l => l.FuelUsed),
        MaxFuelPerLap = validLaps.Max(l => l.FuelUsed),
        StdDevFuelPerLap = CalculateStdDev(validLaps.Select(l => l.FuelUsed)),
        AvgLapTime = validLaps.Average(l => l.LapTime),
        FastestLapTime = validLaps.Min(l => l.LapTime)
    };
    
    // 3. Save to database
    _historyService.SaveSession(_currentFingerprint, stats);
    
    _logger.LogInformation(
        "Session saved: {Laps} laps, {AvgFuel:F2}L/lap avg",
        stats.TotalLaps, stats.AvgFuelPerLap
    );
}
```

---

## 🧮 Historical Data Usage

### Confidence Scoring Algorithm

The confidence score (0-100%) determines how much we trust historical data:

```csharp
public float CalculateConfidence(FingerprintStats stats)
{
    float confidence = 0;
    
    // Factor 1: Sample Size (50% weight)
    // More sessions = more confidence
    int sessions = stats.TotalSessions;
    int laps = stats.TotalLaps;
    
    if (laps >= 100)
        confidence += 50;  // 100+ laps = full confidence
    else if (laps >= 50)
        confidence += 40;  // 50-99 laps = high confidence
    else if (laps >= 20)
        confidence += 30;  // 20-49 laps = medium confidence
    else if (laps >= 10)
        confidence += 20;  // 10-19 laps = low confidence
    else
        confidence += 10;  // <10 laps = very low confidence
    
    // Factor 2: Consistency (30% weight)
    // Lower std dev = more predictable = higher confidence
    float consistencyScore = 1.0f - Math.Min(stats.StdDevFuelPerLap / stats.WeightedAvgFuelPerLap, 1.0f);
    confidence += consistencyScore * 30;
    
    // Factor 3: Recency (20% weight)
    // More recent data = more relevant (setup changes, game updates)
    var daysSinceLastUse = (DateTime.Now - stats.LastUpdated).TotalDays;
    if (daysSinceLastUse <= 7)
        confidence += 20;  // Used this week
    else if (daysSinceLastUse <= 30)
        confidence += 15;  // Used this month
    else if (daysSinceLastUse <= 90)
        confidence += 10;  // Used this quarter
    else
        confidence += 5;   // Older than 3 months
    
    return Math.Min(confidence, 100);
}
```

### Weighted Average Calculation

Give more weight to recent sessions:

```csharp
public float CalculateWeightedAverage(List<SessionHistory> sessions)
{
    // Sort by date (newest first)
    sessions = sessions.OrderByDescending(s => s.SessionDate).ToList();
    
    float weightedSum = 0;
    float totalWeight = 0;
    
    for (int i = 0; i < sessions.Count; i++)
    {
        // Exponential decay: newest = 1.0, 2nd = 0.9, 3rd = 0.81, etc.
        float weight = (float)Math.Pow(0.9, i);
        
        weightedSum += sessions[i].AvgFuelPerLap * weight;
        totalWeight += weight;
    }
    
    return weightedSum / totalWeight;
}
```

### Blending Historical + Live Data

Gradually transition from historical to live data:

```csharp
public float GetBlendedFuelAverage(int currentLap)
{
    if (currentLap == 0)
        return _historicalAvg;  // 100% historical
    
    if (currentLap >= 5)
        return _liveAvg;  // 100% live
    
    // Blend: lap 1 = 80% historical, lap 2 = 60%, lap 3 = 40%, lap 4 = 20%
    float historicalWeight = Math.Max(0, 1.0f - (currentLap * 0.2f));
    float liveWeight = 1.0f - historicalWeight;
    
    return (_historicalAvg * historicalWeight) + (_liveAvg * liveWeight);
}
```

---

## 🎯 Use Cases

### 1. Race Start Fuel Prediction

**Problem**: "How much fuel do I need for a 30-lap race?" (lap 0, no data yet)

**Solution**:
```csharp
var history = _historyService.GetHistory(currentFingerprint);

if (history != null && history.Confidence > 60)
{
    // Use historical data with confidence-based buffer
    float avgFuel = history.WeightedAvgFuelPerLap;
    float buffer = history.RecommendedBufferLaps;
    
    float fuelNeeded = avgFuel * (raceLaps + buffer);
    
    DisplayMessage($"Predicted: {fuelNeeded:F1}L ({history.Confidence}% confident)");
}
else
{
    DisplayMessage("No history - learning mode");
}
```

### 2. Setup Comparison

**Problem**: "Did my new fuel map change consumption?"

**Solution**:
```csharp
// Compare current session vs historical average
var liveAvg = _currentSession.AvgFuelPerLap;
var historicalAvg = _history.WeightedAvgFuelPerLap;

float delta = liveAvg - historicalAvg;
float deltaPct = (delta / historicalAvg) * 100;

if (Math.Abs(deltaPct) > 5)
{
    DisplayWarning($"Fuel usage {deltaPct:+0.0;-0.0}% vs history - setup change?");
}
```

### 3. Track Learning

**Problem**: "What lap should I pit on this track?"

**Solution**:
```csharp
var history = _historyService.GetHistory(currentFingerprint);

if (history.TotalSessions >= 3)
{
    int recommendedLap = history.RecommendedPitLap;
    DisplayInfo($"Typical pit window: Lap {recommendedLap-1} to {recommendedLap+1}");
}
```

### 4. Weather Impact Analysis

**Problem**: "How much more fuel do I use when it's hot?"

**Solution**:
```csharp
// Query all sessions for same car+track, different temps
var sessions = _historyService.GetSessionsByCarTrack(car, track);

var coldSessions = sessions.Where(s => s.TrackTempC < 20).ToList();
var hotSessions = sessions.Where(s => s.TrackTempC > 30).ToList();

if (coldSessions.Any() && hotSessions.Any())
{
    float coldAvg = coldSessions.Average(s => s.AvgFuelPerLap);
    float hotAvg = hotSessions.Average(s => s.AvgFuelPerLap);
    float impact = ((hotAvg - coldAvg) / coldAvg) * 100;
    
    _logger.LogInformation("Temperature impact: {Impact:+0.0;-0.0}% fuel at 30°C vs 20°C", impact);
}
```

---

## 🔒 Privacy & Storage Management

### Data Privacy

**✅ What we store**:
- Aggregated performance data (averages, totals)
- Track/car/weather combinations
- Session date/time (for recency weighting)

**❌ What we DON'T store**:
- Raw telemetry data
- Driver names or usernames
- Exact GPS coordinates
- Race results or positions
- Any personally identifiable information

### Storage Limits

Prevent database bloat:

```csharp
public class StorageManager
{
    private const int MAX_SESSIONS_PER_FINGERPRINT = 50;
    private const int MAX_TOTAL_FINGERPRINTS = 500;
    private const int MAX_AGE_DAYS = 365;
    
    public void CleanupOldData()
    {
        // 1. Remove sessions older than 1 year
        _db.Execute(@"
            DELETE FROM SessionHistory 
            WHERE julianday('now') - julianday(SessionDate) > ?",
            MAX_AGE_DAYS);
        
        // 2. Keep only newest 50 sessions per fingerprint
        _db.Execute(@"
            DELETE FROM SessionHistory 
            WHERE SessionID NOT IN (
                SELECT SessionID FROM SessionHistory
                ORDER BY SessionDate DESC
                LIMIT ? OFFSET ?
            )",
            MAX_SESSIONS_PER_FINGERPRINT * MAX_TOTAL_FINGERPRINTS,
            0);
        
        // 3. Vacuum database to reclaim space
        _db.Execute("VACUUM");
    }
}
```

**Automatic Cleanup**: Run on startup if database >10 MB.

---

## 📊 UI Integration

### Settings Panel

Add new section to Settings:

```
┌─ Historical Data ─────────────────────────────┐
│                                               │
│ ☑ Enable telemetry history                   │
│   Store session data for better predictions   │
│                                               │
│ Database Size: 2.4 MB                         │
│ Fingerprints: 47 unique combos                │
│ Sessions: 238 total                           │
│                                               │
│ [View History] [Clean Up Old Data] [Export]  │
│                                               │
└───────────────────────────────────────────────┘
```

### Fuel Widget Enhancement

Show confidence when using historical data:

```
┌─ FUEL ASSIST ─────────────────────────────────┐
│ 15.2 L  (45%)  ⬇ -0.8L                       │
│ ▓▓▓▓▓▓▓▓▓░░░░░░░░░░░░░░░░░░                  │
│                                               │
│ AVG: 2.42L/lap (📊 85% confident)            │ <- Historical data indicator
│ L10: 2.39L/lap                                │
│ SESSION: 2.45L/lap                            │
│                                               │
│ LAPS: 6.3 to go                               │
│ TO GO: --                                     │
│ PIT: Lap 13-15 recommended                   │ <- Historical pit window
└───────────────────────────────────────────────┘
```

### Dashboard Page (New)

Add "History" tab to dashboard:

```
┌─ TELEMETRY HISTORY ──────────────────────────────────────────────────┐
│                                                                       │
│ Top 5 Most Driven Combos:                                           │
│                                                                       │
│ 1. Dallara iL-15 @ Laguna Seca             35 sessions, 892 laps   │
│    Avg Fuel: 2.42L/lap  |  Avg Lap: 1:15.3  |  Conf: 95%           │
│                                                                       │
│ 2. Porsche 992 GT3 Cup @ Watkins Glen      18 sessions, 467 laps   │
│    Avg Fuel: 3.15L/lap  |  Avg Lap: 1:52.7  |  Conf: 88%           │
│                                                                       │
│ 3. BMW M4 GT3 @ Nürburgring GP             12 sessions, 234 laps   │
│    Avg Fuel: 4.22L/lap  |  Avg Lap: 2:02.1  |  Conf: 72%           │
│                                                                       │
│ [View All History] [Export to CSV] [Clear All Data]                 │
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘
```

---

## 🚀 Implementation Plan

### Phase 1: MVP (2 days)

**Day 1**: Core Infrastructure
- [ ] Create `TelemetryHistoryService` class
- [ ] Implement fingerprint generation
- [ ] Create SQLite schema (Tables 1-3)
- [ ] Add session start/end hooks in `IRacingTelemetryService`
- [ ] Implement basic save/load functionality

**Day 2**: FuelCalculator Integration
- [ ] Add `InitializeFromHistory()` method to `FuelCalculatorService`
- [ ] Implement confidence scoring algorithm
- [ ] Add blending logic (historical → live transition)
- [ ] Update Fuel Widget to show confidence indicator
- [ ] Test with mock data

### Phase 2: Polish (1 day)

**Day 3**: UI & Settings
- [ ] Add Settings UI for enabling/disabling history
- [ ] Add database stats display (size, fingerprints, sessions)
- [ ] Implement cleanup functionality
- [ ] Add export to CSV feature
- [ ] Add Dashboard history viewer

### Phase 3: Advanced Features (Future)

- [ ] Setup factor integration (fuel map, downforce)
- [ ] Weather impact analysis
- [ ] Track learning insights (best pit laps)
- [ ] Multi-car comparison (which car is most fuel efficient?)
- [ ] Cloud sync (optional - sync history across machines)

---

## 🔬 Testing Strategy

### Unit Tests

```csharp
[TestClass]
public class TelemetryHistoryServiceTests
{
    [TestMethod]
    public void GenerateFingerprint_SameInputs_SameHash()
    {
        var fp1 = CreateTestFingerprint();
        var fp2 = CreateTestFingerprint();
        
        var hash1 = _service.GenerateFingerprint(fp1);
        var hash2 = _service.GenerateFingerprint(fp2);
        
        Assert.AreEqual(hash1, hash2);
    }
    
    [TestMethod]
    public void CalculateConfidence_100Laps_HighConfidence()
    {
        var stats = new FingerprintStats
        {
            TotalLaps = 100,
            StdDevFuelPerLap = 0.1f,
            WeightedAvgFuelPerLap = 2.5f,
            LastUpdated = DateTime.Now
        };
        
        float confidence = _service.CalculateConfidence(stats);
        
        Assert.IsTrue(confidence >= 80, $"Expected high confidence, got {confidence}");
    }
}
```

### Integration Tests

1. **Test Scenario 1**: No history available
   - Connect to telemetry
   - Verify fuel calculator starts in "learning mode"
   - Complete 5 laps
   - Disconnect
   - Verify session saved to database

2. **Test Scenario 2**: Historical data available
   - Load test database with 10 sessions (same fingerprint)
   - Connect to telemetry
   - Verify fuel calculator initializes with historical average
   - Complete 3 laps
   - Verify blend from historical → live

3. **Test Scenario 3**: Temperature tolerance
   - Create sessions at 20°C, 22°C, 25°C (same car/track)
   - Query at 23°C
   - Verify all 3 sessions match (±5°C tolerance)

---

## 🎓 Lessons from Similar Systems

### iRacing Crew Chief (Existing Solution)

What they do well:
- ✅ Track-specific fuel predictions
- ✅ Pit strategy recommendations
- ✅ Voice output for hands-free info

What we can improve:
- ✅ **Weather-aware predictions** (they don't adjust for temperature)
- ✅ **Setup-aware predictions** (they don't factor fuel maps)
- ✅ **Visual confidence indicators** (they don't show data quality)
- ✅ **Faster initialization** (they need 3-5 laps, we can start at lap 0)

### SimHub Telemetry

What they do well:
- ✅ Comprehensive data logging
- ✅ CSV export for analysis

What we can improve:
- ✅ **Automatic fingerprinting** (they require manual session tagging)
- ✅ **Built-in aggregation** (they just log raw data)
- ✅ **Real-time usage** (they're more for post-race analysis)

---

## ✅ Recommendation: Start with Tier 1 + SQLite

**Rationale**:
1. **Tier 1 fingerprint** (car + track + weather) covers 95% of use cases
2. **SQLite database** provides fast queries and aggregation
3. **Simple implementation** (2-3 days vs weeks for advanced features)
4. **Immediate value** to users (better lap 0 predictions)
5. **Easy to extend** (add Tier 3 setup factors later)

**Next Steps**:
1. Create `TelemetryHistoryService.cs`
2. Add SQLite NuGet package
3. Implement fingerprint generation
4. Hook into session start/end events
5. Test with live telemetry
6. Add UI for history management

**Estimated Storage**: ~1-5 MB for typical user (500 sessions)  
**Performance Impact**: Negligible (one query at session start, one write at session end)  
**User Value**: **HIGH** (instant accurate predictions from lap 0)

---

## 📝 Open Questions

1. **Should we track multi-class races separately?**
   - Option A: Yes (separate fingerprints for GT3 vs GTE races)
   - Option B: No (combine all class combos for same car/track)
   - **Recommendation**: Yes (class matters for traffic/fuel strategy)

2. **How to handle game updates?**
   - Option A: Reset all data after major updates
   - Option B: Add "game version" to fingerprint
   - Option C: Automatic recency weighting handles it
   - **Recommendation**: Option C (old data naturally fades out)

3. **Should we warn users about low confidence?**
   - Option A: Always show confidence %
   - Option B: Only warn if <50% confidence
   - **Recommendation**: Option A (transparency builds trust)

4. **Export format for analysis?**
   - Option A: CSV (easy for Excel)
   - Option B: JSON (preserves structure)
   - Option C: Both
   - **Recommendation**: Option A for MVP, add B later

---

## 🎯 Success Metrics

How do we know this system works?

1. **Prediction Accuracy**: Historical predictions within 5% of actual fuel usage
2. **User Feedback**: Survey users on whether they trust lap 0 predictions
3. **Data Coverage**: 80% of users have historical data for their top 5 combos after 1 month
4. **Performance**: Database queries <50ms on average
5. **Storage**: Database stays <10 MB for 90% of users

---

**Status**: ✅ Ready for implementation  
**Next Action**: Create `TelemetryHistoryService.cs` and begin Phase 1
