# Dynamic Shift Point Detection Research
**Project:** MRTui - iRacing Overlay  
**Date:** October 12, 2025  
**Status:** Research Phase - DO NOT IMPLEMENT YET

## 📋 Current Implementation

### Existing Algorithm (ShiftPointCalculator.cs)
The current system uses a **learning-based approach**:

```csharp
// Core mechanism:
1. Track maximum observed RPM during session
2. Estimate redline = maxObservedRPM × 1.02
3. Optimal shift point = estimatedRedline × 0.93 (93%)
4. Color zones:
   - Safe: < 85% redline (Teal)
   - Warning: 85-93% redline (Yellow)
   - Optimal: 93-95% redline (Orange - SHIFT NOW)
   - Danger: ≥ 95% redline (Red - at limiter)
```

**Strengths:**
✅ Simple and lightweight  
✅ Works across all cars without configuration  
✅ Adapts automatically to each vehicle  
✅ No external data required  

**Limitations:**
❌ Assumes 93% is optimal for all cars (not always true)  
❌ No consideration of gear ratios or torque curves  
❌ Doesn't persist between sessions  
❌ Takes time to "learn" the car each session  
❌ Can't distinguish between different cars/setups  

---

## 🔬 Research: Advanced Detection Methods

### Option 1: Gear Ratio Analysis
**Theory:** Optimal shift point is when RPM in next gear > current gear's peak power RPM

**iRacing Telemetry Available:**
- `Gear` - Current gear number
- `RPM` - Current engine RPM
- `Speed` - Vehicle speed (m/s)

**Algorithm:**
```python
# Calculate gear ratios during session
gear_ratios = {}

for telemetry_sample in session:
    if speed > 10 and throttle > 0.8:  # Under acceleration
        ratio = rpm / speed
        gear_ratios[current_gear] = ratio

# Predict next gear RPM
def get_rpm_in_next_gear(current_rpm, current_gear):
    if current_gear not in gear_ratios or current_gear + 1 not in gear_ratios:
        return None
    
    current_ratio = gear_ratios[current_gear]
    next_ratio = gear_ratios[current_gear + 1]
    
    # Calculate what RPM would be in next gear at same speed
    predicted_rpm = current_rpm * (next_ratio / current_ratio)
    return predicted_rpm

# Optimal shift when: 
# next_gear_rpm > peak_power_rpm (around 85-90% of redline)
```

**Data Requirements:**
- Telemetry history for each gear
- Time to learn ratios (30+ seconds per gear)
- Speed/RPM samples under consistent throttle

**Pros:**
✅ More accurate for cars with narrow power bands  
✅ Prevents shifting too early (landing below power band)  
✅ Works with telemetry we already have  

**Cons:**
❌ Requires significant data collection time  
❌ Affected by tire slip, terrain changes  
❌ Complex implementation  

---

### Option 2: Acceleration-Based Learning
**Theory:** Optimal shift point is where acceleration rate stops increasing

**iRacing Telemetry Available:**
- `Speed` - Vehicle speed (m/s)
- `Accel` - Lateral/longitudinal acceleration (if available)
- Delta Time between samples

**Algorithm:**
```python
# Track acceleration rate at different RPM ranges
acceleration_by_rpm = {}

previous_speed = 0
previous_time = 0

for sample in telemetry_stream:
    if throttle > 0.9 and steering < 5 degrees:  # Full throttle, straight
        delta_speed = speed - previous_speed
        delta_time = time - previous_time
        
        if delta_time > 0:
            acceleration = delta_speed / delta_time
            
            # Bin by RPM ranges (500 RPM increments)
            rpm_bin = int(rpm / 500) * 500
            
            if rpm_bin not in acceleration_by_rpm:
                acceleration_by_rpm[rpm_bin] = []
            
            acceleration_by_rpm[rpm_bin].append(acceleration)
    
    previous_speed = speed
    previous_time = time

# Find RPM where acceleration starts dropping
def find_optimal_shift():
    peak_accel = 0
    peak_rpm = 0
    
    for rpm_bin in sorted(acceleration_by_rpm.keys()):
        avg_accel = average(acceleration_by_rpm[rpm_bin])
        
        if avg_accel > peak_accel:
            peak_accel = avg_accel
            peak_rpm = rpm_bin
        elif avg_accel < peak_accel * 0.95:
            # Acceleration dropped 5% - shift before this
            return peak_rpm
    
    return peak_rpm
```

