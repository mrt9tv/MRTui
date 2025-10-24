using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Fuel calculation and tracking service for race strategy
/// Tracks lap-by-lap fuel consumption, calculates averages, and provides race strategy data
/// </summary>
public class FuelCalculatorService
{
    private readonly List<FuelLapHistory> _lapHistory = new();
    private float _fuelAtLapStart;
    private float _lastFuelLevel;
    private int _lastCompletedLap = -1;
    private bool _isFirstUpdate = true;
    private LapFlagStatus _currentFlagStatus = LapFlagStatus.Green;
    private bool _wasOnPitRoadLastUpdate = false;
    private bool _justLeftPits = false;
    private int _lapsCompletedWhenProcessed = -1; // Prevent duplicate lap processing
    
    // Stint tracking for StintAverage calculation
    private int _stintStartLapNumber = 0;  // Lap number when current stint started (after pit exit)
    
    // EMA (Exponential Moving Average) tracking
    private float _emaValue = 0f;  // Current EMA value
    private bool _emaInitialized = false;  // Whether EMA has been initialized with first lap
    // REMOVED: Fixed EMA_ALPHA constant - now calculated adaptively based on fuel consistency
    
    // Incident tracking for outlier detection
    private int _lastIncidentCount = 0;  // Track incident count to detect new incidents during laps
    
    // Delta tracking for convergence analysis and historical accuracy (Phase 3)
    private readonly List<DeltaHistoryRecord> _deltaHistory = new();
    private const int MAX_DELTA_HISTORY = 50;  // Keep last 50 laps of delta data
    private float _lastDelta = 0f;  // Track previous delta for convergence calculation
    
    // Pit stop tracking state machine
    private readonly SessionPersistenceService _persistenceService;
    private PitStopData? _currentPitStop = null;
    private SessionStatistics? _sessionStats = null;
    private string _currentTrackName = "";
    private int _currentCarClassId = -1;
    private bool _sessionStatsLoaded = false;
    
    // Pit stop state tracking
    private enum PitStopState
    {
        NotOnPitRoad,
        Entering,       // Crossed pit entry line, heading to pit box
        InPitBox,       // Stopped in pit box, service starting
        Servicing,      // Refueling/tires
        Departing,      // Service complete, leaving pit box
        Exiting         // Leaving pit lane
    }
    private PitStopState _pitState = PitStopState.NotOnPitRoad;
    
    // Sputtering threshold tracking (Enhanced Phase 2.1)
    private bool _baselineFuelPressureEstablished = false;
    private readonly List<float> _fuelPressureHistory = new();  // Track pressure for baseline calculation
    private const int BASELINE_LAPS_NEEDED = 3;  // Laps needed to establish baseline pressure
    
    // Dynamic buffer configuration
    private float _bufferLaps = 1.0f;  // User-configured base buffer
    private bool _enableDynamicBuffer = true;  // Whether dynamic buffer is enabled
    
    // Pit lap accuracy fix (Phase 1: FUEL_PIT_LAP_AND_FLAGS_FIX.md)
    private float _lastLapDistPct = 0f;  // Track lap distance percentage from previous update
    private int _virtualLapsCompleted = 0;  // Lap counter relative to pit entry (not start/finish)

    // ENHANCEMENT: Dynamic pit entry detection (learns from first pit stop instead of hardcoded value)
    private float? _learnedPitEntryPct = null;  // Learned pit entry location (null = not yet learned)
    private bool _isPitEntryLearned = false;    // Whether pit entry has been learned this session
    
    /// <summary>
    /// Initialize fuel calculator service with session persistence
    /// </summary>
    public FuelCalculatorService()
    {
        _persistenceService = new SessionPersistenceService();
    }
    
    /// <summary>
    /// Current fuel calculation data
    /// </summary>
    public FuelData CurrentData { get; private set; } = new();
    
    /// <summary>
    /// Event fired when fuel data is updated
    /// </summary>
    public event EventHandler<FuelData>? FuelDataUpdated;
    
    /// <summary>
    /// Configure buffer laps settings (call before Update)
    /// </summary>
    public void ConfigureBufferSettings(float bufferLaps, bool enableDynamicBuffer)
    {
        _bufferLaps = bufferLaps;
        _enableDynamicBuffer = enableDynamicBuffer;
    }
    
    /// <summary>
    /// Update fuel calculations with new telemetry data
    /// </summary>
    public void Update(TelemetryData telemetry)
    {
        // Initialize on first update
        if (_isFirstUpdate)
        {
            _fuelAtLapStart = telemetry.FuelLevel;
            _lastFuelLevel = telemetry.FuelLevel;
            CurrentData.StartingFuel = telemetry.FuelLevel;
            _isFirstUpdate = false;
        }
        
        // Update current state
        CurrentData.CurrentFuel = telemetry.FuelLevel;
        CurrentData.FuelPct = telemetry.FuelLevelPct;
        CurrentData.TankCapacity = telemetry.FuelLevelMax;
        CurrentData.FuelPressure = telemetry.FuelPress;
        CurrentData.IRacingLapsRemaining = telemetry.SessionLapsRemainEx;
        CurrentData.SessionTimeRemaining = telemetry.SessionTimeRemain;
        CurrentData.LastUpdate = DateTime.UtcNow;
        CurrentData.SessionState = telemetry.SessionState;  // Track session state for pace lap detection
        
        // Update session tracking info for PitStrategyWindow
        CurrentData.CurrentLap = telemetry.LapsCompleted + 1; // LapsCompleted is 0-based, CurrentLap is 1-based
        CurrentData.SessionLaps = telemetry.SessionLaps;
        CurrentData.LapsCompleted = telemetry.LapsCompleted;
        CurrentData.CarClassId = telemetry.PlayerCarClass;
        
        // Set car-specific sputtering threshold (Enhanced Phase 2.1)
        CurrentData.FuelSputteringThreshold = FuelSputteringDatabase.GetSputteringThreshold(
            telemetry.PlayerCarClass, 
            telemetry.FuelLevelMax
        );
        
        // Track fuel pressure and establish baseline (Enhanced Phase 2.1)
        UpdateFuelPressureTracking(telemetry);
        
        // FIX #3: Update lap completion context (race position awareness)
        UpdateLapCompletionContext(telemetry);
        
        // Determine session type and calculate race laps remaining
        CurrentData.IsTimedSession = telemetry.SessionLaps == 0 || telemetry.SessionLaps == -1;
        
        if (CurrentData.IsTimedSession)
        {
            // Time-based session: Calculate laps from time remaining
            // Use average lap time from lap history if available
            if (CurrentData.AverageLapTime > 0 && CurrentData.SessionTimeRemaining > 0)
            {
                CurrentData.EstimatedLapsFromTime = (float)(CurrentData.SessionTimeRemaining / CurrentData.AverageLapTime);
                // Keep fractional laps for smoother tracking - round UP for safety margin in calculations
                CurrentData.RaceLapsRemaining = (int)Math.Ceiling(CurrentData.EstimatedLapsFromTime);
            }
            else
            {
                CurrentData.EstimatedLapsFromTime = 0;
                CurrentData.RaceLapsRemaining = 0;
            }
        }
        else
        {
            // Lap-based session: Use lap count (integer by nature)
            CurrentData.RaceLapsRemaining = Math.Max(0, telemetry.SessionLaps - telemetry.LapsCompleted);
            CurrentData.EstimatedLapsFromTime = CurrentData.RaceLapsRemaining; // Store as float for consistency
        }
        
        // Detect flag status
        _currentFlagStatus = DetectFlagStatus(telemetry.SessionFlags);
        CurrentData.IsUnderYellow = _currentFlagStatus == LapFlagStatus.Yellow;
        
        // FIX #2: Detect checkered flag and freeze calculations
        bool isCheckered = _currentFlagStatus == LapFlagStatus.Checkered;
        if (isCheckered)
        {
            // Race is over - freeze strategy calculations at last valid state
            // Keep current laps remaining, but stop recalculating fuel needs
            LogDebug("CHECKERED FLAG: Race ended, freezing fuel strategy calculations");
            CurrentData.RaceLapsRemaining = 0; // Race is complete
            CurrentData.FuelNeededToFinish = 0;
            CurrentData.FuelToAddAtPit = 0;
            CurrentData.OptimalPitLap = 0;
            CurrentData.OptimalPitReason = "Race complete";
            // Don't fire update event - let last pre-checkered data remain visible
            return;
        }
        
        // Detect pit road status changes and out-laps
        bool isOnPitRoad = telemetry.OnPitRoad;
        if (_wasOnPitRoadLastUpdate && !isOnPitRoad)
        {
            // Just left pit road - this lap is an out-lap
            _justLeftPits = true;
            _stintStartLapNumber = telemetry.LapsCompleted;  // Reset stint tracking
            LogDebug($"OUT-LAP DETECTED: Just left pit road, stint starts at lap {_stintStartLapNumber}");
        }
        _wasOnPitRoadLastUpdate = isOnPitRoad;
        
        // Calculate current lap fuel usage (real-time estimate)
        float fuelUsedThisLap = _fuelAtLapStart - telemetry.FuelLevel;
        CurrentData.FuelUsedThisLap = Math.Max(0, fuelUsedThisLap);
        
        // Estimate current lap fuel rate based on lap distance
        if (telemetry.LapDistPct > 0.05f) // At least 5% into lap
        {
            CurrentData.CurrentLapFuelRate = fuelUsedThisLap / telemetry.LapDistPct;
        }
        
        // Improved refueling detection
        // FIXED: Lowered threshold from 0.5L to 0.3L to detect partial refuels and splash-n-dash stops
        float fuelDelta = telemetry.FuelLevel - _lastFuelLevel;
        bool isRefueling = fuelDelta > 0.3f; // Fuel increased (catches small top-ups)
        
        if (isRefueling)
        {
            CurrentData.LastRefuelAmount = fuelDelta;
            CurrentData.RefuelCount++;
            _fuelAtLapStart = telemetry.FuelLevel; // Reset lap fuel tracking after refuel
            LogDebug($"REFUEL DETECTED: Added {fuelDelta:F3}L (OnPitRoad={isOnPitRoad})");
        }

        // ===== DYNAMIC PIT ENTRY DETECTION (ENHANCEMENT) =====
        // Learn pit entry location from first pit stop (more accurate than hardcoded 85%)
        if (!_wasOnPitRoadLastUpdate && isOnPitRoad && !_isPitEntryLearned)
        {
            // First pit entry detected - learn the location
            _learnedPitEntryPct = telemetry.LapDistPct;
            _isPitEntryLearned = true;
            LogDebug($"PIT ENTRY LEARNED: {_learnedPitEntryPct:F3} ({_learnedPitEntryPct * 100:F1}% track distance)");

            // Save to session statistics for future sessions
            if (_sessionStats != null)
            {
                _sessionStats.PitEntryPct = _learnedPitEntryPct.Value;
                _persistenceService.SaveStatistics(_sessionStats);
            }
        }

        // ===== PIT LAP ACCURACY FIX (Phase 1: FUEL_PIT_LAP_AND_FLAGS_FIX.md) =====
        // PROBLEM: telemetry.LapsCompleted increments at start/finish line (0% track distance)
        //          but pit entry is typically at 80-95% track distance (e.g., Road America at 85%)
        // SOLUTION: Use LapDistPct to detect when player crosses pit entry threshold
        //           Record "virtual lap completion" at pit entry for accurate fuel calculations
        
        // Detect crossing pit entry threshold (virtual lap completion)
        // Use learned pit entry location (or fallback to 0.85 if not yet learned)
        float pitEntryThreshold = _learnedPitEntryPct ?? 0.85f;

        if (_lastLapDistPct < pitEntryThreshold && telemetry.LapDistPct >= pitEntryThreshold)
        {
            // Player crossed pit entry - record "virtual lap completion"
            _virtualLapsCompleted++;

            if (_virtualLapsCompleted != _lapsCompletedWhenProcessed && _lastCompletedLap >= 0)
            {
                OnLapCompleted(telemetry, isRefueling || _justLeftPits);
                _lapsCompletedWhenProcessed = _virtualLapsCompleted;
                LogDebug($"VIRTUAL LAP COMPLETED at pit entry ({pitEntryThreshold*100:F0}%): Lap {_virtualLapsCompleted} (LapDistPct={telemetry.LapDistPct:F3})");
            }
        }
        
        // Handle wrap-around (95% → 5% without crossing pit entry)
        // This happens when player crosses start/finish line without entering pits
        if (_lastLapDistPct > 0.9f && telemetry.LapDistPct < 0.1f)
        {
            // Wrapped around without pit entry - sync virtual laps to iRacing's lap counter
            if (telemetry.LapsCompleted > _lastCompletedLap)
            {
                _virtualLapsCompleted = telemetry.LapsCompleted;
                LogDebug($"LAP WRAP-AROUND: Synced virtual laps to {_virtualLapsCompleted} (crossed start/finish without pit entry)");
            }
        }
        
        _lastLapDistPct = telemetry.LapDistPct;  // Track lap distance for next update
        
        // Reset out-lap flag after lap completion
        if (telemetry.LapDistPct > 0.5f && _justLeftPits)
        {
            _justLeftPits = false; // Clear flag mid-lap to prepare for next detection
        }
        
        // Update tracking variables
        _lastCompletedLap = telemetry.LapsCompleted;
        _lastFuelLevel = telemetry.FuelLevel;
        
        // Calculate averages and strategy
        CalculateAverages();
        ApplyTemperatureCorrection(telemetry);  // ENHANCEMENT: Temperature correction for fuel consumption
        ApplyRealTimeFuelFlow(telemetry);      // ENHANCEMENT: Real-time fuel flow integration
        CalculateDynamicBufferLaps(telemetry, _bufferLaps, _enableDynamicBuffer);  // Calculate dynamic buffer before strategy
        CalculateStrategy();
        CalculateDeltaTracking(telemetry);  // Track delta convergence and confidence (Phase 3)
        CalculateFuelSaving(telemetry);
        
        // Track pit stops for session persistence
        UpdatePitStopTracking(telemetry);
        
        // Fire update event
        FuelDataUpdated?.Invoke(this, CurrentData);
    }
    
    /// <summary>
    /// Handle lap completion and track fuel usage
    /// </summary>
    private void OnLapCompleted(TelemetryData telemetry, bool wasPitLap)
    {
        float fuelUsed = _fuelAtLapStart - telemetry.FuelLevel;
        
        // Detect tow usage: negative fuel (refueled without pit road) or very first lap with suspiciously low fuel
        bool usedTow = false;
        if (fuelUsed < -0.1f) // Negative fuel = tow with refuel
        {
            usedTow = true;
            LogDebug($"TOW DETECTED: Negative fuel usage ({fuelUsed:F3}L) - car was towed/reset");
        }
        else if (_lapHistory.Count == 0 && fuelUsed < 0.3f && telemetry.LapsCompleted == 1)
        {
            // Very first lap with minimal fuel use = likely tow/reset from garage
            usedTow = true;
            LogDebug($"TOW/RESET DETECTED: First lap with minimal fuel ({fuelUsed:F3}L) - started from garage/tow");
        }
        
        // DEBUG: Log lap completion details
        LogDebug($"Lap {telemetry.LapsCompleted} completed: FuelAtStart={_fuelAtLapStart:F3}L, FuelAtEnd={telemetry.FuelLevel:F3}L, FuelUsed={fuelUsed:F3}L, PitLap={wasPitLap}, Tow={usedTow}");
        
        // Create lap history record
        var lapRecord = new FuelLapHistory
        {
            LapNumber = telemetry.LapsCompleted,
            FuelUsed = Math.Max(0, fuelUsed), // Don't record negative fuel
            LapTime = telemetry.LapLastLapTime,
            FuelAtStart = _fuelAtLapStart,
            FuelAtEnd = telemetry.FuelLevel,
            FlagStatus = _currentFlagStatus,
            WasPitLap = wasPitLap || usedTow, // Treat tow same as pit lap
            RefuelAmount = wasPitLap ? CurrentData.LastRefuelAmount : (usedTow && fuelUsed < 0 ? Math.Abs(fuelUsed) : 0),
            IsFormationLap = telemetry.LapsCompleted == 1 && !usedTow, // First lap might be formation (unless towed)
            IsIncompleteLap = false, // If OnLapCompleted fires, the lap WAS completed (LapDistPct resets to 0)
            Timestamp = DateTime.UtcNow,
            IncidentCountAtStart = _lastIncidentCount,  // Incident count at lap start
            IncidentCountAtEnd = telemetry.PlayerCarMyIncidentCount,  // Incident count at lap end
            SessionState = telemetry.SessionState  // Track session state for pace lap detection
        };
        
        // Update incident tracking for next lap
        _lastIncidentCount = telemetry.PlayerCarMyIncidentCount;
        
        _lapHistory.Add(lapRecord);
        CurrentData.LapsCompleted = telemetry.LapsCompleted;
        
        // Update last lap fuel usage
        if (lapRecord.IsValidForAveraging)
        {
            CurrentData.FuelUsedLastLap = lapRecord.FuelUsed;
            
            // Calculate lap-to-lap delta
            var previousValidLap = _lapHistory
                .Where(l => l.IsValidForAveraging && l.LapNumber < lapRecord.LapNumber)
                .OrderByDescending(l => l.LapNumber)
                .FirstOrDefault();
            
            if (previousValidLap != null)
            {
                CurrentData.LapToLapDelta = lapRecord.FuelUsed - previousValidLap.FuelUsed;
            }
        }
        
        // Reset for next lap
        _fuelAtLapStart = telemetry.FuelLevel;
    }
    
