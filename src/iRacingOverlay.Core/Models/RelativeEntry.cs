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
    /// Used for OFF TRACK indicator in status column.
    /// </summary>
    public bool IsOffTrack { get; set; }

    /// <summary>
    /// Continuous seconds the car has been off-track.
    /// &gt; 0.5s to show OFF TRACK indicator, &gt; 2.0s to flash it.
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

    /// <summary>
    /// Detailed pit status based on TrackSurface enum.
    /// None = not pitting, Approaching = entering pit lane, InStall = at pit box,
    /// Exiting = leaving pit lane back to track.
    /// </summary>
    public PitStatus PitState { get; set; } = PitStatus.None;

    /// <summary>
    /// Whether this driver's last lap is their personal best.
    /// Used for teal coloring in LAST column.
    /// </summary>
    public bool IsPersonalBest { get; set; }

    /// <summary>
    /// Whether this driver's last lap is the session best (overall fastest).
    /// Used for purple coloring in LAST column.
    /// </summary>
    public bool IsSessionBest { get; set; }

    /// <summary>
    /// iRating of the driver (from session YAML). 0 = unavailable.
    /// </summary>
    public int IRating { get; set; }

    /// <summary>
    /// Safety rating of the driver (e.g., 3.45). 0 = unavailable.
    /// </summary>
    public float SafetyRating { get; set; }

    /// <summary>
    /// License class letter (e.g., "A", "B", "C", "D", "R", "Pro", "WC").
    /// </summary>
    public string LicenseClass { get; set; } = string.Empty;

    /// <summary>
    /// Whether this car has a meatball/repair flag (CarIdxSessionFlags bit 0x100000).
    /// </summary>
    public bool HasMeatball { get; set; }

    /// <summary>
    /// Whether this car has a black flag (CarIdxSessionFlags bit 0x10000).
    /// </summary>
    public bool HasBlackFlag { get; set; }

    /// <summary>
    /// Whether this car gained incidents recently (CurDriverIncidentCount increased).
    /// Auto-clears after approximately 8 seconds.
    /// </summary>
    public bool HasRecentIncident { get; set; }

    /// <summary>
    /// Cumulative incident count for this driver (from session info).
    /// </summary>
    public int IncidentCount { get; set; }

    /// <summary>
    /// How many incidents gained in the last incident event (e.g. 2 = 2x, 4 = 4x).
    /// Only set when HasRecentIncident is true.
    /// </summary>
    public int IncidentDelta { get; set; }

    /// <summary>
    /// Whether this car was towed to pits (went from on-track to pit stall
    /// without going through pit road approach).
    /// </summary>
    public bool WasTowed { get; set; }

    /// <summary>
    /// Car model short name (3-letter abbreviation from session YAML).
    /// </summary>
    public string CarModel { get; set; } = string.Empty;

    /// <summary>
    /// Whether this car is on an out-lap after a pit stop with tire change.
    /// Shows "OUTLAP" in status column.
    /// </summary>
    public bool IsOnOutLap { get; set; }

    /// <summary>
    /// UTC time when the car entered the pit stall (InPitStall surface).
    /// Used to compute BOX MM:SS timer.
    /// </summary>
    public DateTime? PitStallEntryTime { get; set; }

    /// <summary>
    /// Elapsed seconds in pit stall (only valid when PitState == InPit).
    /// </summary>
    public float PitStallDuration { get; set; }

    /// <summary>
    /// The final BOX duration when the driver exited the pit (seconds).
    /// Used to blink the time for 3 seconds after leaving the box.
    /// </summary>
    public float FinalBoxDuration { get; set; }

    /// <summary>
    /// Elapsed seconds since the driver started exiting pit.
    /// Used to control the 3-second blink duration.
    /// </summary>
    public float ExitingPitDuration { get; set; }

    /// <summary>
    /// How far through the outlap this car is (0.0 - 1.0).
    /// Used for dimming/hiding OUTLAP indicator past 85%.
    /// </summary>
    public float OutLapProgress { get; set; }

    // ── New Features ────────────────────────────────────────────────

    /// <summary>Number of pit stops this driver has made during the session.</summary>
    public int PitStopCount { get; set; }

    /// <summary>
    /// Closing rate in seconds per lap (positive = closing on you, negative = pulling away).
    /// Computed from interval trend over last 3-5 completed laps.
    /// </summary>
    public float ClosingRate { get; set; }

    /// <summary>
    /// Position change vs 5 laps ago (positive = gained positions, e.g. P8→P5 = +3).
    /// </summary>
    public int PositionDelta { get; set; }

    /// <summary>Whether the gap to this car is currently shrinking compared to previous lap.</summary>
    public bool IsGapClosing { get; set; }

    /// <summary>2-letter country code from iRacing Club data (approximate mapping).</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>Whether this entry is the safety/pace car. Display as "SC" with no driver info.</summary>
    public bool IsSafetyCar { get; set; }

    // ── CarIdx telemetry (complete SDK coverage) ─────────────────

    /// <summary>Laps completed by this car (CarIdxLapCompleted).</summary>
    public int LapCompleted { get; set; }

    /// <summary>Lap number of this car's best lap (CarIdxBestLapNum).</summary>
    public int BestLapNum { get; set; }

    /// <summary>Fast repairs used by this car (CarIdxFastRepairsUsed).</summary>
    public int FastRepairsUsed { get; set; }

    /// <summary>Current tire compound index (CarIdxTireCompound). 0 = default/unknown.</summary>
    public int TireCompound { get; set; }

    /// <summary>Qualifying tire compound index (CarIdxQualTireCompound).</summary>
    public int QualTireCompound { get; set; }

    /// <summary>Track surface material under this car (CarIdxTrackSurfaceMaterial enum).</summary>
    public int TrackSurfaceMaterial { get; set; }

    /// <summary>Push-to-pass uses remaining (CarIdxP2P_Count).</summary>
    public int P2P_Count { get; set; }

    /// <summary>Push-to-pass currently active (CarIdxP2P_Status).</summary>
    public bool P2P_Active { get; set; }

    /// <summary>Pace flags for this car (CarIdxPaceFlags).</summary>
    public int PaceFlags { get; set; }

    /// <summary>Pace line assignment (CarIdxPaceLine).</summary>
    public int PaceLine { get; set; }

    /// <summary>Pace row assignment (CarIdxPaceRow).</summary>
    public int PaceRow { get; set; }
}

/// <summary>
/// Detailed pit status derived from iRacing TrackSurface + OnPitRoad.
/// </summary>
public enum PitStatus
{
    /// <summary>Not on pit road</summary>
    None,
    /// <summary>Entering pit lane (OnPitRoad=true, TrackSurface=ApproachingPits or OnTrack)</summary>
    Pitting,
    /// <summary>Stopped in pit box (TrackSurface=InPitStall)</summary>
    InPit,
    /// <summary>Leaving pit stall but still on pit road</summary>
    ExitingPit
}
