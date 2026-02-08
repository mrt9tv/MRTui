using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Calculates fuel saving (lift &amp; coast) requirements based on current consumption
/// vs estimated laps remaining. Populates the Phase 3 fuel saving fields in FuelData.
///
/// Core question answered: "How much do I need to lift &amp; coast to make it to the end?"
/// </summary>
public sealed class FuelSavingService
{
    // ── configuration ────────────────────────────────────────────────
    /// <summary>Default pit stop time (pit entry + service + exit) in seconds.</summary>
    private const float DEFAULT_PIT_TIME = 28f;

    /// <summary>
    /// Approximate time penalty factor. Saving 1% of fuel costs ~1.5% of lap time.
    /// Empirically, lift &amp; coast at corner exits is the primary technique.
    /// </summary>
    private const float FUEL_SAVE_TIME_PENALTY_FACTOR = 1.5f;

    /// <summary>
    /// Maximum realistic fuel saving per lap (L/lap reduction from average).
    /// Beyond this, the car is essentially driving a parade lap.
    /// Set to 40% of average consumption as ceiling.
    /// </summary>
    private const float MAX_SAVING_FRACTION = 0.40f;

    /// <summary>
    /// Minimum laps of data before fuel saving calculations are meaningful.
    /// </summary>
    private const int MIN_LAPS_FOR_SAVING = 3;

    // ── tracking state ──────────────────────────────────────────────
    private readonly List<float> _recentConsumption = new();
    private const int RECENT_WINDOW = 3; // compare last 3 laps
    private float _lastLapFuelUsed;
    private int _lastLapSeen = -1;

    /// <summary>
    /// Update fuel saving calculations. Call after FuelCalculatorService.Update().
    /// </summary>
    public void Update(FuelData fuel)
    {
        // ── guard: not enough data yet ──────────────────────────────
        if (!fuel.HasSufficientData || fuel.AvgFuelPerLap <= 0)
        {
            ClearSavingFields(fuel);
            return;
        }

        // Track per-lap actual consumption for recent window
        TrackRecentConsumption(fuel);

        float avg = fuel.AvgFuelPerLap;          // selected-method average
        float min = fuel.MinFuelPerLap;           // best efficiency ever achieved
        float current = fuel.CurrentFuel;
        float avgLapTime = fuel.AverageLapTime;

        // ── determine effective laps remaining ──────────────────────
        int effectiveRaceLaps = GetEffectiveRaceLaps(fuel);
        if (effectiveRaceLaps <= 0)
        {
            ClearSavingFields(fuel);
            return;
        }

        // ── core: can we finish at current pace? ────────────────────
        float fuelNeededAtAvg = effectiveRaceLaps * avg;
        float fuelDelta = current - fuelNeededAtAvg;

        fuel.NeedsFuelSaving = fuelDelta < 0;

        if (!fuel.NeedsFuelSaving)
        {
            // We're fine — clear saving mode but still show projected surplus
            ClearSavingFields(fuel);
            fuel.ProjectedFuelDelta = fuelDelta;
            fuel.CanSaveFuelToFinish = true;
            fuel.NeedsFuelSaving = false;
            fuel.StrategicAlert = fuelDelta < avg
                ? "FUEL MARGINAL — consider saving"
                : null;
            fuel.AlertSeverity = fuelDelta < avg ? 1 : 0;
            return;
        }

        // ── target consumption to finish without stopping ───────────
        // targetPerLap = currentFuel / lapsRemaining
        float targetPerLap = current / effectiveRaceLaps;
        float savingNeeded = avg - targetPerLap; // L/lap reduction needed

        fuel.FuelSavingTarget = savingNeeded;

        // ── is fuel saving physically possible? ─────────────────────
        // The saving range is between average and minimum ever achieved.
        // Allow a small extrapolation (10%) beyond min, since lift & coast
        // at additional corners can beat the historical min.
        float savableRange = avg - (min * 0.90f);
        fuel.CanSaveFuelToFinish = savableRange > 0 && savingNeeded <= savableRange;

        // ── current saving rate (recent laps vs average) ────────────
        float recentAvg = GetRecentAverage();
        if (recentAvg > 0)
        {
            fuel.CurrentSavingRate = avg - recentAvg; // positive = saving fuel
        }
        else
        {
            fuel.CurrentSavingRate = 0;
        }

        // ── is fuel saving working? ─────────────────────────────────
        // Working = current saving rate meets or exceeds target
        fuel.FuelSavingWorking = fuel.CurrentSavingRate >= savingNeeded;

        // ── projected fuel at finish ────────────────────────────────
        float effectiveRate = recentAvg > 0 ? recentAvg : avg;
        float projectedFuelAtFinish = current - (effectiveRate * effectiveRaceLaps);
        fuel.ProjectedFuelDelta = projectedFuelAtFinish;

        // ── saving progress (0-100%) ────────────────────────────────
        if (savingNeeded > 0 && fuel.CurrentSavingRate > 0)
        {
            fuel.SavingProgress = Math.Clamp(
                (fuel.CurrentSavingRate / savingNeeded) * 100f, 0f, 100f);
        }
        else
        {
            fuel.SavingProgress = 0;
        }

        // ── target lap time for conservation ────────────────────────
        if (avgLapTime > 10f && avg > 0)
        {
            // Saving fraction of fuel → proportional time penalty
            float savingFraction = Math.Clamp(savingNeeded / avg, 0, MAX_SAVING_FRACTION);
            float timePenaltyPerLap = avgLapTime * savingFraction * FUEL_SAVE_TIME_PENALTY_FACTOR;
            fuel.TargetLapTime = avgLapTime + timePenaltyPerLap;
        }

        // ── pit vs save strategy comparison ─────────────────────────
        CalculateStrategyComparison(fuel, effectiveRaceLaps, avg, avgLapTime, savingNeeded);

        // ── lift point suggestions ──────────────────────────────────
        CalculateLiftPoints(fuel, savingNeeded, avg);

        // ── strategic alerts ────────────────────────────────────────
        GenerateAlerts(fuel, effectiveRaceLaps);
    }