    /// <summary>
    /// Calculate all fuel averages from lap history
    /// </summary>
    private void CalculateAverages()
    {
        var validLaps = _lapHistory.Where(l => l.IsValidForAveraging).ToList();
        
        // DEBUG: Log lap history for diagnostics
        LogDebug($"Total laps in history: {_lapHistory.Count}, Valid laps: {validLaps.Count}");
        foreach (var lap in _lapHistory.TakeLast(3))
        {
            LogDebug($"  Lap {lap.LapNumber}: FuelUsed={lap.FuelUsed:F3}L, Valid={lap.IsValidForAveraging}, Pit={lap.WasPitLap}, Formation={lap.IsFormationLap}, Incomplete={lap.IsIncompleteLap}");
        }
        
        if (validLaps.Count == 0)
        {
            CurrentData.HasSufficientData = false;
            CurrentData.WarningMessage = "Need at least 1 completed lap for fuel calculations";
            LogDebug("No valid laps for averaging - need at least 1 completed lap");
            return;
        }
        
        CurrentData.HasSufficientData = validLaps.Count >= 2;
        
        // Last lap average (most recent completed lap)
        CurrentData.AvgFuelPerLap_Last = validLaps.LastOrDefault()?.FuelUsed ?? 0f;
        LogDebug($"AvgFuelPerLap_Last = {CurrentData.AvgFuelPerLap_Last:F4}L");
        
        // Last 5 laps average (exponentially weighted for smoother, more responsive predictions)
        var last5 = validLaps.TakeLast(5).ToList();
        if (last5.Count > 0)
        {
            // Use exponential weighting: most recent lap has highest influence
            // Weights: [1.0, 1.15, 1.3, 1.45, 1.6] (oldest to newest)
            // This gives 27% weight to most recent lap, smoothly decreasing to 17% for 5th lap back
            // FIXED: Reduced from 0.2f to 0.15f to limit outlier influence (was 32% → now 27%)
            float totalWeight = 0f;
            float weightedSum = 0f;
            for (int i = 0; i < last5.Count; i++)
            {
                float weight = 1.0f + (i * 0.15f); // Reduced weight increment for more balanced distribution
                weightedSum += last5[i].FuelUsed * weight;
                totalWeight += weight;
            }
            CurrentData.AvgFuelPerLap_L5 = weightedSum / totalWeight;
        }
        else
        {
            CurrentData.AvgFuelPerLap_L5 = 0f;
        }
        LogDebug($"AvgFuelPerLap_L5 = {CurrentData.AvgFuelPerLap_L5:F4}L (weighted from {last5.Count} laps)");
        
        // Last 10 laps average (simple average for longer-term trend)
        var last10 = validLaps.TakeLast(10).ToList();
        CurrentData.AvgFuelPerLap_L10 = last10.Count > 0 ? last10.Average(l => l.FuelUsed) : 0f;
        LogDebug($"AvgFuelPerLap_L10 = {CurrentData.AvgFuelPerLap_L10:F4}L (from {last10.Count} laps)");
        
        // Session average with enhanced outlier filtering
        // Phase 2: Multi-method outlier detection (MAD + lap time correlation + incident tracking)
        if (validLaps.Count >= 3)
        {
            // Apply enhanced outlier detection (MAD + lap time + incidents)
            var analyzedLaps = DetectOutliers(validLaps);
            
            // Filter out flagged outliers for averaging
            var cleanLaps = analyzedLaps.Where(l => !l.IsFlaggedAsOutlier).ToList();
            
            // Log outlier detection results
            int flaggedCount = analyzedLaps.Count(l => l.IsFlaggedAsOutlier);
            if (flaggedCount > 0)
            {
                LogDebug($"OUTLIER DETECTION: Flagged {flaggedCount}/{analyzedLaps.Count} laps:");
                foreach (var outlier in analyzedLaps.Where(l => l.IsFlaggedAsOutlier))
                {
                    LogDebug($"  Lap {outlier.LapNumber}: {outlier.FuelUsed:F3}L, {outlier.LapTime:F1}s - {outlier.OutlierReason}");
                }
            }
            
            // Fallback: If outlier filtering removed too many laps (>40%), use IQR method instead
            // This prevents over-filtering in races with legitimate fuel saving or variable conditions
            if (cleanLaps.Count < validLaps.Count * 0.6f)
            {
                LogDebug($"WARNING: MAD filtering removed {flaggedCount}/{validLaps.Count} laps (>{40}%), falling back to IQR method");
                
                // IQR fallback: Calculate interquartile range for robust outlier detection
                var sortedFuel = validLaps.Select(l => l.FuelUsed).OrderBy(f => f).ToList();
                int q1Index = sortedFuel.Count / 4;
                int q3Index = (sortedFuel.Count * 3) / 4;
                float q1 = sortedFuel[q1Index];
                float q3 = sortedFuel[q3Index];
                float iqr = q3 - q1;
                
                // Outlier thresholds: Q1 - 1.5*IQR to Q3 + 1.5*IQR (standard statistical method)
                float lowerBound = Math.Max(0f, q1 - (1.5f * iqr)); // Fuel can't be negative
                float upperBound = q3 + (1.5f * iqr);
                
                cleanLaps = validLaps.Where(l => l.FuelUsed >= lowerBound && l.FuelUsed <= upperBound).ToList();
                
                // If IQR filtering still removed too many, use median filter
                if (cleanLaps.Count < validLaps.Count * 0.7f)
                {
                    float median = sortedFuel[sortedFuel.Count / 2];
                    cleanLaps = validLaps.Where(l => l.FuelUsed <= median * 1.5f).ToList();
                    LogDebug($"IQR filtering also aggressive, using median filter: {cleanLaps.Count}/{validLaps.Count} laps");
                }
                else
                {
                    LogDebug($"IQR fallback: {cleanLaps.Count}/{validLaps.Count} laps, bounds: {lowerBound:F3}-{upperBound:F3}");
                }
            }
            
            CurrentData.AvgFuelPerLap_Session = cleanLaps.Count > 0 
                ? cleanLaps.Average(l => l.FuelUsed) 
                : validLaps.Average(l => l.FuelUsed);
            
            LogDebug($"AvgFuelPerLap_Session = {CurrentData.AvgFuelPerLap_Session:F4}L (from {cleanLaps.Count}/{validLaps.Count} laps after outlier detection)");
        }
        else
        {
            // Not enough data for outlier detection, use simple average
            CurrentData.AvgFuelPerLap_Session = validLaps.Average(l => l.FuelUsed);
            LogDebug($"AvgFuelPerLap_Session = {CurrentData.AvgFuelPerLap_Session:F4}L (from {validLaps.Count} laps, no filtering)");
        }
        
        // Min/Max fuel per lap (track extremes for strategic planning)
        CurrentData.MinFuelPerLap = validLaps.Min(l => l.FuelUsed);
        CurrentData.MaxFuelPerLap = validLaps.Max(l => l.FuelUsed);
        LogDebug($"Min/Max = {CurrentData.MinFuelPerLap:F4}L / {CurrentData.MaxFuelPerLap:F4}L (range: {CurrentData.MaxFuelPerLap - CurrentData.MinFuelPerLap:F4}L)");
        
        // Green flag average (for racing conditions)
        var greenLaps = validLaps.Where(l => l.IsGreenFlagLap).ToList();
        CurrentData.GreenFlagLapCount = greenLaps.Count;
        CurrentData.GreenFlagAverage = greenLaps.Count > 0 ? greenLaps.Average(l => l.FuelUsed) : 0f;
        LogDebug($"GreenFlagAverage = {CurrentData.GreenFlagAverage:F4}L (from {CurrentData.GreenFlagLapCount} laps)");
        
        // Yellow flag average (for caution periods)
        var yellowLaps = validLaps.Where(l => l.IsYellowFlagLap).ToList();
        CurrentData.YellowFlagLapCount = yellowLaps.Count;
        CurrentData.YellowFlagAverage = yellowLaps.Count > 0 ? yellowLaps.Average(l => l.FuelUsed) : 0f;
        LogDebug($"YellowFlagAverage = {CurrentData.YellowFlagAverage:F4}L (from {CurrentData.YellowFlagLapCount} laps)");
        
        // ===== NEW ADVANCED AVERAGING METHODS =====

        // 1. Exponential Moving Average (EMA) - smoother transitions, responsive to trends
        // Formula: EMA = (CurrentValue * Alpha) + (PreviousEMA * (1 - Alpha))
        // ENHANCED: Adaptive alpha based on fuel consistency (variance)
        if (validLaps.Count > 0)
        {
            var mostRecentLap = validLaps.Last();
            if (!_emaInitialized)
            {
                // Initialize EMA with first lap value
                _emaValue = mostRecentLap.FuelUsed;
                _emaInitialized = true;
                LogDebug($"EMA INITIALIZED: {_emaValue:F4}L");
            }
            else
            {
                // Calculate adaptive alpha based on fuel consistency variance (CurrentData.FuelConsistencyVariance)
                // More consistent driving → higher alpha (more responsive)
                // More variable driving → lower alpha (smoother, less reactive to outliers)
                float adaptiveAlpha = CurrentData.FuelConsistencyVariance switch
                {
                    < 0.1f => 0.5f,  // Very consistent (±0.1L) → highly responsive (50% recent)
                    < 0.2f => 0.4f,  // Normal consistency (±0.2L) → balanced (40% recent)
                    < 0.3f => 0.3f,  // Variable (±0.3L) → smoother (30% recent)
                    _ => 0.2f        // Very variable (±0.3L+) → very smooth (20% recent)
                };

                // Update EMA with adaptive exponential smoothing
                _emaValue = (mostRecentLap.FuelUsed * adaptiveAlpha) + (_emaValue * (1 - adaptiveAlpha));
                CurrentData.AvgFuelPerLap_EMA = _emaValue;
                LogDebug($"AvgFuelPerLap_EMA = {CurrentData.AvgFuelPerLap_EMA:F4}L (adaptive alpha={adaptiveAlpha:F2}, variance={CurrentData.FuelConsistencyVariance:F3}L)");
            }
        }
        else
        {
            CurrentData.AvgFuelPerLap_EMA = 0f;
        }
        
        // 2. Green Flag Only Average - uses existing GreenFlagAverage calculation
        // This excludes all yellow/caution laps for pure race pace fuel consumption
        CurrentData.AvgFuelPerLap_GreenOnly = CurrentData.GreenFlagAverage;
        LogDebug($"AvgFuelPerLap_GreenOnly = {CurrentData.AvgFuelPerLap_GreenOnly:F4}L (from {CurrentData.GreenFlagLapCount} green laps)");
        
        // 3. Stint Average - fuel consumption since last pit stop
        var stintLaps = validLaps.Where(l => l.LapNumber > _stintStartLapNumber).ToList();
        CurrentData.StintLapCount = stintLaps.Count;
        if (stintLaps.Count > 0)
        {
            CurrentData.AvgFuelPerLap_Stint = stintLaps.Average(l => l.FuelUsed);
            LogDebug($"AvgFuelPerLap_Stint = {CurrentData.AvgFuelPerLap_Stint:F4}L (from {stintLaps.Count} laps since lap {_stintStartLapNumber})");
        }
        else
        {
            // No stint data yet, fallback to L5 average
            CurrentData.AvgFuelPerLap_Stint = CurrentData.AvgFuelPerLap_L5;
            LogDebug($"AvgFuelPerLap_Stint = {CurrentData.AvgFuelPerLap_Stint:F4}L (fallback to L5, no stint laps yet)");
        }
        
        // 4. Adaptive Weighted Average - adjusts weighting based on fuel consistency
        // Tight weighting if fuel use is consistent, loose weighting if variable
        var last5ForAdaptive = validLaps.TakeLast(5).ToList();
        if (last5ForAdaptive.Count >= 3)
        {
            // Calculate standard deviation of last 5 laps to measure consistency
            float mean = last5ForAdaptive.Average(l => l.FuelUsed);
            float variance = last5ForAdaptive.Sum(l => (float)Math.Pow(l.FuelUsed - mean, 2)) / last5ForAdaptive.Count;
            float stdDev = (float)Math.Sqrt(variance);
            
            // Coefficient of variation (CV) = StdDev / Mean
            // Low CV (<0.05) = very consistent → use tight weights (favor recent laps heavily)
            // High CV (>0.15) = variable → use loose weights (spread weight more evenly)
            float cv = mean > 0 ? stdDev / mean : 0f;
            
            // Adaptive weight spread: CV < 0.05 → tight (0.3), CV > 0.15 → loose (0.1)
            float weightSpread = cv < 0.05f ? 0.3f : cv > 0.15f ? 0.1f : 0.2f;
            
            // Apply adaptive weighting
            float totalWeight = 0f;
            float weightedSum = 0f;
            for (int i = 0; i < last5ForAdaptive.Count; i++)
            {
                float weight = 1.0f + (i * weightSpread);  // Adaptive weight based on consistency
                weightedSum += last5ForAdaptive[i].FuelUsed * weight;
                totalWeight += weight;
            }
            CurrentData.AvgFuelPerLap_Adaptive = weightedSum / totalWeight;
            LogDebug($"AvgFuelPerLap_Adaptive = {CurrentData.AvgFuelPerLap_Adaptive:F4}L (CV={cv:F3}, weightSpread={weightSpread:F2}, from {last5ForAdaptive.Count} laps)");
        }
        else
        {
            // Not enough data for adaptive weighting, fallback to L5
            CurrentData.AvgFuelPerLap_Adaptive = CurrentData.AvgFuelPerLap_L5;
            LogDebug($"AvgFuelPerLap_Adaptive = {CurrentData.AvgFuelPerLap_Adaptive:F4}L (fallback to L5, need 3+ laps)");
        }
        
        // Calculate average lap time for time-based sessions
        if (validLaps.Count > 0)
        {
            var lapsWithTime = validLaps.Where(l => l.LapTime > 0).ToList();
            if (lapsWithTime.Count > 0)
            {
                // Use weighted average for lap time (same as L5 fuel: recent laps weighted more)
                var recentLaps = lapsWithTime.TakeLast(5).ToList();
                float totalWeight = 0f;
                float weightedSum = 0f;
                for (int i = 0; i < recentLaps.Count; i++)
                {
                    float weight = 1.0f + (i * 0.2f);
                    weightedSum += recentLaps[i].LapTime * weight;
                    totalWeight += weight;
                }
                CurrentData.AverageLapTime = weightedSum / totalWeight;
                LogDebug($"AverageLapTime = {CurrentData.AverageLapTime:F4}s (weighted from {recentLaps.Count} laps)");
            }
        }
        
        // Pace lap fuel consumption tracking (separate from race laps)
        var paceLaps = _lapHistory.Where(l => l.IsValidPaceLap).ToList();
        CurrentData.PaceLapCount = paceLaps.Count;
        if (paceLaps.Count > 0)
        {
            CurrentData.AvgFuelPerLap_PaceLaps = paceLaps.Average(l => l.FuelUsed);
            LogDebug($"PaceLapAverage = {CurrentData.AvgFuelPerLap_PaceLaps:F4}L (from {CurrentData.PaceLapCount} pace laps, ~{(CurrentData.AvgFuelPerLap_PaceLaps / Math.Max(0.01f, CurrentData.AvgFuelPerLap_Session) * 100):F0}% of race pace)");
        }
        else
        {
            CurrentData.AvgFuelPerLap_PaceLaps = 0f;
            LogDebug("No pace laps recorded yet");
        }
        
        // Total fuel used
        CurrentData.TotalFuelUsed = CurrentData.StartingFuel - CurrentData.CurrentFuel + 
                                    (_lapHistory.Where(l => l.WasPitLap).Sum(l => l.RefuelAmount));
    }
    
