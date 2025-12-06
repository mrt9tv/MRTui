using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services.SetupEngineering;

/// <summary>
/// Driving behavior features extracted from telemetry
/// All features automatically detected - NO user input required!
/// 
/// Used as input to Neural Network for setup recommendations
/// </summary>
public class DrivingBehaviorFeatures
{
    // ===== HANDLING CHARACTERISTICS (auto-detected) =====
    
    /// <summary>
    /// Oversteer severity (0-10 scale)
    /// Detected from: steering corrections + lateral G + yaw rate
    /// </summary>
    public float OversteerSeverity { get; set; }
    
    /// <summary>
    /// Understeer severity (0-10 scale)
    /// Detected from: throttle position + speed gain + steering angle
    /// </summary>
    public float UndersteerSeverity { get; set; }
    
    /// <summary>
    /// Brake stability score (0-10 scale)
    /// Lower = more lockups detected
    /// </summary>
    public float BrakeStability { get; set; }
    
    /// <summary>
    /// Corner entry instability (0-10 scale)
    /// Measured by steering angle variance on turn-in
    /// </summary>
    public float CornerEntryInstability { get; set; }
    
    /// <summary>
    /// Corner exit traction score (0-10 scale)
    /// Measured by throttle application smoothness
    /// </summary>
    public float CornerExitTraction { get; set; }
    
    // ===== TIRE DEGRADATION PATTERNS =====
    
    /// <summary>
    /// Front tire degradation rate (°C increase per lap)
    /// Average of LF and RF tire temps
    /// </summary>
    public float FrontTireDegRate { get; set; }
    
    /// <summary>
    /// Rear tire degradation rate (°C increase per lap)
    /// Average of LR and RR tire temps
    /// </summary>
    public float RearTireDegRate { get; set; }
    
    /// <summary>
    /// Tire temperature imbalance (°C delta between left and right)
    /// Positive = left hotter, negative = right hotter
    /// </summary>
    public float TireImbalance { get; set; }
    
    // ===== DRIVER INPUT QUALITY =====
    
    /// <summary>
    /// Throttle smoothness score (0-100)
    /// Measures gradual vs jerky throttle application
    /// </summary>
    public float ThrottleSmoothnessScore { get; set; }
    
    /// <summary>
    /// Brake modulation score (0-100)
    /// Measures smooth brake release vs on/off braking
    /// </summary>
    public float BrakeModulationScore { get; set; }
    
    /// <summary>
    /// Steering consistency score (0-100)
    /// Measures repeatability of steering inputs lap-to-lap
    /// </summary>
    public float SteeringConsistency { get; set; }
    
    // ===== CORNER-SPECIFIC ISSUES =====
    
    /// <summary>
    /// Per-corner analysis results
    /// Key = corner number (1, 2, 3, etc.)
    /// Value = handling analysis for that corner
    /// </summary>
    public Dictionary<int, CornerAnalysis> CornerProblems { get; set; } = new();
    
    // ===== LAP STATISTICS =====
    
    /// <summary>
    /// Number of laps analyzed
    /// </summary>
    public int LapsAnalyzed { get; set; }
    
    /// <summary>
    /// Average lap time (seconds)
    /// </summary>
    public float AverageLapTime { get; set; }
    
    /// <summary>
    /// Lap time consistency (standard deviation)
    /// Lower = more consistent
    /// </summary>
    public float LapTimeConsistency { get; set; }
    
    /// <summary>
    /// Total lockups detected across all laps
    /// </summary>
    public int TotalLockups { get; set; }
    
    /// <summary>
    /// Average oversteer severity across all corners
    /// </summary>
    public float AverageOversteerSeverity { get; set; }
    
    /// <summary>
    /// Average understeer severity across all corners
    /// </summary>
    public float AverageUndersteerSeverity { get; set; }
    
    // ===== SUMMARY =====
    
    /// <summary>
    /// Primary handling issue ("Oversteer", "Understeer", "Brake Instability", "Balanced")
    /// </summary>
    public string PrimaryIssue { get; set; } = "Unknown";
    
    /// <summary>
    /// Severity of primary issue (0-10)
    /// </summary>
    public float PrimaryIssueSeverity { get; set; }
    
    /// <summary>
    /// Human-readable summary of driving behavior
    /// </summary>
    public string Summary { get; set; } = "";
    
    /// <summary>
    /// Timestamp when analysis was performed
    /// </summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Builder for DrivingBehaviorFeatures
/// Analyzes multiple laps and generates comprehensive behavior profile
/// </summary>
public class DrivingBehaviorAnalyzer
{
    private readonly HandlingDetector _handlingDetector;
    private readonly CornerSegmenter _cornerSegmenter;
    
    public DrivingBehaviorAnalyzer()
    {
        _handlingDetector = new HandlingDetector();
        _cornerSegmenter = new CornerSegmenter();
    }
    
