using System.Text.Json;
using Microsoft.Extensions.Logging;
using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Service for tracking player's current turn based on LapDistPct and track database
/// </summary>
public class TurnTrackingService
{
    private readonly ILogger<TurnTrackingService> _logger;
    private readonly TrackTurnDatabase _database;
    private string _currentTrackKey = string.Empty;
    private TrackTurnData? _currentTrackData = null;
    
    public TurnTrackingService(ILogger<TurnTrackingService> logger)
    {
        _logger = logger;
        _database = LoadTrackDatabase();
    }
    
    /// <summary>
    /// Load track turn database from embedded JSON resource
    /// </summary>
    private TrackTurnDatabase LoadTrackDatabase()
    {
        try
        {
            // Load from Data/TrackTurnDatabase.json
            // Use AppContext.BaseDirectory (works in single-file publish)
            var assemblyPath = AppContext.BaseDirectory;
            var jsonPath = Path.Combine(assemblyPath, "Data", "TrackTurnDatabase.json");
            
            if (!File.Exists(jsonPath))
            {
                _logger.LogWarning("Track turn database not found at {Path}. Turn tracking disabled.", jsonPath);
                return new TrackTurnDatabase();
            }
            
            var json = File.ReadAllText(jsonPath);
            var database = JsonSerializer.Deserialize<TrackTurnDatabase>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (database == null)
            {
                _logger.LogWarning("Failed to deserialize track turn database. Turn tracking disabled.");
                return new TrackTurnDatabase();
            }
            
            _logger.LogInformation("Loaded track turn database: {TrackCount} tracks, version {Version}", 
                database.Tracks.Count, database.Version);
            
            return database;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading track turn database");
            return new TrackTurnDatabase();
        }
    }
    
    /// <summary>
    /// Set the current track context
    /// </summary>
    /// <param name="trackName">Track name from iRacing SDK (e.g., "spa", "monza")</param>
    public void SetTrack(string trackName)
    {
        if (string.IsNullOrEmpty(trackName))
        {
            _currentTrackKey = string.Empty;
            _currentTrackData = null;
            return;
        }
        
        // Normalize track name to lowercase for dictionary lookup
        var trackKey = trackName.ToLowerInvariant().Trim();
        
        // Skip if already set to this track
        if (trackKey == _currentTrackKey)
            return;
        
        _currentTrackKey = trackKey;
        
        // Try exact match first
        if (_database.Tracks.TryGetValue(trackKey, out var trackData))
        {
            _currentTrackData = trackData;
            _logger.LogInformation("Turn tracking enabled for {TrackName} ({TrackDisplay}) - {TurnCount} turns mapped",
                trackKey, trackData.DisplayName, trackData.TotalTurns);
            return;
        }
        
        // Try normalized match (strip spaces, hyphens, underscores)
        var normalizedKey = NormalizeTrackKey(trackKey);
        foreach (var kvp in _database.Tracks)
        {
            if (NormalizeTrackKey(kvp.Key) == normalizedKey)
            {
                _currentTrackData = kvp.Value;
                _logger.LogInformation("Turn tracking enabled for {TrackName} via fuzzy match → {DbKey} ({TrackDisplay}) - {TurnCount} turns mapped",
                    trackName, kvp.Key, kvp.Value.DisplayName, kvp.Value.TotalTurns);
                return;
            }
        }
        
        // Try contains match (track name contains a database key or vice versa)
        foreach (var kvp in _database.Tracks)
        {
            if (normalizedKey.Contains(NormalizeTrackKey(kvp.Key)) || NormalizeTrackKey(kvp.Key).Contains(normalizedKey))
            {
                _currentTrackData = kvp.Value;
                _logger.LogInformation("Turn tracking enabled for {TrackName} via partial match → {DbKey} ({TrackDisplay}) - {TurnCount} turns mapped",
                    trackName, kvp.Key, kvp.Value.DisplayName, kvp.Value.TotalTurns);
                return;
            }
        }
        
        _currentTrackData = null;
        _logger.LogInformation("Track '{TrackName}' (normalized: '{NormalizedKey}') not found in turn database. Turn tracking disabled for this track.", trackName, normalizedKey);
    }
    
