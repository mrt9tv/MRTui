using System;
using System.Collections.Generic;
using System.Linq;
using iRacingOverlay.Core.Models;
using Microsoft.Extensions.Logging;

namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Validates lap records for inclusion in fuel averaging calculations.
/// Handles detection of formation laps, pace laps, pit laps, out-laps, grid start partial laps, and incomplete laps.
/// Extracted from FuelCalculatorService (Optional Fine-Tuning phase).
/// </summary>
public class LapValidator
{
    private readonly ILogger<LapValidator> _logger;

    public LapValidator(ILogger<LapValidator>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LapValidator>.Instance;
    }

    /// <summary>
    /// Validate a completed lap and create lap history record with validation flags.
    /// </summary>
    /// <param name="telemetry">Current telemetry data</param>
    /// <param name="fuelAtLapStart">Fuel level when lap started</param>
    /// <param name="wasRefueled">Whether SDK detected refueling (from telemetry)</param>
    /// <param name="pittedThisLap">Whether driver entered pit road this lap</param>
    /// <param name="fuelBeforePit">Fuel level saved before entering pit road (0 if didn't pit)</param>
    /// <param name="justLeftPits">Whether driver just exited pit road (for out-lap detection)</param>
    /// <param name="sessionStateAtLapStart">SessionState when lap started (for pace lap detection)</param>
    /// <param name="lapDistPctAtLapStart">LapDistPct when lap started (for grid start detection)</param>
    /// <param name="previousLapWasPitLap">Whether previous lap was a pit lap</param>
    /// <param name="currentFlagStatus">Current race flag status</param>
    /// <param name="lastIncidentCount">Incident count from previous lap</param>
    /// <returns>Validated FuelLapHistory record</returns>
    public FuelLapHistory ValidateAndCreateLapRecord(
        TelemetryData telemetry,
        float fuelAtLapStart,
        bool wasRefueled,
        bool pittedThisLap,
        float fuelBeforePit,
        bool justLeftPits,
        int sessionStateAtLapStart,
        float lapDistPctAtLapStart,
        bool previousLapWasPitLap,
        LapFlagStatus currentFlagStatus,
        int lastIncidentCount)
    {
        // Calculate fuel used this lap
        // FIX: Use saved fuel level from before pit entry to calculate fuel used
        // This prevents refueling from contaminating the lap fuel calculation
        float fuelAtLapEnd = pittedThisLap && fuelBeforePit > 0 ? fuelBeforePit : telemetry.FuelLevel;
        float fuelUsed = fuelAtLapStart - fuelAtLapEnd;

        _logger.LogDebug("LAP {Lap} fuel calculation: Start={StartFuel:F2}L, End={EndFuel:F2}L, Used={Used:F3}L, Pitted={Pitted}",
            telemetry.LapsCompleted, fuelAtLapStart, fuelAtLapEnd, fuelUsed, pittedThisLap);

        // Detect out-lap: first lap after leaving pits (previous lap was pit lap OR just left pits flag)
        bool isOutLap = justLeftPits || previousLapWasPitLap;

        // Formation lap (lap 0) should be skipped BEFORE calling this function
        bool isFormationLap = false;

        // FIX: Check if this is a pace lap using SessionState from lap START (not lap END)
        // PROBLEM: If lap 1 starts during parade (SessionState=3) but green flag drops mid-lap,
        //          SessionState will be 4 (Racing) when lap completes, incorrectly marking it as non-pace
        // SOLUTION: Use sessionStateAtLapStart which was saved when the lap began
        bool isPaceLap = sessionStateAtLapStart == 3;

        // FIX: WasPitLap should ONLY be true if we actually REFUELED (not low fuel or tow)
        // Only actual refueling should mark a lap as a pit lap
        bool wasPitLap = wasRefueled;
        
        // FIX: Detect grid start partial laps - when grid is behind S/F line
        // At race start, lap 1 may only cover a small portion of the track (grid to S/F)
        // This uses very little fuel (e.g., 0.09L instead of 1.0L) and corrupts averages
        // Detection: Lap 1 with less than 50% track distance covered
        float lapDistanceCovered = 1.0f - lapDistPctAtLapStart; // How much of track we actually covered
        if (lapDistanceCovered < 0) lapDistanceCovered += 1.0f; // Handle wrap-around
        bool isGridStartLap = telemetry.LapsCompleted == 1 && lapDistanceCovered < 0.5f;
        
        if (isGridStartLap)
        {
            _logger.LogDebug("🏁 GRID START LAP DETECTED: Lap 1 only covered {DistCovered:P0} of track (started at {StartPct:P0})",
                lapDistanceCovered, lapDistPctAtLapStart);
            _logger.LogDebug("   Fuel used: {FuelUsed:F3}L - This lap will be EXCLUDED from averages", fuelUsed);
        }

        // DEBUG: Log lap completion details with ALL validity flags
        _logger.LogDebug("Lap {Lap} completed: FuelAtStart={StartFuel:F3}L, FuelAtEnd={EndFuel:F3}L, FuelUsed={Used:F3}L",
            telemetry.LapsCompleted, fuelAtLapStart, fuelAtLapEnd, fuelUsed);
        _logger.LogDebug("  Flags: PitLap={PitLap}, OutLap={OutLap}, Formation={Formation}, PaceLap={PaceLap}, GridStart={GridStart} (SessionState@Start={StateStart}, @End={StateEnd}), EnteredPitRoad={Pitted}",
            wasPitLap, isOutLap, isFormationLap, isPaceLap, isGridStartLap, sessionStateAtLapStart, telemetry.SessionState, pittedThisLap);

        // Create lap history record
        var lapRecord = new FuelLapHistory
        {
            LapNumber = telemetry.LapsCompleted,
            FuelUsed = Math.Max(0, fuelUsed), // Don't record negative fuel
            LapTime = telemetry.LapLastLapTime,
            FuelAtStart = fuelAtLapStart,
            FuelAtEnd = fuelAtLapEnd, // Use saved fuel if pitted, current fuel otherwise
            FlagStatus = currentFlagStatus,
            WasPitLap = wasPitLap, // True if refueled (or towed with refuel)
            RefuelAmount = wasRefueled ? telemetry.FuelLevel - fuelAtLapStart : 0, // Calculate refuel amount
            IsFormationLap = isFormationLap,
            IsOutLap = isOutLap, // Flag out-laps for exclusion from averages (cool tires, careful driving)
            IsIncompleteLap = false, // If this method is called, the lap WAS completed (LapDistPct resets to 0)
            IsGridStartLap = isGridStartLap, // FIX: Grid start partial lap (grid behind S/F line)
            LapDistanceCovered = lapDistanceCovered, // Track percentage of lap covered
            Timestamp = DateTime.UtcNow,
            IncidentCountAtStart = lastIncidentCount,  // Incident count at lap start
            IncidentCountAtEnd = telemetry.PlayerCarMyIncidentCount,  // Incident count at lap end
            SessionState = sessionStateAtLapStart  // FIX: Use SessionState from lap START for accurate pace lap detection
        };
        
        // DEBUG: Log validation result
        _logger.LogDebug("  IsValidForAveraging={Valid} (needs: !PitLap && !Formation && !Incomplete && !PaceLap && !OutLap && !GridStart && FuelUsed>0)",
            lapRecord.IsValidForAveraging);
        
        return lapRecord;
    }

    /// <summary>
    /// Calculate lap-to-lap fuel usage delta.
    /// </summary>
    /// <param name="lapHistory">Complete lap history</param>
    /// <param name="currentLap">Current lap record</param>
    /// <returns>Fuel usage delta (positive = using more fuel, negative = saving fuel)</returns>
    public float CalculateLapToLapDelta(List<FuelLapHistory> lapHistory, FuelLapHistory currentLap)
    {
        if (!currentLap.IsValidForAveraging)
            return 0f;

        var previousValidLap = lapHistory
            .Where(l => l.IsValidForAveraging && l.LapNumber < currentLap.LapNumber)
            .OrderByDescending(l => l.LapNumber)
            .FirstOrDefault();
        
        if (previousValidLap == null)
            return 0f;

        return currentLap.FuelUsed - previousValidLap.FuelUsed;
    }
}
