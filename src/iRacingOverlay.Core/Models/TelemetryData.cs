namespace iRacingOverlay.Core.Models;

/// <summary>
/// Represents telemetry data from iRacing simulator with dirty field tracking
/// </summary>
public class TelemetryData
{
    // ===== DIRTY FIELD TRACKING (Task 6 Optimization) =====
    // Tracks which fields changed since last update to optimize widget rendering
    // Widgets check this HashSet before expensive Dispatcher.Invoke operations
    // Result: 50%+ reduction in UI thread overhead (only update changed values)
    
    /// <summary>
    /// Which high-frequency fields changed since the previous telemetry frame.
    ///
    /// This was a HashSet&lt;string&gt; allocated and populated on every one of the
    /// 60 ticks per second, with case-insensitive string hashing on each add — and
    /// nothing ever read it. A flags bitmask costs nothing to build or test.
    /// </summary>
    public TelemetryChanges ChangedFields { get; set; }

    /// <summary>Check whether any of the given fields changed since the previous frame.</summary>
    public bool HasChanged(TelemetryChanges fields) => (ChangedFields & fields) != 0;
    
    // ===== TELEMETRY PROPERTIES =====
    
    /// <summary>
    /// Car speed in meters per second
    /// </summary>
    public float Speed { get; set; }

    /// <summary>
    /// Engine RPM
    /// </summary>
    public float RPM { get; set; }
    
    /// <summary>
    /// Car's redline RPM (from car setup/info if available)
    /// </summary>
    public float EngineRedlineRPM { get; set; }
    
    // ===== PROFESSIONAL SHIFT LIGHT TELEMETRY =====
    // iRacing SDK provides professional-grade, car-specific shift points
    // based on actual engine physics and torque curves
    
    /// <summary>
    /// RPM when shift lights START illuminating (professional grade, car-specific)
    /// </summary>
    public float PlayerCarSLFirstRPM { get; set; }
    
    /// <summary>
    /// OPTIMAL SHIFT POINT RPM (professional grade, car-specific)
    /// This is THE KEY VALUE - iRacing's calculated optimal shift point
    /// </summary>
    public float PlayerCarSLShiftRPM { get; set; }
    
    /// <summary>
    /// RPM when shift lights are FULLY LIT (professional grade, car-specific)
    /// </summary>
    public float PlayerCarSLLastRPM { get; set; }
    
    /// <summary>
    /// RPM when shift lights BLINK (over-rev warning, professional grade, car-specific)
    /// </summary>
    public float PlayerCarSLBlinkRPM { get; set; }

    /// <summary>
    /// Current gear (-1 = Reverse, 0 = Neutral, 1+ = Forward gears)
    /// </summary>
    public int Gear { get; set; }

    /// <summary>
    /// Throttle input (0.0 to 1.0)
    /// </summary>
    public float Throttle { get; set; }

    /// <summary>
    /// Brake input (0.0 to 1.0)
    /// </summary>
    public float Brake { get; set; }

    /// <summary>
    /// ABS (Anti-lock Braking System) active indicator
    /// True when ABS is actively preventing wheel lock
    /// </summary>
    public bool BrakeABSactive { get; set; }

    /// <summary>
    /// Brake bias adjustment percentage (front bias)
    /// Value represents percentage of braking force to front (e.g., 55.5 = 55.5% front)
    /// Adjustable in-car via driver controls
    /// </summary>
    public float BrakeBias { get; set; }

    /// <summary>
    /// Traction control setting/level (integer 0-20 depending on car)
    /// Value represents TC strength level (0 = OFF, 1-20 = Active levels)
    /// Only available in cars equipped with traction control
    /// Adjustable in-car via driver controls
    /// </summary>
    public int TractionControl { get; set; }

    /// <summary>
    /// Pit speed limiter active state
    /// True when pit speed limiter is engaged (typically F1 button or assigned key)
    /// Used to enforce pit lane speed restrictions
    /// </summary>
    public bool PitSpeedLimiterActive { get; set; }

    /// <summary>
    /// Time required for mandatory pit repairs in seconds
    /// Value > 0 indicates damage that MUST be repaired before continuing
    /// </summary>
    public float PitRepairLeft { get; set; }

    /// <summary>
    /// Time required for optional pit repairs in seconds
    /// Value > 0 indicates damage that CAN be repaired (but not mandatory)
    /// Used to assess damage severity for strategic decisions
    /// </summary>
    public float PitOptRepairLeft { get; set; }

    /// <summary>
    /// Player currently in pit stall (from SDK PlayerCarInPitStall)
    /// </summary>
    public bool PlayerCarInPitStall { get; set; }

    /// <summary>
    /// Tow time remaining in seconds (0 when not being towed)
    /// </summary>
    public float PlayerCarTowTime { get; set; }

    /// <summary>
    /// Pitstop service currently in progress
    /// </summary>
    public bool PitstopActive { get; set; }

    /// <summary>
    /// Pit service flags bitfield (PitServiceFlags enum)
    /// Bits: LFTireChange=1, RFTireChange=2, LRTireChange=4, RRTireChange=8,
    /// FuelFill=16, WindshieldTearoff=32, FastRepair=64
    /// </summary>
    public int PitSvFlags { get; set; }