    /// <summary>
    /// Calculate race strategy (laps remaining, fuel needed, etc.)
    /// </summary>
    private void CalculateStrategy()
    {
        float avgFuel = CurrentData.AvgFuelPerLap;
        
        if (avgFuel <= 0)
        {
            CurrentData.LapsRemaining = 0;
            CurrentData.CanFinishWithoutStop = false;
            return;
        }
        
        // Account for car-specific fuel sputtering threshold (Enhanced Phase 2.1)
        // Only calculate usable fuel (fuel above the sputtering threshold)
        float sputteringThreshold = CurrentData.FuelSputteringThreshold;
        float usableFuel = Math.Max(0, CurrentData.CurrentFuel - sputteringThreshold);
        
        // Laps remaining based on selected averaging method (using usable fuel only)
        CurrentData.LapsRemaining = usableFuel / avgFuel;
        LogDebug($"LapsRemaining = {CurrentData.LapsRemaining:F4} (Usable fuel: {usableFuel:F4}L / Avg: {avgFuel:F4}L, Sputtering threshold: {sputteringThreshold:F2}L [{FuelSputteringDatabase.GetCarCategory(CurrentData.CarClassId)}])");
        
        // Compare with iRacing's estimate
        CurrentData.LapsDifference = CurrentData.LapsRemaining - CurrentData.IRacingLapsRemaining;
        LogDebug($"iRacing estimate: {CurrentData.IRacingLapsRemaining:F2} laps, Difference: {CurrentData.LapsDifference:F4} laps");
        
        // Fuel needed to finish race
        if (CurrentData.RaceLapsRemaining > 0)
        {
            // Add buffer laps for safety
            float lapsToFinish = CurrentData.RaceLapsRemaining + CurrentData.FuelBufferLaps;
            // Account for sputtering threshold: need extra fuel to avoid running out before finish line
            CurrentData.FuelNeededToFinish = (lapsToFinish * avgFuel) + sputteringThreshold;
            CurrentData.FuelDeltaToFinish = CurrentData.CurrentFuel - CurrentData.FuelNeededToFinish;
            CurrentData.CanFinishWithoutStop = CurrentData.FuelDeltaToFinish >= 0;
            
            if (CurrentData.IsTimedSession)
            {
                LogDebug($"TIME-BASED SESSION: {CurrentData.SessionTimeRemaining:F0}s remaining = {CurrentData.EstimatedLapsFromTime:F2} laps (avg lap time: {CurrentData.AverageLapTime:F2}s)");
            }
            
            LogDebug($"FuelNeededToFinish = {CurrentData.FuelNeededToFinish:F4}L (for {lapsToFinish:F2} laps w/ {CurrentData.FuelBufferLaps} buffer + {sputteringThreshold:F2}L sputtering reserve)");
            LogDebug($"FuelDeltaToFinish = {CurrentData.FuelDeltaToFinish:F4}L - Can finish: {CurrentData.CanFinishWithoutStop}");
            
            // Fuel to add at next pit stop
            if (!CurrentData.CanFinishWithoutStop)
            {
                // Need enough fuel to finish, accounting for sputtering threshold
                CurrentData.FuelToAddAtPit = Math.Abs(CurrentData.FuelDeltaToFinish);
                // Round up to nearest 0.5L for safety
                CurrentData.FuelToAddAtPit = (float)Math.Ceiling(CurrentData.FuelToAddAtPit * 2) / 2;
                LogDebug($"FuelToAddAtPit = {CurrentData.FuelToAddAtPit:F4}L (rounded to 0.5L increments)");
            }
            else
            {
                CurrentData.FuelToAddAtPit = 0;
            }
        }
        
        // Warning for low fuel (account for sputtering threshold in warnings)
        // Enhanced Phase 2.1: Use fuel pressure correlation if available
        if (CurrentData.CurrentFuel <= sputteringThreshold)
        {
            CurrentData.WarningMessage = $"CRITICAL: At sputtering threshold ({sputteringThreshold:F2}L) - FUEL NOW!";
        }
        else if (CurrentData.FuelPressureLow && CurrentData.LapsRemaining < 2)
        {
            CurrentData.WarningMessage = $"CRITICAL: Fuel pressure low ({CurrentData.FuelPressureDropPct:F0}% drop) - PIT SOON!";
        }
        else if (CurrentData.LapsRemaining < 1)
        {
            CurrentData.WarningMessage = "CRITICAL: Less than 1 lap of usable fuel remaining!";
        }
        else if (CurrentData.LapsRemaining < 2)
        {
            CurrentData.WarningMessage = "WARNING: Less than 2 laps of usable fuel remaining";
        }
        else if (CurrentData.LapsRemaining < 5)
        {
            CurrentData.WarningMessage = "CAUTION: Less than 5 laps of usable fuel remaining";
        }
        else if (CurrentData.IsUnderYellow && CurrentData.GreenFlagLapCount > 0)
        {
            // Check if fuel would be sufficient under green flag racing (using usable fuel)
            float usableFuelForGreen = Math.Max(0, CurrentData.CurrentFuel - sputteringThreshold);
            float greenLapsRemaining = usableFuelForGreen / CurrentData.GreenFlagAverage;
            if (greenLapsRemaining < CurrentData.RaceLapsRemaining)
            {
                CurrentData.WarningMessage = $"WARNING: Insufficient fuel for green flag racing (only {greenLapsRemaining:F1} laps)";
            }
        }
        else
        {
            CurrentData.WarningMessage = null;
        }
    }
    
    /// <summary>
    /// Calculate delta tracking, convergence, confidence, and historical accuracy (Phase 3)
    /// Provides intelligent insights into how our predictions compare with iRacing's
    /// </summary>
    private void CalculateDeltaTracking(TelemetryData telemetry)
    {
        // Skip if no valid data
        if (CurrentData.LapsRemaining <= 0 || CurrentData.IRacingLapsRemaining <= 0)
        {
            CurrentData.DeltaConvergenceTrend = "Unknown";
            CurrentData.DeltaConfidence = "Unknown";
            CurrentData.DeltaExplanation = "Insufficient data for delta analysis";
            return;
        }
        
        // Calculate current delta
        float currentDelta = CurrentData.LapsDifference;  // Already calculated in CalculateStrategy()
        
        // Record delta history (once per lap completion)
        if (telemetry.LapsCompleted > 0 && 
            (_deltaHistory.Count == 0 || _deltaHistory.Last().LapNumber < telemetry.LapsCompleted))
        {
            var record = new DeltaHistoryRecord
            {
                LapNumber = telemetry.LapsCompleted,
                OurLapsRemaining = CurrentData.LapsRemaining,
                IRacingLapsRemaining = CurrentData.IRacingLapsRemaining,
                Delta = currentDelta,
                MethodUsed = CurrentData.SelectedMethod,
                CurrentFuel = CurrentData.CurrentFuel,
                RaceLapsRemaining = CurrentData.RaceLapsRemaining,
                Timestamp = DateTime.UtcNow
            };
            
            _deltaHistory.Add(record);
            
            // Limit history size
            if (_deltaHistory.Count > MAX_DELTA_HISTORY)
            {
                _deltaHistory.RemoveAt(0);
            }
            
            LogDebug($"DELTA TRACKING: Lap {record.LapNumber}, Our={record.OurLapsRemaining:F2}, iRacing={record.IRacingLapsRemaining:F2}, Delta={record.Delta:F2}");
        }
        
        // ===== CONVERGENCE TRACKING =====
        
        if (_deltaHistory.Count >= 3)
        {
            // Calculate convergence rate from recent delta history
            var recentDeltas = _deltaHistory.TakeLast(5).ToList();
            
            // Linear regression to determine if delta is narrowing or widening
            // Positive slope = delta increasing (widening)
            // Negative slope = delta decreasing (narrowing toward zero)
            float avgLap = (float)recentDeltas.Average(d => d.LapNumber);
            float avgDelta = (float)recentDeltas.Average(d => Math.Abs(d.Delta));  // Use absolute delta
            
            float numerator = 0f;
            float denominator = 0f;
            foreach (var d in recentDeltas)
            {
                numerator += (d.LapNumber - avgLap) * (Math.Abs(d.Delta) - avgDelta);
                denominator += (d.LapNumber - avgLap) * (d.LapNumber - avgLap);
            }
            
            float slope = denominator > 0 ? numerator / denominator : 0f;
            CurrentData.DeltaConvergenceRate = -slope;  // Negative slope = narrowing (positive convergence rate)
            
            // Determine convergence trend
            if (Math.Abs(slope) < 0.05f)
            {
                CurrentData.DeltaConvergenceTrend = "Stable";
            }
            else if (slope < 0)
            {
                CurrentData.DeltaConvergenceTrend = "Narrowing";  // Delta decreasing = predictions converging
            }
            else
            {
                CurrentData.DeltaConvergenceTrend = "Widening";  // Delta increasing = predictions diverging
            }
            
            LogDebug($"CONVERGENCE: Trend={CurrentData.DeltaConvergenceTrend}, Rate={CurrentData.DeltaConvergenceRate:F3} laps/lap, Slope={slope:F3}");
        }
        else
        {
            CurrentData.DeltaConvergenceTrend = "Unknown";
            CurrentData.DeltaConvergenceRate = 0f;
        }
        
        // ===== CONFIDENCE CALCULATION =====
        
        if (_deltaHistory.Count >= 5)
        {
            // Calculate delta standard deviation (low stdev = high confidence)
            var last10Deltas = _deltaHistory.TakeLast(10).Select(d => Math.Abs(d.Delta)).ToList();
            float avgAbsDelta = last10Deltas.Average();
            float variance = last10Deltas.Sum(d => (float)Math.Pow(d - avgAbsDelta, 2)) / last10Deltas.Count;
            float stdDev = (float)Math.Sqrt(variance);
            
            // Coefficient of variation (CV) = StdDev / Mean
            // Low CV = consistent delta = high confidence
            float cv = avgAbsDelta > 0 ? stdDev / avgAbsDelta : 0f;
            
            // Confidence score: 100 = perfect (CV=0), 0 = terrible (CV>1)
            // CV < 0.1 = High confidence (90-100)
            // CV 0.1-0.3 = Medium confidence (50-90)
            // CV > 0.3 = Low confidence (0-50)
            CurrentData.DeltaConfidenceScore = Math.Clamp(100f * (1f - cv), 0f, 100f);
            
            if (CurrentData.DeltaConfidenceScore >= 80f)
            {
                CurrentData.DeltaConfidence = "High";
            }
            else if (CurrentData.DeltaConfidenceScore >= 50f)
            {
                CurrentData.DeltaConfidence = "Medium";
            }
            else
            {
                CurrentData.DeltaConfidence = "Low";
            }
            
            LogDebug($"CONFIDENCE: Score={CurrentData.DeltaConfidenceScore:F1}, Level={CurrentData.DeltaConfidence}, CV={cv:F3}, StdDev={stdDev:F3}");
        }
        else
        {
            CurrentData.DeltaConfidence = "Unknown";
            CurrentData.DeltaConfidenceScore = 0f;
        }
        
        // ===== EXPLANATION OF DELTA =====
        
        // Build explanation based on method used
        string methodExplanation = CurrentData.SelectedMethod switch
        {
            FuelAveragingMethod.Current => "real-time fuel rate from current lap",
            FuelAveragingMethod.Last => "fuel usage from last completed lap only",
            FuelAveragingMethod.Last5 => "weighted average of last 5 laps (recent laps weighted higher)",
            FuelAveragingMethod.Last10 => "simple average of last 10 laps",
            FuelAveragingMethod.Session => "session average with outlier filtering (most reliable)",
            FuelAveragingMethod.Max => "maximum fuel per lap (worst case scenario)",
            FuelAveragingMethod.EMA => "exponential moving average (smooth transitions)",
            FuelAveragingMethod.GreenFlagOnly => "green flag laps only (excludes yellow laps)",
            FuelAveragingMethod.StintAverage => "fuel consumption since last pit stop",
            FuelAveragingMethod.Adaptive => "adaptive weighting based on fuel consistency",
            _ => "weighted averaging method"
        };
        
        CurrentData.DeltaExplanation = $"iRacing uses current lap fuel rate only. We use {methodExplanation}.";
        
        // Add convergence info to explanation
        if (CurrentData.DeltaConvergenceTrend == "Narrowing")
        {
            CurrentData.DeltaExplanation += " Delta narrowing - predictions converging.";
        }
        else if (CurrentData.DeltaConvergenceTrend == "Widening")
        {
            CurrentData.DeltaExplanation += " Delta widening - check fuel consistency.";
        }
        
        // ===== SANITY CHECKS =====
        
        float absDelta = Math.Abs(currentDelta);
        CurrentData.DeltaSuspicious = absDelta > 5.0f;
        
        if (CurrentData.DeltaSuspicious)
        {
            // Investigate why delta is so large
            List<string> reasons = new();
            
            if (CurrentData.SelectedMethod == FuelAveragingMethod.Current && telemetry.LapDistPct < 0.3f)
            {
                reasons.Add("early in lap (current fuel rate unstable)");
            }
            
            if (CurrentData.AvgFuelPerLap_L5 > 0 && 
                Math.Abs(CurrentData.AvgFuelPerLap_L5 - CurrentData.AvgFuelPerLap_Session) > CurrentData.AvgFuelPerLap_Session * 0.3f)
            {
                reasons.Add("recent fuel usage differs significantly from session average");
            }
            
            if (_lapHistory.Count < 5)
            {
                reasons.Add("insufficient lap history (need 5+ laps for accurate averaging)");
            }
            
            if (CurrentData.FuelConsistencyVariance > CurrentData.AvgFuelPerLap * 0.2f)
            {
                reasons.Add($"high fuel variance (±{CurrentData.FuelConsistencyVariance:F2}L)");
            }
            
            CurrentData.DeltaSuspiciousReason = reasons.Count > 0 
                ? string.Join(", ", reasons) 
                : "large delta (>5 laps difference) - investigate fuel calculation";
                
            LogDebug($"⚠️ SUSPICIOUS DELTA: {absDelta:F2} laps - {CurrentData.DeltaSuspiciousReason}");
        }
        else
        {
            CurrentData.DeltaSuspiciousReason = null;
        }
        
        // ===== HISTORICAL ACCURACY (stub for future post-race analysis) =====
        
        // Count predictions with known outcomes
        var completedPredictions = _deltaHistory.Where(d => d.WasOurPredictionBetter.HasValue).ToList();
        CurrentData.TotalPredictions = completedPredictions.Count;
        
        if (CurrentData.TotalPredictions > 0)
        {
            int ourWins = completedPredictions.Count(d => d.WasOurPredictionBetter == true);
            int iRacingWins = completedPredictions.Count(d => d.WasOurPredictionBetter == false);
            
            CurrentData.OurMethodAccuracy = (float)ourWins / CurrentData.TotalPredictions;
            CurrentData.IRacingMethodAccuracy = (float)iRacingWins / CurrentData.TotalPredictions;
            
            // Recommend method based on accuracy (need at least 10 predictions for confidence)
            if (CurrentData.TotalPredictions >= 10)
            {
                if (CurrentData.OurMethodAccuracy > CurrentData.IRacingMethodAccuracy + 0.1f)
                {
                    CurrentData.RecommendedMethod = "UseOurs";
                    CurrentData.SuggestCurrentMethod = false;
                }
                else if (CurrentData.IRacingMethodAccuracy > CurrentData.OurMethodAccuracy + 0.1f)
                {
                    CurrentData.RecommendedMethod = "UseIRacing";
                    CurrentData.SuggestCurrentMethod = true;  // Suggest switching to "Current" method to match iRacing
                }
                else
                {
                    CurrentData.RecommendedMethod = "Uncertain";
                    CurrentData.SuggestCurrentMethod = false;
                }
                
                LogDebug($"ACCURACY: Ours={CurrentData.OurMethodAccuracy * 100:F0}%, iRacing={CurrentData.IRacingMethodAccuracy * 100:F0}%, Recommend={CurrentData.RecommendedMethod}");
            }
        }
        else
        {
            // No historical data yet - default to our method
            CurrentData.OurMethodAccuracy = 0f;
            CurrentData.IRacingMethodAccuracy = 0f;
            CurrentData.RecommendedMethod = "UseOurs";
            CurrentData.SuggestCurrentMethod = false;
        }
        
        // Update last delta for next iteration
        _lastDelta = currentDelta;
    }
    
