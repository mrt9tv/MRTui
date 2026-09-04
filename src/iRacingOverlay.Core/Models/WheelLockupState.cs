namespace iRacingOverlay.Core.Models;

/// <summary>
/// Wheel lockup detection result with granular per-wheel information
/// </summary>
public class WheelLockupState
{
    // Individual wheel lockup flags
    public bool LeftFrontLocked { get; set; }
    public bool RightFrontLocked { get; set; }
    public bool LeftRearLocked { get; set; }
    public bool RightRearLocked { get; set; }
    
    // Aggregate flags
    public bool FrontAxleLockup { get; set; }
    public bool RearAxleLockup { get; set; }
    public bool AnyWheelLocked { get; set; }
    
    // Lock attempt flags (wheels would lock without ABS)
    public bool LeftFrontLockAttempted { get; set; }
    public bool RightFrontLockAttempted { get; set; }
    public bool LeftRearLockAttempted { get; set; }
    public bool RightRearLockAttempted { get; set; }
    
    // ABS information
    public bool ABSActive { get; set; }
    public bool ABSPreventingLockup { get; set; }
    
    // Detection metadata
    public LockupDetectionMethod DetectionMethod { get; set; }
    public LockupConfidence Confidence { get; set; }
    
    /// <summary>
    /// Copy every field from another state. Used to publish a result without
    /// handing out an internal scratch object that later calls will mutate.
    /// </summary>
    public void CopyFrom(WheelLockupState other)
    {
        LeftFrontLocked = other.LeftFrontLocked;
        RightFrontLocked = other.RightFrontLocked;
        LeftRearLocked = other.LeftRearLocked;
        RightRearLocked = other.RightRearLocked;
        FrontAxleLockup = other.FrontAxleLockup;
        RearAxleLockup = other.RearAxleLockup;
        AnyWheelLocked = other.AnyWheelLocked;
        LeftFrontLockAttempted = other.LeftFrontLockAttempted;
        RightFrontLockAttempted = other.RightFrontLockAttempted;
        LeftRearLockAttempted = other.LeftRearLockAttempted;
        RightRearLockAttempted = other.RightRearLockAttempted;
        ABSActive = other.ABSActive;
        ABSPreventingLockup = other.ABSPreventingLockup;
        DetectionMethod = other.DetectionMethod;
        Confidence = other.Confidence;
    }

    /// <summary>Reset all fields to defaults for reuse (avoid heap allocation)</summary>
    public void Reset()
    {
        LeftFrontLocked = false;
        RightFrontLocked = false;
        LeftRearLocked = false;
        RightRearLocked = false;
        FrontAxleLockup = false;
        RearAxleLockup = false;
        AnyWheelLocked = false;
        LeftFrontLockAttempted = false;
        RightFrontLockAttempted = false;
        LeftRearLockAttempted = false;
        RightRearLockAttempted = false;
        ABSActive = false;
        ABSPreventingLockup = false;
        DetectionMethod = LockupDetectionMethod.None;
        Confidence = LockupConfidence.None;
    }
}

/// <summary>
/// Detection method used to identify lockup
/// </summary>
public enum LockupDetectionMethod
{
    None,
    OdometerBased,      // Wheel rotation rate vs car speed
    ABS,                // ABS system active
    PressureCollapse,   // Brake pressure collapse
    PressureImbalance,  // Individual wheel pressure drop
    DecelerationPlateau,// Deceleration plateau/drop
    TireRumble          // Tire rumble pitch spike
}

/// <summary>
/// Confidence level of lockup detection
/// </summary>
public enum LockupConfidence
{
    None,
    Low,
    Medium,
    High
}