    /// <summary>
    /// Fuel fill amount requested for pit stop (liters)
    /// </summary>
    public float PitSvFuel { get; set; }

    /// <summary>
    /// Pit service status (PitServiceStatus enum)
    /// 0=None, 1=InProgress, 2=Complete, 100=TooFarLeft, 101=TooFarRight,
    /// 102=TooFarForward, 103=TooFarBack, 104=BadAngle, 105=CantFixThat
    /// </summary>
    public int PlayerCarPitSvStatus { get; set; }

    /// <summary>
    /// Clutch input (0.0 to 1.0)
    /// </summary>
    public float Clutch { get; set; }

    /// <summary>
    /// Steering wheel angle in radians
    /// </summary>
    public float SteeringWheelAngle { get; set; }

    /// <summary>
    /// Current lap number
    /// </summary>
    public int Lap { get; set; }

    /// <summary>
    /// Distance around lap (0.0 to 1.0)
    /// </summary>
    public float LapDistPct { get; set; }

    /// <summary>
    /// Distance in meters from start/finish line (absolute track distance)
    /// </summary>
    public float LapDist { get; set; }

    /// <summary>
    /// Current position in race (SDK position - updates at start/finish line)
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Player's overall race position (from SDK PlayerCarPosition - updates at S/F line)
    /// Different from PlayerCarClassPosition (Position) - this is overall across all classes
    /// </summary>
    public int PlayerCarPosition { get; set; }

    /// <summary>
    /// Live overall position (updates every tick, mode-aware, frozen on checkered flag)
    /// Calculated based on session type: Race=lap count, Qualifying=best time
    /// </summary>
    public int LivePosition { get; set; }

    /// <summary>
    /// Live class position (updates every tick, mode-aware, frozen on checkered flag)
    /// Falls back to SDK PlayerCarClassPosition if calculation unavailable
    /// </summary>
    public int LiveClassPosition { get; set; }

    /// <summary>
    /// Session state enum (0=Invalid, 1=GetInCar, 2=Warmup, 3=ParadeLaps, 4=Racing, 5=Checkered, 6=CoolDown)
    /// Used to detect checkered flag and freeze final positions
    /// </summary>
    public int SessionState { get; set; }

    /// <summary>
    /// Session type from YAML ("Practice", "Qualifying", "Warmup", "Race")
    /// Used to determine position calculation mode
    /// </summary>
    public string SessionType { get; set; } = string.Empty;

    /// <summary>
    /// Player's car index in the session (0-63)
    /// Used to identify player in CarIdx arrays
    /// </summary>
    public int PlayerCarIdx { get; set; }
    
    /// <summary>
    /// Player's car class ID
    /// Used for class filtering in proximity detection
    /// </summary>
    public int PlayerCarClass { get; set; }

    /// <summary>
    /// Timestamp when data was received
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // MVP 2 - Critical race data
    /// <summary>
    /// Fuel level in liters
    /// </summary>
    public float FuelLevel { get; set; }

    /// <summary>
    /// Fuel level percentage (0.0 to 1.0)
    /// </summary>
    public float FuelLevelPct { get; set; }

    /// <summary>
    /// Fuel consumption rate in kg/hr
    /// </summary>
    public float FuelUsePerHour { get; set; }

    /// <summary>
    /// Fuel line pressure in bar
    /// </summary>
    public float FuelPress { get; set; }
    
    /// <summary>
    /// Fuel tank capacity in liters (car maximum)
    /// </summary>
    public float FuelLevelMax { get; set; }
    
    /// <summary>
    /// Laps remaining based on fuel (iRacing's calculation)
    /// </summary>
    public float SessionLapsRemainEx { get; set; }
    
    /// <summary>
    /// Number of laps completed by player
    /// </summary>
    public int LapsCompleted { get; set; }
    
    /// <summary>
    /// Total laps in race/session (0 for time-based sessions)
    /// DEPRECATED: Use SessionLapsTotal instead (more reliable from SDK)
    /// </summary>
    public int SessionLaps { get; set; }

    /// <summary>
    /// DIRECT FROM SDK: Total laps in the race (replaces buggy SessionLaps)
    /// More reliable than SessionLaps which sometimes returns 0
    /// </summary>
    public int SessionLapsTotal { get; set; }

    /// <summary>
    /// DIRECT FROM SDK: Laps remaining in the race
    /// No calculation needed - SDK provides this value directly!
    /// </summary>
    public int SessionLapsRemain { get; set; }

    /// <summary>
    /// Estimated total race laps (from FuelCalculatorService).
    /// For lap-based races: equals SessionLapsTotal.
    /// For timed races: leader's lap + estimated remaining laps from time.
    /// Propagated from FuelData after fuel calculator update.
    /// </summary>
    public int EstimatedTotalRaceLaps { get; set; }

    /// <summary>
    /// DIRECT FROM SDK: Total session time in seconds
    /// Used with SessionTimeRemain for timed session calculations
    /// </summary>
    public float SessionTimeTotal { get; set; }

    /// <summary>
    /// ACTUAL leading lap in the race (highest lap number any car is currently on)
    /// This is THE CRITICAL VALUE for race end calculations.
    /// Different from race leader's lap (P1 by position) when leader is lapped.
    /// Example: In a 20-lap race, if P1 is on lap 18 but P2 is on lap 19 (unlapping), this is 19.
    /// </summary>
    public int ActualLeadingLapNumber { get; set; }

