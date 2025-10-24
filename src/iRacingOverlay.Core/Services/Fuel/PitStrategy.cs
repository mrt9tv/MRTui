namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Pit strategy calculation results
/// Contains optimal pit lap, fuel to add, position predictions, and multi-stop analysis
/// </summary>
public class PitStrategy
{
    // Basic strategy
    public int OptimalPitLap { get; set; }
    public string? OptimalPitReason { get; set; }
    public float FuelToAddAtPit { get; set; }
    public bool CanFinishWithoutStop { get; set; }
    
    // Pit windows
    public int EarliestPitLap { get; set; }
    public int LatestPitLap { get; set; }
    public int PitWindowStart { get; set; }
    public int PitWindowEnd { get; set; }
    public string? PitWindowReason { get; set; }
    
    // Position prediction
    public int PitExitPosition { get; set; }
    public string? PitExitGapDescription { get; set; }
    public bool PitExitPositionValid { get; set; }
    public int RacePosition { get; set; }
    public int TotalCars { get; set; }
    
    // Multi-stop strategy
    public float OneStopTotalTime { get; set; }
    public float TwoStopTotalTime { get; set; }
    public float ThreeStopTotalTime { get; set; }
    public string? MultiStopRecommendation { get; set; }
    public string? MultiStopComparison { get; set; }
    
    // Partial refuel optimization
    public float PartialRefuelAmount { get; set; }
    public float PartialRefuelTimeSaved { get; set; }
    public string? PartialRefuelRecommendation { get; set; }
    
    // Analysis factors
    public float FuelCriticalityScore { get; set; }
    public float TrackPositionCost { get; set; }
    public float YellowFlagProbability { get; set; }
    public int EstimatedLapsUntilYellow { get; set; }
}
