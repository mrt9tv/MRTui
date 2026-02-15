namespace iRacingOverlay.Core.Models;

/// <summary>
/// A transient event detected near the player (14s ahead, 7s behind on track).
/// Displayed by the Proximity Feed widget as an animated notification.
/// IntervalToPlayer is live-updated each tick for active events.
/// </summary>
public class NearbyEvent
{
    /// <summary>Unique ID for deduplication and animation tracking.</summary>
    public long Id { get; set; }

    /// <summary>Car index that triggered this event (0-63).</summary>
    public int CarIdx { get; set; }

    /// <summary>Driver name (formatted for display).</summary>
    public string DriverName { get; set; } = string.Empty;

    /// <summary>Car number string.</summary>
    public string CarNumber { get; set; } = string.Empty;

    /// <summary>Class ID for color stripe.</summary>
    public int CarClassId { get; set; }

    /// <summary>Type of event detected.</summary>
    public NearbyEventType EventType { get; set; }

    /// <summary>Time interval to player in seconds (positive = ahead, negative = behind).</summary>
    public float IntervalToPlayer { get; set; }

    /// <summary>Brief display text (e.g., "OFF TRACK", "PITTING", "SLOW").</summary>
    public string DisplayText { get; set; } = string.Empty;

    /// <summary>UTC timestamp when this event was first detected.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>How long this event should remain visible (seconds).</summary>
    public float DisplayDuration { get; set; } = 4.0f;

    /// <summary>Severity for visual priority (higher = more prominent).</summary>
    public NearbyEventSeverity Severity { get; set; } = NearbyEventSeverity.Info;

    /// <summary>Whether this event is ahead (+) or behind (-) the player.</summary>
    public bool IsAhead => IntervalToPlayer > 0;

    /// <summary>
    /// Whether the underlying condition is still active (e.g. car still off-track).
    /// Ongoing events never expire by timer; they persist until the condition clears,
    /// at which point IsOngoing is set false and normal timer expiry resumes.
    /// </summary>
    public bool IsOngoing { get; set; }

    /// <summary>
    /// When the ongoing condition cleared, giving us a timestamp for fade-out timing.
    /// Null while the condition is still active.
    /// </summary>
    public DateTime? ClearedAt { get; set; }

    /// <summary>Elapsed seconds since creation.</summary>
    public double Age => (DateTime.UtcNow - CreatedAt).TotalSeconds;

    /// <summary>
    /// Elapsed seconds since the condition cleared (0 while still ongoing).
    /// Used for fade-out timer after an ongoing event's condition ends.
    /// </summary>
    public double AgeSinceCleared => ClearedAt.HasValue
        ? (DateTime.UtcNow - ClearedAt.Value).TotalSeconds
        : 0;

    /// <summary>Whether the event has exceeded its display duration.</summary>
    public bool IsExpired => IsOngoing ? false
        : ClearedAt.HasValue ? AgeSinceCleared > DisplayDuration
        : Age > DisplayDuration;

    /// <summary>
    /// Whether this event has been escalated to INCIDENT status due to
    /// repeated dangerous events from the same car in a short time window.
    /// When true, the UI renders with enhanced visuals (blink, color escalation).
    /// </summary>
    public bool IsIncident { get; set; }
}

/// <summary>
/// Categories of nearby events detected from telemetry.
/// </summary>
public enum NearbyEventType
{
    /// <summary>Car went off track (TrackSurface = OffTrack).</summary>
    OffTrack,

    /// <summary>Car-on-car contact detected (incident count jumped by 2+).</summary>
    Collision,

    /// <summary>Car is significantly slower than expected (possible spin/stall).</summary>
    SlowCar,

    /// <summary>Car entered pit road.</summary>
    Pitting,

    /// <summary>Car is in pit stall (being serviced).</summary>
    InBox,

    /// <summary>Car exiting pit lane (on out-lap).</summary>
    PitExit,

    /// <summary>Car received meatball/repair flag.</summary>
    MeatballFlag,

    /// <summary>Car received black flag.</summary>
    BlackFlag,

    /// <summary>Car was towed back to pits.</summary>
    Towed,

    /// <summary>Yellow/caution flag in the area.</summary>
    LocalYellow,

    /// <summary>Car has stopped on track (speed near zero while on track surface).</summary>
    Stopped,

    /// <summary>Car spun (large yaw rate change — detected via rapid LapDistPct stall + direction reversal).</summary>
    Spin,

    /// <summary>Higher-class car overtaking imminently (closing fast from behind).</summary>
    OvertakingImminent,

    /// <summary>Car has been disqualified (DSQ flag 0x20000).</summary>
    Disqualified,

    /// <summary>Car is about to be overlapped — blue flag shown or faster-class closing from behind.</summary>
    BlueFlagged,

    /// <summary>Safety car / full-course caution is active.</summary>
    SafetyCar,

    /// <summary>Race start sequence (Ready / Set / Go).</summary>
    StartSequence,

    /// <summary>Checkered flag has been shown — race is ending.</summary>
    CheckeredFlag,

    /// <summary>White flag — final lap.</summary>
    WhiteFlag,

    /// <summary>Red flag — session stopped.</summary>
    RedFlag,

    /// <summary>Per-car pace flag: driver sent to end of restart line.</summary>
    PaceEndOfLine,

    /// <summary>Per-car pace flag: driver given a free pass around the pace car.</summary>
    PaceFreePass,

    /// <summary>Per-car pace flag: driver told to wave around the pace car.</summary>
    PaceWaveAround,

    /// <summary>Player is stopped/slow and a fast car is closing from behind.</summary>
    IncomingFast,
}

/// <summary>
/// Visual severity for event display priority and styling.
/// </summary>
public enum NearbyEventSeverity
{
    /// <summary>Informational — subtle appearance.</summary>
    Info,

    /// <summary>Warning — moderate emphasis (off-track, pitting).</summary>
    Warning,

    /// <summary>Danger — high emphasis (collision, stopped car, yellow).</summary>
    Danger,

    /// <summary>Critical — maximum emphasis (car stopped ahead, imminent hazard).</summary>
    Critical,
}
