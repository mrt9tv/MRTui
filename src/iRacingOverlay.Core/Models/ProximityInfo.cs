namespace iRacingOverlay.Core.Models;

/// <summary>
/// Proximity information for a single car relative to player
/// </summary>
public class ProximityInfo
{
    /// <summary>
    /// Car index (0-63) in session
    /// </summary>
    public int CarIdx { get; set; }
    
    /// <summary>
    /// Relative distance in METERS (positive = ahead, negative = behind)
    /// This is the actual distance around the track considering wrap-around.
    /// </summary>
    public float RelativeDistance { get; set; }
    
    /// <summary>
    /// Absolute distance in METERS (always positive)
    /// This is |RelativeDistance| used for sorting by proximity.
    /// </summary>
    public float AbsoluteDistance { get; set; }
    
    /// <summary>
    /// Race position of the car
    /// </summary>
    public int Position { get; set; }
    
    /// <summary>
    /// Class position of the car
    /// </summary>
    public int ClassPosition { get; set; }
    
    /// <summary>
    /// Car class ID
    /// </summary>
    public int CarClass { get; set; }
    
    /// <summary>
    /// Current lap number
    /// </summary>
    public int Lap { get; set; }
    
    /// <summary>
    /// Is the car on pit road?
    /// </summary>
    public bool OnPitRoad { get; set; }
    
    /// <summary>
    /// Distance zone classification
    /// </summary>
    public ProximityZone Zone { get; set; }
    
    /// <summary>
    /// Is this car ahead of the player?
    /// </summary>
    public bool IsAhead => RelativeDistance > 0;
    
    /// <summary>
    /// Is this car behind the player?
    /// </summary>
    public bool IsBehind => RelativeDistance < 0;
}
