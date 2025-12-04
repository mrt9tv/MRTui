namespace iRacingOverlay.Core.Models;

/// <summary>
/// Comprehensive fuel calculation data for race strategy and monitoring
/// </summary>
public class FuelData
{
    // ===== CURRENT STATE =====
    
    /// <summary>Current fuel level in liters</summary>
    public float CurrentFuel { get; set; }
    
    /// <summary>Current fuel percentage (0-1)</summary>
    public float FuelPct { get; set; }
    
    /// <summary>Maximum tank capacity in liters</summary>
    public float TankCapacity { get; set; }
    
    /// <summary>Current fuel pressure (bar)</summary>
    public float FuelPressure { get; set; }
    
    /// <summary>Fuel pressure warning threshold exceeded</summary>
    public bool FuelPressureWarning { get; set; }
    
    // ===== LAP CONSUMPTION =====
    
    /// <summary>Fuel used on current lap (in progress)</summary>
    public float FuelUsedThisLap { get; set; }
    
    /// <summary>Fuel used on last completed lap</summary>
    public float FuelUsedLastLap { get; set; }
    
    /// <summary>Fuel consumption rate this lap (L/lap, real-time estimate)</summary>
    public float CurrentLapFuelRate { get; set; }
    
    // ===== AVERAGES =====
    
    /// <summary>Average fuel per lap - Last completed lap</summary>
    public float AvgFuelPerLap_Last { get; set; }
    
    /// <summary>Average fuel per lap - Last 5 laps</summary>
    public float AvgFuelPerLap_L5 { get; set; }
    
    /// <summary>Average fuel per lap - Last 10 laps</summary>
    public float AvgFuelPerLap_L10 { get; set; }
    
    /// <summary>Average fuel per lap - All laps in session</summary>
    public float AvgFuelPerLap_Session { get; set; }
    
    /// <summary>Exponential Moving Average (EMA) - smoother transitions, responsive to trends</summary>
    public float AvgFuelPerLap_EMA { get; set; }
    
    /// <summary>Green flag only average - excludes yellow/caution laps for race pace</summary>
    public float AvgFuelPerLap_GreenOnly { get; set; }
    
    /// <summary>Stint average - fuel consumption since last pit stop</summary>
    public float AvgFuelPerLap_Stint { get; set; }
    
    /// <summary>Adaptive weighted average - adjusts weighting based on fuel consistency</summary>
    public float AvgFuelPerLap_Adaptive { get; set; }
    
    /// <summary>Minimum fuel per lap (best efficiency achieved)</summary>
    public float MinFuelPerLap { get; set; }
    
    /// <summary>Maximum fuel per lap (worst efficiency)</summary>
    public float MaxFuelPerLap { get; set; }
    
    /// <summary>Laps completed in current stint (since last pit stop)</summary>
    public int StintLapCount { get; set; }
    
    /// <summary>Average fuel per lap during pace/parade laps (separate tracking)</summary>
    public float AvgFuelPerLap_PaceLaps { get; set; }
    
    /// <summary>Number of pace laps recorded (for pace lap average validity)</summary>
    public int PaceLapCount { get; set; }
    
    /// <summary>Current session state (0=Invalid, 1=GetInCar, 2=Warmup, 3=ParadeLaps, 4=Racing, 5=Checkered, 6=CoolDown)</summary>
    public int SessionState { get; set; }
    
    /// <summary>Is currently in pace lap mode (SessionState == 3)</summary>
    public bool IsPacingMode => SessionState == 3;
    
    /// <summary>Selected averaging method for calculations (default: L5)</summary>
    public FuelAveragingMethod SelectedMethod { get; set; } = FuelAveragingMethod.Last5;
    
    /// <summary>Average fuel per lap using selected method</summary>
    public float AvgFuelPerLap => SelectedMethod switch
    {
        FuelAveragingMethod.Current => CurrentLapFuelRate,
        FuelAveragingMethod.Last => AvgFuelPerLap_Last,
        FuelAveragingMethod.Last5 => AvgFuelPerLap_L5,
        FuelAveragingMethod.Last10 => AvgFuelPerLap_L10,
        FuelAveragingMethod.Session => AvgFuelPerLap_Session,
        FuelAveragingMethod.Max => MaxFuelPerLap,
        FuelAveragingMethod.EMA => AvgFuelPerLap_EMA,
        FuelAveragingMethod.GreenFlagOnly => AvgFuelPerLap_GreenOnly,
        FuelAveragingMethod.StintAverage => AvgFuelPerLap_Stint,
        FuelAveragingMethod.Adaptive => AvgFuelPerLap_Adaptive,
        _ => AvgFuelPerLap_L5
    };
    
