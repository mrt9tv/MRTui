# TC Scale Investigation - Critical Findings

## 🚨 CRITICAL DISCOVERY: TC Scales are Car-Specific and Inverted on Some Cars

### The Problem

The `dcTractionControl` SDK variable returns **raw values without context**. Different cars use different TC scaling systems:

**Type 1: Normal Scale (0 = OFF)**
- TC 0 = OFF
- TC 1-12 = Low to High intervention
- Higher number = MORE traction control

**Type 2: Inverted Scale (12 = OFF)**
- TC 12 = OFF
- TC 11 = Low intervention
- TC 1 = Maximum intervention
- Higher number = LESS traction control

### Confirmed Car Behaviors

#### Normal Scale (0 = OFF) ✅
Examples:
- **Dallara F3**: TC 0 = OFF, TC 1-12 = levels (1 = min, 12 = max)
- **Some LMP2 cars**: TC 0 = OFF, TC 1-20 = levels

#### Inverted Scale (12 = OFF) ⚠️
Examples:
- **Some GT3 cars** (Ferrari 488 GT3, others?): TC 12 = OFF, TC 11-1 = levels (11 = min, 1 = max)
- Exact list needs community verification

### Why This Happens

**iRacing Philosophy**: The TC scale represents the *physical dial/switch in the real car*
- Real Ferrari 488 GT3: Has dial from 0-12, with 12 being OFF
- Real Dallara F3: Has dial from 0-12, with 0 being OFF
- SDK reports *what the dial says*, not a standardized scale

### SDK Investigation Results

#### ❌ NO SDK Variables for TC Scale Detection

Checked all available sources:
- ✅ `dcTractionControl` - Raw value (what dial shows)
- ❌ `dcTractionControlMin` - NOT AVAILABLE
- ❌ `dcTractionControlMax` - NOT AVAILABLE
- ❌ `dcTractionControlDirection` - NOT AVAILABLE
- ❌ `dcTractionControlOffValue` - NOT AVAILABLE

#### ❌ NO YAML SessionInfo for TC Metadata

Checked `SessionInfo` YAML sections:
- ✅ `CarSetup` section exists BUT doesn't contain TC scale metadata
- ✅ `DriverInfo` has car specs BUT no TC configuration
- ❌ No field for "TC scale direction" or "TC OFF value"
- ❌ No field for "TC min/max range"

### What We CAN Detect

#### Available Information ✅

1. **Current TC Value**: `dcTractionControl` (0-20 depending on car)
2. **Car Name**: `DriverInfo.Drivers[].CarScreenName` from YAML
3. **Car Path**: `DriverInfo.Drivers[].CarPath` from YAML (internal identifier)

#### What We CANNOT Detect ❌

1. **TC Scale Direction**: Whether higher = more TC or less TC
2. **OFF Value**: Whether 0 is OFF or 12 is OFF (or something else)
3. **TC Range**: Min/max values for the car
4. **TC Behavior**: Linear vs non-linear scaling

---

## 🔧 Possible Solutions

### Solution 1: Car-Specific Mapping Database (RECOMMENDED)

Create a static database mapping car identifiers to TC scale configurations:

```csharp
public class TractionControlConfig
{
    public string CarPath { get; set; }           // "ferrari488gt3"
    public string CarScreenName { get; set; }     // "Ferrari 488 GT3"
    public int MinValue { get; set; }             // 0 or 1
    public int MaxValue { get; set; }             // 12 or 20
    public int OffValue { get; set; }             // 0 or 12
    public TcScaleDirection Direction { get; set; } // Normal or Inverted
}

public enum TcScaleDirection
{
    Normal,    // Higher = more TC (0=OFF, 12=MAX)
    Inverted   // Higher = less TC (12=OFF, 1=MAX)
}

// Database
private static readonly Dictionary<string, TractionControlConfig> TcDatabase = new()
{
    // Normal scale cars (0 = OFF)
    ["dallaraF3"] = new TractionControlConfig
    {
        CarPath = "dallaraF3",
        CarScreenName = "Dallara F3",
        MinValue = 0,
        MaxValue = 12,
        OffValue = 0,
        Direction = TcScaleDirection.Normal
    },
    
    ["dallarap217"] = new TractionControlConfig
    {
        CarPath = "dallarap217",
        CarScreenName = "Dallara P217",
        MinValue = 0,
        MaxValue = 20,
        OffValue = 0,
        Direction = TcScaleDirection.Normal
    },
    
    // Inverted scale cars (12 = OFF)
    ["ferrari488gt3"] = new TractionControlConfig
    {
        CarPath = "ferrari488gt3",
        CarScreenName = "Ferrari 488 GT3",
        MinValue = 1,
        MaxValue = 12,
        OffValue = 12,
        Direction = TcScaleDirection.Inverted
    },
    
    // TODO: Add more cars as we discover them
};
```

