using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Mode-aware telemetry collection engine
/// Manages telemetry recording with mode-specific sampling rates and channel filtering
/// 
/// Design:
/// - Setup Engineering: 60Hz, 48 channels, full history recording
/// - Strategy Scouting: 10Hz, 25 channels, aggregate-only recording
/// - Driving Mode: 60Hz, essential channels, current session only
/// 
/// Integrates with:
/// - ModeController: Gets current mode and telemetry profile
/// - IRacingTelemetryService: Subscribes to telemetry updates
/// - TelemetryPersistenceService: Saves collected data
/// </summary>
public class TelemetryCollectionEngine
{
    private readonly ModeController _modeController;
    private readonly TelemetryPersistenceService _persistenceService;
    private readonly SectorTelemetryAnalyzer _sectorAnalyzer;
    private readonly object _collectionLock = new object();
    
    // Current lap telemetry buffer (60Hz samples)
    private readonly List<TelemetrySnapshot> _currentLapBuffer = new();
    
    // Lap tracking state
    private int _lastLapNumber = 0;
    private float _lastLapDistPct = 0f;
    private bool _isCollecting = false;
    
    // Sampling rate control (mode-specific)
    private int _sampleRateHz = 60;
    private DateTime _lastSampleTime = DateTime.MinValue;
    private readonly TimeSpan _minSampleInterval;
    
    // Statistics
    private int _samplesCollected = 0;
    private int _lapsRecorded = 0;
    private long _totalBytesProcessed = 0;
    
    public TelemetryCollectionEngine(
        ModeController modeController,
        TelemetryPersistenceService persistenceService,
        SectorTelemetryAnalyzer sectorAnalyzer)
    {
        _modeController = modeController;
        _persistenceService = persistenceService;
        _sectorAnalyzer = sectorAnalyzer;
        
        // Default to 60Hz (16.67ms interval)
        _minSampleInterval = TimeSpan.FromMilliseconds(1000.0 / _sampleRateHz);
        
        // Subscribe to mode changes
        _modeController.ModeChanged += OnModeChanged;
        
        // Update sampling rate based on current mode
        UpdateSamplingRate(_modeController.CurrentMode);
    }
    
    /// <summary>
    /// Start telemetry collection for new session
    /// </summary>
    public void StartCollection(string trackName, string carName, string sessionType)
    {
        lock (_collectionLock)
        {
            if (_isCollecting)
            {
                LogWarning("Collection already active. Stopping previous session.");
                StopCollection();
            }
            
            // Start persistence session
            _persistenceService.StartSession(
                _modeController.CurrentMode,
                trackName,
                carName,
                sessionType
            );
            
            // Reset state
            _currentLapBuffer.Clear();
            _lastLapNumber = 0;
            _lastLapDistPct = 0f;
            _samplesCollected = 0;
            _lapsRecorded = 0;
            _totalBytesProcessed = 0;
            
            _isCollecting = true;
            
            LogInfo($"Collection started: {_modeController.CurrentMode}, {trackName}, {carName}, {_sampleRateHz}Hz");
        }
    }
    
    /// <summary>
    /// Stop telemetry collection and finalize session
    /// </summary>
    public void StopCollection()
    {
        lock (_collectionLock)
        {
            if (!_isCollecting)
            {
                return;
            }
            
            // Save final lap if exists
            if (_currentLapBuffer.Count > 0)
            {
                ProcessCompletedLap(_lastLapNumber);
            }
            
            // End persistence session
            _persistenceService.EndSession();
            
            _isCollecting = false;
            
            LogInfo($"Collection stopped: {_lapsRecorded} laps, {_samplesCollected} samples, {_totalBytesProcessed / 1_000_000.0:F2} MB");
        }
    }
    
