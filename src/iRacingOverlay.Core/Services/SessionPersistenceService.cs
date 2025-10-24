using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Persists session statistics (fuel averages, pit stop times) between sessions
/// Allows intelligent fuel strategy predictions even on first lap of new session
/// Data stored per track/car combination for maximum accuracy
/// </summary>
public class SessionPersistenceService
{
    private readonly string _dataDirectory;
    private readonly Dictionary<string, SessionStatistics> _cache = new();
    
    public SessionPersistenceService()
    {
        // Store data in Documents/MRT-UI/SessionData/
        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MRT-UI",
            "SessionData"
        );
        
        Directory.CreateDirectory(_dataDirectory);
    }
    
    /// <summary>
    /// Get session statistics for a track/car combination
    /// Returns cached data if available, otherwise loads from disk
    /// </summary>
    public SessionStatistics? GetStatistics(string trackName, int carClassId)
    {
        string key = GetCacheKey(trackName, carClassId);
        
        // Check cache first
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        
        // Load from disk
        string filePath = GetFilePath(trackName, carClassId);
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var stats = JsonSerializer.Deserialize<SessionStatistics>(json);
                if (stats != null)
                {
                    _cache[key] = stats;
                    return stats;
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to load session statistics from {filePath}: {ex.Message}");
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Save session statistics for a track/car combination
    /// Updates cache and persists to disk
    /// </summary>
    public void SaveStatistics(SessionStatistics stats)
    {
        if (string.IsNullOrEmpty(stats.TrackName) || stats.CarClassId <= 0)
        {
            LogError("Cannot save statistics: invalid track name or car class ID");
            return;
        }
        
        string key = GetCacheKey(stats.TrackName, stats.CarClassId);
        string filePath = GetFilePath(stats.TrackName, stats.CarClassId);
        
        try
        {
            stats.LastUpdated = DateTime.UtcNow;
            
            // Update cache
            _cache[key] = stats;
            
            // Persist to disk (pretty-printed JSON for debugging)
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(stats, options);
            File.WriteAllText(filePath, json);
            
            LogDebug($"Saved session statistics: {stats.TrackName} / Class {stats.CarClassId} ({stats.LapsSampled} laps, {stats.PitStopsRecorded} pit stops)");
        }
        catch (Exception ex)
        {
            LogError($"Failed to save session statistics to {filePath}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Update session statistics with new pit stop data
    /// Calculates rolling averages for pit entry, service, and exit times
    /// Also tracks environmental conditions and session type breakdown
    /// </summary>
    public void UpdatePitStopStatistics(SessionStatistics stats, PitStopData pitStop)
    {
        if (!pitStop.IsComplete)
        {
            LogDebug("Skipping incomplete pit stop for statistics update");
            return;
        }
        
        // Calculate new averages (weighted average with existing data)
        int totalStops = stats.PitStopsRecorded + 1;
        
        stats.AveragePitEntryTime = ((stats.AveragePitEntryTime * stats.PitStopsRecorded) + pitStop.PitEntryDuration) / totalStops;
        stats.AverageRefuelServiceTime = ((stats.AverageRefuelServiceTime * stats.PitStopsRecorded) + pitStop.ServiceDuration) / totalStops;
        stats.AveragePitExitTime = ((stats.AveragePitExitTime * stats.PitStopsRecorded) + pitStop.PitExitDuration) / totalStops;
        stats.AverageTotalPitTime = ((stats.AverageTotalPitTime * stats.PitStopsRecorded) + pitStop.ActivePitStopTime) / totalStops;
        
        stats.PitStopsRecorded = totalStops;
        
        // Update environmental condition ranges
        if (stats.PitStopsRecorded == 1)
        {
            // First pit stop - initialize ranges
            stats.MinTrackTemp = stats.MaxTrackTemp = stats.AvgTrackTemp = pitStop.TrackTemp;
            stats.MinAirTemp = stats.MaxAirTemp = stats.AvgAirTemp = pitStop.AirTemp;
        }
        else
        {
            // Update ranges
            stats.MinTrackTemp = Math.Min(stats.MinTrackTemp, pitStop.TrackTemp);
            stats.MaxTrackTemp = Math.Max(stats.MaxTrackTemp, pitStop.TrackTemp);
            stats.AvgTrackTemp = ((stats.AvgTrackTemp * (totalStops - 1)) + pitStop.TrackTemp) / totalStops;
            
            stats.MinAirTemp = Math.Min(stats.MinAirTemp, pitStop.AirTemp);
            stats.MaxAirTemp = Math.Max(stats.MaxAirTemp, pitStop.AirTemp);
            stats.AvgAirTemp = ((stats.AvgAirTemp * (totalStops - 1)) + pitStop.AirTemp) / totalStops;
        }
        
        // Track session type breakdown
        switch (pitStop.SessionType.ToLowerInvariant())
        {
            case "practice":
                stats.PracticeStops++;
                break;
            case "qualify":
            case "qualifying":
            case "lone qualify":
            case "open qualify":
                stats.QualifyingStops++;
                break;
            case "warmup":
            case "warm up":
                stats.WarmupStops++;
                break;
            case "race":
                stats.RaceStops++;
                break;
        }
        
        LogDebug($"Updated pit stop statistics: Entry={stats.AveragePitEntryTime:F1}s, Service={stats.AverageRefuelServiceTime:F1}s, Exit={stats.AveragePitExitTime:F1}s, Total={stats.AverageTotalPitTime:F1}s");
        LogDebug($"  Environmental: Track={pitStop.TrackTemp:F1}°C (range {stats.MinTrackTemp:F1}-{stats.MaxTrackTemp:F1}°C), Air={pitStop.AirTemp:F1}°C");
        LogDebug($"  Session types: Practice={stats.PracticeStops}, Qualifying={stats.QualifyingStops}, Warmup={stats.WarmupStops}, Race={stats.RaceStops}");
    }
    
    /// <summary>
    /// Update session statistics with new fuel consumption data
    /// Calculates rolling average for fuel per lap
    /// </summary>
    public void UpdateFuelStatistics(SessionStatistics stats, float sessionAverage, int lapCount)
    {
        stats.SessionAverageFuelPerLap = sessionAverage;
        stats.LapsSampled = lapCount;
        
        LogDebug($"Updated fuel statistics: {sessionAverage:F3}L/lap (from {lapCount} laps)");
    }
    
    /// <summary>
    /// Clear all cached statistics (call when changing tracks/cars)
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }
    
    /// <summary>
    /// Get file path for track/car combination
    /// </summary>
    private string GetFilePath(string trackName, int carClassId)
    {
        // Sanitize track name for file system
        string safeTrackName = string.Join("_", trackName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_dataDirectory, $"{safeTrackName}_Class{carClassId}.json");
    }
    
    /// <summary>
    /// Get cache key for track/car combination
    /// </summary>
    private string GetCacheKey(string trackName, int carClassId)
    {
        return $"{trackName}_{carClassId}";
    }
    
    /// <summary>
    /// Write debug message to log file
    /// </summary>
    private void LogDebug(string message)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MRT-UI",
                "fuel_debug.log"
            );
            var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] [SessionPersistence] {message}\n";
            File.AppendAllText(logPath, logMessage);
        }
        catch
        {
            // Ignore logging errors
        }
    }
    
    /// <summary>
    /// Write error message to log file
    /// </summary>
    private void LogError(string message)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MRT-UI",
                "fuel_debug.log"
            );
            var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] [SessionPersistence ERROR] {message}\n";
            File.AppendAllText(logPath, logMessage);
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