**Pros**:
- ✅ Accurate per-car behavior
- ✅ Can display "OFF" correctly
- ✅ Can show meaningful level (1-11 instead of raw 12-1)

**Cons**:
- ❌ Requires manual database maintenance
- ❌ Breaks when iRacing adds new cars
- ❌ Community effort needed to map all cars

---

### Solution 2: Heuristic Detection (SEMI-RELIABLE)

Detect OFF value by observing when TC value changes:

```csharp
private int? _lastTcValue = null;
private int _tcOffCandidate = -1;

private void DetectTcScale(int currentTcValue)
{
    if (_lastTcValue == null)
    {
        _lastTcValue = currentTcValue;
        return;
    }
    
    // If TC jumps to 0, assume 0 is OFF
    if (currentTcValue == 0 && _lastTcValue > 0)
    {
        _tcOffCandidate = 0;
    }
    
    // If TC jumps to 12 from lower value, assume 12 is OFF
    if (currentTcValue == 12 && _lastTcValue < 12)
    {
        _tcOffCandidate = 12;
    }
    
    _lastTcValue = currentTcValue;
}
```

**Pros**:
- ✅ No manual database required
- ✅ Works for any car automatically

**Cons**:
- ❌ Unreliable: Requires driver to cycle TC to OFF
- ❌ Doesn't detect scale direction
- ❌ False positives possible

---

### Solution 3: Display Raw Value (CURRENT IMPLEMENTATION)

Just show the raw value without interpretation:

```csharp
TelemetryField.TractionControl => value switch
{
    int tc => tc < 0 ? "N/A" : tc == 0 ? "OFF" : $"{tc}",
    float tcf => tcf < 0 ? "N/A" : tcf == 0 ? "OFF" : $"{(int)tcf}",
    _ => "---"
}
```

**Pros**:
- ✅ Simple implementation
- ✅ Always shows what iRacing shows

**Cons**:
- ❌ Confusing: "12" might mean OFF or MAX depending on car
- ❌ No semantic meaning
- ❌ User has to know their car's TC system

---

### Solution 4: Community-Sourced Database (BEST LONG-TERM)

Create a JSON configuration file that users can contribute to:

```json
{
  "version": "2025.10.18",
  "cars": {
    "ferrari488gt3": {
      "name": "Ferrari 488 GT3",
      "tc": {
        "available": true,
        "min": 1,
        "max": 12,
        "off": 12,
        "direction": "inverted",
        "description": "12=OFF, 11=Low, 1=High"
      }
    },
    "dallaraF3": {
      "name": "Dallara F3",
      "tc": {
        "available": true,
        "min": 0,
        "max": 12,
        "off": 0,
        "direction": "normal",
        "description": "0=OFF, 1=Low, 12=High"
      }
    },
    "streetstock": {
      "name": "Street Stock",
      "tc": {
        "available": false
      }
    }
  }
}
```

Load from file at startup:

```csharp
public class CarTelemetryDatabase
{
    private Dictionary<string, CarTcConfig> _database;
    
    public void LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<CarDatabaseConfig>(json);
        _database = config.Cars;
    }
    
    public CarTcConfig? GetTcConfig(string carPath)
    {
        return _database.TryGetValue(carPath, out var config) ? config : null;
    }
}
```