    /// <summary>
    /// Process incoming telemetry update
    /// Called by IRacingTelemetryService every frame (~60Hz)
    /// </summary>
    public void OnTelemetryUpdate(TelemetryData telemetry)
    {
        lock (_collectionLock)
        {
            if (!_isCollecting)
            {
                return;
            }
            
            // Check sampling rate throttle
            var now = DateTime.UtcNow;
            if ((now - _lastSampleTime) < _minSampleInterval)
            {
                return;  // Skip this sample (throttle to target Hz)
            }
            _lastSampleTime = now;
            
            // Detect lap boundary (lap number incremented or crossed finish line)
            int currentLap = telemetry.Lap;
            float currentLapDistPct = telemetry.LapDistPct;
            
            bool lapCompleted = false;
            if (currentLap > _lastLapNumber)
            {
                // Lap number incremented
                lapCompleted = true;
            }
            else if (currentLap == _lastLapNumber && currentLapDistPct < 0.1f && _lastLapDistPct > 0.9f)
            {
                // Crossed finish line (wrap around from ~1.0 to ~0.0)
                lapCompleted = true;
            }
            
            if (lapCompleted && _lastLapNumber > 0)
            {
                // Process completed lap
                ProcessCompletedLap(_lastLapNumber);
                _currentLapBuffer.Clear();
            }
            
            // Update lap tracking state
            _lastLapNumber = currentLap;
            _lastLapDistPct = currentLapDistPct;
            
            // Collect telemetry sample based on mode
            var snapshot = CreateSnapshot(telemetry);
            _currentLapBuffer.Add(snapshot);
            _samplesCollected++;
            _totalBytesProcessed += EstimateSnapshotSize(snapshot);
        }
    }
    
    /// <summary>
    /// Create telemetry snapshot from TelemetryData
    /// Channel selection based on current mode
    /// </summary>
    private TelemetrySnapshot CreateSnapshot(TelemetryData telemetry)
    {
        var mode = _modeController.CurrentMode;
        var profile = _modeController.GetCollectionProfile(mode);
        
        // Base snapshot (common to all modes)
        var snapshot = new TelemetrySnapshot
        {
            Timestamp = DateTime.UtcNow,
            LapNumber = telemetry.Lap,
            LapDistPct = telemetry.LapDistPct,
            SessionTimeRemain = (float)telemetry.SessionTimeRemain,
            Speed = telemetry.Speed,
            Throttle = telemetry.Throttle
        };
        
        // Mode-specific channel collection
        if (profile.RequiredChannels == null)
        {
            // Driving mode: Essential channels only (existing behavior)
            snapshot.FuelLevel = telemetry.FuelLevel;
            snapshot.RPM = telemetry.RPM;
            snapshot.Gear = telemetry.Gear;
        }
        else if (profile.RequiredChannels == TelemetryChannels.SetupEngineering.All)
        {
            // Setup Engineering: Full telemetry (48 channels)
            CollectSetupEngineeringChannels(snapshot, telemetry);
        }
        else if (profile.RequiredChannels == TelemetryChannels.StrategyScouting.All)
        {
            // Strategy Scouting: Strategic channels (25 channels)
            CollectStrategyScoutingChannels(snapshot, telemetry);
        }
        
        return snapshot;
    }
    
