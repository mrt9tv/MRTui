using System.Collections.Generic;
using System.Linq;

namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Service for tracking delta between our predictions and iRacing's predictions
/// Analyzes convergence, confidence, and accuracy over time
/// Phase 5: Extracted from FuelCalculatorService
/// </summary>
public class DeltaTrackingService
{
    private readonly List<DeltaHistory> _deltaHistory = new();
    private int _totalPredictions = 0;
    private int _ourMethodCorrect = 0;
    private int _iracingMethodCorrect = 0;
    
    private class DeltaHistory
    {
        public float Delta { get; set; }
        public int LapNumber { get; set; }
        public float OurPrediction { get; set; }
        public float IRacingPrediction { get; set; }
    }
    
    /// <summary>
    /// Track delta between prediction methods and analyze convergence
    /// </summary>
    public DeltaData Track(
        float ourLapsRemaining,
        float iracingLapsRemaining,
        int currentLap)
    {
        var deltaData = new DeltaData();
        
        if (ourLapsRemaining <= 0 || iracingLapsRemaining <= 0)
            return deltaData;
        
        // Calculate current delta
        deltaData.CurrentDelta = ourLapsRemaining - iracingLapsRemaining;
        
        if (iracingLapsRemaining > 0)
        {
            deltaData.DeltaPercentage = (deltaData.CurrentDelta / iracingLapsRemaining) * 100f;
        }
        
        // Add to history
        _deltaHistory.Add(new DeltaHistory
        {
            Delta = deltaData.CurrentDelta,
            LapNumber = currentLap,
            OurPrediction = ourLapsRemaining,
            IRacingPrediction = iracingLapsRemaining
        });
        
        // Keep only last 20 laps
        if (_deltaHistory.Count > 20)
        {
            _deltaHistory.RemoveAt(0);
        }
        
        // Analyze convergence trend
        AnalyzeConvergence(deltaData);
        
        // Calculate confidence score
        CalculateConfidence(deltaData);
        
        // Detect suspicious deltas
        DetectSuspiciousDelta(deltaData);
        
        // Track accuracy
        TrackAccuracy(deltaData, ourLapsRemaining, iracingLapsRemaining);
        
        return deltaData;
    }
    
    private void AnalyzeConvergence(DeltaData deltaData)
    {
        if (_deltaHistory.Count < 3)
        {
            deltaData.ConvergenceTrend = "Insufficient data";
            deltaData.ConvergenceRate = 0f;
            return;
        }
        
        // Get recent deltas (last 5 laps)
        var recentDeltas = _deltaHistory.TakeLast(5).Select(h => Math.Abs(h.Delta)).ToList();
        
        if (recentDeltas.Count < 2)
        {
            deltaData.ConvergenceTrend = "Insufficient data";
            return;
        }
        
        // Calculate average delta change
        float totalChange = 0f;
        for (int i = 1; i < recentDeltas.Count; i++)
        {
            totalChange += recentDeltas[i] - recentDeltas[i - 1];
        }
        
        float avgChange = totalChange / (recentDeltas.Count - 1);
        deltaData.ConvergenceRate = avgChange;
        
        // Determine trend
        if (Math.Abs(avgChange) < 0.01f)
        {
            deltaData.ConvergenceTrend = "Stable";
        }
        else if (avgChange < -0.05f)
        {
            deltaData.ConvergenceTrend = "Converging (methods agreeing)";
        }
        else if (avgChange > 0.05f)
        {
            deltaData.ConvergenceTrend = "Diverging (methods disagreeing)";
        }
        else
        {
            deltaData.ConvergenceTrend = avgChange < 0 ? "Slowly converging" : "Slowly diverging";
        }
    }
    
