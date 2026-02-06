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
    private float _fuelAtLapStart;
    private int _lastLap = -1;
    private bool _initialized;
    private float _previousFuel;
    private float _minFuel = float.MaxValue;
    private float _maxFuel;

    /// <summary>Current fuel calculation snapshot exposed to consumers.</summary>
    public FuelData CurrentData { get; } = new();

    /// <summary>
    /// Called every telemetry tick (~60 Hz). Detects lap changes and recalculates.
    /// </summary>
    public void Update(TelemetryData data)
    {
        var fuel = data.FuelLevel;
        var lap = data.Lap;

        // ── bootstrap on first tick ──────────────────────────────────────
        if (!_initialized)
        {
            _fuelAtLapStart = fuel;
            _previousFuel = fuel;
            _lastLap = lap;
            _initialized = true;
        }

        // ── pit stop detection (fuel increased) ─────────────────────────
        if (fuel > _previousFuel + 0.5f)
        {
            _fuelAtLapStart = fuel;
            CurrentData.RefuelCount++;
            CurrentData.LastRefuelAmount = fuel - _previousFuel;
            CurrentData.StintLapCount = 0;
        }

        // ── lap change ──────────────────────────────────────────────────
        if (lap != _lastLap && lap > _lastLap)
        {
            float used = _fuelAtLapStart - fuel;

            if (used > 0.01f && used < 50f) // sanity bounds
            {
                _lapFuelUsage.Add(used);

                if (used < _minFuel) _minFuel = used;
                if (used > _maxFuel) _maxFuel = used;

                CurrentData.FuelUsedLastLap = used;
                CurrentData.LapsCompleted = _lapFuelUsage.Count;

                // Lap-to-lap delta
                if (_lapFuelUsage.Count >= 2)
                    CurrentData.LapToLapDelta = used - _lapFuelUsage[^2];
            }

            _fuelAtLapStart = fuel;
            _lastLap = lap;
            CurrentData.StintLapCount++;
        }

        // ── live mid-lap consumption estimate ───────────────────────────
        CurrentData.FuelUsedThisLap = Math.Max(0, _fuelAtLapStart - fuel);
        CurrentData.CurrentLapFuelRate = data.LapDistPct > 0.05f
            ? CurrentData.FuelUsedThisLap / data.LapDistPct
            : 0f;

        _previousFuel = fuel;

        // ── populate snapshot ───────────────────────────────────────────
        CurrentData.CurrentFuel = fuel;
        CurrentData.FuelPct = data.FuelLevelPct;
        CurrentData.TankCapacity = data.FuelLevelMax;
        CurrentData.FuelPressure = data.FuelPress;
        CurrentData.CurrentLap = lap;
        CurrentData.SessionState = data.SessionState;
        CurrentData.LastUpdate = DateTime.UtcNow;

        // Averages
        CurrentData.AvgFuelPerLap_Last = _lapFuelUsage.Count > 0 ? _lapFuelUsage[^1] : 0;
        CurrentData.AvgFuelPerLap_L5 = WindowAverage(5);
        CurrentData.AvgFuelPerLap_L10 = WindowAverage(10);
        CurrentData.AvgFuelPerLap_Session = _lapFuelUsage.Count > 0
            ? _lapFuelUsage.Average()
            : 0;

        // Stint average
        int stintCount = Math.Min(CurrentData.StintLapCount, _lapFuelUsage.Count);
        CurrentData.AvgFuelPerLap_Stint = stintCount > 0
            ? _lapFuelUsage.TakeLast(stintCount).Average()
            : 0;

        CurrentData.MinFuelPerLap = _minFuel < float.MaxValue ? _minFuel : 0;
        CurrentData.MaxFuelPerLap = _maxFuel;

        // HasSufficientData — need at least 2 valid laps
        CurrentData.HasSufficientData = _lapFuelUsage.Count >= 2;

        // ── laps remaining ──────────────────────────────────────────────
        float avgForCalc = CurrentData.AvgFuelPerLap_L5 > 0
            ? CurrentData.AvgFuelPerLap_L5
            : CurrentData.AvgFuelPerLap_Session;

        CurrentData.LapsRemaining = avgForCalc > 0
            ? fuel / avgForCalc
            : 0;

        // iRacing's estimate (from SDK SessionLapsRemain)
        CurrentData.IRacingLapsRemaining = data.SessionLapsRemain;
        CurrentData.LapsDifference = CurrentData.LapsRemaining - CurrentData.IRacingLapsRemaining;

        // ── race strategy basics ────────────────────────────────────────
        CurrentData.RaceLapsRemaining = data.SessionLapsRemain;
        CurrentData.SessionTimeRemaining = data.SessionTimeRemain;
        CurrentData.IsTimedSession = data.SessionLapsTotal <= 0 && data.SessionTimeRemain > 0;

        if (avgForCalc > 0 && CurrentData.RaceLapsRemaining > 0)
        {
            float buffer = CurrentData.FuelBufferLaps;
            CurrentData.FuelNeededToFinish = (CurrentData.RaceLapsRemaining + buffer) * avgForCalc;
            CurrentData.FuelDeltaToFinish = fuel - CurrentData.FuelNeededToFinish;
            CurrentData.CanFinishWithoutStop = CurrentData.FuelDeltaToFinish >= 0;
            CurrentData.FuelToAddAtPit = Math.Max(0, CurrentData.FuelNeededToFinish - fuel);
        }

        // Total fuel used
        CurrentData.TotalFuelUsed = _lapFuelUsage.Sum();
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private float WindowAverage(int window)
    {
        if (_lapFuelUsage.Count == 0) return 0;
        int count = Math.Min(window, _lapFuelUsage.Count);
        return _lapFuelUsage.TakeLast(count).Average();
    }
}
