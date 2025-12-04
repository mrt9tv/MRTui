using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Phase 7: Real-time lap delta tracker
/// Compares current lap time to target pace and provides live feedback
/// </summary>
public class LapDeltaTracker
{
    private float _currentLapStartTime;
    private float _lastLapTime;
    private readonly Queue<float> _lapTimeHistory = new(10);
    
    /// <summary>
    /// Start tracking a new lap
    /// </summary>
    public void StartLap(float sessionTime)
    {
        _currentLapStartTime = sessionTime;
    }
    
    /// <summary>
    /// Complete the current lap and record time
    /// </summary>
    public void CompleteLap(float sessionTime, float lapTime)
    {
        _lastLapTime = lapTime;
        
        if (lapTime > 0 && lapTime < 600) // Valid lap (< 10 minutes)
        {
            _lapTimeHistory.Enqueue(lapTime);
            if (_lapTimeHistory.Count > 10)
                _lapTimeHistory.Dequeue();
        }
    }
    
    /// <summary>
    /// Calculate live delta for current lap
    /// </summary>
    public LapDelta CalculateLiveDelta(
        float sessionTime,
        float targetLapTime,
        float averageLapTime,
        bool isOnTrack)
    {
        var delta = new LapDelta();
        
        if (!isOnTrack || _currentLapStartTime <= 0 || targetLapTime <= 0)
        {
            delta.IsValid = false;
            return delta;
        }
        
        // Calculate current lap time so far
        float currentLapTime = sessionTime - _currentLapStartTime;
        
        // Avoid showing delta for very short times (< 5 seconds)
        if (currentLapTime < 5.0f)
        {
            delta.IsValid = false;
            return delta;
        }
        
        // Calculate expected time at this point in the lap
        float lapProgress = currentLapTime / averageLapTime;
        if (lapProgress > 1.5f) // More than 150% of average lap - probably invalid
        {
            delta.IsValid = false;
            return delta;
        }
        
        float expectedTimeForTarget = targetLapTime * lapProgress;
        
        // Delta: positive = faster than target, negative = slower than target
        delta.DeltaToTarget = expectedTimeForTarget - currentLapTime;
        delta.CurrentLapTime = currentLapTime;
        delta.TargetLapTime = targetLapTime;
        delta.LapProgress = Math.Min(lapProgress, 1.0f);
        delta.IsValid = true;
        
        // Predict final lap time based on current pace
        if (lapProgress > 0.2f) // Need at least 20% of lap for prediction
        {
            delta.PredictedLapTime = currentLapTime / lapProgress;
            delta.PredictedDelta = targetLapTime - delta.PredictedLapTime;
        }
        
        return delta;
    }
    
    /// <summary>
    /// Get average lap time from history
    /// </summary>
    public float GetAverageLapTime()
    {
        if (_lapTimeHistory.Count == 0)
            return 0f;
        
        return _lapTimeHistory.Average();
    }
    
    /// <summary>
    /// Get last completed lap time
    /// </summary>
    public float GetLastLapTime()
    {
        return _lastLapTime;
    }
    
    /// <summary>
    /// Reset all tracking data
    /// </summary>
    public void Reset()
    {
        _currentLapStartTime = 0;
        _lastLapTime = 0;
        _lapTimeHistory.Clear();
    }
}

/// <summary>
/// Live lap delta data
/// </summary>
public class LapDelta
{
    public bool IsValid { get; set; }
    public float DeltaToTarget { get; set; }
    public float CurrentLapTime { get; set; }
    public float TargetLapTime { get; set; }
    public float LapProgress { get; set; }
    public float PredictedLapTime { get; set; }
    public float PredictedDelta { get; set; }
}
