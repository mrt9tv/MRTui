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

    // ── Reusable scratch buffers (avoid allocations on the hot path) ────

    private readonly List<RelativeEntry> _result = new(MAX_CARS);
    private readonly List<RelativeEntry> _ahead = new(MAX_CARS / 2);
    private readonly List<RelativeEntry> _behind = new(MAX_CARS / 2);

    /// <summary>Cached reference lap time for distance → time conversion</summary>
    private float _referenceLapTime = DEFAULT_LAP_TIME;

    /// <summary>Track off-track duration per car index (seconds). Reset when on-track.</summary>
    private readonly float[] _offTrackDuration = new float[MAX_CARS];

    /// <summary>Timestamp of last Calculate call (for delta timing)</summary>
    private DateTime _lastCalcTime = DateTime.UtcNow;

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
    public List<RelativeEntry> Calculate(TelemetryData data, int maxAhead = 6, int maxBehind = 6)
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

        RelativeEntry? playerEntry = null;
        int carCount = Math.Min(data.CarIdxLapDistPct.Length, MAX_CARS);

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

            // ── Build entry ────────────────────────────────────────
            var entry = new RelativeEntry
            {
                CarIdx = i,
                LapDistPct = pct,
                LapNumber = ArrayInt(data.CarIdxLap, i, 0),
                OverallPosition = ArrayInt(data.CarIdxPosition, i, 0),
                ClassPosition = ArrayInt(data.CarIdxClassPosition, i, 0),
                CarClassId = ArrayInt(data.CarIdxClass, i, 0),
                LastLapTime = ArrayFloat(data.CarIdxLastLapTime, i, 0f),
                BestLapTime = ArrayFloat(data.CarIdxBestLapTime, i, 0f),
                IsOnPitRoad = ArrayBool(data.CarIdxOnPitRoad, i, false),
                DriverName = DictString(data.CarIdxToDriverName, i, string.Empty),
                CarNumber = DictString(data.CarIdxToCarNumber, i, i.ToString()),
                IsPlayer = (i == playerIdx),
                IsConnected = surface >= 0,
                IsOffTrack = surface == TRACK_SURFACE_OFF_TRACK
            };

            // ── Off-track duration accumulation ────────────────────
            if (entry.IsOffTrack)
                _offTrackDuration[i] += deltaTime;
            else
                _offTrackDuration[i] = 0f;
            entry.OffTrackDuration = _offTrackDuration[i];

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

        // ── Player row ─────────────────────────────────────────────
        if (playerEntry != null)
            _result.Add(playerEntry);

        // ── Select and order behind cars ───────────────────────────
        // Sort descending by interval (closest behind = least negative first)
        _behind.Sort((a, b) => b.IntervalToPlayer.CompareTo(a.IntervalToPlayer));
        // Take the N closest
        int takeBehind = Math.Min(_behind.Count, maxBehind);
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
}