    /// <summary>
    /// Calculate fuel saving mode data (Phase 3)
    /// </summary>
    private void CalculateFuelSaving(TelemetryData telemetry)
    {
        // Reset fuel saving data if not in race mode, no valid data, or race is ending (white/checkered flag)
        // FIX: Only consider "race ending" on white flag OR when truly at the end of the race
        // For timed sessions: Use percentage of session time remaining (< 5% of total session time AND < 1 lap estimated)
        // This scales properly for all track lengths (Nürburgring ~10min/lap vs short ovals ~30sec/lap)
        bool isRaceEnding = (_currentFlagStatus == LapFlagStatus.White || 
                            _currentFlagStatus == LapFlagStatus.Checkered || 
                            (CurrentData.RaceLapsRemaining == 0 && _lapHistory.Count > 5) || // Lap-based: RaceLapsRemaining hits 0 after 5+ laps
                            (CurrentData.IsTimedSession && CurrentData.AverageLapTime > 0 && 
                             CurrentData.SessionTimeRemaining < (CurrentData.AverageLapTime * 1.5) && // Less than 1.5 laps of time remaining
                             CurrentData.EstimatedLapsFromTime < 1.0f)); // AND less than 1 lap estimated
        
        if (CurrentData.RaceLapsRemaining <= 0 || CurrentData.AvgFuelPerLap_L5 <= 0 || !CurrentData.HasSufficientData || isRaceEnding)
        {
            CurrentData.NeedsFuelSaving = false;
            CurrentData.FuelSavingWorking = false;
            CurrentData.FuelSavingTarget = 0;
            CurrentData.CurrentSavingRate = 0;
            CurrentData.TargetLapTime = 0;
            CurrentData.SavingProgress = 0;
            CurrentData.ProjectedFuelDelta = 0;
            CurrentData.StrategicAlert = isRaceEnding ? "Final lap - race ending" : null;
            CurrentData.AlertSeverity = 0;
            CurrentData.OptimalPitLap = 0;
            CurrentData.OptimalPitReason = isRaceEnding ? "Race ending" : null;
            return;
        }
        
        // FIX #3: Use realistic baseline for fuel saving calculations
        // If L5 > Session, driver is currently using MORE fuel than session average
        // Realistic baseline = Session average (proven achievable) OR lower L10 average
        // Target should be to reduce FROM current L5 TO session/L10 level
        float baselineAverage = CurrentData.AvgFuelPerLap_Session;
        
        // Fallback: if session avg not available, use L10 or L5
        if (baselineAverage <= 0)
        {
            baselineAverage = CurrentData.AvgFuelPerLap_L10 > 0 ? CurrentData.AvgFuelPerLap_L10 : CurrentData.AvgFuelPerLap_L5;
        }
        
        // If current L5 is significantly higher than baseline, use baseline for calculation
        // Otherwise use L5 (driver is already at or below session average)
        float avgFuel = CurrentData.AvgFuelPerLap_L5 > baselineAverage * 1.05f 
            ? baselineAverage  // Use lower baseline if L5 is 5%+ higher
            : CurrentData.AvgFuelPerLap_L5;  // Use current L5 if already efficient
        
        float currentFuel = CurrentData.CurrentFuel;
        int lapsRemaining = CurrentData.RaceLapsRemaining;
        
        LogDebug($"FUEL SAVING: L5={CurrentData.AvgFuelPerLap_L5:F3}L, Session={CurrentData.AvgFuelPerLap_Session:F3}L, Using baseline={avgFuel:F3}L for calculations");
        
        // Calculate fuel needed to finish (without buffer for fuel saving calculation)
        // Enhanced Phase 2.1: Use car-specific sputtering threshold
        float sputteringThreshold = CurrentData.FuelSputteringThreshold;
        float fuelNeededToFinish = (lapsRemaining * avgFuel) + sputteringThreshold;
        float fuelDeficit = fuelNeededToFinish - currentFuel;
        
        // Determine if fuel saving is needed
        CurrentData.NeedsFuelSaving = fuelDeficit > 0;
        
        // ===== PIT STRATEGY VS FUEL SAVING TIME COMPARISON =====
        
        // Load session statistics on first update or track/car change
        if (!_sessionStatsLoaded || telemetry.TrackName != _currentTrackName || telemetry.PlayerCarClass != _currentCarClassId)
        {
            _currentTrackName = telemetry.TrackName;
            _currentCarClassId = telemetry.PlayerCarClass;
            _sessionStats = _persistenceService.GetStatistics(_currentTrackName, _currentCarClassId);
            _sessionStatsLoaded = true;
            
            if (_sessionStats != null && _sessionStats.HasSufficientData)
            {
                LogDebug($"LOADED SESSION STATS: {_sessionStats.PitStopsRecorded} stops, {_sessionStats.LapsSampled} laps, Avg pit time: {_sessionStats.AverageTotalPitTime:F1}s");
                LogDebug($"  Environmental: Track {_sessionStats.MinTrackTemp:F1}-{_sessionStats.MaxTrackTemp:F1}°C, Air {_sessionStats.MinAirTemp:F1}-{_sessionStats.MaxAirTemp:F1}°C");
                LogDebug($"  Session types: Practice={_sessionStats.PracticeStops}, Qualifying={_sessionStats.QualifyingStops}, Warmup={_sessionStats.WarmupStops}, Race={_sessionStats.RaceStops}");

                // Load learned pit entry location from historical data
                if (_sessionStats.PitEntryPct > 0 && _sessionStats.PitEntryPct < 1.0f)
                {
                    _learnedPitEntryPct = _sessionStats.PitEntryPct;
                    _isPitEntryLearned = true;
                    LogDebug($"  Pit Entry: {_sessionStats.PitEntryPct:F3} ({_sessionStats.PitEntryPct * 100:F1}% track distance) - loaded from historical data");
                }

                // ENHANCEMENT: Track-specific fuel consumption database integration
                // Compare current fuel consumption to historical baseline for this track/car
                if (_sessionStats.SessionAverageFuelPerLap > 0)
                {
                    CurrentData.HistoricalFuelAverage = _sessionStats.SessionAverageFuelPerLap;

                    // Calculate variance percentage (only if we have current session data)
                    if (CurrentData.AvgFuelPerLap_Session > 0)
                    {
                        float variance = ((CurrentData.AvgFuelPerLap_Session - _sessionStats.SessionAverageFuelPerLap) / _sessionStats.SessionAverageFuelPerLap) * 100f;
                        CurrentData.HistoricalFuelVariance = variance;

                        if (Math.Abs(variance) > 10f)
                        {
                            LogDebug($"HISTORICAL FUEL VARIANCE: Current {CurrentData.AvgFuelPerLap_Session:F3}L vs Historical {_sessionStats.SessionAverageFuelPerLap:F3}L ({variance:+0.0;-0.0;0}%)");
                        }
                    }

                    // Bootstrap initial fuel estimates from historical data (before 5 laps completed)
                    if (_lapHistory.Count < 5)
                    {
                        CurrentData.AvgFuelPerLap_Session = _sessionStats.SessionAverageFuelPerLap;
                        LogDebug($"BOOTSTRAP: Using historical average {_sessionStats.SessionAverageFuelPerLap:F3}L (only {_lapHistory.Count} laps completed)");
                    }
                }
            }
            else
            {
                LogDebug($"NO SESSION STATS AVAILABLE: First time at {_currentTrackName} with class {_currentCarClassId}");
            }
        }
        
        // Estimate pit stop time using historical data OR conservative default formula
        bool usingHistoricalData = false;
        bool environmentalMatch = false;
        
        if (_sessionStats != null && _sessionStats.HasSufficientData)
        {
            // Check if current environmental conditions match historical data
            environmentalMatch = _sessionStats.IsSimilarConditions(telemetry.TrackTemp, telemetry.AirTemp);
            
            if (environmentalMatch)
            {
                // Use historical data - high confidence
                CurrentData.EstimatedPitStopTime = _sessionStats.AverageTotalPitTime;
                usingHistoricalData = true;
                LogDebug($"PIT STOP ESTIMATE (HISTORICAL - HIGH CONFIDENCE): {CurrentData.EstimatedPitStopTime:F1}s from {_sessionStats.PitStopsRecorded} stops");
                LogDebug($"  Current conditions: Track={telemetry.TrackTemp:F1}°C, Air={telemetry.AirTemp:F1}°C (matches historical range)");
            }
            else
            {
                // Environmental conditions differ - use formula but with historical pit speed limit if available
                float pitSpeed = _sessionStats.PitSpeedLimit > 0 ? _sessionStats.PitSpeedLimit : 16.7f;
                float trackLength = telemetry.TrackLength;
                float estimatedPitLaneLength = trackLength * 0.175f;
                float pitTransitTime = estimatedPitLaneLength / pitSpeed;
                float serviceTime = _sessionStats.AverageRefuelServiceTime > 0 ? _sessionStats.AverageRefuelServiceTime : 7.0f;
                CurrentData.EstimatedPitStopTime = pitTransitTime + serviceTime;
                
                LogDebug($"PIT STOP ESTIMATE (FORMULA - MODERATE CONFIDENCE): {CurrentData.EstimatedPitStopTime:F1}s (conditions differ)");
                LogDebug($"  Current: Track={telemetry.TrackTemp:F1}°C, Air={telemetry.AirTemp:F1}°C");
                LogDebug($"  Historical: Track {_sessionStats.MinTrackTemp:F1}-{_sessionStats.MaxTrackTemp:F1}°C, Air {_sessionStats.MinAirTemp:F1}-{_sessionStats.MaxAirTemp:F1}°C");
            }
        }
        else
        {
            // No historical data - use conservative default formula
            // Conservative: Assume slower pit speed (15 m/s instead of 16.7) and longer service time (8s instead of 7s)
            float trackLength = telemetry.TrackLength;
            float pitSpeed = telemetry.TrackPitSpeedLimit > 0 ? telemetry.TrackPitSpeedLimit : 15.0f; // Use parsed limit or conservative 54 kph
            float estimatedPitLaneLength = trackLength * 0.175f; // ~17.5% of track length
            float pitTransitTime = estimatedPitLaneLength / pitSpeed; // seconds in pit lane
            float serviceTime = 8.0f; // Conservative service time (fuel only, no tire change)
            CurrentData.EstimatedPitStopTime = pitTransitTime + serviceTime;
            
            LogDebug($"PIT STOP ESTIMATE (CONSERVATIVE DEFAULT - LOW CONFIDENCE): {CurrentData.EstimatedPitStopTime:F1}s");
            LogDebug($"  Track={trackLength:F0}m, PitLane={estimatedPitLaneLength:F0}m, PitSpeed={pitSpeed:F1}m/s, Transit={pitTransitTime:F1}s, Service={serviceTime:F1}s");
            LogDebug($"  Using conservative defaults (no historical data available)");
        }
        
        // Total pit stop time loss (includes entry/exit)
        CurrentData.PitStopTimeLoss = CurrentData.EstimatedPitStopTime;
        
        if (CurrentData.NeedsFuelSaving)
        {
            // Calculate target fuel reduction per lap
            CurrentData.FuelSavingTarget = fuelDeficit / lapsRemaining;
            
            // Calculate target lap time (realistic fuel saving model)
            // FIXED: Changed from 0.5f to 0.8f for realistic lap time impact
            // Real-world: 1% fuel saved = 0.8% slower lap (lifting/coasting/reduced throttle)
            // Old formula was too optimistic: 10% fuel save = 5% slower (unrealistic)
            // New formula more accurate: 10% fuel save = 8% slower (realistic)
            float fuelReductionPct = (CurrentData.FuelSavingTarget / avgFuel) * 100f;
            float targetLapTimeIncrease = fuelReductionPct * 0.8f; // 0.8% slower for every 1% fuel saved
            CurrentData.TargetLapTime = CurrentData.AverageLapTime * (1 + (targetLapTimeIncrease / 100f));
            
            // FIX: Removed generic "Fast corners, straights" text - not useful without track-specific data
            // Track-specific lift points would require detailed track map data and analysis
            CurrentData.LiftPoints = null; // Hide generic advice
            
            // FIX #3: Calculate current saving rate based on recent trend (L5), not just last lap
            // Compare current L5 average to baseline average to see if saving is happening
            float currentL5 = CurrentData.AvgFuelPerLap_L5;
            if (currentL5 > 0 && currentL5 < avgFuel)
            {
                // Current L5 is LOWER than baseline = successfully saving fuel
                CurrentData.CurrentSavingRate = avgFuel - currentL5;
            }
            else if (CurrentData.FuelUsedLastLap > 0 && CurrentData.FuelUsedLastLap < avgFuel)
            {
                // Fallback: last lap was lower than baseline
                CurrentData.CurrentSavingRate = avgFuel - CurrentData.FuelUsedLastLap;
            }
            else
            {
                // Not saving fuel yet (current usage >= baseline)
                CurrentData.CurrentSavingRate = 0;
            }
            
            // Calculate saving progress (0-100%)
            if (CurrentData.FuelSavingTarget > 0)
            {
                CurrentData.SavingProgress = Math.Clamp((CurrentData.CurrentSavingRate / CurrentData.FuelSavingTarget) * 100f, 0f, 100f);
            }
            else
            {
                CurrentData.SavingProgress = 0;
            }
            
            // Project fuel delta if current saving rate continues
            float projectedFuelUsage = (avgFuel - CurrentData.CurrentSavingRate) * lapsRemaining;
            CurrentData.ProjectedFuelDelta = currentFuel - projectedFuelUsage - sputteringThreshold;
            
            // Determine if fuel saving is working
            CurrentData.FuelSavingWorking = CurrentData.ProjectedFuelDelta >= 0;
            
            // ===== STRATEGY COMPARISON: PIT STOP vs FUEL SAVING =====
            
            // Calculate fuel saving time loss (slower lap times × laps remaining)
            float normalLapTime = CurrentData.AverageLapTime;
            float savingLapTime = CurrentData.TargetLapTime;
            float lapTimePenalty = savingLapTime - normalLapTime; // seconds slower per lap
            CurrentData.FuelSavingTimeLoss = lapTimePenalty * lapsRemaining;
            
            // Check if fuel saving is physically possible
            // Maximum realistic fuel saving: 15% per lap (e.g., 1.39L → 1.18L = 0.21L saving)
            float maxRealisticSaving = avgFuel * 0.15f;
            CurrentData.CanSaveFuelToFinish = CurrentData.FuelSavingTarget <= maxRealisticSaving;
            
            // Compare strategies: Pit stop time vs fuel saving time
            CurrentData.IsPittingFaster = CurrentData.PitStopTimeLoss < CurrentData.FuelSavingTimeLoss;
            CurrentData.StrategyTimeDelta = CurrentData.FuelSavingTimeLoss - CurrentData.PitStopTimeLoss;
            
            LogDebug($"STRATEGY COMPARISON:");
            LogDebug($"  Fuel Saving: {lapTimePenalty:F2}s/lap × {lapsRemaining} laps = {CurrentData.FuelSavingTimeLoss:F1}s total");
            LogDebug($"  Pit Stop: {CurrentData.PitStopTimeLoss:F1}s");
            LogDebug($"  Time Delta: {CurrentData.StrategyTimeDelta:F1}s ({(CurrentData.IsPittingFaster ? "PIT FASTER" : "SAVE FUEL FASTER")})");
            LogDebug($"  Can Save Fuel: {CurrentData.CanSaveFuelToFinish} (target={CurrentData.FuelSavingTarget:F3}L, max realistic={maxRealisticSaving:F3}L)");
            
            // Generate strategic alerts
            GenerateStrategicAlerts();
            
            LogDebug($"FUEL SAVING MODE: Target={CurrentData.FuelSavingTarget:F3}L/lap, Current={CurrentData.CurrentSavingRate:F3}L/lap, Progress={CurrentData.SavingProgress:F1}%, Projected={CurrentData.ProjectedFuelDelta:F2}L");
        }
        else
        {
            // No fuel saving needed - we can finish
            CurrentData.FuelSavingTarget = 0;
            CurrentData.CurrentSavingRate = 0;
            CurrentData.TargetLapTime = 0;
            CurrentData.SavingProgress = 0;
            CurrentData.FuelSavingWorking = true; // Not needed = working
            CurrentData.ProjectedFuelDelta = -fuelDeficit; // Surplus
            CurrentData.StrategicAlert = null;
            CurrentData.AlertSeverity = 0;
        }
        
        // Calculate optimal pit lap (minimize time loss based on track position)
        CalculateOptimalPitLap(telemetry);
        
        // Calculate multi-stop strategy comparison (Phase 5)
        CalculateMultiStopStrategy(telemetry);
    }
    