    // ===== LAPS REMAINING =====
    
    /// <summary>Calculated laps remaining based on selected averaging method</summary>
    public float LapsRemaining { get; set; }
    
    /// <summary>iRacing's estimated laps remaining (from SDK)</summary>
    public float IRacingLapsRemaining { get; set; }
    
    /// <summary>Difference between our calculation and iRacing's (positive = we have more fuel)</summary>
    public float LapsDifference { get; set; }
    
    // ===== DELTA TRACKING & CONFIDENCE (PHASE 3: INTELLIGENT DELTA DISPLAY) =====
    
    /// <summary>Delta convergence trend: "Narrowing", "Widening", "Stable"</summary>
    public string DeltaConvergenceTrend { get; set; } = "Unknown";
    
    /// <summary>Delta convergence rate (laps/lap) - positive = narrowing, negative = widening</summary>
    public float DeltaConvergenceRate { get; set; }
    
    /// <summary>Delta confidence level: "High", "Medium", "Low"</summary>
    public string DeltaConfidence { get; set; } = "Unknown";
    
    /// <summary>Confidence score (0-100) based on delta stability and consistency</summary>
    public float DeltaConfidenceScore { get; set; }
    
    /// <summary>Explanation of why our calculation differs from iRacing</summary>
    public string DeltaExplanation { get; set; } = "iRacing uses current lap fuel rate, we use weighted average";
    
    /// <summary>Is the delta suspicious (>5 laps difference)?</summary>
    public bool DeltaSuspicious { get; set; }
    
    /// <summary>Reason for suspicious delta flag</summary>
    public string? DeltaSuspiciousReason { get; set; }
    
    /// <summary>Historical accuracy: How often our prediction was closer than iRacing's (0-1)</summary>
    public float OurMethodAccuracy { get; set; }
    
    /// <summary>Historical accuracy: How often iRacing's prediction was closer (0-1)</summary>
    public float IRacingMethodAccuracy { get; set; }
    
    /// <summary>Total predictions made for accuracy tracking</summary>
    public int TotalPredictions { get; set; }
    
    /// <summary>Recommended method based on historical accuracy: "UseOurs", "UseIRacing", "Uncertain"</summary>
    public string RecommendedMethod { get; set; } = "UseOurs";
    
    /// <summary>Should we suggest switching to "Current" method? (if iRacing more accurate)</summary>
    public bool SuggestCurrentMethod { get; set; }
    
    // ===== RACE STRATEGY =====
    
    /// <summary>Total laps remaining in race/session</summary>
    public int RaceLapsRemaining { get; set; }
    
    /// <summary>Session time remaining in seconds (for time-based sessions)</summary>
    public double SessionTimeRemaining { get; set; }
    
    /// <summary>Estimated laps remaining based on time (for time-based sessions)</summary>
    public float EstimatedLapsFromTime { get; set; }
    
    /// <summary>Is this a time-based session (vs lap-based)?</summary>
    public bool IsTimedSession { get; set; }
    
    /// <summary>Average lap time in seconds (used for time-based calculations)</summary>
    public float AverageLapTime { get; set; }
    
    /// <summary>Fuel needed to finish race (with current average)</summary>
    public float FuelNeededToFinish { get; set; }
    
    /// <summary>Fuel delta: Current - Needed (positive = surplus, negative = shortage)</summary>
    public float FuelDeltaToFinish { get; set; }
    
    /// <summary>Can finish race without refueling</summary>
    public bool CanFinishWithoutStop { get; set; }
    
    /// <summary>Fuel to add at next pit stop (0 if can finish)</summary>
    public float FuelToAddAtPit { get; set; }
    
    /// <summary>Safety buffer for fuel calculations (dynamic or user-configured)</summary>
    public float FuelBufferLaps { get; set; } = 1.0f;
    
    /// <summary>Fuel consumption consistency variance (standard deviation in L/lap)</summary>
    public float FuelConsistencyVariance { get; set; }
    
    /// <summary>Yellow flag probability (0-1, based on historical session data)</summary>
    public float YellowFlagProbability { get; set; }
    
