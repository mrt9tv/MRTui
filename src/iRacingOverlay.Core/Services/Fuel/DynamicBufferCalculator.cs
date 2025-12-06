namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Service for calculating dynamic fuel buffer based on race conditions
/// Considers consistency, position, weather, yellow flags, and race phase
/// Phase 6: Extracted from FuelCalculatorService
/// </summary>
public class DynamicBufferCalculator
{
    /// <summary>
    /// Calculate dynamic buffer based on multiple factors
    /// </summary>
    /// <param name="userBufferLaps">User-configured base buffer laps</param>
    /// <param name="enableDynamicBuffer">Whether to apply dynamic adjustments</param>
    public BufferData Calculate(
        float consistencyFactor,
        int currentPosition,
        int totalCars,
        bool isRaining,
        int yellowFlagCount,
        int currentLap,
        int totalLaps,
        bool isTimedSession,
        float userBufferLaps,
        bool enableDynamicBuffer)
    {
        var bufferData = new BufferData
        {
            TotalBuffer = userBufferLaps  // Use user setting instead of hardcoded value
        };
        
        // If dynamic buffer disabled, return user setting immediately
        if (!enableDynamicBuffer)
        {
            bufferData.Reason = $"Fixed: {userBufferLaps:F2} laps (dynamic buffer disabled)";
            return bufferData;
        }
        
        var reasons = new List<string>();
        
        // Factor 1: Consistency
        bufferData.ConsistencyFactor = CalculateConsistencyFactor(consistencyFactor);
        if (bufferData.ConsistencyFactor > 0)
        {
            bufferData.TotalBuffer += bufferData.ConsistencyFactor;
            reasons.Add($"Consistency: +{bufferData.ConsistencyFactor:F2}");
        }
        
        // Factor 2: Position (higher risk at front/back)
        bufferData.PositionFactor = CalculatePositionFactor(currentPosition, totalCars);
        if (bufferData.PositionFactor > 0)
        {
            bufferData.TotalBuffer += bufferData.PositionFactor;
            reasons.Add($"Position: +{bufferData.PositionFactor:F2}");
        }
        
        // Factor 3: Weather
        bufferData.WeatherFactor = CalculateWeatherFactor(isRaining);
        if (bufferData.WeatherFactor > 0)
        {
            bufferData.TotalBuffer += bufferData.WeatherFactor;
            reasons.Add($"Weather: +{bufferData.WeatherFactor:F2}");
        }
        
        // Factor 4: Yellow flag history
        bufferData.YellowFlagFactor = CalculateYellowFlagFactor(yellowFlagCount, currentLap);
        if (bufferData.YellowFlagFactor > 0)
        {
            bufferData.TotalBuffer += bufferData.YellowFlagFactor;
            reasons.Add($"Yellow flags: +{bufferData.YellowFlagFactor:F2}");
        }
        
        // Factor 5: End of race
        bufferData.EndOfRaceFactor = CalculateEndOfRaceFactor(currentLap, totalLaps, isTimedSession);
        if (bufferData.EndOfRaceFactor != 0)
        {
            bufferData.TotalBuffer += bufferData.EndOfRaceFactor;
            if (bufferData.EndOfRaceFactor < 0)
                reasons.Add($"End of race: {bufferData.EndOfRaceFactor:F2}");
            else
                reasons.Add($"End of race: +{bufferData.EndOfRaceFactor:F2}");
        }
        
        // Ensure minimum buffer
        bufferData.TotalBuffer = Math.Max(0.2f, bufferData.TotalBuffer);
        
        // Build reason string
        if (reasons.Count > 0)
        {
            bufferData.Reason = $"Base: {userBufferLaps:F2} | {string.Join(" | ", reasons)}";
        }
        else
        {
            bufferData.Reason = $"Base: {userBufferLaps:F2} (no adjustments)";
        }
        
        return bufferData;
    }
    
