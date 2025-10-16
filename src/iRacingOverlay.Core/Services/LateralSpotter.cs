using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Simple wrapper for lateral spotter data (left/right car detection)
/// Maps iRacing SDK CarLeftRight enum to our LateralPosition enum
/// </summary>
public class LateralSpotter
{
    /// <summary>
    /// Get lateral position from telemetry data
    /// </summary>
    /// <param name="data">Telemetry data</param>
    /// <returns>Lateral position enum</returns>
    public LateralPosition GetLateralPosition(TelemetryData data)
    {
        // CarLeftRight is stored as int in TelemetryData (cast from SDK enum)
        // CORRECT SDK Values: 0=Off, 1=Clear, 2=CarLeft, 3=CarRight, 4=CarBothSides, 5=TwoCarsLeft, 6=TwoCarsRight
        return (LateralPosition)data.CarLeftRight;
    }
    
    /// <summary>
    /// Check if any car is on the left side
    /// </summary>
    public bool HasCarLeft(TelemetryData data)
    {
        var position = GetLateralPosition(data);
        return position == LateralPosition.CarLeft || position == LateralPosition.CarBothSides;
    }
    
    /// <summary>
    /// Check if any car is on the right side
    /// </summary>
    public bool HasCarRight(TelemetryData data)
    {
        var position = GetLateralPosition(data);
        return position == LateralPosition.CarRight || position == LateralPosition.CarBothSides;
    }
    
    /// <summary>
    /// Check if any car is beside the player (left or right)
    /// </summary>
    public bool HasCarBeside(TelemetryData data)
    {
        return GetLateralPosition(data) != LateralPosition.Clear;
    }
    
    /// <summary>
    /// Get human-readable description of lateral position
    /// </summary>
    public string GetPositionDescription(TelemetryData data)
    {
        return GetLateralPosition(data) switch
        {
            LateralPosition.Clear => "Clear",
            LateralPosition.CarLeft => "Car on LEFT",
            LateralPosition.CarRight => "Car on RIGHT",
            LateralPosition.CarBothSides => "Cars on BOTH SIDES",
            _ => "Unknown"
        };
    }
}
