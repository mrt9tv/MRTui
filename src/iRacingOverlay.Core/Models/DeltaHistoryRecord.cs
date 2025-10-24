namespace iRacingOverlay.Core.Models;

/// <summary>
/// Records lap-by-lap delta between our fuel prediction and iRacing's prediction
/// Used for convergence tracking, confidence analysis, and historical accuracy
/// </summary>
public class DeltaHistoryRecord
{
    /// <summary>Lap number when delta was recorded</summary>
    public int LapNumber { get; set; }
    
    /// <summary>Our calculated laps remaining</summary>
    public float OurLapsRemaining { get; set; }
    
    /// <summary>iRacing's calculated laps remaining</summary>
    public float IRacingLapsRemaining { get; set; }
    
    /// <summary>Delta between calculations (OurLaps - iRacingLaps)</summary>
    public float Delta { get; set; }
    
    /// <summary>Absolute delta (for accuracy comparisons)</summary>
    public float AbsoluteDelta => Math.Abs(Delta);
    
    /// <summary>Our averaging method used for this prediction</summary>
    public FuelAveragingMethod MethodUsed { get; set; }
    
    /// <summary>Current fuel level when prediction was made</summary>
    public float CurrentFuel { get; set; }
    
    /// <summary>Race laps remaining when prediction was made</summary>
    public int RaceLapsRemaining { get; set; }
    
    /// <summary>Timestamp of prediction</summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>Actual laps completed from this point (filled in post-race for accuracy analysis)</summary>
    public int? ActualLapsCompleted { get; set; }
    
    /// <summary>Was our prediction closer to actual? (filled in post-race)</summary>
    public bool? WasOurPredictionBetter { get; set; }
}
