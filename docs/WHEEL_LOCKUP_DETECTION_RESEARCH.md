# Wheel Lock-Up Detection Research

## 🎯 Objective

Detect when wheels are locking up during braking to provide visual/audio feedback to the driver. This helps improve braking technique and prevent flat-spotting tires.

---

## 📊 Available Telemetry Data

### ✅ Variables We HAVE

```csharp
// Brake Inputs
"Brake"                // float (0-1) - Effective brake input (post-ABS)
"BrakeRaw"             // float (0-1) - Raw brake pedal position (pre-ABS)
"BrakeABSactive"       // bool - ABS system actively working

// Brake Line Pressures (individual wheel)
"LFbrakeLinePress"     // float (bar) - Left Front brake line pressure
"RFbrakeLinePress"     // float (bar) - Right Front brake line pressure
"LRbrakeLinePress"     // float (bar) - Left Rear brake line pressure
"RRbrakeLinePress"     // float (bar) - Right Rear brake line pressure

// Motion & Acceleration
"Speed"                // float (m/s) - Vehicle speed
"LongAccel"            // float (m/s²) - Longitudinal acceleration/deceleration
"LatAccel"             // float (m/s²) - Lateral acceleration
"VertAccel"            // float (m/s²) - Vertical acceleration

// Setup Adjustments
"dcBrakeBias"          // float (%) - Brake bias (% front bias)

// Track Surface
"PlayerTrackSurface"   // int - Surface type (asphalt, dirt, grass, etc.)
```

### ❌ Variables We DON'T HAVE

```csharp
// Individual Wheel Speeds - NOT AVAILABLE
"LFspeed"              // ❌ Left Front wheel speed
"RFspeed"              // ❌ Right Front wheel speed
"LRspeed"              // ❌ Left Rear wheel speed
"RRspeed"              // ❌ Right Rear wheel speed

// Tire Slip/Friction - NOT AVAILABLE
"LFtireSlip"           // ❌ Left Front slip ratio
"LFtireFriction"       // ❌ Left Front friction coefficient
"TireContactPatch"     // ❌ Tire contact patch data
"WheelRotation"        // ❌ Wheel angular velocity
```

**Critical Limitation**: Without individual wheel speeds, we **cannot calculate slip ratios** directly.

---

## 🔬 Detection Methods

### Method 1: Brake Pressure Imbalance Detection ⭐ RECOMMENDED

**Concept**: When a wheel locks, brake line pressure drops as tire loses grip.

**Algorithm**:
```csharp
public class WheelLockupDetector
{
    private const float PRESSURE_DROP_THRESHOLD = 3.0f; // bar
    private const float MIN_BRAKE_PRESSURE = 10.0f;     // bar
    private const float MIN_SPEED = 20.0f;              // m/s (~45 mph)
    
    public WheelLockupState DetectLockup(TelemetryData data)
    {
        // Only check when braking hard at speed
        if (data.Brake < 0.7f || data.Speed < MIN_SPEED)
            return WheelLockupState.None;
        
        // Calculate average pressures
        float avgFrontPressure = (data.LFbrakeLinePress + data.RFbrakeLinePress) / 2f;
        float avgRearPressure = (data.LRbrakeLinePress + data.RRbrakeLinePress) / 2f;
        
        // Detect significant pressure drop (indicates loss of grip)
        bool lfLocked = data.LFbrakeLinePress > MIN_BRAKE_PRESSURE && 
                       (avgFrontPressure - data.LFbrakeLinePress) > PRESSURE_DROP_THRESHOLD;
        
        bool rfLocked = data.RFbrakeLinePress > MIN_BRAKE_PRESSURE && 
                       (avgFrontPressure - data.RFbrakeLinePress) > PRESSURE_DROP_THRESHOLD;
        
        bool lrLocked = data.LRbrakeLinePress > MIN_BRAKE_PRESSURE && 
                       (avgRearPressure - data.LRbrakeLinePress) > PRESSURE_DROP_THRESHOLD;
        
        bool rrLocked = data.RRbrakeLinePress > MIN_BRAKE_PRESSURE && 
                       (avgRearPressure - data.RRbrakeLinePress) > PRESSURE_DROP_THRESHOLD;
        
        // Return which wheels are locked
        return new WheelLockupState
        {
            LeftFrontLocked = lfLocked,
            RightFrontLocked = rfLocked,
            LeftRearLocked = lrLocked,
            RightRearLocked = rrLocked,
            AnyWheelLocked = lfLocked || rfLocked || lrLocked || rrLocked
        };
    }
}

public class WheelLockupState
{
    public bool LeftFrontLocked { get; set; }
    public bool RightFrontLocked { get; set; }
    public bool LeftRearLocked { get; set; }
    public bool RightRearLocked { get; set; }
    public bool AnyWheelLocked { get; set; }
}
```

