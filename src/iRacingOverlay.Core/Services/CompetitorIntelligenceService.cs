using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Service for tracking and analyzing competitor intelligence
/// Provides pit stop tracking, lap time comparisons, and gap analysis
/// </summary>
public class CompetitorIntelligenceService
{
    private readonly Dictionary<int, bool> _previousPitStatus = new(); // CarIdx -> Previous pit road status
    private readonly List<CompetitorPitActivity> _recentPitActivity = new(); // Recent pit stops (last 10 laps)
    private const int MAX_PIT_HISTORY = 20; // Keep last 20 pit stops

    /// <summary>
    /// Get all competitors with their current information
    /// </summary>
    public List<CompetitorInfo> GetCompetitors(TelemetryData data, bool sameClassOnly = true)
    {
        var competitors = new List<CompetitorInfo>();
        
        if (data.CarIdxPosition == null || data.CarIdxLapDistPct == null)
            return competitors;

        int playerCarIdx = data.PlayerCarIdx;
        int playerClass = data.PlayerCarClass;

        for (int i = 0; i < data.CarIdxPosition.Length; i++)
        {
            // Skip invalid entries and player's own car
            if (data.CarIdxPosition[i] <= 0 || i == playerCarIdx)
                continue;

            // Filter by class if requested
            if (sameClassOnly && data.CarIdxClass != null && data.CarIdxClass[i] != playerClass)
                continue;

            var competitor = new CompetitorInfo
            {
                CarIdx = i,
                Position = data.CarIdxPosition[i],
                Lap = data.CarIdxLap?[i] ?? 0,
                TimeToLeader = data.CarIdxF2Time?[i] ?? 0f,
                LastLapTime = data.CarIdxLastLapTime?[i] ?? 0f,
                BestLapTime = data.CarIdxBestLapTime?[i] ?? 0f,
                IsOnPitRoad = data.CarIdxOnPitRoad?[i] ?? false,
                LapDistPct = data.CarIdxLapDistPct[i],
                CarClass = data.CarIdxClass?[i] ?? 0,
                CarNumber = data.CarIdxToCarNumber?.GetValueOrDefault(i) ?? $"#{i}",
                DriverName = data.CarIdxToDriverName?.GetValueOrDefault(i) ?? $"Driver {i}"
            };

            competitors.Add(competitor);
        }

        return competitors.OrderBy(c => c.Position).ToList();
    }

    /// <summary>
    /// Get competitor in specific position
    /// </summary>
    public CompetitorInfo? GetCompetitorInPosition(TelemetryData data, int position, bool sameClassOnly = true)
    {
        return GetCompetitors(data, sameClassOnly).FirstOrDefault(c => c.Position == position);
    }

    /// <summary>
    /// Get competitor ahead of player
    /// </summary>
    public CompetitorInfo? GetCompetitorAhead(TelemetryData data, bool sameClassOnly = true)
    {
        int playerPosition = data.LiveClassPosition;
        return GetCompetitorInPosition(data, playerPosition - 1, sameClassOnly);
    }

    /// <summary>
    /// Get competitor behind player
    /// </summary>
    public CompetitorInfo? GetCompetitorBehind(TelemetryData data, bool sameClassOnly = true)
    {
        int playerPosition = data.LiveClassPosition;
        return GetCompetitorInPosition(data, playerPosition + 1, sameClassOnly);
    }

    /// <summary>
    /// Get race leader
    /// </summary>
    public CompetitorInfo? GetLeader(TelemetryData data, bool sameClassOnly = true)
    {
        return GetCompetitorInPosition(data, 1, sameClassOnly);
    }

    /// <summary>
    /// Calculate gap to leader
    /// </summary>
    public float GetGapToLeader(TelemetryData data)
    {
        if (data.CarIdxF2Time == null || data.PlayerCarIdx < 0 || data.PlayerCarIdx >= data.CarIdxF2Time.Length)
            return 0f;

        return data.CarIdxF2Time[data.PlayerCarIdx];
    }

