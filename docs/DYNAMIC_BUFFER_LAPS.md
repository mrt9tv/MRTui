# Phase 2.2: Dynamic Buffer Laps Enhancement

## 📋 Overview

**Status**: ✅ **COMPLETED**  
**Priority**: High  
**Build**: ✅ Successful (0 errors, 0 warnings)

Implemented intelligent, dynamic buffer lap calculation based on real-time race conditions. The system automatically adjusts fuel safety margins by analyzing:
- **Fuel consumption consistency** (variance)
- **Race position** (leading vs battling)
- **Weather conditions** (rain/overcast)
- **Yellow flag probability** (historical session data)
- **Pit window constraints** (late-race optimization)

---

## 🎯 Implementation Summary

### New FuelData Properties

```csharp
public float FuelConsistencyVariance { get; set; }      // Standard deviation of fuel/lap
public float YellowFlagProbability { get; set; }        // 0-1 (yellow laps / total laps)
public string BufferLapReason { get; set; }             // Explanation for buffer value
```

### AppSettings Configuration

```csharp
public bool FuelWidget_EnableDynamicBuffer { get; set; } = true;  // Enable/disable dynamic buffer
public float FuelWidget_BufferLaps { get; set; } = 1.0f;          // Base buffer (minimum)
```

### Core Algorithm: `CalculateDynamicBufferLaps()`

Located in: `FuelCalculatorService.cs` (lines 711-862)

**Process Flow:**
1. Check if dynamic buffer is enabled (fallback to static value if disabled)
2. Validate sufficient lap data (minimum 5 laps for variance calculation)
3. Calculate 5 buffer adjustment factors
4. Combine factors with min/max thresholds (0.5 - 3.0 laps)
5. Generate explanation string for transparency

---

## 🔢 Buffer Calculation Factors

### Factor 1: Fuel Consistency Variance

**Method**: Standard deviation of fuel consumption per lap

```csharp
float variance = validLaps.Sum(l => MathF.Pow(l.FuelUsed - avgFuel, 2)) / validLaps.Count;
float stdDev = MathF.Sqrt(variance);
```

**Mapping**:
- `< 0.1L` → **+0.5 laps** (very consistent driver/car)
- `< 0.2L` → **+0.75 laps** (good consistency)
- `< 0.3L` → **+1.0 laps** (normal variance)
- `< 0.4L` → **+1.5 laps** (high variance)
- `≥ 0.5L` → **+2.0 laps** (very inconsistent)

**Rationale**: More consistent fuel usage = tighter margin acceptable. Inconsistent usage requires larger buffer to avoid running dry.

---

### Factor 2: Race Position

**Method**: Calculate position percentile (player position / total cars)

```csharp
float positionPct = (float)playerPosition / totalCars;
```

**Mapping**:
- Top 10% (leading pack) → **-0.3 laps** (can control pace)
- Top 30% (front runners) → **0.0 laps** (neutral)
- 30-70% (midfield) → **+0.3 laps** (battles increase consumption)
- 70%+ (back markers) → **+0.5 laps** (unpredictable battles)

**Rationale**: Leaders can manage pace and traffic. Back markers battle constantly, leading to unpredictable fuel usage.

---

### Factor 3: Weather Conditions

**Method**: Check iRacing Skies enum value

```csharp
// iRacing Skies: 0=Clear, 1=PartlyCloudy, 2=MostlyCloudy, 3=Overcast, 4=Rain
```

**Mapping**:
- **Rain** (skies ≥ 4) → **-0.5 laps** (LOWER fuel consumption)
- **Overcast** (skies = 3) → **-0.2 laps** (slightly lower consumption)
- **Clear/Partly Cloudy** → **0.0 laps** (no adjustment)

**Rationale**: Wet conditions = LOWER average fuel consumption (slower speeds, cautious throttle, wet tires). Similar to yellow flags. NOTE: Consumption variance is already handled by Factor 1 (consistency variance).

---

### Factor 4: Yellow Flag Probability

**Method**: Calculate historical yellow rate for the session

```csharp
CurrentData.YellowFlagProbability = (float)CurrentData.YellowFlagLapCount / totalLapsCompleted;
```

**Mapping** (after 10+ laps completed):
- **>30% yellows** → **-0.5 laps** (very yellow-heavy race, short oval)
- **20-30% yellows** → **-0.3 laps** (moderate yellows)
- **10-20% yellows** → **0.0 laps** (normal racing)
- **<10% yellows** → **+0.2 laps** (clean race, increase buffer)
- **0 yellows after 10 laps** → **+0.3 laps** (assume clean race)

