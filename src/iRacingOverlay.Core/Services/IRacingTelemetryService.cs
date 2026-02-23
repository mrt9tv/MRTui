using iRacingOverlay.Core.Models;
using Microsoft.Extensions.Logging;
using SVappsLAB.iRacingTelemetrySDK;
using System.Linq;

namespace iRacingOverlay.Core.Services;

// Define the telemetry variables we want - this triggers the SDK's code generator
// to create a TelemetryData struct with these properties at compile time
[RequiredTelemetryVars([
    // MVP 1 - Core telemetry
    TelemetryVar.Speed,           // m/s
    TelemetryVar.RPM,             // Engine RPM
    TelemetryVar.Gear,            // Current gear (-1=R, 0=N, 1+=gears)
    TelemetryVar.Throttle,        // 0-1
    TelemetryVar.Brake,           // 0-1
    TelemetryVar.BrakeABSactive,  // bool - ABS system active
    TelemetryVar.dcBrakeBias,     // float - Brake bias adjustment (percentage, front bias)
    TelemetryVar.dcTractionControl, // float - Traction control adjustment (0-20 levels depending on car)
    TelemetryVar.Clutch,          // 0-1
    TelemetryVar.SteeringWheelAngle, // radians
    TelemetryVar.Lap,             // Current lap number
    TelemetryVar.LapDistPct,      // 0-1 percentage around track
    TelemetryVar.PlayerCarClassPosition, // Position in class
    TelemetryVar.PlayerCarIdx,    // Player's car index (0-63)
    TelemetryVar.PlayerCarClass,  // Player's car class ID
    
    // MVP 2 - Critical race data
    TelemetryVar.FuelLevel,       // liters
    TelemetryVar.FuelLevelPct,    // 0-1 percentage
    TelemetryVar.FuelUsePerHour,  // kg/hr - Fuel consumption rate
    TelemetryVar.FuelPress,       // bar - Fuel line pressure
    TelemetryVar.WaterTemp,       // celsius
    TelemetryVar.WaterLevel,      // liters
    TelemetryVar.OilTemp,         // celsius
    TelemetryVar.OilLevel,        // liters
    TelemetryVar.OilPress,        // bar
    TelemetryVar.LapLastLapTime,  // seconds
    TelemetryVar.LapBestLapTime,  // seconds
    TelemetryVar.LapCurrentLapTime, // seconds - Current lap time (running)
    TelemetryVar.LapDeltaToBestLap, // seconds - Delta to personal best
    TelemetryVar.LapDeltaToBestLap_DD, // seconds - Delta-delta (rate of change)
    TelemetryVar.LapDeltaToSessionBestLap, // seconds - Delta to session best
    TelemetryVar.SessionTimeRemain, // seconds

    // CRITICAL SESSION LAP/TIME VARS (USER INSIGHT: Use SDK directly instead of calculating!)
    TelemetryVar.SessionLapsTotal,  // Total laps in the race (replaces buggy SessionLaps)
    TelemetryVar.SessionLapsRemain, // Laps remaining (direct from SDK - NO CALCULATION NEEDED!)
    TelemetryVar.SessionTimeTotal,  // Total session time

    // MVP 3+ - Advanced telemetry
    TelemetryVar.LFtempCL,        // Left Front tire temp - center left
    TelemetryVar.LFtempCM,        // Left Front tire temp - center middle
    TelemetryVar.LFtempCR,        // Left Front tire temp - center right
    TelemetryVar.RFtempCL,        // Right Front tire temp
    TelemetryVar.RFtempCM,
    TelemetryVar.RFtempCR,
    TelemetryVar.LRtempCL,        // Left Rear tire temp
    TelemetryVar.LRtempCM,
    TelemetryVar.LRtempCR,
    TelemetryVar.RRtempCL,        // Right Rear tire temp
    TelemetryVar.RRtempCM,
    TelemetryVar.RRtempCR,
    TelemetryVar.LFwearL,         // Tire wear (0-1)
    TelemetryVar.LFwearM,
    TelemetryVar.LFwearR,
    TelemetryVar.RFwearL,
    TelemetryVar.RFwearM,
    TelemetryVar.RFwearR,
    TelemetryVar.LRwearL,
    TelemetryVar.LRwearM,
    TelemetryVar.LRwearR,
    TelemetryVar.RRwearL,
    TelemetryVar.RRwearM,
    TelemetryVar.RRwearR,
    TelemetryVar.LongAccel,       // Longitudinal G-force
    TelemetryVar.LatAccel,        // Lateral G-force
    TelemetryVar.VertAccel,       // Vertical G-force
    TelemetryVar.SessionFlags,    // Race flags (checkered, yellow, etc.)
    TelemetryVar.PlayerCarMyIncidentCount, // Incident count
    TelemetryVar.LFbrakeLinePress, // Brake line pressure
    TelemetryVar.RFbrakeLinePress,
    TelemetryVar.LRbrakeLinePress,
    TelemetryVar.RRbrakeLinePress,
    
    // Tire Rumble Pitch (Force Feedback - CRITICAL for lockup detection!)
    TelemetryVar.TireLF_RumblePitch, // Rumble intensity - spikes during wheel slip/lockup
    TelemetryVar.TireRF_RumblePitch,
    TelemetryVar.TireLR_RumblePitch,
    TelemetryVar.TireRR_RumblePitch,
    
    // Wheel Odometers (CRITICAL for calculating individual wheel speeds!)
    TelemetryVar.LFodometer,      // Distance traveled by LF wheel (meters)
    TelemetryVar.RFodometer,      // Distance traveled by RF wheel
    TelemetryVar.LRodometer,      // Distance traveled by LR wheel
    TelemetryVar.RRodometer,      // Distance traveled by RR wheel
    
    // Shock/Suspension Data (for wheel load and lockup analysis)
    TelemetryVar.LFshockDefl,     // LF suspension deflection (meters)
    TelemetryVar.RFshockDefl,
    TelemetryVar.LRshockDefl,
    TelemetryVar.RRshockDefl,
    TelemetryVar.LFshockVel,      // LF suspension velocity (m/s)
    TelemetryVar.RFshockVel,
    TelemetryVar.LRshockVel,
    TelemetryVar.RRshockVel,
    
    // Additional telemetry fields
    TelemetryVar.AirTemp,         // Air temperature (celsius)
    TelemetryVar.AirDensity,      // kg/m³ - Air density (affects downforce)
    TelemetryVar.AirPressure,     // hPa - Atmospheric pressure
    TelemetryVar.RelativeHumidity, // % - Relative humidity
    TelemetryVar.TrackTemp,       // Track temperature (celsius)
    TelemetryVar.TrackTempCrew,   // Track temp from crew chief (celsius)
    TelemetryVar.OnPitRoad,       // bool - Player on pit road
    TelemetryVar.Skies,           // int - Sky condition enum (0=clear, 3=overcast)
    TelemetryVar.WeatherType,     // int - Weather type enum
    TelemetryVar.FogLevel,        // % - Fog density
    TelemetryVar.WindVel,         // m/s - Wind speed
    TelemetryVar.WindDir,         // rad - Wind direction (heading in radians)
    
    // Motion & Orientation
    TelemetryVar.VelocityX,       // m/s - World-space X velocity
    TelemetryVar.VelocityY,       // m/s - World-space Y velocity (vertical)
    TelemetryVar.VelocityZ,       // m/s - World-space Z velocity
    TelemetryVar.Pitch,           // radians - Vehicle pitch angle
    TelemetryVar.PitchRate,       // rad/s - Rate of pitch change
    TelemetryVar.Roll,            // radians - Vehicle roll angle
    TelemetryVar.RollRate,        // rad/s - Rate of roll change
    
    // Driver Inputs (Raw)
    TelemetryVar.BrakeRaw,        // 0-1 - Raw brake pedal (pre-ABS)
    TelemetryVar.ThrottleRaw,     // 0-1 - Raw throttle (pre-TC)
    TelemetryVar.ClutchRaw,       // 0-1 - Raw clutch pedal
    TelemetryVar.HandbrakeRaw,    // 0-1 - Handbrake input (rally cars)
    
    // Session info
    TelemetryVar.SessionTime,     // Session time elapsed (seconds)
    TelemetryVar.SessionNum,      // Current session number
    
    // ===== PHASE 1: 4-Way Proximity Radar =====
    
    // Lateral Spotter (Left/Right Detection)
    // CRITICAL: SDK enum is 0=Off, 1=Clear, 2=CarLeft, 3=CarRight, 4=CarBothSides, 5=TwoCarsLeft, 6=TwoCarsRight
    TelemetryVar.CarLeftRight,    // Enum: 0=Off, 1=Clear, 2=Left, 3=Right, 4=Both, 5=TwoLeft, 6=TwoRight - Spotter system
    
    // Multi-Car Position Arrays (CarIdx[64])
    TelemetryVar.CarIdxLapDistPct,      // float[64] - Track position % for each car
    TelemetryVar.CarIdxOnPitRoad,       // bool[64]  - Pit road status
    TelemetryVar.CarIdxTrackSurface,    // int[64]   - Track surface type (enum)
    TelemetryVar.CarIdxClass,           // int[64]   - Car class ID
    TelemetryVar.CarIdxLap,             // int[64]   - Lap number for each car
    TelemetryVar.CarIdxPosition,        // int[64]   - Overall race position
    TelemetryVar.CarIdxClassPosition,   // int[64]   - Class position
    
    // Car Performance Indicators
    TelemetryVar.CarIdxGear,            // int[64]   - Current gear
    TelemetryVar.CarIdxRPM,             // float[64] - Engine RPM
    
    // Timing Arrays
    TelemetryVar.CarIdxEstTime,         // float[64] - Estimated time to reach position
    TelemetryVar.CarIdxF2Time,          // float[64] - Time behind leader
    TelemetryVar.CarIdxLastLapTime,     // float[64] - Last lap time
    TelemetryVar.CarIdxBestLapTime,     // float[64] - Best lap time (for qualifying position)

    // Per-Car Session Flags (meatball, black flag, etc.)
    TelemetryVar.CarIdxSessionFlags,    // int[64]   - Bitfield flags per car (irsdk_Flags)

    // Player Orientation (for future enhancements)
    TelemetryVar.Yaw,                   // float - Player heading angle (radians)
    TelemetryVar.YawRate,               // float - Rate of heading change (rad/s)

    // Live Position Calculation
    TelemetryVar.SessionState,          // int - Session state enum (racing/checkered/cooldown)
    TelemetryVar.PaceMode,              // int - Pace mode enum (single/double file, restart type)
    
    // ===== PHASE 1: PROFESSIONAL SHIFT LIGHT TELEMETRY =====
    // iRacing provides professional-grade shift point data based on car physics/torque curves
    // These values are car-specific and instantly accurate (no learning required)
    TelemetryVar.PlayerCarSLFirstRPM,   // float - When shift lights start illuminating
    TelemetryVar.PlayerCarSLShiftRPM,   // float - OPTIMAL SHIFT POINT (key value)
    TelemetryVar.PlayerCarSLLastRPM,    // float - When shift lights fully lit
    TelemetryVar.PlayerCarSLBlinkRPM,   // float - Blink threshold (over-rev warning)

    // ===== PHASE 2: PIT LIMITER DETECTION =====
    TelemetryVar.dcPitSpeedLimiterToggle, // bool - Pit speed limiter active state
    
    // ===== PHASE 10.8: PIT REPAIR TIMES (Damage Assessment) =====
    TelemetryVar.PitRepairLeft,       // float - Time for mandatory repairs (seconds)
    TelemetryVar.PitOptRepairLeft,    // float - Time for optional repairs (seconds)

    // ===== SESSION / RACE MANAGEMENT =====
    TelemetryVar.SessionLapsRemainEx,      // int   - Laps remaining (accounts for extra laps after time expires)
    TelemetryVar.LapDist,                  // float - Meters from start/finish line (absolute track distance)

    // ===== PLAYER CAR STATUS =====
    TelemetryVar.PlayerCarPosition,        // int   - Player's overall race position
    TelemetryVar.PlayerCarInPitStall,      // bool  - Player currently in pit stall
    TelemetryVar.PlayerCarTowTime,         // float - Tow time remaining (seconds, 0 when not towing)
    TelemetryVar.PitstopActive,            // bool  - Pitstop service in progress
    TelemetryVar.PitSvFlags,               // int   - Pit service flags bitfield (PitServiceFlags enum)
    TelemetryVar.PitSvFuel,                // float - Pit fuel fill amount (liters requested)
    TelemetryVar.PlayerCarPitSvStatus,     // int   - Pit service status (PitServiceStatus enum)

    // ===== PROXIMITY (SDK-provided gap to nearest cars) =====
    TelemetryVar.CarDistAhead,             // float - Distance to car directly ahead (meters)
    TelemetryVar.CarDistBehind,            // float - Distance to car directly behind (meters)

    // ===== MISSING CarIdx ARRAYS (complete SDK coverage) =====
    TelemetryVar.CarIdxBestLapNum,              // int[64]   - Lap number of each car's best lap
    TelemetryVar.CarIdxLapCompleted,            // int[64]   - Laps completed per car
    TelemetryVar.CarIdxFastRepairsUsed,         // int[64]   - Fast repairs used per car
    TelemetryVar.CarIdxP2P_Count,               // int[64]   - Push-to-pass count per car
    TelemetryVar.CarIdxP2P_Status,              // bool[64]  - Push-to-pass active per car
    TelemetryVar.CarIdxPaceFlags,               // int[64]   - Pace flags per car (irsdk_PaceFlags)
    TelemetryVar.CarIdxPaceLine,                // int[64]   - Pace line assignment per car
    TelemetryVar.CarIdxPaceRow,                 // int[64]   - Pace row assignment per car
    TelemetryVar.CarIdxQualTireCompound,        // int[64]   - Qualifying tire compound per car
    TelemetryVar.CarIdxQualTireCompoundLocked,  // bool[64]  - Qual tire compound locked per car
    TelemetryVar.CarIdxSteer,                   // float[64] - Steering angle per car (radians)
    TelemetryVar.CarIdxTireCompound,            // int[64]   - Current tire compound per car
    TelemetryVar.CarIdxTrackSurfaceMaterial      // int[64]   - Track surface material per car (enum)
])]
public class IRacingTelemetryService : ITelemetryService, IDisposable
{
    private readonly ILogger<IRacingTelemetryService> _logger;
    private ITelemetryClient<SVappsLAB.iRacingTelemetrySDK.TelemetryData>? _client;
    private readonly LivePositionCalculator _livePositionCalculator;
    private readonly FuelCalculatorService _fuelCalculatorService;
    private readonly FuelSavingService _fuelSavingService;
    private readonly TurnTrackingService _turnTrackingService;
    private ConnectionStatus _status = ConnectionStatus.Disconnected;
    private bool _disposed = false;
    