    /// <summary>
    /// Analyze multiple laps and generate driving behavior profile
    /// Requires 5-10 laps for accurate analysis
    /// </summary>
    public DrivingBehaviorFeatures Analyze(List<List<TelemetryData>> laps)
    {
        var features = new DrivingBehaviorFeatures
        {
            LapsAnalyzed = laps.Count
        };
        
        if (laps.Count == 0)
            return features;
        
        // Analyze each lap
        var allCornerAnalyses = new List<CornerAnalysis>();
        var lapTimes = new List<float>();
        int totalLockups = 0;
        
        foreach (var lapData in laps)
        {
            if (lapData.Count == 0)
                continue;
            
            // Calculate lap time
            var lapTime = (float)(lapData.Last().Timestamp - lapData.First().Timestamp).TotalSeconds;
            lapTimes.Add(lapTime);
            
            // Segment corners and analyze handling
            var corners = _cornerSegmenter.SegmentCorners(lapData);
            var cornerAnalyses = _cornerSegmenter.AnalyzeCornerHandling(corners, _handlingDetector);
            allCornerAnalyses.AddRange(cornerAnalyses);
            
            // Detect lockups across entire lap
            var lockupResult = _handlingDetector.DetectLockups(lapData);
            totalLockups += lockupResult.LockupCount;
        }
        
        // Calculate aggregate statistics
        if (lapTimes.Count > 0)
        {
            features.AverageLapTime = lapTimes.Average();
            features.LapTimeConsistency = CalculateStandardDeviation(lapTimes);
        }
        
        features.TotalLockups = totalLockups;
        features.BrakeStability = CalculateBrakeStability(totalLockups, laps.Count);
        
        // Aggregate corner-specific issues
        if (allCornerAnalyses.Count > 0)
        {
            // Group by corner number and average severity
            var cornerGroups = allCornerAnalyses.GroupBy(c => c.CornerNumber);
            foreach (var group in cornerGroups)
            {
                var avgAnalysis = new CornerAnalysis
                {
                    CornerNumber = group.Key,
                    IssueType = group.First().IssueType, // Use most common
                    Severity = group.Average(c => c.Severity),
                    OversteerSeverity = group.Average(c => c.OversteerSeverity),
                    UndersteerSeverity = group.Average(c => c.UndersteerSeverity),
                    TimeLost = group.Average(c => c.TimeLost)
                };
                
                features.CornerProblems[group.Key] = avgAnalysis;
            }
            
            // Overall handling characteristics
            features.AverageOversteerSeverity = allCornerAnalyses.Average(c => c.OversteerSeverity);
            features.AverageUndersteerSeverity = allCornerAnalyses.Average(c => c.UndersteerSeverity);
            features.OversteerSeverity = features.AverageOversteerSeverity;
            features.UndersteerSeverity = features.AverageUndersteerSeverity;
        }
        
        // Determine primary issue
        if (features.OversteerSeverity > features.UndersteerSeverity && features.OversteerSeverity >= 3f)
        {
            features.PrimaryIssue = "Oversteer";
            features.PrimaryIssueSeverity = features.OversteerSeverity;
        }
        else if (features.UndersteerSeverity > features.OversteerSeverity && features.UndersteerSeverity >= 3f)
        {
            features.PrimaryIssue = "Understeer";
            features.PrimaryIssueSeverity = features.UndersteerSeverity;
        }
        else if (features.BrakeStability < 7f)
        {
            features.PrimaryIssue = "Brake Instability";
            features.PrimaryIssueSeverity = 10f - features.BrakeStability;
        }
        else
        {
            features.PrimaryIssue = "Balanced";
            features.PrimaryIssueSeverity = 0f;
        }
        
        // Generate summary
        features.Summary = GenerateSummary(features);
        
        return features;
    }
    
    private float CalculateStandardDeviation(List<float> values)
    {
        if (values.Count == 0)
            return 0f;
        
        var avg = values.Average();
        var sumOfSquares = values.Sum(v => (v - avg) * (v - avg));
        return (float)Math.Sqrt(sumOfSquares / values.Count);
    }
    
    private float CalculateBrakeStability(int lockupCount, int lapCount)
    {
        if (lapCount == 0)
            return 10f;
        
        // 0 lockups = 10/10 stability
        // 1 lockup per lap = 5/10 stability
        // 2+ lockups per lap = 0/10 stability
        var lockupsPerLap = (float)lockupCount / lapCount;
        var stability = 10f - (lockupsPerLap * 5f);
        return Math.Clamp(stability, 0f, 10f);
    }
    
    private string GenerateSummary(DrivingBehaviorFeatures features)
    {
        var summary = $"Analyzed {features.LapsAnalyzed} laps. ";
        
        if (features.PrimaryIssue == "Oversteer")
        {
            summary += $"Oversteer detected (severity {features.PrimaryIssueSeverity:F1}/10). ";
            var worstCorner = features.CornerProblems
                .Where(kv => kv.Value.OversteerSeverity >= 5f)
                .OrderByDescending(kv => kv.Value.OversteerSeverity)
                .FirstOrDefault();
            
            if (worstCorner.Value != null)
                summary += $"Worst in Turn {worstCorner.Key} (severity {worstCorner.Value.OversteerSeverity:F1}/10). ";
        }
        else if (features.PrimaryIssue == "Understeer")
        {
            summary += $"Understeer detected (severity {features.PrimaryIssueSeverity:F1}/10). ";
            var worstCorner = features.CornerProblems
                .Where(kv => kv.Value.UndersteerSeverity >= 5f)
                .OrderByDescending(kv => kv.Value.UndersteerSeverity)
                .FirstOrDefault();
            
            if (worstCorner.Value != null)
                summary += $"Worst in Turn {worstCorner.Key} (severity {worstCorner.Value.UndersteerSeverity:F1}/10). ";
        }
        else if (features.PrimaryIssue == "Brake Instability")
        {
            summary += $"{features.TotalLockups} lockups detected ({features.TotalLockups / features.LapsAnalyzed:F1} per lap). ";
        }
        else
        {
            summary += "Car handling balanced. ";
        }
        
        return summary;
    }
}