    private void CalculateConfidence(DeltaData deltaData)
    {
        if (_deltaHistory.Count < 3)
        {
            deltaData.ConfidenceScore = 0f;
            deltaData.ConfidenceLevel = "Low (insufficient data)";
            return;
        }
        
        // Calculate consistency of recent deltas
        var recentDeltas = _deltaHistory.TakeLast(5).Select(h => h.Delta).ToList();
        float avgDelta = recentDeltas.Average();
        float variance = recentDeltas.Sum(d => (d - avgDelta) * (d - avgDelta)) / recentDeltas.Count;
        float stdDev = (float)Math.Sqrt(variance);
        
        // Lower standard deviation = higher confidence
        // Map stdDev to 0-100 confidence score
        // stdDev < 0.1 = very confident (90-100)
        // stdDev 0.1-0.3 = confident (70-90)
        // stdDev 0.3-0.5 = moderate (50-70)
        // stdDev > 0.5 = low (0-50)
        
        if (stdDev < 0.1f)
        {
            deltaData.ConfidenceScore = 95f - (stdDev * 50f);
            deltaData.ConfidenceLevel = "Very High";
        }
        else if (stdDev < 0.3f)
        {
            deltaData.ConfidenceScore = 80f - ((stdDev - 0.1f) * 50f);
            deltaData.ConfidenceLevel = "High";
        }
        else if (stdDev < 0.5f)
        {
            deltaData.ConfidenceScore = 60f - ((stdDev - 0.3f) * 50f);
            deltaData.ConfidenceLevel = "Moderate";
        }
        else
        {
            deltaData.ConfidenceScore = Math.Max(0f, 50f - ((stdDev - 0.5f) * 100f));
            deltaData.ConfidenceLevel = "Low";
        }
        
        // Boost confidence if converging
        if (deltaData.ConvergenceTrend.Contains("Converging"))
        {
            deltaData.ConfidenceScore = Math.Min(100f, deltaData.ConfidenceScore + 10f);
        }
        // Reduce confidence if diverging
        else if (deltaData.ConvergenceTrend.Contains("Diverging"))
        {
            deltaData.ConfidenceScore = Math.Max(0f, deltaData.ConfidenceScore - 10f);
        }
    }
    
    private void DetectSuspiciousDelta(DeltaData deltaData)
    {
        deltaData.DeltaSuspicious = false;
        deltaData.SuspicionReason = string.Empty;
        
        if (_deltaHistory.Count < 3)
            return;
        
        // Check for sudden large delta spikes
        var recentDeltas = _deltaHistory.TakeLast(3).ToList();
        if (recentDeltas.Count >= 2)
        {
            float previousDelta = Math.Abs(recentDeltas[recentDeltas.Count - 2].Delta);
            float currentDelta = Math.Abs(deltaData.CurrentDelta);
            
            // Spike detection: > 0.5 lap sudden change
            if (Math.Abs(currentDelta - previousDelta) > 0.5f)
            {
                deltaData.DeltaSuspicious = true;
                deltaData.SuspicionReason = "Sudden delta spike detected";
                return;
            }
        }
        
        // Check for persistent large delta (> 1.0 laps)
        if (Math.Abs(deltaData.CurrentDelta) > 1.0f && _deltaHistory.Count >= 5)
        {
            var last5Deltas = _deltaHistory.TakeLast(5).Select(h => Math.Abs(h.Delta)).ToList();
            if (last5Deltas.All(d => d > 1.0f))
            {
                deltaData.DeltaSuspicious = true;
                deltaData.SuspicionReason = "Persistent large delta (> 1.0 lap)";
                return;
            }
        }
        
        // Check for oscillating pattern
        if (_deltaHistory.Count >= 6)
        {
            var last6Signs = _deltaHistory.TakeLast(6).Select(h => Math.Sign(h.Delta)).ToList();
            int signChanges = 0;
            for (int i = 1; i < last6Signs.Count; i++)
            {
                if (last6Signs[i] != last6Signs[i - 1])
                    signChanges++;
            }
            
            // If sign changes more than 3 times in 6 laps, it's oscillating
            if (signChanges >= 3)
            {
                deltaData.DeltaSuspicious = true;
                deltaData.SuspicionReason = "Oscillating pattern detected";
            }
        }
    }
    
    private void TrackAccuracy(DeltaData deltaData, float ourPrediction, float iracingPrediction)
    {
        // Note: This is a simplified placeholder
        // Real accuracy tracking would require comparing predictions against actual finish
        
        deltaData.TotalPredictions = _totalPredictions;
        
        if (_totalPredictions > 0)
        {
            deltaData.OurMethodAccuracy = (_ourMethodCorrect / (float)_totalPredictions) * 100f;
            deltaData.IRacingMethodAccuracy = (_iracingMethodCorrect / (float)_totalPredictions) * 100f;
            
            float diff = deltaData.OurMethodAccuracy - deltaData.IRacingMethodAccuracy;
            
            if (Math.Abs(diff) < 2f)
            {
                deltaData.AccuracyComparison = "Methods equally accurate";
            }
            else if (diff > 0)
            {
                deltaData.AccuracyComparison = $"Our method {diff:F1}% more accurate";
            }
            else
            {
                deltaData.AccuracyComparison = $"iRacing method {Math.Abs(diff):F1}% more accurate";
            }
        }
        else
        {
            deltaData.AccuracyComparison = "Insufficient data for comparison";
        }
    }
    
    /// <summary>
    /// Reset tracking for new session
    /// </summary>
    public void Reset()
    {
        _deltaHistory.Clear();
        _totalPredictions = 0;
        _ourMethodCorrect = 0;
        _iracingMethodCorrect = 0;
    }
}