**Rationale**: 
- High yellow probability = can take more risk (likely to get caution period for fuel saving)
- Clean race = need larger buffer (no opportunity for fuel-saving laps under yellow)
- **Strategic Note**: During yellows, drivers can actively SAVE fuel (pit limiter speed) or BURN fuel (rev limiter) depending on strategy needs

**Yellow Flag Definition**: YellowFlagLapCount tracks laps where car is under caution (pace car laps, full course yellows). Does NOT include quick local yellows or debris cautions that don't bunch the field.

---

### Factor 5: Pit Window Optimization

**Method**: Check laps remaining in race

```csharp
if (CurrentData.RaceLapsRemaining <= 5)
    pitWindowBuffer = -0.5f;
```

**Mapping**:
- **≤5 laps to go** → **-0.5 laps** (aggressive, maximize laps before pit)
- **>5 laps to go** → **0.0 laps** (normal)

**Rationale**: Late-race scenario where pit window is closing. Reduce buffer to squeeze out maximum laps and minimize time loss.

---

## 📊 Example Scenarios

### Scenario 1: Leading in Clean Conditions
```
Position: 1/40 (2.5% - top 10%)
Fuel Variance: ±0.12L (consistent)
Weather: Clear
Yellow Rate: 5% (clean race)
Laps to Go: 30

Calculation:
  Consistency Buffer:  +0.75 (good consistency)
  Position Buffer:     -0.30 (leading, can control pace)
  Weather Buffer:      +0.00 (clear)
  Yellow Buffer:       +0.20 (clean race, no saving opportunities)
  Pit Window Buffer:   +0.00 (plenty of time)
  ────────────────────
  FINAL BUFFER:        +0.65 laps

Reason: "Dynamic: 0.7L (consistency:0.8 pos:-0.3 weather:0.0 yellow:0.2 pit:0.0)"
```

### Scenario 2: Midfield Battle in Rain
```
Position: 18/40 (45% - midfield)
Fuel Variance: ±0.35L (high variance from battles)
Weather: Rain (skies=4)
Yellow Rate: 25% (moderate yellows)
Laps to Go: 45

Calculation:
  Consistency Buffer:  +1.50 (high variance from battles)
  Position Buffer:     +0.30 (midfield battles)
  Weather Buffer:      -0.50 (rain = lower avg consumption)
  Yellow Buffer:       -0.30 (moderate yellow probability)
  Pit Window Buffer:   +0.00 (normal)
  ────────────────────
  FINAL BUFFER:        +1.00 laps

Reason: "Dynamic: 1.0L (consistency:1.5 pos:0.3 weather:-0.5 yellow:-0.3 pit:0.0)"
```

### Scenario 3: Short Oval Late Race
```
Position: 25/40 (62.5% - midfield)
Fuel Variance: ±0.08L (very consistent, short track)
Weather: Clear
Yellow Rate: 40% (very yellow-heavy, short oval)
Laps to Go: 4

Calculation:
  Consistency Buffer:  +0.50 (very consistent)
  Position Buffer:     +0.30 (midfield)
  Weather Buffer:      +0.00 (clear)
  Yellow Buffer:       -0.50 (high yellow probability)
  Pit Window Buffer:   -0.50 (pit window closing)
  ────────────────────
  RAW: -0.20 → CLAMPED: +0.50 laps (minimum threshold)

Reason: "Dynamic: 0.5L (consistency:0.5 pos:0.3 weather:0.0 yellow:-0.5 pit:-0.5)"
```

---

## 🛡️ Safety Thresholds

### Minimum Buffer: 0.5 Laps
Ensures driver never runs completely dry, even in perfect leading conditions.

### Maximum Buffer: 3.0 Laps
Prevents excessive conservatism in worst-case scenarios (rain + back markers + inconsistent).

### Fallback Logic
If dynamic buffer disabled OR insufficient data (<5 laps), use static `FuelWidget_BufferLaps` value.

---

## 🔧 Configuration & Usage

### Enable/Disable Dynamic Buffer

**Settings UI**: Fuel Widget Phase 3 section  
**Setting**: "🎯 Dynamic Buffer Laps (Enhanced)"  
**Description**: Auto-adjust buffer based on consistency, position, weather, yellow probability

**Default**: Enabled (`FuelWidget_EnableDynamicBuffer = true`)

### User-Configured Base Buffer

**Setting**: `FuelWidget_BufferLaps` (default: 1.0 laps)  
**Usage**: 
- Used as fallback when dynamic buffer is disabled
- NOT used as minimum when dynamic buffer is enabled (minimum is 0.5 laps)

