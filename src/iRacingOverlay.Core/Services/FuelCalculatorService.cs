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
        }

        // ── lap change ──────────────────────────────────────────────
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
                
                // Track lap times for timed session estimation
                if (data.LapLastLapTime > 0 && data.LapLastLapTime < 600)
                    _lapTimes.Add(data.LapLastLapTime);
            }

            _fuelAtLapStart = fuel;
            _lastLap = lap;
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

        // Stint average
        int stintCount = Math.Min(CurrentData.StintLapCount, _lapFuelUsage.Count);
        CurrentData.AvgFuelPerLap_Stint = stintCount > 0
            ? _lapFuelUsage.TakeLast(stintCount).Average()
            : 0;

        CurrentData.MinFuelPerLap = _minFuel < float.MaxValue ? _minFuel : 0;
        CurrentData.MaxFuelPerLap = _maxFuel;

        // HasSufficientData — need at least 2 valid laps
        CurrentData.HasSufficientData = _lapFuelUsage.Count >= 2;

        // Average lap time for timed session calculations
        CurrentData.AverageLapTime = _lapTimes.Count > 0
            ? _lapTimes.TakeLast(5).Average()
            : 0;

        // ── laps remaining (accounts for splutter/buffer zone) ──────────
        float avgForCalc = CurrentData.AvgFuelPerLap_L5 > 0
            ? CurrentData.AvgFuelPerLap_L5
            : CurrentData.AvgFuelPerLap_Session;

        // Usable fuel = current fuel minus splutter threshold (unusable fuel at bottom of tank)
        float usableFuel = Math.Max(0, fuel - CurrentData.FuelSputteringThreshold);

        CurrentData.LapsRemaining = avgForCalc > 0
            ? usableFuel / avgForCalc
            : 0;

        // iRacing's estimate (from SDK SessionLapsRemain)
        CurrentData.IRacingLapsRemaining = data.SessionLapsRemain;
        CurrentData.LapsDifference = CurrentData.LapsRemaining - CurrentData.IRacingLapsRemaining;

        // ── race strategy ───────────────────────────────────────────
        // iRacing returns huge values (32767) for SessionLapsRemain in practice/qual/timed.
        // Cap at 500 laps — anything beyond is clearly not a real lap-limited race.
        const int MAX_SANE_LAPS = 500;
        int rawLapsRemain = data.SessionLapsRemain;
        bool hasValidLapCount = rawLapsRemain > 0 && rawLapsRemain <= MAX_SANE_LAPS;

        CurrentData.RaceLapsRemaining = hasValidLapCount ? rawLapsRemain : 0;
        CurrentData.SessionTimeRemaining = data.SessionTimeRemain;
        CurrentData.IsTimedSession = !hasValidLapCount && data.SessionTimeRemain > 0;

        // For timed sessions, estimate laps remaining from time + avg lap time
        int effectiveRaceLaps = CurrentData.RaceLapsRemaining;
        if (CurrentData.IsTimedSession && CurrentData.AverageLapTime > 10f)
        {
            float estimatedLaps = (float)(CurrentData.SessionTimeRemaining / CurrentData.AverageLapTime);
            CurrentData.EstimatedLapsFromTime = estimatedLaps;
            effectiveRaceLaps = (int)Math.Ceiling(estimatedLaps);
        }

        if (avgForCalc > 0 && effectiveRaceLaps > 0)
        {
            float buffer = CurrentData.FuelBufferLaps;
            CurrentData.FuelNeededToFinish = (effectiveRaceLaps + buffer) * avgForCalc + CurrentData.FuelSputteringThreshold;
            CurrentData.FuelDeltaToFinish = fuel - CurrentData.FuelNeededToFinish;
            CurrentData.CanFinishWithoutStop = CurrentData.FuelDeltaToFinish >= 0;
            CurrentData.FuelToAddAtPit = Math.Max(0, CurrentData.FuelNeededToFinish - fuel);
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