    /// <summary>
    /// Generate strategic alerts based on fuel saving status and pit strategy comparison
    /// </summary>
    private void GenerateStrategicAlerts()
    {
        // Critical: Pit this lap (fuel too low) - check BEFORE running out!
        // FIX: Check at LapsRemaining < 1.2 to give warning BEFORE the fuel runs out, not after
        if (CurrentData.LapsRemaining < 1.2f && !CurrentData.CanFinishWithoutStop)
        {
            CurrentData.StrategicAlert = "⚠️ PIT THIS LAP - CRITICAL FUEL";
            CurrentData.AlertSeverity = 3;
            LogDebug("STRATEGIC ALERT: PIT THIS LAP");
            return;
        }
        
        // ===== FUEL SAVING IMPOSSIBLE → RECOMMEND PIT STRATEGY =====
        if (CurrentData.NeedsFuelSaving && !CurrentData.CanSaveFuelToFinish)
        {
            // Fuel saving target exceeds realistic capability (>15% reduction)
            // Recommend optimal pit stop strategy instead
            CurrentData.StrategicAlert = $"🔧 PIT LAP {CurrentData.OptimalPitLap}: Add {CurrentData.FuelToAddAtPit:F1}L (saving {CurrentData.FuelSavingTarget:F2}L/lap impossible)";
            CurrentData.AlertSeverity = 2;
            LogDebug($"STRATEGIC ALERT: Fuel saving impossible, recommend pit on lap {CurrentData.OptimalPitLap}");
            return;
        }
        
        // ===== PIT STOP FASTER THAN FUEL SAVING =====
        if (CurrentData.NeedsFuelSaving && CurrentData.IsPittingFaster && Math.Abs(CurrentData.StrategyTimeDelta) > 5.0f)
        {
            // Pitting is >5 seconds faster than fuel saving
            CurrentData.StrategicAlert = $"🔧 PIT FASTER: Lap {CurrentData.OptimalPitLap} saves {Math.Abs(CurrentData.StrategyTimeDelta):F0}s vs fuel saving";
            CurrentData.AlertSeverity = 2;
            LogDebug($"STRATEGIC ALERT: Pit stop {CurrentData.PitStopTimeLoss:F1}s vs fuel saving {CurrentData.FuelSavingTimeLoss:F1}s = pit {Math.Abs(CurrentData.StrategyTimeDelta):F1}s faster");
            return;
        }
        
        // Warning: Not saving enough fuel
        if (CurrentData.NeedsFuelSaving && CurrentData.SavingProgress < 50f && CurrentData.LapsRemaining > 5)
        {
            float needsMore = CurrentData.FuelSavingTarget - CurrentData.CurrentSavingRate;
            CurrentData.StrategicAlert = $"🔴 INCREASE SAVING: Need {needsMore:F2}L more per lap";
            CurrentData.AlertSeverity = 2;
            LogDebug($"STRATEGIC ALERT: Increase saving by {needsMore:F3}L/lap");
            return;
        }
        
        // Info: Fuel saving working (and faster than pitting)
        if (CurrentData.NeedsFuelSaving && CurrentData.FuelSavingWorking)
        {
            if (!CurrentData.IsPittingFaster)
            {
                // Fuel saving is faster strategy
                CurrentData.StrategicAlert = $"✅ FUEL SAVING WORKING: {CurrentData.ProjectedFuelDelta:F1}L surplus (saves {Math.Abs(CurrentData.StrategyTimeDelta):F0}s vs pit)";
            }
            else
            {
                // Fuel saving working but pitting would be faster (marginal difference <5s)
                CurrentData.StrategicAlert = $"✅ FUEL SAVING WORKING: {CurrentData.ProjectedFuelDelta:F1}L surplus projected";
            }
            CurrentData.AlertSeverity = 1;
            LogDebug($"STRATEGIC ALERT: Fuel saving working, projected surplus {CurrentData.ProjectedFuelDelta:F2}L");
            return;
        }
        
        // Warning: Need to start saving fuel (and it's the faster strategy)
        if (CurrentData.NeedsFuelSaving && !CurrentData.FuelSavingWorking && !CurrentData.IsPittingFaster)
        {
            CurrentData.StrategicAlert = $"💡 SAVE {CurrentData.FuelSavingTarget:F2}L/LAP TO FINISH (saves {Math.Abs(CurrentData.StrategyTimeDelta):F0}s vs pit)";
            CurrentData.AlertSeverity = 2;
            LogDebug($"STRATEGIC ALERT: Need to save {CurrentData.FuelSavingTarget:F3}L/lap");
            return;
        }
        
        // No alert needed
        CurrentData.StrategicAlert = null;
        CurrentData.AlertSeverity = 0;
    }
    
    /// <summary>
    /// Calculate optimal pit lap to minimize time loss (Phase 5.B Enhanced)
    /// Multi-factor optimization: fuel criticality, dynamic track position cost, yellow flag prediction, pit window
    /// </summary>
    private void CalculateOptimalPitLap(TelemetryData telemetry)
    {
        if (CurrentData.RaceLapsRemaining <= 0 || CurrentData.CanFinishWithoutStop)
        {
            CurrentData.OptimalPitLap = 0;
            CurrentData.OptimalPitReason = null;
            CurrentData.EarliestPitLap = 0;
            CurrentData.LatestPitLap = 0;
            CurrentData.PitWindowStart = 0;
            CurrentData.PitWindowEnd = 0;
            CurrentData.PitWindowReason = null;
            return;
        }
        
        float avgFuel = CurrentData.AvgFuelPerLap_L5;
        if (avgFuel <= 0)
        {
            CurrentData.OptimalPitLap = 0;
            CurrentData.OptimalPitReason = null;
            return;
        }
        
        int currentLap = telemetry.LapsCompleted;
        float lapsOnCurrentFuel = CurrentData.LapsRemaining;
        int raceLapsRemaining = CurrentData.RaceLapsRemaining;
        
        // ===== SECTION 1: FUEL CRITICALITY ANALYSIS =====
        // Score 0-100 based on laps of fuel remaining
        float fuelCriticality;
        if (lapsOnCurrentFuel < 1.0f)
            fuelCriticality = 100f; // CRITICAL
        else if (lapsOnCurrentFuel < 2.0f)
            fuelCriticality = 80f + (2f - lapsOnCurrentFuel) * 20f; // 80-100: URGENT
        else if (lapsOnCurrentFuel < 5.0f)
            fuelCriticality = 50f + (5f - lapsOnCurrentFuel) * 10f; // 50-80: MODERATE
        else if (lapsOnCurrentFuel < 10.0f)
            fuelCriticality = 20f + (10f - lapsOnCurrentFuel) * 6f; // 20-50: COMFORTABLE
        else
            fuelCriticality = Math.Max(0f, 20f - (lapsOnCurrentFuel - 10f)); // 0-20: PLENTY
        
        CurrentData.FuelCriticalityScore = fuelCriticality;
        LogDebug($"FUEL CRITICALITY: {fuelCriticality:F1}/100 ({lapsOnCurrentFuel:F1} laps remaining)");
        
        // ===== SECTION 2: DYNAMIC TRACK POSITION COST =====
        // Calculate field percentile for dynamic position cost (scales to any field size)
        int totalCars = 0;
        int playerPosition = 0;
        
        if (telemetry.CarIdxPosition != null && telemetry.CarIdxPosition.Length > 0)
        {
            // Count total active cars (position > 0)
            totalCars = telemetry.CarIdxPosition.Count(p => p > 0);
            
            // Get player position
            if (telemetry.PlayerCarIdx >= 0 && telemetry.PlayerCarIdx < telemetry.CarIdxPosition.Length)
            {
                playerPosition = telemetry.CarIdxPosition[telemetry.PlayerCarIdx];
            }
        }
        
        CurrentData.RacePosition = playerPosition;
        CurrentData.TotalCars = totalCars;
        
        // Calculate percentile-based position cost
        float positionCost = 15f; // Default moderate cost
        if (totalCars > 1 && playerPosition > 0)
        {
            float percentile = (float)playerPosition / totalCars;
            
            if (percentile <= 0.10f)        // Top 10% (P1-P4 in 40-car race)
                positionCost = 25f;          // Preserve position at all costs
            else if (percentile <= 0.25f)   // Top 25% (P5-P10 in 40-car)
                positionCost = 20f;          // High value position
            else if (percentile <= 0.50f)   // Top 50% (P11-P20 in 40-car)
                positionCost = 15f;          // Moderate cost
            else if (percentile <= 0.75f)   // 50-75% (P21-P30 in 40-car)
                positionCost = 10f;          // Low cost
            else                             // Bottom 25% (P31+ in 40-car)
                positionCost = 5f;           // Undercut opportunity
            
            LogDebug($"TRACK POSITION: P{playerPosition}/{totalCars} ({percentile*100:F0}th percentile) = {positionCost}s cost");
        }
        else
        {
            LogDebug($"TRACK POSITION: Unknown (using default {positionCost}s cost)");
        }
        
        CurrentData.TrackPositionCost = positionCost;
        
        // ===== SECTION 3: YELLOW FLAG PROBABILITY PREDICTION =====
        bool yellowExpected = false;
        int lapsUntilYellow = 0;
        float yellowProbability = 0f;
        
        if (CurrentData.YellowFlagLapCount > 0 && _lapHistory.Count > 10)
        {
            // Calculate average laps between yellows
            int totalLaps = _lapHistory.Count;
            float yellowFrequency = (float)totalLaps / CurrentData.YellowFlagLapCount;
            
            // Find laps since last yellow
            var lastYellowLap = _lapHistory.LastOrDefault(l => l.IsYellowFlagLap);
            int lapsSinceLastYellow = lastYellowLap != null ? (currentLap - lastYellowLap.LapNumber) : totalLaps;
            
            // Predict yellow if we've passed 80% of typical frequency
            yellowProbability = Math.Min(1.0f, lapsSinceLastYellow / yellowFrequency);
            yellowExpected = yellowProbability > 0.8f;
            
            if (yellowExpected)
            {
                lapsUntilYellow = (int)Math.Ceiling(yellowFrequency - lapsSinceLastYellow);
            }
            
            LogDebug($"YELLOW FLAG PREDICTION: Frequency={yellowFrequency:F1} laps, Since last={lapsSinceLastYellow}, Probability={yellowProbability*100:F0}%, Expected={yellowExpected}");
        }
        
        CurrentData.YellowFlagExpected = yellowExpected;
        CurrentData.LapsUntilYellow = lapsUntilYellow;
        CurrentData.YellowFlagProbability = yellowProbability;
        
        // ===== SECTION 4: PIT WINDOW CALCULATION =====
        // Calculate earliest/optimal/latest pit laps based on fuel needs and race length
        
        // Earliest: When we've used enough fuel to add race-ending fuel (with buffer)
        float fuelNeededToFinish = (raceLapsRemaining + CurrentData.FuelBufferLaps) * avgFuel;
        float fuelToUse = CurrentData.TankCapacity - fuelNeededToFinish;
        int lapsToUseExcessFuel = fuelToUse > 0 ? (int)Math.Floor(fuelToUse / avgFuel) : 0;
        CurrentData.EarliestPitLap = currentLap + Math.Max(1, lapsToUseExcessFuel);
        
        // Latest: Just before running out (with 1 lap safety buffer)
        CurrentData.LatestPitLap = currentLap + Math.Max(1, (int)Math.Floor(lapsOnCurrentFuel) - 1);
        
        // Optimal pit window: 3-5 lap green zone in middle of earliest/latest
        int midPoint = (CurrentData.EarliestPitLap + CurrentData.LatestPitLap) / 2;
        CurrentData.PitWindowStart = Math.Max(CurrentData.EarliestPitLap, midPoint - 2);
        CurrentData.PitWindowEnd = Math.Min(CurrentData.LatestPitLap, midPoint + 2);
        
        LogDebug($"PIT WINDOW: Earliest=L{CurrentData.EarliestPitLap}, Optimal=L{CurrentData.PitWindowStart}-L{CurrentData.PitWindowEnd}, Latest=L{CurrentData.LatestPitLap}");
        
        // ===== SECTION 5: STRATEGIC DECISION TREE =====
        int optimalPitLap;
        string pitReason;
        
        // Priority 1: Critical fuel (< 1.5 laps) - PIT IMMEDIATELY
        // ===== STRATEGIC DECISION TREE: PRIORITY-BASED PIT TIMING =====
        // Priority 0: ONE LAP TO GREEN (HIGHEST PRIORITY!)
        // This is THE most critical pit timing decision in racing - pit on pace lap before restart
        if ((_currentFlagStatus == LapFlagStatus.OneLapToGreen || _currentFlagStatus == LapFlagStatus.GreenHeld) 
            && lapsOnCurrentFuel > 2f)
        {
            optimalPitLap = currentLap; // Pit THIS lap (on pace lap) - NOT next lap!
            pitReason = "🟢 ONE LAP TO GREEN - PIT NOW before restart!";
            CurrentData.PitWindowReason = "Critical restart window - pit under yellow saves 10-20s";
            LogDebug($"ONE LAP TO GREEN DETECTED: Recommending immediate pit (saves position loss on restart)");
        }
        // Priority 1: Critical Fuel (Criticality > 95)
        else if (fuelCriticality > 95f)
        {
            optimalPitLap = currentLap; // Pit THIS lap - NOT next lap!
            pitReason = $"CRITICAL FUEL ({lapsOnCurrentFuel:F1} laps) - PIT NOW!";
            CurrentData.PitWindowReason = "Critical fuel - no pit window";
        }
        // Priority 2: Under yellow with low fuel (< 10 laps) - SEIZE OPPORTUNITY
        else if (CurrentData.IsUnderYellow && lapsOnCurrentFuel < 10f)
        {
            optimalPitLap = currentLap; // Pit THIS lap - NOT next lap!
            pitReason = $"Yellow flag opportunity (save {positionCost:F0}s vs green flag pit)";
            CurrentData.PitWindowReason = "Yellow flag - pit now to avoid position loss";
        }
        // Priority 3: Yellow expected soon + fuel comfortable - DELAY FOR YELLOW
        else if (yellowExpected && lapsOnCurrentFuel > 5f && lapsUntilYellow <= 5)
        {
            optimalPitLap = currentLap + Math.Max(1, lapsUntilYellow);
            pitReason = $"Yellow expected in ~{lapsUntilYellow} laps ({yellowProbability*100:F0}% probability)";
            CurrentData.PitWindowReason = $"Delay {lapsUntilYellow} laps for predicted yellow";
        }
        // Priority 4: Top position (top 25%) - PIT LATE IN WINDOW
        else if (positionCost >= 20f)
        {
            optimalPitLap = CurrentData.PitWindowEnd; // Maximize track time in good position
            pitReason = $"Top position (P{playerPosition}) - pit late to preserve track time";
            CurrentData.PitWindowReason = $"High position cost ({positionCost}s) - extend stint";
        }
        // Priority 5: Back of pack (bottom 50%) - PIT EARLY FOR UNDERCUT
        else if (positionCost <= 10f && CurrentData.PitWindowStart <= CurrentData.LatestPitLap - 3)
        {
            optimalPitLap = CurrentData.PitWindowStart; // Early pit for undercut
            pitReason = $"Undercut opportunity (P{playerPosition}) - pit early for fresh tire advantage";
            CurrentData.PitWindowReason = $"Low position cost ({positionCost}s) - undercut strategy";
        }
        // Default: Mid-window pit (balanced strategy)
        else
        {
            optimalPitLap = midPoint;
            pitReason = $"Balanced strategy - pit at mid-window (L{CurrentData.PitWindowStart}-{CurrentData.PitWindowEnd})";
            CurrentData.PitWindowReason = "Standard pit window";
        }
        
        // CRITICAL VALIDATION: Optimal pit lap CANNOT exceed latest pit lap (fuel constraint)
        // Strategic decisions must respect fuel reality - can't pit at lap 16 if fuel runs out at lap 10!
        if (optimalPitLap > CurrentData.LatestPitLap)
        {
            LogDebug($"⚠️ FUEL CONSTRAINT OVERRIDE: Strategic choice L{optimalPitLap} exceeds fuel limit L{CurrentData.LatestPitLap} - adjusting to latest safe lap");
            optimalPitLap = CurrentData.LatestPitLap;
            pitReason = $"Fuel limited - must pit by L{optimalPitLap} (originally wanted {pitReason})";
            CurrentData.PitWindowReason = "Fuel constraint override";
        }
        
        CurrentData.OptimalPitLap = optimalPitLap;
        CurrentData.OptimalPitReason = pitReason;
        
        // ===== SECTION 5D: PIT EXIT POSITION PREDICTION =====
        // Calculate projected position after pit stop with class filtering
        CalculatePitExitPosition(telemetry);
        
        // ===== SECTION 6: PIT DELTA ESTIMATION (NOW VS LATER) =====
        // Compare: Pitting now vs pitting at optimal lap
        int lapsUntilOptimalPit = Math.Max(0, optimalPitLap - currentLap);
        
        // Fuel weight penalty: More fuel = slower lap times (estimated ~0.03s per lap per liter)
        float currentFuelWeight = CurrentData.CurrentFuel * 0.03f; // seconds per lap
        float optimalFuelWeight = (CurrentData.CurrentFuel - (lapsUntilOptimalPit * avgFuel)) * 0.03f;
        float fuelWeightAdvantage = (currentFuelWeight - optimalFuelWeight) * lapsUntilOptimalPit;
        
        // Position cost: Pitting now under green vs pitting under yellow/late
        float positionAdvantage = CurrentData.IsUnderYellow ? positionCost : 0f;
        
        // Net delta: Positive = better to pit now, Negative = better to wait
        CurrentData.PitDeltaNowVsLater = positionAdvantage - fuelWeightAdvantage;
        CurrentData.PitDeltaComparisonLaps = lapsUntilOptimalPit;
        
        LogDebug($"STRATEGIC DECISION: Optimal=L{optimalPitLap}, Reason={pitReason}");
        LogDebug($"PIT DELTA: Now vs L{optimalPitLap} = {CurrentData.PitDeltaNowVsLater:F1}s (Position:{positionAdvantage:F1}s, FuelWeight:{-fuelWeightAdvantage:F1}s)");
    }
    
