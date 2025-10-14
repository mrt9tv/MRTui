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
    "SessionNum"       // Current session number
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
            
            // The SDK client should have a GetSessionInfoString or similar method
            // Try to access it via reflection if not directly available
            var clientType = _client.GetType();
            _logger.LogDebug("Client type: {ClientType}", clientType.FullName);
            
            // List all available methods and properties for debugging
            var methods = clientType.GetMethods().Select(m => m.Name).ToList();
            var properties = clientType.GetProperties().Select(p => p.Name).ToList();
            _logger.LogDebug("Available methods: {Methods}", string.Join(", ", methods));
            _logger.LogDebug("Available properties: {Properties}", string.Join(", ", properties));
            
            var getSessionInfoMethod = clientType.GetMethod("GetSessionInfoString") 
                                    ?? clientType.GetMethod("GetSessionInfo")
                                    ?? clientType.GetProperty("SessionInfo")?.GetMethod;
            
            if (getSessionInfoMethod != null)
            {
                _logger.LogDebug("Found SessionInfo method: {MethodName}", getSessionInfoMethod.Name);
                var sessionInfo = getSessionInfoMethod.Invoke(_client, null) as string;
                if (!string.IsNullOrEmpty(sessionInfo))
                {
                    _logger.LogDebug("SessionInfo YAML length: {Length} characters", sessionInfo.Length);
                    _logger.LogDebug("First 200 chars of YAML: {Preview}", sessionInfo.Substring(0, Math.Min(200, sessionInfo.Length)));
                    ParseSessionInfo(sessionInfo);
                }
                else
                {
                    _logger.LogWarning("SessionInfo is empty or null");
                }
            }
            else
            {
                _logger.LogWarning("Could not find SessionInfo method on telemetry client. Session info will not be available.");
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
            if (!_sessionInfoParsed)
            {
                TryParseSessionInfo();
                _sessionInfoParsed = true; // Only try once to avoid repeated reflection calls
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
                TrackName = _trackName
            };

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
