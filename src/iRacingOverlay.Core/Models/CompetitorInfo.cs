namespace iRacingOverlay.Core.Models;

/// <summary>
/// Represents competitor information for intelligence tracking
/// </summary>
public class CompetitorInfo
{
    /// <summary>
    /// Car index (0-63)
    /// </summary>
    public int CarIdx { get; set; }
    
    /// <summary>
    /// Driver name
    /// </summary>
    public string DriverName { get; set; } = string.Empty;
    
    /// <summary>
    /// Car number
    /// </summary>
    public string CarNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Current race position (1st, 2nd, etc.)
    /// </summary>
    public int Position { get; set; }
    
    /// <summary>
    /// Current lap number
    /// </summary>
    public int Lap { get; set; }
    
    /// <summary>
    /// Time behind leader (seconds)
    /// </summary>
    public float TimeToLeader { get; set; }
    
    /// <summary>
    /// Last lap time (seconds)
    /// </summary>
    public float LastLapTime { get; set; }
    
    /// <summary>
    /// Best lap time (seconds)
    /// </summary>
    public float BestLapTime { get; set; }
    
    /// <summary>
    /// Is car currently on pit road?
    /// </summary>
    public bool IsOnPitRoad { get; set; }
    
    /// <summary>
    /// Track position percentage (0.0-1.0)
    /// </summary>
    public float LapDistPct { get; set; }
    
    /// <summary>
    /// Car class ID (for filtering by class)
    /// </summary>
    public int CarClass { get; set; }
}

/// <summary>
/// Competitor pit activity information
/// </summary>
public class CompetitorPitActivity
{
    /// <summary>
    /// Car number
    /// </summary>
    public string CarNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Driver name
    /// </summary>
    public string DriverName { get; set; } = string.Empty;
    
    /// <summary>
    /// Lap when pit occurred
    /// </summary>
    public int PitLap { get; set; }
    
    /// <summary>
    /// Current position
    /// </summary>
    public int Position { get; set; }
    
    /// <summary>
    /// Activity status (e.g., "PITTING NOW", "PIT EXIT")
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