    /// <summary>Explanation for current buffer laps value (debugging/transparency)</summary>
    public string BufferLapReason { get; set; } = "User configured";
    
    // ===== LAP COMPLETION CONTEXT (ENHANCED PHASE 3) =====
    
    /// <summary>Leader's laps completed (P1 in race)</summary>
    public int LeaderLapsCompleted { get; set; }
    
    /// <summary>How many laps behind the leader (-1 = lapped once, 0 = on lead lap, +1 = ahead by 1 lap)</summary>
    public int LapsBehindLeader { get; set; }
    
    /// <summary>Is player currently being lapped (lap down)</summary>
    public bool IsBeingLapped => LapsBehindLeader < 0;
    
    /// <summary>Track position percentage (0-1, where player is on track)</summary>
    public float TrackPositionPct { get; set; }
    
    /// <summary>Current lap time in seconds (lap in progress)</summary>
    public float CurrentLapTime { get; set; }
    
    /// <summary>Projected lap completion time based on current pace (seconds)</summary>
    public float ProjectedLapTime { get; set; }
    
    /// <summary>Is current lap extending beyond typical lap time (running longer than average)</summary>
    public bool IsExtendingLap { get; set; }
    
    // ===== SPUTTERING THRESHOLD (ENHANCED) =====
    
    /// <summary>Dynamic sputtering threshold in liters (car-specific, default 0.3L)</summary>
    /// <remarks>
    /// Threshold at which fuel pressure drops and engine begins to sputter.
    /// Varies by car: GT3 ~0.3L, Formula ~0.2L, Heavy cars ~0.5L
    /// </remarks>
    public float FuelSputteringThreshold { get; set; } = 0.3f;
    
    /// <summary>Baseline fuel pressure for this car (bar) - established from early laps</summary>
    public float BaselineFuelPressure { get; set; }
    
    /// <summary>Fuel pressure drop percentage from baseline (0-100%)</summary>
    public float FuelPressureDropPct { get; set; }
    
    /// <summary>Fuel pressure warning active (pressure dropped >10% from baseline)</summary>
    public bool FuelPressureLow { get; set; }
    
    /// <summary>Car ID/class for sputtering threshold lookup</summary>
    public int CarClassId { get; set; }
    
    // ===== LAP-TO-LAP COMPARISON =====
    
    /// <summary>Difference between last lap and previous lap (positive = used more)</summary>
    public float LapToLapDelta { get; set; }
    
    /// <summary>Trend indicator: ↑ using more, ↓ using less, → consistent</summary>
    public string LapToLapTrend => LapToLapDelta switch
    {
        > 0.1f => "↑",
        < -0.1f => "↓",
        _ => "→"
    };
    
    // ===== SAFETY CAR / YELLOW FLAG =====
    
    /// <summary>Currently under yellow/caution flag</summary>
    public bool IsUnderYellow { get; set; }
    
    /// <summary>Average fuel per lap under yellow flags</summary>
    public float YellowFlagAverage { get; set; }
    
    /// <summary>Number of yellow flag laps completed</summary>
    public int YellowFlagLapCount { get; set; }
    
    /// <summary>Average fuel per lap under green flags only</summary>
    public float GreenFlagAverage { get; set; }
    
    /// <summary>Number of green flag laps completed</summary>
    public int GreenFlagLapCount { get; set; }
    
    // ===== SESSION TRACKING =====
    
    /// <summary>Current lap number</summary>
    public int CurrentLap { get; set; }
    
    /// <summary>Total laps in session (0 for time-based sessions)</summary>
    public int SessionLaps { get; set; }
    
    /// <summary>Total number of laps completed</summary>
    public int LapsCompleted { get; set; }
    
    /// <summary>Total fuel used in session</summary>
    public float TotalFuelUsed { get; set; }
    
    /// <summary>Fuel level at session start</summary>
    public float StartingFuel { get; set; }
    
    /// <summary>Last refuel amount detected</summary>
    public float LastRefuelAmount { get; set; }
    
    /// <summary>Number of pit stops / refuels</summary>
    public int RefuelCount { get; set; }
    
    // ===== DATA VALIDITY =====
    
    /// <summary>Sufficient lap data available for reliable calculations (at least 2 laps)</summary>
    public bool HasSufficientData { get; set; }
    
    /// <summary>Warning message for user (if any)</summary>
    public string? WarningMessage { get; set; }
    
