using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Simplified fuel calculator — tracks per-lap consumption and derives averages.
/// Replaces the original 1,400-line + 17 sub-service implementation with the
/// minimum set of calculations needed for MRT One's fuel display.
/// </summary>
public sealed class FuelCalculatorService
{
    private readonly List<float> _lapFuelUsage = new();
    private readonly List<float> _greenLapFuelUsage = new();
    private readonly List<float> _yellowLapFuelUsage = new();
    private readonly List<float> _lapTimes = new();
    private float _fuelAtLapStart;
    private int _lastLap = -1;
    private bool _initialized;
    private float _previousFuel;
    private float _minFuel = float.MaxValue;
    private float _maxFuel;
    private bool _wasUnderYellow;
    private bool _isPitLap; // true when a pit stop was detected this lap — skip from averages
    private float _sdkFuelEstimate; // SDK FuelUsePerHour converted to L/lap for early-lap fallback
    private bool _isFirstRaceLap = true; // first completed lap is always partial (standing/rolling start)
    private int _stintStartIndex; // index in _lapFuelUsage where current stint started (for clean stint avg)

    // Session change detection
    private int _lastSessionNum = -1;
    private string _lastTrackName = string.Empty;
    private string _lastCarScreenName = string.Empty;

    /// <summary>Current fuel calculation snapshot exposed to consumers.</summary>
    public FuelData CurrentData { get; } = new();

    /// <summary>
    /// Reset all fuel calculation state. Called when session/car/track changes.
    /// </summary>
    public void Reset()
    {
        _lapFuelUsage.Clear();
        _greenLapFuelUsage.Clear();
        _yellowLapFuelUsage.Clear();
        _lapTimes.Clear();
        _fuelAtLapStart = 0f;
        _lastLap = -1;
        _initialized = false;
        _previousFuel = 0f;
        _minFuel = float.MaxValue;
        _maxFuel = 0f;
        _wasUnderYellow = false;
        _isPitLap = false;
        _sdkFuelEstimate = 0f;
        _isFirstRaceLap = true;
        _stintStartIndex = 0;

        // Reset snapshot (keep defaults)
        var data = CurrentData;
        data.LapsCompleted = 0;
        data.FuelUsedLastLap = 0;
        data.FuelUsedThisLap = 0;
        data.CurrentLapFuelRate = 0;
        data.StartingFuel = 0;
        data.RefuelCount = 0;
        data.LastRefuelAmount = 0;
        data.StintLapCount = 0;
        data.LapToLapDelta = 0;
        data.GreenFlagLapCount = 0;
        data.YellowFlagLapCount = 0;
    }

