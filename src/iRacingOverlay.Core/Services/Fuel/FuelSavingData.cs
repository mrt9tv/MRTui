namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Fuel saving calculation results
/// Contains target fuel reduction, progress tracking, and strategic recommendations
/// </summary>
public class FuelSavingData
{
    // Fuel saving mode
    public bool NeedsFuelSaving { get; set; }
    public float FuelSavingTarget { get; set; }
    public float CurrentSavingRate { get; set; }
    public float SavingProgress { get; set; }
    public bool CanSaveFuelToFinish { get; set; }
    public bool FuelSavingWorking { get; set; }
    
    // Strategy comparison (pit vs save)
    public bool IsPittingFaster { get; set; }
    public float StrategyTimeDelta { get; set; }
    
    // Strategic alerts
    public string? StrategicAlert { get; set; }
    public int AlertSeverity { get; set; } // 0=Info, 1=Warning, 2=Critical
    
    // Historical context
    public bool HasHistoricalData { get; set; }
    public string? HistoricalContext { get; set; }
}