    /// <summary>Last update timestamp</summary>
    public DateTime LastUpdate { get; set; }

    /// <summary>
    /// Indicates if this update is a lightweight "live" update (mid-lap projection)
    /// vs a full strategic calculation (lap completion).
    /// Live updates: Projected values based on current lap progress
    /// Strategic updates: Full recalculation with complete lap history
    /// </summary>
    public bool IsLiveUpdate { get; set; }

    // ===== PHASE 3: FUEL SAVING MODE =====
    
    /// <summary>Target fuel reduction per lap to finish without additional pit stop (L/lap)</summary>
    public float FuelSavingTarget { get; set; }
    
    /// <summary>Current fuel saving rate (L/lap) - how much less than average we're using</summary>
    public float CurrentSavingRate { get; set; }
    
    /// <summary>Target lap time needed for fuel conservation (seconds)</summary>
    public float TargetLapTime { get; set; }
    
    /// <summary>Suggested lift points for fuel saving (comma-separated turn numbers)</summary>
    public string? LiftPoints { get; set; }
    
    /// <summary>Fuel saving progress percentage (0-100)</summary>
    public float SavingProgress { get; set; }
    
    /// <summary>Is fuel saving mode currently needed?</summary>
    public bool NeedsFuelSaving { get; set; }
    
    /// <summary>Is fuel saving mode currently working (on track to finish)?</summary>
    public bool FuelSavingWorking { get; set; }
    
    /// <summary>Fuel surplus/deficit if current saving rate continues (liters)</summary>
    public float ProjectedFuelDelta { get; set; }
    
    // ===== PIT STRATEGY VS FUEL SAVING COMPARISON =====
    
    /// <summary>Estimated pit stop time including entry, service, exit (seconds)</summary>
    public float EstimatedPitStopTime { get; set; }
    
    /// <summary>Total time loss if pitting for fuel (seconds)</summary>
    public float PitStopTimeLoss { get; set; }
    
    /// <summary>Total time loss if saving fuel to finish without pitting (seconds)</summary>
    public float FuelSavingTimeLoss { get; set; }
    
    /// <summary>Is pitting faster than fuel saving? (true = pit recommended, false = save fuel)</summary>
    public bool IsPittingFaster { get; set; }
    
    /// <summary>Time difference between strategies (positive = fuel saving slower)</summary>
    public float StrategyTimeDelta { get; set; }
    
    /// <summary>Is fuel saving physically possible with remaining laps?</summary>
    public bool CanSaveFuelToFinish { get; set; }
    
    /// <summary>Optimal pit lap to minimize time loss (based on track position)</summary>
    public int OptimalPitLap { get; set; }
    
    /// <summary>Reason for optimal pit lap recommendation</summary>
    public string? OptimalPitReason { get; set; }
    
    /// <summary>Strategic alert message (PIT THIS LAP, FUEL SAVING WORKING, etc.)</summary>
    public string? StrategicAlert { get; set; }
    
    /// <summary>Alert severity level (0=None, 1=Info, 2=Warning, 3=Critical)</summary>
    public int AlertSeverity { get; set; }
    
    /// <summary>Historical fuel usage context (comparison to past sessions)</summary>
    public string? HistoricalContext { get; set; }
    
    // ===== PHASE 7: REAL-TIME LAP DELTA TRACKING =====
    
    /// <summary>Live delta to target pace (positive = faster, negative = slower)</summary>
    public float LiveDeltaToTarget { get; set; }
    
    /// <summary>Predicted final lap time based on current pace</summary>
    public float PredictedLapTime { get; set; }
    
    /// <summary>Predicted delta vs target (positive = will be faster, negative = will be slower)</summary>
    public float PredictedDelta { get; set; }
    
    /// <summary>Current lap progress (0.0 to 1.0)</summary>
    public float LapProgress { get; set; }
    
    /// <summary>Is live delta valid and displayable</summary>
    public bool LiveDeltaValid { get; set; }
    
    // ===== PHASE 8: TIRE STRATEGY INTEGRATION =====
    
    /// <summary>Tire life remaining (0.0 to 1.0, 1.0 = new tires)</summary>
    public float TireLifeRemaining { get; set; }
    
    /// <summary>Laps remaining on current tires before change needed</summary>
    public int TireLapsRemaining { get; set; }
    
    /// <summary>Recommended lap to pit for tires</summary>
    public int TirePitLap { get; set; }
    