    // ── internal helpers ─────────────────────────────────────────────

    private void TrackRecentConsumption(FuelData fuel)
    {
        // Detect new lap completion by checking if FuelUsedLastLap changed
        if (fuel.CurrentLap != _lastLapSeen && fuel.FuelUsedLastLap > 0.01f)
        {
            if (Math.Abs(fuel.FuelUsedLastLap - _lastLapFuelUsed) > 0.001f)
            {
                _recentConsumption.Add(fuel.FuelUsedLastLap);
                _lastLapFuelUsed = fuel.FuelUsedLastLap;

                // Keep only recent window
                while (_recentConsumption.Count > RECENT_WINDOW)
                    _recentConsumption.RemoveAt(0);
            }
            _lastLapSeen = fuel.CurrentLap;
        }
    }

    private float GetRecentAverage()
    {
        return _recentConsumption.Count > 0
            ? _recentConsumption.Average()
            : 0;
    }

    private static int GetEffectiveRaceLaps(FuelData fuel)
    {
        if (fuel.RaceLapsRemaining > 0)
            return fuel.RaceLapsRemaining;

        // Timed session: estimate from time + avg lap time
        if (fuel.IsTimedSession && fuel.AverageLapTime > 10f)
            return (int)Math.Ceiling(fuel.SessionTimeRemaining / fuel.AverageLapTime);

        return 0;
    }