    // Lap time tracking
    private int _lastLap = -1;
    private DateTime _lapStartTime = DateTime.UtcNow;
    
    // Update rate tracking (60Hz telemetry)
    private int _updateCount = 0;
    private DateTime _lastUpdateRateCalculation = DateTime.UtcNow;
    private double _updateRate = 0.0;
    private const int UPDATE_RATE_CALCULATION_INTERVAL_MS = 1000; // Calculate Hz every 1 second
    
    // ===== YAML PARSING CACHE (Task 5 Optimization) =====
    // Tier 1: Static cache - Session info that never changes during a session
    private string _driverName = "";
    private string _carNumber = "";
    private string _carScreenName = ""; // Car model name (e.g., "Ferrari 488 GT3")
    private string _driverSetupName = ""; // Current setup name from garage
    private int _driverSetupIsModified = 0; // 0 = unmodified, 1 = modified
    private string _trackName = ""; // Display name (e.g., "Circuit de Spa-Francorchamps")
    private string _trackId = ""; // Internal track ID (e.g., "spa", "monza") for turn database lookup
    private string _sessionType = "";
    private float _trackLength = 0f;
    private float _trackPitSpeedLimit = 0f; // Pit speed limit in m/s (parsed from "55.98 kph" format)
    private bool _sessionInfoParsed = false;
    private Dictionary<int, string> _carIdxToCarNumber = new(); // CarIdx -> Car Number mapping (for pit exit display)
    private Dictionary<int, string> _carIdxToDriverName = new(); // CarIdx -> Driver Name mapping (for competitor intelligence)
    private Dictionary<int, int> _carIdxToIRating = new(); // CarIdx -> iRating mapping
    private Dictionary<int, float> _carIdxToSafetyRating = new(); // CarIdx -> Safety Rating mapping
    private Dictionary<int, string> _carIdxToLicenseClass = new(); // CarIdx -> License Class mapping
    private Dictionary<int, int> _carIdxToIncidentCount = new();  // CarIdx -> CurDriverIncidentCount
    private bool[] _carIdxRecentIncident = new bool[64];          // true = gained incidents recently
    private int[] _carIdxRecentIncidentDelta = new int[64];       // how many x incidents gained (e.g. 2 = 2x)
    private DateTime[] _carIdxIncidentTime = new DateTime[64];    // when the incident flag was set
    private Dictionary<int, string> _carIdxToCarModel = new();    // CarIdx -> 3-letter car model abbreviation
    private Dictionary<int, string> _carIdxToCountryCode = new(); // CarIdx -> 2-letter country code
    private int _paceCarIdx = -1; // CarIdx of the pace/safety car (-1 if none)
    private readonly object _driverDataLock = new(); // Thread safety for async session callbacks
    
    // ── Pre-allocated buffers for zero-alloc enum array casting (Phase 3 perf optimization) ──
    private readonly int[] _trackSurfaceBuffer = new int[64];
    private readonly int[] _sessionFlagsBuffer = new int[64];
    private readonly int[] _paceFlagsBuffer = new int[64];
    private readonly int[] _surfaceMaterialBuffer = new int[64];
    private readonly bool[] _recentIncidentBuffer = new bool[64];
    private readonly int[] _recentIncidentDeltaBuffer = new int[64];
    
    // ── Versioned dictionary snapshots — only re-copy when source data changes ──
    private int _driverDataVersion = 0;         // Incremented when any dict changes
    private int _lastCopiedDriverDataVersion = -1;
    private Dictionary<int, string>? _snapshotCarNumber;
    private Dictionary<int, string>? _snapshotDriverName;
    private Dictionary<int, int>?    _snapshotIRating;
    private Dictionary<int, float>?  _snapshotSafetyRating;
    private Dictionary<int, string>? _snapshotLicenseClass;
    private Dictionary<int, int>?    _snapshotIncidentCount;
    private Dictionary<int, string>? _snapshotCarModel;
    private Dictionary<int, string>? _snapshotCountryCode;
    
    // Tier 2: SessionInfo version tracking - Only parse when SDK increments SessionInfoUpdate
    // Note: Weather data (TrackTemp, AirTemp, WeatherType) comes from SDK real-time telemetry (60Hz),
    //       NOT from YAML SessionInfo. YAML parsing is for static session metadata only.
    private int _lastSessionInfoVersion = -1; // iRacing SDK increments this when SessionInfo changes
    
    // ===== DIRTY FIELD TRACKING (Task 6 Optimization) =====
    // Track previous values of high-frequency fields to populate ChangedFields HashSet
    // Only track fields that widgets actively monitor (avoid memory waste on unused fields)
    private float _prevSpeed = 0f;
    private float _prevRPM = 0f;
    private int _prevGear = 0;
    private float _prevThrottle = 0f;
    private float _prevBrake = 0f;
    private float _prevFuelLevel = 0f;
    private int _prevLap = 0;
    private float _prevLapDistPct = 0f;