    /// <summary>Maximum tire wear rate (percent per lap)</summary>
    public float MaxTireWearRate { get; set; }
    
    /// <summary>Combined fuel+tire pit lap (optimal for both)</summary>
    public int CombinedPitLap { get; set; }
    
    /// <summary>Whether combined fuel+tire stop is recommended</summary>
    public bool CombinedStopRecommended { get; set; }
    
    // ===== PHASE 9: HISTORICAL LEARNING =====
    
    /// <summary>Are we using historical predictions for this session?</summary>
    public bool UsingHistoricalPredictions { get; set; }
    
    /// <summary>Confidence in historical prediction (0-100)</summary>
    public float HistoricalConfidence { get; set; }
    
    /// <summary>Number of past sessions used for prediction</summary>
    public int HistoricalSessionCount { get; set; }
    
    // ===== PHASE 5: ENHANCED PIT STRATEGY OPTIMIZATION =====
    
    /// <summary>Earliest possible pit lap before fuel becomes critical (minimum safe lap)</summary>
    public int EarliestPitLap { get; set; }
    
    /// <summary>Latest recommended pit lap without significant risk (maximum delay)</summary>
    public int LatestPitLap { get; set; }
    
    /// <summary>Ideal pit window start (recommended earliest pit)</summary>
    public int PitWindowStart { get; set; }
    
    /// <summary>Ideal pit window end (recommended latest pit)</summary>
    public int PitWindowEnd { get; set; }
    
    /// <summary>Pit window reason/explanation (e.g., "Optimal for track position", "Yellow flag expected")</summary>
    public string? PitWindowReason { get; set; }
    
    /// <summary>Current race position (1 = P1, 2 = P2, etc.)</summary>
    public int RacePosition { get; set; }
    
    /// <summary>Total cars in race</summary>
    public int TotalCars { get; set; }
    
    /// <summary>Track position cost - seconds lost per position (higher = more costly to pit)</summary>
    public float TrackPositionCost { get; set; }
    
    /// <summary>Yellow flag expected in next N laps (based on probability)</summary>
    public bool YellowFlagExpected { get; set; }
    
    /// <summary>Laps until expected yellow (0 = imminent, -1 = not expected)</summary>
    public int LapsUntilYellow { get; set; }
    
    /// <summary>Pit delta - time difference pitting now vs N laps later (seconds, positive = later is better)</summary>
    public float PitDeltaNowVsLater { get; set; }
    
    /// <summary>Comparison laps for pit delta (e.g., 5 = comparing now vs 5 laps later)</summary>
    public int PitDeltaComparisonLaps { get; set; } = 5;
    
    /// <summary>Fuel criticality score (0-100, higher = more urgent to pit)</summary>
    public float FuelCriticalityScore { get; set; }
    
    // ===== MULTI-STOP STRATEGY =====
    
    /// <summary>1-stop strategy: Total race time estimate (seconds)</summary>
    public float OneStopTotalTime { get; set; }
    
    /// <summary>1-stop strategy: Pit lap recommendation</summary>
    public int OneStopPitLap { get; set; }
    
    /// <summary>1-stop strategy: Fuel to add at pit (liters)</summary>
    public float OneStopFuelToAdd { get; set; }
    
    /// <summary>2-stop strategy: Total race time estimate (seconds)</summary>
    public float TwoStopTotalTime { get; set; }
    
    /// <summary>2-stop strategy: First pit lap</summary>
    public int TwoStopPit1Lap { get; set; }
    
    /// <summary>2-stop strategy: Second pit lap</summary>
    public int TwoStopPit2Lap { get; set; }
    
    /// <summary>2-stop strategy: Fuel per stint (liters)</summary>
    public float TwoStopFuelPerStint { get; set; }
    
    /// <summary>3-stop strategy: Total race time estimate (seconds)</summary>
    public float ThreeStopTotalTime { get; set; }
    
    /// <summary>3-stop strategy: Pit laps (comma-separated, e.g., "10,20,30")</summary>
    public string ThreeStopPitLaps { get; set; } = "";
    
    /// <summary>3-stop strategy: Fuel per stint (liters)</summary>
    public float ThreeStopFuelPerStint { get; set; }
    
    /// <summary>Recommended strategy (1, 2, or 3 stops)</summary>
    public int RecommendedStops { get; set; }
    
    /// <summary>Recommended strategy reason/explanation</summary>
    public string? RecommendedStopsReason { get; set; }
    