    private void CalculateStrategyComparison(
        FuelData fuel, int raceLaps, float avg, float avgLapTime, float savingNeeded)
    {
        // Pit stop time loss
        float pitTime = fuel.EstimatedPitStopTime > 0
            ? fuel.EstimatedPitStopTime
            : DEFAULT_PIT_TIME;
        fuel.PitStopTimeLoss = pitTime;

        if (avgLapTime <= 10f || avg <= 0)
        {
            fuel.IsPittingFaster = true;
            fuel.StrategyTimeDelta = 0;
            return;
        }

        // Fuel saving time loss over remaining laps
        float savingFraction = Math.Clamp(savingNeeded / avg, 0, MAX_SAVING_FRACTION);
        float timePenaltyPerLap = avgLapTime * savingFraction * FUEL_SAVE_TIME_PENALTY_FACTOR;
        float totalSavingTimeLoss = timePenaltyPerLap * raceLaps;
        fuel.FuelSavingTimeLoss = totalSavingTimeLoss;

        // Compare strategies
        fuel.IsPittingFaster = pitTime < totalSavingTimeLoss;
        fuel.StrategyTimeDelta = totalSavingTimeLoss - pitTime; // positive = saving is slower

        // If saving impossible, pitting is the only option
        if (!fuel.CanSaveFuelToFinish)
        {
            fuel.IsPittingFaster = true;
            fuel.FuelSavingTimeLoss = float.MaxValue;
        }
    }

    private static void CalculateLiftPoints(FuelData fuel, float savingNeeded, float avg)
    {
        // Suggest lift intensity based on saving fraction
        if (avg <= 0) return;

        float savingPct = (savingNeeded / avg) * 100f;

        fuel.LiftPoints = savingPct switch
        {
            < 5f  => "Light lift on longest straight",
            < 10f => "Lift before 2 braking zones",
            < 15f => "Lift before 3 braking zones",
            < 20f => "Lift before all major braking zones",
            < 30f => "Heavy lift & coast all corners",
            _     => "Maximum conservation — consider pitting"
        };
    }

    private static void GenerateAlerts(FuelData fuel, int raceLaps)
    {
        if (!fuel.NeedsFuelSaving)
        {
            fuel.AlertSeverity = 0;
            return;
        }

        if (!fuel.CanSaveFuelToFinish)
        {
            fuel.StrategicAlert = "FUEL SAVING NOT POSSIBLE — PIT REQUIRED";
            fuel.AlertSeverity = 3; // Critical
            return;
        }

        if (fuel.FuelSavingWorking)
        {
            fuel.StrategicAlert = $"FUEL SAVING WORKING — {fuel.SavingProgress:F0}%";
            fuel.AlertSeverity = 1; // Info
            return;
        }

        // Need more saving
        float deficit = fuel.FuelSavingTarget - fuel.CurrentSavingRate;
        if (deficit > 0 && fuel.AvgFuelPerLap > 0)
        {
            float pctMore = (deficit / fuel.AvgFuelPerLap) * 100f;
            fuel.StrategicAlert = $"SAVE MORE — need {pctMore:F1}% more lift";
            fuel.AlertSeverity = 2; // Warning
        }
        else
        {
            fuel.StrategicAlert = "FUEL SAVING NEEDED";
            fuel.AlertSeverity = 2; // Warning
        }

        // Override: if pitting is clearly faster and laps are running out
        if (fuel.IsPittingFaster && raceLaps <= 10)
        {
            fuel.StrategicAlert = $"PIT RECOMMENDED — saves {fuel.StrategyTimeDelta:F1}s vs saving";
            fuel.AlertSeverity = 3;
        }
    }

    private static void ClearSavingFields(FuelData fuel)
    {
        fuel.FuelSavingTarget = 0;
        fuel.CurrentSavingRate = 0;
        fuel.TargetLapTime = 0;
        fuel.LiftPoints = null;
        fuel.SavingProgress = 0;
        fuel.FuelSavingWorking = false;
        fuel.ProjectedFuelDelta = 0;
        fuel.PitStopTimeLoss = 0;
        fuel.FuelSavingTimeLoss = 0;
        fuel.IsPittingFaster = false;
        fuel.StrategyTimeDelta = 0;
        fuel.CanSaveFuelToFinish = true;
        fuel.StrategicAlert = null;
        fuel.AlertSeverity = 0;
    }
}
