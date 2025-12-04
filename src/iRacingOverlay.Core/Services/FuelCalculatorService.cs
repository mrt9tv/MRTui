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
        _pitStrategyService = new PitStrategyService();
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

            // Calculate pit strategy (optimal pit lap and multi-stop strategy)
            CalculateOptimalPitLap(telemetry);
            CalculateMultiStopStrategy(telemetry);

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

        // DEBUG: Log lap completion details with ALL validity flags
        LogDebug($"Lap {telemetry.LapsCompleted} completed: FuelAtStart={_fuelAtLapStart:F3}L, FuelAtEnd={fuelAtLapEnd:F3}L, FuelUsed={fuelUsed:F3}L");
        LogDebug($"  Flags: PitLap={wasPitLap}, OutLap={isOutLap}, Formation={isFormationLap}, PaceLap={isPaceLap} (SessionState@Start={_sessionStateAtLapStart}, @End={telemetry.SessionState}), EnteredPitRoad={pittedThisLap}");

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
            Timestamp = DateTime.UtcNow,
            IncidentCountAtStart = _lastIncidentCount,  // Incident count at lap start
            IncidentCountAtEnd = telemetry.PlayerCarMyIncidentCount,  // Incident count at lap end
            SessionState = _sessionStateAtLapStart  // FIX: Use SessionState from lap START for accurate pace lap detection
        };
        
        // DEBUG: Log validation result
        LogDebug($"  IsValidForAveraging={lapRecord.IsValidForAveraging} (needs: !PitLap && !Formation && !Incomplete && !PaceLap && !OutLap && FuelUsed>0)");
        
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
        
        // FIX #2 (Baseline Consistency): Respect user's selected averaging method
        // Previously hardcoded Session average, but this created contradictory messages
        // when Strategy used a different method (e.g., L5 or Adaptive)
        // NOW: Use the same averaging method as strategy calculations for consistency
        float baselineAverage = CurrentData.AvgFuelPerLap; // Respects SelectedMethod
        
        // Fallback: if selected method returns 0, cascade through available methods
        if (baselineAverage <= 0)
        {
            baselineAverage = CurrentData.AvgFuelPerLap_L5 > 0 ? CurrentData.AvgFuelPerLap_L5 :
                             CurrentData.AvgFuelPerLap_L10 > 0 ? CurrentData.AvgFuelPerLap_L10 :
                             CurrentData.AvgFuelPerLap_Session;
        }
        
        // Use baseline directly - no longer override with L5
        // Both Strategy and FuelSaving now use same method (SelectedMethod)
        float avgFuel = baselineAverage;
        
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
                    // ENHANCEMENT: Only use historical data if conditions are similar
                    if (_lapHistory.Count < 5)
                    {
                        bool conditionsMatch = _sessionStats.IsSimilarConditions(telemetry.TrackTemp, telemetry.AirTemp);

                        if (conditionsMatch)
                        {
                            CurrentData.AvgFuelPerLap_Session = _sessionStats.SessionAverageFuelPerLap;
                            LogDebug($"BOOTSTRAP: Using historical average {_sessionStats.SessionAverageFuelPerLap:F3}L (only {_lapHistory.Count} laps, conditions match)");
                        }
                        else
                        {
                            // Conditions differ - apply temperature correction to historical data
                            float tempDelta = telemetry.AirTemp - _sessionStats.AvgAirTemp;
                            float correctionFactor = 1.0f + (tempDelta * 0.0025f); // +10°C = +2.5% fuel
                            float correctedHistorical = _sessionStats.SessionAverageFuelPerLap * correctionFactor;

                            CurrentData.AvgFuelPerLap_Session = correctedHistorical;
                            LogDebug($"BOOTSTRAP: Using temperature-corrected historical average {correctedHistorical:F3}L (conditions differ: {tempDelta:+0.0;-0.0}°C, {_lapHistory.Count} laps)");
                        }
                    }
                }
            }
            else
            {
                LogDebug($"NO SESSION STATS AVAILABLE: First time at {_currentTrackName} with class {_currentCarClassId}");
            }
        }
        
        // Estimate pit stop time using historical data OR conservative default formula
        // bool usingHistoricalData = false; // REMOVED: Variable assigned but never used
        bool environmentalMatch = false;
        
        if (_sessionStats != null && _sessionStats.HasSufficientData)
        {
            // Check if current environmental conditions match historical data
            environmentalMatch = _sessionStats.IsSimilarConditions(telemetry.TrackTemp, telemetry.AirTemp);
            
            if (environmentalMatch)
            {
                // Use historical data - high confidence
                CurrentData.EstimatedPitStopTime = _sessionStats.AverageTotalPitTime;
                // usingHistoricalData = true; // REMOVED: Variable not used
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
            // No historical data - use pit lane database formula
            // Conservative: Assume slower pit speed (15 m/s instead of 16.7) and longer service time (8s instead of 7s)
            float trackLength = telemetry.TrackLength;
            float pitSpeed = telemetry.TrackPitSpeedLimit > 0 ? telemetry.TrackPitSpeedLimit : 15.0f; // Use parsed limit or conservative 54 kph

            // ENHANCEMENT: Use track-specific pit lane length from database
            float pitLanePercentage = PitLaneDatabase.GetPitLanePercentage(telemetry.TrackName);
            float estimatedPitLaneLength = trackLength * pitLanePercentage;

            float pitTransitTime = estimatedPitLaneLength / pitSpeed; // seconds in pit lane
            float serviceTime = 8.0f; // Conservative service time (fuel only, no tire change)
            CurrentData.EstimatedPitStopTime = pitTransitTime + serviceTime;
            
            LogDebug($"PIT STOP ESTIMATE (TRACK DATABASE - MODERATE CONFIDENCE): {CurrentData.EstimatedPitStopTime:F1}s");
            LogDebug($"  Track={trackLength:F0}m, PitLane={estimatedPitLaneLength:F0}m ({pitLanePercentage*100:F1}%), Category: {PitLaneDatabase.GetTrackCategory(telemetry.TrackName)}");
            LogDebug($"  PitSpeed={pitSpeed:F1}m/s, Transit={pitTransitTime:F1}s, Service={serviceTime:F1}s");
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
        // CRITICAL FIX: Only block pit window if RaceLapsRemaining <= 0 (not in a race)
        // DO NOT block if CanFinishWithoutStop = true, because:
        // 1. User might have large tank but start with partial fuel (needs pit window)
        // 2. Strategic pitting for tires/damage/undercut is valuable even with enough fuel
        // 3. Pit window provides race strategy insight beyond just fuel emergencies
        if (CurrentData.RaceLapsRemaining <= 0)
        {
            // Debug logging to diagnose why pit window isn't showing
            LogDebug($"⚠️ PIT WINDOW BLOCKED: RaceLapsRemaining = {CurrentData.RaceLapsRemaining} (SessionLaps={telemetry.SessionLaps}, LapsCompleted={telemetry.LapsCompleted}, IsTimedSession={CurrentData.IsTimedSession}, AvgLapTime={CurrentData.AverageLapTime:F2}s, TimeRemaining={CurrentData.SessionTimeRemaining:F0}s)");
            
            CurrentData.OptimalPitLap = 0;
            CurrentData.OptimalPitReason = null;
            CurrentData.EarliestPitLap = 0;
            CurrentData.LatestPitLap = 0;
            CurrentData.PitWindowStart = 0;
            CurrentData.PitWindowEnd = 0;
            CurrentData.PitWindowReason = null;
            return;
        }
        
        // Log if we can finish without stop (for debugging, but don't block calculation)
        if (CurrentData.CanFinishWithoutStop)
        {
            LogDebug($"ℹ️ PIT WINDOW: CanFinishWithoutStop = true, but calculating pit window anyway (FuelDeltaToFinish={CurrentData.FuelDeltaToFinish:F2}L, CurrentFuel={CurrentData.CurrentFuel:F2}L, FuelNeededToFinish={CurrentData.FuelNeededToFinish:F2}L, LapsRemaining={CurrentData.LapsRemaining:F2})");
        }
        
        float avgFuel = CurrentData.AvgFuelPerLap_L5;
        if (avgFuel <= 0)
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
        
        // ===== SECTION 3: ENHANCED YELLOW FLAG PROBABILITY PREDICTION =====
        // Multi-factor model considering frequency, field size, race progress, and incident rate
        bool yellowExpected = false;
        int lapsUntilYellow = 0;
        float yellowProbability = 0f;

        if (_lapHistory.Count > 10)
        {
            // FACTOR 1: Historical Frequency (base probability)
            float frequencyProbability = 0f;
            float yellowFrequency = 0f;
            int lapsSinceLastYellow = 0;

            if (CurrentData.YellowFlagLapCount > 0)
            {
                int totalLaps = _lapHistory.Count;
                yellowFrequency = (float)totalLaps / CurrentData.YellowFlagLapCount;

                var lastYellowLap = _lapHistory.LastOrDefault(l => l.IsYellowFlagLap);
                lapsSinceLastYellow = lastYellowLap != null ? (currentLap - lastYellowLap.LapNumber) : totalLaps;

                // Frequency-based probability (0-1)
                frequencyProbability = Math.Min(1.0f, lapsSinceLastYellow / yellowFrequency);
            }

            // FACTOR 2: Field Size (more cars = higher incident probability)
            // Large fields (>30 cars): +20% probability boost
            // Medium fields (15-30): baseline
            // Small fields (<15): -20% probability reduction
            float fieldSizeModifier = 1.0f;
            if (totalCars > 30)
                fieldSizeModifier = 1.2f;  // +20% for large fields
            else if (totalCars > 15)
                fieldSizeModifier = 1.0f;  // Baseline for medium fields
            else if (totalCars > 0)
                fieldSizeModifier = 0.8f;  // -20% for small fields

            // FACTOR 3: Race Progress (more yellows early in race)
            // First 25% of laps: +30% probability (start chaos)
            // 25-50%: +10% (still settling)
            // 50-75%: baseline
            // Last 25%: +20% (desperation moves)
            float progressModifier = 1.0f;
            if (raceLapsRemaining > 0 && telemetry.SessionLaps > 0)
            {
                float raceProgress = (float)(telemetry.LapsCompleted) / telemetry.SessionLaps;
                if (raceProgress < 0.25f)
                    progressModifier = 1.3f;  // Early race chaos
                else if (raceProgress < 0.5f)
                    progressModifier = 1.1f;  // Still settling
                else if (raceProgress > 0.75f)
                    progressModifier = 1.2f;  // Late-race desperation
                else
                    progressModifier = 1.0f;  // Mid-race baseline
            }

            // FACTOR 4: Recent Incident Rate (last 5 laps)
            // High incident rate = higher yellow probability
            var last5Laps = _lapHistory.TakeLast(5).ToList();
            int recentIncidents = last5Laps.Sum(l => l.IncidentsDuringLap);
            float incidentModifier = 1.0f;
            if (recentIncidents >= 3)
                incidentModifier = 1.5f;  // +50% if 3+ incidents in last 5 laps
            else if (recentIncidents >= 1)
                incidentModifier = 1.2f;  // +20% if any incidents

            // COMBINED PROBABILITY: Base * Modifiers
            yellowProbability = frequencyProbability * fieldSizeModifier * progressModifier * incidentModifier;
            yellowProbability = Math.Clamp(yellowProbability, 0f, 1.0f); // Cap at 100%

            // Predict yellow if combined probability exceeds threshold
            yellowExpected = yellowProbability > 0.7f;  // Lower threshold (70%) due to multi-factor confidence

            if (yellowExpected && yellowFrequency > 0)
            {
                lapsUntilYellow = (int)Math.Ceiling(yellowFrequency - lapsSinceLastYellow);
            }

            LogDebug($"YELLOW FLAG PREDICTION: Freq={yellowFrequency:F1} laps, Since last={lapsSinceLastYellow}, " +
                     $"Field={totalCars} cars ({fieldSizeModifier:F2}x), Progress={progressModifier:F2}x, " +
                     $"Incidents={recentIncidents} ({incidentModifier:F2}x) → {yellowProbability*100:F0}% ({(yellowExpected ? "EXPECTED" : "unlikely")})");
        }

        CurrentData.YellowFlagExpected = yellowExpected;
        CurrentData.LapsUntilYellow = lapsUntilYellow;
        CurrentData.YellowFlagProbability = yellowProbability;
        
        // ===== SECTION 4: PIT WINDOW CALCULATION =====
        // Calculate earliest/optimal/latest pit laps based on fuel needs and race length
        
        // Latest: Just before running out (with 1 lap safety buffer)
        // This is the CONSTRAINT - must pit before fuel runs out
        CurrentData.LatestPitLap = currentLap + Math.Max(1, (int)Math.Floor(lapsOnCurrentFuel) - 1);
        
        // Earliest: When we've used enough fuel to add race-ending fuel (with buffer)
        // CRITICAL FIX: Add sputtering threshold to match CalculateStrategy() calculation (line 803)
        // Must account for unusable fuel below sputtering threshold
        float sputteringThreshold = CurrentData.FuelSputteringThreshold;
        float fuelNeededToFinish = (raceLapsRemaining + CurrentData.FuelBufferLaps) * avgFuel + sputteringThreshold;
        // FIX: Account for sputtering threshold - can't use fuel below threshold even with full tank
        float fuelToUse = (CurrentData.TankCapacity - sputteringThreshold) - fuelNeededToFinish;
        int lapsToUseExcessFuel = fuelToUse > 0 ? (int)Math.Floor(fuelToUse / avgFuel) : 0;
        int calculatedEarliestPitLap = currentLap + Math.Max(1, lapsToUseExcessFuel);
        
        // CRITICAL FIX: EarliestPitLap CANNOT exceed LatestPitLap (fuel constraint)
        // If tank is large but current fuel is low, earliest must respect fuel reality
        CurrentData.EarliestPitLap = Math.Min(calculatedEarliestPitLap, CurrentData.LatestPitLap);
        
        // Optimal pit window: 3-5 lap green zone in middle of earliest/latest
        int midPoint = (CurrentData.EarliestPitLap + CurrentData.LatestPitLap) / 2;
        CurrentData.PitWindowStart = Math.Max(CurrentData.EarliestPitLap, midPoint - 2);
        CurrentData.PitWindowEnd = Math.Min(CurrentData.LatestPitLap, midPoint + 2);

        // FIX: Validate pit window isn't inverted (PitWindowStart > PitWindowEnd)
        if (CurrentData.PitWindowStart > CurrentData.PitWindowEnd)
        {
            LogDebug($"⚠️ PIT WINDOW INVERTED: Start L{CurrentData.PitWindowStart} > End L{CurrentData.PitWindowEnd} - swapping");
            (CurrentData.PitWindowStart, CurrentData.PitWindowEnd) = (CurrentData.PitWindowEnd, CurrentData.PitWindowStart);
        }

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
        
        // Fuel weight penalty: More fuel = slower lap times (car-class-specific penalty)
        float fuelWeightPenalty = FuelWeightDatabase.GetFuelWeightPenalty(CurrentData.CarClassId);
        float currentFuelWeight = CurrentData.CurrentFuel * fuelWeightPenalty; // seconds per lap
        float optimalFuelWeight = (CurrentData.CurrentFuel - (lapsUntilOptimalPit * avgFuel)) * fuelWeightPenalty;
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
        
        // Build gap description (abbreviated): "+4s #14 | -10s #9"
        var gapParts = new List<string>();
        
        if (carAheadIdx >= 0)
        {
            float gapToCarAhead = playerProjectedTime - telemetry.CarIdxF2Time[carAheadIdx];
            string carNum = telemetry.CarIdxToCarNumber.TryGetValue(carAheadIdx, out var num) ? num : $"{carAheadIdx}";
            gapParts.Add($"+{gapToCarAhead:F0}s #{carNum}");
        }
        
        if (carBehindIdx >= 0)
        {
            float gapFromCarBehind = telemetry.CarIdxF2Time[carBehindIdx] - playerProjectedTime;
            string carNum = telemetry.CarIdxToCarNumber.TryGetValue(carBehindIdx, out var num) ? num : $"{carBehindIdx}";
            
            if (gapParts.Count > 0)
                gapParts.Add($"-{gapFromCarBehind:F0}s #{carNum}");
            else
                gapParts.Add($"+{gapFromCarBehind:F0}s #{carNum}");
        }
        
        if (gapParts.Count > 0)
        {
            CurrentData.PitExitGapDescription = string.Join(" | ", gapParts);
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
        // Use car-class-specific penalty from database
        float fuelWeightPenalty = FuelWeightDatabase.GetFuelWeightPenalty(CurrentData.CarClassId);

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
