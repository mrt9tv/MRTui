using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Calculates the relative proximity table from telemetry data.
/// Sorts all on-track cars by their distance to the player,
/// computes time intervals, and returns a display-ready list.
/// 
/// Thread-safety: NOT thread-safe. Call from a single thread (UI dispatch).
/// Performance: Reuses internal lists to minimize GC pressure at 60Hz.
/// </summary>
public class RelativeCalculator
{
    /// <summary>Maximum number of cars the iRacing SDK supports</summary>
    private const int MAX_CARS = 64;

    /// <summary>Default lap time estimate when no real data available (90s)</summary>
    private const float DEFAULT_LAP_TIME = 90.0f;

    /// <summary>Minimum valid LapDistPct (below this = not on track)</summary>
    private const float MIN_VALID_LAP_DIST = 0.001f;

    /// <summary>iRacing TrackSurface = NotInWorld sentinel</summary>
    private const int TRACK_SURFACE_NOT_IN_WORLD = -1;

    /// <summary>iRacing TrackSurface = OffTrack value</summary>
    private const int TRACK_SURFACE_OFF_TRACK = 0;

    /// <summary>iRacing TrackSurface = InPitStall (at pit box)</summary>
    private const int TRACK_SURFACE_IN_PIT_STALL = 1;

    /// <summary>iRacing TrackSurface = ApproachingPits (entering/exiting pit lane)</summary>
    private const int TRACK_SURFACE_APPROACHING_PITS = 2;

    /// <summary>iRacing TrackSurface = OnTrack</summary>
    private const int TRACK_SURFACE_ON_TRACK = 3;

    // ── Reusable scratch buffers (avoid allocations on the hot path) ────

    private readonly List<RelativeEntry> _result = new(MAX_CARS);
    private readonly List<RelativeEntry> _ahead = new(MAX_CARS / 2);
    private readonly List<RelativeEntry> _behind = new(MAX_CARS / 2);

    /// <summary>Cached reference lap time for distance → time conversion</summary>
    private float _referenceLapTime = DEFAULT_LAP_TIME;

    /// <summary>Track off-track duration per car index (seconds). Reset when on-track.</summary>
    private readonly float[] _offTrackDuration = new float[MAX_CARS];

    /// <summary>Track whether each car has visited pit stall this pit stop (for exit detection).</summary>
    private readonly bool[] _wasInPitStall = new bool[MAX_CARS];

    /// <summary>Track pit stall entry time for BOX timer.</summary>
    private readonly DateTime?[] _pitStallEntryTime = new DateTime?[MAX_CARS];

    /// <summary>Track whether each car was serviced in the pit (for OUTLAP detection).</summary>
    private readonly bool[] _wasServiced = new bool[MAX_CARS];

    /// <summary>Track whether each car is on an out-lap after pit exit.</summary>
    private readonly bool[] _isOnOutLap = new bool[MAX_CARS];

    /// <summary>Track LapDistPct when each car exited the pit (for OUTLAP 85% tracking).</summary>
    private readonly float[] _pitExitLapDistPct = new float[MAX_CARS];

    /// <summary>Final BOX duration when driver left pit stall (for blink on exit).</summary>
    private readonly float[] _finalBoxDuration = new float[MAX_CARS];

    /// <summary>UTC time when driver started exiting pit (for 3-second blink).</summary>
    private readonly DateTime?[] _exitingPitStartTime = new DateTime?[MAX_CARS];

    /// <summary>iRacing flag bit: Meatball / Repair required</summary>
    private const int FLAG_REPAIR = 0x100000;

    /// <summary>iRacing flag bit: Black flag</summary>
    private const int FLAG_BLACK = 0x10000;

    /// <summary>Track last non-NotInWorld surface per car (for tow detection).</summary>
    private readonly int[] _lastTrackSurface = new int[MAX_CARS];

    /// <summary>Track whether each car was towed (skipped pit road approach).</summary>
    private readonly bool[] _wasTowed = new bool[MAX_CARS];