**Pros:**
✅ Based on actual car performance  
✅ Accounts for power curve naturally  
✅ Self-correcting with more data  

**Cons:**
❌ Requires very clean data (straight line, full throttle)  
❌ Slow to converge (multiple laps needed)  
❌ Sensitive to track conditions, tire wear  
❌ Complex noise filtering required  

---

### Option 3: Car Database + Session Tracking
**Theory:** Store optimal shift points per car, refine during session

**iRacing Telemetry Available:**
- `CarId` or `CarName` - Vehicle identifier (need to verify availability)
- `SessionNum` - Session identifier
- `TrackId` or `TrackName` - Track identifier

**Algorithm:**
```python
# Persistent database structure
shift_database = {
    "car_id_123": {
        "base_redline": 8500,
        "optimal_shift": 7900,  # 93% of redline
        "gear_specific": {
            1: 7800,  # Shift from 1st at 7800 RPM
            2: 7900,
            3: 8000,
            4: 8000,
            5: 8100,
            6: 8200
        },
        "confidence": 0.85,  # 85% confident in these values
        "sample_count": 450
    }
}

# During session:
def get_shift_point(car_id, gear):
    if car_id in shift_database:
        # Use stored data
        if gear in shift_database[car_id]["gear_specific"]:
            return shift_database[car_id]["gear_specific"][gear]
        else:
            return shift_database[car_id]["optimal_shift"]
    else:
        # Fall back to learning algorithm
        return current_learning_algorithm()

# Update database during session
def update_shift_data(car_id, observed_redline, gear_shifts):
    if car_id not in shift_database:
        shift_database[car_id] = initialize_car_data()
    
    # Exponential moving average with new data
    db = shift_database[car_id]
    alpha = 0.1  # Learning rate
    
    db["base_redline"] = alpha * observed_redline + (1 - alpha) * db["base_redline"]
    db["sample_count"] += 1
    db["confidence"] = min(1.0, db["sample_count"] / 1000)
```

**Storage Options:**
1. **Local JSON file** - `shift_database.json` in app directory
2. **SQLite database** - `shift_data.db` for better performance
3. **Cloud sync** - Optional for multi-device setups

**Pros:**
✅ Instant optimal shift points for known cars  
✅ Improves over time with more data  
✅ Shareable between users (crowd-sourced)  
✅ Gear-specific tuning possible  
✅ Falls back gracefully for unknown cars  

**Cons:**
❌ Requires persistent storage system  
❌ Initial learning period for new cars  
❌ May need updates when car physics change  
❌ Privacy considerations if cloud-synced  

---

### Option 4: Torque Curve Estimation (Advanced)
**Theory:** Estimate torque curve from power delivery, find peak

**iRacing Telemetry Available:**
- `Power` - Engine power (if exposed, need verification)
- `Torque` - Engine torque (if exposed, need verification)
- `RPM`, `Throttle`, `Speed`

**Algorithm:**
```python
# If torque is directly available:
if "Torque" in telemetry:
    # Track torque vs RPM
    torque_curve = {}
    
    for sample in full_throttle_samples:
        rpm_bin = int(rpm / 100) * 100
        if rpm_bin not in torque_curve:
            torque_curve[rpm_bin] = []
        torque_curve[rpm_bin].append(torque)
    
    # Find peak torque RPM
    peak_torque_rpm = max(torque_curve.keys(), 
                          key=lambda k: average(torque_curve[k]))
    
    # Optimal shift is typically 10-15% past peak torque
    optimal_shift = peak_torque_rpm * 1.12

# If only power is available:
else:
    # Power = Torque × RPM / 5252 (in imperial)
    # Torque = (Power × 5252) / RPM
    
    # Estimate from power and RPM
    estimated_torque = (power * 5252) / rpm
```