    /// <summary>
    /// Calculate projected position after pit stop with class filtering (Phase 5D)
    /// Uses CarIdxF2Time gaps, pit stop time, and filters by player's car class
    /// </summary>
    private void CalculatePitExitPosition(TelemetryData telemetry)
    {
        // Reset pit exit data
        CurrentData.PitExitPosition = 0;
        CurrentData.PitExitGapDescription = null;
        CurrentData.PitExitPositionValid = false;
        
        // Validation: Need position arrays, pit time data, and car numbers
        if (telemetry.CarIdxPosition == null || telemetry.CarIdxF2Time == null ||
            telemetry.CarIdxClass == null || telemetry.CarIdxToCarNumber == null)
        {
            LogDebug("PIT EXIT: Missing telemetry arrays");
            return;
        }
        
        // Get pit stop time from SessionPersistenceService
        var sessionStats = _persistenceService?.GetStatistics(telemetry.TrackName, telemetry.PlayerCarClass);
        float pitStopTime = sessionStats?.AverageTotalPitTime ?? 30f; // Default 30s if no historical data
        
        if (pitStopTime <= 0)
        {
            LogDebug("PIT EXIT: Invalid pit stop time");
            return;
        }
        
        int playerCarIdx = telemetry.PlayerCarIdx;
        int playerClass = telemetry.PlayerCarClass;
        int playerPosition = CurrentData.RacePosition;
        
        if (playerPosition <= 0 || playerCarIdx < 0)
        {
            LogDebug("PIT EXIT: Invalid player position or car index");
            return;
        }
        
        // Get player's gap to leader (F2Time)
        float playerGapToLeader = telemetry.CarIdxF2Time[playerCarIdx];
        
        // Calculate player's projected time after pit stop
        float playerProjectedTime = playerGapToLeader + pitStopTime;
        
        // Find cars in player's class and calculate exit position
        var classCarData = new List<(int carIdx, int position, float gapToLeader, string carNumber)>();
        
        for (int i = 0; i < telemetry.CarIdxPosition.Length; i++)
        {
            int pos = telemetry.CarIdxPosition[i];
            
            // Skip invalid positions, player, and other classes
            if (pos <= 0 || i == playerCarIdx)
                continue;
                
            // CLASS FILTERING: Only include cars in player's class
            if (telemetry.CarIdxClass[i] != playerClass)
                continue;
            
            float gap = telemetry.CarIdxF2Time[i];
            string carNumber = telemetry.CarIdxToCarNumber.TryGetValue(i, out var num) ? num : $"Car{i}";
            
            classCarData.Add((i, pos, gap, carNumber));
        }
        
        // Sort by gap to leader (ascending - leader first)
        classCarData = classCarData.OrderBy(c => c.gapToLeader).ToList();
        
        // Calculate projected position
        int projectedPosition = 1; // Start at P1
        int carAheadIdx = -1;
        int carBehindIdx = -1;
        
        foreach (var car in classCarData)
        {
            if (car.gapToLeader < playerProjectedTime)
            {
                projectedPosition++;
                carAheadIdx = car.carIdx; // Track last car ahead
            }
            else
            {
                if (carBehindIdx == -1)
                    carBehindIdx = car.carIdx; // Track first car behind
            }
        }
        
        CurrentData.PitExitPosition = projectedPosition;
        CurrentData.PitExitPositionValid = true;
        
        // Build gap description: "4s ahead of #14, 10s behind #9"
        var gapParts = new List<string>();
        
        if (carAheadIdx >= 0)
        {
            float gapToCarAhead = playerProjectedTime - telemetry.CarIdxF2Time[carAheadIdx];
            string carNum = telemetry.CarIdxToCarNumber.TryGetValue(carAheadIdx, out var num) ? num : $"{carAheadIdx}";
            gapParts.Add($"{gapToCarAhead:F0}s to #{carNum}");
        }
        
        if (carBehindIdx >= 0)
        {
            float gapFromCarBehind = telemetry.CarIdxF2Time[carBehindIdx] - playerProjectedTime;
            string carNum = telemetry.CarIdxToCarNumber.TryGetValue(carBehindIdx, out var num) ? num : $"{carBehindIdx}";
            
            if (gapParts.Count > 0)
                gapParts.Add($"{gapFromCarBehind:F0}s from #{carNum}");
            else
                gapParts.Add($"{gapFromCarBehind:F0}s ahead of #{carNum}");
        }
        
        if (gapParts.Count > 0)
        {
            CurrentData.PitExitGapDescription = string.Join(", ", gapParts);
        }
        
        // ===== PIT STATUS DETECTION (Option B) =====
        // Detect cars currently on pit road (class-filtered)
        CurrentData.CarsPitting.Clear();
        if (telemetry.CarIdxOnPitRoad != null)
        {
            for (int i = 0; i < telemetry.CarIdxOnPitRoad.Length; i++)
            {
                // Skip player and other classes
                if (i == playerCarIdx || telemetry.CarIdxClass[i] != playerClass)
                    continue;
                
                // Check if car is on pit road
                if (telemetry.CarIdxOnPitRoad[i])
                {
                    string carNum = telemetry.CarIdxToCarNumber.TryGetValue(i, out var num) ? num : $"Car{i}";
                    CurrentData.CarsPitting.Add(carNum);
                }
            }
        }
        
        // ===== CONFIDENCE SCORE CALCULATION (Option B) =====
        // Calculate confidence based on data quality and race conditions
        float confidence = 100f;
        
        // Factor 1: Pit time data quality (40% weight)
        if (pitStopTime == 30f) // Using default fallback
            confidence -= 40f;
        else if (sessionStats?.PitStopsRecorded < 3)
            confidence -= 20f; // Limited historical data
        
        // Factor 2: Position data completeness (30% weight)
        int carsInClass = classCarData.Count;
        if (carsInClass < 5)
            confidence -= 30f;
        else if (carsInClass < 10)
            confidence -= 15f;
        
        // Factor 3: Race conditions (30% weight)
        if (CurrentData.IsUnderYellow)
            confidence -= 30f; // Unpredictable during yellow
        else if (telemetry.LapsCompleted < 3)
            confidence -= 20f; // Start/restart chaos
        
        CurrentData.PitExitConfidenceScore = Math.Max(0, confidence);
        
        // Set confidence icon
        CurrentData.PitExitConfidenceIcon = CurrentData.PitExitConfidenceScore switch
        {
            >= 80f => "🟢", // High confidence
            >= 50f => "🟡", // Medium confidence
            _ => "🔴"       // Low confidence
        };
        
        LogDebug($"PIT EXIT: P{playerPosition} → P{projectedPosition} after {pitStopTime:F1}s stop | {CurrentData.PitExitGapDescription ?? "No gaps"} | Confidence: {CurrentData.PitExitConfidenceScore:F0}% {CurrentData.PitExitConfidenceIcon} | Pitting: {CurrentData.CarsPitting.Count} cars");
    }
    
    /// <summary>
    /// Calculate multi-stop strategy comparison (1-stop, 2-stop, 3-stop) (Phase 5)
    /// Determines optimal number of stops based on fuel capacity, stint length, and pit stop time
    /// </summary>
    private void CalculateMultiStopStrategy(TelemetryData telemetry)
    {
        if (CurrentData.RaceLapsRemaining <= 0 || CurrentData.AvgFuelPerLap <= 0)
        {
            CurrentData.RecommendedStops = 0;
            CurrentData.RecommendedStopsReason = "No pit stops needed";
            return;
        }
        
        float avgFuel = CurrentData.AvgFuelPerLap;
        float avgLapTime = CurrentData.AverageLapTime > 0 ? CurrentData.AverageLapTime : 90f; // Default 90s if unknown
        int raceLapsRemaining = CurrentData.RaceLapsRemaining;
        int currentLap = telemetry.LapsCompleted;
        
        // Pit stop times (use historical data if available)
        float fuelOnlyStop = CurrentData.FuelOnlyStopTime > 0 ? CurrentData.FuelOnlyStopTime : 
                             (CurrentData.EstimatedPitStopTime > 0 ? CurrentData.EstimatedPitStopTime : 30f);
        float fuelAndTiresStop = CurrentData.FuelAndTiresStopTime > 0 ? CurrentData.FuelAndTiresStopTime : fuelOnlyStop + 10f;
        
        CurrentData.FuelOnlyStopTime = fuelOnlyStop;
        CurrentData.FuelAndTiresStopTime = fuelAndTiresStop;
        
        float tankCapacity = CurrentData.TankCapacity;
        float maxStintLaps = tankCapacity / avgFuel;
        
        // ===== 1-STOP STRATEGY =====
        if (raceLapsRemaining <= maxStintLaps * 2)
        {
            // 1-stop is possible
            float stint1Laps = Math.Min(CurrentData.LapsRemaining, raceLapsRemaining / 2f);
            CurrentData.OneStopPitLap = currentLap + (int)Math.Floor(stint1Laps);
            
            float stint2Laps = raceLapsRemaining - stint1Laps;
            CurrentData.OneStopFuelToAdd = Math.Min(tankCapacity, stint2Laps * avgFuel + CurrentData.FuelBufferLaps * avgFuel);
            
            // Total time: Racing laps + 1 pit stop
            CurrentData.OneStopTotalTime = (raceLapsRemaining * avgLapTime) + fuelOnlyStop;
            
            LogDebug($"1-STOP: Pit L{CurrentData.OneStopPitLap}, Add {CurrentData.OneStopFuelToAdd:F1}L, Time: {CurrentData.OneStopTotalTime:F0}s");
        }
        else
        {
            CurrentData.OneStopTotalTime = float.MaxValue; // Not possible
        }
        
        // ===== 2-STOP STRATEGY =====
        if (raceLapsRemaining <= maxStintLaps * 3)
        {
            // 2-stop is possible
            float lapsPerStint = raceLapsRemaining / 3f;
            CurrentData.TwoStopPit1Lap = currentLap + (int)Math.Floor(Math.Min(CurrentData.LapsRemaining, lapsPerStint));
            CurrentData.TwoStopPit2Lap = CurrentData.TwoStopPit1Lap + (int)Math.Floor(lapsPerStint);
            CurrentData.TwoStopFuelPerStint = Math.Min(tankCapacity, lapsPerStint * avgFuel);
            
            // Total time: Racing laps + 2 pit stops
            CurrentData.TwoStopTotalTime = (raceLapsRemaining * avgLapTime) + (fuelOnlyStop * 2);
            
            LogDebug($"2-STOP: Pit L{CurrentData.TwoStopPit1Lap} & L{CurrentData.TwoStopPit2Lap}, Fuel: {CurrentData.TwoStopFuelPerStint:F1}L/stint, Time: {CurrentData.TwoStopTotalTime:F0}s");
        }
        else
        {
            CurrentData.TwoStopTotalTime = float.MaxValue;
        }
        
        // ===== 3-STOP STRATEGY (Endurance) =====
        if (raceLapsRemaining > maxStintLaps * 3)
        {
            // 3-stop required or optimal
            float lapsPerStint = raceLapsRemaining / 4f;
            int pit1 = currentLap + (int)Math.Floor(Math.Min(CurrentData.LapsRemaining, lapsPerStint));
            int pit2 = pit1 + (int)Math.Floor(lapsPerStint);
            int pit3 = pit2 + (int)Math.Floor(lapsPerStint);
            
            CurrentData.ThreeStopPitLaps = $"{pit1},{pit2},{pit3}";
            CurrentData.ThreeStopFuelPerStint = Math.Min(tankCapacity, lapsPerStint * avgFuel);
            
            // Total time: Racing laps + 3 pit stops
            CurrentData.ThreeStopTotalTime = (raceLapsRemaining * avgLapTime) + (fuelOnlyStop * 3);
            
            LogDebug($"3-STOP: Pit L{pit1}, L{pit2}, L{pit3}, Fuel: {CurrentData.ThreeStopFuelPerStint:F1}L/stint, Time: {CurrentData.ThreeStopTotalTime:F0}s");
        }
        else
        {
            CurrentData.ThreeStopTotalTime = float.MaxValue;
        }
        
        // ===== DETERMINE RECOMMENDED STRATEGY =====
        float[] strategyTimes = { CurrentData.OneStopTotalTime, CurrentData.TwoStopTotalTime, CurrentData.ThreeStopTotalTime };
        float bestTime = strategyTimes.Min();
        
        if (bestTime == float.MaxValue)
        {
            CurrentData.RecommendedStops = 0;
            CurrentData.RecommendedStopsReason = "Can finish without pit stop";
        }
        else if (bestTime == CurrentData.OneStopTotalTime)
        {
            CurrentData.RecommendedStops = 1;
            CurrentData.RecommendedStopsReason = $"1-stop optimal (Total: {bestTime / 60:F1} min)";
        }
        else if (bestTime == CurrentData.TwoStopTotalTime)
        {
            CurrentData.RecommendedStops = 2;
            float delta = CurrentData.TwoStopTotalTime - CurrentData.OneStopTotalTime;
            if (delta < 5f) // Very close
                CurrentData.RecommendedStopsReason = $"2-stop marginal ({delta:F1}s faster than 1-stop)";
            else
                CurrentData.RecommendedStopsReason = $"2-stop optimal ({delta:F0}s faster than 1-stop)";
        }
        else // 3-stop
        {
            CurrentData.RecommendedStops = 3;
            float delta = CurrentData.ThreeStopTotalTime - CurrentData.TwoStopTotalTime;
            if (delta < 5f)
                CurrentData.RecommendedStopsReason = $"3-stop marginal ({delta:F1}s faster than 2-stop)";
            else
                CurrentData.RecommendedStopsReason = $"3-stop required (long race)";
        }
        
        // Time difference between best and second-best
        var sortedTimes = strategyTimes.Where(t => t < float.MaxValue).OrderBy(t => t).ToList();
        if (sortedTimes.Count >= 2)
        {
            CurrentData.StrategyTimeDifference = sortedTimes[1] - sortedTimes[0];
        }
        
        LogDebug($"STRATEGY RECOMMENDATION: {CurrentData.RecommendedStops} stops - {CurrentData.RecommendedStopsReason}");

        // ENHANCEMENT: Partial refuel optimization (calculate optimal fuel load)
        CalculatePartialRefuelOptimization(raceLapsRemaining, avgFuel, avgLapTime);
    }

    /// <summary>
    /// Calculate partial refuel optimization (ENHANCEMENT)
    /// Determines if partial refuel is faster than full tank based on fuel weight penalty
    /// Sometimes adding less fuel is faster overall due to lighter car weight
    /// </summary>
    private void CalculatePartialRefuelOptimization(int lapsRemaining, float avgFuel, float avgLapTime)
    {
        if (lapsRemaining <= 0 || CurrentData.CanFinishWithoutStop || avgFuel <= 0)
            return;

        // Calculate exact fuel needed to finish (with sputtering threshold, no buffer for optimization)
        float fuelToFinish = lapsRemaining * avgFuel + CurrentData.FuelSputteringThreshold;
        float fuelDeficit = fuelToFinish - CurrentData.CurrentFuel;

        if (fuelDeficit <= 0)
            return; // Already have enough fuel

        // Get fuel flow rate from session stats (or use default)
        float fuelFlowRate = _sessionStats?.AverageFuelFlowRate ?? 2.5f; // L/s

        // Car-specific fuel weight penalty (kg per liter affects lap time)
        // Formula car: ~0.06s/lap per liter, GT3: ~0.03s/lap per liter, NASCAR: ~0.015s/lap per liter
        // Using conservative 0.03s/lap per liter as default
        float fuelWeightPenalty = 0.03f; // seconds per lap per liter

        // === OPTION 1: FULL TANK ===
        float fullTankFuel = Math.Min(CurrentData.TankCapacity, CurrentData.TankCapacity - CurrentData.CurrentFuel);
        float fullTankRefuelTime = fullTankFuel / fuelFlowRate;
        float fullTankAvgWeight = (CurrentData.CurrentFuel + fullTankFuel) / 2f; // Average weight during stint
        float fullTankWeightPenalty = fullTankAvgWeight * fuelWeightPenalty * lapsRemaining;
        float fullTankTotalLoss = fullTankRefuelTime + fullTankWeightPenalty;

        // === OPTION 2: PARTIAL REFUEL (exact amount needed) ===
        float partialFuel = fuelDeficit;
        float partialRefuelTime = partialFuel / fuelFlowRate;
        float partialAvgWeight = (CurrentData.CurrentFuel + partialFuel) / 2f;
        float partialWeightPenalty = partialAvgWeight * fuelWeightPenalty * lapsRemaining;
        float partialTotalLoss = partialRefuelTime + partialWeightPenalty;

        // Compare strategies
        float timeSaved = fullTankTotalLoss - partialTotalLoss;

        if (timeSaved > 2f) // Partial refuel saves >2 seconds
        {
            CurrentData.FuelToAddAtPit = partialFuel;
            CurrentData.AlternativeStrategyInfo =
                $"Partial fill ({partialFuel:F1}L) saves {timeSaved:F1}s vs full tank ({fullTankFuel:F1}L)";
            LogDebug($"PARTIAL REFUEL: Add {partialFuel:F1}L (saves {timeSaved:F1}s vs full {fullTankFuel:F1}L)");
            LogDebug($"  Partial: Refuel {partialRefuelTime:F1}s + Weight {partialWeightPenalty:F1}s = {partialTotalLoss:F1}s");
            LogDebug($"  Full: Refuel {fullTankRefuelTime:F1}s + Weight {fullTankWeightPenalty:F1}s = {fullTankTotalLoss:F1}s");
        }
        else if (timeSaved < -2f) // Full tank is faster
        {
            CurrentData.AlternativeStrategyInfo =
                $"Full tank faster (saves {Math.Abs(timeSaved):F1}s vs partial {partialFuel:F1}L)";
            LogDebug($"FULL TANK: Better strategy (saves {Math.Abs(timeSaved):F1}s vs partial {partialFuel:F1}L)");
        }
        else // Marginal difference (<2s)
        {
            CurrentData.AlternativeStrategyInfo =
                $"Partial vs full marginal ({Math.Abs(timeSaved):F1}s difference)";
        }
    }