    // ── New: pit stop counting ──────────────────────────────────────
    private readonly int[] _pitStopCount = new int[MAX_CARS];

    // ── New: interval history for closing rate (per-lap snapshots) ──
    private const int HISTORY_SIZE = 5;
    private readonly float[][] _intervalHistory;
    private readonly int[][] _positionHistory;
    private readonly int[] _historyIdx = new int[MAX_CARS];
    private readonly int[] _lastKnownLap = new int[MAX_CARS];
    private readonly float[] _prevInterval = new float[MAX_CARS]; // previous-frame interval for trend

    /// <summary>Timestamp of last Calculate call (for delta timing)</summary>
    private DateTime _lastCalcTime = DateTime.UtcNow;

    public RelativeCalculator()
    {
        // Initialize per-car circular history buffers
        _intervalHistory = new float[MAX_CARS][];
        _positionHistory = new int[MAX_CARS][];
        for (int i = 0; i < MAX_CARS; i++)
        {
            _intervalHistory[i] = new float[HISTORY_SIZE];
            _positionHistory[i] = new int[HISTORY_SIZE];
        }
    }

    /// <summary>
    /// Calculate the relative display entries from current telemetry.
    /// </summary>
    /// <param name="data">Current telemetry snapshot</param>
    /// <param name="maxAhead">Max cars to show ahead of player</param>
    /// <param name="maxBehind">Max cars to show behind player</param>
    /// <returns>
    /// Ordered list: farthest-ahead → closest-ahead → PLAYER → closest-behind → farthest-behind.
    /// The returned list reference is reused — copy it if you need to hold it across calls.
    /// </returns>
    public List<RelativeEntry> Calculate(TelemetryData data, int maxAhead = 3, int maxBehind = 3)
    {
        _result.Clear();
        _ahead.Clear();
        _behind.Clear();

        // Guard: we need at minimum the track position array
        if (data.CarIdxLapDistPct == null)
            return _result;

        // Delta timing for off-track duration tracking
        var now = DateTime.UtcNow;
        float deltaTime = (float)(now - _lastCalcTime).TotalSeconds;
        _lastCalcTime = now;
        // Clamp to avoid huge jumps (app resume, breakpoints)
        if (deltaTime > 1.0f) deltaTime = 0.016f;

        int playerIdx = data.PlayerCarIdx;
        float playerPct = data.LapDistPct;
        int playerLap = data.Lap;

        UpdateReferenceLapTime(data);

        // ── Find session best lap time for purple highlighting ─────
        float sessionBestLapTime = float.MaxValue;
        int carCount = Math.Min(data.CarIdxLapDistPct.Length, MAX_CARS);
        if (data.CarIdxBestLapTime != null)
        {
            for (int i = 0; i < Math.Min(data.CarIdxBestLapTime.Length, MAX_CARS); i++)
            {
                float best = data.CarIdxBestLapTime[i];
                if (best > 1.0f && best < sessionBestLapTime)
                    sessionBestLapTime = best;
            }
        }

        RelativeEntry? playerEntry = null;

        for (int i = 0; i < carCount; i++)
        {
            float pct = data.CarIdxLapDistPct[i];

            // Skip cars with invalid track position
            if (pct < MIN_VALID_LAP_DIST || pct > 1.0f)
                continue;

            // Skip cars that are not in the world
            int surface = ArrayInt(data.CarIdxTrackSurface, i, -1);
            if (surface == TRACK_SURFACE_NOT_IN_WORLD)
                continue;

            // Detect pace/safety car
            bool isSafetyCar = (i == data.PaceCarIdx && data.PaceCarIdx >= 0);

            // Safety car: only show when caution is active and car is on track (not in pits)
            if (isSafetyCar)
            {
                bool isOnTrack = surface == TRACK_SURFACE_ON_TRACK || surface == TRACK_SURFACE_APPROACHING_PITS;
                if (!data.IsCautionActive || !isOnTrack)
                    continue;
            }

            bool isOnPitRoad = ArrayBool(data.CarIdxOnPitRoad, i, false);

            // Track last valid surface for tow detection
            if (surface != TRACK_SURFACE_NOT_IN_WORLD)
                _lastTrackSurface[i] = surface;

            // ── Determine pit status ───────────────────────────────
            PitStatus pitState = PitStatus.None;
            if (surface == TRACK_SURFACE_IN_PIT_STALL)
            {
                pitState = PitStatus.InPit;
                if (!_wasInPitStall[i])
                {
                    _pitStallEntryTime[i] = now; // record entry time
                    _wasServiced[i] = true; // assume service when entering stall
                    _pitStopCount[i]++; // increment pit stop counter
                    // Tow detection: entering pit stall from on-track without approaching pits
                    int lastSurf = _lastTrackSurface[i];
                    if (lastSurf == TRACK_SURFACE_ON_TRACK || lastSurf == TRACK_SURFACE_OFF_TRACK)
                        _wasTowed[i] = true;
                    else
                        _wasTowed[i] = false;
                }
                _wasInPitStall[i] = true;
            }
            else if (isOnPitRoad)
            {
                if (_wasInPitStall[i])
                {
                    pitState = PitStatus.ExitingPit; // was in stall, now on pit road = exiting
                    // Record final BOX duration and exit start time
                    if (_exitingPitStartTime[i] == null)
                    {
                        _exitingPitStartTime[i] = now;
                        _finalBoxDuration[i] = _pitStallEntryTime[i] is DateTime entryDt
                            ? (float)(now - entryDt).TotalSeconds : 0f;
                    }
                }
                else
                    pitState = PitStatus.Pitting; // on pit road, haven't reached stall = entering
            }
            else
            {
                // Not on pit road anymore — clear tow state
                _wasTowed[i] = false;
                if (_wasInPitStall[i] && _wasServiced[i])
                {
                    _isOnOutLap[i] = true; // just left pits after service → out-lap
                    _pitExitLapDistPct[i] = pct; // record where they exited
                }
                _wasInPitStall[i] = false;
                _pitStallEntryTime[i] = null;
                // Clear exiting state after 3 seconds
                if (_exitingPitStartTime[i] is DateTime exitStart && (now - exitStart).TotalSeconds > 3.5)
                {
                    _exitingPitStartTime[i] = null;
                    _finalBoxDuration[i] = 0f;
                }
            }

            // Out-lap ends when the car has traveled 85% of the remaining track distance.
            // This makes OUTLAP visible for ~85% of the outlap, then disappears.
            if (_isOnOutLap[i] && !isOnPitRoad && surface == TRACK_SURFACE_ON_TRACK)
            {
                float exitPct = _pitExitLapDistPct[i];
                float outlaplength = 1.0f - exitPct; // distance from pit exit to S/F
                float threshold = exitPct + 0.85f * outlaplength; // 85% of outlap
                
                if (threshold <= 1.0f)
                {
                    if (pct >= threshold)
                        _isOnOutLap[i] = false;
                }
                else
                {
                    // Wrapped past S/F — threshold is in the next lap
                    float wrapThreshold = threshold - 1.0f;
                    if (pct < exitPct && pct >= wrapThreshold)
                        _isOnOutLap[i] = false;
                }
            }

            // ── Per-car flags (meatball, black) ────────────────────
            int carFlags = ArrayInt(data.CarIdxSessionFlags, i, 0);
            bool hasMeatball = (carFlags & FLAG_REPAIR) != 0;
            bool hasBlackFlag = (carFlags & FLAG_BLACK) != 0;

            // ── Build entry ────────────────────────────────────────
            float lastLap = ArrayFloat(data.CarIdxLastLapTime, i, 0f);
            float bestLap = ArrayFloat(data.CarIdxBestLapTime, i, 0f);

            var entry = new RelativeEntry
            {
                CarIdx = i,
                LapDistPct = pct,
                LapNumber = ArrayInt(data.CarIdxLap, i, 0),
                OverallPosition = ArrayInt(data.CarIdxPosition, i, 0),
                ClassPosition = ArrayInt(data.CarIdxClassPosition, i, 0),
                CarClassId = ArrayInt(data.CarIdxClass, i, 0),
                LastLapTime = lastLap,
                BestLapTime = bestLap,
                IsOnPitRoad = isOnPitRoad,
                DriverName = DictString(data.CarIdxToDriverName, i, string.Empty),
                CarNumber = DictString(data.CarIdxToCarNumber, i, string.Empty),
                IsPlayer = (i == playerIdx),
                IsConnected = surface >= 0,
                IsOffTrack = surface == TRACK_SURFACE_OFF_TRACK,
                PitState = pitState,
                // Personal best: last lap matches their best lap (within tolerance)
                IsPersonalBest = lastLap > 1.0f && bestLap > 1.0f && Math.Abs(lastLap - bestLap) < 0.01f,
                // Session best: last lap matches overall session best (within tolerance)
                IsSessionBest = lastLap > 1.0f && sessionBestLapTime < float.MaxValue && Math.Abs(lastLap - sessionBestLapTime) < 0.01f,
                // iRating, Safety Rating, License Class from YAML
                IRating = DictInt(data.CarIdxToIRating, i, 0),
                SafetyRating = DictFloat(data.CarIdxToSafetyRating, i, 0f),
                LicenseClass = DictString(data.CarIdxToLicenseClass, i, string.Empty),
                // Per-car flags
                HasMeatball = hasMeatball,
                HasBlackFlag = hasBlackFlag,
                HasRecentIncident = data.CarIdxRecentIncident != null && i < data.CarIdxRecentIncident.Length && data.CarIdxRecentIncident[i],
                IncidentCount = DictInt(data.CarIdxToIncidentCount, i, 0),
                IncidentDelta = ArrayInt(data.CarIdxRecentIncidentDelta, i, 0),
                WasTowed = _wasTowed[i],
                CarModel = DictString(data.CarIdxToCarModel, i, string.Empty),
                // Out-lap & pit stall timer
                IsOnOutLap = _isOnOutLap[i],
                PitStallEntryTime = _pitStallEntryTime[i],
                PitStallDuration = _pitStallEntryTime[i] is DateTime entry_dt
                    ? (float)(now - entry_dt).TotalSeconds : 0f,
                FinalBoxDuration = _finalBoxDuration[i],
                ExitingPitDuration = _exitingPitStartTime[i] is DateTime exitDt
                    ? (float)(now - exitDt).TotalSeconds : 0f,
                // New feature fields
                PitStopCount = _pitStopCount[i],
                CountryCode = DictString(data.CarIdxToCountryCode, i, string.Empty),
                IsSafetyCar = isSafetyCar,
            };

            // ── Off-track duration accumulation ────────────────────
            if (entry.IsOffTrack)
                _offTrackDuration[i] += deltaTime;
            else
                _offTrackDuration[i] = 0f;
            entry.OffTrackDuration = _offTrackDuration[i];

            // ── Out-lap progress (0.0 → 1.0 through outlap) ───────
            if (entry.IsOnOutLap)
            {
                float exitPct = _pitExitLapDistPct[i];
                float lapLen = 1.0f - exitPct;
                if (lapLen > 0.01f)
                {
                    float outDist = pct >= exitPct ? pct - exitPct : (1.0f - exitPct) + pct;
                    entry.OutLapProgress = Math.Clamp(outDist / lapLen, 0f, 1f);
                }
            }

            // ── Per-lap history for closing rate + position delta ───
            int carLap = ArrayInt(data.CarIdxLap, i, 0);
            if (carLap > 0 && carLap != _lastKnownLap[i])
            {
                // New lap completed — snapshot current interval and position
                int idx2 = _historyIdx[i] % HISTORY_SIZE;
                _intervalHistory[i][idx2] = entry.IntervalToPlayer;
                _positionHistory[i][idx2] = entry.OverallPosition;
                _historyIdx[i]++;
                _lastKnownLap[i] = carLap;
            }

            // Closing rate: compare current interval to 3 laps ago
            if (_historyIdx[i] >= 3 && !entry.IsPlayer)
            {
                int cur = (_historyIdx[i] - 1) % HISTORY_SIZE;
                int old = (_historyIdx[i] - 3) % HISTORY_SIZE;
                float oldAbs = Math.Abs(_intervalHistory[i][old]);
                float curAbs = Math.Abs(entry.IntervalToPlayer);
                entry.ClosingRate = (oldAbs - curAbs) / 3f; // positive = closing
                entry.IsGapClosing = curAbs < oldAbs;
            }
            else
            {
                // Per-frame gap trend as fallback
                float prevAbs = Math.Abs(_prevInterval[i]);
                float curAbs2 = Math.Abs(entry.IntervalToPlayer);
                entry.IsGapClosing = curAbs2 < prevAbs && prevAbs > 0.01f;
            }
            _prevInterval[i] = entry.IntervalToPlayer;

            // Position delta: compare to 5 laps ago
            if (_historyIdx[i] >= 5 && !entry.IsPlayer)
            {
                int old5 = (_historyIdx[i] - 5) % HISTORY_SIZE;
                entry.PositionDelta = _positionHistory[i][old5] - entry.OverallPosition; // positive = gained
            }

            // ── Player row ─────────────────────────────────────────
            if (i == playerIdx)
            {
                entry.IntervalToPlayer = 0f;
                entry.LapDelta = 0;
                // Populate player data from direct telemetry fields
                entry.DriverName = !string.IsNullOrEmpty(data.DriverName) ? data.DriverName : entry.DriverName;
                entry.CarNumber = !string.IsNullOrEmpty(data.CarNumber) ? data.CarNumber : entry.CarNumber;
                entry.OverallPosition = data.LivePosition > 0 ? data.LivePosition : entry.OverallPosition;
                entry.ClassPosition = data.LiveClassPosition > 0 ? data.LiveClassPosition : entry.ClassPosition;
                entry.LastLapTime = data.LapLastLapTime > 0 ? data.LapLastLapTime : entry.LastLapTime;
                entry.BestLapTime = data.LapBestLapTime > 0 ? data.LapBestLapTime : entry.BestLapTime;
                playerEntry = entry;
                continue; // placed later in the middle
            }

            // ── Track distance (circular, wraps at start/finish) ───
            float dist = pct - playerPct;
            if (dist > 0.5f) dist -= 1.0f;
            if (dist < -0.5f) dist += 1.0f;

            // Convert to time
            entry.IntervalToPlayer = dist * _referenceLapTime;

            // ── Lap delta adjusted for S/F crossing ────────────────
            int rawLapDelta = entry.LapNumber - playerLap;
            if (dist > 0f && rawLapDelta < 0)
                rawLapDelta += 1; // ahead on track but lap count behind → S/F crossing
            else if (dist < 0f && rawLapDelta > 0)
                rawLapDelta -= 1; // behind on track but lap count ahead → S/F crossing
            entry.LapDelta = rawLapDelta;

            if (dist >= 0f)
                _ahead.Add(entry);
            else
                _behind.Add(entry);
        }

        // ── Select and order ahead cars ────────────────────────────
        // Sort ascending by interval (closest ahead first)
        _ahead.Sort((a, b) => a.IntervalToPlayer.CompareTo(b.IntervalToPlayer));
        // Take the N closest
        int takeAhead = Math.Min(_ahead.Count, maxAhead);
        // Reverse order for display: farthest visible at top → closest at bottom (near player)
        for (int i = takeAhead - 1; i >= 0; i--)
            _result.Add(_ahead[i]);

        // ── Select and order behind cars ───────────────────────────
        // Sort descending by interval (closest behind = least negative first)
        _behind.Sort((a, b) => b.IntervalToPlayer.CompareTo(a.IntervalToPlayer));
        int takeBehind = Math.Min(_behind.Count, maxBehind);

        // ── Player row in MIDDLE (between ahead and behind) ─────────
        if (playerEntry != null)
            _result.Add(playerEntry);

        // Behind cars (closest to farthest)
        for (int i = 0; i < takeBehind; i++)
            _result.Add(_behind[i]);

        // ── Compute gap-to-car-ahead in display order ──────────────
        // In the display list, each row's gap = time difference to the
        // row directly above it. First row has no car ahead (gap = 0).
        ComputeGaps(_result);

        return _result;
    }