### Configure at Runtime

```csharp
FuelCalculatorService.ConfigureBufferSettings(
    bufferLaps: AppSettings.Instance.FuelWidget_BufferLaps,
    enableDynamicBuffer: AppSettings.Instance.FuelWidget_EnableDynamicBuffer
);
```

Called automatically by FuelWidget before each fuel data update.

---

## 📈 UI Enhancements

### Tank Capacity Tooltip (Enhanced)

**Location**: FuelWidget → Tank capacity display  
**Format**:
```
Sputtering threshold: 0.30L
Car category: GT3
Buffer laps: 1.3
Buffer reason: Dynamic: 1.3L (consistency:0.8 pos:0.0 weather:0.0 yellow:0.5 pit:0.0)
Fuel variance: ±0.183L
Yellow flag probability: 15%
```

**Provides full transparency** of dynamic buffer calculation for debugging and driver awareness.

---

## 🧪 Testing Checklist

### Test Case 1: Static Buffer (Dynamic Disabled)
- [ ] Disable dynamic buffer in settings
- [ ] Verify buffer always equals `FuelWidget_BufferLaps` setting
- [ ] Tooltip shows "User configured (static)"

### Test Case 2: Insufficient Data
- [ ] Start new session (0-4 laps completed)
- [ ] Verify buffer uses static value
- [ ] Tooltip shows "Insufficient data (only X laps)"

### Test Case 3: Leading Position
- [ ] Lead race in GT3 car (position 1-3 / ~40 cars)
- [ ] Drive consistently (±0.1L variance)
- [ ] Verify buffer reduces to 0.5-0.7 laps
- [ ] Tooltip shows negative position buffer

### Test Case 4: Midfield Battles
- [ ] Race in midfield (position 15-25 / ~40 cars)
- [ ] Engage in battles (higher variance)
- [ ] Verify buffer increases to 1.5-2.0 laps
- [ ] Tooltip shows positive position buffer

### Test Case 5: Rain Conditions
- [ ] Start session in rain (skies=4)
- [ ] Verify buffer increases by +1.0 laps
- [ ] Tooltip shows weather buffer contribution

### Test Case 6: Yellow-Heavy Race (Short Oval)
- [ ] Run short oval race (Bristol, Martinsville)
- [ ] Observe frequent yellows (>30% of laps)
- [ ] Verify buffer REDUCES by -0.5 laps
- [ ] Tooltip shows negative yellow buffer

### Test Case 7: Clean Road Course
- [ ] Run road course with minimal yellows (<10%)
- [ ] Verify buffer INCREASES by +0.2-0.3 laps
- [ ] Tooltip shows positive yellow buffer

### Test Case 8: Late Race Pit Window
- [ ] Reach final 5 laps of race
- [ ] Verify buffer reduces by -0.5 laps (pit window closing)
- [ ] Tooltip shows negative pit window buffer

### Test Case 9: Min/Max Clamping
- [ ] Create worst-case scenario (rain + back markers + inconsistent)
- [ ] Verify buffer never exceeds 3.0 laps
- [ ] Create best-case scenario (leading + consistent + clean)
- [ ] Verify buffer never goes below 0.5 laps

---

## 🐛 Known Limitations

### 1. Position Data Availability
- Requires `LivePosition` and `CarIdxPosition` from telemetry
- Falls back to neutral (0.0) if position data unavailable

### 2. Weather Detection Simplicity
- Uses simple Skies enum (no wetness tracking, no track state)
- Future: Could integrate TrackWetness telemetry variable

### 3. Yellow Flag Tracking
- Only tracks full-course yellows (pace car laps)
- Does NOT distinguish between debris caution (1 lap) vs incident caution (5+ laps)
- Probability calculated as simple percentage (no time-weighting)
- **Does NOT model driver fuel strategy during yellows**: Drivers can actively SAVE fuel (pit limiter) or BURN fuel (rev limiter) depending on needs

### 4. Pit Window Logic Simplicity
- Uses simple lap threshold (≤5 laps = closing)
- Future: Could integrate track position, fuel delta, pit stop time

### 5. First 10 Laps (Yellow Probability)
- Yellow flag probability not calculated until 10 laps completed
- Early-race strategy may be slightly conservative

---

## 🚀 Future Enhancements (Phase 3+)

