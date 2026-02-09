namespace iRacingOverlay.Core.Models;

/// <summary>
/// Represents one row in the Relative display.
/// Sorted by proximity to the player on track.
/// </summary>
public class RelativeEntry
{
    /// <summary>Car index in the session (0-63)</summary>
    public int CarIdx { get; set; }

    /// <summary>Driver name (raw from session YAML)</summary>
    public string DriverName { get; set; } = string.Empty;

    /// <summary>Car number string (e.g., "44", "99")</summary>
    public string CarNumber { get; set; } = string.Empty;

    /// <summary>Overall race position (1-based, 0 = not classified)</summary>
    public int OverallPosition { get; set; }

    /// <summary>Class position (1-based, 0 = not classified)</summary>
    public int ClassPosition { get; set; }

    /// <summary>Car class ID (for class color stripe)</summary>
    public int CarClassId { get; set; }

    /// <summary>
    /// Time interval to the player in seconds.
    /// Positive = car is ahead on track. Negative = car is behind on track.
    /// Zero = player's own entry.
    /// </summary>
    public float IntervalToPlayer { get; set; }

    /// <summary>Last lap time in seconds (0 or negative = no time set)</summary>
    public float LastLapTime { get; set; }

    /// <summary>Best lap time in seconds (0 or negative = no time set)</summary>
    public float BestLapTime { get; set; }

    /// <summary>Whether the car is currently on pit road</summary>
    public bool IsOnPitRoad { get; set; }

    /// <summary>Whether this entry is the player</summary>
    public bool IsPlayer { get; set; }

    /// <summary>Current lap number</summary>
    public int LapNumber { get; set; }

    /// <summary>Track position percentage (0.0-1.0)</summary>
    public float LapDistPct { get; set; }

    /// <summary>
    /// Whether the car is connected and actively on-track.
    /// False = disconnected or not in world (should be dimmed in UI).
    /// </summary>
    public bool IsConnected { get; set; } = true;

    /// <summary>
    /// Whether the car is currently off-track (iRacing TrackSurface = OffTrack).
    /// Used for OFF indicator in status column.
    /// </summary>
    public bool IsOffTrack { get; set; }

    /// <summary>
    /// Continuous seconds the car has been off-track.
    /// &gt; 0.5s to show OFF indicator, &gt; 2.0s to flash it.
    /// Reset to 0 when back on track.
    /// </summary>
    public float OffTrackDuration { get; set; }

    /// <summary>
    /// Gap to the car directly ahead in race position (seconds).
    /// Positive value. Zero if this is the leader or unavailable.
    /// </summary>
    public float GapToCarAhead { get; set; }

    /// <summary>
    /// Lap delta relative to player.
    /// Positive = car is N laps ahead. Negative = car is N laps behind (lapped).
    /// Zero = same lap.
    /// </summary>
    public int LapDelta { get; set; }
}
