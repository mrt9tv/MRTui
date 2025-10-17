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
    /// Track length in meters (parsed from YAML SessionInfo)
    /// </summary>
    public float TrackLength { get; set; }
    
    // ===== PHASE 1: 4-Way Proximity Radar =====
    
    /// <summary>
    /// Lateral spotter enum: 0=Clear, 1=CarLeft, 2=CarRight, 3=CarBothSides
    /// This is the same data the in-game spotter uses for left/right warnings.
    /// </summary>
    public int CarLeftRight { get; set; }
    
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
    /// Player heading angle in radians (yaw around Z-axis)
    /// </summary>
    public float Yaw { get; set; }
    
    /// <summary>
    /// Rate of heading change in radians per second
    /// </summary>
    public float YawRate { get; set; }

    /// <summary>
    /// Speed in km/h (calculated from m/s)
    /// </summary>
    public float SpeedKmh => Speed * 3.6f;

    /// <summary>
    /// Speed in mph (calculated from m/s)
    /// </summary>
    public float SpeedMph => Speed * 2.23694f;
}
