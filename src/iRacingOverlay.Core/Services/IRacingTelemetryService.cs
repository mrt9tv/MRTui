using iRacingOverlay.Core.Models;
using Microsoft.Extensions.Logging;
using SVappsLAB.iRacingTelemetrySDK;
using System.Linq;

namespace iRacingOverlay.Core.Services;

// Define the telemetry variables we want - this triggers the SDK's code generator
// to create a TelemetryData struct with these properties at compile time
[RequiredTelemetryVars([
    // MVP 1 - Core telemetry
    "Speed",           // m/s
    "RPM",             // Engine RPM
    "Gear",            // Current gear (-1=R, 0=N, 1+=gears)
    "Throttle",        // 0-1
    "Brake",           // 0-1
    "Clutch",          // 0-1
    "SteeringWheelAngle", // radians
    "Lap",             // Current lap number
    "LapDistPct",      // 0-1 percentage around track
    "PlayerCarClassPosition", // Position in class
    "PlayerCarIdx",    // Player's car index (0-63)
    "PlayerCarClass",  // Player's car class ID
    
    // MVP 2 - Critical race data
    "FuelLevel",       // liters
    "FuelLevelPct",    // 0-1 percentage
    "WaterTemp",       // celsius
    "OilTemp",         // celsius
    "LapLastLapTime",  // seconds
    "LapBestLapTime",  // seconds
    "SessionTimeRemain", // seconds
    
    // MVP 3+ - Advanced telemetry
    "LFtempCL",        // Left Front tire temp - center left
    "LFtempCM",        // Left Front tire temp - center middle
    "LFtempCR",        // Left Front tire temp - center right
    "RFtempCL",        // Right Front tire temp
    "RFtempCM",
    "RFtempCR",
    "LRtempCL",        // Left Rear tire temp
    "LRtempCM",
    "LRtempCR",
    "RRtempCL",        // Right Rear tire temp
    "RRtempCM",
    "RRtempCR",
    "LFwearL",         // Tire wear (0-1)
    "LFwearM",
    "LFwearR",
    "RFwearL",
    "RFwearM",
    "RFwearR",
    "LRwearL",
    "LRwearM",
    "LRwearR",
    "RRwearL",
    "RRwearM",
    "RRwearR",
    "LongAccel",       // Longitudinal G-force
    "LatAccel",        // Lateral G-force
    "VertAccel",       // Vertical G-force
    "SessionFlags",    // Race flags (checkered, yellow, etc.)
    "PlayerCarMyIncidentCount", // Incident count
    "LFbrakeLinePress", // Brake line pressure
    "RFbrakeLinePress",
    "LRbrakeLinePress",
    "RRbrakeLinePress",
    
    // Additional telemetry fields
    "AirTemp",         // Air temperature (celsius)
    "TrackTemp",       // Track temperature (celsius)
    "TrackTempCrew",   // Track temp from crew chief (celsius)
    
    // Session info
    "SessionTime",     // Session time elapsed (seconds)
    "SessionNum",      // Current session number
    
    // ===== PHASE 1: 4-Way Proximity Radar =====
    
    // Lateral Spotter (Left/Right Detection)
    // CRITICAL: SDK enum is 0=Off, 1=Clear, 2=CarLeft, 3=CarRight, 4=CarBothSides, 5=TwoCarsLeft, 6=TwoCarsRight
    "CarLeftRight",    // Enum: 0=Off, 1=Clear, 2=Left, 3=Right, 4=Both, 5=TwoLeft, 6=TwoRight - Spotter system
    
    // Multi-Car Position Arrays (CarIdx[64])
    "CarIdxLapDistPct",      // float[64] - Track position % for each car
    "CarIdxOnPitRoad",       // bool[64]  - Pit road status
    "CarIdxTrackSurface",    // int[64]   - Track surface type (enum)
    "CarIdxClass",           // int[64]   - Car class ID
    "CarIdxLap",             // int[64]   - Lap number for each car
    "CarIdxPosition",        // int[64]   - Overall race position
    "CarIdxClassPosition",   // int[64]   - Class position
    
    // Car Performance Indicators
    "CarIdxGear",            // int[64]   - Current gear
    "CarIdxRPM",             // float[64] - Engine RPM
    
    // Timing Arrays
    "CarIdxEstTime",         // float[64] - Estimated time to reach position
    "CarIdxF2Time",          // float[64] - Time behind leader
    "CarIdxLastLapTime",     // float[64] - Last lap time
    
    // Player Orientation (for future enhancements)
    "Yaw",                   // float - Player heading angle (radians)
    "YawRate"                // float - Rate of heading change (rad/s)
])]
public class IRacingTelemetryService : ITelemetryService, IDisposable
{
    private readonly ILogger<IRacingTelemetryService> _logger;
    private ITelemetryClient<SVappsLAB.iRacingTelemetrySDK.TelemetryData>? _client;
    private ConnectionStatus _status = ConnectionStatus.Disconnected;
    private bool _disposed = false;
    