    /// <summary>Time difference between best and second-best strategy (seconds)</summary>
    public float StrategyTimeDifference { get; set; }
    
    /// <summary>Is tire change required/recommended? (affects pit stop duration)</summary>
    public bool TireChangeRecommended { get; set; }
    
    /// <summary>Estimated pit stop time for fuel-only stop (seconds)</summary>
    public float FuelOnlyStopTime { get; set; }
    
    /// <summary>Estimated pit stop time for fuel + tires stop (seconds)</summary>
    public float FuelAndTiresStopTime { get; set; }
    
    /// <summary>Undercut opportunity available (pit early to gain track position)</summary>
    public bool UndercutAvailable { get; set; }
    
    /// <summary>Overcut opportunity available (pit late to gain track position)</summary>
    public bool OvercutAvailable { get; set; }
    
    /// <summary>Alternative strategy explanation (e.g., "Pit lap 15 vs lap 20: +3.2s")</summary>
    public string? AlternativeStrategyInfo { get; set; }

    // ===== PIT EXIT POSITION PREDICTION =====

    /// <summary>Projected race position after pit stop (0 if not calculated or invalid)</summary>
    public int PitExitPosition { get; set; }

    /// <summary>Gap description after pit stop (e.g., "4s ahead of #14, 10s behind #9") - class-filtered</summary>
    public string? PitExitGapDescription { get; set; }

    /// <summary>Whether pit exit position calculation is valid (requires position data, pit time, and gaps)</summary>
    public bool PitExitPositionValid { get; set; }

    /// <summary>Confidence score for pit exit prediction (0-100, higher = more reliable)</summary>
    public float PitExitConfidenceScore { get; set; }

    /// <summary>Confidence icon for display (🟢 high, 🟡 medium, 🔴 low)</summary>
    public string PitExitConfidenceIcon { get; set; } = "";

    /// <summary>List of car numbers currently on pit road (class-filtered)</summary>
    public List<string> CarsPitting { get; set; } = new();

    // ===== TEMPERATURE CORRECTION (ENHANCEMENT) =====

    /// <summary>Temperature correction factor for fuel consumption (1.0 = no correction, >1.0 = hotter = more fuel)</summary>
    public float TemperatureCorrectionFactor { get; set; } = 1.0f;

    /// <summary>Explanation of temperature correction applied (e.g., "Air temp +12°C vs historical avg (+3.0% fuel)")</summary>
    public string TemperatureCorrectionReason { get; set; } = "";

    // ===== REAL-TIME FUEL FLOW (ENHANCEMENT) =====

    /// <summary>Instantaneous fuel flow rate from iRacing SDK (liters/hour)</summary>
    public float InstantaneousFuelFlow { get; set; }

    /// <summary>Projected fuel consumption for this lap based on current flow rate and lap time (liters)</summary>
    public float ProjectedLapFuel { get; set; }

    // ===== HISTORICAL FUEL COMPARISON (ENHANCEMENT) =====

    /// <summary>Historical fuel average for this track/car from previous sessions (liters/lap)</summary>
    public float HistoricalFuelAverage { get; set; }

    /// <summary>Variance from historical fuel average (percentage, positive = using more fuel)</summary>
    public float HistoricalFuelVariance { get; set; }
}

/// <summary>
/// Fuel averaging calculation methods
/// </summary>
public enum FuelAveragingMethod
{
    /// <summary>Current lap fuel rate (real-time, volatile)</summary>
    Current,
    
    /// <summary>Last completed lap</summary>
    Last,
    
    /// <summary>Last 5 laps average (recommended default)</summary>
    Last5,
    
    /// <summary>Last 10 laps average (more stable)</summary>
    Last10,
    
    /// <summary>All laps in session average</summary>
    Session,
    
    /// <summary>Maximum fuel per lap (conservative/worst case)</summary>
    Max,
    
    /// <summary>Exponential Moving Average - smoother transitions, trend-following</summary>
    EMA,
    
    /// <summary>Green flag only average - excludes yellow/caution laps for race pace calculations</summary>
    GreenFlagOnly,
    
    /// <summary>Stint average - fuel consumption since last pit stop</summary>
    StintAverage,
    
    /// <summary>Adaptive weighted average - adjusts weighting based on fuel consistency (tight weight if consistent, loose if variable)</summary>
    Adaptive
}