    /// <summary>
    /// Race leader's current lap number (P1 by position, not necessarily highest lap)
    /// May be lower than ActualLeadingLapNumber if race leader is lapped.
    /// Used for position-based strategy (what lap is the winner on?).
    /// </summary>
    public int RaceLeaderLapNumber { get; set; }

    /// <summary>
    /// Water temperature in Celsius
    /// </summary>
    public float WaterTemp { get; set; }

    /// <summary>
    /// Coolant level in liters
    /// </summary>
    public float WaterLevel { get; set; }

    /// <summary>
    /// Oil temperature in Celsius
    /// </summary>
    public float OilTemp { get; set; }

    /// <summary>
    /// Oil level in liters
    /// </summary>
    public float OilLevel { get; set; }

    /// <summary>
    /// Oil pressure in bar
    /// </summary>
    public float OilPress { get; set; }

    /// <summary>
    /// Last lap time in seconds
    /// </summary>
    public float LapLastLapTime { get; set; }

    /// <summary>
    /// Best lap time in seconds
    /// </summary>
    public float LapBestLapTime { get; set; }
    
    /// <summary>
    /// Current lap time in seconds (from SDK)
    /// </summary>
    public float LapCurrentLapTime { get; set; }
    
    /// <summary>
    /// Delta to personal best lap in seconds (from SDK)
    /// </summary>
    public float LapDeltaToBestLap { get; set; }
    
    /// <summary>
    /// Delta-delta (rate of change) to personal best lap in seconds (from SDK)
    /// </summary>
    public float LapDeltaToBestLap_DD { get; set; }
    
    /// <summary>
    /// Delta to session best lap in seconds (from SDK)
    /// </summary>
    public float LapDeltaToSessionBestLap { get; set; }
    
    /// <summary>
    /// Current lap time in seconds (calculated locally - kept for backward compatibility)
    /// </summary>
    public float CurrentLapTime { get; set; }
    
    /// <summary>
    /// Delta to personal best lap (calculated locally - kept for backward compatibility)
    /// </summary>
    public float DeltaToBestLap { get; set; }
    
    /// <summary>
    /// Delta to session best lap (calculated locally - kept for backward compatibility)
    /// </summary>
    public float DeltaToSessionBest { get; set; }

    /// <summary>
    /// Session time remaining in seconds
    /// </summary>
    public double SessionTimeRemain { get; set; }
    
    /// <summary>
    /// Session time elapsed in seconds
    /// </summary>
    public double SessionTime { get; set; }
    
    /// <summary>
    /// Current session number
    /// </summary>
    public int SessionNum { get; set; }

    // MVP 3+ - Advanced telemetry
    public float LFtempCL { get; set; }
    public float LFtempCM { get; set; }
    public float LFtempCR { get; set; }
    public float RFtempCL { get; set; }
    public float RFtempCM { get; set; }
    public float RFtempCR { get; set; }
    public float LRtempCL { get; set; }
    public float LRtempCM { get; set; }
    public float LRtempCR { get; set; }
    public float RRtempCL { get; set; }
    public float RRtempCM { get; set; }
    public float RRtempCR { get; set; }

    public float LFwearL { get; set; }
    public float LFwearM { get; set; }
    public float LFwearR { get; set; }
    public float RFwearL { get; set; }
    public float RFwearM { get; set; }
    public float RFwearR { get; set; }
    public float LRwearL { get; set; }
    public float LRwearM { get; set; }
    public float LRwearR { get; set; }
    public float RRwearL { get; set; }
    public float RRwearM { get; set; }
    public float RRwearR { get; set; }

    public float LongAccel { get; set; }
    public float LatAccel { get; set; }
    public float VertAccel { get; set; }

    public uint SessionFlags { get; set; }
    public int PlayerCarMyIncidentCount { get; set; }
    
    /// <summary>
    /// Pace mode enumeration (0=SingleFileStart, 1=DoubleFileStart, 2=SingleFileRestart, 3=DoubleFileRestart, 4=NotPacing)
    /// Indicates restart formation type - useful for restart strategy
    /// </summary>
    public int PaceMode { get; set; }

    public float LFbrakeLinePress { get; set; }
    public float RFbrakeLinePress { get; set; }
    public float LRbrakeLinePress { get; set; }
    public float RRbrakeLinePress { get; set; }

    // Tire Rumble (Force Feedback indicators - CRITICAL for lockup detection!)
    /// <summary>
    /// Left Front tire rumble pitch - spikes during wheel slip/lockup
    /// </summary>
    public float TireLF_RumblePitch { get; set; }
    
    /// <summary>
    /// Right Front tire rumble pitch - spikes during wheel slip/lockup
    /// </summary>
    public float TireRF_RumblePitch { get; set; }
    
    /// <summary>
    /// Left Rear tire rumble pitch - spikes during wheel slip/lockup
    /// </summary>
    public float TireLR_RumblePitch { get; set; }
    
    /// <summary>
    /// Right Rear tire rumble pitch - spikes during wheel slip/lockup
    /// </summary>
    public float TireRR_RumblePitch { get; set; }