    /// <summary>
    /// Update fuel pressure tracking and establish baseline (Enhanced Phase 2.1)
    /// </summary>
    private void UpdateFuelPressureTracking(TelemetryData telemetry)
    {
        float currentPressure = telemetry.FuelPress;
        
        // Establish baseline pressure from first few laps (when fuel tank is full)
        if (!_baselineFuelPressureEstablished && _lapHistory.Count < BASELINE_LAPS_NEEDED)
        {
            // Only track pressure when fuel is above 80% (ensures fuel pump is fully submerged)
            if (telemetry.FuelLevelPct > 0.8f && currentPressure > 0)
            {
                _fuelPressureHistory.Add(currentPressure);
            }
            
            // Once we have enough samples, calculate baseline
            if (_fuelPressureHistory.Count >= 5)
            {
                // Use median to avoid outliers from sensor noise
                var sortedPressures = _fuelPressureHistory.OrderBy(p => p).ToList();
                CurrentData.BaselineFuelPressure = sortedPressures[sortedPressures.Count / 2];
                _baselineFuelPressureEstablished = true;
                LogDebug($"BASELINE FUEL PRESSURE ESTABLISHED: {CurrentData.BaselineFuelPressure:F2} bar");
            }
        }
        
        // Calculate pressure drop if baseline established
        if (_baselineFuelPressureEstablished && CurrentData.BaselineFuelPressure > 0)
        {
            float pressureDrop = CurrentData.BaselineFuelPressure - currentPressure;
            CurrentData.FuelPressureDropPct = (pressureDrop / CurrentData.BaselineFuelPressure) * 100f;
            
            // Warning threshold: >10% drop from baseline indicates low fuel risk
            CurrentData.FuelPressureLow = CurrentData.FuelPressureDropPct > 10f;
            
            // Additional critical threshold: >20% drop = imminent sputtering
            if (CurrentData.FuelPressureDropPct > 20f)
            {
                LogDebug($"CRITICAL: Fuel pressure dropped {CurrentData.FuelPressureDropPct:F1}% from baseline ({currentPressure:F2} bar vs {CurrentData.BaselineFuelPressure:F2} bar)");
            }
            else if (CurrentData.FuelPressureLow)
            {
                LogDebug($"WARNING: Fuel pressure dropped {CurrentData.FuelPressureDropPct:F1}% from baseline ({currentPressure:F2} bar vs {CurrentData.BaselineFuelPressure:F2} bar)");
            }
        }
    }
    
    /// <summary>
    /// Update lap completion context for enhanced race position awareness (FIX #3)
    /// Accounts for: track position, leader's laps, being lapped, lap time projection
    /// CRITICAL for accurate fuel strategy in endurance races and when being lapped
    /// </summary>
    private void UpdateLapCompletionContext(TelemetryData telemetry)
    {
        // Track position percentage (0-1)
        CurrentData.TrackPositionPct = telemetry.LapDistPct;
        
        // Current lap time (lap in progress)
        CurrentData.CurrentLapTime = telemetry.LapCurrentLapTime;
        
        // Find leader's laps completed (P1 position)
        // CarIdxPosition array: position[carIdx] = position in race (1-based)
        // Need to find which car is in position 1 (leader)
        int leaderCarIdx = -1;
        if (telemetry.CarIdxPosition != null && telemetry.CarIdxPosition.Length > 0)
        {
            for (int i = 0; i < telemetry.CarIdxPosition.Length; i++)
            {
                if (telemetry.CarIdxPosition[i] == 1) // Position 1 = leader
                {
                    leaderCarIdx = i;
                    break;
                }
            }
        }
        
        if (leaderCarIdx >= 0 && telemetry.CarIdxLap != null && leaderCarIdx < telemetry.CarIdxLap.Length)
        {
            CurrentData.LeaderLapsCompleted = telemetry.CarIdxLap[leaderCarIdx];
            
            // Calculate laps behind leader (negative = lapped, 0 = on lead lap, positive = ahead)
            CurrentData.LapsBehindLeader = telemetry.LapsCompleted - CurrentData.LeaderLapsCompleted;
            
            if (CurrentData.IsBeingLapped)
            {
                LogDebug($"LAPPED: Player laps={telemetry.LapsCompleted}, Leader laps={CurrentData.LeaderLapsCompleted}, Delta={CurrentData.LapsBehindLeader}");
            }
        }
        else
        {
            // Fallback: assume on lead lap if leader not found
            CurrentData.LeaderLapsCompleted = telemetry.LapsCompleted;
            CurrentData.LapsBehindLeader = 0;
        }
        
        // Project lap completion time based on current pace
        if (CurrentData.AverageLapTime > 0 && telemetry.LapDistPct > 0.1f)
        {
            // Estimate time to complete current lap based on current pace
            CurrentData.ProjectedLapTime = CurrentData.CurrentLapTime / telemetry.LapDistPct;
            
            // Determine if lap is extending (running longer than average)
            // Allow 5% tolerance for normal variation
            float tolerance = CurrentData.AverageLapTime * 0.05f;
            CurrentData.IsExtendingLap = CurrentData.ProjectedLapTime > (CurrentData.AverageLapTime + tolerance);
            
            if (CurrentData.IsExtendingLap)
            {
                LogDebug($"LAP EXTENDING: Projected={CurrentData.ProjectedLapTime:F2}s, Average={CurrentData.AverageLapTime:F2}s (LapDist={telemetry.LapDistPct * 100:F1}%)");
            }
        }
        else
        {
            CurrentData.ProjectedLapTime = 0;
            CurrentData.IsExtendingLap = false;
        }
    }
    
    /// <summary>
    /// Calculate dynamic buffer laps based on race conditions
    /// Factors: fuel consistency, race position, weather, yellow flag probability
    /// </summary>
    /// <param name="telemetry">Current telemetry data</param>
    /// <param name="baseBufferLaps">User-configured base buffer laps</param>
    /// <param name="enableDynamicBuffer">Whether dynamic buffer calculation is enabled</param>
    private void CalculateDynamicBufferLaps(TelemetryData telemetry, float baseBufferLaps, bool enableDynamicBuffer)
    {
        // If dynamic buffer disabled, use static value
        if (!enableDynamicBuffer)
        {
            CurrentData.FuelBufferLaps = baseBufferLaps;
            CurrentData.BufferLapReason = "User configured (static)";
            return;
        }
        
        // Start with user-configured base buffer as minimum
        float baseBuffer = baseBufferLaps;
        
        // Need at least 5 laps of data for meaningful variance calculation
        var validLaps = _lapHistory.Where(l => l.IsValidForAveraging).ToList();
        if (validLaps.Count < 5)
        {
            CurrentData.FuelBufferLaps = baseBuffer;
            CurrentData.BufferLapReason = $"Insufficient data (only {validLaps.Count} laps)";
            return;
        }
        
        // ===== FACTOR 1: FUEL CONSISTENCY VARIANCE =====
        // Calculate standard deviation of fuel consumption
        float avgFuel = validLaps.Average(l => l.FuelUsed);
        float variance = validLaps.Sum(l => MathF.Pow(l.FuelUsed - avgFuel, 2)) / validLaps.Count;
        float stdDev = MathF.Sqrt(variance);
        CurrentData.FuelConsistencyVariance = stdDev;
        
        // Map variance to buffer contribution:
        // ±0.1L variance = +0.5 lap buffer (very consistent)
        // ±0.3L variance = +1.0 lap buffer (normal)
        // ±0.5L variance = +2.0 lap buffer (inconsistent)
        float consistencyBuffer = stdDev switch
        {
            < 0.1f => 0.5f,  // Very consistent driver/car
            < 0.2f => 0.75f, // Good consistency
            < 0.3f => 1.0f,  // Normal variance
            < 0.4f => 1.5f,  // High variance
            _ => 2.0f        // Very inconsistent (±0.5L+)
        };
        
        LogDebug($"DYNAMIC BUFFER: Fuel variance = ±{stdDev:F3}L → consistency buffer = {consistencyBuffer:F2} laps");
        
        // ===== FACTOR 2: RACE POSITION =====
        // Leading = tighter buffer (can manage pace)
        // Battling = larger buffer (unpredictable consumption)
        float positionBuffer = 0f;
        int totalCars = telemetry.CarIdxPosition?.Count(pos => pos > 0) ?? 1;
        int playerPosition = telemetry.LivePosition;
        
        if (totalCars > 1 && playerPosition > 0)
        {
            float positionPct = (float)playerPosition / totalCars;
            
            positionBuffer = positionPct switch
            {
                < 0.1f => -0.3f,  // Leading pack (top 10%) - reduce buffer, can control pace
                < 0.3f => 0.0f,   // Front runners (top 30%) - neutral
                < 0.7f => 0.3f,   // Midfield (30-70%) - slightly higher buffer for battles
                _ => 0.5f         // Back markers (70%+) - higher buffer, unpredictable battles
            };
            
            LogDebug($"DYNAMIC BUFFER: Position {playerPosition}/{totalCars} ({positionPct * 100:F0}%) → position buffer = {positionBuffer:F2} laps");
        }
        
        // ===== FACTOR 3: WEATHER CONDITIONS =====
        // Rain/wet track = LOWER fuel consumption (similar to yellow flags)
        float weatherBuffer = 0f;
        int skies = telemetry.Skies;
        
        // iRacing Skies enum: 0=Clear, 1=PartlyCloudy, 2=MostlyCloudy, 3=Overcast, 4=Rain
        if (skies >= 4)
        {
            weatherBuffer = -0.5f;  // Rain = REDUCE buffer (lower consumption from slower speeds/cautious driving)
            LogDebug($"DYNAMIC BUFFER: Rain conditions (skies={skies}) → weather buffer = {weatherBuffer:F2} laps");
        }
        else if (skies >= 3)
        {
            weatherBuffer = -0.2f;  // Overcast = small reduction (cooler temps)
            LogDebug($"DYNAMIC BUFFER: Overcast conditions (skies={skies}) → weather buffer = {weatherBuffer:F2} laps");
        }
        
        // ===== FACTOR 4: YELLOW FLAG PROBABILITY =====
        // Frequent yellows = larger buffer (can save fuel under caution)
        float yellowBuffer = 0f;
        int totalLapsCompleted = CurrentData.LapsCompleted;
        
        if (totalLapsCompleted >= 10 && CurrentData.YellowFlagLapCount > 0)
        {
            // Calculate yellow flag probability: (yellow laps / total laps)
            CurrentData.YellowFlagProbability = (float)CurrentData.YellowFlagLapCount / totalLapsCompleted;
            
            // High yellow probability = can take more risk (will likely get caution for fuel saving)
            // >30% yellows = reduce buffer by 0.5 laps (very yellow-heavy race, oval short track)
            // 20-30% yellows = reduce buffer by 0.3 laps (moderate yellows)
            // 10-20% yellows = neutral (normal racing)
            // <10% yellows = increase buffer by 0.2 laps (clean race, road course)
            yellowBuffer = CurrentData.YellowFlagProbability switch
            {
                > 0.3f => -0.5f,  // Very yellow-heavy (short oval) - reduce buffer
                > 0.2f => -0.3f,  // Moderate yellows - reduce buffer
                > 0.1f => 0.0f,   // Normal yellows - neutral
                _ => 0.2f         // Clean race - increase buffer
            };
            
            LogDebug($"DYNAMIC BUFFER: Yellow flag probability = {CurrentData.YellowFlagProbability * 100:F0}% ({CurrentData.YellowFlagLapCount}/{totalLapsCompleted}) → yellow buffer = {yellowBuffer:F2} laps");
        }
        else if (totalLapsCompleted >= 10)
        {
            // No yellows yet = assume clean race
            CurrentData.YellowFlagProbability = 0f;
            yellowBuffer = 0.3f;  // Increase buffer for clean races
            LogDebug($"DYNAMIC BUFFER: No yellows after {totalLapsCompleted} laps (clean race) → yellow buffer = {yellowBuffer:F2} laps");
        }
        
        // ===== FACTOR 5: PIT WINDOW OPTIMIZATION =====
        // If pit window closing soon (within 5 laps of race end), reduce buffer to squeeze out max laps
        float pitWindowBuffer = 0f;
        if (CurrentData.RaceLapsRemaining > 0 && CurrentData.RaceLapsRemaining <= 5)
        {
            pitWindowBuffer = -0.5f;  // Aggressive: reduce buffer when running out of pit opportunities
            LogDebug($"DYNAMIC BUFFER: Pit window closing ({CurrentData.RaceLapsRemaining} laps to go) → pit window buffer = {pitWindowBuffer:F2} laps");
        }
        
        // ===== COMBINE ALL FACTORS =====
        float dynamicBuffer = consistencyBuffer + positionBuffer + weatherBuffer + yellowBuffer + pitWindowBuffer;
        
        // Apply minimum threshold (never go below 0.5 laps, even if leading in perfect conditions)
        dynamicBuffer = Math.Max(0.5f, dynamicBuffer);
        
        // Apply maximum threshold (never exceed 3.0 laps, even in worst conditions)
        dynamicBuffer = Math.Min(3.0f, dynamicBuffer);
        
        CurrentData.FuelBufferLaps = dynamicBuffer;
        
        // Build explanation string
        string reason = $"Dynamic: {dynamicBuffer:F1}L " +
                       $"(consistency:{consistencyBuffer:F1} " +
                       $"pos:{positionBuffer:F1} " +
                       $"weather:{weatherBuffer:F1} " +
                       $"yellow:{yellowBuffer:F1} " +
                       $"pit:{pitWindowBuffer:F1})";
        CurrentData.BufferLapReason = reason;
        
        LogDebug($"DYNAMIC BUFFER FINAL: {dynamicBuffer:F2} laps - {reason}");
    }
    
    /// <summary>
    /// Detect flag status from session flags bitfield (Enhanced Phase 5 Fix)
    /// Now includes: One Lap to Green, Green Held, Debris, Blue, Start Lights, etc.
    /// </summary>
    private LapFlagStatus DetectFlagStatus(uint sessionFlags)
    {
        // iRacing SessionFlags bitfield constants (from irsdk_defines.h v1.19)
        const uint Checkered = 0x00000001;
        const uint White = 0x00000002;
        const uint Green = 0x00000004;
        const uint Yellow = 0x00000008;
        const uint Red = 0x00000010;
        const uint Blue = 0x00000020;
        const uint Debris = 0x00000040;
        const uint YellowWaving = 0x00000100;
        const uint OneLapToGreen = 0x00000200;  // ⭐ NEW! Critical for pit timing
        const uint GreenHeld = 0x00000400;
        const uint TenToGo = 0x00000800;
        const uint FiveToGo = 0x00001000;
        const uint Caution = 0x00004000;
        const uint CautionWaving = 0x00008000;
        
        // Start lights (standing start sequences)
        const uint StartReady = 0x20000000;
        const uint StartSet = 0x40000000;
        const uint StartGo = 0x80000000;
        
        // Priority order (higher priority flags checked first)
        
        // 1. Race ending flags (highest priority)
        if ((sessionFlags & Red) != 0)
            return LapFlagStatus.Red;
        
        if ((sessionFlags & Checkered) != 0)
            return LapFlagStatus.Checkered;
        
        // White flag = final lap (treat as race ending)
        if ((sessionFlags & White) != 0)
            return LapFlagStatus.White;
        
        // 2. Start sequence flags (standing starts)
        if ((sessionFlags & StartGo) != 0)
            return LapFlagStatus.StartGo;
        
        if ((sessionFlags & StartSet) != 0)
            return LapFlagStatus.StartSet;
        
        if ((sessionFlags & StartReady) != 0)
            return LapFlagStatus.StartReady;
        
        // 3. Critical restart flags (MOST IMPORTANT for pit strategy!)
        if ((sessionFlags & OneLapToGreen) != 0)  // ⭐ Critical pit window!
            return LapFlagStatus.OneLapToGreen;
        
        if ((sessionFlags & GreenHeld) != 0)
            return LapFlagStatus.GreenHeld;
        
        // 4. Caution flags (full course yellow)
        if ((sessionFlags & (Yellow | Caution | CautionWaving)) != 0)
            return LapFlagStatus.Yellow;
        
        // 5. Local flags (informational)
        if ((sessionFlags & YellowWaving) != 0)  // Local yellow (not full course)
            return LapFlagStatus.YellowWaving;
        
        if ((sessionFlags & Debris) != 0)
            return LapFlagStatus.Debris;
        
        if ((sessionFlags & Blue) != 0)
            return LapFlagStatus.Blue;
        
        // 6. Race progress informational flags
        if ((sessionFlags & TenToGo) != 0)
            return LapFlagStatus.TenToGo;
        
        if ((sessionFlags & FiveToGo) != 0)
            return LapFlagStatus.FiveToGo;
        
        // 7. Green flag (normal racing)
        if ((sessionFlags & Green) != 0)
            return LapFlagStatus.Green;
        
        return LapFlagStatus.Unknown;
    }
    
