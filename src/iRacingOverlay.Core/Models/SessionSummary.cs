namespace iRacingOverlay.Core.Models;

/// <summary>
/// Aggregated session summary for efficient storage
/// Contains lap-by-lap summaries instead of full 60Hz telemetry
/// Strategy Scouting: ~3.6 KB per 30-lap session (500× reduction from raw data)
/// Setup Engineering: References full telemetry in separate storage
/// </summary>
public class SessionSummary
{
    /// <summary>
    /// Unique session identifier (GUID)
    /// </summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Session start timestamp (UTC)
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Session end timestamp (UTC), null if still active
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Operational mode during this session
    /// </summary>
    public OperationalMode Mode { get; set; }
    
    /// <summary>
    /// Track name (e.g., "Spa-Francorchamps")
    /// </summary>
    public string TrackName { get; set; } = string.Empty;
    
    /// <summary>
    /// Car name (e.g., "BMW M4 GT3")
    /// </summary>
    public string CarName { get; set; } = string.Empty;
    
    /// <summary>
    /// Session type (Practice, Qualify, Race, Test)
    /// </summary>
    public string SessionType { get; set; } = string.Empty;
    
    /// <summary>
    /// Weather conditions at session start (JSON)
    /// Example: {"AirTemp": 25.5, "TrackTemp": 35.2, "Skies": "Clear"}
    /// </summary>
    public string? WeatherData { get; set; }
    
    /// <summary>
    /// Lap summaries (aggregated telemetry per lap)
    /// Setup Engineering: 5-10 laps typically
    /// Strategy Scouting: 20-50 laps
    /// </summary>
    public List<LapSummary> Laps { get; set; } = new();
    
    /// <summary>
    /// Outlier laps flagged for review (incidents, invalid laps, extreme values)
    /// </summary>
    public List<OutlierLap> Outliers { get; set; } = new();
    
    /// <summary>
    /// Session statistics (computed from lap summaries)
    /// </summary>
    public SessionStats Stats { get; set; } = new();
    
    /// <summary>
    /// ML feature vectors for Setup Engineering mode
    /// Extracted features ready for CatBoost/NN inference
    /// </summary>
    public SetupFeatures? SetupFeatures { get; set; }
    
    /// <summary>
    /// ML feature vectors for Strategy Scouting mode
    /// Fuel/tire data for strategy predictions
    /// </summary>
    public StrategyFeatures? StrategyFeatures { get; set; }
    
    /// <summary>
    /// User notes (optional)
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Aggregated telemetry for a single lap
/// Contains 12-15 key values instead of 2000+ raw data points
/// ~100 bytes per lap (vs 200 KB raw)
/// </summary>
public class LapSummary
{
    /// <summary>
    /// Lap number (1-indexed)
    /// </summary>
    public int LapNumber { get; set; }
    
    /// <summary>
    /// Lap time in seconds (null if incomplete)
    /// </summary>
    public float? LapTime { get; set; }
    
    /// <summary>
    /// Sector times (seconds), typically 3 sectors
    /// </summary>
    public float[] SectorTimes { get; set; } = Array.Empty<float>();
    
    /// <summary>
    /// Fuel used this lap (liters)
    /// </summary>
    public float FuelUsed { get; set; }
    
    /// <summary>
    /// Average tire temperatures (°C) - [LF, RF, LR, RR]
    /// </summary>
    public float[] AvgTireTemps { get; set; } = new float[4];
    
    /// <summary>
    /// Tire wear delta this lap (%) - [LF, RF, LR, RR]
    /// Example: [0.5, 0.6, 0.4, 0.5] = 0.5% wear on LF
    /// </summary>
    public float[] TireWearDelta { get; set; } = new float[4];
    
    /// <summary>
    /// Average speed (m/s)
    /// </summary>
    public float AvgSpeed { get; set; }
    
    /// <summary>
    /// Maximum speed (m/s)
    /// </summary>
    public float MaxSpeed { get; set; }
    
    /// <summary>
    /// Incidents this lap (count)
    /// </summary>
    public int Incidents { get; set; }
    
    /// <summary>
    /// Pit stop occurred this lap
    /// </summary>
    public bool PitStop { get; set; }
    
    /// <summary>
    /// Lap valid for timing (no off-tracks, incidents)
    /// </summary>
    public bool IsValid { get; set; } = true;
    
    /// <summary>
    /// Timestamp when lap completed (UTC)
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Outlier lap flagged for review
/// Incidents, invalid laps, extreme fuel consumption, etc.
/// </summary>
public class OutlierLap
{
    /// <summary>
    /// Lap number
    /// </summary>
    public int LapNumber { get; set; }
    
