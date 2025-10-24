namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Delta tracking and convergence analysis results
/// Tracks prediction accuracy and confidence over time
/// </summary>
public class DeltaData
{
    // Current delta
    public float CurrentDelta { get; set; }
    public float DeltaPercentage { get; set; }
    
    // Convergence analysis
    public string ConvergenceTrend { get; set; } = "";
    public float ConvergenceRate { get; set; }
    
    // Confidence scoring
    public float ConfidenceScore { get; set; }
    public string ConfidenceLevel { get; set; } = "";
    
    // Suspicion detection
    public bool DeltaSuspicious { get; set; }
    public string? SuspicionReason { get; set; }
    
    // Historical accuracy
    public int TotalPredictions { get; set; }
    public float OurMethodAccuracy { get; set; }
    public float IRacingMethodAccuracy { get; set; }
    public string? AccuracyComparison { get; set; }
}