**Pros**:
- ✅ User-updatable without code changes
- ✅ Community can contribute via GitHub
- ✅ Fallback to raw display if car not in database

**Cons**:
- ❌ Requires file distribution/management
- ❌ Still needs initial population

---

## 📊 Recommended Approach

### Phase 1: Immediate Fix (Keep Current Behavior)

**Current code is CORRECT for now**:
- Display raw value (what iRacing shows)
- Show "OFF" only when value is 0
- Show "N/A" when value is -1

**Reasoning**:
- Most common cars use normal scale (0 = OFF)
- Advanced users know their car's TC system
- No worse than iRacing's own display

### Phase 2: Add Car Database (Community Effort)

1. Create `car-telemetry-database.json` file
2. Populate with known cars (Ferrari 488 GT3, Dallara F3, etc.)
3. Load at startup, use for TC display
4. Fall back to raw display if car not found

### Phase 3: Smart Display Logic

```csharp
public string FormatTcValue(int tcValue, string carPath)
{
    // Try to get car config
    var config = CarDatabase.GetTcConfig(carPath);
    
    if (config == null)
    {
        // Fallback: Raw display
        return tcValue < 0 ? "N/A" : tcValue == 0 ? "OFF" : $"{tcValue}";
    }
    
    // Car-specific formatting
    if (!config.Tc.Available)
        return "N/A";
    
    if (tcValue == config.Tc.Off)
        return "OFF";
    
    // Normalize to 1-11 scale for inverted cars
    if (config.Tc.Direction == "inverted")
    {
        int normalizedLevel = config.Tc.Max - tcValue; // 12-11=1, 12-1=11
        return $"{normalizedLevel}";
    }
    
    // Normal scale: just show value
    return $"{tcValue}";
}
```

---

## 🔍 Research Needed

### Community Questions

Need to confirm TC scale for these cars:

**GT3 Cars** (likely mixed):
- ✅ Ferrari 488 GT3: **INVERTED** (12=OFF, confirmed by user)
- ❓ Mercedes-AMG GT3: Normal or inverted?
- ❓ BMW M4 GT3: Normal or inverted?
- ❓ Porsche 911 GT3 R: Normal or inverted?
- ❓ Audi R8 LMS: Normal or inverted?

**LMP2 Cars** (likely normal):
- ✅ Dallara P217: **NORMAL** (0=OFF, assumed)
- ❓ Other LMP2 cars?

**Formula Cars** (likely normal):
- ✅ Dallara F3: **NORMAL** (0=OFF, assumed)
- ❓ Formula Renault 3.5?
- ❓ Other formula cars?

### Data Collection Method

1. **Manual Testing**: Drive each car, cycle TC from min to max, note OFF value
2. **Community Survey**: Ask iRacing community to submit TC ranges
3. **Replay Analysis**: Parse IBT files with known cars and TC settings

---

## 🎯 Action Items

### For Now (Keep Working)
- [x] Document TC scale issue
- [ ] Update `TRACTION_CONTROL_REFERENCE.md` with scale warning
- [ ] Add comment in code about scale ambiguity
- [ ] Consider adding tooltip: "Raw TC value (scale varies by car)"

### For Next Sprint (Database Implementation)
- [ ] Create `car-telemetry-database.json` structure
- [ ] Add JSON loading to `IRacingTelemetryService`
- [ ] Implement smart TC formatting with fallback
- [ ] Test with Ferrari 488 GT3 (inverted) and Dallara F3 (normal)

### For Community (Long-term)
- [ ] Create GitHub issue for TC scale contributions
- [ ] Template for car TC scale submissions
- [ ] Automated testing with IBT replay files

---

## 📚 References

- **SDK Variable**: `TelemetryVar.dcTractionControl` (raw float/int value)
- **YAML Data**: No TC metadata available
- **iRacing Forums**: Community discussions about TC systems
- **Real Car Specs**: Ferrari 488 GT3 has 12-position dial (12=OFF)

---

**Last Updated**: October 18, 2025  
**Status**: Issue confirmed, workaround documented, database solution planned  
**Next Steps**: Keep current raw display, add car database in next sprint
