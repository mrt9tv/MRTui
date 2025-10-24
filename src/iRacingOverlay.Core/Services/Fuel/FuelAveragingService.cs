using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Service for calculating fuel consumption averages using multiple methods
/// Provides 9 different averaging algorithms for different strategic needs
/// </summary>
public class FuelAveragingService
{
    // EMA (Exponential Moving Average) state tracking
    private float _emaValue = 0f;
    private bool _emaInitialized = false;
    
    /// <summary>
    /// Calculate all fuel consumption averages from lap history
    /// </summary>
    /// <param name="lapHistory">Complete lap history (filtered laps provided by caller)</param>
    /// <param name="validLaps">Pre-filtered valid laps for averaging</param>
    /// <param name="stintStartLapNumber">Lap number when current stint started</param>
    /// <param name="fuelConsistencyVariance">Pre-calculated fuel consistency variance (for EMA adaptive alpha)</param>
    /// <returns>FuelAverages containing all 9 averaging methods</returns>
    public FuelAverages Calculate(
        List<FuelLapHistory> lapHistory,
        List<FuelLapHistory> validLaps, 
        int stintStartLapNumber,
        float fuelConsistencyVariance)
    {
        var result = new FuelAverages();
        
        // Check if we have sufficient data
        if (validLaps.Count == 0)
        {
            result.HasSufficientData = false;
            result.WarningMessage = "Need at least 1 completed lap for fuel calculations";
            return result;
        }
        
        result.HasSufficientData = validLaps.Count >= 2;
        
        // 1. Last lap average (most recent completed lap)
        result.Last = validLaps.LastOrDefault()?.FuelUsed ?? 0f;
        
        // 2. Last 5 laps average (exponentially weighted for smoother, more responsive predictions)
        result.L5 = CalculateL5Weighted(validLaps);
        
        // 3. Last 10 laps average (simple average for longer-term trend)
        result.L10 = CalculateL10(validLaps);
        
        // 4. Session average (calculated by caller with outlier filtering)
        // This is intentionally left to caller since outlier detection is a separate service
        
        // 5. Min/Max fuel per lap
        result.Min = validLaps.Min(l => l.FuelUsed);
        result.Max = validLaps.Max(l => l.FuelUsed);
        
        // 6. Green flag average (for racing conditions)
        var greenLaps = validLaps.Where(l => l.IsGreenFlagLap).ToList();
        result.GreenFlagLapCount = greenLaps.Count;
        result.GreenOnly = greenLaps.Count > 0 ? greenLaps.Average(l => l.FuelUsed) : 0f;
        
        // 7. Yellow flag average (for caution periods)
        var yellowLaps = validLaps.Where(l => l.IsYellowFlagLap).ToList();
        result.YellowFlagLapCount = yellowLaps.Count;
        result.YellowFlagAverage = yellowLaps.Count > 0 ? yellowLaps.Average(l => l.FuelUsed) : 0f;
        
        // 8. Exponential Moving Average (EMA) - smoother transitions, responsive to trends
        result.EMA = CalculateEMA(validLaps, fuelConsistencyVariance);
        
        // 9. Stint average - fuel consumption since last pit stop
        result.Stint = CalculateStintAverage(validLaps, stintStartLapNumber, result.L5);
        result.StintLapCount = validLaps.Count(l => l.LapNumber > stintStartLapNumber);
        
        // 10. Adaptive weighted average - adjusts weighting based on fuel consistency
        result.Adaptive = CalculateAdaptiveWeighted(validLaps, result.L5);
        
        // 11. Average lap time (for time-based sessions)
        result.AverageLapTime = CalculateAverageLapTime(validLaps);
        
        // 12. Pace lap average (formation/warmup laps)
        var paceLaps = lapHistory.Where(l => l.IsValidPaceLap).ToList();
        result.PaceLapCount = paceLaps.Count;
        result.PaceLaps = paceLaps.Count > 0 ? paceLaps.Average(l => l.FuelUsed) : 0f;
        
        // Store consistency variance for reference
        result.FuelConsistencyVariance = fuelConsistencyVariance;
        
        return result;
    }
    
    /// <summary>
    /// Calculate Last 5 laps average with exponential weighting
    /// Most recent lap has highest influence (27%), smoothly decreasing to 17% for 5th lap
    /// </summary>
    private float CalculateL5Weighted(List<FuelLapHistory> validLaps)
    {
        var last5 = validLaps.TakeLast(5).ToList();
        if (last5.Count == 0)
            return 0f;
        
        // Use exponential weighting: most recent lap has highest influence
        // Weights: [1.0, 1.15, 1.3, 1.45, 1.6] (oldest to newest)
        // Reduced from 0.2f to 0.15f to limit outlier influence
        float totalWeight = 0f;
        float weightedSum = 0f;
        for (int i = 0; i < last5.Count; i++)
        {
            float weight = 1.0f + (i * 0.15f);
            weightedSum += last5[i].FuelUsed * weight;
            totalWeight += weight;
        }
        
        return weightedSum / totalWeight;
    }
    
