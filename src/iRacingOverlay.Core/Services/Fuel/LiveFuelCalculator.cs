using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Calculates live (mid-lap) fuel projections and strategy updates
/// Extracted from FuelCalculatorService Phase 6 refactoring
/// </summary>
public class LiveFuelCalculator
{
    /// <summary>
    /// Update live fuel values based on current lap progress
    /// Returns true if calculations were performed (lap progress > 5%)
    /// </summary>
    public bool UpdateLiveValues(
        TelemetryData telemetry,
        FuelData currentData,
        out LiveFuelData liveData)
    {
        liveData = new LiveFuelData();

        // Project current lap fuel usage to full lap
        float lapProgress = telemetry.LapDistPct;
        if (lapProgress <= 0.05f)
            return false; // Too early in lap for reliable projection

        // Calculate projected lap fuel usage based on current progress
        float fuelUsedSoFar = currentData.FuelUsedThisLap;
        float projectedLapUsage = fuelUsedSoFar / lapProgress;

        // Update current lap fuel rate (live projection)
        liveData.CurrentLapFuelRate = projectedLapUsage;

        // Calculate live laps remaining using projected usage
        // Uses current fuel and projected usage (more responsive than historical average)
        if (projectedLapUsage > 0)
        {
            liveData.LapsRemaining = telemetry.FuelLevel / projectedLapUsage;
        }

        // Update live fuel needed to finish (uses existing strategy average, not projected)
        // This balances live feel with strategic accuracy
        if (currentData.AvgFuelPerLap > 0)
        {
            liveData.FuelNeededToFinish = currentData.RaceLapsRemaining * currentData.AvgFuelPerLap;
            liveData.FuelDeltaToFinish = telemetry.FuelLevel - liveData.FuelNeededToFinish;
            liveData.CanFinishWithoutStop = liveData.FuelDeltaToFinish >= 0;
        }

        // ===== LIVE PIT WINDOW CALCULATIONS =====
        // Update pit window in real-time as player progresses through current lap
        // This makes strategy truly "live" instead of frozen until lap completion
        if (currentData.RaceLapsRemaining > 0 && currentData.AvgFuelPerLap_L5 > 0)
        {
            float avgFuel = currentData.AvgFuelPerLap_L5;
            float lapsOnCurrentFuel = liveData.LapsRemaining;

            // Current lap position (fractional lap number)
            // Example: Lap 10 at 50% = 10.5, Lap 15 at 75% = 15.75
            float currentLapFractional = telemetry.LapsCompleted + lapProgress;

            // LIVE FUEL CRITICALITY (updates every frame)
            if (lapsOnCurrentFuel < 1.0f)
                liveData.FuelCriticalityScore = 100f; // CRITICAL
            else if (lapsOnCurrentFuel < 2.0f)
                liveData.FuelCriticalityScore = 80f + (2.0f - lapsOnCurrentFuel) * 20f; // URGENT (80-100)
            else if (lapsOnCurrentFuel < 5.0f)
                liveData.FuelCriticalityScore = 50f + (5.0f - lapsOnCurrentFuel) * 10f; // MODERATE (50-80)
            else if (lapsOnCurrentFuel < 10.0f)
                liveData.FuelCriticalityScore = 20f + (10.0f - lapsOnCurrentFuel) * 6f; // COMFORTABLE (20-50)
            else
                liveData.FuelCriticalityScore = Math.Max(0f, 20f - (lapsOnCurrentFuel - 10.0f) * 2f); // PLENTY (0-20)

            // LIVE PIT WINDOW (earliest, optimal, latest)
            // Earliest: When enough fuel has been used to add race-ending fuel
            float fuelNeededToFinish = (currentData.RaceLapsRemaining + currentData.FuelBufferLaps) * avgFuel + currentData.FuelSputteringThreshold;
            float fuelAvailableToAdd = currentData.TankCapacity - currentData.CurrentFuel;
            float fuelNeedsToBurn = Math.Max(0, fuelNeededToFinish - fuelAvailableToAdd);
            float lapsToEarliestPit = fuelNeedsToBurn / avgFuel;
            liveData.EarliestPitLap = (int)Math.Ceiling(currentLapFractional + lapsToEarliestPit);

            // Latest: Just before running out (with buffer)
            float lapsToLatestPit = lapsOnCurrentFuel - currentData.FuelBufferLaps;
            liveData.LatestPitLap = (int)Math.Floor(currentLapFractional + Math.Max(0, lapsToLatestPit));

            // Optimal: Middle of pit window (balanced risk)
            if (liveData.EarliestPitLap <= liveData.LatestPitLap)
            {
                liveData.OptimalPitLap = (liveData.EarliestPitLap + liveData.LatestPitLap) / 2;
            }
            else
            {
                // Window collapsed - pit ASAP
                liveData.OptimalPitLap = liveData.EarliestPitLap;
            }
        }

        return true;
    }

    /// <summary>
    /// Apply real-time fuel flow integration using FuelUsePerHour telemetry
    /// Provides more accurate mid-lap fuel predictions based on instantaneous consumption
    /// </summary>
    public void ApplyRealTimeFuelFlow(
        TelemetryData telemetry,
        FuelData currentData,
        out float projectedLapFuel,
        out float currentLapFuelRate)
    {
        projectedLapFuel = 0f;
        currentLapFuelRate = 0f;

        // Calculate projected lap fuel if we have average lap time
        if (currentData.AverageLapTime > 0 && telemetry.FuelUsePerHour > 0)
        {
            // Convert L/hour to L/lap
            float hoursPerLap = currentData.AverageLapTime / 3600f;
            projectedLapFuel = telemetry.FuelUsePerHour * hoursPerLap;

            // Compare to historical average (only if valid)
            if (currentData.AvgFuelPerLap_L5 > 0)
            {
                float flowDelta = projectedLapFuel - currentData.AvgFuelPerLap_L5;

                // Log significant deviations (>0.3L difference)
                if (Math.Abs(flowDelta) > 0.3f)
                {
                    LogDebug($"FUEL FLOW: Current flow projects {projectedLapFuel:F3}L/lap vs avg {currentData.AvgFuelPerLap_L5:F3}L ({flowDelta:+0.00;-0.00}L)");
                }
            }

            // Use fuel flow for early lap prediction (after 30% but before 90% of lap)
            // This gives more accurate real-time estimates than waiting for lap completion
            if (telemetry.LapDistPct > 0.3f && telemetry.LapDistPct < 0.9f && projectedLapFuel > 0)
            {
                currentLapFuelRate = projectedLapFuel;
            }
        }
    }

    private void LogDebug(string message)
    {
        try
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MRT-UI",
                "fuel_debug.log"
            );

            var directory = System.IO.Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
            System.IO.File.AppendAllText(logPath, logMessage);
        }
        catch
        {
            // Ignore logging errors
        }
    }
}

/// <summary>
/// Live fuel calculation results
/// </summary>
public class LiveFuelData
{
    public float CurrentLapFuelRate { get; set; }
    public float LapsRemaining { get; set; }
    public float FuelNeededToFinish { get; set; }
    public float FuelDeltaToFinish { get; set; }
    public bool CanFinishWithoutStop { get; set; }
    public float FuelCriticalityScore { get; set; }
    public int EarliestPitLap { get; set; }
    public int LatestPitLap { get; set; }
    public int OptimalPitLap { get; set; }
}
