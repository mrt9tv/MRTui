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
    /// Considers: MAD statistical outliers, lap time correlation, incidents, and trends
    /// NOTE: Incidents and off-track are part of racing - we flag but don't auto-exclude
    /// ENHANCEMENT: Trend-aware detection distinguishes systematic changes (fuel saving) from true outliers
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

        // ENHANCEMENT: Detect systematic trend (fuel saving or pushing)
        var trendAnalysis = DetectTrend(laps);
        bool hasTrend = trendAnalysis.HasSystematicTrend;
        
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

                // ENHANCEMENT: If trend detected, use relaxed MAD threshold to avoid flagging systematic changes
                float threshold = hasTrend ? (MAD_THRESHOLD * 1.5f) : MAD_THRESHOLD; // 5.25 vs 3.5 for trends

                if (fuelDeviation > threshold)
                {
                    reasons.Add($"Fuel MAD={fuelDeviation:F1} (>{threshold:F1})");
                }
                else if (hasTrend && fuelDeviation > MAD_THRESHOLD)
                {
                    // Would be flagged normally, but trend detected
                    reasons.Add($"Trend-adjusted (MAD={fuelDeviation:F1}, {trendAnalysis.TrendDescription})");
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

    /// <summary>
    /// Detect systematic trend in fuel consumption (fuel saving or pushing)
    /// Uses linear regression to identify consistent increase/decrease patterns
    /// </summary>
    /// <param name="laps">List of laps to analyze</param>
    /// <returns>Trend analysis with direction and strength</returns>
    private TrendAnalysis DetectTrend(List<FuelLapHistory> laps)
    {
        var result = new TrendAnalysis();

        if (laps.Count < 5)
        {
            // Need at least 5 laps for meaningful trend detection
            return result;
        }

        // Use last 10 laps for trend analysis (or all if less than 10)
        var recentLaps = laps.TakeLast(Math.Min(10, laps.Count)).ToList();

        // Perform linear regression: y = mx + b (fuel = slope * lap + intercept)
        // x = lap index (0, 1, 2, ...), y = fuel used
        int n = recentLaps.Count;
        float sumX = 0f, sumY = 0f, sumXY = 0f, sumX2 = 0f;

        for (int i = 0; i < n; i++)
        {
            float x = i; // Lap index
            float y = recentLaps[i].FuelUsed; // Fuel used

            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
        }

        // Calculate slope (m) and intercept (b)
        float slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        float intercept = (sumY - slope * sumX) / n;

        // Calculate correlation coefficient (R²) for trend strength
        float meanY = sumY / n;
        float ssTotal = 0f, ssResidual = 0f;

        for (int i = 0; i < n; i++)
        {
            float predicted = slope * i + intercept;
            float actual = recentLaps[i].FuelUsed;

            ssTotal += (float)Math.Pow(actual - meanY, 2);
            ssResidual += (float)Math.Pow(actual - predicted, 2);
        }

        float r2 = ssTotal > 0 ? 1f - (ssResidual / ssTotal) : 0f;

        // Determine if trend is significant
        // Criteria: R² > 0.6 (strong correlation) AND slope magnitude > 0.05L/lap (meaningful change)
        result.Slope = slope;
        result.R2 = r2;
        result.HasSystematicTrend = r2 > 0.6f && Math.Abs(slope) > 0.05f;

        if (result.HasSystematicTrend)
        {
            if (slope < -0.05f)
                result.TrendDescription = $"Fuel saving trend ({Math.Abs(slope):F3}L/lap decrease, R²={r2:F2})";
            else if (slope > 0.05f)
                result.TrendDescription = $"Pushing trend ({slope:F3}L/lap increase, R²={r2:F2})";
        }
        else
        {
            result.TrendDescription = $"No systematic trend (R²={r2:F2}, slope={slope:F3})";
        }

        return result;
    }
}

/// <summary>
/// Result of trend analysis for outlier detection
/// </summary>
internal class TrendAnalysis
{
    /// <summary>Linear regression slope (L/lap change)</summary>
    public float Slope { get; set; }

    /// <summary>R² correlation coefficient (0-1)</summary>
    public float R2 { get; set; }

    /// <summary>Whether a systematic trend was detected</summary>
    public bool HasSystematicTrend { get; set; }

    /// <summary>Human-readable description of trend</summary>
    public string TrendDescription { get; set; } = "";
}