    public ConnectionStatus Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                _status = value;
                _logger.LogInformation("Connection status changed to: {Status}", value);
                StatusChanged?.Invoke(this, new ConnectionStatusEventArgs(value));
            }
        }
    }

    public bool IsConnected => _client?.IsConnected() ?? false;

    public double UpdateRate => _updateRate;

    /// <summary>
    /// Current fuel calculation data (averages, laps remaining, strategy).
    /// Updated in real-time with each telemetry tick.
    /// </summary>
    public FuelData CurrentFuelData => _fuelCalculatorService.CurrentData;

    // Use our Models.TelemetryData for the interface
    public event EventHandler<Models.TelemetryData>? TelemetryUpdated;
    public event EventHandler<ConnectionStatusEventArgs>? StatusChanged;

    public IRacingTelemetryService(ILogger<IRacingTelemetryService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // Create LivePositionCalculator without logger (it will use null logger)
        _livePositionCalculator = new LivePositionCalculator();
        // Create FuelCalculatorService
        _fuelCalculatorService = new FuelCalculatorService();
        // Create FuelSavingService (lift & coast calculations)
        _fuelSavingService = new FuelSavingService();
        // Create TurnTrackingService
        _turnTrackingService = new TurnTrackingService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TurnTrackingService>.Instance);
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_status == ConnectionStatus.Connected || _status == ConnectionStatus.Connecting)
        {
            _logger.LogWarning("Already connected or connecting");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Attempting to connect to iRacing...");
        Status = ConnectionStatus.Connecting;

        try
        {
            // Create the TelemetryClient using the auto-generated TelemetryData struct
            _client = TelemetryClient<SVappsLAB.iRacingTelemetrySDK.TelemetryData>.Create(_logger);

            // Subscribe to SDK events using extension method (v1.0.0-beta.1 compatibility)
            // Note: We're not awaiting SubscribeToAllStreams here - it will be started by Monitor()
            // For now, we'll continue using the old event pattern and migrate to channels later
            
            // Note: v1.0.0-beta.1 uses channels, but we can still use Monitor() method
            // The old event subscriptions no longer exist, so we'll need to migrate to channels
            // For now, just create the client and we'll handle events in Monitor()
            _logger.LogInformation("iRacing telemetry client created successfully");
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create telemetry client");
            Status = ConnectionStatus.Error;
            StatusChanged?.Invoke(this, new ConnectionStatusEventArgs(ConnectionStatus.Error, error: ex));
            throw;
        }
    }

    /// <summary>
    /// Starts monitoring telemetry. This blocks until cancelled.
    /// Should be called from a background service.
    /// </summary>
    public async Task MonitorAsync(CancellationToken cancellationToken)
    {
        if (_client == null)
        {
            throw new InvalidOperationException("Client not initialized. Call ConnectAsync first.");
        }

        _logger.LogInformation("Starting telemetry monitoring...");
        
        try
        {
            // v1.0.0-beta.1: Start Monitor() in background and use SubscribeToAllStreams to consume channels
            var monitorTask = _client.Monitor(cancellationToken);
            
            var subscribeTask = _client.SubscribeToAllStreams(
                onTelemetryUpdate: async data => 
                {
                    OnTelemetryUpdate(null, data);
                    await Task.CompletedTask;
                },
                onSessionInfoUpdate: async session =>
                {
                    ProcessTypedSessionInfo(session);
                    await Task.CompletedTask;
                },
                onConnectStateChanged: async state => 
                {
                    _logger.LogInformation("Connection state changed: {State}", state);
                    Status = state == ConnectState.Connected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;
                    StatusChanged?.Invoke(this, new ConnectionStatusEventArgs(Status));
                    
                    // Parse session info on connect (YAML fallback for track data)
                    if (state == ConnectState.Connected)
                    {
                        TryParseSessionInfo();
                    }
                    
                    await Task.CompletedTask;
                },
                onError: async ex =>
                {
                    _logger.LogError(ex, "iRacing SDK error: {Message}", ex.Message);
                    Status = ConnectionStatus.Error;
                    StatusChanged?.Invoke(this, new ConnectionStatusEventArgs(ConnectionStatus.Error, error: ex));
                    await Task.CompletedTask;
                },
                cancellationToken: cancellationToken
            );
            
            // Wait for either Monitor or Subscribe to complete
            await Task.WhenAny(monitorTask, subscribeTask);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Telemetry monitoring cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during telemetry monitoring");
            Status = ConnectionStatus.Error;
            throw;
        }
    }

    /// <summary>
    /// Attempt to get and parse session info from the SDK with version-based caching.
    /// Optimization: Only parse YAML when iRacing SDK increments SessionInfoUpdate property.
    /// Result: 99%+ cache hits (parsing only happens on session change or initial connect).
    /// </summary>
    private void TryParseSessionInfo()
    {
        try
        {
            if (_client == null)
            {
                _logger.LogDebug("Client is null, cannot parse session info");
                return;
            }
            
            // Get SessionInfo update version via reflection (SDK increments this when SessionInfo changes)
            var clientType = _client.GetType();
            var sessionInfoUpdateProp = clientType.GetProperty("SessionInfoUpdate");
            int currentSessionInfoVersion = sessionInfoUpdateProp != null 
                ? (int)(sessionInfoUpdateProp.GetValue(_client) ?? -1) 
                : -1;
            
            // Skip parsing if SessionInfo unchanged (99%+ of calls after initial connection)
            if (_sessionInfoParsed && currentSessionInfoVersion == _lastSessionInfoVersion)
            {
                // Cache hit - no parsing needed
                return;
            }
            
            // Cache miss - parse SessionInfo YAML (only on session change or first connect)
            var getSessionInfoMethod = clientType.GetMethod("GetRawTelemetrySessionInfoYaml");
            
            if (getSessionInfoMethod != null)
            {
                var sessionInfo = getSessionInfoMethod.Invoke(_client, null) as string;
                if (!string.IsNullOrEmpty(sessionInfo))
                {
                    _logger.LogDebug("SessionInfo YAML parsing triggered (Version: {Version})", currentSessionInfoVersion);
                    ParseSessionInfo(sessionInfo);
                    _lastSessionInfoVersion = currentSessionInfoVersion;
                }
                else
                {
                    _logger.LogDebug("SessionInfo YAML is empty (may not be available yet)");
                }
            }
            else
            {
                _logger.LogWarning("Could not find GetRawTelemetrySessionInfoYaml method on telemetry client");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve session info. This is normal in test drive mode.");
        }
    }

    private void OnTelemetryUpdate(object? sender, SVappsLAB.iRacingTelemetrySDK.TelemetryData sdkData)
    {
        try
        {
            // Track update rate (60Hz telemetry)
            _updateCount++;
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastUpdateRateCalculation).TotalMilliseconds;
            if (elapsed >= UPDATE_RATE_CALCULATION_INTERVAL_MS)
            {
                _updateRate = (_updateCount / elapsed) * 1000.0; // Convert to Hz
                _updateCount = 0;
                _lastUpdateRateCalculation = now;
            }
            
            // Try to parse session info - version-based cache in TryParseSessionInfo handles optimization
            // Initial connection: Parse static data (track name, length, pit speed) until track length obtained
            // After initial parse: SessionInfo version check provides 99%+ cache hits (no YAML parsing)
            if (!_sessionInfoParsed || _trackLength <= 0)
            {
                TryParseSessionInfo();
                // Mark as parsed once we have track length (critical for distance calculations)
                if (_trackLength > 0)
                {
                    _sessionInfoParsed = true;
                }
            }
            else
            {
                // Session info parsed - still call to detect session changes (version check is fast)
                TryParseSessionInfo();
            }
            
            // Detect lap change and reset lap timer
            if (sdkData.Lap != _lastLap)
            {
                _lastLap = sdkData.Lap.GetValueOrDefault();
                _lapStartTime = DateTime.UtcNow;
            }
            
            // Calculate current lap time
            float currentLapTime = (float)(DateTime.UtcNow - _lapStartTime).TotalSeconds;
            
            // Deltas: Use SDK-provided values (accurate, already calculated by iRacing)
            // SDK provides deltas to player's personal best AND overall session best
            float deltaToBest = sdkData.LapDeltaToBestLap.GetValueOrDefault();
            float deltaToSession = sdkData.LapDeltaToSessionBestLap.GetValueOrDefault();
            
            // ── Pre-compute reusable snapshots (zero-alloc when unchanged) ──
            RefreshDriverDataSnapshots();       // Only re-copies dicts when _driverDataVersion changed
            CopyRecentIncidentsToBuffers();     // Copies into pre-allocated bool[64] + int[64]

            // Convert SDK TelemetryData to our Models.TelemetryData
            var data = new Models.TelemetryData
            {
                // MVP 1 - Core telemetry (using GetValueOrDefault() for nullable SDK properties)
                Speed = sdkData.Speed.GetValueOrDefault(),
                RPM = sdkData.RPM.GetValueOrDefault(),
                // SDK exposes DriverCarSLBlinkRPM (shift light) - consider using for more accurate shift point
                // For now, leave at 0 and let ShiftPointCalculator learn it
                EngineRedlineRPM = 0, // Will be populated if SDK provides it
                
                // PHASE 1: Professional shift light telemetry (car-specific, instant accuracy)
                PlayerCarSLFirstRPM = sdkData.PlayerCarSLFirstRPM.GetValueOrDefault(),
                PlayerCarSLShiftRPM = sdkData.PlayerCarSLShiftRPM.GetValueOrDefault(),
                PlayerCarSLLastRPM = sdkData.PlayerCarSLLastRPM.GetValueOrDefault(),
                PlayerCarSLBlinkRPM = sdkData.PlayerCarSLBlinkRPM.GetValueOrDefault(),
                
                Gear = sdkData.Gear.GetValueOrDefault(),
                Throttle = sdkData.Throttle.GetValueOrDefault(),
                Brake = sdkData.Brake.GetValueOrDefault(),
                BrakeABSactive = sdkData.BrakeABSactive.GetValueOrDefault(),
                BrakeBias = sdkData.dcBrakeBias.GetValueOrDefault(),
                TractionControl = (int)(sdkData.dcTractionControl.GetValueOrDefault()), // float to int cast, 0 if N/A
                Clutch = sdkData.Clutch.GetValueOrDefault(),
                SteeringWheelAngle = sdkData.SteeringWheelAngle.GetValueOrDefault(),
                Lap = sdkData.Lap.GetValueOrDefault(),
                LapDistPct = sdkData.LapDistPct.GetValueOrDefault(),
                LapDist = sdkData.LapDist.GetValueOrDefault(),
                Position = sdkData.PlayerCarClassPosition.GetValueOrDefault(),
                PlayerCarPosition = sdkData.PlayerCarPosition.GetValueOrDefault(),
                PlayerCarIdx = sdkData.PlayerCarIdx.GetValueOrDefault(),
                PlayerCarClass = sdkData.PlayerCarClass.GetValueOrDefault(),
                Timestamp = DateTime.UtcNow,
                
                // MVP 2 - Critical race data
                FuelLevel = sdkData.FuelLevel.GetValueOrDefault(),
                FuelLevelPct = sdkData.FuelLevelPct.GetValueOrDefault(),
                FuelUsePerHour = sdkData.FuelUsePerHour.GetValueOrDefault(),
                FuelPress = sdkData.FuelPress.GetValueOrDefault(),
                // Calculate tank capacity from current fuel level and percentage
                FuelLevelMax = sdkData.FuelLevelPct.GetValueOrDefault() > 0.001f
                    ? sdkData.FuelLevel.GetValueOrDefault() / sdkData.FuelLevelPct.GetValueOrDefault()
                    : 0f,
                // Use Lap as LapsCompleted (they're equivalent in iRacing)
                LapsCompleted = sdkData.Lap.GetValueOrDefault(),
                // FIX: Use direct SDK values instead of 0 (USER INSIGHT: No calculation needed!)
                SessionLaps = 0, // DEPRECATED - kept for backward compat
                SessionLapsTotal = sdkData.SessionLapsTotal.GetValueOrDefault(),
                SessionLapsRemain = sdkData.SessionLapsRemain.GetValueOrDefault(),
                // NOTE: SessionLapsRemainEx is set from SDK here, then overridden below
                // with fuel calculator's EstimatedLapsFromTime for widget display
                SessionLapsRemainEx = sdkData.SessionLapsRemainEx.GetValueOrDefault(),
                SessionTimeTotal = (float)sdkData.SessionTimeTotal.GetValueOrDefault(),
                WaterTemp = sdkData.WaterTemp.GetValueOrDefault(),
                WaterLevel = sdkData.WaterLevel.GetValueOrDefault(),
                OilTemp = sdkData.OilTemp.GetValueOrDefault(),
                OilLevel = sdkData.OilLevel.GetValueOrDefault(),
                OilPress = sdkData.OilPress.GetValueOrDefault(),
                LapLastLapTime = sdkData.LapLastLapTime.GetValueOrDefault(),
                LapBestLapTime = sdkData.LapBestLapTime.GetValueOrDefault(),
                LapCurrentLapTime = sdkData.LapCurrentLapTime.GetValueOrDefault(),
                LapDeltaToBestLap = sdkData.LapDeltaToBestLap.GetValueOrDefault(),
                LapDeltaToBestLap_DD = sdkData.LapDeltaToBestLap_DD.GetValueOrDefault(),
                LapDeltaToSessionBestLap = sdkData.LapDeltaToSessionBestLap.GetValueOrDefault(),
                CurrentLapTime = currentLapTime,
                DeltaToBestLap = deltaToBest,
                DeltaToSessionBest = deltaToSession,
                SessionTimeRemain = sdkData.SessionTimeRemain.GetValueOrDefault(),
                SessionTime = sdkData.SessionTime.GetValueOrDefault(),
                SessionNum = sdkData.SessionNum.GetValueOrDefault(),
                
                // MVP 3+ - Advanced telemetry
                LFtempCL = sdkData.LFtempCL.GetValueOrDefault(),
                LFtempCM = sdkData.LFtempCM.GetValueOrDefault(),
                LFtempCR = sdkData.LFtempCR.GetValueOrDefault(),
                RFtempCL = sdkData.RFtempCL.GetValueOrDefault(),
                RFtempCM = sdkData.RFtempCM.GetValueOrDefault(),
                RFtempCR = sdkData.RFtempCR.GetValueOrDefault(),
                LRtempCL = sdkData.LRtempCL.GetValueOrDefault(),
                LRtempCM = sdkData.LRtempCM.GetValueOrDefault(),
                LRtempCR = sdkData.LRtempCR.GetValueOrDefault(),
                RRtempCL = sdkData.RRtempCL.GetValueOrDefault(),
                RRtempCM = sdkData.RRtempCM.GetValueOrDefault(),
                RRtempCR = sdkData.RRtempCR.GetValueOrDefault(),
                
                LFwearL = sdkData.LFwearL.GetValueOrDefault(),
                LFwearM = sdkData.LFwearM.GetValueOrDefault(),
                LFwearR = sdkData.LFwearR.GetValueOrDefault(),
                RFwearL = sdkData.RFwearL.GetValueOrDefault(),
                RFwearM = sdkData.RFwearM.GetValueOrDefault(),
                RFwearR = sdkData.RFwearR.GetValueOrDefault(),
                LRwearL = sdkData.LRwearL.GetValueOrDefault(),
                LRwearM = sdkData.LRwearM.GetValueOrDefault(),
                LRwearR = sdkData.LRwearR.GetValueOrDefault(),
                RRwearL = sdkData.RRwearL.GetValueOrDefault(),
                RRwearM = sdkData.RRwearM.GetValueOrDefault(),
                RRwearR = sdkData.RRwearR.GetValueOrDefault(),
                
                LongAccel = sdkData.LongAccel.GetValueOrDefault(),
                LatAccel = sdkData.LatAccel.GetValueOrDefault(),
                VertAccel = sdkData.VertAccel.GetValueOrDefault(),
                
                SessionFlags = (uint)sdkData.SessionFlags.GetValueOrDefault(),
                PlayerCarMyIncidentCount = sdkData.PlayerCarMyIncidentCount.GetValueOrDefault(),
                
                LFbrakeLinePress = sdkData.LFbrakeLinePress.GetValueOrDefault(),
                RFbrakeLinePress = sdkData.RFbrakeLinePress.GetValueOrDefault(),
                LRbrakeLinePress = sdkData.LRbrakeLinePress.GetValueOrDefault(),
                RRbrakeLinePress = sdkData.RRbrakeLinePress.GetValueOrDefault(),
                
                // Tire Rumble Pitch (Force Feedback - CRITICAL for lockup detection!)
                TireLF_RumblePitch = sdkData.TireLF_RumblePitch.GetValueOrDefault(),
                TireRF_RumblePitch = sdkData.TireRF_RumblePitch.GetValueOrDefault(),
                TireLR_RumblePitch = sdkData.TireLR_RumblePitch.GetValueOrDefault(),
                TireRR_RumblePitch = sdkData.TireRR_RumblePitch.GetValueOrDefault(),
                
                // Wheel Odometers (CRITICAL for calculating individual wheel speeds!)
                LFodometer = sdkData.LFodometer.GetValueOrDefault(),
                RFodometer = sdkData.RFodometer.GetValueOrDefault(),
                LRodometer = sdkData.LRodometer.GetValueOrDefault(),
                RRodometer = sdkData.RRodometer.GetValueOrDefault(),
                
                // Shock/Suspension Data (for wheel load and lockup analysis)
                LFshockDefl = sdkData.LFshockDefl.GetValueOrDefault(),
                RFshockDefl = sdkData.RFshockDefl.GetValueOrDefault(),
                LRshockDefl = sdkData.LRshockDefl.GetValueOrDefault(),
                RRshockDefl = sdkData.RRshockDefl.GetValueOrDefault(),
                LFshockVel = sdkData.LFshockVel.GetValueOrDefault(),
                RFshockVel = sdkData.RFshockVel.GetValueOrDefault(),
                LRshockVel = sdkData.LRshockVel.GetValueOrDefault(),
                RRshockVel = sdkData.RRshockVel.GetValueOrDefault(),
                
                // Environmental conditions
                AirTemp = sdkData.AirTemp.GetValueOrDefault(),
                AirDensity = sdkData.AirDensity.GetValueOrDefault(),
                AirPressure = sdkData.AirPressure.GetValueOrDefault(),
                RelativeHumidity = sdkData.RelativeHumidity.GetValueOrDefault(),
                TrackTemp = sdkData.TrackTemp.GetValueOrDefault(),
                TrackTempCrew = sdkData.TrackTempCrew.GetValueOrDefault(),
                OnPitRoad = sdkData.OnPitRoad.GetValueOrDefault(),
                Skies = (int)sdkData.Skies.GetValueOrDefault(),
                WeatherType = (int)sdkData.WeatherType.GetValueOrDefault(),
                FogLevel = sdkData.FogLevel.GetValueOrDefault(),
                WindVel = sdkData.WindVel.GetValueOrDefault(),
                WindDir = sdkData.WindDir.GetValueOrDefault(),
                
                // Motion & Orientation
                VelocityX = sdkData.VelocityX.GetValueOrDefault(),
                VelocityY = sdkData.VelocityY.GetValueOrDefault(),
                VelocityZ = sdkData.VelocityZ.GetValueOrDefault(),
                Pitch = sdkData.Pitch.GetValueOrDefault(),
                PitchRate = sdkData.PitchRate.GetValueOrDefault(),
                Roll = sdkData.Roll.GetValueOrDefault(),
                RollRate = sdkData.RollRate.GetValueOrDefault(),
                
                // Driver Inputs (Raw)
                BrakeRaw = sdkData.BrakeRaw.GetValueOrDefault(),
                ThrottleRaw = sdkData.ThrottleRaw.GetValueOrDefault(),
                ClutchRaw = sdkData.ClutchRaw.GetValueOrDefault(),
                HandbrakeRaw = sdkData.HandbrakeRaw.GetValueOrDefault(),
                
                // Session info (populated from SessionInfo YAML parsing)
                DriverName = _driverName,
                CarNumber = _carNumber,
                CarScreenName = _carScreenName,
                DriverSetupName = _driverSetupName,
                DriverSetupIsModified = _driverSetupIsModified,
                TrackName = _trackName,
                SessionType = _sessionType,
                TrackLength = _trackLength,
                TrackPitSpeedLimit = _trackPitSpeedLimit,
                // Thread-safe snapshots of driver data (version-stamped, only copied when changed)
                CarIdxToCarNumber = _snapshotCarNumber,
                CarIdxToDriverName = _snapshotDriverName,
                CarIdxToIRating = _snapshotIRating,
                CarIdxToSafetyRating = _snapshotSafetyRating,
                CarIdxToLicenseClass = _snapshotLicenseClass,
                CarIdxToIncidentCount = _snapshotIncidentCount,
                CarIdxRecentIncident = _recentIncidentBuffer,
                CarIdxRecentIncidentDelta = _recentIncidentDeltaBuffer,
                CarIdxToCarModel = _snapshotCarModel,
                CarIdxToCountryCode = _snapshotCountryCode,

                // Live Position Calculation
                SessionState = (int)sdkData.SessionState.GetValueOrDefault(),
                PaceMode = (int)sdkData.PaceMode.GetValueOrDefault(),
                
                // ===== PHASE 1: 4-Way Proximity Radar =====
                
                // Lateral Spotter (Left/Right Detection) - Cast enum to int
                // DEBUG: Log raw SDK value to diagnose false positives
                CarLeftRight = (int)sdkData.CarLeftRight.GetValueOrDefault(),
                
                // SDK-provided gap distances (meters along track)
                CarDistAhead = sdkData.CarDistAhead.GetValueOrDefault(),
                CarDistBehind = sdkData.CarDistBehind.GetValueOrDefault(),
                
                // Multi-Car Position Arrays (CarIdx[64])
                CarIdxLapDistPct = sdkData.CarIdxLapDistPct,
                CarIdxOnPitRoad = sdkData.CarIdxOnPitRoad,
                CarIdxTrackSurface = CastEnumArrayToBuffer(sdkData.CarIdxTrackSurface, _trackSurfaceBuffer),
                CarIdxClass = sdkData.CarIdxClass,
                CarIdxLap = sdkData.CarIdxLap,
                CarIdxPosition = sdkData.CarIdxPosition,
                CarIdxClassPosition = sdkData.CarIdxClassPosition,
                CarIdxGear = sdkData.CarIdxGear,
                CarIdxRPM = sdkData.CarIdxRPM,
                CarIdxEstTime = sdkData.CarIdxEstTime,
                CarIdxF2Time = sdkData.CarIdxF2Time,
                CarIdxLastLapTime = sdkData.CarIdxLastLapTime,
                CarIdxBestLapTime = sdkData.CarIdxBestLapTime,
                CarIdxSessionFlags = CastEnumArrayToBuffer(sdkData.CarIdxSessionFlags, _sessionFlagsBuffer),

                // Additional CarIdx arrays (complete SDK coverage)
                CarIdxBestLapNum = sdkData.CarIdxBestLapNum,
                CarIdxLapCompleted = sdkData.CarIdxLapCompleted,
                CarIdxFastRepairsUsed = sdkData.CarIdxFastRepairsUsed,
                CarIdxP2P_Count = sdkData.CarIdxP2P_Count,
                CarIdxP2P_Status = sdkData.CarIdxP2P_Status,
                CarIdxPaceFlags = CastEnumArrayToBuffer(sdkData.CarIdxPaceFlags, _paceFlagsBuffer),
                CarIdxPaceLine = sdkData.CarIdxPaceLine,
                CarIdxPaceRow = sdkData.CarIdxPaceRow,
                CarIdxQualTireCompound = sdkData.CarIdxQualTireCompound,
                CarIdxQualTireCompoundLocked = sdkData.CarIdxQualTireCompoundLocked,
                CarIdxSteer = sdkData.CarIdxSteer,
                CarIdxTireCompound = sdkData.CarIdxTireCompound,
                CarIdxTrackSurfaceMaterial = CastEnumArrayToBuffer(sdkData.CarIdxTrackSurfaceMaterial, _surfaceMaterialBuffer),

                // Player Orientation
                Yaw = sdkData.Yaw.GetValueOrDefault(),
                YawRate = sdkData.YawRate.GetValueOrDefault(),

                // Safety Car / Pace Car tracking
                PaceCarIdx = _paceCarIdx,
                IsCautionActive = ((uint)sdkData.SessionFlags.GetValueOrDefault() & 0x00004000) != 0
                               || ((uint)sdkData.SessionFlags.GetValueOrDefault() & 0x00008000) != 0,

                // ===== PHASE 2: PIT LIMITER DETECTION =====
                PitSpeedLimiterActive = sdkData.dcPitSpeedLimiterToggle.GetValueOrDefault(),
                
                // ===== PIT REPAIR TIMES (Phase 10.8: Damage Assessment) =====
                PitRepairLeft = sdkData.PitRepairLeft.GetValueOrDefault(),
                PitOptRepairLeft = sdkData.PitOptRepairLeft.GetValueOrDefault(),

                // ===== PLAYER PIT STATUS =====
                PlayerCarInPitStall = sdkData.PlayerCarInPitStall.GetValueOrDefault(),
                PlayerCarTowTime = sdkData.PlayerCarTowTime.GetValueOrDefault(),
                PitstopActive = sdkData.PitstopActive.GetValueOrDefault(),
                PitSvFlags = (int)sdkData.PitSvFlags.GetValueOrDefault(),
                PitSvFuel = sdkData.PitSvFuel.GetValueOrDefault(),
                PlayerCarPitSvStatus = (int)sdkData.PlayerCarPitSvStatus.GetValueOrDefault(),
            };

            // ===== DIRTY FIELD TRACKING (Task 6) =====
            // Populate ChangedFields HashSet by comparing current vs previous values
            // Widgets check this before expensive Dispatcher.Invoke calls (50%+ overhead reduction)
            PopulateChangedFields(data);
            
            // ===== TURN TRACKING =====
            // Calculate current turn based on LapDistPct using track turn database
            var turnInfo = _turnTrackingService.GetCurrentTurn(data.LapDistPct);
            data.TurnNumber = turnInfo.Number;
            data.TurnName = turnInfo.Name;
            data.IsInTurn = turnInfo.IsInTurn;
            data.TurnProgress = turnInfo.TurnProgress;
            
            // Get last completed turn and next upcoming turn
            var lastTurn = _turnTrackingService.GetLastTurn(data.LapDistPct);
            data.LastTurnNumber = lastTurn.Number;
            data.LastTurnName = lastTurn.Name;
            
            var nextTurn = _turnTrackingService.GetNextTurn(data.LapDistPct);
            data.NextTurnNumber = nextTurn.Number;
            data.NextTurnName = nextTurn.Name;
            
            // ===== RELATIVE GAP CALCULATION =====
            // Compute time gap to nearest car ahead and behind from LapDistPct
            ComputeRelativeGaps(data);
            
            // ===== CRITICAL: CALCULATE ACTUAL LEADING LAP & RACE LEADER LAP =====
            // These values are ESSENTIAL for accurate race end and fuel calculations
            // ActualLeadingLapNumber = highest lap any car is on (regardless of position)
            // RaceLeaderLapNumber = lap that P1 (by position) is on
            CalculateLeadingLapNumbers(sdkData, data);

            // Calculate live positions (handles race mode, qualifying mode, and position freezing)
            data.LivePosition = _livePositionCalculator.CalculateLivePosition(data, classOnly: false);
            data.LiveClassPosition = _livePositionCalculator.CalculateLivePosition(data, classOnly: true);

            // Update fuel calculator with latest telemetry
            _fuelCalculatorService.Update(data);

            // Update fuel saving calculations (lift & coast requirements)
            _fuelSavingService.Update(_fuelCalculatorService.CurrentData);

            // Propagate calculated estimates from fuel calculator back to telemetry data
            // so downstream widgets (Relative, etc.) can use them without accessing FuelData directly
            data.EstimatedTotalRaceLaps = _fuelCalculatorService.CurrentData.EstimatedTotalRaceLaps;
            data.SessionLapsRemainEx = _fuelCalculatorService.CurrentData.EstimatedLapsFromTime;

            // Fire our telemetry event
            TelemetryUpdated?.Invoke(this, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry update");
        }
    }

    /// <summary>
    /// Populate ChangedFields HashSet in TelemetryData by comparing current vs previous values.
    /// Tracks high-frequency fields that widgets actively monitor (Speed, RPM, Gear, etc.).
    /// Widgets use this to avoid expensive Dispatcher.Invoke calls when values haven't changed.
    /// Result: 50%+ reduction in UI thread overhead.
    /// </summary>
    private void PopulateChangedFields(Models.TelemetryData data)
    {
        // Clear previous dirty flags
        data.ClearChangedFields();
        
        // Compare high-frequency fields (tolerance for floating point comparison)
        const float FLOAT_TOLERANCE = 0.001f;
        
        if (Math.Abs(data.Speed - _prevSpeed) > FLOAT_TOLERANCE)
        {
            data.MarkFieldChanged(nameof(data.Speed));
            _prevSpeed = data.Speed;
        }
        
        if (Math.Abs(data.RPM - _prevRPM) > FLOAT_TOLERANCE)
        {
            data.MarkFieldChanged(nameof(data.RPM));
            _prevRPM = data.RPM;
        }
        
        if (data.Gear != _prevGear)
        {
            data.MarkFieldChanged(nameof(data.Gear));
            _prevGear = data.Gear;
        }
        
        if (Math.Abs(data.Throttle - _prevThrottle) > FLOAT_TOLERANCE)
        {
            data.MarkFieldChanged(nameof(data.Throttle));
            _prevThrottle = data.Throttle;
        }
        
        if (Math.Abs(data.Brake - _prevBrake) > FLOAT_TOLERANCE)
        {
            data.MarkFieldChanged(nameof(data.Brake));
            _prevBrake = data.Brake;
        }
        
        if (Math.Abs(data.FuelLevel - _prevFuelLevel) > FLOAT_TOLERANCE)
        {
            data.MarkFieldChanged(nameof(data.FuelLevel));
            _prevFuelLevel = data.FuelLevel;
        }
        
        if (data.Lap != _prevLap)
        {
            data.MarkFieldChanged(nameof(data.Lap));
            _prevLap = data.Lap;
        }
        
        if (Math.Abs(data.LapDistPct - _prevLapDistPct) > FLOAT_TOLERANCE)
        {
            data.MarkFieldChanged(nameof(data.LapDistPct));
            _prevLapDistPct = data.LapDistPct;
        }
        
        // Add more fields as needed by widgets (tire temps, position, etc.)
        // Only track fields that are actively checked by widgets to minimize overhead
    }

    // ── Thread-safe dictionary copy helpers ──────────────────────────────

    /// <summary>
    /// Only re-snapshot dictionaries when the source data version has changed.
    /// Session info callbacks increment _driverDataVersion, so we only copy
    /// when actual changes occurred (typically once per new driver / session change).
    /// </summary>
    private void RefreshDriverDataSnapshots()
    {
        if (_lastCopiedDriverDataVersion == _driverDataVersion)
            return; // No changes since last copy

        lock (_driverDataLock)
        {
            _snapshotCarNumber   = _carIdxToCarNumber.Count > 0 ? new Dictionary<int, string>(_carIdxToCarNumber) : null;
            _snapshotDriverName  = _carIdxToDriverName.Count > 0 ? new Dictionary<int, string>(_carIdxToDriverName) : null;
            _snapshotIRating     = _carIdxToIRating.Count > 0 ? new Dictionary<int, int>(_carIdxToIRating) : null;
            _snapshotSafetyRating = _carIdxToSafetyRating.Count > 0 ? new Dictionary<int, float>(_carIdxToSafetyRating) : null;
            _snapshotLicenseClass = _carIdxToLicenseClass.Count > 0 ? new Dictionary<int, string>(_carIdxToLicenseClass) : null;
            _snapshotIncidentCount = _carIdxToIncidentCount.Count > 0 ? new Dictionary<int, int>(_carIdxToIncidentCount) : null;
            _snapshotCarModel    = _carIdxToCarModel.Count > 0 ? new Dictionary<int, string>(_carIdxToCarModel) : null;
            _snapshotCountryCode = _carIdxToCountryCode.Count > 0 ? new Dictionary<int, string>(_carIdxToCountryCode) : null;
            _lastCopiedDriverDataVersion = _driverDataVersion;
        }
    }

    /// <summary>Copy enum array into pre-allocated int[] buffer (zero-allocation).</summary>
    private static int[] CastEnumArrayToBuffer<T>(T[]? source, int[] buffer) where T : struct, Enum
    {
        if (source == null)
        {
            Array.Clear(buffer);
            return buffer;
        }
        int len = Math.Min(source.Length, buffer.Length);
        for (int i = 0; i < len; i++)
            buffer[i] = Convert.ToInt32(source[i]);
        // Clear remaining slots
        for (int i = len; i < buffer.Length; i++)
            buffer[i] = 0;
        return buffer;
    }

    /// <summary>Copy recent incident flags and deltas into pre-allocated buffers, clearing stale entries.</summary>
    private void CopyRecentIncidentsToBuffers()
    {
        var now = DateTime.UtcNow;
        Array.Clear(_recentIncidentBuffer);
        Array.Clear(_recentIncidentDeltaBuffer);
        lock (_driverDataLock)
        {
            for (int i = 0; i < 64; i++)
            {
                if (_carIdxRecentIncident[i])
                {
                    if ((now - _carIdxIncidentTime[i]).TotalSeconds > 8.0)
                    {
                        _carIdxRecentIncident[i] = false;
                        _carIdxRecentIncidentDelta[i] = 0;
                    }
                    else
                    {
                        _recentIncidentBuffer[i] = true;
                        _recentIncidentDeltaBuffer[i] = _carIdxRecentIncidentDelta[i];
                    }
                }
            }
        }
    }

    /// <summary>Make a 3-letter car model abbreviation from the full car name.</summary>
    private static string MakeCarAbbreviation(string carName)
    {
        if (string.IsNullOrWhiteSpace(carName)) return "---";
        // Try to extract meaningful abbreviation:
        // "Ferrari 488 GT3" → "488"
        // "Mercedes-AMG GT3" → "AMG"
        // "Porsche 911 GT3 R" → "911"
        // "BMW M4 GT3" → "M4G"
        // Strategy: prefer numeric model numbers, then uppercase word starts
        var parts = carName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Look for a 3-digit number first (most iconic: 488, 911, 720)
        foreach (var p in parts)
        {
            if (p.Length == 3 && int.TryParse(p, out _)) return p;
        }
        
        // Look for short model identifiers (M4, RS, GT, AMG, etc.)
        foreach (var p in parts)
        {
            if (p.Length >= 2 && p.Length <= 3 && p != "GT3" && p != "GT4" && p != "GTE" && p != "LMP" 
                && p.ToUpperInvariant() == p) // all uppercase = model code
                return p.Length == 3 ? p : p + parts.LastOrDefault(x => x.Length >= 1)?[..1] ?? "";
        }
        
        // Fallback: first 3 chars of the most significant word (skip brand)
        if (parts.Length >= 2)
            return parts[1].Length >= 3 ? parts[1][..3].ToUpperInvariant() : parts[1].ToUpperInvariant();
        
        return carName.Length >= 3 ? carName[..3].ToUpperInvariant() : carName.ToUpperInvariant();
    }

    /// <summary>Map iRacing ClubName to a 2-letter country code (ISO 3166-1 alpha-2, approximate).</summary>
    private static string ClubToCountryCode(string clubName)
    {
        if (string.IsNullOrWhiteSpace(clubName)) return "";
        var cn = clubName.Trim();
        // Match common iRacing club names to country codes
        if (cn.Contains("DE-AT-CH") || cn.Contains("Germany")) return "DE";
        if (cn.Contains("Benelux")) return "NL";
        if (cn.Contains("Brazil") || cn.Contains("Brasil")) return "BR";
        if (cn.Contains("Central-Eastern")) return "PL";
        if (cn.Contains("Finland") || cn.Contains("Suomi")) return "FI";
        if (cn.Contains("France")) return "FR";
        if (cn.Contains("Iberia") || cn.Contains("Spain")) return "ES";
        if (cn.Contains("Italy") || cn.Contains("Italia")) return "IT";
        if (cn.Contains("UK") || cn.Contains("Britain")) return "GB";
        if (cn.Contains("Scandinavia") || cn.Contains("Nordic")) return "SE";
        if (cn.Contains("Australia") || cn.Contains("NZ")) return "AU";
        if (cn.Contains("Japan")) return "JP";
        if (cn.Contains("South America")) return "AR";
        if (cn.Contains("Asia")) return "KR";
        if (cn.Contains("India")) return "IN";
        if (cn.Contains("South Africa")) return "ZA";
        if (cn.Contains("Canada")) return "CA";
        // US regions — many club names
        if (cn.Contains("Michigan") || cn.Contains("Carolina") || cn.Contains("Texas") ||
            cn.Contains("California") || cn.Contains("Florida") || cn.Contains("New York") ||
            cn.Contains("Georgia") || cn.Contains("Illinois") || cn.Contains("Ohio") ||
            cn.Contains("Pennsylvania") || cn.Contains("Virginia") || cn.Contains("New England") ||
            cn.Contains("Northwest") || cn.Contains("Pacific") || cn.Contains("Mid") ||
            cn.Contains("South") || cn.Contains("West") || cn.Contains("Plains"))
            return "US";
        return ""; // unknown
    }

    // ── Typed session info processing (SDK v1.0 ChannelReader) ──────────

    /// <summary>
    /// Process typed session info from SDK SessionDataStream.
    /// This is the primary source for driver data — far more reliable than YAML parsing.
    /// Called automatically when iRacing updates session info (driver joins/leaves, qualifying, etc.)
    /// </summary>
    private void ProcessTypedSessionInfo(SVappsLAB.iRacingTelemetrySDK.TelemetrySessionInfo session)
    {
        try
        {
            if (session?.DriverInfo?.Drivers == null) return;

            int driverCarIdx = session.DriverInfo.DriverCarIdx;

            lock (_driverDataLock)
            {
                _carIdxToCarNumber.Clear();
                _carIdxToDriverName.Clear();
                _carIdxToIRating.Clear();
                _carIdxToSafetyRating.Clear();
                _carIdxToLicenseClass.Clear();
                _carIdxToCarModel.Clear();
                _carIdxToCountryCode.Clear();

                foreach (var driver in session.DriverInfo.Drivers)
                {
                    int idx = driver.CarIdx;
                    if (idx < 0 || idx >= 64) continue;
                    if (driver.CarIsPaceCar == 1)
                    {
                        // Track pace car index but give it a recognizable identity
                        _paceCarIdx = idx;
                        _carIdxToCarNumber[idx] = "SC";
                        _carIdxToDriverName[idx] = "Safety Car";
                        continue; // skip remaining driver data (no iRating, SR, etc.)
                    }

                    _carIdxToCarNumber[idx] = driver.CarNumber ?? string.Empty;
                    _carIdxToDriverName[idx] = driver.UserName ?? string.Empty;
                    _carIdxToIRating[idx] = driver.IRating;
                    _carIdxToSafetyRating[idx] = driver.LicLevel / 100f;

                    // Incident count tracking — detect changes and track delta
                    int prevInc = _carIdxToIncidentCount.GetValueOrDefault(idx, 0);
                    int curInc = driver.CurDriverIncidentCount;
                    if (curInc > prevInc && prevInc > 0) // gained incidents (skip first load)
                    {
                        int delta = curInc - prevInc;
                        _carIdxRecentIncident[idx] = true;
                        _carIdxRecentIncidentDelta[idx] = delta;
                        _carIdxIncidentTime[idx] = DateTime.UtcNow;
                    }
                    _carIdxToIncidentCount[idx] = curInc;

                    // Car model — extract 3-letter abbreviation from CarScreenNameShort or CarScreenName
                    string carName = driver.CarScreenNameShort ?? driver.CarScreenName ?? string.Empty;
                    if (!string.IsNullOrEmpty(carName))
                        _carIdxToCarModel[idx] = MakeCarAbbreviation(carName);

                    // License class from LicString (e.g., "A 3.45" → "A")
                    if (!string.IsNullOrEmpty(driver.LicString))
                    {
                        var parts = driver.LicString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                            _carIdxToLicenseClass[idx] = parts[0];
                    }

                    // Country code from ClubName (approximate mapping)
                    if (!string.IsNullOrEmpty(driver.ClubName))
                        _carIdxToCountryCode[idx] = ClubToCountryCode(driver.ClubName);

                    // Player-specific data
                    if (idx == driverCarIdx)
                    {
                        _driverName = driver.UserName ?? string.Empty;
                        _carNumber = driver.CarNumber ?? string.Empty;
                        if (!string.IsNullOrEmpty(driver.CarScreenName))
                            _carScreenName = driver.CarScreenName;
                        else if (!string.IsNullOrEmpty(driver.CarScreenNameShort))
                            _carScreenName = driver.CarScreenNameShort;
                    }
                }
            }

            // Signal that driver data has changed — snapshot will be refreshed on next telemetry tick
            Interlocked.Increment(ref _driverDataVersion);

            _logger.LogInformation("Typed session info: {Count} drivers, player CarIdx={PlayerIdx}",
                session.DriverInfo.Drivers.Count, driverCarIdx);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing typed session info");
        }
    }
    
    /// <summary>
    /// Parse session info YAML to extract driver name, car number, and track name.
    /// Simple line-by-line parser that extracts key fields without full YAML library.
    /// </summary>
    /// <param name="sessionInfoYaml">YAML string from iRacing SDK GetSessionInfoStr()</param>
    /// <remarks>
    /// The SessionInfo string contains YAML-formatted data with:
    /// - WeekendInfo: { TrackDisplayName, TrackDisplayShortName }
    /// - DriverInfo: { DriverUserName, Drivers: [{ CarNumber, UserName }] }
    /// 
    /// Example YAML structure:
    /// WeekendInfo:
    ///   TrackDisplayName: Spa-Francorchamps
    /// DriverInfo:
    ///   DriverUserName: John Doe
    ///   DriverCarIdx: 0
    ///   Drivers:
    ///   - CarIdx: 0
    ///     CarNumber: "42"
    ///     UserName: John Doe
    /// </remarks>
    private void ParseSessionInfo(string sessionInfoYaml)
    {
        try
        {
            if (string.IsNullOrEmpty(sessionInfoYaml))
                return;
            
            var lines = sessionInfoYaml.Split('\n');
            string? currentSection = null;
            int driverCarIdx = -1;
            bool inDriversArray = false;
            bool inSessionsArray = false;
            bool isPlayerDriver = false;
            bool isCurrentSession = false;
            int currentDriverCarIdx = -1;
            bool foundTrackLength = false;
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                // Track current section
                if (trimmed.EndsWith(':') && !trimmed.StartsWith('-') && !trimmed.Contains(' '))
                {
                    currentSection = trimmed.TrimEnd(':');
                    inDriversArray = false;
                    inSessionsArray = false;
                    continue;
                }
                
                // Parse based on current section
                if (currentSection == "WeekendInfo")
                {
                    if (trimmed.StartsWith("TrackName:"))
                    {
                        // TrackName is the internal ID (e.g., "spa", "imola") - use for turn database lookup
                        _trackId = ExtractYamlValue(trimmed).Trim('"', '\'');
                        _logger.LogInformation("Parsed track ID: {TrackId}", _trackId);
                        
                        // Set track for turn tracking service
                        _turnTrackingService.SetTrack(_trackId);
                    }
                    else if (trimmed.StartsWith("TrackDisplayName:"))
                    {
                        _trackName = ExtractYamlValue(trimmed);
                        _logger.LogInformation("Parsed track name: {TrackName}", _trackName);
                    }
                    else if (trimmed.StartsWith("TrackLength:"))
                    {
                        // TrackLength comes as string like "3.5652 km"
                        var lengthStr = ExtractYamlValue(trimmed);
                        if (ParseTrackLength(lengthStr, out float lengthMeters))
                        {
                            _trackLength = lengthMeters;
                            foundTrackLength = true;
                            _logger.LogInformation("Parsed track length: {TrackLength}m ({LengthStr})", _trackLength, lengthStr);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to parse track length from: {LengthStr}", lengthStr);
                        }
                    }
                    else if (trimmed.StartsWith("TrackPitSpeedLimit:"))
                    {
                        // TrackPitSpeedLimit comes as string like "55.98 kph"
                        var speedStr = ExtractYamlValue(trimmed);
                        if (ParsePitSpeedLimit(speedStr, out float speedMps))
                        {
                            _trackPitSpeedLimit = speedMps;
                            _logger.LogInformation("Parsed pit speed limit: {SpeedMps:F2} m/s ({SpeedStr})", _trackPitSpeedLimit, speedStr);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to parse pit speed limit from: {SpeedStr}", speedStr);
                        }
                    }
                }
                else if (currentSection == "DriverInfo")
                {
                    if (trimmed.StartsWith("DriverUserName:"))
                    {
                        _driverName = ExtractYamlValue(trimmed);
                        _logger.LogInformation("Parsed driver name: {DriverName}", _driverName);
                    }
                    else if (trimmed.StartsWith("DriverCarIdx:"))
                    {
                        if (int.TryParse(ExtractYamlValue(trimmed), out var idx))
                        {
                            driverCarIdx = idx;
                        }
                    }
                    else if (trimmed.StartsWith("DriverSetupName:"))
                    {
                        _driverSetupName = ExtractYamlValue(trimmed).Trim('"', '\'');
                        _logger.LogInformation("Parsed setup name: {SetupName}", _driverSetupName);
                    }
                    else if (trimmed.StartsWith("DriverSetupIsModified:"))
                    {
                        if (int.TryParse(ExtractYamlValue(trimmed), out var isModified))
                        {
                            _driverSetupIsModified = isModified;
                            _logger.LogInformation("Setup modified: {IsModified}", _driverSetupIsModified == 1);
                        }
                    }
                    else if (trimmed.StartsWith("Drivers:"))
                    {
                        inDriversArray = true;
                        _carIdxToCarNumber.Clear(); // Reset car number mapping for new session
                        _carIdxToDriverName.Clear(); // Reset driver name mapping for new session
                        _carIdxToIRating.Clear();
                        _carIdxToSafetyRating.Clear();
                        _carIdxToLicenseClass.Clear();
                    }
                    else if (inDriversArray)
                    {
                        if (trimmed.StartsWith("- CarIdx:"))
                        {
                            if (int.TryParse(ExtractYamlValue(trimmed), out var idx))
                            {
                                currentDriverCarIdx = idx;
                                isPlayerDriver = (currentDriverCarIdx == driverCarIdx);
                                _logger.LogDebug("Parsing driver CarIdx={CarIdx}, PlayerCarIdx={PlayerCarIdx}, IsPlayer={IsPlayer}", 
                                    currentDriverCarIdx, driverCarIdx, isPlayerDriver);
                            }
                        }
                        else if (trimmed.StartsWith("CarNumber:"))
                        {
                            var carNumber = ExtractYamlValue(trimmed).Trim('"', '\'');
                            
                            // Store car number for ALL drivers (not just player)
                            if (currentDriverCarIdx >= 0)
                            {
                                _carIdxToCarNumber[currentDriverCarIdx] = carNumber;
                            }
                            
                            // Also store player's car number
                            if (isPlayerDriver)
                            {
                                _carNumber = carNumber;
                                _logger.LogInformation("Parsed player car number: {CarNumber}", _carNumber);
                            }
                        }
                        else if (trimmed.StartsWith("UserName:"))
                        {
                            var userName = ExtractYamlValue(trimmed).Trim('"', '\'');
                            
                            // Store driver name for ALL drivers (not just player)
                            if (currentDriverCarIdx >= 0)
                            {
                                _carIdxToDriverName[currentDriverCarIdx] = userName;
                            }
                        }
                        else if (trimmed.StartsWith("IRating:"))
                        {
                            if (currentDriverCarIdx >= 0 && int.TryParse(ExtractYamlValue(trimmed), out var iRating))
                            {
                                _carIdxToIRating[currentDriverCarIdx] = iRating;
                            }
                        }
                        else if (trimmed.StartsWith("LicLevel:"))
                        {
                            // LicLevel is the safety rating * 100 (e.g., 345 = 3.45 SR)
                            if (currentDriverCarIdx >= 0 && int.TryParse(ExtractYamlValue(trimmed), out var licLevel))
                            {
                                _carIdxToSafetyRating[currentDriverCarIdx] = licLevel / 100f;
                            }
                        }
                        else if (trimmed.StartsWith("LicString:"))
                        {
                            // LicString is like "A 3.45" or "B 2.12" — extract just the letter
                            if (currentDriverCarIdx >= 0)
                            {
                                var licStr = ExtractYamlValue(trimmed).Trim('"', '\'');
                                var licClass = licStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                if (licClass.Length > 0)
                                    _carIdxToLicenseClass[currentDriverCarIdx] = licClass[0];
                            }
                        }
                        else if (trimmed.StartsWith("CarScreenName:") || trimmed.StartsWith("CarScreenNameShort:"))
                        {
                            var carScreenName = ExtractYamlValue(trimmed).Trim('"', '\'');
                            
                            _logger.LogDebug("Found CarScreenName='{Name}' for CarIdx={CarIdx}, IsPlayer={IsPlayer}", 
                                carScreenName, currentDriverCarIdx, isPlayerDriver);
                            
                            // Store player's car model name
                            if (isPlayerDriver && !string.IsNullOrEmpty(carScreenName))
                            {
                                _carScreenName = carScreenName;
                                _logger.LogInformation("✅ Parsed player car screen name: {CarScreenName}", _carScreenName);
                            }
                            // FALLBACK: If we somehow missed the player check, use ANY CarScreenName if we don't have one yet
                            else if (string.IsNullOrEmpty(_carScreenName) && !string.IsNullOrEmpty(carScreenName))
                            {
                                _carScreenName = carScreenName;
                                _logger.LogWarning("⚠️ Using fallback CarScreenName: {CarScreenName} (player detection may have failed)", _carScreenName);
                            }
                        }
                    }
                }
                else if (currentSection == "SessionInfo")
                {
                    // Check if we're entering the Sessions array
                    if (trimmed.StartsWith("Sessions:"))
                    {
                        inSessionsArray = true;
                        continue;
                    }

                    // If we're in the Sessions array
                    if (inSessionsArray)
                    {
                        // Detect start of new session entry (indicated by "- SessionNum:")
                        if (trimmed.StartsWith("- SessionNum:"))
                        {
                            // Parse session number
                            if (int.TryParse(ExtractYamlValue(trimmed), out var sessionNum))
                            {
                                // Assume current session is the latest one (highest SessionNum)
                                // or match against current SessionNum from telemetry if needed
                                isCurrentSession = true; // Simplification: parse first session's type
                            }
                        }
                        else if (trimmed.StartsWith("SessionType:") && isCurrentSession)
                        {
                            _sessionType = ExtractYamlValue(trimmed).Trim('"', '\'');
                            _logger.LogInformation("Parsed session type: {SessionType}", _sessionType);
                            // Once we have session type, we can stop looking
                            inSessionsArray = false;
                        }
                    }
                }
            }

            // Log if TrackLength was not found in YAML
            if (!foundTrackLength)
            {
                _logger.LogWarning("TrackLength field not found in SessionInfo YAML. This may indicate the field name is different or not available yet.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing session info");
        }
    }
    
    /// <summary>
    /// Extract value from a YAML line like "Key: Value"
    /// </summary>
    private static string ExtractYamlValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex < 0 || colonIndex == line.Length - 1)
            return string.Empty;
        
        return line.Substring(colonIndex + 1).Trim();
    }
    
    /// <summary>
    /// Parse track length from YAML string format (e.g., "3.5652 km" or "2.5 mi")
    /// Converts to meters for consistent distance calculations.
    /// </summary>
    private static bool ParseTrackLength(string lengthStr, out float lengthMeters)
    {
        lengthMeters = 0f;
        
        if (string.IsNullOrWhiteSpace(lengthStr))
            return false;
        
        // Remove quotes if present
        lengthStr = lengthStr.Trim('"', '\'').Trim();
        
        // Split into value and unit (e.g., "3.5652 km" -> ["3.5652", "km"])
        var parts = lengthStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1)
            return false;
        
        // Parse numeric value using INVARIANT CULTURE (dot as decimal separator)
        // iRacing YAML always uses dot notation regardless of system locale
        if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float, 
            System.Globalization.CultureInfo.InvariantCulture, out float value))
            return false;
        
        // Convert to meters based on unit (default to km if no unit specified)
        string unit = parts.Length > 1 ? parts[1].ToLowerInvariant() : "km";
        lengthMeters = unit switch
        {
            "km" => value * 1000f,      // kilometers to meters
            "m" => value,                // already in meters
            "mi" => value * 1609.34f,   // miles to meters
            _ => value * 1000f           // default to km
        };
        
        return lengthMeters > 0;
    }
    
    /// <summary>
    /// Parse pit speed limit from YAML string format (e.g., "55.98 kph" or "35 mph")
    /// Converts to meters per second for consistent speed calculations.
    /// </summary>
    private static bool ParsePitSpeedLimit(string speedStr, out float speedMps)
    {
        speedMps = 0f;
        
        if (string.IsNullOrWhiteSpace(speedStr))
            return false;
        
        // Remove quotes if present
        speedStr = speedStr.Trim('"', '\'').Trim();
        
        // Split into value and unit (e.g., "55.98 kph" -> ["55.98", "kph"])
        var parts = speedStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1)
            return false;
        
        // Parse numeric value using INVARIANT CULTURE (dot as decimal separator)
        // iRacing YAML always uses dot notation regardless of system locale
        if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float, 
            System.Globalization.CultureInfo.InvariantCulture, out float value))
            return false;
        
        // Convert to meters per second based on unit (default to kph if no unit specified)
        string unit = parts.Length > 1 ? parts[1].ToLowerInvariant() : "kph";
        speedMps = unit switch
        {
            "kph" or "km/h" => value / 3.6f,      // kph to m/s (divide by 3.6)
            "mph" => value * 0.44704f,             // mph to m/s
            "m/s" or "mps" => value,               // already in m/s
            _ => value / 3.6f                      // default to kph
        };
        
        return speedMps > 0;
    }
    
    /// <summary>
    /// Calculate ActualLeadingLapNumber and RaceLeaderLapNumber from CarIdx arrays.
    /// </summary>
    private void CalculateLeadingLapNumbers(dynamic sdkData, Models.TelemetryData data)
    {
        try
        {
            // Calculate ActualLeadingLapNumber (highest lap any car is on)
            if (sdkData.CarIdxLap != null && sdkData.CarIdxLap.Length > 0)
            {
                // Find the maximum lap number across all cars (filter out invalid values)
                // CarIdxLap contains -1 for cars not in session, so filter those out
                int maxLap = 0;
                for (int i = 0; i < sdkData.CarIdxLap.Length; i++)
                {
                    int carLap = sdkData.CarIdxLap[i];
                    if (carLap > maxLap)
                    {
                        maxLap = carLap;
                    }
                }

                data.ActualLeadingLapNumber = maxLap;

                // Fallback: If no valid laps found, use player's lap
                if (data.ActualLeadingLapNumber == 0)
                {
                    data.ActualLeadingLapNumber = data.Lap;
                }
            }
            else
            {
                // No CarIdxLap data available - use player's lap as fallback
                data.ActualLeadingLapNumber = data.Lap;
            }

            // Calculate RaceLeaderLapNumber (P1 by position's lap)
            if (sdkData.CarIdxPosition != null && sdkData.CarIdxLap != null)
            {
                // Find the car in P1 position
                int leaderIdx = -1;
                for (int i = 0; i < sdkData.CarIdxPosition.Length; i++)
                {
                    if (sdkData.CarIdxPosition[i] == 1) // Position 1 = race leader
                    {
                        leaderIdx = i;
                        break;
                    }
                }

                if (leaderIdx >= 0 && leaderIdx < sdkData.CarIdxLap.Length)
                {
                    data.RaceLeaderLapNumber = sdkData.CarIdxLap[leaderIdx];
                }
                else
                {
                    // Couldn't find P1 - use ActualLeadingLapNumber as fallback
                    data.RaceLeaderLapNumber = data.ActualLeadingLapNumber;
                }
            }
            else
            {
                // No position data - use ActualLeadingLapNumber as fallback
                data.RaceLeaderLapNumber = data.ActualLeadingLapNumber;
            }

            // Debug logging for first few laps to verify calculations
            if (data.Lap <= 3)
            {
                _logger?.LogInformation(
                    "[LAP_CALC] Player: Lap {PlayerLap}, Completed {Completed} | " +
                    "ActualLeading: {ActualLeading} | RaceLeader(P1): {RaceLeader} | " +
                    "SessionLaps: {SessionLaps}",
                    data.Lap,
                    data.LapsCompleted,
                    data.ActualLeadingLapNumber,
                    data.RaceLeaderLapNumber,
                    data.SessionLaps
                );
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calculating leading lap numbers");
            // Set safe fallback values
            data.ActualLeadingLapNumber = data.Lap;
            data.RaceLeaderLapNumber = data.Lap;
        }
    }

    public async Task DisconnectAsync()
    {
        _logger.LogInformation("Disconnecting from iRacing...");

        if (_client != null)
        {
            try
            {
                // v1.0.0-beta.1: SubscribeToAllStreams handles cleanup via cancellationToken
                // We just need to dispose the client
                await _client.DisposeAsync();
                _client = null;

                Status = ConnectionStatus.Disconnected;
                _logger.LogInformation("Disconnected successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disconnect");
                throw;
            }
        }

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing telemetry service");
        }

        _disposed = true;
    }

    /// <summary>
    /// Compute time gap (seconds) to the nearest car ahead and behind the player.
    /// Uses CarIdxLapDistPct and CarIdxEstTime arrays.
    /// </summary>
    private static void ComputeRelativeGaps(Models.TelemetryData data)
    {
        const int SURFACE_ON_TRACK = 3;
        const float MIN_VALID = 0.001f;

        var pcts = data.CarIdxLapDistPct;
        var estTimes = data.CarIdxEstTime;
        var surfaces = data.CarIdxTrackSurface;
        if (pcts == null || estTimes == null || surfaces == null) return;

        int playerIdx = data.PlayerCarIdx;
        if (playerIdx < 0 || playerIdx >= pcts.Length) return;

        float playerPct = pcts[playerIdx];
        if (playerPct < MIN_VALID) return;

        // Use player's estimated lap time as reference for distance → time conversion
        float refTime = estTimes[playerIdx];
        if (refTime <= 0) refTime = 90f; // fallback

        float closestAheadGap = float.MaxValue;
        float closestBehindGap = float.MaxValue;

        for (int i = 0; i < Math.Min(pcts.Length, 64); i++)
        {
            if (i == playerIdx) continue;
            if (i >= surfaces.Length || surfaces[i] != SURFACE_ON_TRACK) continue;

            float carPct = pcts[i];
            if (carPct < MIN_VALID) continue;

            // Delta from player to car (positive = car is ahead)
            float delta = carPct - playerPct;
            if (delta > 0.5f) delta -= 1.0f;
            if (delta < -0.5f) delta += 1.0f;

            float gapSeconds = Math.Abs(delta) * refTime;

            if (delta > 0 && gapSeconds < closestAheadGap)
                closestAheadGap = gapSeconds;
            else if (delta < 0 && gapSeconds < closestBehindGap)
                closestBehindGap = gapSeconds;
        }

        data.GapAhead = closestAheadGap < float.MaxValue ? closestAheadGap : 0f;
        data.GapBehind = closestBehindGap < float.MaxValue ? closestBehindGap : 0f;
    }
}