    /// <summary>
    /// Called every telemetry tick (~60 Hz). Detects lap changes and recalculates.
    /// </summary>
    public void Update(TelemetryData data)
    {
        // ── Session/car/track change detection (reset calculator) ────
        bool sessionChanged = data.SessionNum != _lastSessionNum && _lastSessionNum >= 0;
        bool trackChanged = !string.IsNullOrEmpty(data.TrackName)
                         && !string.IsNullOrEmpty(_lastTrackName)
                         && data.TrackName != _lastTrackName;
        bool carChanged = !string.IsNullOrEmpty(data.CarScreenName)
                       && !string.IsNullOrEmpty(_lastCarScreenName)
                       && data.CarScreenName != _lastCarScreenName;

        if (sessionChanged || trackChanged || carChanged)
        {
            Reset();
        }

        _lastSessionNum = data.SessionNum;
        if (!string.IsNullOrEmpty(data.TrackName)) _lastTrackName = data.TrackName;
        if (!string.IsNullOrEmpty(data.CarScreenName)) _lastCarScreenName = data.CarScreenName;

        var fuel = data.FuelLevel;
        var lap = data.Lap;

        // ── detect yellow flag (caution) from session flags ──────────
        bool isUnderYellow = (data.SessionFlags & 0x00004000) != 0 // Caution
                          || (data.SessionFlags & 0x00000008) != 0; // Yellow

        // ── bootstrap on first tick ──────────────────────────────────
        if (!_initialized)
        {
            _fuelAtLapStart = fuel;
            _previousFuel = fuel;
            _lastLap = lap;
            _initialized = true;
            CurrentData.StartingFuel = fuel;
        }

        // ── pit stop detection (fuel increased beyond noise) ─────────
        if (fuel > _previousFuel + 0.5f)
        {
            _fuelAtLapStart = fuel;
            CurrentData.RefuelCount++;
            CurrentData.LastRefuelAmount = fuel - _previousFuel;
            CurrentData.StintLapCount = 0;
            _isPitLap = true; // flag: this lap's consumption is contaminated
            _stintStartIndex = _lapFuelUsage.Count; // stint avg starts after this point
        }

        // ── lap change ──────────────────────────────────────────────
        if (lap != _lastLap && lap > _lastLap)
        {
            float used = _fuelAtLapStart - fuel;

            // Always clear the first-race-lap flag on the FIRST lap transition,
            // even if fuel data fails sanity (e.g., pace lap with minimal fuel usage).
            // BUG FIX: Previously this was inside the sanity check, so if the first
            // lap had used ≤ 0.01 (formation/pace lap), the flag stayed true forever
            // and ALL subsequent laps were skipped as "first lap".
            bool skipFirstLap = _isFirstRaceLap;
            if (_isFirstRaceLap)
                _isFirstRaceLap = false;

            if (used > 0.01f && used < 50f) // sanity bounds
            {
                // Skip pit laps from averages — consumption is contaminated
                // (partial lap before/after pit distorts the numbers)
                if (!_isPitLap && !skipFirstLap)
                {
                    CurrentData.FuelUsedLastLap = used;
                    _lapFuelUsage.Add(used);

                    if (used < _minFuel) _minFuel = used;
                    if (used > _maxFuel) _maxFuel = used;

                    CurrentData.LapsCompleted = _lapFuelUsage.Count;

                    // Track green vs yellow separately
                    if (_wasUnderYellow)
                    {
                        _yellowLapFuelUsage.Add(used);
                        CurrentData.YellowFlagLapCount = _yellowLapFuelUsage.Count;
                    }
                    else
                    {
                        _greenLapFuelUsage.Add(used);
                        CurrentData.GreenFlagLapCount = _greenLapFuelUsage.Count;
                    }

                    // Lap-to-lap delta
                    if (_lapFuelUsage.Count >= 2)
                        CurrentData.LapToLapDelta = used - _lapFuelUsage[^2];
                }

                // Always track lap times (even pit laps are valid for time estimation)
                if (data.LapLastLapTime > 0 && data.LapLastLapTime < 600)
                    _lapTimes.Add(data.LapLastLapTime);
            }

            _fuelAtLapStart = fuel;
            _lastLap = lap;
            _isPitLap = false; // reset pit flag for next lap
            CurrentData.StintLapCount++;
        }

        // Remember yellow state for the lap we just completed
        _wasUnderYellow = isUnderYellow;

        // ── live mid-lap consumption estimate ───────────────────────
        CurrentData.FuelUsedThisLap = Math.Max(0, _fuelAtLapStart - fuel);
        CurrentData.CurrentLapFuelRate = data.LapDistPct > 0.05f
            ? CurrentData.FuelUsedThisLap / data.LapDistPct
            : 0f;

        _previousFuel = fuel;

        // ── populate snapshot ───────────────────────────────────────
        CurrentData.CurrentFuel = fuel;
        CurrentData.FuelPct = data.FuelLevelPct;
        CurrentData.TankCapacity = data.FuelLevelMax;
        CurrentData.FuelPressure = data.FuelPress;
        CurrentData.CurrentLap = lap;
        CurrentData.SessionState = data.SessionState;
        CurrentData.IsUnderYellow = isUnderYellow;
        CurrentData.LastUpdate = DateTime.UtcNow;

        // ── SDK-based fuel estimate (for early-lap fallback) ────────
        // iRacing provides FuelUsePerHour (kg/hr). Convert to L/lap estimate.
        // Use best lap time, last lap time, average, or projected from current lap.
        float sdkLapTime = data.LapBestLapTime > 1.0f ? data.LapBestLapTime
            : data.LapLastLapTime > 1.0f ? data.LapLastLapTime
            : CurrentData.AverageLapTime > 10f ? CurrentData.AverageLapTime
            : 0f;
        // Fallback for lap 1: project from current lap progress (need >15% of lap for reasonable estimate)
        if (sdkLapTime <= 1.0f && data.LapDistPct > 0.15f && data.LapCurrentLapTime > 5.0f)
        {
            sdkLapTime = (float)(data.LapCurrentLapTime / data.LapDistPct);
        }
        if (data.FuelUsePerHour > 0 && sdkLapTime > 1.0f)
        {
            _sdkFuelEstimate = (data.FuelUsePerHour / 3600f) * sdkLapTime;
            CurrentData.SdkFuelEstimate = _sdkFuelEstimate;
        }

        // ── averages ────────────────────────────────────────────────
        CurrentData.AvgFuelPerLap_Last = _lapFuelUsage.Count > 0 ? _lapFuelUsage[^1] : 0;
        CurrentData.AvgFuelPerLap_L3 = WindowAverage(_lapFuelUsage, 3);
        CurrentData.AvgFuelPerLap_L5 = WindowAverage(_lapFuelUsage, 5);
        CurrentData.AvgFuelPerLap_L10 = WindowAverage(_lapFuelUsage, 10);
        CurrentData.AvgFuelPerLap_Session = _lapFuelUsage.Count > 0
            ? _lapFuelUsage.Average()
            : 0;

        // Green/yellow averages
        CurrentData.GreenFlagAverage = _greenLapFuelUsage.Count > 0
            ? _greenLapFuelUsage.Average()
            : 0;
        CurrentData.YellowFlagAverage = _yellowLapFuelUsage.Count > 0
            ? _yellowLapFuelUsage.Average()
            : 0;

        // Stint average — only entries added AFTER the last pit stop
        int stintEntries = _lapFuelUsage.Count - _stintStartIndex;
        CurrentData.AvgFuelPerLap_Stint = stintEntries > 0
            ? _lapFuelUsage.Skip(_stintStartIndex).Average()
            : 0;

        CurrentData.MinFuelPerLap = _minFuel < float.MaxValue ? _minFuel : 0;
        CurrentData.MaxFuelPerLap = _maxFuel;

        // HasSufficientData — need at least 2 valid laps (or SDK estimate)
        CurrentData.HasSufficientData = _lapFuelUsage.Count >= 2 || _sdkFuelEstimate > 0;

        // Average lap time for timed session calculations
        CurrentData.AverageLapTime = _lapTimes.Count > 0
            ? _lapTimes.TakeLast(5).Average()
            : 0;

        // ── laps remaining (accounts for splutter/buffer zone) ──────────
        // Early-lap blending: the first race lap is excluded (partial distance from
        // standing/rolling start). SDK estimate fills in until real data arrives.
        // Once we have clean laps, we trust measured data quickly since there's no
        // garbage first-lap polluting the averages.
        float avgForCalc = CurrentData.AvgFuelPerLap_L5 > 0
            ? CurrentData.AvgFuelPerLap_L5
            : CurrentData.AvgFuelPerLap_Session;

        if (_sdkFuelEstimate > 0)
        {
            if (_lapFuelUsage.Count == 0)
            {
                // No measured data yet (pre-race or first lap still running) — pure SDK
                avgForCalc = _sdkFuelEstimate;
            }
            else if (_lapFuelUsage.Count == 1)
            {
                // 1 clean lap: 60% measured, 40% SDK (first-lap is already excluded,
                // so this is a full-distance lap — more trustworthy)
                avgForCalc = _lapFuelUsage[0] * 0.60f + _sdkFuelEstimate * 0.40f;
            }
            else if (_lapFuelUsage.Count == 2)
            {
                // 2 clean laps: 85% measured, 15% SDK
                float measured = _lapFuelUsage.Average();
                avgForCalc = measured * 0.85f + _sdkFuelEstimate * 0.15f;
            }
            // 3+ laps: pure measured data (avgForCalc already set above)
        }

        // Usable fuel = current fuel minus splutter threshold (unusable fuel at bottom of tank)
        float usableFuel = Math.Max(0, fuel - CurrentData.FuelSputteringThreshold);

        // Stabilize LapsRemaining near S/F line to prevent "countdown" effect.
        // The issue: fuel drops continuously but avgForCalc only updates at lap boundaries.
        // Near line crossing, FuelUsedThisLap is at maximum → LapsRemaining dips, then
        // snaps back after crossing when fuelAtLapStart resets.
        // Fix: blend in the mid-lap projected rate so the estimate transitions smoothly.
        float displayAvg = avgForCalc;
        if (_lapFuelUsage.Count >= 1 && CurrentData.CurrentLapFuelRate > 0 && data.LapDistPct > 0.30f)
        {
            // Weight mid-lap projection more as we approach S/F line
            float progressWeight = Math.Clamp((data.LapDistPct - 0.30f) / 0.70f, 0f, 0.5f);
            displayAvg = avgForCalc * (1f - progressWeight) + CurrentData.CurrentLapFuelRate * progressWeight;
        }

        CurrentData.LapsRemaining = displayAvg > 0
            ? usableFuel / displayAvg
            : 0;

        // ── race strategy ───────────────────────────────────────────
        // iRacing returns huge values (32767) for SessionLapsRemain in practice/qual/timed.
        // Cap at 500 laps — anything beyond is clearly not a real lap-limited race.
        const int MAX_SANE_LAPS = 500;
        int rawLapsRemain = data.SessionLapsRemain;
        bool hasValidLapCount = rawLapsRemain > 0 && rawLapsRemain <= MAX_SANE_LAPS;

        // iRacing's estimate — use the same capping to avoid nonsensical 32767-based differences
        CurrentData.IRacingLapsRemaining = hasValidLapCount ? rawLapsRemain : 0;
        CurrentData.LapsDifference = CurrentData.IRacingLapsRemaining > 0
            ? CurrentData.LapsRemaining - CurrentData.IRacingLapsRemaining
            : 0;

        CurrentData.RaceLapsRemaining = hasValidLapCount ? rawLapsRemain : 0;
        CurrentData.SessionTimeRemaining = data.SessionTimeRemain;
        CurrentData.IsTimedSession = !hasValidLapCount && data.SessionTimeRemain > 0;

        // For timed sessions, estimate laps remaining from time + avg lap time.
        // In timed races, the LEADER determines when the white flag comes out.
        // The player still needs to finish their own remaining laps.
        // We estimate based on player's pace (they still have to drive those laps).
        int effectiveRaceLaps = CurrentData.RaceLapsRemaining;
        if (CurrentData.IsTimedSession && CurrentData.AverageLapTime > 10f)
        {
            float estimatedLaps = (float)(CurrentData.SessionTimeRemaining / CurrentData.AverageLapTime);
            // Add 1 extra lap: after time expires the leader crosses S/F, then all cars
            // complete their current lap. Conservative: player might need 1 more lap than time says.
            estimatedLaps += 1.0f;
            CurrentData.EstimatedLapsFromTime = estimatedLaps;
            effectiveRaceLaps = (int)Math.Ceiling(estimatedLaps);
        }

        // ── estimated total race laps ───────────────────────────────
        // For lap-based: SessionLapsTotal from SDK.
        // For timed: use leader's lap + estimated remaining (leader's pace controls race end).
        int sessionLapsTotal = data.SessionLapsTotal;
        bool hasValidTotal = sessionLapsTotal > 0 && sessionLapsTotal <= MAX_SANE_LAPS;
        if (hasValidTotal)
        {
            CurrentData.EstimatedTotalRaceLaps = sessionLapsTotal;
        }
        else if (CurrentData.IsTimedSession && CurrentData.AverageLapTime > 10f)
        {
            // Timed session: leader's current lap is the basis for total estimation.
            // The leader controls when the race ends; use leader lap if available.
            int baseLap = data.RaceLeaderLapNumber > 0 ? data.RaceLeaderLapNumber : lap;
            CurrentData.EstimatedTotalRaceLaps = baseLap + effectiveRaceLaps;
        }
        else if (CurrentData.IsTimedSession && sdkLapTime > 1.0f && data.SessionTimeTotal > 0)
        {
            // Very early in timed session: use session total time / best SDK lap estimate
            CurrentData.EstimatedTotalRaceLaps = (int)Math.Ceiling(data.SessionTimeTotal / sdkLapTime);
        }
        else
        {
            CurrentData.EstimatedTotalRaceLaps = 0; // unknown
        }

        // Expose the blended avg for downstream services (FuelSavingService, widget display)
        CurrentData.EffectiveAvgFuelPerLap = avgForCalc;

        if (avgForCalc > 0 && effectiveRaceLaps > 0)
        {
            float buffer = CurrentData.FuelBufferLaps;
            CurrentData.FuelNeededToFinish = (effectiveRaceLaps + buffer) * avgForCalc + CurrentData.FuelSputteringThreshold;
            CurrentData.FuelDeltaToFinish = fuel - CurrentData.FuelNeededToFinish;
            CurrentData.CanFinishWithoutStop = CurrentData.FuelDeltaToFinish >= 0;
            CurrentData.FuelToAddAtPit = Math.Max(0, CurrentData.FuelNeededToFinish - fuel);
        }
        else
        {
            // No valid data — reset strategy fields to prevent stale values
            CurrentData.FuelNeededToFinish = 0;
            CurrentData.FuelDeltaToFinish = 0;
            CurrentData.CanFinishWithoutStop = false;
            CurrentData.FuelToAddAtPit = 0;
        }

        // Total fuel used
        CurrentData.TotalFuelUsed = _lapFuelUsage.Sum();

        // Fuel consistency variance (standard deviation)
        if (_lapFuelUsage.Count >= 3)
        {
            float mean = _lapFuelUsage.Average();
            float sumSqDiff = _lapFuelUsage.Sum(v => (v - mean) * (v - mean));
            CurrentData.FuelConsistencyVariance = (float)Math.Sqrt(sumSqDiff / _lapFuelUsage.Count);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static float WindowAverage(List<float> list, int window)
    {
        if (list.Count == 0) return 0;
        int count = Math.Min(window, list.Count);
        return list.TakeLast(count).Average();
    }
}
