namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Fuel consumption averages calculated from lap history
/// Contains 9 different averaging methods for different use cases
/// </summary>
public class FuelAverages
{
    /// <summary>
    /// Current lap fuel usage (real-time estimate)
    /// </summary>
    public float Current { get; set; }
    
    /// <summary>
    /// Last completed lap fuel usage
    /// </summary>
    public float Last { get; set; }
    
    /// <summary>
    /// Last 5 laps average (exponentially weighted, most recent lap weighted highest)
    /// </summary>
    public float L5 { get; set; }
    
    /// <summary>
    /// Last 10 laps average (simple average for longer-term trend)
    /// </summary>
    public float L10 { get; set; }
    
    /// <summary>
    /// Session average with outlier filtering (most accurate overall)
    /// </summary>
    public float Session { get; set; }
    
    /// <summary>
    /// Maximum fuel consumption lap (for worst-case planning)
    /// </summary>
    public float Max { get; set; }
    
    /// <summary>
    /// Minimum fuel consumption lap
    /// </summary>
    public float Min { get; set; }
    
    /// <summary>
    /// Exponential Moving Average (EMA) with adaptive alpha based on consistency
    /// </summary>
    public float EMA { get; set; }
    
    /// <summary>
    /// Green flag only average (excludes yellow/caution laps)
    /// </summary>
    public float GreenOnly { get; set; }
    
    /// <summary>
    /// Number of green flag laps used for GreenOnly average
    /// </summary>
    public int GreenFlagLapCount { get; set; }
    
    /// <summary>
    /// Yellow flag average (for caution periods)
    /// </summary>
    public float YellowFlagAverage { get; set; }
    
    /// <summary>
    /// Number of yellow flag laps
    /// </summary>
    public int YellowFlagLapCount { get; set; }
    
    /// <summary>
    /// Stint average (fuel consumption since last pit stop)
    /// </summary>
    public float Stint { get; set; }
    
    /// <summary>
    /// Number of laps in current stint
    /// </summary>
    public int StintLapCount { get; set; }
    
    /// <summary>
    /// Adaptive weighted average (adjusts weighting based on fuel consistency)
    /// </summary>
    public float Adaptive { get; set; }
    
    /// <summary>
    /// Pace lap average (formation/warmup laps)
    /// </summary>
    public float PaceLaps { get; set; }
    
    /// <summary>
    /// Number of pace laps
    /// </summary>
    public int PaceLapCount { get; set; }
    
    /// <summary>
    /// Average lap time (for time-based sessions)
    /// </summary>
    public float AverageLapTime { get; set; }
    
    /// <summary>
    /// Fuel consistency variance (standard deviation for dynamic buffer calculation)
    /// </summary>
    public float FuelConsistencyVariance { get; set; }
    
    /// <summary>
    /// Whether there is sufficient data for reliable averages (at least 2 valid laps)
    /// </summary>
    public bool HasSufficientData { get; set; }
    
    /// <summary>
    /// Warning message if insufficient data
    /// </summary>
    public string? WarningMessage { get; set; }
}
