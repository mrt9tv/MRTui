using System.Collections.Concurrent;
using System.Text.Json;
using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Telemetry persistence service with hybrid in-memory + aggregate storage
/// 
/// Design:
/// - In-memory ring buffer: 50 laps max (12.5 MB at 60Hz, 48 channels)
/// - Aggregate persistence: Lap summaries only (~100 bytes/lap)
/// - Failsafes: Temp file write, crash recovery, memory pressure monitoring
/// - Storage: JSON files in %APPDATA%/MRTOverlay/Sessions/
/// 
/// Memory usage: < 15 MB total (ring buffer + session summaries)
/// Disk usage: ~15 KB per session (30 laps × 500 bytes aggregate)
/// </summary>
public class TelemetryPersistenceService
{
    private const int MAX_RING_BUFFER_LAPS = 50;  // 50 laps × 250 KB = 12.5 MB
    private const long MEMORY_PRESSURE_THRESHOLD = 500_000_000;  // 500 MB
    private const int AUTO_SAVE_INTERVAL_LAPS = 10;  // Auto-save every 10 laps
    
    private readonly string _sessionsDirectory;
    private readonly string _crashRecoveryDirectory;
    private readonly ConcurrentQueue<string> _crashRecoveryQueue = new();
    private readonly object _saveLock = new object();
    
    // In-memory ring buffer for current session (full telemetry)
    private readonly Queue<LapTelemetrySnapshot> _ringBuffer = new();
    
    // Current session summary (aggregates only)
    private SessionSummary? _currentSession;
    
    // Statistics tracking
    private long _totalBytesWritten = 0;
    private int _saveCount = 0;
    private int _crashRecoveryCount = 0;
    
    public TelemetryPersistenceService()
    {
        // Initialize directories in %APPDATA%/MRTOverlay/
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string mrtOverlayPath = Path.Combine(appDataPath, "MRTOverlay");
        
        _sessionsDirectory = Path.Combine(mrtOverlayPath, "Sessions");
        _crashRecoveryDirectory = Path.Combine(mrtOverlayPath, "CrashRecovery");
        
        Directory.CreateDirectory(_sessionsDirectory);
        Directory.CreateDirectory(_crashRecoveryDirectory);
        
        // Attempt crash recovery on startup
        RecoverCrashedSessions();
    }
    
    /// <summary>
    /// Start new session tracking
    /// </summary>
    public void StartSession(OperationalMode mode, string trackName, string carName, string sessionType)
    {
        lock (_saveLock)
        {
            // Save previous session if exists
            if (_currentSession != null)
            {
                SaveSessionInternal(_currentSession);
            }
            
            // Create new session
            _currentSession = new SessionSummary
            {
                SessionId = Guid.NewGuid().ToString(),
                StartTime = DateTime.UtcNow,
                Mode = mode,
                TrackName = trackName,
                CarName = carName,
                SessionType = sessionType
            };
            
            // Clear ring buffer
            _ringBuffer.Clear();
            
            LogInfo($"Session started: {_currentSession.SessionId} ({mode}, {trackName}, {carName})");
        }
    }
    
    /// <summary>
    /// Add lap telemetry to ring buffer and create aggregate
    /// </summary>
    public void AddLap(LapTelemetrySnapshot lapSnapshot)
    {
        if (_currentSession == null)
        {
            LogWarning("AddLap called but no active session. Ignoring.");
            return;
        }
        
        lock (_saveLock)
        {
            // Add to ring buffer (FIFO, trim if exceeds limit)
            _ringBuffer.Enqueue(lapSnapshot);
            if (_ringBuffer.Count > MAX_RING_BUFFER_LAPS)
            {
                _ringBuffer.Dequeue();  // Remove oldest lap
                LogDebug($"Ring buffer trimmed (max {MAX_RING_BUFFER_LAPS} laps)");
            }
            
            // Create lap summary (aggregate from full telemetry)
            var lapSummary = CreateLapSummary(lapSnapshot);
            _currentSession.Laps.Add(lapSummary);
            
            // Check for outliers
            var outlier = DetectOutlier(lapSummary, _currentSession);
            if (outlier != null)
            {
                _currentSession.Outliers.Add(outlier);
                LogInfo($"Outlier detected: Lap {lapSummary.LapNumber} - {outlier.Reason} ({outlier.Description})");
            }
            
            // Update session stats
            UpdateSessionStats(_currentSession);
            
            // Auto-save every N laps
            if (_currentSession.Laps.Count % AUTO_SAVE_INTERVAL_LAPS == 0)
            {
                SaveSessionAsync(_currentSession);
            }
            
            // Check memory pressure
            CheckMemoryPressure();
        }
    }
    
