using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Service for detecting outliers in fuel consumption data
/// Uses Median Absolute Deviation (MAD) and lap time correlation
/// Phase 2: Extracted from FuelCalculatorService
/// </summary>
public class FuelOutlierDetector
{
    // MAD threshold: 3.5 = conservative (only extreme outliers)
    // 3.0 = moderate, 2.5 = strict
    private const float MAD_THRESHOLD = 3.5f;
    
    // Lap time correlation threshold: 15% deviation from median
    // Allows for fuel saving (5-10% slower) but flags major incidents (>15% slower)
    private const float LAP_TIME_THRESHOLD = 0.15f;
    
    // IQR fallback thresholds
    private const float IQR_MULTIPLIER = 1.5f;
    private const float MEDIAN_FILTER_MULTIPLIER = 1.5f;
    
    /// <summary>
    /// Detect outliers using multiple methods and flag suspicious laps
    /// Considers: MAD statistical outliers, lap time correlation, and incidents
    /// NOTE: Incidents and off-track are part of racing - we flag but don't auto-exclude
    /// </summary>
    /// <param name="laps">List of laps to analyze</param>
    /// <returns>Outlier analysis with flagged laps and statistics</returns>
    public OutlierAnalysis DetectOutliers(List<FuelLapHistory> laps)
    {
        var analysis = new OutlierAnalysis { AnalyzedLaps = laps };
        
        if (laps.Count < 3)
        {
            // Need at least 3 laps for meaningful outlier detection
            return analysis;
        }
        
        // Extract fuel values and lap times for analysis
        var fuelValues = laps.Select(l => l.FuelUsed).ToList();
        var lapTimes = laps.Where(l => l.LapTime > 0).Select(l => l.LapTime).ToList();
        
        // Calculate MAD for fuel consumption
        float fuelMedian = fuelValues.OrderBy(f => f).ToList()[fuelValues.Count / 2];
        float fuelMAD = CalculateMAD(fuelValues);
        
        analysis.FuelMedian = fuelMedian;
        analysis.FuelMAD = fuelMAD;
        
        // Calculate MAD for lap times (if available)
        float lapTimeMedian = 0f;
        float lapTimeMAD = 0f;
        if (lapTimes.Count >= 3)
        {
            lapTimeMedian = lapTimes.OrderBy(t => t).ToList()[lapTimes.Count / 2];
            lapTimeMAD = CalculateMAD(lapTimes);
        }
        
        analysis.LapTimeMedian = lapTimeMedian;
        analysis.LapTimeMAD = lapTimeMAD;
        
        // Analyze each lap for outliers
        foreach (var lap in laps)
        {
            List<string> reasons = new();
            
            // Check MAD statistical outlier (fuel consumption)
            if (fuelMAD > 0.001f) // Avoid division by zero
            {
                float fuelDeviation = Math.Abs(lap.FuelUsed - fuelMedian) / fuelMAD;
                if (fuelDeviation > MAD_THRESHOLD)
                {
                    reasons.Add($"Fuel MAD={fuelDeviation:F1} (>{MAD_THRESHOLD})");
                }
            }
            
            // Check lap time correlation (if lap time available)
            if (lap.LapTime > 0 && lapTimeMAD > 0.001f && lapTimeMedian > 0)
            {
                float lapTimeDeviation = (lap.LapTime - lapTimeMedian) / lapTimeMedian;
                if (lapTimeDeviation > LAP_TIME_THRESHOLD)
                {
                    reasons.Add($"Lap time +{lapTimeDeviation * 100:F0}% slower");
                }
            }
            
            // Note incidents but don't auto-flag (they're part of racing)
            // Just add to reason string for transparency
            if (lap.HadIncident)
            {
                reasons.Add($"{lap.IncidentsDuringLap}x incident(s)");
                // Don't set IsFlaggedAsOutlier - incidents alone don't make it invalid
                // Only flag if ALSO statistically abnormal
            }
            
            // Set outlier flag and reason
            if (reasons.Count > 0)
            {
                // Only flag as outlier if there's a statistical reason (MAD or lap time)
                // Incidents alone are not enough (they're normal in racing)
                bool hasStatisticalReason = reasons.Any(r => r.Contains("MAD") || r.Contains("Lap time"));
                
                if (hasStatisticalReason)
                {
                    lap.IsFlaggedAsOutlier = true;
                    lap.OutlierReason = string.Join(", ", reasons);
                    analysis.OutlierCount++;
                }
                else
                {
                    // Just incidents, not a statistical outlier
                    lap.IsFlaggedAsOutlier = false;
                    lap.OutlierReason = string.Join(", ", reasons) + " (not flagged)";
                }
            }
        }
        
        return analysis;
    }
    
    /// <summary>
    /// Apply IQR (Interquartile Range) fallback filtering
    /// Used when MAD filtering is too aggressive (removes >40% of laps)
    /// </summary>
    /// <param name="laps">List of laps to filter</param>
    /// <returns>Laps within IQR bounds</returns>
    public List<FuelLapHistory> ApplyIQRFallback(List<FuelLapHistory> laps)
    {
        if (laps.Count < 3)
            return laps;
        
        var sortedFuel = laps.Select(l => l.FuelUsed).OrderBy(f => f).ToList();
        int q1Index = sortedFuel.Count / 4;
        int q3Index = (sortedFuel.Count * 3) / 4;
        float q1 = sortedFuel[q1Index];
        float q3 = sortedFuel[q3Index];
        float iqr = q3 - q1;
        
        // Outlier thresholds: Q1 - 1.5*IQR to Q3 + 1.5*IQR (standard statistical method)
        float lowerBound = Math.Max(0f, q1 - (IQR_MULTIPLIER * iqr)); // Fuel can't be negative
        float upperBound = q3 + (IQR_MULTIPLIER * iqr);
        
        return laps.Where(l => l.FuelUsed >= lowerBound && l.FuelUsed <= upperBound).ToList();
    }
    
    /// <summary>
    /// Apply median filter as last resort
    /// Used when both MAD and IQR are too aggressive
    /// </summary>
    /// <param name="laps">List of laps to filter</param>
    /// <returns>Laps within median bounds</returns>
    public List<FuelLapHistory> ApplyMedianFilter(List<FuelLapHistory> laps)
    {
        if (laps.Count == 0)
            return laps;
        
        var sortedFuel = laps.Select(l => l.FuelUsed).OrderBy(f => f).ToList();
        float median = sortedFuel[sortedFuel.Count / 2];
        
        return laps.Where(l => l.FuelUsed <= median * MEDIAN_FILTER_MULTIPLIER).ToList();
    }
    
    /// <summary>
    /// Calculate Median Absolute Deviation (MAD) for robust outlier detection
    /// MAD is more robust than standard deviation for small datasets with outliers
    /// </summary>
    /// <param name="values">List of values to analyze</param>
    /// <returns>MAD value (median of absolute deviations from median)</returns>
    public float CalculateMAD(List<float> values)
    {
        if (values.Count == 0)
            return 0f;
            
        // Calculate median
        var sorted = values.OrderBy(v => v).ToList();
        float median = sorted[sorted.Count / 2];
        
        // Calculate absolute deviations from median
        var deviations = values.Select(v => Math.Abs(v - median)).OrderBy(d => d).ToList();
        
        // Return median of deviations
        return deviations[deviations.Count / 2];
    }
}
