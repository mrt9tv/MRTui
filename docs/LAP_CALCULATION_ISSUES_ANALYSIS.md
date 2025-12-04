# Lap Calculation Issues - Analysis & Recommendations

## Executive Summary

The iRacing Telemetry Overlay has **three fundamental lap calculation concepts** that are conflated and confused throughout the codebase:

1. **Player's Current Lap** (what lap am I on right now?)
2. **Leader's Lap** (what lap is P1 on?)
3. **Race Total Laps** (how many total laps will the winner complete?)

These three values are **critical for accurate fuel strategy**, but currently have implementation issues that lead to incorrect calculations.

---

## Problem 1: Player's Current Lap Display

### Current Implementation
[FuelCalculatorService.cs:168-174](../src/iRacingOverlay.Core/Services/FuelCalculatorService.cs#L168-L174)
```csharp
// iRacing increments LapsCompleted at 0% LapDistPct
int displayLap = telemetry.LapsCompleted;
if (telemetry.LapDistPct < 0.05f && telemetry.LapsCompleted > 0)
{
    displayLap = telemetry.LapsCompleted - 1; // Show previous lap near start/finish
}
CurrentData.CurrentLap = displayLap + 1; // 0-based → 1-based
```

### Issue
- **User expects**: "Lap 5" to display from start/finish line crossing (0%) until next crossing
- **Current behavior**: Shows "Lap 4" near start/finish (0-5%), then "Lap 5" from 5-100%
- **Root cause**: Attempting to "delay" lap increment but creates confusing UX

### Impact
- User sees "Lap 4" when they've actually crossed into Lap 5
- Fuel calculations tied to `CurrentLap` become misaligned with user expectations
- Pit strategy recommendations reference wrong lap numbers

---

## Problem 2: Race Laps Remaining Calculation

### Current Implementation
[FuelCalculatorService.cs:192-216](../src/iRacingOverlay.Core/Services/FuelCalculatorService.cs#L192-L216)

**For Lap-Based Sessions:**
```csharp
CurrentData.RaceLapsRemaining = Math.Max(0, telemetry.SessionLaps - telemetry.LapsCompleted);
```

**For Time-Based Sessions:**
```csharp
if (CurrentData.AverageLapTime > 0 && CurrentData.SessionTimeRemaining > 0)
{
    CurrentData.EstimatedLapsFromTime = SessionTimeRemaining / AverageLapTime;
    CurrentData.RaceLapsRemaining = (int)Math.Round(EstimatedLapsFromTime);
}
```

### Issues

#### Issue 2a: Lap-Based Calculation Ambiguity
```csharp
// Scenario: 20-lap race, player on Lap 5
SessionLaps = 20          // Total laps in race
LapsCompleted = 4         // Player has FINISHED 4 laps
CurrentLap = 5            // Player is ON lap 5

// Current calculation:
RaceLapsRemaining = 20 - 4 = 16 laps

// BUT: Does this mean player will FINISH 16 more laps, or COMPLETE 15 + current lap?
// The leader might be on lap 6, so race ends in 14 laps for player!
```

**The ambiguity**: `RaceLapsRemaining` doesn't account for:
- Current lap in progress (should it count?)
- Leader's position (if leader finishes lap 20, player might only complete 18 laps if lapped)

#### Issue 2b: Time-Based Sessions - Lag Behind Leader
```csharp
// Scenario: 30-minute race, player averages 2 minutes/lap
SessionTimeRemaining = 1200 seconds (20 minutes)
AverageLapTime = 120 seconds
EstimatedLapsFromTime = 1200 / 120 = 10 laps

// BUT: This assumes player starts lap 11 right now
// If leader is faster (1:55/lap), leader will complete more laps
// When leader crosses finish on their last lap, player may be mid-lap
```

**The problem**: Time-based calculation ignores leader's pace and assumes player completes a full lap at the exact moment time expires.

---

## Problem 3: Leader's Lap Tracking

### Current Implementation
[FuelCalculatorService.cs:2097-2130](../src/iRacingOverlay.Core/Services/FuelCalculatorService.cs#L2097-L2130)
```csharp
// Find leader's laps completed (P1 position)
int leaderCarIdx = -1;
if (telemetry.CarIdxPosition != null)
{
    for (int i = 0; i < telemetry.CarIdxPosition.Length; i++)
    {
        if (telemetry.CarIdxPosition[i] == 1) // P1 = leader
        {
            leaderCarIdx = i;
            break;
        }
    }
}

if (leaderCarIdx >= 0 && telemetry.CarIdxLap != null)
{
    CurrentData.LeaderLapsCompleted = telemetry.CarIdxLap[leaderCarIdx];
    CurrentData.LapsBehindLeader = telemetry.LapsCompleted - CurrentData.LeaderLapsCompleted;
}
```

### Issues

#### Issue 3a: Position vs Leading Lap Confusion
- `CarIdxPosition[i] == 1` finds the **race leader by POSITION**
- **But**: In time-based races or with lapped cars, P1 might not be on the **leading lap**
- **Example**: P1 could be on lap 18 while P2 (unlapping themselves) is on lap 19

#### Issue 3b: No "Actual Leading Lap" Calculation
```csharp
// What we need: "What is the HIGHEST lap any car is on?"
int actualLeadingLap = telemetry.CarIdxLap.Max(); // Actual highest lap

// NOT the same as:
int leaderLap = telemetry.CarIdxLap[leaderIdx]; // P1's lap (might be behind if lapped)
```

#### Issue 3c: Race End Condition Uncertainty
```csharp
// When does the race actually end?

// Lap-based race (20 laps):
// - Race ends when LEADER crosses lap 20 start/finish
// - But which lap is "leader"? P1 by position or highest lap number?

// Time-based race (30 min):
// - Race ends when timer expires
// - Cars finish their current lap (white flag lap)
// - So player needs fuel for "laps until leader crosses during/after timer expiry"
```

---

## Problem 4: RaceLapsRemaining Usage Throughout Codebase

### Critical Dependencies

`RaceLapsRemaining` is used in **34+ locations** for fuel calculations:

1. **Fuel needed to finish** [FuelCalculatorService.cs:667](../src/iRacingOverlay.Core/Services/FuelCalculatorService.cs#L667)
   ```csharp
   float lapsToFinish = CurrentData.RaceLapsRemaining + CurrentData.FuelBufferLaps;
   ```

2. **Pit strategy windows** [PitStrategyService.cs:96](../src/iRacingOverlay.Core/Services/Fuel/PitStrategyService.cs#L96)
   ```csharp
   CalculatePitWindows(currentData, averages, lapsOnCurrentFuel, raceLapsRemaining, strategy);
   ```

3. **Fuel saving calculations** [FuelSavingCalculator.cs:50](../src/iRacingOverlay.Core/Services/Fuel/FuelSavingCalculator.cs#L50)
   ```csharp
   float fuelNeeded = (currentData.RaceLapsRemaining * baselineAverage) + sputteringThreshold;
   ```

4. **Multi-stop strategy** [FuelCalculatorService.cs:1866-1917](../src/iRacingOverlay.Core/Services/FuelCalculatorService.cs#L1866-L1917)
   ```csharp
   if (raceLapsRemaining <= maxStintLaps * 2) { /* 1-stop */ }
   if (raceLapsRemaining <= maxStintLaps * 3) { /* 2-stop */ }
   ```

**Impact**: If `RaceLapsRemaining` is off by even 1 lap, **all fuel strategies are incorrect**.

---

## Recommended Solutions

### Solution 1: Clarify Lap Semantics with Clear Naming

**Current** (ambiguous):
```csharp
CurrentData.CurrentLap = 5;              // What does this mean?
CurrentData.RaceLapsRemaining = 16;      // Remaining for who? Includes current lap?
```

**Proposed** (explicit):
```csharp
CurrentData.PlayerCurrentLapNumber = 5;        // Player is ON lap 5 right now
CurrentData.PlayerLapsCompleted = 4;           // Player FINISHED 4 laps
CurrentData.PlayerLapsToComplete = 16;         // Player will FINISH 16 more laps (total 20)
CurrentData.PlayerCurrentLapIncluded = false;  // Does LapsToComplete include current lap?

CurrentData.LeaderCurrentLapNumber = 6;        // Leader is ON lap 6
CurrentData.ActualLeadingLapNumber = 6;        // Highest lap anyone is on
```

### Solution 2: Race End Condition Service

Create a dedicated service to handle race end logic:

```csharp
public class RaceEndCalculator
{
    /// <summary>
    /// Calculate how many more FULL laps player will complete before race ends
    /// </summary>
    public int CalculatePlayerLapsToFinish(TelemetryData data)
    {
        // Lap-based race: Calculate based on leader's progress
        if (data.SessionLaps > 0)
        {
            int leadingLap = GetActualLeadingLap(data); // Highest lap anyone is on
            int raceFinishLap = data.SessionLaps;       // e.g., 20

            int leaderLapsToGo = raceFinishLap - leadingLap; // e.g., 20 - 6 = 14

            // Player needs fuel for:
            // - Current lap (if > 50% complete, might finish it)
            // - leaderLapsToGo full laps

            return leaderLapsToGo + (data.LapDistPct > 0.5 ? 1 : 0);
        }

        // Time-based race: Calculate based on time + pace difference
        else
        {
            float timeRemaining = (float)data.SessionTimeRemain;
            float playerLapTime = GetAverageLapTime(); // Player's pace

            // Estimate: How many laps until timer expires?
            float lapsFromTime = timeRemaining / playerLapTime;

            // Add current lap progress
            float currentLapProgress = data.LapDistPct;

            return (int)Math.Ceiling(lapsFromTime + currentLapProgress);
        }
    }

    /// <summary>
    /// Find the ACTUAL leading lap (highest lap number any car is on)
    /// This is NOT the same as the race leader's lap (P1 might be lapped)
    /// </summary>
    private int GetActualLeadingLap(TelemetryData data)
    {
        if (data.CarIdxLap == null || data.CarIdxLap.Length == 0)
            return data.Lap; // Fallback to player's lap

        return data.CarIdxLap.Max(); // Highest lap number
    }
}
```

### Solution 3: Simplified Player Lap Display

**Remove the 5% delay** - users are confused by seeing "Lap 4" when they've crossed into Lap 5:

```csharp
// BEFORE (confusing):
if (telemetry.LapDistPct < 0.05f && telemetry.LapsCompleted > 0)
{
    displayLap = telemetry.LapsCompleted - 1; // Show previous lap
}

// AFTER (clear):
CurrentData.PlayerCurrentLapNumber = telemetry.Lap; // Direct from SDK (1-based)
CurrentData.PlayerLapsCompleted = telemetry.LapsCompleted; // 0-based completed laps
```

**Rationale**: iRacing SDK already handles lap numbering correctly. Trust the SDK.

### Solution 4: Add Telemetry Fields for Clarity

Add to `TelemetryData.cs`:
```csharp
/// <summary>
/// ACTUAL leading lap in the race (highest lap any car is on)
/// Used to calculate race end condition accurately
/// </summary>
public int ActualLeadingLapNumber { get; set; }

/// <summary>
/// Race leader's current lap (P1 by position)
/// May differ from ActualLeadingLapNumber if leader is lapped
/// </summary>
public int RaceLeaderLapNumber { get; set; }
```

Calculate in `IRacingTelemetryService.cs`:
```csharp
// Calculate actual leading lap
if (sdkData.CarIdxLap != null && sdkData.CarIdxLap.Length > 0)
{
    data.ActualLeadingLapNumber = sdkData.CarIdxLap.Max();

    // Find race leader's lap (P1 position)
    for (int i = 0; i < sdkData.CarIdxPosition.Length; i++)
    {
        if (sdkData.CarIdxPosition[i] == 1)
        {
            data.RaceLeaderLapNumber = sdkData.CarIdxLap[i];
            break;
        }
    }
}
```

---

## Testing Strategy

### Test Scenarios Required

1. **Lap-Based Race - On Lead Lap**
   - 20-lap race, player on lap 5, leader on lap 5
   - Expected: `PlayerLapsToComplete = 15` (finish lap 5, then 14 more)

2. **Lap-Based Race - Lapped**
   - 20-lap race, player on lap 5, leader on lap 7 (player is 2 laps down)
   - Expected: `PlayerLapsToComplete = 13` (leader finishes in 13 laps, player stops mid-lap)

3. **Time-Based Race - Even Pace**
   - 30-min race, 20 min remaining, player averaging 2:00/lap
   - Expected: `PlayerLapsToComplete = 10`

4. **Time-Based Race - Slower Than Leader**
   - 30-min race, 20 min remaining, player 2:00/lap, leader 1:55/lap
   - Expected: Player completes ~10 laps, leader completes ~10.5 laps
   - Race ends when leader crosses during/after timer expiry

5. **Near Race End - Current Lap Handling**
   - Lap 20 of 20, player at 75% through lap
   - Expected: `PlayerLapsToComplete = 0` (will finish current lap but no more)

---

## Migration Path

### Phase 1: Add New Fields (Non-Breaking)
1. Add `ActualLeadingLapNumber`, `RaceLeaderLapNumber` to `TelemetryData`
2. Add `PlayerCurrentLapNumber`, `PlayerLapsToComplete` to `FuelData`
3. Keep existing fields for backward compatibility

### Phase 2: Implement RaceEndCalculator
1. Create new service with explicit lap finish calculations
2. Use new service in `FuelCalculatorService`
3. Compare results with old calculations (log differences)

### Phase 3: Update UI & Deprecate Old Fields
1. Update widgets to use new explicit field names
2. Mark old fields as `[Obsolete]` with migration hints
3. Update documentation

### Phase 4: Remove Deprecated Fields
1. After validation period, remove old ambiguous fields
2. Clean up all references
3. Update tests

---

## References

### Key Files
- [TelemetryData.cs](../src/iRacingOverlay.Core/Models/TelemetryData.cs) - Lap fields from SDK
- [FuelData.cs](../src/iRacingOverlay.Core/Models/FuelData.cs) - Lap calculation results
- [FuelCalculatorService.cs](../src/iRacingOverlay.Core/Services/FuelCalculatorService.cs) - Main lap logic
- [PitIntelligenceService.cs](../src/iRacingOverlay.Core/Services/PitIntelligenceService.cs) - Pit lap calculations

### SDK Reference
- iRacing SDK Variables: [docs/iRacing_SDK_Variables_Reference.md](../docs/iRacing_SDK_Variables_Reference.md)
  - `Lap` - Current lap (1-based)
  - `LapsCompleted` - Laps finished (0-based)
  - `SessionLaps` - Total laps in session (0 = time-based)
  - `CarIdxLap[64]` - Lap number for each car
  - `CarIdxPosition[64]` - Position for each car

---

## Priority Recommendations

**🔴 Critical (Do First)**:
1. Add `ActualLeadingLapNumber` calculation - this is THE missing piece
2. Fix `RaceLapsRemaining` to use actual leading lap, not P1's lap

**🟡 Important (Do Next)**:
3. Create `RaceEndCalculator` service with explicit lap finish logic
4. Remove 5% lap delay (trust SDK lap numbering)

**🟢 Nice to Have**:
5. Rename fields for clarity (breaking change, lower priority)
6. Add comprehensive lap calculation unit tests
