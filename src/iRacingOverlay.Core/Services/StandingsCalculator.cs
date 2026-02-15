using System;
using System.Collections.Generic;
using System.Linq;
using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Calculates full-field standings from live telemetry arrays.
/// Returns a sorted list of <see cref="StandingsEntry"/> for the overlay.
/// 
/// Thread-safety: single-threaded; call from UI dispatch only.
/// </summary>
public class StandingsCalculator
{
    private const int MAX_CARS = 64;

    /// <summary>
    /// Reference lap time for converting LapDistPct gaps to seconds.
    /// Updated each tick from the leader's best/last lap.
    /// </summary>
    private float _referenceLapTime;

    /// <summary>
    /// Starting positions captured at first valid update (for position-change calc).
    /// Key = CarIdx, Value = starting overall position.
    /// </summary>
    private readonly Dictionary<int, int> _startPositions = new();

    /// <summary>Pit-stop counters: Key = CarIdx.</summary>
    private readonly int[] _pitStopCounts = new int[MAX_CARS];

    /// <summary>Track whether car was on pit road last tick (for pit-stop counting).</summary>
    private readonly bool[] _wasPitting = new bool[MAX_CARS];

    private bool _initialised;

    /// <summary>
    /// Produce the standings list from the current telemetry frame.
    /// </summary>
    /// <param name="data">Latest telemetry snapshot.</param>
    /// <param name="maxRows">Maximum entries to return (0 = all).</param>
    /// <param name="alwaysIncludePlayer">If true, ensure the player is in the returned list even if beyond maxRows.</param>
    /// <returns>Sorted standings list, or empty if data is insufficient.</returns>
    public List<StandingsEntry> Calculate(TelemetryData data, int maxRows = 0, bool alwaysIncludePlayer = false)
    {
        if (data.CarIdxPosition == null || data.CarIdxLapDistPct == null)
            return new List<StandingsEntry>();

        var entries = new List<StandingsEntry>(MAX_CARS);
        int playerIdx = data.PlayerCarIdx;
        int leaderLap = 0;

        // First pass — build entries for all connected cars
        for (int i = 0; i < MAX_CARS; i++)
        {
            int pos = data.CarIdxPosition != null && i < data.CarIdxPosition.Length
                ? data.CarIdxPosition[i] : 0;
            if (pos <= 0) continue; // Not classified / not in session

            float lapDistPct = data.CarIdxLapDistPct != null && i < data.CarIdxLapDistPct.Length
                ? data.CarIdxLapDistPct[i] : 0f;

            // Skip disconnected (off-world) cars: lapDistPct stays at -1 or 0 with pos 0
            // Already filtered by pos <= 0 above.

            int lap = data.CarIdxLap != null && i < data.CarIdxLap.Length
                ? data.CarIdxLap[i] : 0;

            bool onPitRoad = data.CarIdxOnPitRoad != null && i < data.CarIdxOnPitRoad.Length
                && data.CarIdxOnPitRoad[i];

            // Track pit stops
            if (onPitRoad && !_wasPitting[i])
                _pitStopCounts[i]++;
            _wasPitting[i] = onPitRoad;

            // Capture starting position on first valid tick
            if (!_initialised)
                _startPositions[i] = pos;

            var entry = new StandingsEntry
            {
                CarIdx = i,
                DriverName = data.CarIdxToDriverName?.GetValueOrDefault(i) ?? $"Car {i}",
                CarNumber = data.CarIdxToCarNumber?.GetValueOrDefault(i) ?? i.ToString(),
                CarClassId = data.CarIdxClass != null && i < data.CarIdxClass.Length
                    ? data.CarIdxClass[i] : 0,
                CarModel = data.CarIdxToCarModel?.GetValueOrDefault(i) ?? string.Empty,
                CountryCode = data.CarIdxToCountryCode?.GetValueOrDefault(i) ?? string.Empty,
                OverallPosition = pos,
                ClassPosition = data.CarIdxClassPosition != null && i < data.CarIdxClassPosition.Length
                    ? data.CarIdxClassPosition[i] : 0,
                LastLapTime = data.CarIdxLastLapTime != null && i < data.CarIdxLastLapTime.Length
                    ? data.CarIdxLastLapTime[i] : 0f,
                BestLapTime = data.CarIdxBestLapTime != null && i < data.CarIdxBestLapTime.Length
                    ? data.CarIdxBestLapTime[i] : 0f,
                CurrentLap = lap,
                LapDistPct = lapDistPct,
                IsOnPitRoad = onPitRoad,
                IsPlayer = (i == playerIdx),
                PitStopCount = _pitStopCounts[i],
                IRating = data.CarIdxToIRating?.GetValueOrDefault(i) ?? 0,
                LicenseClass = data.CarIdxToLicenseClass?.GetValueOrDefault(i) ?? string.Empty,
                PositionChange = _startPositions.TryGetValue(i, out int startP) ? startP - pos : 0,
            };

            if (pos == 1) leaderLap = lap;
            entries.Add(entry);
        }

        _initialised = true;

        // ── Live position calculation ─────────────────────────────
        // During active racing (SessionState 3=ParadeLaps, 4=Racing),
        // compute live positions from total distance (lap + lapDistPct)
        // instead of SDK CarIdxPosition which only updates at S/F.
        bool isRacing = data.SessionState == 3 || data.SessionState == 4;

        if (isRacing && entries.Count > 1)
        {
            // Sort by total distance covered (descending = leader first)
            entries.Sort((a, b) =>
            {
                float totalA = a.CurrentLap + a.LapDistPct;
                float totalB = b.CurrentLap + b.LapDistPct;
                return totalB.CompareTo(totalA);
            });

            // Assign live overall positions
            for (int p = 0; p < entries.Count; p++)
                entries[p].OverallPosition = p + 1;

            // Assign live class positions (within each class)
            var classGroups = new Dictionary<int, int>(); // classId → next position
            for (int p = 0; p < entries.Count; p++)
            {
                int cls = entries[p].CarClassId;
                if (!classGroups.TryGetValue(cls, out int nextCP))
                    nextCP = 1;
                entries[p].ClassPosition = nextCP;
                classGroups[cls] = nextCP + 1;
            }

            if (entries.Count > 0)
                leaderLap = entries[0].CurrentLap;

            // Recompute position change from start positions
            for (int p = 0; p < entries.Count; p++)
            {
                var e = entries[p];
                e.PositionChange = _startPositions.TryGetValue(e.CarIdx, out int startP)
                    ? startP - e.OverallPosition : 0;
            }
        }
        else
        {
            // Non-race: sort by SDK overall position
            entries.Sort((a, b) => a.OverallPosition.CompareTo(b.OverallPosition));
        }

        // Second pass — compute intervals and gap-to-leader
        // Use LapDistPct-based gaps for smooth per-frame updates.
        // Falls back to F2Time when no reference lap time is available yet.

        // Build reference lap time from leader's timing
        if (entries.Count > 0)
        {
            var leader = entries[0];
            float best = leader.BestLapTime;
            float last = leader.LastLapTime;
            float newRef = best > 1.0f ? best : last > 1.0f ? last : 0f;
            // Also try player's best if leader has nothing yet
            if (newRef <= 0)
                newRef = data.LapBestLapTime > 1.0f ? data.LapBestLapTime : 0f;
            // Last resort: scan field for ANY car's best lap (early-race fallback)
            if (newRef <= 0 && data.CarIdxBestLapTime != null)
            {
                for (int idx = 0; idx < Math.Min(data.CarIdxBestLapTime.Length, MAX_CARS); idx++)
                {
                    float t = data.CarIdxBestLapTime[idx];
                    if (t > 1.0f) { newRef = t; break; }
                }
            }
            if (newRef > 0)
                _referenceLapTime = newRef;
        }

        for (int idx = 0; idx < entries.Count; idx++)
        {
            var e = entries[idx];
            int ci = e.CarIdx;

            // Lap delta relative to leader
            e.LapDelta = leaderLap > 0 ? e.CurrentLap - leaderLap : 0;

            if (idx == 0)
            {
                e.GapToLeader = 0f;
                e.Interval = 0f;
            }
            else if (_referenceLapTime > 0)
            {
                // LapDistPct-based: total distance in fractional laps → gap in seconds
                var leader = entries[0];
                float leaderTotalDist = leader.CurrentLap + leader.LapDistPct;
                float carTotalDist = e.CurrentLap + e.LapDistPct;
                float aheadTotalDist = entries[idx - 1].CurrentLap + entries[idx - 1].LapDistPct;

                float gapLaps = leaderTotalDist - carTotalDist;
                float intLaps = aheadTotalDist - carTotalDist;

                e.GapToLeader = Math.Max(0f, gapLaps * _referenceLapTime);
                e.Interval = Math.Max(0f, intLaps * _referenceLapTime);
            }
            else
            {
                // Fallback: F2Time (sector-boundary updates) until laps are completed
                float f2 = data.CarIdxF2Time != null && ci < data.CarIdxF2Time.Length
                    ? data.CarIdxF2Time[ci] : 0f;
                e.GapToLeader = f2;

                var ahead = entries[idx - 1];
                float aheadF2 = data.CarIdxF2Time != null && ahead.CarIdx < data.CarIdxF2Time.Length
                    ? data.CarIdxF2Time[ahead.CarIdx] : 0f;
                e.Interval = f2 - aheadF2;
            }
        }

        if (maxRows > 0 && entries.Count > maxRows)
        {
            // Check if the player is beyond the visible range
            if (alwaysIncludePlayer)
            {
                int playerIdx2 = entries.FindIndex(e => e.IsPlayer);
                if (playerIdx2 >= maxRows)
                {
                    // Player is outside visible range — replace last visible row with player
                    var playerEntry = entries[playerIdx2];
                    entries.RemoveRange(maxRows, entries.Count - maxRows);
                    entries[maxRows - 1] = playerEntry;
                }
                else
                {
                    entries.RemoveRange(maxRows, entries.Count - maxRows);
                }
            }
            else
            {
                entries.RemoveRange(maxRows, entries.Count - maxRows);
            }
        }

        return entries;
    }

    /// <summary>Reset all tracked state (new session).</summary>
    public void Reset()
    {
        _startPositions.Clear();
        Array.Clear(_pitStopCounts, 0, MAX_CARS);
        Array.Clear(_wasPitting, 0, MAX_CARS);
        _initialised = false;
        _referenceLapTime = 0f;
    }
}