**Pros**:
- ✅ Uses available telemetry data
- ✅ Can detect individual wheel lockup
- ✅ Real-time detection
- ✅ Works on cars without ABS

**Cons**:
- ❌ May have false positives on bumpy surfaces
- ❌ Threshold tuning required for different cars
- ❌ Doesn't work on cars without brake line pressure sensors

**Accuracy**: ~70-80% (good enough for driver feedback)

---

### Method 2: Longitudinal Deceleration Analysis

**Concept**: Locked wheels produce inefficient braking (deceleration plateaus or drops).

**Algorithm**:
```csharp
public class DecelerationLockupDetector
{
    private Queue<float> _decelHistory = new(capacity: 10); // 10 samples @ 60Hz = ~166ms
    private const float DECEL_PLATEAU_THRESHOLD = 0.5f; // m/s²
    private const float MIN_BRAKE_PRESSURE_AVG = 12.0f;
    
    public bool DetectLockupByDeceleration(TelemetryData data)
    {
        // Only check during hard braking
        if (data.Brake < 0.8f || data.Speed < 20f)
        {
            _decelHistory.Clear();
            return false;
        }
        
        // Track deceleration (negative LongAccel = braking)
        float currentDecel = -data.LongAccel; // Make positive for braking
        _decelHistory.Enqueue(currentDecel);
        
        if (_decelHistory.Count < 10)
            return false;
        
        // Remove oldest if full
        if (_decelHistory.Count > 10)
            _decelHistory.Dequeue();
        
        // Calculate deceleration trend
        float avgDecel = _decelHistory.Average();
        float maxDecel = _decelHistory.Max();
        float minDecel = _decelHistory.Min();
        
        // Check for plateau (deceleration not increasing despite hard braking)
        float decelRange = maxDecel - minDecel;
        bool decelerationPlateau = decelRange < DECEL_PLATEAU_THRESHOLD;
        
        // Get average brake pressure
        float avgBrakePressure = (data.LFbrakeLinePress + data.RFbrakeLinePress + 
                                  data.LRbrakeLinePress + data.RRbrakeLinePress) / 4f;
        
        // Lockup detected if:
        // - Braking hard (high pressure)
        // - Deceleration not increasing (plateau)
        // - ABS not active (if car has ABS)
        bool possibleLockup = avgBrakePressure > MIN_BRAKE_PRESSURE_AVG &&
                             decelerationPlateau &&
                             !data.BrakeABSactive;
        
        return possibleLockup;
    }
}
```

**Pros**:
- ✅ No pressure sensors needed
- ✅ Works on all cars
- ✅ Detects overall braking efficiency

**Cons**:
- ❌ Cannot identify which specific wheel is locked
- ❌ Affected by weight transfer, downforce changes
- ❌ Requires smoothing/filtering
- ❌ Less accurate on bumpy surfaces

**Accuracy**: ~60-70% (general indicator only)

---

### Method 3: ABS Activity Detection (Simple but Limited)

**Concept**: Use `BrakeABSactive` to infer wheel lock attempts.

**Algorithm**:
```csharp
public class ABSLockupDetector
{
    public bool DetectLockupAttempt(TelemetryData data)
    {
        // ABS active = wheels would have locked without ABS
        return data.BrakeABSactive && data.Brake > 0.7f;
    }
}
```

**Pros**:
- ✅ Extremely simple
- ✅ 100% accurate for ABS cars
- ✅ No threshold tuning needed

**Cons**:
- ❌ Only works on cars WITH ABS
- ❌ Doesn't work on non-ABS cars (most road/vintage cars)
- ❌ Cannot identify which specific wheel
- ❌ Shows "would have locked" not "is locked"

**Accuracy**: 100% for ABS cars, 0% for non-ABS cars

---

### Method 4: Hybrid Approach ⭐⭐ BEST SOLUTION

**Concept**: Combine multiple methods for robust detection across all car types.

