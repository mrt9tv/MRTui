namespace iRacingOverlay.Core.Models;

/// <summary>
/// Lateral position relative to player (left/right detection)
/// Maps to iRacing SDK CarLeftRight enum values
/// 
/// CRITICAL: SDK enum starts at 0=Off, 1=Clear (NOT 0=Clear as previously assumed!)
/// Source: https://github.com/SVappsLAB/iRacingTelemetrySDK/blob/main/Sdk/SVappsLAB.iRacingTelemetrySDK.EnumsAndFlags/TelemetryClient_Enums.cs#L81-L88
/// 
/// SDK Definition:
///   Off = 0           (spotter system disabled)
///   Clear = 1         (no cars beside player) ← THIS IS THE KEY!
///   CarLeft = 2       (car on left side)
///   CarRight = 3      (car on right side)
///   CarLeftRight = 4  (cars on both sides)
///   TwoCarsLeft = 5   (two cars on left)
///   TwoCarsRight = 6  (two cars on right)
/// </summary>
public enum LateralPosition
{
    /// <summary>
    /// Spotter system OFF or disabled (SDK value 0)
    /// Should rarely be seen during active racing
    /// </summary>
    Off = 0,
    
    /// <summary>
    /// No cars beside player (SDK value 1)
    /// This is the "all clear" state we've been looking for!
    /// </summary>
    Clear = 1,
    
    /// <summary>
    /// Car(s) on LEFT side of player (SDK value 2)
    /// </summary>
    CarLeft = 2,
    
    /// <summary>
    /// Car(s) on RIGHT side of player (SDK value 3)
    /// </summary>
    CarRight = 3,
    
    /// <summary>
    /// Cars on BOTH sides of player (SDK value 4)
    /// </summary>
    CarBothSides = 4,
    
    /// <summary>
    /// TWO cars on LEFT side (SDK value 5)
    /// Currently treated same as CarLeft for UI purposes
    /// </summary>
    TwoCarsLeft = 5,
    
    /// <summary>
    /// TWO cars on RIGHT side (SDK value 6)
    /// Currently treated same as CarRight for UI purposes
    /// </summary>
    TwoCarsRight = 6
}
