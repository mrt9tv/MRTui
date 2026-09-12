namespace iRacingOverlay.Core.Models;

/// <summary>How the driver had the car set at the moment the lights went out.</summary>
public enum LaunchTechnique
{
    /// <summary>Not yet classified.</summary>
    Unknown,

    /// <summary>In gear with the clutch pedal down; the launch is the clutch release.</summary>
    Clutch,

    /// <summary>Waiting in neutral; the launch is the shift into gear.</summary>
    NeutralToGear,

    /// <summary>In gear, clutch already up (auto-clutch, anti-stall, or a paddle car); the launch is the throttle.</summary>
    ThrottleOnly,

    /// <summary>Rolling start — already moving, so the "launch" is a throttle increase.</summary>
    Rolling,
}

/// <summary>Which input moved first after the lights.</summary>
public enum LaunchInput
{
    None,
    Throttle,
    Clutch,
    Gear,
}

/// <summary>
/// One race start, measured against the sim clock.
///
/// Telemetry arrives at 60 Hz, so both the go-light frame and the input frame
/// carry up to ~16 ms of uncertainty. Two decimals is the honest precision.
/// </summary>
public sealed class RaceStartResult
{
    /// <summary>Seconds from lights-out to the first input. Zero when a jump start pre-empted it.</summary>
    public float ReactionSeconds { get; init; }

    /// <summary>Seconds from lights-out to the car first moving. Zero if it had not moved by the timeout.</summary>
    public float LaunchSeconds { get; init; }

    /// <summary>The car moved before the lights went out.</summary>
    public bool JumpStart { get; init; }

    /// <summary>How the driver had the car set when the lights went out.</summary>
    public LaunchTechnique Technique { get; init; }

    /// <summary>Which input registered first.</summary>
    public LaunchInput FirstInput { get; init; }

    /// <summary>Gear selected at lights-out (0 = neutral).</summary>
    public int GearAtGo { get; init; }

    /// <summary>Best reaction of the session so far, this start included. Zero when this is the first.</summary>
    public float SessionBestSeconds { get; init; }

    /// <summary>Short label for the technique: "clutch", "N→1st", "throttle", "rolling".</summary>
    public string TechniqueLabel => Technique switch
    {
        LaunchTechnique.Clutch => "clutch",
        LaunchTechnique.NeutralToGear => "N→gear",
        LaunchTechnique.ThrottleOnly => "throttle",
        LaunchTechnique.Rolling => "rolling",
        _ => "",
    };
}
