using iRacingOverlay.Core.Models;
using System.Text.Json;

namespace iRacingOverlay.Core.Services.History;

/// <summary>
/// Phase 9: Telemetry History Service
/// Loads historical fuel/tire patterns from past sessions
/// Provides instant predictions at race start instead of 3-lap warmup
/// </summary>
public class TelemetryHistoryService
{
    private readonly string _historyFilePath;
    private readonly Dictionary<string, TrackCarHistory> _historyCache = new();
    private bool _isLoaded = false;
    
    public TelemetryHistoryService(string? dataDirectory = null)
    {
        dataDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "iRacingOverlay",
            "History");
        
        Directory.CreateDirectory(dataDirectory);
        _historyFilePath = Path.Combine(dataDirectory, "telemetry_history.json");
    }
    
    /// <summary>
    /// Load historical data from disk
    /// </summary>
    public async Task LoadHistoryAsync()
    {
        if (_isLoaded)
            return;
        
        try
        {
            if (File.Exists(_historyFilePath))
            {
                var json = await File.ReadAllTextAsync(_historyFilePath);
                var history = JsonSerializer.Deserialize<Dictionary<string, TrackCarHistory>>(json);
                
                if (history != null)
                {
                    foreach (var kvp in history)
                    {
                        _historyCache[kvp.Key] = kvp.Value;
                    }
                }
            }
            
            _isLoaded = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TelemetryHistory] Failed to load history: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Get historical predictions for track/car combination
    /// </summary>
    public HistoricalPrediction? GetPrediction(string trackName, int carClassId)
    {
        var key = GetKey(trackName, carClassId);
        
        if (_historyCache.TryGetValue(key, out var history))
        {
            return new HistoricalPrediction
            {
                AvgFuelPerLap = history.AvgFuelPerLap,
                AvgLapTime = history.AvgLapTime,
                AvgTireWearRate = history.AvgTireWearRate,
                TireLapsAverage = history.TireLapsAverage,
                SessionCount = history.SessionCount,
                LastUpdated = history.LastUpdated,
                ConfidenceScore = CalculateConfidence(history)
            };
        }
        
        return null;
    }
    
    /// <summary>
    /// Update history with completed session data
    /// </summary>
    public async Task UpdateHistoryAsync(
        string trackName,
        int carClassId,
        float avgFuelPerLap,
        float avgLapTime,
        float avgTireWearRate,
        int tireLaps)
    {
        var key = GetKey(trackName, carClassId);
        
        if (!_historyCache.TryGetValue(key, out var history))
        {
            history = new TrackCarHistory
            {
                TrackName = trackName,
                CarClassId = carClassId
            };
            _historyCache[key] = history;
        }
        
        // Update with exponential moving average (weight recent sessions more)
        float alpha = 0.3f; // 30% weight to new data
        
        if (history.SessionCount == 0)
        {
            // First session - use raw values
            history.AvgFuelPerLap = avgFuelPerLap;
            history.AvgLapTime = avgLapTime;
            history.AvgTireWearRate = avgTireWearRate;
            history.TireLapsAverage = tireLaps;
        }
        else
        {
            // EMA update
            history.AvgFuelPerLap = history.AvgFuelPerLap * (1 - alpha) + avgFuelPerLap * alpha;
            history.AvgLapTime = history.AvgLapTime * (1 - alpha) + avgLapTime * alpha;
            history.AvgTireWearRate = history.AvgTireWearRate * (1 - alpha) + avgTireWearRate * alpha;
            history.TireLapsAverage = (int)(history.TireLapsAverage * (1 - alpha) + tireLaps * alpha);
        }
        
        history.SessionCount++;
        history.LastUpdated = DateTime.UtcNow;
        
        // Save to disk
        await SaveHistoryAsync();
    }
    
    /// <summary>
    /// Save history to disk
    /// </summary>
    private async Task SaveHistoryAsync()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            var json = JsonSerializer.Serialize(_historyCache, options);
            await File.WriteAllTextAsync(_historyFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TelemetryHistory] Failed to save history: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Calculate confidence score for prediction
    /// </summary>
    private float CalculateConfidence(TrackCarHistory history)
    {
        // Confidence based on number of sessions
        // 1 session = 30%, 5 sessions = 70%, 10+ sessions = 95%
        float sessionConfidence = Math.Min(history.SessionCount * 10f, 95f);
        
        // Reduce confidence if data is old (>30 days = -20%)
        var age = (DateTime.UtcNow - history.LastUpdated).TotalDays;
        float ageMultiplier = age > 30 ? 0.8f : 1.0f;
        
        return sessionConfidence * ageMultiplier;
    }
    
    /// <summary>
    /// Get cache key for track/car combination
    /// </summary>
    private string GetKey(string trackName, int carClassId)
    {
        return $"{trackName}_{carClassId}";
    }
    
    /// <summary>
    /// Check if we have history for this track/car
    /// </summary>
    public bool HasHistory(string trackName, int carClassId)
    {
        var key = GetKey(trackName, carClassId);
        return _historyCache.ContainsKey(key);
    }
    
    /// <summary>
    /// Clear all history (for testing or reset)
    /// </summary>
    public async Task ClearHistoryAsync()
    {
        _historyCache.Clear();
        await SaveHistoryAsync();
    }
}

/// <summary>
/// Track/Car combination history
/// </summary>
public class TrackCarHistory
{
    public string TrackName { get; set; } = "";
    public int CarClassId { get; set; }
    public float AvgFuelPerLap { get; set; }
    public float AvgLapTime { get; set; }
    public float AvgTireWearRate { get; set; }
    public int TireLapsAverage { get; set; }
    public int SessionCount { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Historical prediction result
/// </summary>
public class HistoricalPrediction
{
    public float AvgFuelPerLap { get; set; }
    public float AvgLapTime { get; set; }
    public float AvgTireWearRate { get; set; }
    public int TireLapsAverage { get; set; }
    public int SessionCount { get; set; }
    public DateTime LastUpdated { get; set; }
    public float ConfidenceScore { get; set; }
    
    public bool IsHighConfidence => ConfidenceScore >= 70f;
    public bool IsMediumConfidence => ConfidenceScore >= 40f && ConfidenceScore < 70f;
    public bool IsLowConfidence => ConfidenceScore < 40f;
}
