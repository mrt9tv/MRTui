namespace iRacingOverlay.Core.Models;

/// <summary>
/// Represents telemetry data from iRacing simulator
/// </summary>
public class TelemetryData
{
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
    /// Current position in race
    /// </summary>
    public int Position { get; set; }

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
    /// Water temperature in Celsius
    /// </summary>
    public float WaterTemp { get; set; }

    /// <summary>
    /// Oil temperature in Celsius
    /// </summary>
    public float OilTemp { get; set; }

    /// <summary>
    /// Last lap time in seconds
    /// </summary>
    public float LapLastLapTime { get; set; }

    /// <summary>
    /// Best lap time in seconds
    /// </summary>
    public float LapBestLapTime { get; set; }
    
    /// <summary>
    /// Current lap time in seconds (calculated)
    /// </summary>
    public float CurrentLapTime { get; set; }
    
    /// <summary>
    /// Delta to personal best lap (negative = faster)
    /// </summary>
    public float DeltaToBestLap { get; set; }
    
    /// <summary>
    /// Delta to session best lap (negative = faster)
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

    public float LFbrakeLinePress { get; set; }
    public float RFbrakeLinePress { get; set; }
    public float LRbrakeLinePress { get; set; }
    public float RRbrakeLinePress { get; set; }

    // Environmental conditions
    /// <summary>
    /// Air temperature in Celsius
    /// </summary>
    public float AirTemp { get; set; }
    
    /// <summary>
    /// Track surface temperature in Celsius
    /// </summary>
    public float TrackTemp { get; set; }
    
    /// <summary>
    /// Track temperature from crew chief in Celsius
    /// </summary>
    public float TrackTempCrew { get; set; }

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
    /// Track name
    /// </summary>
    public string TrackName { get; set; } = string.Empty;

    /// <summary>
    /// Speed in km/h (calculated from m/s)
    /// </summary>
    public float SpeedKmh => Speed * 3.6f;

    /// <summary>
    /// Speed in mph (calculated from m/s)
    /// </summary>
    public float SpeedMph => Speed * 2.23694f;
}
