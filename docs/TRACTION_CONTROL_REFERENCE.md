# Traction Control (TC) Display Reference

## ⚠️ CRITICAL: TC Scales Vary by Car

**Important Discovery**: TC value meaning is **CAR-SPECIFIC** and NOT standardized!

- **Some cars**: TC 0 = OFF, higher = more TC (normal scale)
- **Other cars**: TC 12 = OFF, lower = more TC (inverted scale)
- **Example**: Ferrari 488 GT3 uses TC 12=OFF, while Dallara F3 uses TC 0=OFF

**See `TC_SCALE_INVESTIGATION.md` for full details and planned solutions.**

---

## 📊 TC Behavior Across Different Cars

### SDK Variable: `dcTractionControl`
- **Type**: `float` (cast to `int` in our code)
- **Range**: Car-specific, typically `-1` to `20`
- **Source**: iRacing SDK telemetry
- **⚠️ WARNING**: Scale direction varies by car (see above)

---

## 🚗 TC Values by Car Type

### **Cars WITHOUT Traction Control**
Most road cars, vintage race cars, and some modern race cars don't have TC systems.

| Value | Meaning | Display |
|-------|---------|---------|
| `-1` | Not Available | `N/A` |

**Examples**:
- Street Stock
- Late Model
- Most NASCAR vehicles
- Vintage Formula cars (Lotus 49, etc.)
- Most production cars without TC

---

### **Cars WITH Traction Control**
Modern race cars with electronic TC systems.

⚠️ **IMPORTANT**: TC scale direction varies by car! See examples below.

#### Normal Scale Cars (0 = OFF)

| Value | Meaning | Display | Note |
|-------|---------|---------|------|
| `0` | TC OFF | `OFF` | TC system exists but disabled |
| `1-20` | TC Level | `1` to `20` | Higher = more aggressive intervention |

**Examples (ASSUMED - need verification)**:
- **LMP2 Cars**: Typically `0-20` range
  - Dallara P217: 0-20 (0=OFF, 20=MAX)
  
- **Formula Cars**: Variable ranges
  - Dallara F3: 0-12 (0=OFF, 12=MAX)

#### Inverted Scale Cars (12 = OFF) ⚠️

| Value | Meaning | Display | Note |
|-------|---------|---------|------|
| `12` | TC OFF | `OFF` | TC disabled (dial position 12) |
| `11-1` | TC Level | `11` to `1` | **INVERTED**: Lower = more intervention |

**Examples (CONFIRMED)**:
- **GT3 Cars** (some models):
  - Ferrari 488 GT3: 12=OFF, 11=Low, 1=High (INVERTED SCALE)
  - Others TBD - need community testing

#### Unknown Scale (Needs Research) ❓

These cars have TC but scale direction is unconfirmed:
- BMW M4 GT3: 0-12 range (direction unknown)
- Mercedes-AMG GT3: 0-12 range (direction unknown)
- Porsche 911 GT3 R: 0-11 range (direction unknown)
- Ferrari 488 GTE: 0-12 range (direction unknown)
- Corvette C8.R: 0-12 range (direction unknown)

---

## 🔍 Understanding "OFF" vs N/A

### Key Distinction
| Display | Meaning | SDK Value | Car Has TC? |
|---------|---------|-----------|-------------|
| **N/A** | Car doesn't have TC | `-1` | ❌ No |
| **OFF** | TC disabled by driver | `0` | ✅ Yes |
| **1-20** | TC active at level X | `1-20` | ✅ Yes |

### Code Logic
```csharp
TelemetryField.TractionControl => value switch
{
    int tc => tc < 0 ? "N/A" : tc == 0 ? "OFF" : $"{tc}",
    float tcf => tcf < 0 ? "N/A" : tcf == 0 ? "OFF" : $"{(int)tcf}",
    _ => "---"
}
```

---

## 🎯 Display Behavior

### Single Digit TC (1-9)
**Old behavior**: `"  1 "` (with manual spacing)  
**New behavior**: `"1"` (TextBlock centers it automatically)

✅ **Result**: Properly centered with TC label

### Double Digit TC (10-20)
**Old behavior**: `" 10 "`  
**New behavior**: `"10"`

✅ **Result**: Properly centered with TC label

### OFF State
**Old behavior**: `" OFF"`  
**New behavior**: `"OFF"`

✅ **Result**: Properly centered with TC label

### N/A State
**Old behavior**: `" N/A"`  
**New behavior**: `"N/A"`

✅ **Result**: Properly centered with TC label

---

## 📝 YAML vs SDK Information

### Available in SDK
- ✅ **Current TC Level** (`dcTractionControl`): Real-time driver setting
- ✅ **TC Range**: Car-specific, but not explicitly documented in SDK
- ❌ **TC Active State**: No variable to detect when TC is actively cutting power

### Available in YAML (SessionInfo)
- ❌ **TC Availability**: Not explicitly flagged
- ❌ **TC Range**: Not documented in session info
- ❌ **TC Defaults**: Not provided

### Detection Method
**We detect TC capability by observing the value**:
- If value is `-1`: Car doesn't have TC
- If value is `0` or higher: Car has TC (OFF or active)

This is **runtime detection**, not configuration-based.

---

## 🚨 Important Notes

### No Active Detection
Unlike ABS (`BrakeABSactive`), there is **no TC activation variable**.

**What we CAN detect**:
- ✅ TC level setting (0-20)
- ✅ Whether TC exists on the car

**What we CANNOT detect**:
- ❌ When TC is actively cutting power
- ❌ How much power is being cut
- ❌ Wheel slip causing TC intervention

### Workaround
For TC activation detection, you would need:
- Wheel speed variables (`LFspeed`, `RFspeed`, etc.) - **NOT AVAILABLE**
- Or throttle difference: `ThrottleRaw - Throttle` (imperfect)

**Current best approach**:
```csharp
// Indirect TC intervention detection (not perfect)
float inputDifference = data.ThrottleRaw - data.Throttle;
bool tcLikelyActive = data.TractionControl > 0 && inputDifference > 0.1f;
```

---

## 🔧 Display Improvements

### Centering Fix
**Problem**: Single digit TC values appeared left-aligned under "TC" label  
**Solution**: Removed manual spacing, rely on WPF TextBlock centering

### TextBlock Properties
```csharp
_leftValueText = new TextBlock
{
    HorizontalAlignment = HorizontalAlignment.Center,  // Center in box
    TextAlignment = TextAlignment.Center,              // Center text content
    // ... other properties
};
```

**Result**: Both single and double digit values center properly under "TC" label

---

## 🎨 Visual Behavior

### Opacity Control
TC values have special opacity handling:

```csharp
// Dim when N/A (car doesn't have TC) or OFF (TC = 0)
// Orange when TC > 0 (enabled and potentially active)
```

**Visual Feedback**:
- `N/A`: Dimmed (0.3 opacity) - car doesn't have TC
- `OFF`: Dimmed (0.3 opacity) - TC exists but disabled
- `1-20`: Full brightness (1.0 opacity) + Orange color - TC active

---

## 📚 Reference Links
- **SDK Variable**: `TelemetryVar.dcTractionControl`
- **Telemetry Registry**: `docs/TELEMETRY_VARIABLE_REGISTRY.md` (line 268)
- **Data Model**: `TelemetryData.TractionControl` property
- **Widget Display**: `MRTOneWidget.cs` - `FormatFieldValue()` method

---

**Last Updated**: October 18, 2025  
**SDK Version**: irsdk_1_19  
**Widget**: MRT One Widget v2.0
