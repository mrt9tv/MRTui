using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services.Tire;

/// <summary>
/// Phase 8: Tire strategy service
/// Tracks tire wear, predicts pit windows based on tire life,
/// and coordinates with fuel strategy for optimal combined stops
/// </summary>
public class TireStrategyService
{
    private readonly Queue<TireWearSnapshot> _wearHistory = new(50);
    private int _lastLapRecorded = -1;
    
    /// <summary>
    /// Update tire wear tracking with latest telemetry
    /// </summary>
    public TireStrategy Update(TelemetryData telemetry, int currentLap)
    {
        var strategy = new TireStrategy();
        
        // Record tire wear snapshot (once per lap)
        if (currentLap > _lastLapRecorded && currentLap > 0)
        {
            RecordTireWear(telemetry, currentLap);
            _lastLapRecorded = currentLap;
        }
        
        // Calculate tire wear rates
        if (_wearHistory.Count >= 3)
        {
            CalculateTireWearRates(strategy);
            PredictTirePitLap(strategy, telemetry, currentLap);
        }
        
        return strategy;
    }
    
    /// <summary>
    /// Record current tire wear state
    /// </summary>
    private void RecordTireWear(TelemetryData telemetry, int lapNumber)
    {
        var snapshot = new TireWearSnapshot
        {
            LapNumber = lapNumber,
            LFwear = (telemetry.LFwearL + telemetry.LFwearM + telemetry.LFwearR) / 3f,
            RFwear = (telemetry.RFwearL + telemetry.RFwearM + telemetry.RFwearR) / 3f,
            LRwear = (telemetry.LRwearL + telemetry.LRwearM + telemetry.LRwearR) / 3f,
            RRwear = (telemetry.RRwearL + telemetry.RRwearM + telemetry.RRwearR) / 3f,
            LFtempM = telemetry.LFtempCM,
            RFtempM = telemetry.RFtempCM,
            LRtempM = telemetry.LRtempCM,
            RRtempM = telemetry.RRtempCM,
            Timestamp = DateTime.UtcNow
        };
        
        _wearHistory.Enqueue(snapshot);
        if (_wearHistory.Count > 50)
            _wearHistory.Dequeue();
    }
    
    /// <summary>
    /// Calculate tire wear rates (percent per lap)
    /// </summary>
    private void CalculateTireWearRates(TireStrategy strategy)
    {
        var snapshots = _wearHistory.ToArray();
        if (snapshots.Length < 3)
            return;
        
        var first = snapshots[0];
        var last = snapshots[^1];
        int lapsDelta = last.LapNumber - first.LapNumber;
        
        if (lapsDelta <= 0)
            return;
        
        // Calculate wear rate for each tire (percent per lap)
        strategy.LFwearRate = (last.LFwear - first.LFwear) / lapsDelta;
        strategy.RFwearRate = (last.RFwear - first.RFwear) / lapsDelta;
        strategy.LRwearRate = (last.LRwear - first.LRwear) / lapsDelta;
        strategy.RRwearRate = (last.RRwear - first.RRwear) / lapsDelta;
        
        // Find maximum wear rate (limiting tire)
        strategy.MaxWearRate = Math.Max(
            Math.Max(strategy.LFwearRate, strategy.RFwearRate),
            Math.Max(strategy.LRwearRate, strategy.RRwearRate));
        
        // Calculate current tire life remaining (100% = new, 0% = cord)
        var current = snapshots[^1];
        float minLife = Math.Min(
            Math.Min(100f - current.LFwear, 100f - current.RFwear),
            Math.Min(100f - current.LRwear, 100f - current.RRwear));
        
        strategy.TireLifeRemaining = minLife / 100f; // 0.0 to 1.0
    }
    
    /// <summary>
    /// Predict when tires will need changing
    /// </summary>
    private void PredictTirePitLap(TireStrategy strategy, TelemetryData telemetry, int currentLap)
    {
        if (strategy.MaxWearRate <= 0)
            return;
        
        // Find current maximum wear (average L/M/R for each tire)
        float lfWear = (telemetry.LFwearL + telemetry.LFwearM + telemetry.LFwearR) / 3f;
        float rfWear = (telemetry.RFwearL + telemetry.RFwearM + telemetry.RFwearR) / 3f;
        float lrWear = (telemetry.LRwearL + telemetry.LRwearM + telemetry.LRwearR) / 3f;
        float rrWear = (telemetry.RRwearL + telemetry.RRwearM + telemetry.RRwearR) / 3f;
        
        float maxWear = Math.Max(
            Math.Max(lfWear, rfWear),
            Math.Max(lrWear, rrWear));
        
        // Assume tire change needed at 95% wear (conservative)
        float wearRemaining = 95f - maxWear;
        
        if (wearRemaining <= 0)
        {
            strategy.TirePitLap = currentLap; // Need tires NOW
            strategy.TireLapsRemaining = 0;
        }
        else
        {
            float lapsRemaining = wearRemaining / strategy.MaxWearRate;
            strategy.TireLapsRemaining = (int)Math.Floor(lapsRemaining);
            strategy.TirePitLap = currentLap + strategy.TireLapsRemaining;
        }
    }
    
    /// <summary>
    /// Coordinate tire and fuel pit strategies
    /// Returns the optimal combined pit lap
    /// </summary>
    public int CalculateCombinedPitStrategy(
        TireStrategy tireStrategy,
        int fuelOptimalPitLap,
        int fuelEarliestPitLap,
        int fuelLatestPitLap,
        int currentLap)
    {
        // If no tire data, use fuel strategy
        if (tireStrategy.TirePitLap <= 0)
            return fuelOptimalPitLap;
        
        // If tire pit is before fuel earliest, pit for tires
        if (tireStrategy.TirePitLap < fuelEarliestPitLap)
            return tireStrategy.TirePitLap;
        
        // If tire pit is after fuel latest, pit for fuel
        if (tireStrategy.TirePitLap > fuelLatestPitLap)
            return fuelOptimalPitLap;
        
        // Tire pit is within fuel window - use tire lap for combined stop
        return tireStrategy.TirePitLap;
    }
    
    /// <summary>
    /// Reset tire tracking
    /// </summary>
    public void Reset()
    {
        _wearHistory.Clear();
        _lastLapRecorded = -1;
    }
}

/// <summary>
/// Tire wear snapshot at a specific lap
/// </summary>
public class TireWearSnapshot
{
    public int LapNumber { get; set; }
    public float LFwear { get; set; }
    public float RFwear { get; set; }
    public float LRwear { get; set; }
    public float RRwear { get; set; }
    public float LFtempM { get; set; }
    public float RFtempM { get; set; }
    public float LRtempM { get; set; }
    public float RRtempM { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Tire strategy calculation results
/// </summary>
public class TireStrategy
{
    // Wear rates (percent per lap)
    public float LFwearRate { get; set; }
    public float RFwearRate { get; set; }
    public float LRwearRate { get; set; }
    public float RRwearRate { get; set; }
    public float MaxWearRate { get; set; }
    
    // Tire life
    public float TireLifeRemaining { get; set; } // 0.0 to 1.0
    public int TireLapsRemaining { get; set; }
    public int TirePitLap { get; set; }
    
    // Combined strategy
    public bool NeedTireChange { get; set; }
    public bool CombinedStopRecommended { get; set; }
}
