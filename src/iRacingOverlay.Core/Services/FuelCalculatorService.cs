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
    
    // EMA (Exponential Moving Average) tracking - REMOVED: No longer used, replaced by DeltaTrackingService
    // private float _emaValue = 0f;  // Current EMA value
    // private bool _emaInitialized = false;  // Whether EMA has been initialized with first lap
    // REMOVED: Fixed EMA_ALPHA constant - now calculated adaptively based on fuel consistency
    
    // Incident tracking for outlier detection
    private int _lastIncidentCount = 0;  // Track incident count to detect new incidents during laps
    
    // Delta tracking for convergence analysis and historical accuracy (Phase 3)
    private readonly List<DeltaHistoryRecord> _deltaHistory = new();
    private const int MAX_DELTA_HISTORY = 50;  // Keep last 50 laps of delta data
    
    // Pit stop tracking state machine
    private readonly SessionPersistenceService _persistenceService;
    private PitStopData? _currentPitStop = null;
    private SessionStatistics? _sessionStats = null;
    
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
        
        // Track fuel pressure and establish baseline (Enhanced Phase 2.1)
        UpdateFuelPressureTracking(telemetry);
        
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
                    _sessionStateAtLapStart = telemetry.SessionState; // Save for next lap
                    _lapDistPctAtLapStart = telemetry.LapDistPct; // Save starting position for lap 1
                    LogDebug($"   _fuelAtLapStart reset to {_fuelAtLapStart:F2}L for lap 1");
                }
                else if (telemetry.LapsCompleted != _lapsCompletedWhenProcessed)
                {
                    // Call lap completion with refueling flag and pit flag
                    OnLapCompleted(telemetry, isRefueling, _pittedThisLap, _fuelBeforeEnteringPits);
                    _lapsCompletedWhenProcessed = telemetry.LapsCompleted;
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
        bool shouldDoFullCalculation = _virtualLapsCompleted != _lapsCompletedWhenProcessed || isRefueling;

        if (shouldDoFullCalculation)
        {
            // FULL STRATEGIC CALCULATION PATH
            LogDebug("[PROGRESSIVE_CALC] Full strategic calculation (lap completed or refueled)");

            // Calculate averages and strategy
            CalculateAverages();
            ApplyTemperatureCorrection(telemetry);  // ENHANCEMENT: Temperature correction for fuel consumption
            ApplyRealTimeFuelFlow(telemetry);      // ENHANCEMENT: Real-time fuel flow integration

            // Calculate dynamic buffer using service (Phase 6)
            var bufferData = _bufferCalculator.Calculate(
                CurrentData.FuelConsistencyVariance,  // Use existing property
                CurrentData.RacePosition,  // Fixed: Use RacePosition instead of Position
                CurrentData.TotalCars,
                false,  // isRaining - simplified for now, can enhance later
                0,  // yellowFlagCount - can add to FuelData if needed
                telemetry.LapsCompleted + 1,
                telemetry.SessionLaps,
                CurrentData.IsTimedSession
            );
            CurrentData.FuelBufferLaps = bufferData.TotalBuffer;
            // BufferReason can be added to FuelData if needed

            CalculateStrategy();

            // Calculate pit strategy using extracted service (Phase 1 refactor)
            var fuelAverages = new FuelAverages
            {
                Current = CurrentData.AvgFuelPerLap,
                Last = CurrentData.AvgFuelPerLap_Last,
                L5 = CurrentData.AvgFuelPerLap_L5,
                L10 = CurrentData.AvgFuelPerLap_L10,
                EMA = CurrentData.AvgFuelPerLap_EMA,
                Session = CurrentData.AvgFuelPerLap_Session
            };
            var pitStrategy = _pitStrategyService.Calculate(telemetry, CurrentData, fuelAverages, _lapHistory);
            ApplyPitStrategyToCurrentData(pitStrategy);

            // Track delta using service (Phase 5) - simplified for now
            var deltaData = _deltaTrackingService.Track(
                CurrentData.LapsRemaining,
                CurrentData.IRacingLapsRemaining,
                telemetry.LapsCompleted + 1
            );
            // Delta properties can be added to FuelData if needed

            // Calculate fuel saving using service (Phase 4) - simplified for now
            var averages = new FuelAverages
            {
                Current = CurrentData.AvgFuelPerLap,
                Last = CurrentData.AvgFuelPerLap_Last,
                L5 = CurrentData.AvgFuelPerLap_L5,
                L10 = CurrentData.AvgFuelPerLap_L10,
                EMA = CurrentData.AvgFuelPerLap_EMA,
                Session = CurrentData.AvgFuelPerLap_Session
            };
            var strategy = new PitStrategy
            {
                OptimalPitLap = CurrentData.OptimalPitLap,
                FuelToAddAtPit = CurrentData.FuelToAddAtPit,
                CanFinishWithoutStop = CurrentData.CanFinishWithoutStop
            };
            var savingData = _fuelSavingCalculator.Calculate(telemetry, CurrentData, averages, strategy);

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

            // Track pit stops for session persistence
            UpdatePitStopTracking(telemetry);

            // Mark this as a full strategic update (not live)
            CurrentData.IsLiveUpdate = false;

            // Fire update event immediately (full calculation)
            FuelDataUpdated?.Invoke(this, CurrentData);
        }
        else if (telemetry.LapDistPct > 0.05f && !telemetry.OnPitRoad)
        {
            // LIGHTWEIGHT LIVE UPDATE PATH (mid-lap projections)
            // Only update if at least 5% into lap and not on pit road
            UpdateLiveValues(telemetry);

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
    /// Update live values mid-lap (Progressive Calculation - Option 3)
    /// FIX #3: Enhanced with LIVE PIT WINDOW CALCULATIONS for real-time strategy updates
    /// </summary>
    private void UpdateLiveValues(TelemetryData telemetry)
    {
        // Project current lap fuel usage to full lap
        float lapProgress = telemetry.LapDistPct;
        if (lapProgress <= 0.05f)
            return; // Too early in lap for reliable projection

        // Calculate projected lap fuel usage based on current progress
        float fuelUsedSoFar = CurrentData.FuelUsedThisLap;
        float projectedLapUsage = fuelUsedSoFar / lapProgress;

        // Update current lap fuel rate (live projection)
        CurrentData.CurrentLapFuelRate = projectedLapUsage;

        // Calculate live laps remaining using projected usage
        // Uses current fuel and projected usage (more responsive than historical average)
        if (projectedLapUsage > 0)
        {
            CurrentData.LapsRemaining = telemetry.FuelLevel / projectedLapUsage;
        }

        // Update live fuel needed to finish (uses existing strategy average, not projected)
        // This balances live feel with strategic accuracy
        if (CurrentData.AvgFuelPerLap > 0)
        {
            CurrentData.FuelNeededToFinish = CurrentData.RaceLapsRemaining * CurrentData.AvgFuelPerLap;
            CurrentData.FuelDeltaToFinish = telemetry.FuelLevel - CurrentData.FuelNeededToFinish;
            CurrentData.CanFinishWithoutStop = CurrentData.FuelDeltaToFinish >= 0;
        }

        // ===== FIX #3: LIVE PIT WINDOW CALCULATIONS =====
        // Update pit window in real-time as player progresses through current lap
        // This makes strategy truly "live" instead of frozen until lap completion
        if (CurrentData.RaceLapsRemaining > 0 && CurrentData.AvgFuelPerLap_L5 > 0)
        {
            float avgFuel = CurrentData.AvgFuelPerLap_L5;
            float lapsOnCurrentFuel = CurrentData.LapsRemaining;

            // Current lap position (fractional lap number)
            // Example: Lap 10 at 50% = 10.5, Lap 15 at 75% = 15.75
            float currentLapFractional = telemetry.LapsCompleted + lapProgress;

            // LIVE FUEL CRITICALITY (updates every frame)
            if (lapsOnCurrentFuel < 1.0f)
                CurrentData.FuelCriticalityScore = 100f; // CRITICAL
            else if (lapsOnCurrentFuel < 2.0f)
                CurrentData.FuelCriticalityScore = 80f + (2.0f - lapsOnCurrentFuel) * 20f; // URGENT (80-100)
            else if (lapsOnCurrentFuel < 5.0f)
                CurrentData.FuelCriticalityScore = 50f + (5.0f - lapsOnCurrentFuel) * 10f; // MODERATE (50-80)
            else if (lapsOnCurrentFuel < 10.0f)
                CurrentData.FuelCriticalityScore = 20f + (10.0f - lapsOnCurrentFuel) * 6f; // COMFORTABLE (20-50)
            else
                CurrentData.FuelCriticalityScore = Math.Max(0f, 20f - (lapsOnCurrentFuel - 10.0f) * 2f); // PLENTY (0-20)

            // LIVE PIT WINDOW (earliest, optimal, latest)
            // Earliest: When enough fuel has been used to add race-ending fuel
            float fuelNeededToFinish = (CurrentData.RaceLapsRemaining + CurrentData.FuelBufferLaps) * avgFuel + CurrentData.FuelSputteringThreshold;
            float fuelAvailableToAdd = CurrentData.TankCapacity - CurrentData.CurrentFuel;
            float fuelNeedsToBurn = Math.Max(0, fuelNeededToFinish - fuelAvailableToAdd);
            float lapsToEarliestPit = fuelNeedsToBurn / avgFuel;
            CurrentData.EarliestPitLap = (int)Math.Ceiling(currentLapFractional + lapsToEarliestPit);

            // Latest: Just before running out (with buffer)
            float lapsToLatestPit = lapsOnCurrentFuel - CurrentData.FuelBufferLaps;
            CurrentData.LatestPitLap = (int)Math.Floor(currentLapFractional + Math.Max(0, lapsToLatestPit));

            // Optimal: Mid-window (3-5 lap green zone)
            CurrentData.OptimalPitLap = (CurrentData.EarliestPitLap + CurrentData.LatestPitLap) / 2;

            // Pit window range
            CurrentData.PitWindowStart = CurrentData.EarliestPitLap;
            CurrentData.PitWindowEnd = CurrentData.LatestPitLap;

            // Update optimal pit reason based on criticality
            if (CurrentData.FuelCriticalityScore >= 95)
                CurrentData.OptimalPitReason = "⚠️ CRITICAL FUEL - PIT NOW";
            else if (CurrentData.FuelCriticalityScore >= 80)
                CurrentData.OptimalPitReason = "⚠️ Urgent - Pit soon";
            else if (CurrentData.FuelCriticalityScore >= 50)
                CurrentData.OptimalPitReason = "🔵 In pit window";
            else
                CurrentData.OptimalPitReason = "✓ Fuel comfortable";
        }

        // Log live update for debugging (first few laps only)
        if (telemetry.Lap <= 3 && _liveUpdateCounter == 0)
        {
            LogDebug($"[LIVE_UPDATE] Lap {telemetry.Lap} @ {lapProgress:P0} | " +
                    $"Projected: {projectedLapUsage:F2}L/lap | " +
                    $"Live Laps Remaining: {CurrentData.LapsRemaining:F1} | " +
                    $"Pit Window: L{CurrentData.PitWindowStart}-L{CurrentData.PitWindowEnd} (Optimal: L{CurrentData.OptimalPitLap}) | " +
                    $"Criticality: {CurrentData.FuelCriticalityScore:F0}");
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
        // FIX: Use saved fuel level from before pit entry to calculate fuel used
        // This prevents refueling from contaminating the lap fuel calculation
        float fuelAtLapEnd = pittedThisLap && fuelBeforePit > 0 ? fuelBeforePit : telemetry.FuelLevel;
        float fuelUsed = _fuelAtLapStart - fuelAtLapEnd;

        LogDebug($"LAP {telemetry.LapsCompleted} fuel calculation: Start={_fuelAtLapStart:F2}L, End={fuelAtLapEnd:F2}L, Used={fuelUsed:F3}L, Pitted={pittedThisLap}");

        // ===== USER INSIGHT: TOW DETECTION USING SDK DATA ONLY =====
        // REMOVED: Heuristic "low fuel = tow" detection (caused false positives on lap 1)
        // NEW: Use ONLY SDK refueling flag - if fuel increased, SDK tells us directly
        //
        // EXAMPLE OF THE PROBLEM:
        //   OLD: Lap 1 uses 0.044L (formation lap) → marked as "tow" → excluded from averages ❌
        //   NEW: Lap 1 uses 0.044L → only marked invalid if it's truly a refuel event ✅
        //
        // BENEFITS:
        //   - Uses actual SDK data (fuel delta > 0.3L = refuel) instead of guessing
        //   - No false positives on legitimate low-fuel laps (formation, slow starts)
        //   - Cleaner logic, fewer invalid lap exclusions
        //
        // NOTE: wasRefueled parameter already contains the SDK refueling indicator
        //       No need for separate tow detection - refuel IS the tow indicator

        // Detect out-lap: first lap after leaving pits (previous lap was pit lap OR just left pits flag)
        bool isOutLap = _justLeftPits || (_lapHistory.Count > 0 && _lapHistory.Last().WasPitLap);

        // FIX: Formation lap (lap 0) is skipped BEFORE calling this function
        // No need to check for formation lap here
        bool isFormationLap = false;

        // FIX: Check if this is a pace lap using SessionState from lap START (not lap END)
        // PROBLEM: If lap 1 starts during parade (SessionState=3) but green flag drops mid-lap,
        //          SessionState will be 4 (Racing) when lap completes, incorrectly marking it as non-pace
        // SOLUTION: Use _sessionStateAtLapStart which was saved when the lap began
        bool isPaceLap = _sessionStateAtLapStart == 3;

        // FIX: WasPitLap should ONLY be true if we actually REFUELED (not low fuel or tow)
        // Only actual refueling should mark a lap as a pit lap
        // Tow is handled separately and should not exclude lap from averages
        bool wasPitLap = wasRefueled;
        
        // FIX: Detect grid start partial laps - when grid is behind S/F line
        // At race start, lap 1 may only cover a small portion of the track (grid to S/F)
        // This uses very little fuel (e.g., 0.09L instead of 1.0L) and corrupts averages
        // Detection: Lap 1 with less than 50% track distance covered
        float lapDistanceCovered = 1.0f - _lapDistPctAtLapStart; // How much of track we actually covered
        if (lapDistanceCovered < 0) lapDistanceCovered += 1.0f; // Handle wrap-around
        bool isGridStartLap = telemetry.LapsCompleted == 1 && lapDistanceCovered < 0.5f;
        
        if (isGridStartLap)
        {
            LogDebug($"🏁 GRID START LAP DETECTED: Lap 1 only covered {lapDistanceCovered:P0} of track (started at {_lapDistPctAtLapStart:P0})");
            LogDebug($"   Fuel used: {fuelUsed:F3}L - This lap will be EXCLUDED from averages");
        }

        // DEBUG: Log lap completion details with ALL validity flags
        LogDebug($"Lap {telemetry.LapsCompleted} completed: FuelAtStart={_fuelAtLapStart:F3}L, FuelAtEnd={fuelAtLapEnd:F3}L, FuelUsed={fuelUsed:F3}L");
        LogDebug($"  Flags: PitLap={wasPitLap}, OutLap={isOutLap}, Formation={isFormationLap}, PaceLap={isPaceLap}, GridStart={isGridStartLap} (SessionState@Start={_sessionStateAtLapStart}, @End={telemetry.SessionState}), EnteredPitRoad={pittedThisLap}");

        // Create lap history record
        var lapRecord = new FuelLapHistory
        {
            LapNumber = telemetry.LapsCompleted,
            FuelUsed = Math.Max(0, fuelUsed), // Don't record negative fuel
            LapTime = telemetry.LapLastLapTime,
            FuelAtStart = _fuelAtLapStart,
            FuelAtEnd = fuelAtLapEnd, // Use saved fuel if pitted, current fuel otherwise
            FlagStatus = _currentFlagStatus,
            WasPitLap = wasPitLap, // True if refueled (or towed with refuel)
            RefuelAmount = wasRefueled ? CurrentData.LastRefuelAmount : 0, // Only record refuel if SDK detected fuel increase
            IsFormationLap = isFormationLap,
            IsOutLap = isOutLap, // Flag out-laps for exclusion from averages (cool tires, careful driving)
            IsIncompleteLap = false, // If OnLapCompleted fires, the lap WAS completed (LapDistPct resets to 0)
            IsGridStartLap = isGridStartLap, // FIX: Grid start partial lap (grid behind S/F line)
            LapDistanceCovered = lapDistanceCovered, // Track percentage of lap covered
            Timestamp = DateTime.UtcNow,
            IncidentCountAtStart = _lastIncidentCount,  // Incident count at lap start
            IncidentCountAtEnd = telemetry.PlayerCarMyIncidentCount,  // Incident count at lap end
            SessionState = _sessionStateAtLapStart  // FIX: Use SessionState from lap START for accurate pace lap detection
        };
        
        // DEBUG: Log validation result
        LogDebug($"  IsValidForAveraging={lapRecord.IsValidForAveraging} (needs: !PitLap && !Formation && !Incomplete && !PaceLap && !OutLap && !GridStart && FuelUsed>0)");
        
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

    // ===== DEAD CODE REMOVED IN REFACTORING =====
    // Phase 1: Pit Strategy methods → PitStrategyService.Calculate()
    //   - CalculateOptimalPitLap, CalculatePitExitPosition, CalculateMultiStopStrategy, CalculatePartialRefuelOptimization
    // Phase 2-4: Dead code methods removed
    //   - CalculateDynamicBufferLaps, CalculateDeltaTracking, CalculateFuelSaving
    // Phase 5: GenerateStrategicAlerts (86 lines, never called - FuelSavingCalculator has its own version)

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
        _lapDistPctAtLapStart = 0f;  // Reset grid start lap tracking
        
        // Reset pit stop tracking
        _currentPitStop = null;
        _pitState = PitStopState.NotOnPitRoad;
        
        // Phase 1: Reset FuelAveragingService EMA state
        _fuelAveragingService.Reset();
        
        // Phase 7: Reset lap delta tracker
        _lapDeltaTracker.Reset();
        
        // Phase 9: Reset historical data flag
        _historicalDataApplied = false;
    }
    
    /// <summary>
    /// Phase 9: Apply historical predictions at session start
    /// Provides instant fuel/tire predictions instead of 3-lap warmup
    /// </summary>
    private void ApplyHistoricalPredictions(TelemetryData telemetry)
    {
        if (_historyService == null || _historicalDataApplied)
            return;
        
        var prediction = _historyService.GetPrediction(
            telemetry.TrackName,
            telemetry.PlayerCarClass);
        
        if (prediction == null || !prediction.IsHighConfidence)
            return;
        
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
        
        // Mark as applied and show confidence
        _historicalDataApplied = true;
        CurrentData.HasSufficientData = true;
        CurrentData.UsingHistoricalPredictions = true;
        CurrentData.HistoricalConfidence = prediction.ConfidenceScore;
        CurrentData.HistoricalSessionCount = prediction.SessionCount;
        
        Console.WriteLine($"[FuelCalculator] Applied historical prediction: {prediction.AvgFuelPerLap:F2}L/lap " +
                         $"(confidence: {prediction.ConfidenceScore:F0}%, sessions: {prediction.SessionCount})");
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

            // FIX #3 (CORRECTED): Apply temperature correction to stored averages ONCE per calculation cycle
            // This is called AFTER CalculateAverages() populates the values, so we modify them once
            // On next cycle, CalculateAverages() will recalculate from raw lap data, then this applies correction again
            // This prevents compounding because we always start from fresh raw averages each cycle
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
    /// ENABLED: Debug logging for diagnostics
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
    }
}