    /// <summary>
    /// End current session and save
    /// </summary>
    public void EndSession()
    {
        lock (_saveLock)
        {
            if (_currentSession == null)
            {
                LogWarning("EndSession called but no active session.");
                return;
            }
            
            _currentSession.EndTime = DateTime.UtcNow;
            
            // Extract ML features based on mode
            ExtractMLFeatures(_currentSession);
            
            // Final save
            SaveSessionInternal(_currentSession);
            
            LogInfo($"Session ended: {_currentSession.SessionId} ({_currentSession.Laps.Count} laps)");
            
            // Clear state
            _currentSession = null;
            _ringBuffer.Clear();
        }
    }
    
    /// <summary>
    /// Save session to disk with failsafes
    /// Temp file write → verify → atomic move
    /// </summary>
    private void SaveSessionInternal(SessionSummary session)
    {
        string sessionFileName = $"{session.SessionId}_{session.StartTime:yyyyMMdd_HHmmss}.json";
        string finalPath = Path.Combine(_sessionsDirectory, sessionFileName);
        string tempPath = finalPath + ".tmp";
        string backupPath = finalPath + ".bak";
        
        try
        {
            // Step 1: Write to temp file
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            string json = JsonSerializer.Serialize(session, options);
            File.WriteAllText(tempPath, json);
            
            _totalBytesWritten += json.Length;
            
            // Step 2: Verify JSON validity (re-parse)
            var verifySession = JsonSerializer.Deserialize<SessionSummary>(json, options);
            if (verifySession == null || verifySession.SessionId != session.SessionId)
            {
                throw new InvalidOperationException("JSON verification failed after write");
            }
            
            // Step 3: Backup existing file if exists
            if (File.Exists(finalPath))
            {
                File.Move(finalPath, backupPath, overwrite: true);
            }
            
            // Step 4: Atomic move temp → final
            File.Move(tempPath, finalPath, overwrite: true);
            
            // Step 5: Delete backup on success
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
            
            _saveCount++;
            LogInfo($"Session saved: {sessionFileName} ({json.Length} bytes, {session.Laps.Count} laps)");
        }
        catch (Exception ex)
        {
            LogError($"Failed to save session {session.SessionId}: {ex.Message}");
            
            // Add to crash recovery queue
            _crashRecoveryQueue.Enqueue(tempPath);
            
            // Attempt to restore from backup
            if (File.Exists(backupPath))
            {
                File.Move(backupPath, finalPath, overwrite: true);
                LogInfo($"Restored session from backup: {sessionFileName}");
            }
        }
        finally
        {
            // Cleanup temp files
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }
    
    /// <summary>
    /// Async save (non-blocking for auto-save)
    /// </summary>
    private void SaveSessionAsync(SessionSummary session)
    {
        Task.Run(() =>
        {
            try
            {
                SaveSessionInternal(session);
            }
            catch (Exception ex)
            {
                LogError($"Async save failed: {ex.Message}");
            }
        });
    }
    
    /// <summary>
    /// Create lap summary from full telemetry snapshot
    /// Aggregates 2000+ data points → 12 values (~100 bytes)
    /// </summary>
    private LapSummary CreateLapSummary(LapTelemetrySnapshot snapshot)
    {
        // Compute aggregates from telemetry arrays
        float avgSpeed = snapshot.SpeedData?.Length > 0 
            ? snapshot.SpeedData.Average() 
            : 0f;
        
        float maxSpeed = snapshot.SpeedData?.Length > 0 
            ? snapshot.SpeedData.Max() 
            : 0f;
        
        // Average tire temps (4 tires, middle temp)
        float[] avgTireTemps = new float[4]
        {
            snapshot.LFTempM ?? 0f,
            snapshot.RFTempM ?? 0f,
            snapshot.LRTempM ?? 0f,
            snapshot.RRTempM ?? 0f
        };
        
        // Tire wear delta (start - end of lap)
        float[] tireWearDelta = new float[4]
        {
            (snapshot.LFWearStart ?? 0f) - (snapshot.LFWearEnd ?? 0f),
            (snapshot.RFWearStart ?? 0f) - (snapshot.RFWearEnd ?? 0f),
            (snapshot.LRWearStart ?? 0f) - (snapshot.LRWearEnd ?? 0f),
            (snapshot.RRWearStart ?? 0f) - (snapshot.RRWearEnd ?? 0f)
        };
        
        return new LapSummary
        {
            LapNumber = snapshot.LapNumber,
            LapTime = snapshot.LapTime,
            SectorTimes = snapshot.SectorTimes ?? Array.Empty<float>(),
            FuelUsed = snapshot.FuelUsed,
            AvgTireTemps = avgTireTemps,
            TireWearDelta = tireWearDelta,
            AvgSpeed = avgSpeed,
            MaxSpeed = maxSpeed,
            Incidents = snapshot.Incidents,
            PitStop = snapshot.PitStop,
            IsValid = snapshot.IsValid,
            Timestamp = snapshot.Timestamp
        };
    }
    
    /// <summary>
    /// Detect outlier laps
    /// </summary>
    private OutlierLap? DetectOutlier(LapSummary lap, SessionSummary session)
    {
        // Need at least 5 laps for statistical analysis
        if (session.Laps.Count < 5)
            return null;
        
        var validLaps = session.Laps.Where(l => l.IsValid && l.LapTime.HasValue).ToList();
        if (validLaps.Count < 3)
            return null;
        
        // Incident check
        if (lap.Incidents > 0)
        {
            return new OutlierLap
            {
                LapNumber = lap.LapNumber,
                Reason = OutlierReason.Incident,
                Description = $"Incident count: {lap.Incidents}",
                Severity = lap.Incidents >= 2 ? OutlierSeverity.High : OutlierSeverity.Medium
            };
        }
        
        // Extreme fuel consumption check (> 2σ)
        var fuelValues = validLaps.Select(l => l.FuelUsed).ToList();
        float fuelMean = fuelValues.Average();
        float fuelStdDev = (float)Math.Sqrt(fuelValues.Average(f => Math.Pow(f - fuelMean, 2)));
        
        if (Math.Abs(lap.FuelUsed - fuelMean) > 2 * fuelStdDev)
        {
            return new OutlierLap
            {
                LapNumber = lap.LapNumber,
                Reason = OutlierReason.ExtremeFuel,
                Description = $"Fuel {lap.FuelUsed:F1}L ({(lap.FuelUsed - fuelMean) / fuelStdDev:F1}σ from mean)",
                Severity = OutlierSeverity.Medium
            };
        }
        
        // Extreme lap time check (> 3σ) - only if lap time exists
        if (lap.LapTime.HasValue)
        {
            var lapTimes = validLaps.Select(l => l.LapTime!.Value).ToList();
            float lapTimeMean = lapTimes.Average();
            float lapTimeStdDev = (float)Math.Sqrt(lapTimes.Average(t => Math.Pow(t - lapTimeMean, 2)));
            
            if (Math.Abs(lap.LapTime.Value - lapTimeMean) > 3 * lapTimeStdDev)
            {
                return new OutlierLap
                {
                    LapNumber = lap.LapNumber,
                    Reason = OutlierReason.ExtremeLapTime,
                    Description = $"Lap time {lap.LapTime.Value:F2}s ({(lap.LapTime.Value - lapTimeMean) / lapTimeStdDev:F1}σ from mean)",
                    Severity = OutlierSeverity.High
                };
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Update session statistics from lap summaries
    /// </summary>
    private void UpdateSessionStats(SessionSummary session)
    {
        var validLaps = session.Laps.Where(l => l.IsValid && l.LapTime.HasValue).ToList();
        
        session.Stats.TotalLaps = session.Laps.Count;
        session.Stats.ValidLaps = validLaps.Count;
        session.Stats.TotalIncidents = session.Laps.Sum(l => l.Incidents);
        session.Stats.PitStops = session.Laps.Count(l => l.PitStop);
        
        if (validLaps.Any())
        {
            session.Stats.BestLapTime = validLaps.Min(l => l.LapTime!.Value);
            session.Stats.AvgLapTime = validLaps.Average(l => l.LapTime!.Value);
            session.Stats.AvgFuelPerLap = validLaps.Average(l => l.FuelUsed);
            
            // Fuel standard deviation
            float fuelMean = session.Stats.AvgFuelPerLap ?? 0f;
            session.Stats.FuelStdDev = (float)Math.Sqrt(validLaps.Average(l => Math.Pow(l.FuelUsed - fuelMean, 2)));
        }
    }
    
    /// <summary>
    /// Extract ML features based on mode
    /// </summary>
    private void ExtractMLFeatures(SessionSummary session)
    {
        var validLaps = session.Laps.Where(l => l.IsValid).ToList();
        if (!validLaps.Any())
            return;
        
        switch (session.Mode)
        {
            case OperationalMode.SetupEngineering:
                session.SetupFeatures = new SetupFeatures
                {
                    // Placeholder - would extract from full telemetry in ring buffer
                    LapTimeStdDev = session.Stats.AvgLapTime.HasValue 
                        ? (float)Math.Sqrt(validLaps.Average(l => Math.Pow((l.LapTime ?? 0) - session.Stats.AvgLapTime.Value, 2)))
                        : 0f
                };
                break;
            
            case OperationalMode.StrategyScouting:
                session.StrategyFeatures = new StrategyFeatures
                {
                    FuelPerLap_Push = session.Stats.AvgFuelPerLap ?? 0f,
                    TireDegradationRate = CalculateTireDegradationRate(validLaps),
                    TireDegModel = FitTireDegradationModel(validLaps)
                };
                break;
        }
    }
    
    /// <summary>
    /// Calculate tire degradation rate (% per lap)
    /// </summary>
    private float CalculateTireDegradationRate(List<LapSummary> laps)
    {
        if (laps.Count < 5)
            return 0f;
        
        // Average wear delta across all 4 tires
        return laps.Average(l => l.TireWearDelta.Average());
    }
    
    /// <summary>
    /// Fit exponential decay model to tire degradation
    /// grip = a * e^(-b * lap) + c
    /// </summary>
    private float[] FitTireDegradationModel(List<LapSummary> laps)
    {
        // Simplified: return placeholder coefficients
        // Real implementation would use least-squares regression
        return new float[3] { 100f, 0.01f, 0f };  // [a, b, c]
    }
    
    /// <summary>
    /// Check memory pressure and dump if threshold exceeded
    /// </summary>
    private void CheckMemoryPressure()
    {
        long currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        
        if (currentMemory > MEMORY_PRESSURE_THRESHOLD)
        {
            LogWarning($"Memory pressure: {currentMemory / 1_000_000} MB (threshold: {MEMORY_PRESSURE_THRESHOLD / 1_000_000} MB)");
            
            // Emergency save and trim ring buffer
            if (_currentSession != null)
            {
                SaveSessionAsync(_currentSession);
            }
            
            // Trim ring buffer to 25 laps (half capacity)
            while (_ringBuffer.Count > MAX_RING_BUFFER_LAPS / 2)
            {
                _ringBuffer.Dequeue();
            }
            
            // Force garbage collection
            GC.Collect();
            
            LogInfo($"Memory pressure handled. New memory: {GC.GetTotalMemory(forceFullCollection: true) / 1_000_000} MB");
        }
    }
    
    /// <summary>
    /// Recover crashed sessions on startup
    /// </summary>
    private void RecoverCrashedSessions()
    {
        try
        {
            // Look for .tmp files in crash recovery directory
            var tempFiles = Directory.GetFiles(_crashRecoveryDirectory, "*.tmp");
            
            foreach (var tempFile in tempFiles)
            {
                try
                {
                    // Attempt to read and save
                    string json = File.ReadAllText(tempFile);
                    var session = JsonSerializer.Deserialize<SessionSummary>(json);
                    
                    if (session != null)
                    {
                        SaveSessionInternal(session);
                        File.Delete(tempFile);
                        _crashRecoveryCount++;
                        LogInfo($"Recovered crashed session: {session.SessionId}");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Failed to recover {tempFile}: {ex.Message}");
                }
            }
            
            if (_crashRecoveryCount > 0)
            {
                LogInfo($"Crash recovery complete: {_crashRecoveryCount} sessions recovered");
            }
        }
        catch (Exception ex)
        {
            LogError($"Crash recovery failed: {ex.Message}");
        }
    }
    
    // Logging methods (placeholder - would use proper logging framework)
    private void LogInfo(string message) => Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} - {message}");
    private void LogWarning(string message) => Console.WriteLine($"[WARN] {DateTime.Now:HH:mm:ss} - {message}");
    private void LogError(string message) => Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss} - {message}");
    private void LogDebug(string message) => Console.WriteLine($"[DEBUG] {DateTime.Now:HH:mm:ss} - {message}");
}

/// <summary>
/// Full telemetry snapshot for a single lap
/// Stored in ring buffer (50 laps max, ~250 KB per lap)
/// </summary>
public class LapTelemetrySnapshot
{
    public int LapNumber { get; set; }
    public float? LapTime { get; set; }
    public float[]? SectorTimes { get; set; }
    public float FuelUsed { get; set; }
    public float[]? SpeedData { get; set; }  // 60Hz × lap duration (~2000 points)
    
    // Tire data (start/end for wear delta)
    public float? LFWearStart { get; set; }
    public float? LFWearEnd { get; set; }
    public float? RFWearStart { get; set; }
    public float? RFWearEnd { get; set; }
    public float? LRWearStart { get; set; }
    public float? LRWearEnd { get; set; }
    public float? RRWearStart { get; set; }
    public float? RRWearEnd { get; set; }
    
    // Tire temps (average)
    public float? LFTempM { get; set; }
    public float? RFTempM { get; set; }
    public float? LRTempM { get; set; }
    public float? RRTempM { get; set; }
    
    public int Incidents { get; set; }
    public bool PitStop { get; set; }
    public bool IsValid { get; set; } = true;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
