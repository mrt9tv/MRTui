namespace iRacingOverlay.Core.Models;

/// <summary>
/// Per-lap fuel consumption tracking record
/// Used to calculate averages and analyze fuel usage patterns
/// </summary>
public class FuelLapHistory
{
    /// <summary>Lap number in session</summary>
    public int LapNumber { get; set; }
    
    /// <summary>Fuel consumed during this lap (liters)</summary>
    public float FuelUsed { get; set; }
    
    /// <summary>Lap time in seconds</summary>
    public float LapTime { get; set; }
    
    /// <summary>Fuel level at lap start (liters)</summary>
    public float FuelAtStart { get; set; }
    
    /// <summary>Fuel level at lap end (liters)</summary>
    public float FuelAtEnd { get; set; }
    
    /// <summary>Flag status during lap (green, yellow, etc.)</summary>
    public LapFlagStatus FlagStatus { get; set; }
    
    /// <summary>Pit stop occurred during this lap</summary>
    public bool WasPitLap { get; set; }
    
    /// <summary>Refuel amount if pit stop occurred (liters)</summary>
    public float RefuelAmount { get; set; }
    
    /// <summary>Formation/warmup lap (exclude from averages)</summary>
    public bool IsFormationLap { get; set; }

    /// <summary>Out-lap after pit stop (first lap after leaving pits)</summary>
    public bool IsOutLap { get; set; }

    /// <summary>Incomplete lap (don't use for calculations)</summary>
    public bool IsIncompleteLap { get; set; }
    
    /// <summary>Lap completion timestamp</summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>Incident count at lap start (for detecting new incidents during lap)</summary>
    public int IncidentCountAtStart { get; set; }
    
    /// <summary>Incident count at lap end (for detecting new incidents during lap)</summary>
    public int IncidentCountAtEnd { get; set; }
    
    /// <summary>Whether this lap had incidents (contact, off-track, etc.)</summary>
    public bool HadIncident => IncidentCountAtEnd > IncidentCountAtStart;
    
    /// <summary>Number of incidents during this lap</summary>
    public int IncidentsDuringLap => Math.Max(0, IncidentCountAtEnd - IncidentCountAtStart);
    
    /// <summary>Whether this lap is a statistical outlier (flagged by outlier detection)</summary>
    public bool IsFlaggedAsOutlier { get; set; }
    
    /// <summary>Reason why lap was flagged as outlier (for debugging)</summary>
    public string? OutlierReason { get; set; }
    
    /// <summary>Session state during lap (0=Invalid, 1=GetInCar, 2=Warmup, 3=ParadeLaps, 4=Racing, 5=Checkered, 6=CoolDown)</summary>
    public int SessionState { get; set; }
    
    /// <summary>Whether this lap is a pace/parade lap (SessionState == 3)</summary>
    public bool IsPaceLap => SessionState == 3;
    
    /// <summary>Whether this lap should be included in average calculations</summary>
    /// <remarks>Out-laps are excluded because they typically use less fuel (cool tires, careful driving)</remarks>
    public bool IsValidForAveraging => !WasPitLap && !IsFormationLap && !IsIncompleteLap && !IsPaceLap && !IsOutLap && FuelUsed > 0;
    
    /// <summary>Whether this lap is a green flag lap for green-only averaging</summary>
    public bool IsGreenFlagLap => FlagStatus == LapFlagStatus.Green && IsValidForAveraging;
    
    /// <summary>Whether this lap is a yellow flag lap for yellow-only averaging</summary>
    public bool IsYellowFlagLap => FlagStatus == LapFlagStatus.Yellow && IsValidForAveraging;
    
    /// <summary>Whether this lap is valid for pace lap fuel averaging (separate from race laps)</summary>
    public bool IsValidPaceLap => IsPaceLap && !WasPitLap && !IsIncompleteLap && FuelUsed > 0;
}

/// <summary>
/// Flag status during a lap for separate tracking
/// </summary>
public enum LapFlagStatus
{
    /// <summary>Green flag - normal racing</summary>
    Green = 1,
    
    /// <summary>Yellow flag - caution/safety car (full course)</summary>
    Yellow = 2,
    
    /// <summary>Red flag - session stopped</summary>
    Red = 3,
    
    /// <summary>White flag - final lap</summary>
    White = 4,
    
    /// <summary>Checkered flag - session ended</summary>
    Checkered = 5,
    
    // Enhanced flags for better pit strategy (Phase 5 Fix)
    
    /// <summary>One lap to green - CRITICAL for pit timing! Pace lap before restart</summary>
    OneLapToGreen = 10,
    
    /// <summary>Green flag held at start line - restart imminent</summary>
    GreenHeld = 11,
    
    /// <summary>Local yellow flag waving (not full course caution)</summary>
    YellowWaving = 12,
    
    /// <summary>Debris on track</summary>
    Debris = 13,
    
    /// <summary>Blue flag - being lapped</summary>
    Blue = 14,
    
    // Race control informational flags
    
    /// <summary>Ten laps to go</summary>
    TenToGo = 20,
    
    /// <summary>Five laps to go</summary>
    FiveToGo = 21,
    
    // Start sequence flags
    
    /// <summary>Start lights: Ready (red lights on)</summary>
    StartReady = 30,
    
    /// <summary>Start lights: Set (all red lights on)</summary>
    StartSet = 31,
    
    /// <summary>Start lights: Go! (lights out)</summary>
    StartGo = 32,
    
    /// <summary>Unknown or mixed flags</summary>
    Unknown = 0
}

