# 🎮 Telemetry Behavior Notes & FAQ

## Steering Wheel Display

### Current Behavior:
The steering bar shows: `[──────────│─────────────────────] -0.01rad (0.6°)`

### How it Works:
- **Center (│)** = Wheels straight ahead
- **Left Turn** = Negative values, bar moves LEFT
- **Right Turn** = Positive values, bar moves RIGHT

### Is This Correct?
**YES!** This is the correct representation:
- When you turn the wheel **LEFT**, the car turns LEFT, so the bar should show LEFT
- When you turn the wheel **RIGHT**, the car turns RIGHT, so the bar should show RIGHT

### Radians vs Degrees:
- **Radians:** Native iRacing unit (more precise for physics calculations)
- **Degrees:** More intuitive for humans
- We now show **BOTH**: `-0.52rad (29.8°)`

**Example:**
- Full lock left: ~-0.9 rad (~-52°)
- Straight: 0.0 rad (0°)
- Full lock right: ~+0.9 rad (~+52°)

---

## Clutch at 100%

### Your Observation:
"Why is clutch showing 100% when driving?"

### Explanation:
This depends on your setup:

#### **Scenario 1: Auto-Clutch Enabled (Most Likely)**
- iRacing has "Auto Clutch" option in settings
- When enabled, iRacing automatically engages/disengages clutch
- **While driving:** Clutch is 0% (released)
- **During shifts:** Clutch briefly goes to 100% (pressed)
- **At standstill:** May show 100% if auto-clutch is holding it

#### **Scenario 2: No Clutch Pedal**
- If you don't have a clutch pedal connected
- iRacing simulates clutch behavior
- May show 100% when not in motion

#### **Scenario 3: Clutch Pedal Pressed**
- If you DO have a clutch pedal
- 100% = Pedal fully pressed
- 0% = Pedal fully released

### How to Check:
1. **In iRacing:** Options → Controls → Clutch
2. Look for "Auto Clutch" setting
3. Check if you have a clutch pedal assigned

### What to Expect:
- **Driving normally:** 0% (clutch released)
- **Shifting gears:** Brief spike to 100%
- **Standing still:** May vary (0-100%)
- **Manual clutch cars:** Follows your pedal input

---

## Additional Telemetry We Could Add

### 🚗 **Vehicle Data:**
```
Currently Tracking:
✅ Speed (m/s, km/h, mph)
✅ RPM
✅ Gear
✅ Lap number
✅ Position in class

Could Add:
🔲 Fuel level (liters or %)
🔲 Fuel usage rate (L/lap)
🔲 Water temperature (°C)
🔲 Oil temperature (°C)
🔲 Oil pressure (kPa)
🔲 Estimated laps remaining (based on fuel)
```

### 🏁 **Race Information:**
```
Could Add:
🔲 Session time remaining
🔲 Session type (Practice, Qualify, Race)
🔲 Current lap time
🔲 Best lap time
🔲 Last lap time
🔲 Delta to best lap
🔲 Flags (green, yellow, blue, etc.)
🔲 Incident count
```

### 🎯 **Performance Data:**
```
Could Add:
🔲 G-Forces (lateral, longitudinal, vertical)
🔲 Tire temperatures (FL, FR, RL, RR)
🔲 Tire wear (%)
🔲 Tire pressure (kPa)
🔲 Brake temperatures
🔲 Track temperature
🔲 Air temperature
🔲 Wind speed & direction
```

### 👥 **Relative Positioning:**
```
Could Add:
🔲 Gap to car ahead (seconds)
🔲 Gap to car behind (seconds)
🔲 Gap to leader (seconds)
🔲 Position changes (up/down)
🔲 Fastest lap in session
```

### 🔧 **Damage & Wear:**
```
Could Add:
🔲 Engine damage (%)
🔲 Suspension damage
🔲 Aero damage
🔲 Tire condition
🔲 Clutch wear
🔲 Brake wear
```

---

## Recommendations for MVP 1

### **Keep for Now:**
✅ Speed, RPM, Gear (essential basics)
✅ Throttle, Brake, Clutch, Steering (driver inputs)
✅ Lap, Position (race context)
✅ Connection status, update rate (technical info)

### **Add in MVP 2 (Overlay):**
When we create the WPF overlay, we should add:
1. **Fuel Level** - Critical for race strategy
2. **Water/Oil Temp** - Important for car health
3. **Lap Times** - Essential for performance tracking
4. **Tire Temps** - Critical for setup and driving

### **Add in MVP 3+ (Advanced):**
- G-Force display
- Relative gaps to other cars
- Tire wear indicators
- Damage warnings

---

## Command-Line Options (NEW!)

### **Run Modes:**

**Default (Full Display):**
```bash
START_TELEMETRY.bat
# or
iRacingOverlay.Core.exe
```

**Quiet Mode (Minimal Output):**
```bash
iRacingOverlay.Core.exe --quiet
# or
iRacingOverlay.Core.exe -q
```
Shows: `✅ Connected | Uptime: 00:03:42 | Updates: 5,682 (25.6 Hz) | Speed: 45 km/h | RPM: 1786 | Gear: 4`

**Verbose Mode (Extra Debug Info):**
```bash
iRacingOverlay.Core.exe --verbose
# or
iRacingOverlay.Core.exe -v
```
Shows: Full display + additional logging

---

## Testing Checklist

### Steering Behavior:
- [ ] Turn wheel left → bar moves left ✅ CORRECT
- [ ] Turn wheel right → bar moves right ✅ CORRECT
- [ ] Degrees match wheel rotation ✅ VERIFY
- [ ] Center position accurate ✅ VERIFY

### Clutch Behavior:
- [ ] Check iRacing auto-clutch setting
- [ ] Verify clutch pedal (if present)
- [ ] Watch clutch during gear shifts
- [ ] Confirm 0% while driving in gear
- [ ] Confirm 100% when manually pressed

### New Features:
- [ ] `--quiet` mode works
- [ ] `--verbose` mode works
- [ ] Default mode unchanged
- [ ] Steering shows degrees
- [ ] All data still accurate

---

## Questions to Answer

### 1. Clutch at 100%
**Q:** Is this normal?  
**A:** YES, if you have auto-clutch enabled or no clutch pedal. Check iRacing settings.

### 2. Steering Direction
**Q:** Should left turn move bar left?  
**A:** YES! Current behavior is CORRECT. Bar matches wheel/car direction.

### 3. Radians vs Degrees
**Q:** Which should we show?  
**A:** BOTH! We now display: `-0.52rad (29.8°)` for best of both worlds.

### 4. Additional Telemetry
**Q:** What else should we track?  
**A:** For MVP 1: Keep it simple. For MVP 2: Add fuel, temps, lap times.

---

## Next Steps

1. **Test Steering Display** - Verify degrees calculation matches real wheel angle
2. **Check Clutch Setting** - Confirm auto-clutch behavior in iRacing
3. **Run Quiet Mode** - Test new `--quiet` flag
4. **Decide on Additional Data** - Pick 3-5 metrics to add in MVP 2

---

**Last Updated:** January 12, 2025  
**Status:** Phase 2 Complete - Quiet Mode Added ✅
