using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services.Fuel;

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
    private bool _justProcessedLap = false; // Flag for triggering full calculation this frame
    private int _lastLoggedSessionLaps = -1; // Track last logged SessionLaps value to avoid spam logging

    // FIX: Save fuel level BEFORE entering pit road to prevent refueling from contaminating lap calculations
    private float _fuelBeforeEnteringPits = 0f;
    private bool _pittedThisLap = false; // Track if we entered pit road during current lap
    
    // Stint tracking for StintAverage calculation
    private int _stintStartLapNumber = 0;  // Lap number when current stint started (after pit exit)
    
    // Fuel averaging service (Phase 1: Service Splitting)
    private readonly FuelAveragingService _fuelAveragingService;
    
    // Fuel outlier detection service (Phase 2: Service Splitting)
    private readonly FuelOutlierDetector _outlierDetector;
    
    // Pit strategy service (Phase 3: Service Splitting)
    private readonly PitStrategyService _pitStrategyService;
    
    // Fuel saving calculator (Phase 4: Service Splitting)
    private readonly FuelSavingCalculator _fuelSavingCalculator;
    
    // Phase 7: Lap delta tracker
    private readonly LapDeltaTracker _lapDeltaTracker;
    
    // Phase 9: Historical learning service
    private readonly History.TelemetryHistoryService? _historyService;
    private bool _historicalDataApplied = false;
    
    // Delta tracking service (Phase 5: Service Splitting)
    private readonly DeltaTrackingService _deltaTrackingService;
    
    // Dynamic buffer calculator (Phase 6: Service Splitting)
    private readonly DynamicBufferCalculator _bufferCalculator;
    
    // Phase 6: Live fuel calculations and pit stop tracking
    private readonly PitStopTracker _pitStopTracker;
    private readonly LiveFuelCalculator _liveFuelCalculator;
    
    // Optional Fine-Tuning: Extracted service fields
    private readonly LapValidator _lapValidator;
    private readonly TemperatureCompensationService _temperatureCompensation;
    
    // EMA (Exponential Moving Average) tracking - REMOVED: No longer used, replaced by DeltaTrackingService
    // private float _emaValue = 0f;  // Current EMA value
    // private bool _emaInitialized = false;  // Whether EMA has been initialized with first lap
    // REMOVED: Fixed EMA_ALPHA constant - now calculated adaptively based on fuel consistency
    
    // Incident tracking for outlier detection
    private int _lastIncidentCount = 0;  // Track incident count to detect new incidents during laps
    
    // Delta tracking for convergence analysis (delegated to DeltaTrackingService)
    
    // Pit stop tracking (Phase 6: Extracted to PitStopTracker service)
    private readonly SessionPersistenceService _persistenceService;
    private SessionStatistics? _sessionStats = null;
    
    // Dynamic buffer configuration
    private float _bufferLaps = 1.0f;  // User-configured base buffer
    private bool _enableDynamicBuffer = true;  // Whether dynamic buffer is enabled
    
    // Pit lap accuracy fix (Phase 1: FUEL_PIT_LAP_AND_FLAGS_FIX.md)
    private float _lastLapDistPct = 0f;  // Track lap distance percentage from previous update
    
    // Grid start lap detection - track LapDistPct when lap starts to detect partial laps
    private float _lapDistPctAtLapStart = 0f;  // LapDistPct when current lap started

    // ENHANCEMENT: Dynamic pit entry detection (learns from first pit stop instead of hardcoded value)
    private float? _learnedPitEntryPct = null;  // Learned pit entry location (null = not yet learned)
    private bool _isPitEntryLearned = false;    // Whether pit entry has been learned this session

    // Live update throttling (Progressive Calculation - Option 3)
    private int _liveUpdateCounter = 0;         // Counter for throttling live updates
    private const int LIVE_UPDATE_THROTTLE = 10; // Fire event every 10th frame (60 Hz → 6 Hz)

    // Pace lap detection fix: Track SessionState at lap START to catch laps that start in parade but end in racing
    private int _sessionStateAtLapStart = 0;  // SessionState when current lap started (checked at lap completion)

    /// <summary>
    /// Initialize fuel calculator service with session persistence
    /// </summary>
    public FuelCalculatorService()
    {
        _persistenceService = new SessionPersistenceService();
        _fuelAveragingService = new FuelAveragingService();
        _outlierDetector = new FuelOutlierDetector();
        _pitStrategyService = new PitStrategyService(_persistenceService);  // Pass persistence for pit time predictions
        _fuelSavingCalculator = new FuelSavingCalculator(_persistenceService);
        _lapDeltaTracker = new LapDeltaTracker();
        _deltaTrackingService = new DeltaTrackingService();
        _bufferCalculator = new DynamicBufferCalculator();
        
        // Phase 6: Extract complex methods to services
        _pitStopTracker = new PitStopTracker();
        _liveFuelCalculator = new LiveFuelCalculator();
        
        // Optional Fine-Tuning: Extracted services
        _lapValidator = new LapValidator();
        _temperatureCompensation = new TemperatureCompensationService();
        
        // Phase 9: Initialize history service (load async in background)
        _historyService = new History.TelemetryHistoryService();
        _ = _historyService.LoadHistoryAsync(); // Fire and forget
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
            
            // FIX: Save initial LapDistPct for grid start detection
            // If we start behind S/F line (e.g., Oulton), this will be > 0
            _lapDistPctAtLapStart = telemetry.LapDistPct;
            LogDebug($"INIT: Starting at LapDistPct={telemetry.LapDistPct:P1} - Grid start detection enabled");
            
            // Phase 9: Apply historical predictions at session start
            ApplyHistoricalPredictions(telemetry);
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
        // FIXED: Trust SDK lap numbering directly (removed confusing 5% delay logic)
        // SDK provides accurate lap numbers that increment at start/finish line (0% LapDistPct)
        CurrentData.CurrentLap = telemetry.Lap; // Direct from SDK (1-based, increments at S/F line)
        CurrentData.SessionLaps = telemetry.SessionLapsTotal; // FIX: Use SessionLapsTotal (SessionLaps is deprecated and always 0)
        CurrentData.LapsCompleted = telemetry.LapsCompleted; // 0-based count of FINISHED laps
        CurrentData.CarClassId = telemetry.PlayerCarClass;

        // CRITICAL DEBUG: Log SessionLaps on EVERY update for first 10 laps
        if (telemetry.Lap <= 10)
        {
            if (telemetry.SessionLaps != _lastLoggedSessionLaps)
            {
                Console.WriteLine($"⚠️ CRITICAL: SessionLaps CHANGED to {telemetry.SessionLaps} at lap {telemetry.Lap}");
                LogDebug($"⚠️ CRITICAL: SessionLaps={telemetry.SessionLaps} (was {_lastLoggedSessionLaps}) at lap {telemetry.Lap}");
                _lastLoggedSessionLaps = telemetry.SessionLaps;
            }
        }
        
        // Set car-specific sputtering threshold (Enhanced Phase 2.1)
        CurrentData.FuelSputteringThreshold = FuelSputteringDatabase.GetSputteringThreshold(
            telemetry.PlayerCarClass, 
            telemetry.FuelLevelMax
        );
        
        // FIX #3: Update lap completion context (race position awareness)
        UpdateLapCompletionContext(telemetry);

        // DEBUG: Always log for first 10 laps to diagnose issue
        bool shouldDebugLog = telemetry.Lap <= 10;

        // ===== SESSION TYPE DETECTION: Use SDK SessionLapsTotal directly =====
        // USER INSIGHT: SDK provides SessionLapsTotal and SessionLapsRemain directly!
        // NO MORE CALCULATING - just use what SDK gives us
        //
        // PRIORITY ORDER:
        //   1. SessionLapsTotal > 0 → LAP-BASED (use SDK value directly)
        //   2. SessionType="Race" → LAP-BASED
        //   3. SessionTimeRemain > 0 → TIME-BASED

        // FIX: Use SDK values directly instead of calculating
        bool hasFixedLaps = telemetry.SessionLapsTotal > 0;
        bool hasTimeLimit = telemetry.SessionTimeRemain > 0;
        bool isRaceSession = telemetry.SessionType?.Equals("Race", StringComparison.OrdinalIgnoreCase) ?? false;

        // SIMPLE: If SDK says we have a lap count, it's lap-based
        bool isLapBasedRace = hasFixedLaps || isRaceSession;
        CurrentData.IsTimedSession = !isLapBasedRace;

        // DIRECT SDK OVERRIDE: If SessionLapsRemain is available, use it immediately
        if (!CurrentData.IsTimedSession && telemetry.SessionLapsRemain > 0)
        {
            CurrentData.RaceLapsRemaining = telemetry.SessionLapsRemain;
            if (shouldDebugLog)
                LogDebug($"✅ USING SDK SessionLapsRemain={telemetry.SessionLapsRemain} directly (no calculation needed)");
        }

        if (shouldDebugLog)
        {
            Console.WriteLine($"📊 [LAP {telemetry.Lap}] SessionType={telemetry.SessionType}, SessionLaps={telemetry.SessionLaps}, IsTimedSession={CurrentData.IsTimedSession}");
            if (isRaceSession && telemetry.SessionLaps == 0)
                Console.WriteLine($"   ✅ RACE SESSION: SessionType=Race detected → Using LAP-BASED calculation (even though SessionLaps=0)");
        }

        if (CurrentData.IsTimedSession)
        {
            // Time-based session: Calculate laps from time remaining
            // Use average lap time from lap history if available, fallback to SDK LastLapTime
            float lapTimeToUse = CurrentData.AverageLapTime;

            // FIX #2: EARLY FALLBACK for circular dependency in timed sessions
            // PROBLEM: CalculateAverages() needs valid laps → but needs RaceLapsRemaining > 0 → which needs AverageLapTime
            // SOLUTION: Use SDK's LastLapTime immediately on first update to break the circular dependency
            if (lapTimeToUse <= 0 && telemetry.LapLastLapTime > 0)
            {
                lapTimeToUse = telemetry.LapLastLapTime;
                // CRITICAL: Update CurrentData.AverageLapTime so it's available for strategy calculations
                // This breaks the circular dependency by providing immediate lap time estimate
                CurrentData.AverageLapTime = lapTimeToUse;
                if (shouldDebugLog)
                {
                    LogDebug($"[TIME_SESSION] Using SDK LastLapTime fallback: {lapTimeToUse:F1}s (breaking circular dependency)");
                }
            }
            
            if (lapTimeToUse > 0 && CurrentData.SessionTimeRemaining > 0)
            {
                CurrentData.EstimatedLapsFromTime = (float)(CurrentData.SessionTimeRemaining / lapTimeToUse);
                // FIX: Use Math.Round instead of Ceiling to avoid overestimating by up to 1 lap
                // Safety margin is handled by FuelBufferLaps in strategy calculations
                CurrentData.RaceLapsRemaining = (int)Math.Round(CurrentData.EstimatedLapsFromTime, MidpointRounding.AwayFromZero);
                
                if (shouldDebugLog)
                {
                    Console.WriteLine($"⏰ TIME_SESSION: RaceLapsRemaining={CurrentData.RaceLapsRemaining} (from {CurrentData.SessionTimeRemaining:F0}s ÷ {lapTimeToUse:F1}s/lap)");
                    LogDebug($"⏰ TIME_SESSION: RaceLapsRemaining={CurrentData.RaceLapsRemaining} (from {CurrentData.SessionTimeRemaining:F0}s ÷ {lapTimeToUse:F1}s/lap)");
                }
            }
            else
            {
                CurrentData.EstimatedLapsFromTime = 0;
                CurrentData.RaceLapsRemaining = 0;
                
                if (shouldDebugLog)
                {
                    LogDebug($"[TIME_SESSION] Lap {telemetry.Lap} | NO DATA - AvgLapTime: {CurrentData.AverageLapTime:F1}s | SDK LastLapTime: {telemetry.LapLastLapTime:F1}s | TimeRemain: {CurrentData.SessionTimeRemaining:F1}s | RaceLapsRemaining: 0");
                }
            }
        }
        else
        {
            // ===== LAP-BASED SESSION: Fixed Laps Trump Time =====
            // RULE: When SessionLaps > 0, use ONLY lap-based calculation
            //       IGNORE SessionTimeRemain even if it shows 24 hours
            // REASON: iRacing sessions often have both timers AND lap counts active
            //         Lap count is the authoritative race end condition when set
            //
            // CALCULATION: Use ActualLeadingLapNumber (highest lap anyone is on)
            //   - Race ends when ActualLeadingLapNumber reaches SessionLaps
            //   - Player needs fuel for laps until THAT happens, not until they finish SessionLaps
            //
            // Example: 20-lap race, player on lap 5, leader on lap 7 (player is 2 laps down)
            //   OLD: 20 - 4 = 16 laps (WRONG - player won't complete 16 more laps)
            //   NEW: 20 - 7 = 13 laps (CORRECT - race ends in 13 laps when leader finishes lap 20)

            int actualLeadingLap = telemetry.ActualLeadingLapNumber; // Highest lap any car is on
            int raceFinishLap = telemetry.SessionLaps;               // Total laps in race (e.g., 35)

            // CRITICAL FIX: Handle SessionLaps=0 in races (SDK sends lap count = 0 sometimes)
            // This happens when iRacing doesn't populate SessionLaps even in lap-based races
            // Fallback: Use a large number to indicate "unlimited" until we know the actual lap count
            if (raceFinishLap == 0 && isRaceSession)
            {
                // Race with unknown lap count - set to very large number (indicates "unknown")
                // The fuel calculation will handle the actual pit strategy based on fuel, not laps
                raceFinishLap = 9999; // Placeholder for "unknown lap limit"
                if (shouldDebugLog)
                    LogDebug($"⚠️ RACE_LAPS_UNKNOWN: SessionLaps=0, using placeholder {raceFinishLap} laps");
            }

            // EDGE CASE PROTECTION: Race overtime or extensions (leader completed more laps than scheduled)
            // This can happen in:
            //   1. Race extensions due to incidents
            //   2. Manual session extensions by admins
            //   3. Overtime rules in special events
            if (raceFinishLap > 0 && actualLeadingLap >= raceFinishLap)
            {
                // Race has ended or is in overtime - freeze calculations at 0 laps remaining
                CurrentData.RaceLapsRemaining = 0;
                LogDebug($"[RACE_OVERTIME] Leader lap {actualLeadingLap} >= Session laps {raceFinishLap} - Race complete/overtime");
            }
            else if (raceFinishLap > 0)
            {
                // Normal race: Calculate laps until leader finishes
                CurrentData.RaceLapsRemaining = Math.Max(0, raceFinishLap - actualLeadingLap);
            }
            else
            {
                // No race lap limit could be determined - set to 0 as safe fallback
                CurrentData.RaceLapsRemaining = 0;
            }

            // Store as float for UI display consistency
            CurrentData.EstimatedLapsFromTime = CurrentData.RaceLapsRemaining;

            // Debug logging - extended to 10 laps to catch more issues
            if (shouldDebugLog)
            {
                Console.WriteLine($"📋 LAP_SESSION: RaceLapsRemaining={CurrentData.RaceLapsRemaining} (from {raceFinishLap} - {actualLeadingLap})");
                LogDebug($"📋 LAP_SESSION: RaceLapsRemaining={CurrentData.RaceLapsRemaining} (from {raceFinishLap} - {actualLeadingLap})");
            }
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
        
        // FIX: Detect pit road status changes and save fuel BEFORE entering pits
        bool isOnPitRoad = telemetry.OnPitRoad;

        // Entering pit road - save fuel level to prevent refueling contamination
        if (!_wasOnPitRoadLastUpdate && isOnPitRoad)
        {
            _fuelBeforeEnteringPits = telemetry.FuelLevel;
            _pittedThisLap = true;
            LogDebug($"ENTERING PIT ROAD: Saved fuel level {_fuelBeforeEnteringPits:F2}L for lap calculation");
        }

        // Exiting pit road - this lap is an out-lap
        if (_wasOnPitRoadLastUpdate && !isOnPitRoad)
        {
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

        // ===== FIX: LAP COMPLETION AT FINISH LINE ONLY =====
        // CHANGE: Complete laps at finish/start line (lap wrap-around) instead of pit entry
        // REASON: More intuitive, matches iRacing lap counter exactly
        // PIT CONTAMINATION: Prevented by saving fuel BEFORE entering pit road (see above)

        // Handle lap completion at finish line (95% → 5% wrap-around)
        if (_lastLapDistPct > 0.9f && telemetry.LapDistPct < 0.1f)
        {
            // Crossed finish/start line - complete the lap
            if (telemetry.LapsCompleted > _lastCompletedLap)
            {
                LogDebug($"LAP COMPLETED at finish line: Lap {telemetry.LapsCompleted} (LapDistPct wrap {_lastLapDistPct:F3}→{telemetry.LapDistPct:F3})");

                // FIX: Skip lap 0 (formation) - check BEFORE calling OnLapCompleted
                if (telemetry.LapsCompleted == 0)
                {
                    LogDebug($"⏭️ SKIPPING LAP 0: Formation lap, resetting fuel tracker");
                    LogDebug($"   Fuel before skip: _fuelAtLapStart={_fuelAtLapStart:F2}L, current={telemetry.FuelLevel:F2}L, used={_fuelAtLapStart - telemetry.FuelLevel:F3}L");
                    _fuelAtLapStart = telemetry.FuelLevel; // Reset for lap 1
                    _pittedThisLap = false; // Reset flag
                    _lapsCompletedWhenProcessed = 0; // Mark lap 0 as processed
                    _justProcessedLap = true; // Trigger full calculation
                    _sessionStateAtLapStart = telemetry.SessionState; // Save for next lap
                    _lapDistPctAtLapStart = telemetry.LapDistPct; // Save starting position for lap 1
                    LogDebug($"   _fuelAtLapStart reset to {_fuelAtLapStart:F2}L for lap 1");
                }
                else if (telemetry.LapsCompleted != _lapsCompletedWhenProcessed)
                {
                    // Call lap completion with refueling flag and pit flag
                    OnLapCompleted(telemetry, isRefueling, _pittedThisLap, _fuelBeforeEnteringPits);
                    _lapsCompletedWhenProcessed = telemetry.LapsCompleted;
                    _justProcessedLap = true; // Trigger full calculation
                    _pittedThisLap = false; // Reset for next lap
                    _fuelBeforeEnteringPits = 0f; // Reset saved fuel
                }

                // FIX: Save SessionState for the NEW lap that's starting (checked when this lap completes)
                // This captures parade/racing state at lap START instead of lap END
                _sessionStateAtLapStart = telemetry.SessionState;
                
                // FIX: Save LapDistPct for the NEW lap that's starting
                // Used to detect grid start partial laps (where grid is behind S/F line)
                _lapDistPctAtLapStart = telemetry.LapDistPct;
            }
        }
        
        _lastLapDistPct = telemetry.LapDistPct;  // Track lap distance for next update

        // FIX: Don't clear out-lap flag mid-lap - it should persist until lap completion
        // The flag will be used by OnLapCompleted to mark the lap as out-lap
        // (Original mid-lap clearing was causing out-laps to not be detected)

        // Update tracking variables
        _lastCompletedLap = telemetry.LapsCompleted;
        _lastFuelLevel = telemetry.FuelLevel;

        // ===== PROGRESSIVE CALCULATION (Option 3): STRATEGIC vs LIVE UPDATES =====
        //
        // Decision point: Full strategic calculation or lightweight live update?
        //
        // FULL STRATEGIC CALCULATION (heavy):
        //   - Runs when lap is completed (OnLapCompleted called above)
        //   - Recalculates averages, strategy, pit windows, multi-stop plans
        //   - Uses complete lap history for accuracy
        //   - Fires event immediately (IsLiveUpdate = false)
        //
        // LIGHTWEIGHT LIVE UPDATE (fast):
        //   - Runs mid-lap (LapDistPct > 5%)
        //   - Projects current lap fuel usage to estimate remaining laps
        //   - Uses existing averages (doesn't recalculate strategy)
        //   - Fires throttled event (6 Hz instead of 60 Hz)
        //   - Marked with IsLiveUpdate = true

        // Detect if we should do full calculation or live update
        bool shouldDoFullCalculation = _justProcessedLap || isRefueling;
        _justProcessedLap = false; // Reset flag after checking

        if (shouldDoFullCalculation)
        {
            // FULL STRATEGIC CALCULATION PATH
            LogDebug("[PROGRESSIVE_CALC] Full strategic calculation (lap completed or refueled)");

            // Calculate averages and strategy
            CalculateAverages();
            ApplyTemperatureCorrection(telemetry);  // ENHANCEMENT: Temperature correction for fuel consumption
            ApplyRealTimeFuelFlow(telemetry);      // ENHANCEMENT: Real-time fuel flow integration

            // Calculate dynamic buffer using service (Phase 6) - FIX: Pass user settings
            var bufferData = _bufferCalculator.Calculate(
                CurrentData.FuelConsistencyVariance,  // Use existing property
                CurrentData.RacePosition,  // Fixed: Use RacePosition instead of Position
                CurrentData.TotalCars,
                false,  // isRaining - simplified for now, can enhance later
                0,  // yellowFlagCount - can add to FuelData if needed
                telemetry.LapsCompleted + 1,
                telemetry.SessionLaps,
                CurrentData.IsTimedSession,
                _bufferLaps,          // FIX: Pass user-configured buffer setting
                _enableDynamicBuffer  // FIX: Pass dynamic buffer toggle
            );
            CurrentData.FuelBufferLaps = bufferData.TotalBuffer;
            // BufferReason can be added to FuelData if needed

            CalculateStrategy();

            // Build FuelAverages object for services (used by pit strategy and fuel saving)
            var fuelAverages = new FuelAverages
            {
                Current = CurrentData.AvgFuelPerLap,
                Last = CurrentData.AvgFuelPerLap_Last,
                L5 = CurrentData.AvgFuelPerLap_L5,
                L10 = CurrentData.AvgFuelPerLap_L10,
                EMA = CurrentData.AvgFuelPerLap_EMA,
                Session = CurrentData.AvgFuelPerLap_Session
            };
            
            // Calculate pit strategy using extracted service (Phase 1 refactor)
            var pitStrategy = _pitStrategyService.Calculate(telemetry, CurrentData, fuelAverages, _lapHistory);
            ApplyPitStrategyToCurrentData(pitStrategy);

            // Track delta using service (Phase 5)
            var deltaData = _deltaTrackingService.Track(
                CurrentData.LapsRemaining,
                CurrentData.IRacingLapsRemaining,
                telemetry.LapsCompleted + 1
            );

            // Calculate fuel saving using service (Phase 4)
            var strategy = new PitStrategy
            {
                OptimalPitLap = CurrentData.OptimalPitLap,
                FuelToAddAtPit = CurrentData.FuelToAddAtPit,
                CanFinishWithoutStop = CurrentData.CanFinishWithoutStop
            };
            var savingData = _fuelSavingCalculator.Calculate(telemetry, CurrentData, fuelAverages, strategy);

            // Phase 6: Copy all fuel saving properties to CurrentData
            CurrentData.NeedsFuelSaving = savingData.NeedsFuelSaving;
            CurrentData.FuelSavingTarget = savingData.FuelSavingTarget;
            CurrentData.CurrentSavingRate = savingData.CurrentSavingRate;
            CurrentData.SavingProgress = savingData.SavingProgress;
            CurrentData.CanSaveFuelToFinish = savingData.CanSaveFuelToFinish;
            CurrentData.FuelSavingWorking = savingData.FuelSavingWorking;
            CurrentData.IsPittingFaster = savingData.IsPittingFaster;
            CurrentData.StrategicAlert = savingData.StrategicAlert;
            CurrentData.AlertSeverity = savingData.AlertSeverity;
            CurrentData.HistoricalContext = savingData.HistoricalContext;

            // Phase 7: Calculate live lap delta
            float targetLapTime = CurrentData.TargetLapTime > 0 ? CurrentData.TargetLapTime : CurrentData.AverageLapTime;
            float averageLapTime = _lapDeltaTracker.GetAverageLapTime();
            if (averageLapTime <= 0)
                averageLapTime = CurrentData.AverageLapTime;

            bool isOnTrack = telemetry.Speed > 1.0f; // Simple check: moving = on track

            var lapDelta = _lapDeltaTracker.CalculateLiveDelta(
                (float)telemetry.SessionTime,
                targetLapTime,
                averageLapTime,
                isOnTrack);

            CurrentData.LiveDeltaValid = lapDelta.IsValid;
            CurrentData.LiveDeltaToTarget = lapDelta.DeltaToTarget;
            CurrentData.PredictedLapTime = lapDelta.PredictedLapTime;
            CurrentData.PredictedDelta = lapDelta.PredictedDelta;
            CurrentData.LapProgress = lapDelta.LapProgress;

            // Track pit stops for session persistence (Phase 6: Use PitStopTracker service)
            var completedPitStop = _pitStopTracker.Update(telemetry);
            if (completedPitStop != null)
            {
                // Update session statistics with completed pit stop
                if (_sessionStats == null)
                {
                    _sessionStats = new SessionStatistics
                    {
                        TrackName = completedPitStop.TrackName,
                        CarClassId = completedPitStop.CarClassId,
                        TrackLength = telemetry.TrackLength,
                        PitSpeedLimit = telemetry.TrackPitSpeedLimit
                    };
                }
                
                _persistenceService.UpdatePitStopStatistics(_sessionStats, completedPitStop);
                _persistenceService.SaveStatistics(_sessionStats);
                
                LogDebug($"UPDATED SESSION STATS: {_sessionStats.PitStopsRecorded} stops, Avg total={_sessionStats.AverageTotalPitTime:F1}s");
            }

            // Mark this as a full strategic update (not live)
            CurrentData.IsLiveUpdate = false;

            // Fire update event immediately (full calculation)
            FuelDataUpdated?.Invoke(this, CurrentData);
        }
        else if (telemetry.LapDistPct > 0.05f && !telemetry.OnPitRoad)
        {
            // LIGHTWEIGHT LIVE UPDATE PATH (mid-lap projections) - Phase 6: Use LiveFuelCalculator
            // Only update if at least 5% into lap and not on pit road
            if (_liveFuelCalculator.UpdateLiveValues(telemetry, CurrentData, out var liveData))
            {
                // Apply live calculation results
                CurrentData.CurrentLapFuelRate = liveData.CurrentLapFuelRate;
                CurrentData.LapsRemaining = liveData.LapsRemaining;
                CurrentData.FuelNeededToFinish = liveData.FuelNeededToFinish;
                CurrentData.FuelDeltaToFinish = liveData.FuelDeltaToFinish;
                CurrentData.CanFinishWithoutStop = liveData.CanFinishWithoutStop;
                CurrentData.FuelCriticalityScore = liveData.FuelCriticalityScore;
                CurrentData.EarliestPitLap = liveData.EarliestPitLap;
                CurrentData.LatestPitLap = liveData.LatestPitLap;
                CurrentData.OptimalPitLap = liveData.OptimalPitLap;
                
                // Apply real-time fuel flow (Phase 6: Use LiveFuelCalculator)
                _liveFuelCalculator.ApplyRealTimeFuelFlow(telemetry, CurrentData, 
                    out float projectedLapFuel, out float currentLapFuelRate);
                CurrentData.InstantaneousFuelFlow = telemetry.FuelUsePerHour;
                CurrentData.ProjectedLapFuel = projectedLapFuel;
                if (currentLapFuelRate > 0)
                    CurrentData.CurrentLapFuelRate = currentLapFuelRate;
            }

            // Throttle event firing (every 10th frame = 6 Hz instead of 60 Hz)
            if (++_liveUpdateCounter >= LIVE_UPDATE_THROTTLE)
            {
                _liveUpdateCounter = 0;

                // Mark this as a live update
                CurrentData.IsLiveUpdate = true;

                // Fire throttled update event
                FuelDataUpdated?.Invoke(this, CurrentData);
            }
        }
    }

    /// <summary>
    /// Handle lap completion and track fuel usage
    /// NOTE: Lap 0 is now filtered BEFORE calling this function (see lap completion detection)
    /// </summary>
    /// <param name="telemetry">Current telemetry data</param>
    /// <param name="wasRefueled">True if refueling was detected this lap</param>
    /// <param name="pittedThisLap">True if entered pit road during this lap</param>
    /// <param name="fuelBeforePit">Fuel level saved before entering pits (0 if didn't pit)</param>
    private void OnLapCompleted(TelemetryData telemetry, bool wasRefueled, bool pittedThisLap, float fuelBeforePit)
    {
        // Optional Fine-Tuning: Use LapValidator for all lap validation logic
        var previousLapWasPitLap = _lapHistory.Count > 0 && _lapHistory.Last().WasPitLap;
        var lapRecord = _lapValidator.ValidateAndCreateLapRecord(
            telemetry,
            _fuelAtLapStart,
            wasRefueled,
            pittedThisLap,
            fuelBeforePit,
            _justLeftPits,
            _sessionStateAtLapStart,
            _lapDistPctAtLapStart,
            previousLapWasPitLap,
            _currentFlagStatus,
            _lastIncidentCount
        );
        
        // Update incident tracking for next lap
        _lastIncidentCount = telemetry.PlayerCarMyIncidentCount;
        
        _lapHistory.Add(lapRecord);
        CurrentData.LapsCompleted = telemetry.LapsCompleted;
        
        // Update last lap fuel usage and lap-to-lap delta
        if (lapRecord.IsValidForAveraging)
        {
            CurrentData.FuelUsedLastLap = lapRecord.FuelUsed;
            CurrentData.LapToLapDelta = _lapValidator.CalculateLapToLapDelta(_lapHistory, lapRecord);
        }
        
        // Reset for next lap
        _fuelAtLapStart = telemetry.FuelLevel;

        // FIX: Clear out-lap flag after lap is recorded
        // This flag was set when exiting pit road and has now been used to mark the lap
        if (_justLeftPits)
        {
            _justLeftPits = false;
            LogDebug($"OUT-LAP FLAG CLEARED: Lap {telemetry.LapsCompleted} recorded as out-lap");
        }

        // Phase 7: Track lap delta
        _lapDeltaTracker.CompleteLap((float)telemetry.SessionTime, lapRecord.LapTime);
        _lapDeltaTracker.StartLap((float)telemetry.SessionTime);
    }
    
    /// <summary>
    /// Calculate all fuel averages from lap history
    /// Phase 1: Uses FuelAveragingService for most calculations, keeps outlier detection inline
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
        
        // Calculate fuel consistency variance for EMA adaptive alpha
        float fuelConsistencyVariance = 0f;
        if (validLaps.Count >= 5)
        {
            var last5 = validLaps.TakeLast(5).ToList();
            float mean = last5.Average(l => l.FuelUsed);
            float variance = last5.Sum(l => (float)Math.Pow(l.FuelUsed - mean, 2)) / last5.Count;
            fuelConsistencyVariance = (float)Math.Sqrt(variance);
        }
        CurrentData.FuelConsistencyVariance = fuelConsistencyVariance;
        
        // Session average with enhanced outlier filtering (Phase 2: Uses FuelOutlierDetector)
        float sessionAverage = 0f;
        if (validLaps.Count >= 3)
        {
            // Apply enhanced outlier detection (MAD + lap time + incidents)
            var analysis = _outlierDetector.DetectOutliers(validLaps);
            
            // Filter out flagged outliers for averaging
            var cleanLaps = analysis.CleanLaps;
            
            // Log outlier detection results
            if (analysis.OutlierCount > 0)
            {
                LogDebug($"OUTLIER DETECTION: Flagged {analysis.OutlierCount}/{analysis.AnalyzedLaps.Count} laps:");
                foreach (var outlier in analysis.AnalyzedLaps.Where(l => l.IsFlaggedAsOutlier))
                {
                    LogDebug($"  Lap {outlier.LapNumber}: {outlier.FuelUsed:F3}L, {outlier.LapTime:F1}s - {outlier.OutlierReason}");
                }
            }
            
            // Fallback: If outlier filtering removed too many laps (>40%), use IQR method instead
            if (cleanLaps.Count < validLaps.Count * 0.6f)
            {
                LogDebug($"WARNING: MAD filtering removed {analysis.OutlierCount}/{validLaps.Count} laps (>40%), falling back to IQR method");
                
                cleanLaps = _outlierDetector.ApplyIQRFallback(validLaps);
                
                // If IQR filtering still removed too many, use median filter
                if (cleanLaps.Count < validLaps.Count * 0.7f)
                {
                    cleanLaps = _outlierDetector.ApplyMedianFilter(validLaps);
                    LogDebug($"IQR filtering also aggressive, using median filter: {cleanLaps.Count}/{validLaps.Count} laps");
                }
                else
                {
                    LogDebug($"IQR fallback: {cleanLaps.Count}/{validLaps.Count} laps");
                }
            }
            
            sessionAverage = cleanLaps.Count > 0 
                ? cleanLaps.Average(l => l.FuelUsed) 
                : validLaps.Average(l => l.FuelUsed);
            
            LogDebug($"AvgFuelPerLap_Session = {sessionAverage:F4}L (from {cleanLaps.Count}/{validLaps.Count} laps after outlier detection)");
        }
        else
        {
            // Not enough data for outlier detection, use simple average
            sessionAverage = validLaps.Average(l => l.FuelUsed);
            LogDebug($"AvgFuelPerLap_Session = {sessionAverage:F4}L (from {validLaps.Count} laps, no filtering)");
        }
        
        // PHASE 1: Use FuelAveragingService for all averaging calculations
        var averages = _fuelAveragingService.Calculate(
            _lapHistory, 
            validLaps, 
            _stintStartLapNumber,
            fuelConsistencyVariance);
        
        // Map results to CurrentData
        CurrentData.HasSufficientData = averages.HasSufficientData;
        CurrentData.WarningMessage = averages.WarningMessage;
        CurrentData.AvgFuelPerLap_Last = averages.Last;
        CurrentData.AvgFuelPerLap_L5 = averages.L5;
        CurrentData.AvgFuelPerLap_L10 = averages.L10;
        CurrentData.AvgFuelPerLap_Session = sessionAverage; // Use outlier-filtered session average
        CurrentData.MinFuelPerLap = averages.Min;
        CurrentData.MaxFuelPerLap = averages.Max;
        CurrentData.AvgFuelPerLap_EMA = averages.EMA;
        CurrentData.AvgFuelPerLap_GreenOnly = averages.GreenOnly;
        CurrentData.GreenFlagLapCount = averages.GreenFlagLapCount;
        CurrentData.GreenFlagAverage = averages.GreenOnly;
        CurrentData.YellowFlagLapCount = averages.YellowFlagLapCount;
        CurrentData.YellowFlagAverage = averages.YellowFlagAverage;
        CurrentData.AvgFuelPerLap_Stint = averages.Stint;
        CurrentData.StintLapCount = averages.StintLapCount;
        CurrentData.AvgFuelPerLap_Adaptive = averages.Adaptive;
        CurrentData.AvgFuelPerLap_PaceLaps = averages.PaceLaps;
        CurrentData.PaceLapCount = averages.PaceLapCount;
        CurrentData.AverageLapTime = averages.AverageLapTime;
        
        // Log calculated averages
        LogDebug($"AvgFuelPerLap_Last = {CurrentData.AvgFuelPerLap_Last:F4}L");
        LogDebug($"AvgFuelPerLap_L5 = {CurrentData.AvgFuelPerLap_L5:F4}L");
        LogDebug($"AvgFuelPerLap_L10 = {CurrentData.AvgFuelPerLap_L10:F4}L");
        LogDebug($"Min/Max = {CurrentData.MinFuelPerLap:F4}L / {CurrentData.MaxFuelPerLap:F4}L");
        LogDebug($"AvgFuelPerLap_EMA = {CurrentData.AvgFuelPerLap_EMA:F4}L");
        LogDebug($"AvgFuelPerLap_GreenOnly = {CurrentData.AvgFuelPerLap_GreenOnly:F4}L (from {CurrentData.GreenFlagLapCount} green laps)");
        LogDebug($"AvgFuelPerLap_Stint = {CurrentData.AvgFuelPerLap_Stint:F4}L (from {CurrentData.StintLapCount} laps since lap {_stintStartLapNumber})");
        LogDebug($"AvgFuelPerLap_Adaptive = {CurrentData.AvgFuelPerLap_Adaptive:F4}L");
        LogDebug($"AverageLapTime = {CurrentData.AverageLapTime:F4}s");
        
        if (CurrentData.PaceLapCount > 0)
        {
            LogDebug($"PaceLapAverage = {CurrentData.AvgFuelPerLap_PaceLaps:F4}L (from {CurrentData.PaceLapCount} pace laps)");
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
            // FIX: Don't overwrite LapsRemaining if using historical predictions (preserves estimate until real data)
            if (!CurrentData.UsingHistoricalPredictions)
            {
                CurrentData.LapsRemaining = 0;
            }
            CurrentData.CanFinishWithoutStop = false;
            return;
        }
        
        // Account for car-specific fuel sputtering threshold (Enhanced Phase 2.1)
        // Only calculate usable fuel (fuel above the sputtering threshold)
        float sputteringThreshold = CurrentData.FuelSputteringThreshold;
        float usableFuel = Math.Max(0, CurrentData.CurrentFuel - sputteringThreshold);
        
        // Laps remaining based on selected averaging method (using usable fuel only)
        CurrentData.LapsRemaining = usableFuel / avgFuel;
        
        // FIX: Once we calculate with real data, clear historical predictions flag
        if (CurrentData.UsingHistoricalPredictions && _lapHistory.Count >= 1)
        {
            CurrentData.UsingHistoricalPredictions = false;
            Console.WriteLine($"[FuelCalculator] ✅ Switched from historical estimate to real data (1st lap complete)");
        }
        
        string dataSource = CurrentData.UsingHistoricalPredictions ? "[HISTORICAL EST]" : "[ACTUAL DATA]";
        LogDebug($"LapsRemaining = {CurrentData.LapsRemaining:F4} {dataSource} (Usable fuel: {usableFuel:F4}L / Avg: {avgFuel:F4}L, Sputtering threshold: {sputteringThreshold:F2}L [{FuelSputteringDatabase.GetCarCategory(CurrentData.CarClassId)}])");
        
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

    // ===== DEAD CODE REMOVED IN REFACTORING =====
    // Phase 1: Pit Strategy methods → PitStrategyService.Calculate()
    //   - CalculateOptimalPitLap, CalculatePitExitPosition, CalculateMultiStopStrategy, CalculatePartialRefuelOptimization
    // Phase 2-4: Dead code methods removed
    //   - CalculateDynamicBufferLaps, CalculateDeltaTracking, CalculateFuelSaving
    // Phase 5: GenerateStrategicAlerts (86 lines, never called - FuelSavingCalculator has its own version)

    /// <summary>
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

    // ===== PHASE 2 REFACTOR: CalculateDynamicBufferLaps removed =====
    // Dynamic buffer calculation consolidated into DynamicBufferCalculator service
    // Called via _bufferCalculator.Calculate() in Update() method (~160 lines removed)

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
    /// Calculate Median Absolute Deviation (MAD) for robust outlier detection
    /// MAD is more robust than standard deviation for small datasets with outliers
    /// </summary>
    /// <param name="values">List of values to analyze</param>
    /// <returns>MAD value (median of absolute deviations from median)</returns>
    
    /// <summary>
    /// Reset all fuel calculations (call when session changes)
    /// </summary>
    public void Reset()
    {
        _lapHistory.Clear();
        CurrentData = new FuelData();
        _fuelAtLapStart = 0;
        _lastFuelLevel = 0;
        _lastCompletedLap = -1;
        _isFirstUpdate = true;
        _currentFlagStatus = LapFlagStatus.Green;
        _wasOnPitRoadLastUpdate = false;
        _justLeftPits = false;
        _justProcessedLap = false;
        // REMOVED: Fuel pressure tracking reset
        // _baselineFuelPressureEstablished = false;
        // _fuelPressureHistory.Clear();
        _lapsCompletedWhenProcessed = -1;
        _lastIncidentCount = 0;  // Reset incident tracking
        _lapDistPctAtLapStart = 0f;  // Reset grid start lap tracking
        
        // FIX: Reset historical data flag to allow re-application on new session
        _historicalDataApplied = false;
        
        // Phase 1: Reset FuelAveragingService EMA state
        _fuelAveragingService.Reset();
        
        // Phase 6: Reset extracted services
        _pitStopTracker.Reset();
        
        // Phase 7: Reset lap delta tracker
        _lapDeltaTracker.Reset();
        
        // Phase 9: Reset historical data flag
        _historicalDataApplied = false;
    }
    
    /// <summary>
    /// Phase 9: Apply historical predictions at session start
    /// Provides instant fuel/tire predictions instead of 3-lap warmup
    /// FIX: Pre-calculate LapsRemaining so it shows immediately on mid-session app start
    /// </summary>
    private void ApplyHistoricalPredictions(TelemetryData telemetry)
    {
        if (_historyService == null || _historicalDataApplied)
            return;
        
        var prediction = _historyService.GetPrediction(
            telemetry.TrackName,
            telemetry.PlayerCarClass);
        
        if (prediction == null || !prediction.IsHighConfidence)
        {
            Console.WriteLine($"[FuelCalculator] No high-confidence historical data for {telemetry.TrackName} / Class {telemetry.PlayerCarClass}");
            return;
        }
        
        // Apply historical fuel prediction
        CurrentData.AvgFuelPerLap_Session = prediction.AvgFuelPerLap;
        CurrentData.AvgFuelPerLap_L5 = prediction.AvgFuelPerLap;
        CurrentData.AvgFuelPerLap_L10 = prediction.AvgFuelPerLap;
        // NOTE: Do NOT set AvgFuelPerLap_Last - it should remain 0 until first lap completes
        CurrentData.AvgFuelPerLap_Last = 0f; // Explicitly zero out
        CurrentData.AverageLapTime = prediction.AvgLapTime;
        CurrentData.HistoricalFuelAverage = prediction.AvgFuelPerLap;
        
        // Apply tire predictions
        CurrentData.MaxTireWearRate = prediction.AvgTireWearRate;
        CurrentData.TireLapsRemaining = prediction.TireLapsAverage;
        
        // FIX: Pre-calculate LapsRemaining using historical avg (prevents showing 0 laps on app start mid-session)
        float sputteringThreshold = FuelSputteringDatabase.GetSputteringThreshold(
            telemetry.PlayerCarClass, 
            telemetry.FuelLevelMax);
        float usableFuel = Math.Max(0, telemetry.FuelLevel - sputteringThreshold);
        CurrentData.LapsRemaining = usableFuel / prediction.AvgFuelPerLap;
        
        // Mark as applied and show confidence
        _historicalDataApplied = true;
        CurrentData.HasSufficientData = true;
        CurrentData.UsingHistoricalPredictions = true;
        CurrentData.HistoricalConfidence = prediction.ConfidenceScore;
        CurrentData.HistoricalSessionCount = prediction.SessionCount;
        
        Console.WriteLine($"[FuelCalculator] ✅ Applied historical prediction: {prediction.AvgFuelPerLap:F2}L/lap → {CurrentData.LapsRemaining:F1} laps remaining " +
                         $"(confidence: {prediction.ConfidenceScore:F0}%, {prediction.SessionCount} sessions)");
        LogDebug($"Historical prediction: Usable fuel: {usableFuel:F2}L, Avg: {prediction.AvgFuelPerLap:F2}L/lap, Sputtering: {sputteringThreshold:F2}L");
    }
    
    /// <summary>
    /// Phase 9: Save session data to history
    /// Should be called at end of session for learning
    /// </summary>
    public async Task SaveSessionHistoryAsync(TelemetryData telemetry)
    {
        if (_historyService == null || _lapHistory.Count < 5)
            return; // Need at least 5 laps for reliable data
        
        // Calculate session averages
        var validLaps = _lapHistory.Where(l => l.IsValidForAveraging).ToList();
        if (validLaps.Count == 0)
            return;
        
        float avgFuel = validLaps.Average(l => l.FuelUsed);
        float avgLapTime = validLaps.Average(l => l.LapTime);
        
        // Get tire data if available (simplified for now)
        float avgTireWear = CurrentData.MaxTireWearRate;
        int tireLaps = CurrentData.TireLapsRemaining;
        
        await _historyService.UpdateHistoryAsync(
            telemetry.TrackName,
            telemetry.PlayerCarClass,
            avgFuel,
            avgLapTime,
            avgTireWear,
            tireLaps);
        
        Console.WriteLine($"[FuelCalculator] Saved session history: {avgFuel:F2}L/lap, {avgLapTime:F1}s/lap");
    }
    
    /// <summary>
    /// Get lap history for analysis/export
    /// </summary>
    public IReadOnlyList<FuelLapHistory> GetLapHistory() => _lapHistory.AsReadOnly();
    
    /// <summary>
    /// <summary>
    /// Apply temperature correction to fuel consumption averages (ENHANCEMENT)
    /// Optional Fine-Tuning: Delegated to TemperatureCompensationService
    /// </summary>
    private void ApplyTemperatureCorrection(TelemetryData telemetry)
    {
        _temperatureCompensation.ApplyTemperatureCorrection(telemetry, _sessionStats, CurrentData);
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
    /// Apply pit strategy results from PitStrategyService to CurrentData
    /// Phase 1 refactor: Single source of truth for pit strategy
    /// </summary>
    private void ApplyPitStrategyToCurrentData(PitStrategy strategy)
    {
        // Basic pit strategy
        CurrentData.OptimalPitLap = strategy.OptimalPitLap;
        CurrentData.OptimalPitReason = strategy.OptimalPitReason;
        CurrentData.FuelToAddAtPit = strategy.FuelToAddAtPit;
        CurrentData.CanFinishWithoutStop = strategy.CanFinishWithoutStop;
        
        // Pit window
        CurrentData.EarliestPitLap = strategy.EarliestPitLap;
        CurrentData.LatestPitLap = strategy.LatestPitLap;
        CurrentData.PitWindowStart = strategy.PitWindowStart;
        CurrentData.PitWindowEnd = strategy.PitWindowEnd;
        CurrentData.PitWindowReason = strategy.PitWindowReason;
        
        // Position prediction
        CurrentData.PitExitPosition = strategy.PitExitPosition;
        CurrentData.PitExitGapDescription = strategy.PitExitGapDescription;
        CurrentData.PitExitPositionValid = strategy.PitExitPositionValid;
        CurrentData.RacePosition = strategy.RacePosition;
        CurrentData.TotalCars = strategy.TotalCars;
        
        // Multi-stop strategy
        CurrentData.OneStopTotalTime = strategy.OneStopTotalTime;
        CurrentData.TwoStopTotalTime = strategy.TwoStopTotalTime;
        CurrentData.ThreeStopTotalTime = strategy.ThreeStopTotalTime;
        CurrentData.RecommendedStopsReason = strategy.MultiStopRecommendation;
        
        // Analysis factors
        CurrentData.FuelCriticalityScore = strategy.FuelCriticalityScore;
        CurrentData.TrackPositionCost = strategy.TrackPositionCost;
        CurrentData.YellowFlagExpected = strategy.YellowFlagProbability > 50f;
        CurrentData.LapsUntilYellow = strategy.EstimatedLapsUntilYellow;
        
        // Partial refuel (if available)
        // Note: PartialRefuelRecommendation could map to AlternativeStrategyInfo
        if (!string.IsNullOrEmpty(strategy.PartialRefuelRecommendation))
        {
            CurrentData.AlternativeStrategyInfo = strategy.PartialRefuelRecommendation;
        }
        
        LogDebug($"[STRATEGY] Pit lap {strategy.OptimalPitLap}: {strategy.OptimalPitReason}");
    }

    /// <summary>
    /// Write debug message to log file
    /// Only enabled in DEBUG builds to reduce file I/O overhead in production
    /// </summary>
    private void LogDebug(string message)
    {
#if DEBUG
        try
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MRT-UI",
                "fuel_debug.log"
            );
            
            // Ensure directory exists
            var directory = System.IO.Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
            
            var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
            System.IO.File.AppendAllText(logPath, logMessage);
        }
        catch
        {
            // Ignore logging errors
        }
#endif
    }
}