    /// <summary>
    /// Calculate Last 10 laps average (simple average for longer-term trend)
    /// </summary>
    private float CalculateL10(List<FuelLapHistory> validLaps)
    {
        var last10 = validLaps.TakeLast(10).ToList();
        return last10.Count > 0 ? last10.Average(l => l.FuelUsed) : 0f;
    }
    
    /// <summary>
    /// Calculate Exponential Moving Average (EMA) with adaptive alpha based on consistency
    /// Formula: EMA = (CurrentValue * Alpha) + (PreviousEMA * (1 - Alpha))
    /// Alpha adjusts based on fuel consistency: more consistent = more responsive
    /// </summary>
    private float CalculateEMA(List<FuelLapHistory> validLaps, float fuelConsistencyVariance)
    {
        if (validLaps.Count == 0)
            return 0f;
        
        var mostRecentLap = validLaps.Last();
        
        if (!_emaInitialized)
        {
            // Initialize EMA with first lap value
            _emaValue = mostRecentLap.FuelUsed;
            _emaInitialized = true;
            return _emaValue;
        }
        
        // Calculate adaptive alpha based on fuel consistency variance
        // More consistent driving → higher alpha (more responsive)
        // More variable driving → lower alpha (smoother, less reactive to outliers)
        float adaptiveAlpha = fuelConsistencyVariance switch
        {
            < 0.1f => 0.5f,  // Very consistent (±0.1L) → highly responsive (50% recent)
            < 0.2f => 0.4f,  // Normal consistency (±0.2L) → balanced (40% recent)
            < 0.3f => 0.3f,  // Variable (±0.3L) → smoother (30% recent)
            _ => 0.2f        // Very variable (±0.3L+) → very smooth (20% recent)
        };
        
        // Update EMA with adaptive exponential smoothing
        _emaValue = (mostRecentLap.FuelUsed * adaptiveAlpha) + (_emaValue * (1 - adaptiveAlpha));
        
        return _emaValue;
    }
    
    /// <summary>
    /// Calculate stint average - fuel consumption since last pit stop
    /// </summary>
    private float CalculateStintAverage(List<FuelLapHistory> validLaps, int stintStartLapNumber, float l5Fallback)
    {
        var stintLaps = validLaps.Where(l => l.LapNumber > stintStartLapNumber).ToList();
        
        if (stintLaps.Count > 0)
            return stintLaps.Average(l => l.FuelUsed);
        
        // No stint data yet, fallback to L5 average
        return l5Fallback;
    }
    
    /// <summary>
    /// Calculate adaptive weighted average - adjusts weighting based on fuel consistency
    /// Tight weighting if fuel use is consistent, loose weighting if variable
    /// </summary>
    private float CalculateAdaptiveWeighted(List<FuelLapHistory> validLaps, float l5Fallback)
    {
        var last5 = validLaps.TakeLast(5).ToList();
        
        if (last5.Count < 3)
        {
            // Not enough data for adaptive weighting, fallback to L5
            return l5Fallback;
        }
        
        // Calculate standard deviation of last 5 laps to measure consistency
        float mean = last5.Average(l => l.FuelUsed);
        float variance = last5.Sum(l => (float)Math.Pow(l.FuelUsed - mean, 2)) / last5.Count;
        float stdDev = (float)Math.Sqrt(variance);
        
        // Coefficient of variation (CV) = StdDev / Mean
        // Low CV (<0.05) = very consistent → use tight weights (favor recent laps heavily)
        // High CV (>0.15) = variable → use loose weights (spread weight more evenly)
        float cv = mean > 0 ? stdDev / mean : 0f;
        
        // Adaptive weight spread: CV < 0.05 → tight (0.3), CV > 0.15 → loose (0.1)
        float weightSpread = cv < 0.05f ? 0.3f : cv > 0.15f ? 0.1f : 0.2f;
        
        // Apply adaptive weighting
        float totalWeight = 0f;
        float weightedSum = 0f;
        for (int i = 0; i < last5.Count; i++)
        {
            float weight = 1.0f + (i * weightSpread);
            weightedSum += last5[i].FuelUsed * weight;
            totalWeight += weight;
        }
        
        return weightedSum / totalWeight;
    }
    
    /// <summary>
    /// Calculate average lap time for time-based sessions
    /// Uses weighted average (recent laps weighted more)
    /// </summary>
    private float CalculateAverageLapTime(List<FuelLapHistory> validLaps)
    {
        var lapsWithTime = validLaps.Where(l => l.LapTime > 0).ToList();
        
        if (lapsWithTime.Count == 0)
            return 0f;
        
        // Use weighted average for lap time (same as L5 fuel: recent laps weighted more)
        var recentLaps = lapsWithTime.TakeLast(5).ToList();
        float totalWeight = 0f;
        float weightedSum = 0f;
        for (int i = 0; i < recentLaps.Count; i++)
        {
            float weight = 1.0f + (i * 0.2f);
            weightedSum += recentLaps[i].LapTime * weight;
            totalWeight += weight;
        }
        
        return weightedSum / totalWeight;
    }
    
    /// <summary>
    /// Reset EMA state (call when session changes or service resets)
    /// </summary>
    public void Reset()
    {
        _emaValue = 0f;
        _emaInitialized = false;
    }
}