**Feasibility Check Required:**
⚠️ Need to verify if iRacing SDK exposes:
- `EnginePower` (watts or horsepower)
- `EngineTorque` (N⋅m or lb⋅ft)

**Pros:**
✅ Most accurate method if data available  
✅ Direct measurement of engine characteristics  
✅ Works for all engine types (turbo, NA, electric)  

**Cons:**
❌ Requires torque/power telemetry (may not be exposed)  
❌ Complex calculations  
❌ May be overkill for racing application  

---

## 🎯 Recommended Implementation Strategy

### Phase 1: Enhanced Learning (Immediate - Easy Win)
**Goal:** Improve current algorithm without major changes

```csharp
public static class ImprovedShiftPointCalculator
{
    // Add gear-specific tracking
    private static Dictionary<int, float> _maxRPMByGear = new();
    private static Dictionary<int, List<float>> _shiftPointHistory = new();
    
    // Track when driver actually shifts
    public static void TrackShift(int fromGear, float atRPM)
    {
        if (!_shiftPointHistory.ContainsKey(fromGear))
            _shiftPointHistory[fromGear] = new List<float>();
        
        _shiftPointHistory[fromGear].Add(atRPM);
        
        // Learn from driver's behavior
        if (_shiftPointHistory[fromGear].Count > 10)
        {
            // Use median of driver's shifts as optimal point
            var sorted = _shiftPointHistory[fromGear].OrderBy(x => x).ToList();
            var median = sorted[sorted.Count / 2];
            
            // Blend with calculated optimum
            var calculated = GetEstimatedRedline() * 0.93f;
            var learned = median * 1.05f; // 5% higher than driver typically shifts
            
            return (calculated + learned) / 2; // Average of both
        }
    }
}
```

**Effort:** Low (2-3 hours)  
**Impact:** Medium (5-10% better accuracy)

---

### Phase 2: Car Database (Medium-term - High Value)
**Goal:** Persistent storage of shift points per car

```csharp
public class ShiftPointDatabase
{
    private static string DbPath = "shift_data.json";
    
    public class CarShiftData
    {
        public string CarId { get; set; }
        public float BaseRedline { get; set; }
        public float OptimalShift { get; set; }
        public Dictionary<int, float> GearSpecificShift { get; set; }
        public int SampleCount { get; set; }
        public DateTime LastUpdated { get; set; }
    }
    
    public static void LoadDatabase() { /* ... */ }
    public static void SaveDatabase() { /* ... */ }
    public static CarShiftData GetCarData(string carId) { /* ... */ }
    public static void UpdateCarData(string carId, TelemetryData data) { /* ... */ }
}
```

**Requirements:**
1. Add `CarId` to TelemetryData model
2. Create JSON persistence layer
3. Implement database migration for future changes
4. Add UI for viewing/editing stored data

**Effort:** Medium (1-2 days)  
**Impact:** High (15-20% better accuracy, instant for known cars)

---

### Phase 3: Gear Ratio Analysis (Long-term - Advanced)
**Goal:** Calculate optimal shifts based on gear ratios

```csharp
public class GearRatioAnalyzer
{
    private Dictionary<int, List<(float speed, float rpm)>> _gearSamples = new();
    
    public void AddSample(int gear, float speed, float rpm, float throttle)
    {
        // Only record clean data
        if (throttle > 0.8 && speed > 10)
        {
            if (!_gearSamples.ContainsKey(gear))
                _gearSamples[gear] = new List<(float, float)>();
            
            _gearSamples[gear].Add((speed, rpm));
        }
    }
    
    public float? CalculateOptimalShift(int currentGear)
    {
        if (!HasEnoughData(currentGear) || !HasEnoughData(currentGear + 1))
            return null;
        
        var currentRatio = CalculateGearRatio(currentGear);
        var nextRatio = CalculateGearRatio(currentGear + 1);
        
        // Complex calculation here...
        return optimalShiftRPM;
    }
}
```

**Requirements:**
1. Implement gear ratio calculation
2. Add statistical analysis for noisy data
3. Create confidence metrics
4. Extensive testing with various cars