    // Wheel Odometers (for calculating individual wheel speeds!)
    /// <summary>
    /// Left Front wheel odometer (meters) - distance traveled by LF wheel
    /// </summary>
    public float LFodometer { get; set; }
    
    /// <summary>
    /// Right Front wheel odometer (meters) - distance traveled by RF wheel
    /// </summary>
    public float RFodometer { get; set; }
    
    /// <summary>
    /// Left Rear wheel odometer (meters) - distance traveled by LR wheel
    /// </summary>
    public float LRodometer { get; set; }
    
    /// <summary>
    /// Right Rear wheel odometer (meters) - distance traveled by RR wheel
    /// </summary>
    public float RRodometer { get; set; }

    // Shock/Suspension Data (for wheel load analysis)
    /// <summary>
    /// Left Front shock deflection (meters) - suspension travel
    /// </summary>
    public float LFshockDefl { get; set; }
    
    /// <summary>
    /// Right Front shock deflection (meters)
    /// </summary>
    public float RFshockDefl { get; set; }
    
    /// <summary>
    /// Left Rear shock deflection (meters)
    /// </summary>
    public float LRshockDefl { get; set; }
    
    /// <summary>
    /// Right Rear shock deflection (meters)
    /// </summary>
    public float RRshockDefl { get; set; }
    
    /// <summary>
    /// Left Front shock velocity (m/s) - rate of suspension compression/extension
    /// </summary>
    public float LFshockVel { get; set; }
    
    /// <summary>
    /// Right Front shock velocity (m/s)
    /// </summary>
    public float RFshockVel { get; set; }
    
    /// <summary>
    /// Left Rear shock velocity (m/s)
    /// </summary>
    public float LRshockVel { get; set; }
    
    /// <summary>
    /// Right Rear shock velocity (m/s)
    /// </summary>
    public float RRshockVel { get; set; }

    // Environmental conditions
    /// <summary>
    /// Air temperature in Celsius
    /// </summary>
    public float AirTemp { get; set; }
    
    /// <summary>
    /// Air density in kg/m³ (affects downforce)
    /// </summary>
    public float AirDensity { get; set; }
    
    /// <summary>
    /// Atmospheric pressure in hPa
    /// </summary>
    public float AirPressure { get; set; }
    
    /// <summary>
    /// Relative humidity percentage (0-100)
    /// </summary>
    public float RelativeHumidity { get; set; }
    
    /// <summary>
    /// Track surface temperature in Celsius
    /// </summary>
    public float TrackTemp { get; set; }
    
    /// <summary>
    /// Track temperature from crew chief in Celsius
    /// </summary>
    public float TrackTempCrew { get; set; }
    
    /// <summary>
    /// Player on pit road status
    /// </summary>
    public bool OnPitRoad { get; set; }
    
    /// <summary>
    /// Sky condition enum (0=clear, 3=overcast)
    /// </summary>
    public int Skies { get; set; }
    
    /// <summary>
    /// Weather type enum
    /// </summary>
    public int WeatherType { get; set; }
    
    /// <summary>
    /// Fog density percentage (0-100)
    /// </summary>
    public float FogLevel { get; set; }
    
    /// <summary>
    /// Track surface wetness. See <see cref="TrackWetnessLevel"/>:
    /// 0 = Unknown, 1 = Dry, 2 = MostlyDry, 3 = VeryLightlyWet, 4 = LightlyWet,
    /// 5 = ModeratelyWet, 6 = VeryWet, 7 = ExtremelyWet.
    /// (The previous comment here was off by one — it started the scale at Dry = 0.)
    /// Only meaningful in sessions with the rain system enabled.
    /// </summary>
    public int TrackWetness { get; set; }
    
    /// <summary>
    /// Wind speed in m/s
    /// </summary>
    public float WindVel { get; set; }
    
    /// <summary>
    /// Wind direction in radians
    /// </summary>
    public float WindDir { get; set; }
    
    /// <summary>
    /// Whether rain tires are allowed (track declared wet)
    /// </summary>
    public bool WeatherDeclaredWet { get; set; }
    
    // Motion & Orientation
    /// <summary>
    /// World-space X velocity in m/s
    /// </summary>
    public float VelocityX { get; set; }
    
    /// <summary>
    /// World-space Y velocity (vertical) in m/s
    /// </summary>
    public float VelocityY { get; set; }
    
    /// <summary>
    /// World-space Z velocity in m/s
    /// </summary>
    public float VelocityZ { get; set; }
    
    /// <summary>
    /// Vehicle pitch angle in radians
    /// </summary>
    public float Pitch { get; set; }
    
    /// <summary>
    /// Rate of pitch change in rad/s
    /// </summary>
    public float PitchRate { get; set; }
    
    /// <summary>
    /// Vehicle roll angle in radians
    /// </summary>
    public float Roll { get; set; }
    
    /// <summary>
    /// Rate of roll change in rad/s
    /// </summary>
    public float RollRate { get; set; }
    
    // Driver Inputs (Raw)
    /// <summary>
    /// Raw brake pedal input (pre-ABS) (0.0 to 1.0)
    /// </summary>
    public float BrakeRaw { get; set; }
    
