namespace iRacingOverlay.Core.Models;

/// <summary>
/// Track turn database root structure
/// </summary>
public class TrackTurnDatabase
{
    public string Version { get; set; } = "1.0";
    public string LastUpdated { get; set; } = string.Empty;
    public Dictionary<string, TrackTurnData> Tracks { get; set; } = new();
}

/// <summary>
/// Turn data for a specific track
/// </summary>
public class TrackTurnData
{
    public string DisplayName { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int TotalTurns { get; set; }
    public float TrackLength { get; set; } // in kilometers
    public List<TurnDefinition> Turns { get; set; } = new();
}

/// <summary>
/// Definition of a single turn on a track
/// </summary>
public class TurnDefinition
{
    /// <summary>
    /// Turn number (1-based)
    /// </summary>
    public int Number { get; set; }
    
    /// <summary>
    /// Turn name (e.g., "Eau Rouge", "Casino Square", "Turn 1")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// LapDistPct where turn starts (0.0 - 1.0)
    /// </summary>
    public float StartPct { get; set; }
    
    /// <summary>
    /// LapDistPct where turn ends (0.0 - 1.0)
    /// </summary>
    public float EndPct { get; set; }
    
    /// <summary>
    /// Turn type for UI categorization
    /// </summary>
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Current turn information for display
/// </summary>
public class TurnInfo
{
    /// <summary>
    /// Turn number (1-based), or 0 if not in a turn
    /// </summary>
    public int Number { get; set; }
    
    /// <summary>
    /// Turn name, or empty string if not in a turn
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether player is currently in a turn
    /// </summary>
    public bool IsInTurn { get; set; }
    
    /// <summary>
    /// Progress through current turn (0.0 - 1.0), or 0 if not in a turn
    /// </summary>
    public float TurnProgress { get; set; }
    
    /// <summary>
    /// Turn type for UI context
    /// </summary>
    public string TurnType { get; set; } = string.Empty;
    
    /// <summary>
    /// Empty turn info (not in any turn)
    /// </summary>
    public static TurnInfo None => new() { Number = 0, Name = string.Empty, IsInTurn = false };
}