### Track-Specific Profiles
Pre-load historical yellow flag rates for tracks:
- Short ovals (Bristol, Martinsville): 35-45% yellow rate
- Intermediate ovals (Charlotte, Texas): 15-25% yellow rate
- Road courses (Watkins Glen, Road America): 5-10% yellow rate

### Machine Learning Model
Train model on historical data:
- Input: Track type, series, lap number, current yellow rate
- Output: Predicted yellow probability for remaining laps

### Fuel Pressure Integration
Already tracking fuel pressure baseline/drop (Phase 2.1):
- Low pressure (>10% drop) → Increase buffer by 0.5 laps
- Critical pressure (>20% drop) → Force pit (override buffer)

### Position Change Rate
Track position changes per lap:
- Stable position → Reduce buffer
- Frequent position changes → Increase buffer (unpredictable battles)

### Lap Time Delta Integration
Compare current lap pace to average:
- Faster pace → Increase buffer (higher consumption)
- Slower pace → Reduce buffer (conserving fuel)

---

## 📝 Code References

### Core Implementation
- **File**: `FuelCalculatorService.cs`
- **Method**: `CalculateDynamicBufferLaps()` (lines 711-862)
- **Call Site**: `Update()` method (line 156)

### Data Models
- **File**: `FuelData.cs`
- **Properties**: `FuelConsistencyVariance`, `YellowFlagProbability`, `BufferLapReason`

### Settings
- **File**: `AppSettings.cs`
- **Property**: `FuelWidget_EnableDynamicBuffer` (line 128)

### UI Configuration
- **File**: `SettingsView.xaml`
- **Section**: Fuel Widget Phase 3 (line 400)

### UI Display
- **File**: `FuelWidget.xaml.cs`
- **Method**: `UpdateUI()` (lines 364-388 - enhanced tooltip)

---

## ✅ Success Criteria

- [x] ✅ Build successful (0 errors, 0 warnings)
- [x] ✅ Dynamic buffer calculation implemented with 5 factors
- [x] ✅ Min/max thresholds enforced (0.5 - 3.0 laps)
- [x] ✅ Settings toggle added (Enable Dynamic Buffer)
- [x] ✅ UI tooltip enhanced with buffer breakdown
- [x] ✅ Configuration method added to FuelCalculatorService
- [x] ✅ Fallback logic for disabled/insufficient data
- [ ] ⏳ Tested in iRacing (GT3, Short Oval, Road Course)
- [ ] ⏳ Verified consistency variance calculation accuracy
- [ ] ⏳ Validated yellow flag probability tracking
- [ ] ⏳ Confirmed rain buffer adjustment

---

## 🎓 Developer Notes

### Architecture Decision: Service Configuration
Instead of referencing `AppSettings` directly from Core project, we:
1. Added configuration method: `ConfigureBufferSettings()`
2. Stored settings in private fields: `_bufferLaps`, `_enableDynamicBuffer`
3. Called from WPF layer before each update

**Benefits**: Maintains clean separation between Core (business logic) and WPF (presentation/settings).

### Variance Calculation: Standard Deviation
Standard deviation captures fuel usage spread:
- **Low variance** (±0.1L): Consistent driver, predictable consumption
- **High variance** (±0.5L): Battles, mistakes, traffic variability

Formula: `σ = sqrt(Σ(xi - μ)² / N)`

### Yellow Flag Probability: Simple Percentage
Current implementation: `yellow laps / total laps`

**Limitation**: Treats all yellows equally (1 lap debris = 5 lap incident). Does NOT model driver's active fuel management during yellows (saving vs burning fuel).

**Future**: 
- Weight by lap count under each yellow period
- Detect driver fuel strategy during yellows (pit limiter = saving, rev limiter = burning)

### Buffer Combination: Additive Model
All factors are summed, then clamped to [0.5, 3.0].

**Alternative considered**: Multiplicative model (factors as multipliers).  
**Decision**: Additive model provides more intuitive debugging and tuning.

---

## 📚 Related Documentation

- **Phase 2.1**: `SPUTTERING_THRESHOLD_ENHANCEMENT.md` (Car-specific thresholds + pressure correlation)
- **Phase 3**: `PHASE3_FUEL_SAVING_PROGRESS.md` (Fuel saving mode, strategic alerts)
- **Telemetry Reference**: `SDK_MASTER_REFERENCE.md` (Available telemetry variables)
- **Fuel Calculator**: `FUEL_STRATEGY_PHASE2_COMPLETE.md` (Core fuel calculation engine)

---

**Implementation Date**: 2025-10-21  
**Status**: ✅ Complete and Build Verified  
**Next Step**: Testing in iRacing across multiple scenarios