    /// <summary>
    /// Raw throttle input (pre-TC) (0.0 to 1.0)
    /// </summary>
    public float ThrottleRaw { get; set; }
    
    /// <summary>
    /// Raw clutch pedal input (0.0 to 1.0)
    /// </summary>
    public float ClutchRaw { get; set; }
    
    /// <summary>
    /// Handbrake input (rally cars) (0.0 to 1.0)
    /// </summary>
    public float HandbrakeRaw { get; set; }

    // Session Info (from session info string)
    /// <summary>
    /// Driver name
    /// </summary>
    public string DriverName { get; set; } = string.Empty;
    
    /// <summary>
    /// Car number
    /// </summary>
    public string CarNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Car model/screen name (e.g., "Ferrari 488 GT3", "Formula IR-04")
    /// Parsed from YAML SessionInfo → DriverInfo → Drivers[PlayerIdx] → CarScreenName
    /// </summary>
    public string CarScreenName { get; set; } = string.Empty;
    
    /// <summary>
    /// Current setup name loaded in iRacing garage
    /// Parsed from YAML SessionInfo → DriverInfo → DriverSetupName
    /// Example: "Garage 61 - MRT#9 - Spa 2025 - Race Setup V3"
    /// </summary>
    public string DriverSetupName { get; set; } = string.Empty;
    
    /// <summary>
    /// Flag indicating if driver has modified the setup from saved version
    /// Parsed from YAML SessionInfo → DriverInfo → DriverSetupIsModified
    /// 0 = Unmodified, 1 = Modified
    /// </summary>
    public int DriverSetupIsModified { get; set; }
    
    /// <summary>
    /// Track name
    /// </summary>
    public string TrackName { get; set; } = string.Empty;
    
    /// <summary>
    /// Track length in meters (parsed from YAML SessionInfo)
    /// </summary>
    public float TrackLength { get; set; }
    
    /// <summary>
    /// Track pit speed limit in meters per second (parsed from YAML SessionInfo)
    /// Converted from kph format (e.g., "55.98 kph" → 15.55 m/s)
    /// </summary>
    public float TrackPitSpeedLimit { get; set; }
    
    // ===== TURN TRACKING =====
    
    /// <summary>
    /// Current turn number (1-based), or 0 if not in a turn
    /// Calculated from LapDistPct using track turn database
    /// </summary>
    public int TurnNumber { get; set; }
    
