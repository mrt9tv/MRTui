using iRacingOverlay.Core.Models;
using Microsoft.Extensions.Logging;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Position calculation modes based on session type and state
/// </summary>
public enum PositionCalculationMode
{
    RaceLaps,        // Race mode: lap count + track position
    FastestLap,      // Qual/Practice: fastest lap time
    Frozen,          // Checkered/CoolDown: frozen final positions
    Unavailable      // Fallback to SDK position
}

/// <summary>
/// Calculates live position based on session type (Race, Qualifying, Practice)
/// with position freezing on checkered flag for accurate final results
/// </summary>
public class LivePositionCalculator
{
    private readonly ILogger<LivePositionCalculator>? _logger;

    // Position freeze cache (for checkered flag → cool-down)
    private int _frozenOverallPosition = -1;
    private int _frozenClassPosition = -1;
    private int _lastSessionState = -1;
    private int _lastSessionNum = -1;

    public LivePositionCalculator(ILogger<LivePositionCalculator>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculate live position - main entry point
    /// Handles race mode, qualifying mode, and position freezing on checkered flag
    /// </summary>
    public int CalculateLivePosition(TelemetryData data, bool classOnly)
    {
        // Detect session changes (clear frozen positions on new session)
        if (data.SessionNum != _lastSessionNum)
        {
            ClearFrozenPositions();
            _lastSessionNum = data.SessionNum;
        }

        var mode = DetermineMode(data);

        // DEBUG: Log position calculation mode and inputs
        // Log on state changes OR first few laps OR every 5 seconds in practice
        bool shouldLog = data.Lap <= 2 ||
                        data.SessionState != _lastSessionState ||
                        (data.Lap % 5 == 0 && data.LapDistPct < 0.1); // Every 5 laps at start line

        if (shouldLog)
        {
            _logger?.LogInformation("[LIVE_POS_DEBUG] SessionState={State} (0=Invalid,1=GetInCar,2=Warmup,3=Parade,4=Racing,5=Checkered,6=CoolDown), Mode={Mode}, Lap={Lap}, LapDistPct={Pct:F3}, SessionType='{SessionType}'",
                data.SessionState, mode, data.Lap, data.LapDistPct, string.IsNullOrEmpty(data.SessionType) ? "NOT_PARSED" : data.SessionType);
        }

        // FROZEN MODE: Use cached positions (checkered flag dropped)
        if (mode == PositionCalculationMode.Frozen)
        {
            // Return cached frozen position
            int frozen = classOnly ? _frozenClassPosition : _frozenOverallPosition;
            if (frozen > 0)
            {
                _logger?.LogDebug("❄️ Using frozen position: {Position} (classOnly={ClassOnly})", frozen, classOnly);
                return frozen;
            }
            // Fallback if cache empty
            return data.Position;
        }

        // Calculate position based on mode
        int position = mode switch
        {
            PositionCalculationMode.RaceLaps => CalculateRacePosition(data, classOnly),
            PositionCalculationMode.FastestLap => CalculateQualifyingPosition(data, classOnly),
            _ => Math.Max(1, data.Position) // Fallback to SDK (already 1-indexed)
        };


        // Detect checkered flag transition: Racing (4) → Checkered (5)
        if (_lastSessionState == 4 && data.SessionState == 5)
        {
            // FREEZE the current positions!
            FreezePositions(data);
        }

        _lastSessionState = data.SessionState;

        return position;
    }

    /// <summary>
    /// Determine which position calculation mode to use based on session state
    /// Uses SessionState enum to infer session type (faster and more reliable than YAML parsing)
    /// </summary>
    public PositionCalculationMode DetermineMode(TelemetryData data)
    {
        int state = data.SessionState;

        // FROZEN MODE: Checkered flag (5) or Cool Down (6)
        if (state == 5 || state == 6)
        {
            return PositionCalculationMode.Frozen;
        }

        // RACE MODE: Active racing states
        // State 3 = ParadeLaps (formation lap before green flag)
        // State 4 = Racing (green flag, active racing)
        if (state == 4)
        {
            // Racing state (green flag) - use lap-based position calculation
            return PositionCalculationMode.RaceLaps;
        }

        // All pre-green flag states (0-3) - use SDK position
        // SDK position reflects grid order and is accurate before racing starts
        if (state == 0 || state == 1 || state == 2 || state == 3)
        {
            // GetInCar, Warmup, or ParadeLaps - use SDK position
            // Live calculation is unreliable before green flag
            return PositionCalculationMode.Unavailable;
        }

        // QUALIFYING/PRACTICE MODE: All other active states
        // State 1 = GetInCar (just getting in, no position yet)
        // State 2 = Warmup (warmup lap, use time-based like qualifying)
        // State 0 = Invalid (session not started)
        if (state == 2)
        {
            // Warmup state - use time-based like qualifying
            return PositionCalculationMode.FastestLap;
        }

        // For all other states (GetInCar, Invalid), try to determine from session type YAML
        // This handles practice/qualifying sessions that might not have specific states
        string sessionType = data.SessionType?.ToLower() ?? "";

        if (!string.IsNullOrEmpty(sessionType))
        {
            // If we have SessionType from YAML, use it
            if (sessionType.Contains("qualif") || sessionType.Contains("practice"))
            {
                return PositionCalculationMode.FastestLap;
            }

            if (sessionType.Contains("race"))
            {
                // Race session but not in racing state yet (probably in garage/grid)
                // Use lap-based calculation anyway
                return PositionCalculationMode.RaceLaps;
            }
        }

        // Ultimate fallback: If SessionState = 0 or 1, we're probably in practice/test
        // In these states, iRacing doesn't provide meaningful position data anyway
        // Use FastestLap mode as it's most appropriate for test/practice
        if (state == 0 || state == 1)
        {
            return PositionCalculationMode.FastestLap;
        }

        // Last resort fallback: use SDK position (but this should rarely happen now)
        return PositionCalculationMode.Unavailable;
    }

    /// <summary>
    /// RACE MODE: Calculate position based on lap count and track position
    /// Includes cars in pits (they're still in the race!)
    /// </summary>
    public int CalculateRacePosition(TelemetryData data, bool classOnly = true)
    {
        if (data.CarIdxLap == null || data.CarIdxLapDistPct == null)
        {
            _logger?.LogDebug("Race position calculation unavailable - missing CarIdx arrays");
            return data.Position; // Fallback to SDK
        }

        // SPECIAL CASE: Player in pit stall (after TOW or during service)
        // Use SDK position which is more reliable when player is in pits
        int? playerTrackSurface = data.CarIdxTrackSurface?[data.PlayerCarIdx];
        if (playerTrackSurface == 1) // InPitStall
        {
            // When in pit stall, your LapDistPct is invalid for position comparison
            // Use SDK position directly (already 1-indexed)
            return Math.Max(1, data.Position);
        }

        int carsAhead = 0;
        int playerLap = data.Lap;
        float playerPct = data.LapDistPct;
        int playerClass = data.PlayerCarClass;

        for (int carIdx = 0; carIdx < data.CarIdxLap.Length; carIdx++)
        {
            // Skip player
            if (carIdx == data.PlayerCarIdx)
                continue;

            // Skip invalid cars (disconnected/not in world)
            if (!IsCarActiveInRace(carIdx, data))
                continue;

            // Skip different class if calculating class position
            if (classOnly && data.CarIdxClass?[carIdx] != playerClass)
                continue;

            // Count if ahead in race
            if (IsCarAheadInRace(carIdx, data))
                carsAhead++;
        }

        int calculatedPosition = carsAhead + 1;

        // DEBUG: Log calculated position on first few laps
        if (data.Lap <= 2)
        {
            _logger?.LogInformation("[LIVE_POS_DEBUG] CalculateRacePosition: carsAhead={CarsAhead}, calculatedPosition={Position}, classOnly={ClassOnly}, SDKPosition={SDKPos}",
                carsAhead, calculatedPosition, classOnly, data.Position);
        }

        return calculatedPosition;
    }

    /// <summary>
    /// QUALIFYING MODE: Calculate position based on fastest lap time
    /// Lower time = better position
    /// Cars without lap times are ranked below cars with times (iRating tiebreaker: future enhancement)
    /// </summary>
    public int CalculateQualifyingPosition(TelemetryData data, bool classOnly = true)
    {
        if (data.CarIdxBestLapTime == null)
        {
            _logger?.LogDebug("Qualifying position calculation unavailable - missing CarIdxBestLapTime");
            return data.Position; // Fallback to SDK
        }

        float playerBestTime = data.LapBestLapTime;
        int playerClass = data.PlayerCarClass;

        // Count cars with valid times and cars without times (for ranking)
        int carsWithFasterTimes = 0;
        int carsWithTimesTotal = 0;
        int carsWithoutTimes = 0;

        for (int carIdx = 0; carIdx < data.CarIdxBestLapTime.Length; carIdx++)
        {
            if (carIdx == data.PlayerCarIdx)
                continue;

            // Skip different class if calculating class position
            if (classOnly && data.CarIdxClass?[carIdx] != playerClass)
                continue;

            float carBestTime = data.CarIdxBestLapTime[carIdx];

            if (carBestTime > 0)
            {
                // Car has a valid lap time
                carsWithTimesTotal++;

                // Count if faster than player
                if (playerBestTime > 0 && carBestTime < playerBestTime)
                {
                    carsWithFasterTimes++;
                }
            }
            else
            {
                // Car has no valid lap time
                carsWithoutTimes++;
            }
        }

        // Determine player position
        if (playerBestTime <= 0)
        {
            // Player has no valid lap - rank below all cars with times
            // Future: Use iRating for tiebreaker among cars without times
            // Current: Rank at bottom
            int position = carsWithTimesTotal + carsWithoutTimes + 1;

            // DEBUG: Log qualifying position when no lap time
            if (data.Lap <= 2)
            {
                _logger?.LogInformation("[LIVE_POS_DEBUG] Qualifying: Player has NO lap time. CarsWithTimes={CarsWithTimes}, CarsWithoutTimes={CarsWithout}, Position={Pos}",
                    carsWithTimesTotal, carsWithoutTimes, position);
            }

            return position;
        }
        else
        {
            // Player has a valid lap - rank by lap time
            int position = carsWithFasterTimes + 1;

            // DEBUG: Log qualifying position
            if (data.Lap <= 2)
            {
                _logger?.LogInformation("[LIVE_POS_DEBUG] Qualifying: PlayerTime={Time:F3}s, CarsFaster={Faster}, Position={Pos}, TotalWithTimes={Total}",
                    playerBestTime, carsWithFasterTimes, position, carsWithTimesTotal + 1);
            }

            return position;
        }
    }

    /// <summary>
    /// Check if car is actively racing (not disconnected, has valid position)
    /// Only excludes: disconnected cars and cars with invalid position data
    /// Cars in pits are INCLUDED (they're still in the race!)
    /// </summary>
    private bool IsCarActiveInRace(int carIdx, TelemetryData data)
    {
        // Check valid track position
        float carPct = data.CarIdxLapDistPct![carIdx];
        if (carPct < 0 || carPct > 1.0f)
            return false; // Not in world/invalid

        // Check track surface
        int? trackSurface = data.CarIdxTrackSurface?[carIdx];

        // Exclude cars not in world (disconnected/spectating)
        if (trackSurface == -1) // NotInWorld
            return false;

        // INCLUDE all other cars:
        // - InPitStall (1) - being serviced, they're still in the race
        // - ApproachingPits (2) - entering/exiting pits
        // - OnTrack (3) - racing on track
        // This is correct: cars in pits don't disappear from the race!

        return true;
    }

    /// <summary>
    /// Check if car is ahead of player in race (lap-aware with wrap-around handling)
    /// </summary>
    private bool IsCarAheadInRace(int carIdx, TelemetryData data)
    {
        int playerLap = data.Lap;
        int carLap = data.CarIdxLap![carIdx];

        // PRIORITY: Lap counter is absolute truth
        // Different lap = simple comparison, no position % needed
        if (carLap > playerLap)
            return true;  // Car is ahead (even if in pits!)
        if (carLap < playerLap)
            return false; // Player is lapping this car

        // Same lap = compare track position
        float playerPct = data.LapDistPct;
        float carPct = data.CarIdxLapDistPct![carIdx];

        // Simple direct comparison - car ahead if greater percentage
        // No complex wrap-around logic that causes instability
        return carPct > playerPct;
    }

    /// <summary>
    /// Freeze positions when checkered flag drops (Racing → Checkered transition)
    /// </summary>
    private void FreezePositions(TelemetryData data)
    {
        // Calculate and cache the final race positions
        _frozenClassPosition = CalculateRacePosition(data, classOnly: true);
        _frozenOverallPosition = CalculateRacePosition(data, classOnly: false);

        _logger?.LogInformation("🏁 CHECKERED FLAG: Frozen positions - Overall: P{Overall}, Class: P{Class}",
            _frozenOverallPosition, _frozenClassPosition);
    }

    /// <summary>
    /// Clear frozen position cache (on session change)
    /// </summary>
    private void ClearFrozenPositions()
    {
        if (_frozenClassPosition > 0 || _frozenOverallPosition > 0)
        {
            _logger?.LogDebug("Clearing frozen positions (session change detected)");
        }

        _frozenOverallPosition = -1;
        _frozenClassPosition = -1;
        _lastSessionState = -1;
    }
}