    private float CalculateConsistencyFactor(float consistencyFactor)
    {
        // consistencyFactor is standard deviation of fuel usage
        // Lower consistency = higher buffer needed
        
        if (consistencyFactor <= 0)
            return 0f;
        
        // Map consistency to buffer:
        // 0.0-0.05: Very consistent, no buffer (+0)
        // 0.05-0.15: Moderate, small buffer (+0.1-0.3)
        // 0.15-0.30: Inconsistent, medium buffer (+0.3-0.6)
        // > 0.30: Very inconsistent, large buffer (+0.6-1.0)
        
        if (consistencyFactor < 0.05f)
        {
            return 0f;
        }
        else if (consistencyFactor < 0.15f)
        {
            // Linear interpolation: 0.05 -> 0.1, 0.15 -> 0.3
            return 0.1f + ((consistencyFactor - 0.05f) / 0.1f) * 0.2f;
        }
        else if (consistencyFactor < 0.30f)
        {
            // Linear interpolation: 0.15 -> 0.3, 0.30 -> 0.6
            return 0.3f + ((consistencyFactor - 0.15f) / 0.15f) * 0.3f;
        }
        else
        {
            // Cap at 1.0 lap buffer
            return Math.Min(1.0f, 0.6f + ((consistencyFactor - 0.30f) / 0.20f) * 0.4f);
        }
    }
    
    private float CalculatePositionFactor(int currentPosition, int totalCars)
    {
        if (totalCars <= 0 || currentPosition <= 0)
            return 0f;
        
        // Higher risk at front (battles) and back (getting lapped)
        float positionRatio = currentPosition / (float)totalCars;
        
        if (positionRatio <= 0.2f) // Top 20%
        {
            return 0.3f; // Fighting for positions
        }
        else if (positionRatio >= 0.8f) // Bottom 20%
        {
            return 0.2f; // Risk of getting lapped
        }
        else
        {
            return 0f; // Mid-pack is safest
        }
    }
    
    private float CalculateWeatherFactor(bool isRaining)
    {
        // Rain increases unpredictability
        return isRaining ? 0.4f : 0f;
    }
    
    private float CalculateYellowFlagFactor(int yellowFlagCount, int currentLap)
    {
        if (currentLap <= 0)
            return 0f;
        
        // Calculate yellow flag frequency
        float yellowsPerLap = yellowFlagCount / (float)currentLap;
        
        // More yellows = more unpredictability
        // 0 yellows: +0
        // 1 yellow per 10 laps: +0.2
        // 1 yellow per 5 laps: +0.4
        // > 1 yellow per 3 laps: +0.6
        
        if (yellowsPerLap <= 0)
        {
            return 0f;
        }
        else if (yellowsPerLap < 0.1f) // Less than 1 per 10 laps
        {
            return 0.2f;
        }
        else if (yellowsPerLap < 0.2f) // 1 per 5-10 laps
        {
            return 0.4f;
        }
        else
        {
            return 0.6f; // Frequent yellows
        }
    }
    
    private float CalculateEndOfRaceFactor(int currentLap, int totalLaps, bool isTimedSession)
    {
        if (isTimedSession)
        {
            // For timed sessions, don't adjust buffer based on laps
            return 0f;
        }
        
        if (totalLaps <= 0 || currentLap <= 0)
            return 0f;
        
        float raceProgress = currentLap / (float)totalLaps;
        
        // Reduce buffer in final 10% of race (less risk needed)
        if (raceProgress >= 0.9f)
        {
            // Linear reduction: 90% -> 0, 100% -> -0.3
            return -0.3f * ((raceProgress - 0.9f) / 0.1f);
        }
        
        // Increase buffer slightly in first 10% (unknowns)
        if (raceProgress <= 0.1f)
        {
            return 0.2f * (1f - (raceProgress / 0.1f));
        }
        
        return 0f;
    }
}
