namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Calculates optimal shift points and RPM color zones based on telemetry data
/// </summary>
public static class ShiftPointCalculator
{
    /// <summary>
    /// RPM color zone for visualization
    /// </summary>
    public enum RPMZone
    {
        Safe,      // Green - normal operating range
        Optimal,   // Orange - optimal shift window
        Warning,   // Yellow - approaching redline
        Danger     // Red - redline/limiter
    }
    
    // Track RPM history to learn the car's characteristics
    private static float _maxObservedRPM = 0f;
    private static float _typicalCruisingRPM = 0f;
    private static int _sampleCount = 0;
    private static float _carRedlineRPM = 0f; // From SDK if available
    
    /// <summary>
    /// Update RPM tracking with new telemetry data
    /// </summary>
    public static void UpdateTracking(float currentRPM, float throttle, int gear, float carRedline = 0f)
    {
        // Store car redline from SDK if provided
        if (carRedline > 0 && _carRedlineRPM == 0)
        {
            _carRedlineRPM = carRedline;
        }
        
        // Track maximum RPM observed (likely redline)
        if (currentRPM > _maxObservedRPM)
        {
            _maxObservedRPM = currentRPM;
        }
        
        // Track typical cruising RPM (at partial throttle)
        if (throttle > 0.3f && throttle < 0.7f && gear > 0)
        {
            _typicalCruisingRPM = (_typicalCruisingRPM * _sampleCount + currentRPM) / (_sampleCount + 1);
            _sampleCount++;
        }
    }
    
    /// <summary>
    /// Get the estimated redline RPM for the current car
    /// </summary>
    public static float GetEstimatedRedline()
    {
        // Use car redline from SDK if available
        if (_carRedlineRPM > 0)
        {
            return _carRedlineRPM;
        }
        
        // TODO: Improve estimation algorithm - consider throttle position, gear ratios, and historical data
        
        // If we haven't observed high RPMs yet, use a more conservative approach
        if (_maxObservedRPM < 1000f)
        {
            return 8500f; // Higher default to avoid false warnings until we learn the car
        }
        
        // Once we've observed some RPMs, be more adaptive (increased threshold for better learning)
        if (_maxObservedRPM < 5000f)
        {
            // Still learning - use higher estimate to avoid false warnings
            return _maxObservedRPM * 1.5f;
        }
        
        // Redline is typically 95-98% of the limiter
        // Add small buffer to account for not hitting absolute max
        return _maxObservedRPM * 1.02f;
    }
    
    /// <summary>
    /// Get the optimal shift point RPM
    /// Typically 90-95% of redline for maximum power
    /// </summary>
    public static float GetOptimalShiftPoint()
    {
        float redline = GetEstimatedRedline();
        
        // Optimal shift is typically 92-95% of redline
        // This is where most engines make peak power
        return redline * 0.93f;
    }
    
    /// <summary>
    /// Get the RPM zone for color coding
    /// </summary>
    public static RPMZone GetRPMZone(float currentRPM, int gear)
    {
        // Neutral or reverse - always safe
        if (gear <= 0)
        {
            return RPMZone.Safe;
        }
        
        float redline = GetEstimatedRedline();
        
        // Calculate zone thresholds as percentages of redline
        float yellowThreshold = redline * 0.90f;   // 90% - Yellow warning
        float orangeThreshold = redline * 0.94f;   // 94% - Orange optimal shift
        float redThreshold = redline * 0.97f;      // 97% - Red danger

        if (currentRPM >= redThreshold)
        {
            return RPMZone.Danger;  // RED - 97-100% of redline
        }
        else if (currentRPM >= orangeThreshold)
        {
            return RPMZone.Optimal; // ORANGE - 94-96% optimal shift window
        }
        else if (currentRPM >= yellowThreshold)
        {
            return RPMZone.Warning; // YELLOW - 90-93% approaching shift
        }
        else
        {
            return RPMZone.Safe;    // TEAL - 0-89% safe operating range
        }
    }
    
    /// <summary>
    /// Get RPM percentage (0-100) relative to redline
    /// </summary>
    public static float GetRPMPercentage(float currentRPM)
    {
        float redline = GetEstimatedRedline();
        if (redline <= 0) return 0f;
        
        return Math.Min(100f, (currentRPM / redline) * 100f);
    }
    
    /// <summary>
    /// Check if the driver should shift now
    /// </summary>
    public static bool ShouldShift(float currentRPM, int gear, float throttle)
    {
        // Don't suggest shift if not on throttle
        if (throttle < 0.5f) return false;
        
        // Don't suggest shift in neutral or reverse
        if (gear <= 0) return false;
        
        float optimalShift = GetOptimalShiftPoint();
        
        // Shift when in optimal window
        return currentRPM >= optimalShift;
    }
    
    /// <summary>
    /// Reset tracking (when changing cars or sessions)
    /// </summary>
    public static void ResetTracking()
    {
        _maxObservedRPM = 0f;
        _typicalCruisingRPM = 0f;
        _sampleCount = 0;
    }
}