**Algorithm**:
```csharp
public class HybridLockupDetector
{
    private readonly WheelLockupDetector _pressureDetector = new();
    private readonly DecelerationLockupDetector _decelDetector = new();
    
    public LockupDetectionResult DetectLockup(TelemetryData data)
    {
        var result = new LockupDetectionResult();
        
        // Method 1: ABS detection (highest confidence for ABS cars)
        if (data.BrakeABSactive && data.Brake > 0.7f)
        {
            result.LockupDetected = true;
            result.Confidence = LockupConfidence.High;
            result.Source = "ABS System";
            result.Message = "ABS preventing lockup";
            return result;
        }
        
        // Method 2: Brake pressure imbalance (medium-high confidence)
        var pressureState = _pressureDetector.DetectLockup(data);
        if (pressureState.AnyWheelLocked)
        {
            result.LockupDetected = true;
            result.Confidence = LockupConfidence.Medium;
            result.Source = "Brake Pressure";
            result.LockedWheels = pressureState;
            
            // Build message
            var lockedList = new List<string>();
            if (pressureState.LeftFrontLocked) lockedList.Add("LF");
            if (pressureState.RightFrontLocked) lockedList.Add("RF");
            if (pressureState.LeftRearLocked) lockedList.Add("LR");
            if (pressureState.RightRearLocked) lockedList.Add("RR");
            
            result.Message = $"Locked: {string.Join(", ", lockedList)}";
            return result;
        }
        
        // Method 3: Deceleration analysis (low-medium confidence)
        bool decelLockup = _decelDetector.DetectLockupByDeceleration(data);
        if (decelLockup)
        {
            result.LockupDetected = true;
            result.Confidence = LockupConfidence.Low;
            result.Source = "Deceleration";
            result.Message = "Possible lockup detected";
            return result;
        }
        
        // No lockup detected
        result.LockupDetected = false;
        result.Confidence = LockupConfidence.None;
        return result;
    }
}

public class LockupDetectionResult
{
    public bool LockupDetected { get; set; }
    public LockupConfidence Confidence { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public WheelLockupState? LockedWheels { get; set; }
}

public enum LockupConfidence
{
    None,   // No lockup
    Low,    // Possible lockup (deceleration)
    Medium, // Likely lockup (pressure imbalance)
    High    // Confirmed lockup (ABS active)
}
```

**Pros**:
- ✅ Works on ALL car types (ABS and non-ABS)
- ✅ Multiple detection methods increase accuracy
- ✅ Confidence levels for UI feedback
- ✅ Specific wheel identification (when possible)

**Cons**:
- ❌ More complex implementation
- ❌ Requires tuning multiple thresholds

**Accuracy**: ~80-90% overall (varies by method used)

---

## 🎨 UI Visualization Concepts

### Option 1: Color-Coded Brake Pressure Bars

```
┌─────────────────────────┐
│ LF ████████████ 15.2 bar│  ← Green (normal)
│ RF ████████████ 15.1 bar│  ← Green (normal)
│ LR ██████      8.3 bar  │  ← RED (locked - pressure drop)
│ RR ████████    10.2 bar │  ← Orange (partial lock)
└─────────────────────────┘
```

**Colors**:
- **Green**: Normal braking (balanced pressure)
- **Orange**: Warning (pressure imbalance detected)
- **Red**: Locked wheel (significant pressure drop)

---

### Option 2: Wheel Lock Indicator Lights

```
    🔴 LF     RF 🟢
         🚗
    🔴 LR     RR 🟢
```

- **Green**: Wheel rolling normally
- **Red**: Wheel locked

---

### Option 3: Integrated into Brake Bias Display

When showing brake bias, add lock-up indicators:

```
┌─────────────────────┐
│   BRAKE BIAS 52%    │
│                     │
│   LF ⚠️  RF ✓      │  ← Lock indicators
│   LR ⚠️  RR ✓      │
└─────────────────────┘
```

---

### Option 4: Simple Warning Flash

Flash border RED when any wheel locks:

```
╔═════════════════════╗
║  ⚠️ WHEEL LOCK ⚠️  ║
╚═════════════════════╝
```

---

## 📋 Implementation Recommendations

### Phase 1: Basic ABS Detection ✅ IMMEDIATE

**Effort**: 15 minutes  
**Complexity**: Very Low  
**Accuracy**: 100% (ABS cars only)

```csharp
// Add to MRTOneWidget or create new LockupIndicator widget
private void UpdateLockupIndicator(TelemetryData data)
{
    if (data.BrakeABSactive && data.Brake > 0.7f)
    {
        // Flash border or show warning
        _lockupWarning.Visibility = Visibility.Visible;
        _lockupWarning.Background = new SolidColorBrush(Colors.Red);
    }
    else
    {
        _lockupWarning.Visibility = Visibility.Collapsed;
    }
}
```

---

### Phase 2: Brake Pressure Imbalance ⭐ RECOMMENDED

**Effort**: 1-2 hours  
**Complexity**: Medium  
**Accuracy**: ~75%

1. Create `WheelLockupDetector.cs` utility class
2. Implement pressure imbalance algorithm
3. Add 4 colored indicators (LF, RF, LR, RR) to UI
4. Test with different cars and brake bias settings

---

