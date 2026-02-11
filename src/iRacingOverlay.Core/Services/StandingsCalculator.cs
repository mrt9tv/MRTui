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
    /// <returns>Sorted standings list, or empty if data is insufficient.</returns>
    public List<StandingsEntry> Calculate(TelemetryData data, int maxRows = 0)
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

        // Sort by overall position
        entries.Sort((a, b) => a.OverallPosition.CompareTo(b.OverallPosition));

        // Second pass — compute intervals and gap-to-leader
        float leaderEstTime = 0f;
        for (int idx = 0; idx < entries.Count; idx++)
        {
            var e = entries[idx];
            int ci = e.CarIdx;

            // Lap delta relative to leader
            e.LapDelta = leaderLap > 0 ? e.CurrentLap - leaderLap : 0;

            // Gap to leader via EstTime
            float estTime = data.CarIdxEstTime != null && ci < data.CarIdxEstTime.Length
                ? data.CarIdxEstTime[ci] : 0f;

            if (idx == 0)
            {
                leaderEstTime = estTime;
                e.GapToLeader = 0f;
                e.Interval = 0f;
            }
            else
            {
                // F2Time = time behind leader (from iRacing)
                float f2 = data.CarIdxF2Time != null && ci < data.CarIdxF2Time.Length
                    ? data.CarIdxF2Time[ci] : 0f;
                e.GapToLeader = f2;

                // Interval = gap to car directly ahead
                var ahead = entries[idx - 1];
                float aheadF2 = data.CarIdxF2Time != null && ahead.CarIdx < data.CarIdxF2Time.Length
                    ? data.CarIdxF2Time[ahead.CarIdx] : 0f;
                e.Interval = f2 - aheadF2;
            }
        }

        if (maxRows > 0 && entries.Count > maxRows)
            entries.RemoveRange(maxRows, entries.Count - maxRows);

        return entries;
    }

    /// <summary>Reset all tracked state (new session).</summary>
    public void Reset()
    {
        _startPositions.Clear();
        Array.Clear(_pitStopCounts, 0, MAX_CARS);
        Array.Clear(_wasPitting, 0, MAX_CARS);
        _initialised = false;
    }
}