    /// <summary>
    /// Pit stop state machine: Track entry, service, and exit timing
    /// Updates session statistics with completed pit stops for historical learning
    /// </summary>
    private void UpdatePitStopTracking(TelemetryData telemetry)
    {
        bool onPitRoad = telemetry.OnPitRoad;
        float speed = telemetry.Speed; // m/s
        float fuelLevel = telemetry.FuelLevel;
        
        // State machine transitions
        switch (_pitState)
        {
            case PitStopState.NotOnPitRoad:
                if (onPitRoad)
                {
                    // Entered pit lane - start new pit stop
                    _currentPitStop = new PitStopData
                    {
                        PitEntryTime = DateTime.UtcNow,
                        LapNumber = telemetry.LapsCompleted + 1,
                        TrackName = telemetry.TrackName,
                        CarClassId = telemetry.PlayerCarClass,
                        SessionType = telemetry.SessionType,
                        TrackTemp = telemetry.TrackTemp,
                        AirTemp = telemetry.AirTemp,
                        WeatherType = telemetry.WeatherType,
                        TrackWetness = 0, // TODO: Add track wetness to TelemetryData if available
                        FuelBefore = fuelLevel
                    };
                    _pitState = PitStopState.Entering;
                    LogDebug($"PIT STOP: Entry detected (Lap {_currentPitStop.LapNumber}, Session={_currentPitStop.SessionType}, Track={_currentPitStop.TrackTemp:F1}°C, Air={_currentPitStop.AirTemp:F1}°C)");
                }
                break;
                
            case PitStopState.Entering:
                if (speed < 0.5f) // Stopped in pit box (< 0.5 m/s = ~1 mph)
                {
                    if (_currentPitStop != null)
                    {
                        _currentPitStop.PitBoxArrivalTime = DateTime.UtcNow;
                        _currentPitStop.ServiceStartTime = DateTime.UtcNow;
                        _pitState = PitStopState.InPitBox;
                        LogDebug($"PIT STOP: Arrived in pit box (Entry duration: {_currentPitStop.PitEntryDuration:F1}s)");
                    }
                }
                else if (!onPitRoad)
                {
                    // Aborted pit entry - reset
                    LogDebug("PIT STOP: Entry aborted (left pit road before stopping)");
                    _currentPitStop = null;
                    _pitState = PitStopState.NotOnPitRoad;
                }
                break;
                
            case PitStopState.InPitBox:
                // Wait for fuel to stop increasing (service complete)
                if (_currentPitStop != null && fuelLevel > _currentPitStop.FuelBefore + 0.1f)
                {
                    // Fuel is increasing - service in progress
                    _pitState = PitStopState.Servicing;
                }
                else if (speed > 0.5f)
                {
                    // Started moving without refuel - likely damage repair or quick stop
                    if (_currentPitStop != null)
                    {
                        _currentPitStop.ServiceEndTime = DateTime.UtcNow;
                        _currentPitStop.PitBoxDepartureTime = DateTime.UtcNow;
                        _pitState = PitStopState.Departing;
                        LogDebug("PIT STOP: Departing pit box (no refuel detected)");
                    }
                }
                break;
                
            case PitStopState.Servicing:
                if (_currentPitStop != null)
                {
                    float fuelDelta = fuelLevel - _currentPitStop.FuelBefore;
                    
                    // Detect service end: Fuel stopped increasing (delta < 0.05L over last update)
                    if (Math.Abs(fuelLevel - _lastFuelLevel) < 0.05f && fuelDelta > 0.5f)
                    {
                        _currentPitStop.ServiceEndTime = DateTime.UtcNow;
                        LogDebug($"PIT STOP: Service complete (Duration: {_currentPitStop.ServiceDuration:F1}s, Fuel added: {fuelDelta:F1}L)");
                    }
                    
                    // Detect departure: Car started moving again
                    if (speed > 0.5f && _currentPitStop.ServiceEndTime > _currentPitStop.ServiceStartTime)
                    {
                        _currentPitStop.PitBoxDepartureTime = DateTime.UtcNow;
                        _pitState = PitStopState.Departing;
                        LogDebug($"PIT STOP: Departing pit box");
                    }
                }
                break;
                
            case PitStopState.Departing:
                if (!onPitRoad)
                {
                    // Exited pit lane - complete pit stop
                    if (_currentPitStop != null)
                    {
                        _currentPitStop.PitExitTime = DateTime.UtcNow;
                        _currentPitStop.FuelAfter = fuelLevel;
                        
                        if (_currentPitStop.IsComplete)
                        {
                            LogDebug($"PIT STOP COMPLETE: Total={_currentPitStop.TotalPitStopTime:F1}s (Entry={_currentPitStop.PitEntryDuration:F1}s, Service={_currentPitStop.ServiceDuration:F1}s, Exit={_currentPitStop.PitExitDuration:F1}s, Active={_currentPitStop.ActivePitStopTime:F1}s)");
                            LogDebug($"  Fuel: Before={_currentPitStop.FuelBefore:F1}L, After={_currentPitStop.FuelAfter:F1}L, Added={_currentPitStop.FuelAdded:F1}L");
                            
                            // Update session statistics with this pit stop
                            if (_sessionStats == null)
                            {
                                _sessionStats = new SessionStatistics
                                {
                                    TrackName = _currentPitStop.TrackName,
                                    CarClassId = _currentPitStop.CarClassId,
                                    TrackLength = telemetry.TrackLength,
                                    PitSpeedLimit = telemetry.TrackPitSpeedLimit
                                };
                            }
                            
                            _persistenceService.UpdatePitStopStatistics(_sessionStats, _currentPitStop);
                            _persistenceService.SaveStatistics(_sessionStats);
                            
                            LogDebug($"UPDATED SESSION STATS: {_sessionStats.PitStopsRecorded} stops, Avg total={_sessionStats.AverageTotalPitTime:F1}s");
                        }
                        else
                        {
                            LogDebug("PIT STOP: Incomplete data (missing timestamps or fuel data)");
                        }
                    }
                    
                    _currentPitStop = null;
                    _pitState = PitStopState.NotOnPitRoad;
                }
                break;
        }
    }
    
    /// <summary>
    /// Calculate Median Absolute Deviation (MAD) for robust outlier detection
    /// MAD is more robust than standard deviation for small datasets with outliers
    /// </summary>
    /// <param name="values">List of values to analyze</param>
    /// <returns>MAD value (median of absolute deviations from median)</returns>
    private float CalculateMAD(List<float> values)
    {
        if (values.Count == 0)
            return 0f;
            
        // Calculate median
        var sorted = values.OrderBy(v => v).ToList();
        float median = sorted[sorted.Count / 2];
        
        // Calculate absolute deviations from median
        var deviations = values.Select(v => Math.Abs(v - median)).OrderBy(d => d).ToList();
        
        // Return median of deviations
        return deviations[deviations.Count / 2];
    }
    
    /// <summary>
    /// Detect outliers using multiple methods and flag suspicious laps
    /// Considers: MAD statistical outliers, lap time correlation, and incidents
    /// NOTE: Incidents and off-track are part of racing - we flag but don't auto-exclude
    /// </summary>
    /// <param name="laps">List of laps to analyze</param>
    /// <returns>List of laps with outlier flags set</returns>
    private List<FuelLapHistory> DetectOutliers(List<FuelLapHistory> laps)
    {
        if (laps.Count < 3)
            return laps; // Need at least 3 laps for meaningful outlier detection
            
        // Extract fuel values and lap times for analysis
        var fuelValues = laps.Select(l => l.FuelUsed).ToList();
        var lapTimes = laps.Where(l => l.LapTime > 0).Select(l => l.LapTime).ToList();
        
        // Calculate MAD for fuel consumption
        float fuelMedian = fuelValues.OrderBy(f => f).ToList()[fuelValues.Count / 2];
        float fuelMAD = CalculateMAD(fuelValues);
        
        // Calculate MAD for lap times (if available)
        float lapTimeMedian = 0f;
        float lapTimeMAD = 0f;
        if (lapTimes.Count >= 3)
        {
            lapTimeMedian = lapTimes.OrderBy(t => t).ToList()[lapTimes.Count / 2];
            lapTimeMAD = CalculateMAD(lapTimes);
        }
        
        // MAD threshold: 3.0 = moderate (keeps fuel saving/incidents), 2.5 = strict
        // Using 3.5 to be conservative - only flag extreme outliers
        const float MAD_THRESHOLD = 3.5f;
        
        // Lap time correlation threshold: 15% deviation from median
        // Allows for fuel saving (5-10% slower) but flags major incidents (>15% slower)
        const float LAP_TIME_THRESHOLD = 0.15f;
        
        foreach (var lap in laps)
        {
            List<string> reasons = new();
            
            // Check MAD statistical outlier (fuel consumption)
            if (fuelMAD > 0.001f) // Avoid division by zero
            {
                float fuelDeviation = Math.Abs(lap.FuelUsed - fuelMedian) / fuelMAD;
                if (fuelDeviation > MAD_THRESHOLD)
                {
                    reasons.Add($"Fuel MAD={fuelDeviation:F1} (>{MAD_THRESHOLD})");
                }
            }
            
            // Check lap time correlation (if lap time available)
            if (lap.LapTime > 0 && lapTimeMAD > 0.001f && lapTimeMedian > 0)
            {
                float lapTimeDeviation = (lap.LapTime - lapTimeMedian) / lapTimeMedian;
                if (lapTimeDeviation > LAP_TIME_THRESHOLD)
                {
                    reasons.Add($"Lap time +{lapTimeDeviation * 100:F0}% slower");
                }
            }
            
            // Note incidents but don't auto-flag (they're part of racing)
            // Just add to reason string for transparency
            if (lap.HadIncident)
            {
                reasons.Add($"{lap.IncidentsDuringLap}x incident(s)");
                // Don't set IsFlaggedAsOutlier - incidents alone don't make it invalid
                // Only flag if ALSO statistically abnormal
            }
            
            // Set outlier flag and reason
            if (reasons.Count > 0)
            {
                // Only flag as outlier if there's a statistical reason (MAD or lap time)
                // Incidents alone are not enough (they're normal in racing)
                bool hasStatisticalReason = reasons.Any(r => r.Contains("MAD") || r.Contains("Lap time"));
                
                if (hasStatisticalReason)
                {
                    lap.IsFlaggedAsOutlier = true;
                    lap.OutlierReason = string.Join(", ", reasons);
                }
                else
                {
                    // Just incidents, not a statistical outlier
                    lap.IsFlaggedAsOutlier = false;
                    lap.OutlierReason = string.Join(", ", reasons) + " (not flagged)";
                }
            }
        }
        
        return laps;
    }
    
    /// <summary>
    /// Reset all fuel calculations (call when session changes)
    /// </summary>
    public void Reset()
    {
        _lapHistory.Clear();
        _deltaHistory.Clear();  // Clear delta tracking history
        CurrentData = new FuelData();
        _fuelAtLapStart = 0;
        _lastFuelLevel = 0;
        _lastCompletedLap = -1;
        _isFirstUpdate = true;
        _currentFlagStatus = LapFlagStatus.Green;
        _wasOnPitRoadLastUpdate = false;
        _justLeftPits = false;
        _baselineFuelPressureEstablished = false;
        _fuelPressureHistory.Clear();
        _lapsCompletedWhenProcessed = -1;
        _lastIncidentCount = 0;  // Reset incident tracking
        _lastDelta = 0f;  // Reset delta tracking
        
        // Reset pit stop tracking
        _currentPitStop = null;
        _pitState = PitStopState.NotOnPitRoad;
        _sessionStatsLoaded = false;
    }
    
    /// <summary>
    /// Get lap history for analysis/export
    /// </summary>
    public IReadOnlyList<FuelLapHistory> GetLapHistory() => _lapHistory.AsReadOnly();
    
    /// <summary>
    /// Get delta history for convergence analysis and post-race accuracy evaluation
    /// </summary>
    public IReadOnlyList<DeltaHistoryRecord> GetDeltaHistory() => _deltaHistory.AsReadOnly();
    
    /// <summary>
    /// Apply temperature correction to fuel consumption averages (ENHANCEMENT)
    /// Adjusts fuel consumption based on air temperature differences from historical baseline
    /// Scientific basis: Hotter air = less dense = less power = richer mixture = more fuel
    /// Rule of thumb: +10°C = +2-3% fuel consumption
    /// </summary>
    private void ApplyTemperatureCorrection(TelemetryData telemetry)
    {
        // Skip if no historical data available
        if (_sessionStats == null || !_sessionStats.HasSufficientData)
        {
            CurrentData.TemperatureCorrectionFactor = 1.0f;
            CurrentData.TemperatureCorrectionReason = "";
            return;
        }

        float currentAirTemp = telemetry.AirTemp;
        float historicalAirTemp = _sessionStats.AvgAirTemp;
        float tempDelta = currentAirTemp - historicalAirTemp;

        // Apply correction: +10°C = +2.5% fuel consumption
        // Formula: 1.0 + (tempDelta * 0.0025)
        // Example: +12°C → 1.0 + (12 * 0.0025) = 1.03 (3% more fuel)
        float correctionFactor = 1.0f + (tempDelta * 0.0025f);

        // Limit correction to ±10% to avoid extreme values from sensor errors
        correctionFactor = Math.Clamp(correctionFactor, 0.9f, 1.1f);

        CurrentData.TemperatureCorrectionFactor = correctionFactor;

        if (Math.Abs(tempDelta) > 5f)
        {
            float correctionPct = (correctionFactor - 1.0f) * 100f;
            CurrentData.TemperatureCorrectionReason =
                $"Air temp {tempDelta:+0.0;-0.0}°C vs historical avg ({correctionPct:+0.0;-0.0}% fuel)";
            LogDebug($"TEMP CORRECTION: {currentAirTemp:F1}°C vs {historicalAirTemp:F1}°C → {correctionFactor:F3}x factor ({correctionPct:+0.0;-0.0}%)");

            // Apply correction to all averages (multiply by correction factor)
            if (CurrentData.AvgFuelPerLap_Last > 0)
                CurrentData.AvgFuelPerLap_Last *= correctionFactor;
            if (CurrentData.AvgFuelPerLap_L5 > 0)
                CurrentData.AvgFuelPerLap_L5 *= correctionFactor;
            if (CurrentData.AvgFuelPerLap_L10 > 0)
                CurrentData.AvgFuelPerLap_L10 *= correctionFactor;
            if (CurrentData.AvgFuelPerLap_Session > 0)
                CurrentData.AvgFuelPerLap_Session *= correctionFactor;
        }
        else
        {
            CurrentData.TemperatureCorrectionReason = "";
        }
    }

    /// <summary>
    /// Apply real-time fuel flow integration using FuelUsePerHour telemetry (ENHANCEMENT)
    /// Provides more accurate mid-lap fuel predictions based on instantaneous consumption
    /// </summary>
    private void ApplyRealTimeFuelFlow(TelemetryData telemetry)
    {
        CurrentData.InstantaneousFuelFlow = telemetry.FuelUsePerHour;

        // Calculate projected lap fuel if we have average lap time
        if (CurrentData.AverageLapTime > 0 && telemetry.FuelUsePerHour > 0)
        {
            // Convert L/hour to L/lap
            float hoursPerLap = CurrentData.AverageLapTime / 3600f;
            CurrentData.ProjectedLapFuel = telemetry.FuelUsePerHour * hoursPerLap;

            // Compare to historical average (only if valid)
            if (CurrentData.AvgFuelPerLap_L5 > 0)
            {
                float flowDelta = CurrentData.ProjectedLapFuel - CurrentData.AvgFuelPerLap_L5;

                // Log significant deviations (>0.3L difference)
                if (Math.Abs(flowDelta) > 0.3f)
                {
                    LogDebug($"FUEL FLOW: Current flow projects {CurrentData.ProjectedLapFuel:F3}L/lap vs avg {CurrentData.AvgFuelPerLap_L5:F3}L ({flowDelta:+0.00;-0.00}L)");
                }
            }

            // Use fuel flow for early lap prediction (after 30% but before 90% of lap)
            // This gives more accurate real-time estimates than waiting for lap completion
            if (telemetry.LapDistPct > 0.3f && telemetry.LapDistPct < 0.9f && CurrentData.ProjectedLapFuel > 0)
            {
                CurrentData.CurrentLapFuelRate = CurrentData.ProjectedLapFuel;
            }
        }
        else
        {
            CurrentData.ProjectedLapFuel = 0;
        }
    }

    /// <summary>
    /// Write debug message to log file
    /// </summary>
    private void LogDebug(string message)
    {
        try
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MRT-UI",
                "fuel_debug.log"
            );
            var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
            System.IO.File.AppendAllText(logPath, logMessage);
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