    /// <summary>Normalize a track key by removing spaces, hyphens, underscores.</summary>
    private static string NormalizeTrackKey(string key)
        => key.Replace(" ", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
    
    /// <summary>
    /// Get current turn information based on LapDistPct
    /// </summary>
    /// <param name="lapDistPct">Current lap distance percentage (0.0 - 1.0)</param>
    /// <returns>TurnInfo with turn number, name, and progress</returns>
    public TurnInfo GetCurrentTurn(float lapDistPct)
    {
        // No track data available
        if (_currentTrackData == null || _currentTrackData.Turns.Count == 0)
            return TurnInfo.None;
        
        // Check each turn definition to see if we're in range
        foreach (var turn in _currentTrackData.Turns)
        {
            // Handle wrap-around at start/finish line (e.g., startPct=0.98, endPct=0.02)
            bool inTurn = turn.StartPct <= turn.EndPct
                ? lapDistPct >= turn.StartPct && lapDistPct <= turn.EndPct
                : lapDistPct >= turn.StartPct || lapDistPct <= turn.EndPct; // Wrap-around case
            
            if (inTurn)
            {
                // Calculate progress through turn
                float turnRange = turn.EndPct - turn.StartPct;
                if (turnRange < 0) turnRange += 1.0f; // Handle wrap-around
                
                float progressInTurn = lapDistPct - turn.StartPct;
                if (progressInTurn < 0) progressInTurn += 1.0f; // Handle wrap-around
                
                float progress = turnRange > 0 ? progressInTurn / turnRange : 0f;
                progress = Math.Clamp(progress, 0f, 1f);
                
                return new TurnInfo
                {
                    Number = turn.Number,
                    Name = turn.Name,
                    IsInTurn = true,
                    TurnProgress = progress,
                    TurnType = turn.Type
                };
            }
        }
        
        // Not in any turn - on a straight
        return TurnInfo.None;
    }
    
    /// <summary>
    /// Get the next upcoming turn based on current LapDistPct
    /// </summary>
    /// <param name="lapDistPct">Current lap distance percentage (0.0 - 1.0)</param>
    /// <returns>Next turn info, or None if no track data</returns>
    public TurnInfo GetNextTurn(float lapDistPct)
    {
        if (_currentTrackData == null || _currentTrackData.Turns.Count == 0)
            return TurnInfo.None;
        
        // Find the next turn ahead of current position
        TurnDefinition? nextTurn = null;
        float minDistance = float.MaxValue;
        
        foreach (var turn in _currentTrackData.Turns)
        {
            float distance = turn.StartPct - lapDistPct;
            if (distance < 0) distance += 1.0f; // Wrap around track
            
            if (distance < minDistance)
            {
                minDistance = distance;
                nextTurn = turn;
            }
        }
        
        if (nextTurn == null)
            return TurnInfo.None;
        
        return new TurnInfo
        {
            Number = nextTurn.Number,
            Name = nextTurn.Name,
            IsInTurn = false,
            TurnProgress = 0f,
            TurnType = nextTurn.Type
        };
    }
    
    /// <summary>
    /// Get the last completed turn based on current LapDistPct
    /// (the turn immediately behind current position)
    /// </summary>
    /// <param name="lapDistPct">Current lap distance percentage (0.0 - 1.0)</param>
    /// <returns>Last turn info, or None if no track data</returns>
    public TurnInfo GetLastTurn(float lapDistPct)
    {
        if (_currentTrackData == null || _currentTrackData.Turns.Count == 0)
            return TurnInfo.None;
        
        // Find the last turn behind current position
        TurnDefinition? lastTurn = null;
        float minDistanceBehind = float.MaxValue;
        
        foreach (var turn in _currentTrackData.Turns)
        {
            float distance = lapDistPct - turn.EndPct;
            if (distance < 0) distance += 1.0f; // Wrap around track
            
            if (distance < minDistanceBehind)
            {
                minDistanceBehind = distance;
                lastTurn = turn;
            }
        }
        
        if (lastTurn == null)
            return TurnInfo.None;
        
        return new TurnInfo
        {
            Number = lastTurn.Number,
            Name = lastTurn.Name,
            IsInTurn = false,
            TurnProgress = 1f, // Completed
            TurnType = lastTurn.Type
        };
    }
    
    /// <summary>
    /// Check if track is supported in the database
    /// </summary>
    public bool IsTrackSupported(string trackName)
    {
        if (string.IsNullOrEmpty(trackName))
            return false;
        
        return _database.Tracks.ContainsKey(trackName.ToLowerInvariant());
    }
    
    /// <summary>
    /// Get list of all supported track names
    /// </summary>
    public IReadOnlyList<string> GetSupportedTracks()
    {
        return _database.Tracks.Keys.ToList();
    }
    
    /// <summary>
    /// Get track data for a specific track (for UI/debugging)
    /// </summary>
    public TrackTurnData? GetTrackData(string trackName)
    {
        if (string.IsNullOrEmpty(trackName))
            return null;
        
        _database.Tracks.TryGetValue(trackName.ToLowerInvariant(), out var trackData);
        return trackData;
    }
}