**Effort:** High (3-5 days)  
**Impact:** Very High (20-30% better accuracy, gear-specific optimization)

---

## 📊 Comparison Matrix

| Method | Accuracy | Complexity | Data Needed | Dev Time | User Friction |
|--------|----------|------------|-------------|----------|---------------|
| **Current (RPM Learning)** | ⭐⭐⭐ | ⭐ | Minimal | Done | None |
| **Enhanced Learning** | ⭐⭐⭐⭐ | ⭐⭐ | Session data | 2-3 hrs | None |
| **Car Database** | ⭐⭐⭐⭐ | ⭐⭐ | Historical | 1-2 days | Minimal |
| **Gear Ratio Analysis** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | Multiple laps | 3-5 days | Low |
| **Acceleration-Based** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Clean samples | 1 week | Medium |
| **Torque Curve** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Torque data | Unknown | High |

---

## 🚀 Recommended Roadmap

### MVP 2.5 (Current Sprint - DO NOT IMPLEMENT YET, PLANNING ONLY)
- [x] Keep current ShiftPointCalculator as-is
- [x] Document research findings
- [ ] User testing to gather feedback on current system
- [ ] Measure: How often are users shifting when we say "optimal"?

### MVP 3 (Next Sprint)
- [ ] Implement **Enhanced Learning** (Phase 1)
- [ ] Add driver shift tracking
- [ ] Blend calculated vs learned shift points
- [ ] A/B test with users

### MVP 4 (Future)
- [ ] Implement **Car Database** (Phase 2)
- [ ] JSON persistence layer
- [ ] UI for shift point management
- [ ] Optional: Community database (crowd-sourced)

### MVP 5+ (Advanced Features)
- [ ] Implement **Gear Ratio Analysis** (Phase 3)
- [ ] Per-gear optimization
- [ ] Confidence metrics
- [ ] Advanced tuning UI

---

## 🔍 Data Verification Needed

Before implementing advanced features, verify iRacing SDK exposes:

| Telemetry Field | Available? | Notes |
|----------------|------------|-------|
| `CarId` or `CarIdx` | ❓ Need to check | For car database |
| `CarName` | ❓ Need to check | Alternative to CarId |
| `SessionNum` | ❓ Need to check | For session tracking |
| `TrackId` or `TrackName` | ❓ Need to check | Track-specific tuning |
| `EnginePower` | ❓ Need to check | For torque calculation |
| `EngineTorque` | ❓ Need to check | Direct torque access |
| `LongAccel` | ❓ Need to check | For acceleration method |
| `GearRatio` array | ❓ Need to check | If ratios are exposed |

**Action Item:** Check iRacing SDK documentation and telemetry variable list.

---

## 💡 Key Insights

1. **Current system is good enough for now** - 93% works for most cars
2. **Don't over-engineer** - Racing is about consistency, not 0.1s per shift
3. **User behavior is valuable data** - Track when drivers actually shift
4. **Persistence is key** - Learning once per car is better than every session
5. **Gear-specific matters** - Lower gears often want earlier shifts
6. **Test with real drivers** - Optimal on paper ≠ optimal in practice

---

## 📚 References

1. **Automotive Engineering** - "Optimizing Transmission Shift Points for Maximum Acceleration"
2. **iRacing Forums** - Community discussions on shift point optimization
3. **Race Engineer Guides** - Gear ratio and shift point calculations
4. **Power-to-Weight Ratios** - How they affect optimal shift RPM
5. **Turbo vs NA Engines** - Different power delivery characteristics

---

## ✅ Conclusion

**Recommendation:** Implement **Phase 1 (Enhanced Learning)** + **Phase 2 (Car Database)** as next steps.

**Reasoning:**
- Low to medium effort (1-3 days total)
- High user value (instant accuracy for repeated cars)
- Foundation for future advanced features
- Minimal risk to existing functionality

**DO NOT IMPLEMENT YET** - Awaiting user approval and prioritization in MVP backlog.

---

**Last Updated:** October 12, 2025  
**Author:** Development Team  
**Status:** Research Complete - Awaiting Decision
