namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Dynamic buffer calculation results
/// Calculates fuel buffer based on race conditions
/// </summary>
public class BufferData
{
    public float TotalBuffer { get; set; }
    public string Reason { get; set; } = "";
    
    // Individual factors
    public float ConsistencyFactor { get; set; }
    public float PositionFactor { get; set; }
    public float WeatherFactor { get; set; }
    public float YellowFlagFactor { get; set; }
    public float EndOfRaceFactor { get; set; }
}