    /// <summary>
    /// Collect Setup Engineering telemetry (48 channels, 60Hz)
    /// </summary>
    private void CollectSetupEngineeringChannels(TelemetrySnapshot snapshot, TelemetryData telemetry)
    {
        // Suspension (16 channels)
        snapshot.LFshockDefl = telemetry.LFshockDefl;
        snapshot.RFshockDefl = telemetry.RFshockDefl;
        snapshot.LRshockDefl = telemetry.LRshockDefl;
        snapshot.RRshockDefl = telemetry.RRshockDefl;
        
        snapshot.LFshockVel = telemetry.LFshockVel;
        snapshot.RFshockVel = telemetry.RFshockVel;
        snapshot.LRshockVel = telemetry.LRshockVel;
        snapshot.RRshockVel = telemetry.RRshockVel;
        
        // Ride height not currently available in TelemetryData
        // snapshot.LFrideHeight = telemetry.LFrideHeight;
        // snapshot.RFrideHeight = telemetry.RFrideHeight;
        // snapshot.LRrideHeight = telemetry.LRrideHeight;
        // snapshot.RRrideHeight = telemetry.RRrideHeight;
        
        snapshot.Roll = telemetry.Roll;
        snapshot.RollRate = telemetry.RollRate;
        snapshot.Pitch = telemetry.Pitch;
        snapshot.PitchRate = telemetry.PitchRate;
        
        // Tires (24 channels)
        snapshot.LFwearL = telemetry.LFwearL;
        snapshot.LFwearM = telemetry.LFwearM;
        snapshot.LFwearR = telemetry.LFwearR;
        snapshot.RFwearL = telemetry.RFwearL;
        snapshot.RFwearM = telemetry.RFwearM;
        snapshot.RFwearR = telemetry.RFwearR;
        snapshot.LRwearL = telemetry.LRwearL;
        snapshot.LRwearM = telemetry.LRwearM;
        snapshot.LRwearR = telemetry.LRwearR;
        snapshot.RRwearL = telemetry.RRwearL;
        snapshot.RRwearM = telemetry.RRwearM;
        snapshot.RRwearR = telemetry.RRwearR;
        
        snapshot.LFtempCL = telemetry.LFtempCL;
        snapshot.LFtempCM = telemetry.LFtempCM;
        snapshot.LFtempCR = telemetry.LFtempCR;
        snapshot.RFtempCL = telemetry.RFtempCL;
        snapshot.RFtempCM = telemetry.RFtempCM;
        snapshot.RFtempCR = telemetry.RFtempCR;
        snapshot.LRtempCL = telemetry.LRtempCL;
        snapshot.LRtempCM = telemetry.LRtempCM;
        snapshot.LRtempCR = telemetry.LRtempCR;
        snapshot.RRtempCL = telemetry.RRtempCL;
        snapshot.RRtempCM = telemetry.RRtempCM;
        snapshot.RRtempCR = telemetry.RRtempCR;
        
        // Dynamics (8 channels)
        snapshot.LongAccel = telemetry.LongAccel;
        snapshot.LatAccel = telemetry.LatAccel;
        snapshot.VertAccel = telemetry.VertAccel;
        
        snapshot.VelocityX = telemetry.VelocityX;
        snapshot.VelocityY = telemetry.VelocityY;
        snapshot.VelocityZ = telemetry.VelocityZ;
        
        // Speed and Throttle already set in base snapshot
    }
    
    /// <summary>
    /// Collect Strategy Scouting telemetry (25 channels, 10Hz)
    /// </summary>
    private void CollectStrategyScoutingChannels(TelemetrySnapshot snapshot, TelemetryData telemetry)
    {
        // Fuel & Pit (11 channels)
        snapshot.FuelLevel = telemetry.FuelLevel;
        snapshot.FuelLevelPct = telemetry.FuelLevelPct;
        snapshot.FuelUsePerHour = telemetry.FuelUsePerHour;
        
        snapshot.OnPitRoad = telemetry.OnPitRoad;
        // Pit service flags not currently available in TelemetryData
        // snapshot.PitSvFlags = telemetry.PitSvFlags;
        
        // Tire Degradation (8 channels - middle wear/temp points)
        snapshot.LFwearM = telemetry.LFwearM;
        snapshot.RFwearM = telemetry.RFwearM;
        snapshot.LRwearM = telemetry.LRwearM;
        snapshot.RRwearM = telemetry.RRwearM;
        
        snapshot.LFtempCM = telemetry.LFtempCM;
        snapshot.RFtempCM = telemetry.RFtempCM;
        snapshot.LRtempCM = telemetry.LRtempCM;
        snapshot.RRtempCM = telemetry.RRtempCM;
        
        // Timing (6 channels)
        snapshot.LapCurrentLapTime = telemetry.LapCurrentLapTime;
        snapshot.LapLastLapTime = telemetry.LapLastLapTime;
        snapshot.LapBestLapTime = telemetry.LapBestLapTime;
        // SessionTimeRemain already set in base snapshot
    }
    