    /// <summary>
    /// Calculate gap to car ahead (in same class)
    /// </summary>
    public float GetGapToCarAhead(TelemetryData data)
    {
        var carAhead = GetCompetitorAhead(data, sameClassOnly: true);
        if (carAhead == null || data.CarIdxF2Time == null)
            return 0f;

        float playerTime = data.CarIdxF2Time[data.PlayerCarIdx];
        float aheadTime = data.CarIdxF2Time[carAhead.CarIdx];
        
        return aheadTime - playerTime; // Negative value means ahead
    }

    /// <summary>
    /// Calculate gap to car behind (in same class)
    /// </summary>
    public float GetGapToCarBehind(TelemetryData data)
    {
        var carBehind = GetCompetitorBehind(data, sameClassOnly: true);
        if (carBehind == null || data.CarIdxF2Time == null)
            return 0f;

        float playerTime = data.CarIdxF2Time[data.PlayerCarIdx];
        float behindTime = data.CarIdxF2Time[carBehind.CarIdx];
        
        return playerTime - behindTime; // Positive value means behind
    }

    /// <summary>
    /// Update pit activity tracking and return recent pit stops
    /// </summary>
    public List<CompetitorPitActivity> UpdatePitActivity(TelemetryData data, int currentLap)
    {
        if (data.CarIdxOnPitRoad == null || data.CarIdxPosition == null)
            return _recentPitActivity;

        // Check each car for pit status changes
        for (int i = 0; i < data.CarIdxOnPitRoad.Length; i++)
        {
            bool currentPitStatus = data.CarIdxOnPitRoad[i];
            bool previousPitStatus = _previousPitStatus.GetValueOrDefault(i, false);

            // Detect pit entry (not on pit road -> on pit road)
            if (!previousPitStatus && currentPitStatus)
            {
                int position = data.CarIdxPosition[i];
                if (position > 0) // Valid position
                {
                    var activity = new CompetitorPitActivity
                    {
                        CarNumber = data.CarIdxToCarNumber?.GetValueOrDefault(i) ?? $"#{i}",
                        DriverName = data.CarIdxToDriverName?.GetValueOrDefault(i) ?? $"Driver {i}",
                        PitLap = currentLap,
                        Position = position,
                        Status = "PITTING NOW"
                    };

                    _recentPitActivity.Insert(0, activity); // Add to front of list

                    // Limit history size
                    if (_recentPitActivity.Count > MAX_PIT_HISTORY)
                        _recentPitActivity.RemoveAt(_recentPitActivity.Count - 1);
                }
            }

            // Update previous status
            _previousPitStatus[i] = currentPitStatus;
        }

        return _recentPitActivity;
    }

    /// <summary>
    /// Get recent pit activity (last N stops)
    /// </summary>
    public List<CompetitorPitActivity> GetRecentPitActivity(int count = 10)
    {
        return _recentPitActivity.Take(count).ToList();
    }

    /// <summary>
    /// Clear pit activity history (e.g., on new session)
    /// </summary>
    public void ClearPitActivity()
    {
        _recentPitActivity.Clear();
        _previousPitStatus.Clear();
    }

    /// <summary>
    /// Get lap time comparison between player and specific competitor
    /// </summary>
    public string CompareLapTime(TelemetryData data, int competitorCarIdx)
    {
        if (data.CarIdxLastLapTime == null)
            return "N/A";

        float playerTime = data.LapLastLapTime;
        float competitorTime = data.CarIdxLastLapTime[competitorCarIdx];

        if (playerTime <= 0 || competitorTime <= 0)
            return "N/A";

        float delta = playerTime - competitorTime;
        
        if (Math.Abs(delta) < 0.01f)
            return "EVEN";
        else if (delta < 0)
            return $"▲ {Math.Abs(delta):F3}s"; // Player faster
        else
            return $"▼ {delta:F3}s"; // Competitor faster
    }

    /// <summary>
    /// Get top N fastest lap times in session
    /// </summary>
    public List<CompetitorInfo> GetFastestLaps(TelemetryData data, int count = 10, bool sameClassOnly = true)
    {
        return GetCompetitors(data, sameClassOnly)
            .Where(c => c.BestLapTime > 0)
            .OrderBy(c => c.BestLapTime)
            .Take(count)
            .ToList();
    }
}