    /// <summary>
    /// Reason for outlier flag
    /// </summary>
    public OutlierReason Reason { get; set; }
    
    /// <summary>
    /// Description (e.g., "Incident count: 2", "Fuel 3.2L (2.5σ above mean)")
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Severity (Low, Medium, High)
    /// </summary>
    public OutlierSeverity Severity { get; set; } = OutlierSeverity.Medium;
}

/// <summary>
/// Reason for outlier classification
/// </summary>
public enum OutlierReason
{
    Incident,           // Collision, off-track, spin
    InvalidLap,         // Failed validation (pit entry/exit, yellow flag)
    ExtremeFuel,        // Fuel consumption > 2σ from mean
    ExtremeLapTime,     // Lap time > 3σ from mean
    MechanicalIssue,    // Damage, tire puncture
    ExtremeWear         // Tire wear > 2σ from mean
}

/// <summary>
/// Outlier severity
/// </summary>
public enum OutlierSeverity
{
    Low,      // Informational only
    Medium,   // Worth reviewing
    High      // Likely invalid data, exclude from analysis
}

/// <summary>
/// Session statistics computed from lap summaries
/// </summary>
public class SessionStats
{
    /// <summary>
    /// Total laps completed
    /// </summary>
    public int TotalLaps { get; set; }
    
    /// <summary>
    /// Valid laps (excludes outliers)
    /// </summary>
    public int ValidLaps { get; set; }
    
    /// <summary>
    /// Best lap time (seconds)
    /// </summary>
    public float? BestLapTime { get; set; }
    
    /// <summary>
    /// Average lap time (seconds, valid laps only)
    /// </summary>
    public float? AvgLapTime { get; set; }
    
    /// <summary>
    /// Average fuel per lap (liters, valid laps only)
    /// </summary>
    public float? AvgFuelPerLap { get; set; }
    
    /// <summary>
    /// Fuel consumption standard deviation
    /// </summary>
    public float? FuelStdDev { get; set; }
    
    /// <summary>
    /// Total incidents
    /// </summary>
    public int TotalIncidents { get; set; }
    
    /// <summary>
    /// Number of pit stops
    /// </summary>
    public int PitStops { get; set; }
}

/// <summary>
/// ML features for Setup Engineering mode
/// Ready for CatBoost/NN inference
/// </summary>
public class SetupFeatures
{
    /// <summary>
    /// Average tire temps across session (4 tires × LMR = 12 values)
    /// </summary>
    public float[] AvgTireTemps { get; set; } = new float[12];
    
    /// <summary>
    /// Average tire pressures (4 tires)
    /// </summary>
    public float[] AvgTirePressures { get; set; } = new float[4];
    
    /// <summary>
    /// Average shock deflection (4 corners)
    /// </summary>
    public float[] AvgShockDeflection { get; set; } = new float[4];
    
    /// <summary>
    /// Average roll/pitch/yaw angles (3 axes)
    /// </summary>
    public float[] AvgAttitude { get; set; } = new float[3];
    
    /// <summary>
    /// Average G-forces (Long/Lat/Vert)
    /// </summary>
    public float[] AvgGForces { get; set; } = new float[3];
    
    /// <summary>
    /// Lap time consistency (standard deviation)
    /// </summary>
    public float LapTimeStdDev { get; set; }
}

/// <summary>
/// ML features for Strategy Scouting mode
/// Fuel/tire data for strategy predictions
/// </summary>
public class StrategyFeatures
{
    /// <summary>
    /// Fuel consumption profile (push pace)
    /// </summary>
    public float FuelPerLap_Push { get; set; }
    
    /// <summary>
    /// Fuel consumption profile (fuel-save pace)
    /// </summary>
    public float? FuelPerLap_Save { get; set; }
    
    /// <summary>
    /// Tire degradation rate (% per lap)
    /// </summary>
    public float TireDegradationRate { get; set; }
    
    /// <summary>
    /// Tire degradation model coefficients (exponential decay)
    /// grip = a * e^(-b * lap) + c
    /// </summary>
    public float[] TireDegModel { get; set; } = new float[3]; // [a, b, c]
    
    /// <summary>
    /// Average pit stop time (seconds)
    /// </summary>
    public float? AvgPitStopTime { get; set; }
    
    /// <summary>
    /// Track temperature (°C)
    /// </summary>
    public float TrackTemp { get; set; }
}
