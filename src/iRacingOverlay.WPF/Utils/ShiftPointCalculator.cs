namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Calculates optimal shift points and RPM color zones based on telemetry data
/// PHASE 1 UPDATE: Now uses iRacing's professional shift light telemetry as primary source
/// Falls back to learning system only when SDK values unavailable
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
        
        // Future enhancement: ML-based shift point prediction using throttle position, gear ratios,
        // track position, and historical optimal shift points per track/car combination
        
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
    /// LEGACY METHOD: Uses learning system only (kept for backward compatibility)
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
    /// Get the RPM zone for color coding using iRacing's professional shift light telemetry
    /// PHASE 1: Primary method - uses SDK shift points when available, falls back to learning system
    /// </summary>
    /// <param name="currentRPM">Current engine RPM</param>
    /// <param name="gear">Current gear</param>
    /// <param name="shiftFirstRPM">SDK: When shift lights START (PlayerCarSLFirstRPM)</param>
    /// <param name="shiftOptimalRPM">SDK: OPTIMAL shift point (PlayerCarSLShiftRPM)</param>
    /// <param name="shiftLastRPM">SDK: When shift lights FULLY LIT (PlayerCarSLLastRPM)</param>
    /// <param name="shiftBlinkRPM">SDK: When shift lights BLINK (PlayerCarSLBlinkRPM)</param>
    public static RPMZone GetRPMZone(float currentRPM, int gear, float shiftFirstRPM, float shiftOptimalRPM, float shiftLastRPM, float shiftBlinkRPM)
    {
        // Neutral or reverse - always safe
        if (gear <= 0)
        {
            return RPMZone.Safe;
        }
        
        // Guard: If SDK hasn't provided ANY shift light data yet (session loading, garage exit),
        // stay Safe (Teal) to avoid false Warning color from stale learning data
        if (shiftFirstRPM <= 0 && shiftOptimalRPM <= 0 && shiftBlinkRPM <= 0)
            return RPMZone.Safe;

        // PHASE 1: Use iRacing's professional shift light values if available
        // These are car-specific and based on actual engine physics/torque curves
        if (shiftOptimalRPM > 0 && shiftBlinkRPM > 0)
        {
            // ASYMMETRIC HYBRID BUFFERING: Larger buffer before optimal (early shift forgiveness), smaller after (safety)
            // This approach provides:
            // - Large early shift window: 2.0% before optimal = 130-300 RPM forgiveness for early shifts
            // - Conservative extension: 0.5% after optimal = 33-75 RPM maximum past optimal (safe from blink zone)
            // - Edge case protection: Handles cars where ShiftRPM ≈ LastRPM without encroaching on danger zones
            // Examples: Street Stock (6500 RPM) = -130/+33 RPM, GT3 (8500 RPM) = -170/+43 RPM, Formula (15000 RPM) = -300/+75 RPM
            
            const float OPTIMAL_BUFFER_BEFORE_PERCENT = 0.02f;  // 2.0% buffer before optimal (early shift window)
            const float OPTIMAL_BUFFER_AFTER_PERCENT = 0.005f;  // 0.5% buffer after optimal (conservative extension)
            
            float optimalBufferBefore = shiftOptimalRPM * OPTIMAL_BUFFER_BEFORE_PERCENT;
            float optimalBufferAfter = shiftOptimalRPM * OPTIMAL_BUFFER_AFTER_PERCENT;
            
            // Calculate zone thresholds with asymmetric intelligent buffering:
            float warningStart = shiftFirstRPM;                      // Yellow starts when shift lights illuminate
            float optimalStart = shiftOptimalRPM - optimalBufferBefore;   // Orange starts 2.0% before optimal
            float optimalEnd = Math.Max(shiftOptimalRPM + optimalBufferAfter, shiftLastRPM); // Orange extends 0.5% past optimal OR to LastRPM
            float dangerStart = optimalEnd;                          // Red starts after orange window
            
            // Professional-grade zones with smart buffering:
            // - Safe: Below shift light start
            // - Warning: Shift lights starting (FirstRPM to OptimalStart)
            // - Optimal: Best shift window (OptimalStart to OptimalEnd) - SHIFT NOW
            // - Danger: Over-rev zone (OptimalEnd to BlinkRPM and beyond)
            
            if (currentRPM >= shiftBlinkRPM)
            {
                return RPMZone.Danger;  // RED - Over-rev warning (blink zone)
            }
            else if (currentRPM >= dangerStart)
            {
                return RPMZone.Danger;  // RED - Past optimal window, approaching blink
            }
            else if (currentRPM >= optimalStart)
            {
                return RPMZone.Optimal; // ORANGE - Optimal shift window (SHIFT NOW)
            }
            else if (currentRPM >= warningStart)
            {
                return RPMZone.Warning; // YELLOW - Shift lights starting (prepare to shift)
            }
            else
            {
                return RPMZone.Safe;    // TEAL - Safe operating range
            }
        }
        
        // FALLBACK: Use learning system if SDK shift light values not available
        // This handles edge cases where SDK might not provide shift light data
        return GetRPMZone(currentRPM, gear);
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
