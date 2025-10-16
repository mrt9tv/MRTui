namespace iRacingOverlay.Core.Models;

/// <summary>
/// Distance zones for proximity radar (meter-based)
/// </summary>
public enum ProximityZone
{
    /// <summary>
    /// No cars in detection range
    /// </summary>
    Clear = 0,
    
    /// <summary>
    /// Car far away (>16m) - SAFE
    /// </summary>
    Far = 1,
    
    /// <summary>
    /// Car at careful distance (12-16m) - CAREFUL
    /// </summary>
    Careful = 2,
    
    /// <summary>
    /// Car at medium distance (7-12m) - CAUTION
    /// </summary>
    Near = 3,
    
    /// <summary>
    /// Car close (4-7m) - WARNING
    /// </summary>
    Close = 4,
    
    /// <summary>
    /// Car very close (<4m) - CRITICAL (blinking)
    /// </summary>
    VeryClose = 5
}