    /// <summary>
    /// Process completed lap and save to persistence
    /// </summary>
    private void ProcessCompletedLap(int lapNumber)
    {
        if (_currentLapBuffer.Count == 0)
        {
            LogDebug($"Lap {lapNumber} completed but buffer empty. Skipping.");
            return;
        }
        
        // Create lap telemetry snapshot for persistence
        var lapSnapshot = new LapTelemetrySnapshot
        {
            LapNumber = lapNumber,
            LapTime = _currentLapBuffer.LastOrDefault()?.LapLastLapTime,
            FuelUsed = CalculateFuelUsed(_currentLapBuffer),
            SpeedData = _currentLapBuffer.Select(s => s.Speed).ToArray(),
            
            // Tire wear (start vs end of lap)
            LFWearStart = _currentLapBuffer.FirstOrDefault()?.LFwearM,
            LFWearEnd = _currentLapBuffer.LastOrDefault()?.LFwearM,
            RFWearStart = _currentLapBuffer.FirstOrDefault()?.RFwearM,
            RFWearEnd = _currentLapBuffer.LastOrDefault()?.RFwearM,
            LRWearStart = _currentLapBuffer.FirstOrDefault()?.LRwearM,
            LRWearEnd = _currentLapBuffer.LastOrDefault()?.LRwearM,
            RRWearStart = _currentLapBuffer.FirstOrDefault()?.RRwearM,
            RRWearEnd = _currentLapBuffer.LastOrDefault()?.RRwearM,
            
            // Tire temps (average)
            LFTempM = _currentLapBuffer.Average(s => s.LFtempCM ?? 0f),
            RFTempM = _currentLapBuffer.Average(s => s.RFtempCM ?? 0f),
            LRTempM = _currentLapBuffer.Average(s => s.LRtempCM ?? 0f),
            RRTempM = _currentLapBuffer.Average(s => s.RRtempCM ?? 0f),
            
            Incidents = 0,  // TODO: Track incidents from telemetry
            PitStop = _currentLapBuffer.Any(s => s.OnPitRoad),
            IsValid = true,  // TODO: Detect invalid laps (off-track, etc.)
            Timestamp = DateTime.UtcNow
        };
        
        // Add to persistence service
        _persistenceService.AddLap(lapSnapshot);
        _lapsRecorded++;
        
        LogInfo($"Lap {lapNumber} recorded: {_currentLapBuffer.Count} samples, {lapSnapshot.LapTime:F2}s");
        
        // TODO: Analyze sector telemetry (needs TrackSectors parameter)
        // var sectorAnalysis = _sectorAnalyzer.AnalyzeLap(trackName, carName, lapNumber, _currentLapBuffer, trackSectors);
    }
    
    /// <summary>
    /// Calculate fuel used during lap
    /// </summary>
    private float CalculateFuelUsed(List<TelemetrySnapshot> lapBuffer)
    {
        if (lapBuffer.Count < 2)
            return 0f;
        
        float fuelStart = lapBuffer.First().FuelLevel ?? 0f;
        float fuelEnd = lapBuffer.Last().FuelLevel ?? 0f;
        
        return Math.Max(0f, fuelStart - fuelEnd);
    }
    
    /// <summary>
    /// Estimate snapshot size in bytes (for statistics)
    /// </summary>
    private int EstimateSnapshotSize(TelemetrySnapshot snapshot)
    {
        // Rough estimate: 4 bytes per float, 50 fields average
        return 4 * 50;  // ~200 bytes per sample
    }
    