    /// <summary>
    /// Compute the gap between consecutive rows in display order.
    /// Uses absolute difference of IntervalToPlayer between adjacent entries.
    /// </summary>
    private static void ComputeGaps(List<RelativeEntry> entries)
    {
        if (entries.Count == 0) return;

        entries[0].GapToCarAhead = 0f;

        for (int i = 1; i < entries.Count; i++)
        {
            float gap = Math.Abs(entries[i].IntervalToPlayer - entries[i - 1].IntervalToPlayer);
            entries[i].GapToCarAhead = gap;
        }
    }

    /// <summary>
    /// Get a smart row count that adapts to the player's field position.
    /// Front-runners see more cars behind; back-markers see more ahead.
    /// </summary>
    /// <param name="playerPosition">Player's overall position (1-based)</param>
    /// <param name="totalCars">Total cars in the field</param>
    /// <param name="totalRows">Total available display rows (excluding player)</param>
    /// <returns>(maxAhead, maxBehind)</returns>
    public static (int ahead, int behind) GetSmartRowCount(int playerPosition, int totalCars, int totalRows = 12)
    {
        if (totalCars <= 0 || playerPosition <= 0)
            return (totalRows / 2, totalRows / 2);

        // Ratio of position in field (0.0 = leader, 1.0 = last place)
        float posRatio = (float)(playerPosition - 1) / Math.Max(totalCars - 1, 1);

        // Bias: front of field → more behind, back → more ahead
        // Leader (0.0): 30% ahead / 70% behind
        // Last (1.0):   70% ahead / 30% behind
        // Mid  (0.5):   50/50
        float aheadRatio = 0.3f + (posRatio * 0.4f); // 0.3 → 0.7
        int ahead = (int)Math.Round(totalRows * aheadRatio);
        int behind = totalRows - ahead;

        return (Math.Max(ahead, 1), Math.Max(behind, 1));
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private void UpdateReferenceLapTime(TelemetryData data)
    {
        if (data.LapBestLapTime > 1.0f)
            _referenceLapTime = data.LapBestLapTime;
        else if (data.LapLastLapTime > 1.0f)
            _referenceLapTime = data.LapLastLapTime;
        // else: keep previous or default
    }

    private static int ArrayInt(int[]? arr, int idx, int def)
        => arr != null && (uint)idx < (uint)arr.Length ? arr[idx] : def;

    private static float ArrayFloat(float[]? arr, int idx, float def)
        => arr != null && (uint)idx < (uint)arr.Length ? arr[idx] : def;

    private static bool ArrayBool(bool[]? arr, int idx, bool def)
        => arr != null && (uint)idx < (uint)arr.Length ? arr[idx] : def;

    private static string DictString(Dictionary<int, string>? dict, int key, string def)
        => dict != null && dict.TryGetValue(key, out var v) ? v : def;

    private static int DictInt(Dictionary<int, int>? dict, int key, int def)
        => dict != null && dict.TryGetValue(key, out var v) ? v : def;

    private static float DictFloat(Dictionary<int, float>? dict, int key, float def)
        => dict != null && dict.TryGetValue(key, out var v) ? v : def;
}
