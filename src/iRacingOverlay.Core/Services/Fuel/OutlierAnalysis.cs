namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Result of outlier detection analysis
/// Contains detected outliers and statistical metrics
/// </summary>
public class OutlierAnalysis
{
    /// <summary>
    /// Laps with outlier flags set
    /// </summary>
    public List<Models.FuelLapHistory> AnalyzedLaps { get; set; } = new();
    
    /// <summary>
    /// Number of laps flagged as outliers
    /// </summary>
    public int OutlierCount { get; set; }
    
    /// <summary>
    /// Median fuel consumption
    /// </summary>
    public float FuelMedian { get; set; }
    
    /// <summary>
    /// Median Absolute Deviation for fuel consumption
    /// </summary>
    public float FuelMAD { get; set; }
    
    /// <summary>
    /// Median lap time (if available)
    /// </summary>
    public float LapTimeMedian { get; set; }
    
    /// <summary>
    /// Median Absolute Deviation for lap times
    /// </summary>
    public float LapTimeMAD { get; set; }
    
    /// <summary>
    /// Laps after filtering outliers (clean laps for averaging)
    /// </summary>
    public List<Models.FuelLapHistory> CleanLaps => AnalyzedLaps.Where(l => !l.IsFlaggedAsOutlier).ToList();
    
    /// <summary>
    /// Whether analysis has sufficient data
    /// </summary>
    public bool HasSufficientData => AnalyzedLaps.Count >= 3;
}