    /// <summary>
    /// Update sampling rate when mode changes
    /// </summary>
    private void UpdateSamplingRate(OperationalMode mode)
    {
        var profile = _modeController.GetCollectionProfile(mode);
        _sampleRateHz = profile.SampleRate;
        var newInterval = TimeSpan.FromMilliseconds(1000.0 / _sampleRateHz);
        
        // Update min sample interval (use reflection to modify readonly field)
        typeof(TelemetryCollectionEngine)
            .GetField(nameof(_minSampleInterval), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(this, newInterval);
        
        LogInfo($"Sampling rate updated: {_sampleRateHz}Hz ({1000.0 / _sampleRateHz:F1}ms interval)");
    }
    
    /// <summary>
    /// Handle mode change events
    /// </summary>
    private void OnModeChanged(object? sender, ModeChangedEventArgs e)
    {
        LogInfo($"Mode changed: {e.PreviousMode} → {e.NewMode}");
        UpdateSamplingRate(e.NewMode);
        
        // Optionally restart collection with new mode
        // (Currently requires manual StartCollection call)
    }
    
    // Logging methods (placeholder - would use proper logging framework)
    private void LogInfo(string message) => Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} TelemetryCollectionEngine - {message}");
    private void LogWarning(string message) => Console.WriteLine($"[WARN] {DateTime.Now:HH:mm:ss} TelemetryCollectionEngine - {message}");
    private void LogDebug(string message) => Console.WriteLine($"[DEBUG] {DateTime.Now:HH:mm:ss} TelemetryCollectionEngine - {message}");
}

/// <summary>
/// Single telemetry sample at a point in time
/// Size: ~200 bytes (50 float fields × 4 bytes)
/// </summary>
public class TelemetrySnapshot
{
    public DateTime Timestamp { get; set; }
    public int LapNumber { get; set; }
    public float LapDistPct { get; set; }
    public float SessionTimeRemain { get; set; }
    
    // Essential (all modes)
    public float Speed { get; set; }
    public float Throttle { get; set; }
    public float? FuelLevel { get; set; }
    public float? RPM { get; set; }
    public int? Gear { get; set; }
    
    // Setup Engineering - Suspension (16 channels)
    public float? LFshockDefl { get; set; }
    public float? RFshockDefl { get; set; }
    public float? LRshockDefl { get; set; }
    public float? RRshockDefl { get; set; }
    public float? LFshockVel { get; set; }
    public float? RFshockVel { get; set; }
    public float? LRshockVel { get; set; }
    public float? RRshockVel { get; set; }
    public float? LFrideHeight { get; set; }
    public float? RFrideHeight { get; set; }
    public float? LRrideHeight { get; set; }
    public float? RRrideHeight { get; set; }
    public float? Roll { get; set; }
    public float? RollRate { get; set; }
    public float? Pitch { get; set; }
    public float? PitchRate { get; set; }
    
    // Setup Engineering - Tires (24 channels)
    public float? LFwearL { get; set; }
    public float? LFwearM { get; set; }
    public float? LFwearR { get; set; }
    public float? RFwearL { get; set; }
    public float? RFwearM { get; set; }
    public float? RFwearR { get; set; }
    public float? LRwearL { get; set; }
    public float? LRwearM { get; set; }
    public float? LRwearR { get; set; }
    public float? RRwearL { get; set; }
    public float? RRwearM { get; set; }
    public float? RRwearR { get; set; }
    public float? LFtempCL { get; set; }
    public float? LFtempCM { get; set; }
    public float? LFtempCR { get; set; }
    public float? RFtempCL { get; set; }
    public float? RFtempCM { get; set; }
    public float? RFtempCR { get; set; }
    public float? LRtempCL { get; set; }
    public float? LRtempCM { get; set; }
    public float? LRtempCR { get; set; }
    public float? RRtempCL { get; set; }
    public float? RRtempCM { get; set; }
    public float? RRtempCR { get; set; }
    
    // Setup Engineering - Dynamics (8 channels)
    public float? LongAccel { get; set; }
    public float? LatAccel { get; set; }
    public float? VertAccel { get; set; }
    public float? VelocityX { get; set; }
    public float? VelocityY { get; set; }
    public float? VelocityZ { get; set; }
    
    // Strategy Scouting - Fuel & Pit (11 channels)
    public float? FuelLevelPct { get; set; }
    public float? FuelUsePerHour { get; set; }
    public bool OnPitRoad { get; set; }
    public uint? PitSvFlags { get; set; }
    
    // Strategy Scouting - Timing (6 channels)
    public float? LapCurrentLapTime { get; set; }
    public float? LapLastLapTime { get; set; }
    public float? LapBestLapTime { get; set; }
}