    /// <summary>
    /// Current turn name (e.g., "Eau Rouge", "Casino Square"), or empty if not in a turn
    /// Loaded from track turn database
    /// </summary>
    public string TurnName { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether player is currently in a defined turn
    /// </summary>
    public bool IsInTurn { get; set; }
    
    /// <summary>
    /// Progress through current turn (0.0 - 1.0), or 0 if not in a turn
    /// </summary>
    public float TurnProgress { get; set; }
    
    /// <summary>
    /// Last completed turn number (1-based), or 0 if no track data
    /// Calculated from LapDistPct using track turn database
    /// </summary>
    public int LastTurnNumber { get; set; }
    
    /// <summary>
    /// Last completed turn name (e.g., "Parabolica"), or empty if no track data
    /// Loaded from track turn database
    /// </summary>
    public string LastTurnName { get; set; } = string.Empty;
    
    /// <summary>
    /// Next upcoming turn number (1-based), or 0 if no track data
    /// Calculated from LapDistPct using track turn database
    /// </summary>
    public int NextTurnNumber { get; set; }
    
    /// <summary>
    /// Next upcoming turn name (e.g., "Ascari"), or empty if no track data
    /// Loaded from track turn database
    /// </summary>
    public string NextTurnName { get; set; } = string.Empty;
    
    // ===== PHASE 1: 4-Way Proximity Radar =====
    
    /// <summary>
    /// Lateral spotter enum: 0=Clear, 1=CarLeft, 2=CarRight, 3=CarBothSides
    /// This is the same data the in-game spotter uses for left/right warnings.
    /// </summary>
    public int CarLeftRight { get; set; }

    /// <summary>
    /// Distance to the car directly ahead in meters (SDK-provided, track-distance based)
    /// </summary>
    public float CarDistAhead { get; set; }

    /// <summary>
    /// Distance to the car directly behind in meters (SDK-provided, track-distance based)
    /// </summary>
    public float CarDistBehind { get; set; }

    /// <summary>
    /// Time gap to nearest car ahead in seconds (computed from LapDistPct and EstTime).
    /// Positive value. 0 = no car ahead or data unavailable.
    /// </summary>
    public float GapAhead { get; set; }

    /// <summary>
    /// Time gap to nearest car behind in seconds (computed from LapDistPct and EstTime).
    /// Positive value. 0 = no car behind or data unavailable.
    /// </summary>
    public float GapBehind { get; set; }
    
    /// <summary>
    /// Track position percentage for each car (0.0-1.0). Array of 64 cars.
    /// Index corresponds to CarIdx. -1 or values outside 0-1 indicate car not on track.
    /// </summary>
    public float[]? CarIdxLapDistPct { get; set; }
    
    /// <summary>
    /// Pit road status for each car. Array of 64 bools.
    /// </summary>
    public bool[]? CarIdxOnPitRoad { get; set; }
    
    /// <summary>
    /// Track surface type for each car (enum). Array of 64 ints.
    /// </summary>
    public int[]? CarIdxTrackSurface { get; set; }
    
    /// <summary>
    /// Car class ID for each car. Array of 64 ints.
    /// </summary>
    public int[]? CarIdxClass { get; set; }
    
    /// <summary>
    /// Lap number for each car. Array of 64 ints.
    /// </summary>
    public int[]? CarIdxLap { get; set; }
    
    /// <summary>
    /// Overall race position for each car (1st, 2nd, etc). Array of 64 ints.
    /// </summary>
    public int[]? CarIdxPosition { get; set; }
    
    /// <summary>
    /// Class position for each car. Array of 64 ints.
    /// </summary>
    public int[]? CarIdxClassPosition { get; set; }
    
    /// <summary>
    /// Current gear for each car. Array of 64 ints.
    /// </summary>
    public int[]? CarIdxGear { get; set; }
    
    /// <summary>
    /// Engine RPM for each car. Array of 64 floats.
    /// </summary>
    public float[]? CarIdxRPM { get; set; }
    
    /// <summary>
    /// Estimated time to reach position for each car. Array of 64 floats.
    /// </summary>
    public float[]? CarIdxEstTime { get; set; }
    
    /// <summary>
    /// Time behind leader for each car. Array of 64 floats.
    /// </summary>
    public float[]? CarIdxF2Time { get; set; }
    
    /// <summary>
    /// Last lap time for each car in seconds. Array of 64 floats.
    /// </summary>
    public float[]? CarIdxLastLapTime { get; set; }

    /// <summary>
    /// Best lap time for each car in seconds. Array of 64 floats.
    /// Used for qualifying/practice position calculation
    /// </summary>
    public float[]? CarIdxBestLapTime { get; set; }

    /// <summary>
    /// Per-car session flags bitfield (irsdk_Flags). Array of 64 ints.
    /// Bits: Black=0x10000, DSQ=0x20000, Repair/Meatball=0x100000
    /// </summary>
    public int[]? CarIdxSessionFlags { get; set; }

    // ===== ADDITIONAL CarIdx ARRAYS (complete SDK coverage) =====

    /// <summary>
    /// Lap number of each car's best lap. Array of 64 ints.
    /// </summary>
    public int[]? CarIdxBestLapNum { get; set; }

    /// <summary>
    /// Laps completed per car. Array of 64 ints.
    /// Different from CarIdxLap (current lap) - this is total completed laps.
    /// </summary>
    public int[]? CarIdxLapCompleted { get; set; }

    /// <summary>
    /// Fast repairs used per car. Array of 64 ints.
    /// </summary>
    public int[]? CarIdxFastRepairsUsed { get; set; }

    /// <summary>
    /// Push-to-pass count remaining per car. Array of 64 ints.
    /// </summary>
    public int[]? CarIdxP2P_Count { get; set; }

    /// <summary>
    /// Push-to-pass active status per car. Array of 64 bools.
    /// </summary>
    public bool[]? CarIdxP2P_Status { get; set; }

    /// <summary>
    /// Pace flags per car (irsdk_PaceFlags). Array of 64 ints.
    /// </summary>
    public int[]? CarIdxPaceFlags { get; set; }

    /// <summary>
    /// Pace line assignment per car. Array of 64 ints.
    /// </summary>
    public int[]? CarIdxPaceLine { get; set; }

    /// <summary>
    /// Pace row assignment per car. Array of 64 ints.
    /// </summary>
    public int[]? CarIdxPaceRow { get; set; }

    /// <summary>
    /// Qualifying tire compound per car. Array of 64 ints.
    /// </summary>
    public int[]? CarIdxQualTireCompound { get; set; }

    /// <summary>
    /// Qualifying tire compound locked per car. Array of 64 bools.
    /// </summary>
    public bool[]? CarIdxQualTireCompoundLocked { get; set; }

    /// <summary>
    /// Steering angle per car in radians. Array of 64 floats.
    /// </summary>
    public float[]? CarIdxSteer { get; set; }

    /// <summary>
    /// Current tire compound per car. Array of 64 ints.
    /// </summary>
    public int[]? CarIdxTireCompound { get; set; }

    /// <summary>
    /// Track surface material type per car (enum). Array of 64 ints.
    /// </summary>
    public int[]? CarIdxTrackSurfaceMaterial { get; set; }
    
    /// <summary>
    /// Map of CarIdx to Car Number string (from YAML DriverInfo:Drivers)
    /// Used for pit exit position display (e.g., "4s ahead of #14")
    /// </summary>
    public Dictionary<int, string>? CarIdxToCarNumber { get; set; }

    /// <summary>
    /// Map of CarIdx to Driver Name string (from YAML DriverInfo:Drivers)
    /// Used for competitor intelligence (e.g., "Lewis Hamilton pitting on Lap 12")
    /// </summary>
    public Dictionary<int, string>? CarIdxToDriverName { get; set; }

    /// <summary>
    /// Map of CarIdx to iRating (from YAML DriverInfo:Drivers)
    /// </summary>
    public Dictionary<int, int>? CarIdxToIRating { get; set; }

    /// <summary>
    /// Map of CarIdx to Safety Rating float (from YAML DriverInfo:Drivers)
    /// </summary>
    public Dictionary<int, float>? CarIdxToSafetyRating { get; set; }

    /// <summary>
    /// Map of CarIdx to License Class string (from YAML DriverInfo:Drivers)
    /// e.g., "A", "B", "C", "D", "R", "Pro", "WC"
    /// </summary>
    public Dictionary<int, string>? CarIdxToLicenseClass { get; set; }

    /// <summary>
    /// Map of CarIdx to cumulative incident count (CurDriverIncidentCount from session info).
    /// Updates on each session info refresh.
    /// </summary>
    public Dictionary<int, int>? CarIdxToIncidentCount { get; set; }

    /// <summary>
    /// Flags per CarIdx: true if driver gained incidents since last session info update.
    /// Cleared automatically after a few seconds. Only for non-player cars.
    /// </summary>
    public bool[]? CarIdxRecentIncident { get; set; }

    /// <summary>
    /// Per-car recent incident delta (how many x incidents gained in the last event, e.g. 2 = 2x, 4 = 4x).
    /// </summary>
    public int[]? CarIdxRecentIncidentDelta { get; set; }

    /// <summary>
    /// Map of CarIdx to short car model name (3-letter abbreviation from session info).
    /// </summary>
    public Dictionary<int, string>? CarIdxToCarModel { get; set; }

    /// <summary>
    /// Map of CarIdx to 2-letter country code (from iRacing ClubID mapping).
    /// </summary>
    public Dictionary<int, string>? CarIdxToCountryCode { get; set; }

    /// <summary>
    /// Player heading angle in radians (yaw around Z-axis)
    /// </summary>
    public float Yaw { get; set; }
    
    /// <summary>
    /// Rate of heading change in radians per second
    /// </summary>
    public float YawRate { get; set; }

    /// <summary>
    /// Car index of the pace/safety car (-1 if none). Set from YAML DriverInfo.
    /// </summary>
    public int PaceCarIdx { get; set; } = -1;

    // ═══════════════════════════════════════════════════════════════════
    // TELEMETRY CAPABILITY AUDIT — newly subscribed channels
    // ═══════════════════════════════════════════════════════════════════

    // ── Session context ───────────────────────────────────────────────

    /// <summary>Player car is on track (not in the garage, not watching a replay).</summary>
    public bool IsOnTrack { get; set; }

    /// <summary>Player is in the garage.</summary>
    public bool IsInGarage { get; set; }

    /// <summary>The garage screen is being displayed.</summary>
    public bool IsGarageVisible { get; set; }

    /// <summary>A replay is currently playing.</summary>
    public bool IsReplayPlaying { get; set; }

    /// <summary>Player's own track surface (iRacing TrackSurface enum).</summary>
    public int PlayerTrackSurface { get; set; }

    /// <summary>
    /// True when overlays should be showing: on track, not in the garage, not in a replay.
    /// </summary>
    public bool IsDriving => IsOnTrack && !IsInGarage && !IsGarageVisible && !IsReplayPlaying;

    // ── Pit service (what is armed for the next stop) ─────────────────

    /// <summary>Fuel fill is armed for the next pit stop.</summary>
    public bool PitSvFuelArmed { get; set; }

    /// <summary>Fuel amount armed, in kilograms.</summary>
    public float PitSvFuelAddKg { get; set; }

    /// <summary>All-four tire change is armed.</summary>
    public bool PitSvTiresArmed { get; set; }

    /// <summary>Fast repair is armed.</summary>
    public bool PitSvFastRepairArmed { get; set; }

    /// <summary>Pit lane is currently open.</summary>
    public bool PitsOpen { get; set; }

    /// <summary>Fast repairs still available in this session.</summary>
    public int FastRepairAvailable { get; set; }

    /// <summary>
    /// Litres still needed to reach the finish, from the fuel calculator.
    /// Propagated onto the frame so the pit confirmation can cross-check it against
    /// the fuel actually armed — the app computed this but never compared the two.
    /// </summary>
    public float FuelNeededToFinishL { get; set; }

    // ── Weather ───────────────────────────────────────────────────────

    // TrackWetness and WeatherDeclaredWet are declared with the other weather
    // fields above. They existed as placeholders but were never populated — the
    // channels were not subscribed, so both always read zero.

    /// <summary>Live precipitation rate.</summary>
    public float Precipitation { get; set; }

    /// <summary>Sun elevation in radians — negative is below the horizon.</summary>
    public float SolarAltitude { get; set; }

    /// <summary>In-sim time of day, in seconds since midnight.</summary>
    public float SessionTimeOfDay { get; set; }

    // ── Engine health ─────────────────────────────────────────────────

    /// <summary>Engine warning flags (see <see cref="EngineWarningFlags"/>).</summary>
    public int EngineWarnings { get; set; }

    /// <summary>Battery voltage.</summary>
    public float Voltage { get; set; }

    /// <summary>The sim's own shift indicator, 0-1.</summary>
    public float ShiftIndicatorPct { get; set; }

    /// <summary>Share of peak power the engine is currently making, 0-1.</summary>
    public float ShiftPowerPct { get; set; }

    // ── Force feedback ────────────────────────────────────────────────

    /// <summary>Current wheel torque as a share of the configured maximum (0-1, &gt;=1 is clipping).</summary>
    public float SteeringWheelPctTorque { get; set; }

    /// <summary>Configured wheel maximum force, in Nm.</summary>
    public float SteeringWheelMaxForceNm { get; set; }

    /// <summary>Peak force seen this session, in Nm.</summary>
    public float SteeringWheelPeakForceNm { get; set; }

    /// <summary>Wheel limiter engagement.</summary>
    public float SteeringWheelLimiter { get; set; }

    // ── Sim performance and network ───────────────────────────────────

    /// <summary>The SIMULATOR's frame rate — not the overlay's.</summary>
    public float SimFrameRate { get; set; }

    /// <summary>GPU utilisation reported by the sim.</summary>
    public float GpuUsage { get; set; }

    /// <summary>Foreground CPU utilisation reported by the sim.</summary>
    public float CpuUsageFG { get; set; }

    /// <summary>Connection quality, 0-1. Below ~0.9 explains warping cars.</summary>
    public float ChanQuality { get; set; }

    /// <summary>Connection latency, in seconds.</summary>
    public float ChanLatency { get; set; }

    /// <summary>Average connection latency, in seconds.</summary>
    public float ChanAvgLatency { get; set; }

    // ── Tire strategy ─────────────────────────────────────────────────

    /// <summary>Tire sets still available.</summary>
    public int TireSetsAvailable { get; set; }

    /// <summary>Tire sets used so far.</summary>
    public int TireSetsUsed { get; set; }

    /// <summary>Dry tire set limit for this series (0 = unlimited).</summary>
    public int DryTireSetLimit { get; set; }

    /// <summary>Compound currently fitted.</summary>
    public int PlayerTireCompound { get; set; }

    /// <summary>Cold pressures per corner. Hot pressures are disk-logging-only.</summary>
    public float LFcoldPressure { get; set; }
    public float RFcoldPressure { get; set; }
    public float LRcoldPressure { get; set; }
    public float RRcoldPressure { get; set; }

    // ── Incidents and penalties ───────────────────────────────────────

    /// <summary>Shared team incident count — the number that matters in endurance racing.</summary>
    public int TeamIncidentCount { get; set; }

    /// <summary>This driver's own incident count.</summary>
    public int DriverIncidentCount { get; set; }

    /// <summary>Success ballast currently carried.</summary>
    public float WeightPenalty { get; set; }

    /// <summary>Fast repairs consumed by the player.</summary>
    public int FastRepairsUsed { get; set; }

    // ── Delta validity ────────────────────────────────────────────────

    /// <summary>
    /// Whether <see cref="LapDeltaToBestLap"/> is meaningful. iRacing clears this on
    /// out-laps, in-laps and before a reference exists — the app previously displayed
    /// the number regardless.
    /// </summary>
    public bool LapDeltaToBestLapOK { get; set; }

    /// <summary>Whether the session-best delta is meaningful.</summary>
    public bool LapDeltaToSessionBestLapOK { get; set; }

    /// <summary>Delta to the theoretical best lap from your own best sectors.</summary>
    public float LapDeltaToOptimalLap { get; set; }

    /// <summary>Whether the optimal-lap delta is meaningful.</summary>
    public bool LapDeltaToOptimalLapOK { get; set; }

    // ── Endurance ─────────────────────────────────────────────────────

    /// <summary>Number of drivers who have taken a stint in this car.</summary>
    public int DriversSoFar { get; set; }

    /// <summary>Driver-change lap status.</summary>
    public int DriverChangeLapStatus { get; set; }

    // ===== SHARED DERIVED STATE =====
    //
    // Computed once per tick on the telemetry thread and published here.
    // Widgets previously each owned a private calculator and re-derived this
    // from the UI thread — the same 64-car work two or three times per frame,
    // with the instances holding separate per-car state (pit timers, out-lap
    // flags) that could disagree about the same car.

    /// <summary>
    /// Full relative table (all on-track cars, ordered farthest-ahead → player →
    /// farthest-behind). Widgets slice this to their own row counts.
    /// Null before the first calculation or when position data is unavailable.
    /// </summary>
    public IReadOnlyList<RelativeEntry>? Relatives { get; set; }

    /// <summary>
    /// Full standings table ordered by overall position.
    /// Null before the first calculation or when position data is unavailable.
    /// </summary>
    public IReadOnlyList<StandingsEntry>? Standings { get; set; }

    /// <summary>
    /// Wheel-lockup state for this frame. Previously this only advanced as a side
    /// effect of a widget displaying the field, so detection silently stopped when
    /// nothing showed it and double-stepped when two boxes did.
    /// </summary>
    public WheelLockupState? WheelLockup { get; set; }

    /// <summary>
    /// Whether a full-course caution (safety car) is currently active.
    /// Derived from SessionFlags bit 0x4000 (Caution) or 0x8 (Yellow).
    /// </summary>
    public bool IsCautionActive { get; set; }

    /// <summary>
    /// Speed in km/h (calculated from m/s)
    /// </summary>
    public float SpeedKmh => Telemetry.UnitConversions.MpsToKmh(Speed);

    /// <summary>
    /// Speed in mph (calculated from m/s)
    /// </summary>
    public float SpeedMph => Telemetry.UnitConversions.MpsToMph(Speed);
}