    // Lap time tracking
    private int _lastLap = -1;
    private DateTime _lapStartTime = DateTime.UtcNow;
    private float _sessionBestLapTime = float.MaxValue;
    private float _personalBestLapTime = float.MaxValue;
    
    // Session info caching (populated from SessionInfo YAML)
    private string _driverName = "";
    private string _carNumber = "";
    private string _trackName = "";
    private float _trackLength = 0f;
    private bool _sessionInfoParsed = false;

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

    // Use our Models.TelemetryData for the interface
    public event EventHandler<Models.TelemetryData>? TelemetryUpdated;
    public event EventHandler<ConnectionStatusEventArgs>? StatusChanged;

    public IRacingTelemetryService(ILogger<IRacingTelemetryService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            // Subscribe to SDK events
            _client.OnConnectStateChanged += OnConnectStateChanged;
            _client.OnTelemetryUpdate += OnTelemetryUpdate;
            _client.OnError += OnError;

            // Note: Monitor() blocks until cancelled, so we don't await it here
            // It will be started by the TelemetryWorker background service
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
            await _client.Monitor(cancellationToken);
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

    private void OnConnectStateChanged(object? sender, ConnectStateChangedEventArgs e)
    {
        _logger.LogInformation("iRacing connection state changed: {State}", e.State);

        var newStatus = e.State switch
        {
            ConnectState.Connected => ConnectionStatus.Connected,
            ConnectState.Disconnected => ConnectionStatus.Disconnected,
            _ => ConnectionStatus.Disconnected
        };

        Status = newStatus;
        
        // Parse session info when we connect
        if (newStatus == ConnectionStatus.Connected)
        {
            TryParseSessionInfo();
        }
    }
    
    /// <summary>
    /// Attempt to get and parse session info from the SDK.
    /// This is called when connection state changes to Connected.
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
            
            // The SDK exposes session info via GetRawTelemetrySessionInfoYaml() method
            var clientType = _client.GetType();
            var getSessionInfoMethod = clientType.GetMethod("GetRawTelemetrySessionInfoYaml");
            
            if (getSessionInfoMethod != null)
            {
                var sessionInfo = getSessionInfoMethod.Invoke(_client, null) as string;
                if (!string.IsNullOrEmpty(sessionInfo))
                {
                    _logger.LogDebug("SessionInfo YAML received: {Length} characters", sessionInfo.Length);
                    ParseSessionInfo(sessionInfo);
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
            // Try to parse session info if not yet parsed (might not be available immediately on connect)
            // Keep trying until we successfully get TrackLength, since it's critical for distance calculations
            if (!_sessionInfoParsed || _trackLength <= 0)
            {
                TryParseSessionInfo();
                // Only mark as parsed once we have track length
                if (_trackLength > 0)
                {
                    _sessionInfoParsed = true;
                }
            }
            
            // Detect lap change and reset lap timer
            if (sdkData.Lap != _lastLap)
            {
                _lastLap = sdkData.Lap;
                _lapStartTime = DateTime.UtcNow;
                
                // Update best lap times when lap completes
                if (sdkData.LapLastLapTime > 0 && sdkData.LapLastLapTime < _personalBestLapTime)
                {
                    _personalBestLapTime = sdkData.LapLastLapTime;
                }
                if (sdkData.LapBestLapTime > 0 && sdkData.LapBestLapTime < _sessionBestLapTime)
                {
                    _sessionBestLapTime = sdkData.LapBestLapTime;
                }
            }
            
            // Calculate current lap time
            float currentLapTime = (float)(DateTime.UtcNow - _lapStartTime).TotalSeconds;
            
            // Calculate deltas (negative = current lap is faster)
            float deltaToBest = _personalBestLapTime < float.MaxValue ? currentLapTime - _personalBestLapTime : 0f;
            float deltaToSession = _sessionBestLapTime < float.MaxValue ? currentLapTime - _sessionBestLapTime : 0f;
            
            // Convert SDK TelemetryData to our Models.TelemetryData
            var data = new Models.TelemetryData
            {
                // MVP 1 - Core telemetry
                Speed = sdkData.Speed,
                RPM = sdkData.RPM,
                // TODO: Check SDK for actual redline field (DriverCarRedLine, EngineMaxRPM, ShiftRPM, etc.)
                // For now, leave at 0 and let ShiftPointCalculator learn it
                EngineRedlineRPM = 0, // Will be populated if SDK provides it
                Gear = sdkData.Gear,
                Throttle = sdkData.Throttle,
                Brake = sdkData.Brake,
                Clutch = sdkData.Clutch,
                SteeringWheelAngle = sdkData.SteeringWheelAngle,
                Lap = sdkData.Lap,
                LapDistPct = sdkData.LapDistPct,
                Position = sdkData.PlayerCarClassPosition,
                PlayerCarIdx = sdkData.PlayerCarIdx,
                PlayerCarClass = sdkData.PlayerCarClass,
                Timestamp = DateTime.UtcNow,
                
                // MVP 2 - Critical race data
                FuelLevel = sdkData.FuelLevel,
                FuelLevelPct = sdkData.FuelLevelPct,
                WaterTemp = sdkData.WaterTemp,
                OilTemp = sdkData.OilTemp,
                LapLastLapTime = sdkData.LapLastLapTime,
                LapBestLapTime = sdkData.LapBestLapTime,
                CurrentLapTime = currentLapTime,
                DeltaToBestLap = deltaToBest,
                DeltaToSessionBest = deltaToSession,
                SessionTimeRemain = sdkData.SessionTimeRemain,
                SessionTime = sdkData.SessionTime,
                SessionNum = sdkData.SessionNum,
                
                // MVP 3+ - Advanced telemetry
                LFtempCL = sdkData.LFtempCL,
                LFtempCM = sdkData.LFtempCM,
                LFtempCR = sdkData.LFtempCR,
                RFtempCL = sdkData.RFtempCL,
                RFtempCM = sdkData.RFtempCM,
                RFtempCR = sdkData.RFtempCR,
                LRtempCL = sdkData.LRtempCL,
                LRtempCM = sdkData.LRtempCM,
                LRtempCR = sdkData.LRtempCR,
                RRtempCL = sdkData.RRtempCL,
                RRtempCM = sdkData.RRtempCM,
                RRtempCR = sdkData.RRtempCR,
                
                LFwearL = sdkData.LFwearL,
                LFwearM = sdkData.LFwearM,
                LFwearR = sdkData.LFwearR,
                RFwearL = sdkData.RFwearL,
                RFwearM = sdkData.RFwearM,
                RFwearR = sdkData.RFwearR,
                LRwearL = sdkData.LRwearL,
                LRwearM = sdkData.LRwearM,
                LRwearR = sdkData.LRwearR,
                RRwearL = sdkData.RRwearL,
                RRwearM = sdkData.RRwearM,
                RRwearR = sdkData.RRwearR,
                
                LongAccel = sdkData.LongAccel,
                LatAccel = sdkData.LatAccel,
                VertAccel = sdkData.VertAccel,
                
                SessionFlags = (uint)sdkData.SessionFlags,
                PlayerCarMyIncidentCount = sdkData.PlayerCarMyIncidentCount,
                
                LFbrakeLinePress = sdkData.LFbrakeLinePress,
                RFbrakeLinePress = sdkData.RFbrakeLinePress,
                LRbrakeLinePress = sdkData.LRbrakeLinePress,
                RRbrakeLinePress = sdkData.RRbrakeLinePress,
                
                // Environmental conditions
                AirTemp = sdkData.AirTemp,
                TrackTemp = sdkData.TrackTemp,
                TrackTempCrew = sdkData.TrackTempCrew,
                
                // Session info (populated from SessionInfo YAML parsing)
                DriverName = _driverName,
                CarNumber = _carNumber,
                TrackName = _trackName,
                TrackLength = _trackLength,
                
                // ===== PHASE 1: 4-Way Proximity Radar =====
                
                // Lateral Spotter (Left/Right Detection) - Cast enum to int
                // DEBUG: Log raw SDK value to diagnose false positives
                CarLeftRight = (int)sdkData.CarLeftRight,
                
                // Multi-Car Position Arrays (CarIdx[64])
                CarIdxLapDistPct = sdkData.CarIdxLapDistPct,
                CarIdxOnPitRoad = sdkData.CarIdxOnPitRoad,
                CarIdxTrackSurface = sdkData.CarIdxTrackSurface?.Select(t => (int)t).ToArray(), // Cast enum array
                CarIdxClass = sdkData.CarIdxClass,
                CarIdxLap = sdkData.CarIdxLap,
                CarIdxPosition = sdkData.CarIdxPosition,
                CarIdxClassPosition = sdkData.CarIdxClassPosition,
                CarIdxGear = sdkData.CarIdxGear,
                CarIdxRPM = sdkData.CarIdxRPM,
                CarIdxEstTime = sdkData.CarIdxEstTime,
                CarIdxF2Time = sdkData.CarIdxF2Time,
                CarIdxLastLapTime = sdkData.CarIdxLastLapTime,
                
                // Player Orientation
                Yaw = sdkData.Yaw,
                YawRate = sdkData.YawRate
            };
            
            // DEBUG: Log CarLeftRight changes to diagnose SDK behavior
            LogLateralSpotterData(sdkData, data);
            
            // Log proximity radar data on first few updates for verification
            if (sdkData.Lap <= 2) // Only log first 2 laps to avoid spam
            {
                LogProximityRadarData(sdkData);
            }

            // Fire our telemetry event
            TelemetryUpdated?.Invoke(this, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry update");
        }
    }

    private void OnError(object? sender, ExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "iRacing SDK error: {Message}", e.Exception.Message);
        Status = ConnectionStatus.Error;
        StatusChanged?.Invoke(this, new ConnectionStatusEventArgs(ConnectionStatus.Error, error: e.Exception));
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
            bool isPlayerDriver = false;
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
                    continue;
                }
                
                // Parse based on current section
                if (currentSection == "WeekendInfo")
                {
                    if (trimmed.StartsWith("TrackDisplayName:"))
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
                    else if (trimmed.StartsWith("Drivers:"))
                    {
                        inDriversArray = true;
                    }
                    else if (inDriversArray)
                    {
                        if (trimmed.StartsWith("- CarIdx:"))
                        {
                            if (int.TryParse(ExtractYamlValue(trimmed), out var idx))
                            {
                                currentDriverCarIdx = idx;
                                isPlayerDriver = (currentDriverCarIdx == driverCarIdx);
                            }
                        }
                        else if (trimmed.StartsWith("CarNumber:") && isPlayerDriver)
                        {
                            _carNumber = ExtractYamlValue(trimmed).Trim('"', '\'');
                            _logger.LogInformation("Parsed car number: {CarNumber}", _carNumber);
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
    
    // Track last CarLeftRight value to only log changes
    private int _lastCarLeftRight = -1;
    
    /// <summary>
    /// Log CarLeftRight value changes to diagnose SDK false positives.
    /// Logs to same file as widget (radar_debug.log) for correlation.
    /// </summary>
    private void LogLateralSpotterData(SVappsLAB.iRacingTelemetrySDK.TelemetryData sdkData, Models.TelemetryData data)
    {
        try
        {
            int currentValue = (int)sdkData.CarLeftRight;
            
            // Only log when value changes
            if (currentValue != _lastCarLeftRight)
            {
                _lastCarLeftRight = currentValue;
                
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                    "MRT-UI", 
                    "radar_debug.log");
                
                var log = new System.Text.StringBuilder();
                log.AppendLine($"\n[{DateTime.Now:HH:mm:ss.fff}] ===== SDK CarLeftRight CHANGED =====");
                log.AppendLine($"SDK RAW VALUE: {currentValue} ({GetLateralDescription(currentValue)})");
                log.AppendLine($"Player Speed: {sdkData.Speed:F1} m/s ({sdkData.Speed * 3.6f:F1} km/h)");
                log.AppendLine($"Player Position: Pct={sdkData.LapDistPct:F4}, Lap={sdkData.Lap}");
                
                // Log nearby cars to see if SDK is detecting something we're not
                if (sdkData.CarIdxLapDistPct != null && data.TrackLength > 0)
                {
                    var nearbyCars = new List<string>();
                    for (int i = 0; i < sdkData.CarIdxLapDistPct.Length; i++)
                    {
                        if (i == data.PlayerCarIdx) continue;
                        
                        float pct = sdkData.CarIdxLapDistPct[i];
                        if (pct < 0 || pct > 1) continue;
                        
                        float diff = pct - sdkData.LapDistPct;
                        if (diff < -0.5f) diff += 1.0f;
                        if (diff > 0.5f) diff -= 1.0f;
                        
                        float distMeters = diff * data.TrackLength;
                        float absDist = Math.Abs(distMeters);
                        
                        // Only log cars within 30m
                        if (absDist < 30.0f)
                        {
                            string direction = diff > 0 ? "AHEAD" : "BEHIND";
                            nearbyCars.Add($"   Car#{i}: {direction} {absDist:F1}m @ Pct={pct:F4}");
                        }
                    }
                    
                    if (nearbyCars.Any())
                    {
                        log.AppendLine($"Cars within 30m ({nearbyCars.Count}):");
                        foreach (var car in nearbyCars)
                        {
                            log.AppendLine(car);
                        }
                    }
                    else
                    {
                        log.AppendLine("No cars within 30m of player");
                    }
                }
                
                log.AppendLine($"========================================\n");
                
                System.IO.File.AppendAllText(logPath, log.ToString());
            }
        }
        catch
        {
            // Ignore logging errors
        }
    }
    
    private string GetLateralDescription(int value)
    {
        return value switch
        {
            0 => "Off",               // Spotter system disabled
            1 => "Clear",             // No cars beside (THE KEY VALUE!)
            2 => "CarLeft",           // Car on left
            3 => "CarRight",          // Car on right
            4 => "CarBothSides",      // Cars both sides
            5 => "TwoCarsLeft",       // Two cars on left
            6 => "TwoCarsRight",      // Two cars on right
            _ => $"Unknown({value})"
        };
    }
    
    /// <summary>
    /// Log proximity radar telemetry data for verification during initial laps.
    /// This helps confirm that CarLeftRight enum and CarIdx arrays are working correctly.
    /// </summary>
    private void LogProximityRadarData(SVappsLAB.iRacingTelemetrySDK.TelemetryData sdkData)
    {
        try
        {
            // Log CarLeftRight enum value (lateral spotter) - Cast to int for comparison
            int lateralValue = (int)sdkData.CarLeftRight;
            string lateralStatus = lateralValue switch
            {
                0 => "Clear (no cars beside)",
                1 => "Car on LEFT",
                2 => "Car on RIGHT",
                3 => "Cars on BOTH SIDES",
                _ => $"Unknown ({lateralValue})"
            };
            _logger.LogInformation("🎯 CarLeftRight: {Status}", lateralStatus);
            
            // Count cars on track from CarIdxLapDistPct array
            if (sdkData.CarIdxLapDistPct != null)
            {
                var carsOnTrack = sdkData.CarIdxLapDistPct
                    .Where(pct => pct >= 0 && pct <= 1)
                    .Count();
                _logger.LogInformation("📊 Cars on track: {Count}/64", carsOnTrack);
                
                // Log top 5 closest cars ahead/behind
                var playerPct = sdkData.LapDistPct;
                var playerLap = sdkData.Lap;
                
                var otherCars = sdkData.CarIdxLapDistPct
                    .Select((pct, idx) => new { Idx = idx, Pct = pct })
                    .Where(c => c.Pct >= 0 && c.Pct <= 1) // Valid position
                    .Where(c => c.Idx != 0) // Not the player (usually idx 0, but check)
                    .Select(c => new
                    {
                        c.Idx,
                        c.Pct,
                        Position = sdkData.CarIdxPosition?[c.Idx] ?? -1,
                        Distance = CalculateRelativeDistance(playerPct, c.Pct)
                    })
                    .OrderBy(c => Math.Abs(c.Distance))
                    .Take(5)
                    .ToList();
                
                if (otherCars.Any())
                {
                    _logger.LogInformation("🚗 Closest cars:");
                    foreach (var car in otherCars)
                    {
                        string direction = car.Distance > 0 ? "AHEAD" : "BEHIND";
                        _logger.LogInformation("   P{Pos} @ {Pct:F3} ({Dir}, {Dist:F3} track %)", 
                            car.Position, car.Pct, direction, Math.Abs(car.Distance));
                    }
                }
            }
            else
            {
                _logger.LogWarning("⚠️ CarIdxLapDistPct array is NULL");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging proximity radar data");
        }
    }
    
    /// <summary>
    /// Calculate relative distance between player and another car on track.
    /// Positive = car is ahead, Negative = car is behind.
    /// LAP-INDEPENDENT - uses circular track logic (shortest path).
    /// </summary>
    private float CalculateRelativeDistance(float playerPct, float carPct)
    {
        float diff = carPct - playerPct;
        
        // Handle circular track wrap-around - always use shortest path
        // Track is circular: 0% -> 100% -> 0%
        if (diff < -0.5f) diff += 1.0f;  // Car closer going forward
        if (diff > 0.5f) diff -= 1.0f;   // Car closer going backward
        
        return diff; // Positive = ahead, negative = behind
    }

    public async Task DisconnectAsync()
    {
        _logger.LogInformation("Disconnecting from iRacing...");

        if (_client != null)
        {
            try
            {
                // Unsubscribe from events
                _client.OnConnectStateChanged -= OnConnectStateChanged;
                _client.OnTelemetryUpdate -= OnTelemetryUpdate;
                _client.OnError -= OnError;

                // Dispose the client (this will stop monitoring)
                _client.Dispose();
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
}