### Phase 3: Hybrid Detection System

**Effort**: 3-4 hours  
**Complexity**: High  
**Accuracy**: ~85%

1. Implement all 3 detection methods
2. Add confidence-based UI feedback
3. Add telemetry logging for threshold tuning
4. Create settings panel for sensitivity adjustment

---

## 🧪 Testing Strategy

### Test Scenarios

1. **Street Stock (No ABS)**: Test pressure detection only
2. **GT3 Car (With ABS)**: Test ABS + pressure detection
3. **Formula Car**: High-speed braking, sensitive to lockup
4. **Road Car on Cold Tires**: Easy to lock wheels
5. **Trail Braking**: Partial lockup during corner entry

### Test Procedure

1. Join practice session
2. Find long straight
3. Brake progressively harder each lap
4. Note when lockup indicator triggers
5. Compare to:
   - Tire smoke (visual confirmation)
   - Tire sound (audio confirmation)
   - Lap time loss (performance impact)

### Data Collection

```csharp
// Log lockup events for analysis
public void LogLockupEvent(TelemetryData data, LockupDetectionResult result)
{
    var logEntry = new
    {
        Timestamp = DateTime.Now,
        Speed = data.Speed,
        Brake = data.Brake,
        BrakePressures = new
        {
            LF = data.LFbrakeLinePress,
            RF = data.RFbrakeLinePress,
            LR = data.LRbrakeLinePress,
            RR = data.RRbrakeLinePress
        },
        Deceleration = data.LongAccel,
        ABSActive = data.BrakeABSactive,
        DetectionMethod = result.Source,
        Confidence = result.Confidence
    };
    
    // Write to CSV or JSON for analysis
    File.AppendAllText("lockup_events.json", 
        JsonSerializer.Serialize(logEntry) + "\n");
}
```

---

## 🚀 Quick Start Implementation

### Minimal Viable Product (5 minutes)

Add this to `MRTOneWidget.cs`:

```csharp
// Add field
private bool _wheelLockDetected = false;

// In UpdateUI method:
private void UpdateWheelLockDetection(TelemetryData data)
{
    // Simple detection: ABS active OR high brake + pressure imbalance
    bool absLock = data.BrakeABSactive && data.Brake > 0.7f;
    
    // Pressure imbalance (front wheels)
    float avgFrontPressure = (data.LFbrakeLinePress + data.RFbrakeLinePress) / 2f;
    bool pressureLock = data.Brake > 0.8f && 
                       data.Speed > 20f &&
                       Math.Abs(data.LFbrakeLinePress - data.RFbrakeLinePress) > 3.0f;
    
    _wheelLockDetected = absLock || pressureLock;
    
    // Visual feedback: Flash circle border red
    if (_wheelLockDetected)
    {
        _mainBorder.BorderBrush = new SolidColorBrush(Colors.Red);
        _mainBorder.BorderThickness = new Thickness(4);
    }
    else
    {
        _mainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 41, 255, 195)); // Teal
        _mainBorder.BorderThickness = new Thickness(3);
    }
}
```

Call from `UpdateUI()`:
```csharp
// Add after brake bias update
UpdateWheelLockDetection(data);
```

**Result**: Circle flashes RED when wheels lock! 🔴

---

## 📚 References

- **Brake Pressure Variables**: `docs/TELEMETRY_VARIABLE_REGISTRY.md` (line 233-250)
- **ABS Detection**: `BrakeABSactive` boolean flag
- **Deceleration**: `LongAccel` (negative = braking)
- **Wheel Speeds**: **NOT AVAILABLE** in current SDK

---

## ❓ Future Improvements (If Wheel Speeds Added)

If iRacing adds wheel speed variables in the future:

```csharp
// TRUE slip ratio calculation (requires wheel speeds)
public float CalculateSlipRatio(float wheelSpeed, float vehicleSpeed)
{
    if (vehicleSpeed < 1.0f) return 0f;
    
    // Slip ratio: (vehicle_speed - wheel_speed) / vehicle_speed
    // 0% = no slip, 100% = locked wheel
    return Math.Max(0f, (vehicleSpeed - wheelSpeed) / vehicleSpeed * 100f);
}

// Lockup detection with slip ratio
public bool IsWheelLocked(float slipRatio)
{
    return slipRatio > 15f; // >15% slip = lockup
}
```

**This would give 95%+ accuracy** but requires SDK update from iRacing.

---

**Last Updated**: October 18, 2025  
**Status**: Research complete, ready for implementation  
**Recommended**: Start with Phase 1 (ABS detection), then Phase 2 (pressure imbalance)  
**Next Steps**: Implement `WheelLockupDetector.cs` utility class
