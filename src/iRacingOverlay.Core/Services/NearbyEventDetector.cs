using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Detects race events near the player (configurable range, default 12s ahead / 6s behind).
/// Monitors all 64 car slots for state changes and emits NearbyEvent
/// notifications for the Proximity Feed widget.
/// Live-updates IntervalToPlayer on active events each tick.
///
/// Performance: O(64) per tick, no allocations on steady-state (object pool).
/// Thread safety: NOT thread-safe. Call Update() from UI dispatch only.
/// </summary>
public sealed class NearbyEventDetector
{
    private const int MAX_CARS = 64;

    /// <summary>Detection radius in seconds AHEAD of the player (default 12s, adjustable).</summary>
    public float DetectionAheadSeconds { get; set; } = 12.0f;

    /// <summary>Detection radius in seconds BEHIND the player (default 6s, adjustable).</summary>
    public float DetectionBehindSeconds { get; set; } = 6.0f;

    /// <summary>Minimum speed (m/s) below which a car on track is considered "slow".</summary>
    private const float SLOW_SPEED_THRESHOLD = 5.0f;

    /// <summary>Speed below which a car on track is "stopped" (m/s). ~5.4 km/h.</summary>
    private const float STOPPED_SPEED_THRESHOLD = 1.5f;

    /// <summary>Minimum seconds off-track before emitting event (avoids curb touches).</summary>
    private const float OFF_TRACK_MIN_DURATION = 0.5f;

    /// <summary>Seconds a "slow car" must persist before emitting.</summary>
    private const float SLOW_CAR_MIN_DURATION = 1.5f;

    /// <summary>Seconds a "stopped" car must persist before emitting.</summary>
    private const float STOPPED_MIN_DURATION = 0.8f;

    /// <summary>Minimum display duration for any event (seconds).</summary>
    private const float MIN_DISPLAY_DURATION = 3.0f;

    /// <summary>Cooldown per car per event type (seconds) to prevent spam.</summary>
    private const float EVENT_COOLDOWN = 3.0f;

    /// <summary>Maximum active events displayed simultaneously.</summary>
    private const int MAX_ACTIVE_EVENTS = 6;

    // iRacing TrackSurface constants
    private const int SURFACE_NOT_IN_WORLD = -1;
    private const int SURFACE_OFF_TRACK = 0;
    private const int SURFACE_IN_PIT_STALL = 1;
    private const int SURFACE_APPROACHING_PITS = 2;
    private const int SURFACE_ON_TRACK = 3;

    // iRacing flag bits
    private const int FLAG_REPAIR = 0x100000;
    private const int FLAG_BLACK = 0x10000;
    private const int FLAG_YELLOW = 0x8;  // per-car local yellow flag
    private const int FLAG_BLUE = 0x20;   // per-car blue flag (yield to lapping car)
    private const int FLAG_DSQ = 0x20000; // per-car disqualified

    // iRacing SessionFlags bits (global)
    private const uint SFLAG_CHECKERED = 0x01;
    private const uint SFLAG_WHITE = 0x02;
    private const uint SFLAG_RED = 0x10;
    private const uint SFLAG_GREEN = 0x04;
    private const uint SFLAG_CAUTION = 0x4000;
    private const uint SFLAG_CAUTION_WAVING = 0x8000;

    // iRacing SessionState constants
    private const int SESSION_STATE_PARADE_LAPS = 3;
    private const int SESSION_STATE_RACING = 4;
    private const int SESSION_STATE_CHECKERED = 5;

    // iRacing CarIdxPaceFlags bits
    private const int PACE_END_OF_LINE = 0x01;
    private const int PACE_FREE_PASS = 0x02;
    private const int PACE_WAVE_AROUND = 0x04;

    // Spin detection thresholds
    /// <summary>Minimum backward speed (m/s) to count as a spin (~18 km/h).
    /// Raised from 3.0 to reduce false positives on tight hairpins.</summary>
    private const float SPIN_BACKWARD_SPEED_MIN = 5.0f;

    /// <summary>Minimum prior forward speed (m/s) to qualify as a spin (~29 km/h).
    /// A real spin starts from speed; a U-turn starts from near-zero.</summary>
    private const float SPIN_PRIOR_FORWARD_MIN = 8.0f;

    /// <summary>Minimum seconds of backward movement to confirm a spin.
    /// Raised from 0.5 to 1.0 to filter hairpin-induced LapDistPct jitter.</summary>
    private const float SPIN_MIN_DURATION = 1.0f;

    /// <summary>Grace period after pit exit (seconds) — suppresses Stopped/SlowCar
    /// while the car is accelerating out of pit lane.</summary>
    private const float PIT_EXIT_GRACE_SECONDS = 5.0f;

    /// <summary>Player speed (m/s) below which we consider the player stopped for incoming-fast warnings.</summary>
    private const float PLAYER_STOPPED_THRESHOLD = 5.0f;

    /// <summary>Minimum speed (m/s) of an approaching car to qualify as "incoming fast" (~36 km/h).</summary>
    private const float INCOMING_FAST_SPEED_MIN = 10.0f;

    /// <summary>Max interval (seconds behind player) for incoming-fast detection.</summary>
    private const float INCOMING_FAST_RANGE = 8.0f;

    /// <summary>Max interval (seconds ahead of player) for approaching-from-ahead when off-track.</summary>
    private const float INCOMING_AHEAD_RANGE = 6.0f;

    /// <summary>Maximum interval (seconds) for pit entry/exit events.
    /// Tighter than the general detection range to avoid false alerts from
    /// distant cars whose pit-road position gives misleading intervals.</summary>
    private const float PIT_EVENT_MAX_INTERVAL = 5.0f;

    /// <summary>Minimum consecutive seconds a car must be on pit road before
    /// emitting a Pitting event. Filters one-frame "on pit road" glitches.</summary>
    private const float PIT_ENTRY_DEBOUNCE = 0.3f;

    // ── Event priority (higher = overrides lower for same car) ──
    // When a higher-priority ongoing event is active for a car,
    // lower-priority events for that car are suppressed / cleared.
    private static int GetEventPriority(NearbyEventType type) => type switch
    {
        NearbyEventType.Collision => 100,
        NearbyEventType.Spin => 90,
        NearbyEventType.OffTrack => 80,
        NearbyEventType.Stopped => 70,
        NearbyEventType.SlowCar => 60,
        NearbyEventType.Towed => 55,
        NearbyEventType.MeatballFlag => 50,
        NearbyEventType.BlackFlag => 50,
        NearbyEventType.Disqualified => 45,
        NearbyEventType.BlueFlagged => 40,
        NearbyEventType.LocalYellow => 35,
        NearbyEventType.WhiteFlag => 32,
        NearbyEventType.IncomingFast => 95,
        NearbyEventType.OvertakingImminent => 30,
        NearbyEventType.PaceEndOfLine => 25,
        NearbyEventType.PaceFreePass => 25,
        NearbyEventType.PaceWaveAround => 25,
        NearbyEventType.Pitting => 20,
        NearbyEventType.PitExit => 15,
        NearbyEventType.InBox => 10,
        // Session-level events don't compete with per-car events
        _ => 0,
    };

    /// <summary>Grace period after race start (seconds) — suppresses Stopped/SlowCar
    /// detection while the grid is still getting up to speed.</summary>
    private const float RACE_START_GRACE_SECONDS = 5.0f;

    // ── Per-car tracking state ──────────────────────────────────────
    private readonly int[] _prevTrackSurface = new int[MAX_CARS];
    private readonly bool[] _prevOnPitRoad = new bool[MAX_CARS];
    private readonly int[] _prevIncidentCount = new int[MAX_CARS]; // from CarIdxSessionFlags incident bits
    private readonly float[] _offTrackDuration = new float[MAX_CARS];
    private readonly float[] _slowDuration = new float[MAX_CARS];
    private readonly bool[] _wasInPitStall = new bool[MAX_CARS];
    private readonly int[] _prevFlags = new int[MAX_CARS];
    private readonly float[] _prevLapDistPct = new float[MAX_CARS];
    private readonly float[] _spinDuration = new float[MAX_CARS];
    /// <summary>Smoothed forward speed per car — retains memory of prior speed for spin detection.</summary>
    private readonly float[] _priorForwardSpeed = new float[MAX_CARS];
    private readonly int[] _prevPaceFlags = new int[MAX_CARS];
    /// <summary>Remaining seconds of pit-exit grace per car (suppresses slow/stopped after pit exit).</summary>
    private readonly float[] _pitExitGrace = new float[MAX_CARS];
    /// <summary>Accumulated seconds car has been on pit road (for debounce).</summary>
    private readonly float[] _pitRoadDuration = new float[MAX_CARS];
    /// <summary>Whether a Pitting event has been emitted for this pit road visit.</summary>
    private readonly bool[] _pittingEmitted = new bool[MAX_CARS];

    // ── Incident escalation tracking ────────────────────────────────
    /// <summary>Timestamps of recent dangerous events per car for incident detection.</summary>
    private readonly List<DateTime>[] _recentDangerEvents = new List<DateTime>[MAX_CARS];
    /// <summary>Time window for counting events toward incident escalation.</summary>
    private const float INCIDENT_WINDOW_SECONDS = 15.0f;
    /// <summary>Number of dangerous events in window to trigger incident escalation.</summary>
    private const int INCIDENT_THRESHOLD = 2;
    /// <summary>Dangerous event types that count toward incident escalation.</summary>
    private static readonly HashSet<NearbyEventType> INCIDENT_EVENT_TYPES = new()
    {
        NearbyEventType.Collision,
        NearbyEventType.Spin,
        NearbyEventType.Stopped,
        NearbyEventType.OffTrack,
    };

    // ── Session-level tracking ──────────────────────────────────────
    private int _prevSessionState;
    private uint _prevSessionFlags;
    private bool _cautionWasActive;
    private bool _redFlagActive;
    private bool _checkeredActive;
    private bool _paceLapsActive;
    private bool _whiteFlagActive;
    private bool _playerBlueFlagActive;
    /// <summary>Remaining seconds of race-start grace (suppresses slow/stopped during grid launch).</summary>
    private float _raceStartGraceRemaining;
    /// <summary>Whether the player was on pit road last tick (for detecting player pit exit).</summary>
    private bool _playerWasOnPitRoad;
    /// <summary>Whether the player is in pit-exit merge mode — stays true from pit road departure until racing speed is reached.</summary>
    private bool _playerInPitExitMerge;
    /// <summary>Speed threshold (m/s) the player must reach to clear pit-exit merge mode (~90 km/h).</summary>
    private const float PIT_EXIT_MERGE_SPEED_THRESHOLD = 25.0f;

    // ── Cooldown tracking (per car × event type) ────────────────────
    private readonly Dictionary<(int carIdx, NearbyEventType type), DateTime> _cooldowns = new();

    // ── Active event list ───────────────────────────────────────────
    private readonly List<NearbyEvent> _activeEvents = new(MAX_ACTIVE_EVENTS * 2);
    private long _nextEventId = 1;
    private DateTime _lastUpdateTime = DateTime.UtcNow;

    /// <summary>Read-only snapshot of currently active (non-expired) events.</summary>
    public IReadOnlyList<NearbyEvent> ActiveEvents => _activeEvents;

    /// <summary>Current session state as tracked by the detector (iRacing SessionState enum).</summary>
    public int CurrentSessionState => _prevSessionState;

    /// <summary>Whether the race start grace period is still active.</summary>
    public bool IsRaceStartGraceActive => _raceStartGraceRemaining > 0;

    /// <summary>
    /// Reset all state. Call on session/car/track change.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_prevTrackSurface);
        Array.Clear(_prevOnPitRoad);
        Array.Clear(_prevIncidentCount);
        Array.Clear(_offTrackDuration);
        Array.Clear(_slowDuration);
        Array.Clear(_wasInPitStall);
        Array.Clear(_prevFlags);
        Array.Clear(_prevLapDistPct);
        Array.Clear(_spinDuration);
        Array.Clear(_priorForwardSpeed);
        Array.Clear(_prevPaceFlags);
        Array.Clear(_pitExitGrace);
        Array.Clear(_pitRoadDuration);
        Array.Clear(_pittingEmitted);
        for (int i = 0; i < MAX_CARS; i++)
            _recentDangerEvents[i]?.Clear();
        _cooldowns.Clear();
        _activeEvents.Clear();
        _nextEventId = 1;
        _lastUpdateTime = DateTime.UtcNow;
        _prevSessionState = 0;
        _prevSessionFlags = 0;
        _cautionWasActive = false;
        _redFlagActive = false;
        _playerBlueFlagActive = false;
        _raceStartGraceRemaining = 0f;
        _playerWasOnPitRoad = false;
        _playerInPitExitMerge = false;
    }

    /// <summary>
    /// Main update — call every telemetry tick (~60Hz).
    /// Scans all cars, detects state transitions, emits events.
    /// </summary>
    public void Update(TelemetryData data, IReadOnlyList<RelativeEntry>? relativeEntries)
    {
        var now = DateTime.UtcNow;
        float dt = (float)(now - _lastUpdateTime).TotalSeconds;
        _lastUpdateTime = now;

        // When dt is too large (pause/alt-tab/focus loss), skip speed-based
        // detections this tick. The pctDelta would be real accumulated movement
        // but divided by tiny clamped dt → wildly inflated speed values.
        // We still update _prevLapDistPct so the NEXT tick has clean state.
        bool skipSpeedDetection = dt > 0.5f;
        if (dt > 1.0f) dt = 1.0f / 60f;

        // Expire old events
        _activeEvents.RemoveAll(e => e.IsExpired);

        if (relativeEntries == null || relativeEntries.Count == 0) return;
        if (data.CarIdxTrackSurface == null || data.CarIdxLapDistPct == null) return;

        int playerIdx = data.PlayerCarIdx;

        // ── SESSION-LEVEL events (not per-car) ─────────────────────
        DetectSessionLevelEvents(data);

        // Tick down race-start grace period
        if (_raceStartGraceRemaining > 0)
            _raceStartGraceRemaining = Math.Max(0, _raceStartGraceRemaining - dt);

        // Live-update IntervalToPlayer for all active events from current relative data
        var intervalLookup = new Dictionary<int, float>();
        foreach (var re in relativeEntries)
        {
            if (re.CarIdx >= 0 && re.CarIdx < MAX_CARS)
                intervalLookup[re.CarIdx] = re.IntervalToPlayer;
        }
        foreach (var evt in _activeEvents)
        {
            if (intervalLookup.TryGetValue(evt.CarIdx, out var liveInterval))
                evt.IntervalToPlayer = liveInterval;
        }

        // Track which ongoing events are still active this tick
        var ongoingStillActive = new HashSet<long>();

        // Only process cars within detection range
        foreach (var entry in relativeEntries)
        {
            int i = entry.CarIdx;
            if (i == playerIdx) continue;
            if (i < 0 || i >= MAX_CARS) continue;
            if (!entry.IsConnected)
            {
                // Disconnected car — clear any lingering events quickly
                ClearAllEventsForCar(i);
                continue;
            }

            float interval = entry.IntervalToPlayer;
            // Asymmetric detection: ahead (positive), behind (negative)
            if (interval > DetectionAheadSeconds || interval < -DetectionBehindSeconds) continue;

            int surface = data.CarIdxTrackSurface != null && i < data.CarIdxTrackSurface.Length
                ? data.CarIdxTrackSurface[i] : SURFACE_NOT_IN_WORLD;

            // ── INSTANT CLEAR: car left the world (tow/disconnect) → kill all events now ──
            if (surface == SURFACE_NOT_IN_WORLD)
            {
                ClearAllEventsForCar(i);
                _prevTrackSurface[i] = surface;
                _prevOnPitRoad[i] = false;
                _offTrackDuration[i] = 0;
                _slowDuration[i] = 0;
                _spinDuration[i] = 0;
                _pitRoadDuration[i] = 0;
                _pittingEmitted[i] = false;
                if (data.CarIdxLapDistPct != null && i < data.CarIdxLapDistPct.Length)
                    _prevLapDistPct[i] = data.CarIdxLapDistPct[i];
                continue;
            }

            bool onPitRoad = data.CarIdxOnPitRoad != null && i < data.CarIdxOnPitRoad.Length
                && data.CarIdxOnPitRoad[i];
            int flags = data.CarIdxSessionFlags != null && i < data.CarIdxSessionFlags.Length
                ? data.CarIdxSessionFlags[i] : 0;

            // ── OFF TRACK (ongoing) ─────────────────────────────
            if (surface == SURFACE_OFF_TRACK)
            {
                _offTrackDuration[i] += dt;
                if (_offTrackDuration[i] >= OFF_TRACK_MIN_DURATION)
                {
                    var existing = FindOngoingEvent(i, NearbyEventType.OffTrack);
                    if (existing != null)
                    {
                        ongoingStillActive.Add(existing.Id);
                    }
                    else if (_prevTrackSurface[i] != SURFACE_OFF_TRACK || _offTrackDuration[i] < OFF_TRACK_MIN_DURATION + dt * 2)
                    {
                        TryEmitOngoing(entry, NearbyEventType.OffTrack, "OFF TRACK",
                            NearbyEventSeverity.Warning, 3.5f, ongoingStillActive);
                    }
                }
            }
            else
            {
                _offTrackDuration[i] = 0;
                // Condition cleared — mark any ongoing OffTrack event for this car
                ClearOngoingEvent(i, NearbyEventType.OffTrack);
            }

            // ── COLLISION (incident flag jump) ──────────────────
            if (entry.HasRecentIncident && entry.IncidentDelta >= 2)
            {
                TryEmit(entry, NearbyEventType.Collision, "COLLISION",
                    NearbyEventSeverity.Danger, 4.0f);
            }

            // ── PITTING (entered pit road) ──────────────────────
            // Debounce: require car to stay on pit road for PIT_ENTRY_DEBOUNCE seconds.
            // Range filter: only emit within PIT_EVENT_MAX_INTERVAL to avoid distant false alarms.
            if (onPitRoad)
            {
                _pitRoadDuration[i] += dt;
                if (!_pittingEmitted[i]
                    && _pitRoadDuration[i] >= PIT_ENTRY_DEBOUNCE
                    && surface != SURFACE_IN_PIT_STALL
                    && Math.Abs(interval) <= PIT_EVENT_MAX_INTERVAL)
                {
                    TryEmit(entry, NearbyEventType.Pitting, "PITTING",
                        NearbyEventSeverity.Info, 3.0f);
                    _pittingEmitted[i] = true;
                }
            }
            else
            {
                _pitRoadDuration[i] = 0;
                _pittingEmitted[i] = false;
            }

            // ── IN BOX — tracked for state only (not emitted; always filtered out in UI)
            _wasInPitStall[i] = surface == SURFACE_IN_PIT_STALL;

            // ── PIT EXIT (left pit road back to track) ──────────
            // Range filter: only emit within PIT_EVENT_MAX_INTERVAL
            if (!onPitRoad && _prevOnPitRoad[i] && surface == SURFACE_ON_TRACK
                && Math.Abs(interval) <= PIT_EVENT_MAX_INTERVAL)
            {
                TryEmit(entry, NearbyEventType.PitExit, "PIT EXIT",
                    NearbyEventSeverity.Info, 3.0f);
                _pitExitGrace[i] = PIT_EXIT_GRACE_SECONDS; // suppress slow/stopped while accelerating
            }
            else if (!onPitRoad && _prevOnPitRoad[i] && surface == SURFACE_ON_TRACK)
            {
                // Still need grace period even for distant pit exits (to avoid false slow/stopped)
                _pitExitGrace[i] = PIT_EXIT_GRACE_SECONDS;
            }

            // ── TOWED (was on track, jumped to pit stall without traversing pit road approach) ──
            if (entry.WasTowed && _prevTrackSurface[i] == SURFACE_ON_TRACK)
            {
                TryEmit(entry, NearbyEventType.Towed, "TOWED",
                    NearbyEventSeverity.Warning, 4.0f);
            }

            // ── MEATBALL FLAG (ongoing) ─────────────────────────
            if ((flags & FLAG_REPAIR) != 0)
            {
                var existing = FindOngoingEvent(i, NearbyEventType.MeatballFlag);
                if (existing != null)
                {
                    ongoingStillActive.Add(existing.Id);
                }
                else if ((_prevFlags[i] & FLAG_REPAIR) == 0)
                {
                    TryEmitOngoing(entry, NearbyEventType.MeatballFlag, "MEATBALL",
                        NearbyEventSeverity.Warning, 4.0f, ongoingStillActive);
                }
            }
            else
            {
                ClearOngoingEvent(i, NearbyEventType.MeatballFlag);
            }

            // ── BLACK FLAG ──────────────────────────────────────
            if ((flags & FLAG_BLACK) != 0 && (_prevFlags[i] & FLAG_BLACK) == 0)
            {
                TryEmit(entry, NearbyEventType.BlackFlag, "BLACK FLAG",
                    NearbyEventSeverity.Danger, 4.0f);
            }

            // ── LOCAL YELLOW FLAG (ongoing while flag bit is set) ─
            if ((flags & FLAG_YELLOW) != 0)
            {
                bool isAhead = interval > 0;
                TryEmitOngoing(entry, NearbyEventType.LocalYellow,
                    isAhead ? "⚑ YELLOW AHEAD" : "⚑ YELLOW",
                    NearbyEventSeverity.Danger, 4.0f, ongoingStillActive);
            }
            else if ((_prevFlags[i] & FLAG_YELLOW) != 0)
            {
                ClearOngoingEvent(i, NearbyEventType.LocalYellow);
            }

            // ── SLOW CAR / STOPPED (ongoing) ────────────────────
            // Suppress during race-start grace period (grid accelerating from standing still).
            // Real pile-ups at the start are caught by Collision (incident flags) and OffTrack.
            // Skip when dt was too large (alt-tab) — speed data would be garbage.
            // Also skip when LapDistPct jumps too far in one frame (teleport/tow/respawn) —
            // the wrap-around correction can't distinguish a real half-track spin from a tow.
            // Tick down pit-exit grace for this car
            if (_pitExitGrace[i] > 0) _pitExitGrace[i] = Math.Max(0, _pitExitGrace[i] - dt);

            if (surface == SURFACE_ON_TRACK && !onPitRoad && _raceStartGraceRemaining <= 0
                && _pitExitGrace[i] <= 0 && !skipSpeedDetection)
            {
                float prevPct = _prevLapDistPct[i];
                float curPct = data.CarIdxLapDistPct![i];
                float pctDelta = curPct - prevPct;
                // Handle wrap-around
                if (pctDelta < -0.5f) pctDelta += 1.0f;
                if (pctDelta > 0.5f) pctDelta -= 1.0f;

                // Guard: if the position jumped more than ~10% of track in one frame,
                // this is almost certainly a teleport (tow, respawn, game glitch), not
                // real movement. At 60Hz, even 400 km/h on a 4km track is only ~1.7%/frame.
                // 10% threshold gives ample headroom for long straights + high speed.
                bool isPositionJump = Math.Abs(pctDelta) > 0.10f;

                // Convert pctDelta to approximate m/s (trackLength * pctDelta / dt)
                float trackLen = data.TrackLength > 0 ? data.TrackLength : 4000f;
                float approxSpeed = (!isPositionJump && dt > 0) ? (Math.Abs(pctDelta) * trackLen / dt) : 999f;

                if (approxSpeed < STOPPED_SPEED_THRESHOLD)
                {
                    _slowDuration[i] += dt;
                    if (_slowDuration[i] >= STOPPED_MIN_DURATION)
                    {
                        bool isAhead = interval > 0;
                        var existingStopped = FindOngoingEvent(i, NearbyEventType.Stopped);
                        if (existingStopped != null)
                        {
                            ongoingStillActive.Add(existingStopped.Id);
                        }
                        else
                        {
                            TryEmitOngoing(entry, NearbyEventType.Stopped,
                                isAhead ? "STOPPED AHEAD" : "STOPPED",
                                isAhead ? NearbyEventSeverity.Critical : NearbyEventSeverity.Danger,
                                5.0f, ongoingStillActive);
                        }
                    }
                }
                else if (approxSpeed < SLOW_SPEED_THRESHOLD)
                {
                    _slowDuration[i] += dt;
                    if (_slowDuration[i] >= SLOW_CAR_MIN_DURATION)
                    {
                        var existingSlow = FindOngoingEvent(i, NearbyEventType.SlowCar);
                        if (existingSlow != null)
                        {
                            ongoingStillActive.Add(existingSlow.Id);
                        }
                        else
                        {
                            TryEmitOngoing(entry, NearbyEventType.SlowCar, "SLOW",
                                NearbyEventSeverity.Warning, 3.5f, ongoingStillActive);
                        }
                    }
                }
                else
                {
                    _slowDuration[i] = 0;
                    ClearOngoingEvent(i, NearbyEventType.Stopped);
                    ClearOngoingEvent(i, NearbyEventType.SlowCar);
                }
            }
            else
            {
                _slowDuration[i] = 0;
                ClearOngoingEvent(i, NearbyEventType.Stopped);
                ClearOngoingEvent(i, NearbyEventType.SlowCar);
            }

            // ── OVERTAKING IMMINENT (higher-class car closing from behind) ──
            // Emit when a car from a faster class is within 3s behind and closing
            if (interval < 0 && interval > -3.0f && surface == SURFACE_ON_TRACK && !onPitRoad)
            {
                int otherClass = (data.CarIdxClass != null && i < data.CarIdxClass.Length) ? data.CarIdxClass[i] : -1;
                int playerClass = data.PlayerCarClass;
                // Different class + car is behind player = potential lapping scenario
                if (otherClass >= 0 && playerClass >= 0 && otherClass != playerClass)
                {
                    // Check if other car is in a higher overall position (likely faster class)
                    int otherPos = entry.OverallPosition;
                    int playerPos = data.PlayerCarPosition > 0 ? data.PlayerCarPosition : 999;
                    if (otherPos < playerPos)
                    {
                        var existingOT = FindOngoingEvent(i, NearbyEventType.OvertakingImminent);
                        if (existingOT != null)
                        {
                            ongoingStillActive.Add(existingOT.Id);
                        }
                        else
                        {
                            TryEmitOngoing(entry, NearbyEventType.OvertakingImminent,
                                "FASTER CLASS",
                                NearbyEventSeverity.Warning, 5.0f, ongoingStillActive);
                        }
                    }
                }
            }
            else
            {
                ClearOngoingEvent(i, NearbyEventType.OvertakingImminent);
            }

            // ── SPIN DETECTION (ongoing) ────────────────────────
            // Detect via significant backward movement on track.
            // Requires: (1) backward speed exceeds SPIN_BACKWARD_SPEED_MIN (~3 m/s)
            //           (2) car was moving forward at SPIN_PRIOR_FORWARD_MIN (~8 m/s) recently.
            // This filters deliberate U-turns (start from near-zero speed, reverse slowly)
            // and minor LapDistPct noise from tight corners.
            // Skip when dt was too large (alt-tab) — speed data would be garbage.
            if (surface == SURFACE_ON_TRACK && !onPitRoad && !skipSpeedDetection)
            {
                float prevPctSpin = _prevLapDistPct[i];
                float curPctSpin = data.CarIdxLapDistPct![i];
                float pctDeltaSpin = curPctSpin - prevPctSpin;
                if (pctDeltaSpin < -0.5f) pctDeltaSpin += 1.0f;
                if (pctDeltaSpin > 0.5f) pctDeltaSpin -= 1.0f;

                // Guard: skip spin detection if position jumped >10% of track in one frame
                // (teleport/tow/respawn — not a real spin)
                bool isSpinPositionJump = Math.Abs(pctDeltaSpin) > 0.10f;

                float trackLen = data.TrackLength > 0 ? data.TrackLength : 4000f;
                float speedSigned = (!isSpinPositionJump && dt > 0) ? (pctDeltaSpin * trackLen / dt) : 0f;
                float backwardSpeed = speedSigned < 0 ? -speedSigned : 0f;

                // Track prior forward speed (smoothed) — retains memory of how fast the car was
                // going before a potential spin. Decays over ~1-2s at 60fps.
                if (speedSigned > 1.0f) // going forward at > 1 m/s
                    _priorForwardSpeed[i] = speedSigned;
                else
                    _priorForwardSpeed[i] *= 0.97f; // slow decay — retains memory ~1s

                // Spin = going backwards at significant speed + car was moving forward recently
                bool isSpinning = backwardSpeed > SPIN_BACKWARD_SPEED_MIN
                                  && _priorForwardSpeed[i] > SPIN_PRIOR_FORWARD_MIN;

                if (isSpinning)
                {
                    _spinDuration[i] += dt;
                    if (_spinDuration[i] >= SPIN_MIN_DURATION)
                    {
                        var existingSpin = FindOngoingEvent(i, NearbyEventType.Spin);
                        if (existingSpin != null)
                        {
                            ongoingStillActive.Add(existingSpin.Id);
                        }
                        else
                        {
                            bool spinAhead = interval > 0;
                            TryEmitOngoing(entry, NearbyEventType.Spin,
                                spinAhead ? "SPIN AHEAD" : "SPIN",
                                spinAhead ? NearbyEventSeverity.Critical : NearbyEventSeverity.Danger,
                                4.0f, ongoingStillActive);
                        }
                    }
                }
                else
                {
                    _spinDuration[i] = 0;
                    ClearOngoingEvent(i, NearbyEventType.Spin);
                }
            }
            else
            {
                _spinDuration[i] = 0;
                ClearOngoingEvent(i, NearbyEventType.Spin);
            }

            // ── BLUE FLAG (about to be overlapped) ──────────────
            // Uses per-car blue flag bit AND/OR interval-based detection for faster-class cars approaching
            bool hasBlueFlag = (flags & FLAG_BLUE) != 0;
            if (hasBlueFlag && (_prevFlags[i] & FLAG_BLUE) == 0)
            {
                TryEmit(entry, NearbyEventType.BlueFlagged, "🔵 BLUE FLAG",
                    NearbyEventSeverity.Warning, 4.0f);
            }

            // ── DISQUALIFIED ────────────────────────────────────
            bool isDSQ = (flags & FLAG_DSQ) != 0;
            if (isDSQ && (_prevFlags[i] & FLAG_DSQ) == 0)
            {
                TryEmit(entry, NearbyEventType.Disqualified, "DSQ",
                    NearbyEventSeverity.Warning, 5.0f);
            }

            // ── PACE FLAGS (EndOfLine / FreePass / WaveAround) ──
            int paceFlags = data.CarIdxPaceFlags != null && i < data.CarIdxPaceFlags.Length
                ? data.CarIdxPaceFlags[i] : 0;
            int prevPace = _prevPaceFlags[i];

            if ((paceFlags & PACE_END_OF_LINE) != 0 && (prevPace & PACE_END_OF_LINE) == 0)
            {
                TryEmit(entry, NearbyEventType.PaceEndOfLine, "END OF LINE",
                    NearbyEventSeverity.Info, 4.0f);
            }
            if ((paceFlags & PACE_FREE_PASS) != 0 && (prevPace & PACE_FREE_PASS) == 0)
            {
                TryEmit(entry, NearbyEventType.PaceFreePass, "FREE PASS",
                    NearbyEventSeverity.Info, 4.0f);
            }
            if ((paceFlags & PACE_WAVE_AROUND) != 0 && (prevPace & PACE_WAVE_AROUND) == 0)
            {
                TryEmit(entry, NearbyEventType.PaceWaveAround, "WAVE AROUND",
                    NearbyEventSeverity.Info, 4.0f);
            }
            _prevPaceFlags[i] = paceFlags;

            // Update previous state
            _prevTrackSurface[i] = surface;
            _prevOnPitRoad[i] = onPitRoad;
            _prevFlags[i] = flags;
            if (data.CarIdxLapDistPct != null && i < data.CarIdxLapDistPct.Length)
                _prevLapDistPct[i] = data.CarIdxLapDistPct[i];
        }

        // ── PLAYER PIT EXIT DETECTION ──────────────────────────────
        // Track when the player leaves pit road to activate merge warnings.
        // Merge mode stays active the ENTIRE time on pit road + until racing speed after exit.
        int playerSurface = data.CarIdxTrackSurface != null
            && playerIdx >= 0 && playerIdx < data.CarIdxTrackSurface.Length
            ? data.CarIdxTrackSurface[playerIdx] : SURFACE_NOT_IN_WORLD;
        bool playerOnOrOffTrack = playerSurface == SURFACE_ON_TRACK || playerSurface == SURFACE_OFF_TRACK;
        bool playerInPits = data.CarIdxOnPitRoad != null && playerIdx >= 0
            && playerIdx < data.CarIdxOnPitRoad.Length && data.CarIdxOnPitRoad[playerIdx];
        bool playerInPitStall = playerSurface == SURFACE_IN_PIT_STALL;

        // Detect player exiting pits → enter merge mode (stays active until racing speed)
        if (_playerWasOnPitRoad && !playerInPits && playerOnOrOffTrack)
            _playerInPitExitMerge = true;
        _playerWasOnPitRoad = playerInPits;

        // Clear merge mode once player reaches racing speed on track
        if (_playerInPitExitMerge && playerOnOrOffTrack && !playerInPits
            && data.Speed >= PIT_EXIT_MERGE_SPEED_THRESHOLD)
            _playerInPitExitMerge = false;

        // Player driving on pit road (not stationary in pit stall) —
        // warn about traffic the entire time they're heading for pit exit.
        bool playerDrivingOnPitRoad = playerInPits && !playerInPitStall;

        // ── APPROACHING (player is stopped/slow OR exiting/on pit road) ──────
        // Case 1: Player is stopped/slow on track → warn about fast cars behind
        // Case 2: Player just exited pits → warn about fast cars behind (merge warning)
        // Case 3: Player is driving on pit road (heading for exit) → early merge warning
        // Case 4: Player is off-track → warn about traffic from BOTH directions
        bool playerStopped = playerOnOrOffTrack && !playerInPits
            && data.Speed < PLAYER_STOPPED_THRESHOLD;
        bool playerOffTrack = playerSurface == SURFACE_OFF_TRACK && !playerInPits;
        bool playerMerging = _playerInPitExitMerge || playerDrivingOnPitRoad;

        // Any vulnerable state triggers approaching detection
        bool playerVulnerable = playerStopped || playerMerging || playerOffTrack;

        // Use higher speed threshold for merge check (car is accelerating but still slow)
        float incomingSpeedThreshold = playerMerging && !playerStopped
            ? PIT_EXIT_MERGE_SPEED_THRESHOLD : INCOMING_FAST_SPEED_MIN;

        if (playerVulnerable && !skipSpeedDetection)
        {
            float trackLen = data.TrackLength > 0 ? data.TrackLength : 4000f;
            foreach (var entry in relativeEntries)
            {
                int ci = entry.CarIdx;
                if (ci == playerIdx || ci < 0 || ci >= MAX_CARS) continue;
                if (!entry.IsConnected) continue;

                float interval = entry.IntervalToPlayer;

                // When off-track OR stopped on track, check cars from BOTH directions
                bool isBehind = interval < 0 && interval >= -INCOMING_FAST_RANGE;
                bool isAhead = (playerOffTrack || playerStopped) && interval > 0 && interval <= INCOMING_AHEAD_RANGE;
                if (!isBehind && !isAhead) continue;

                int ciSurface = data.CarIdxTrackSurface != null && ci < data.CarIdxTrackSurface.Length
                    ? data.CarIdxTrackSurface[ci] : SURFACE_NOT_IN_WORLD;
                if (ciSurface != SURFACE_ON_TRACK) continue;

                // Estimate approaching car speed from LapDistPct delta
                float prevPctCI = _prevLapDistPct[ci];
                float curPctCI = data.CarIdxLapDistPct![ci];
                float pctDeltaCI = curPctCI - prevPctCI;
                if (pctDeltaCI < -0.5f) pctDeltaCI += 1.0f;
                if (pctDeltaCI > 0.5f) pctDeltaCI -= 1.0f;
                if (Math.Abs(pctDeltaCI) > 0.10f) continue; // teleport guard

                float approxSpeedCI = dt > 0 ? (Math.Abs(pctDeltaCI) * trackLen / dt) : 0f;

                if (approxSpeedCI >= incomingSpeedThreshold)
                {
                    var existingIncoming = FindOngoingEvent(ci, NearbyEventType.IncomingFast);
                    if (existingIncoming != null)
                    {
                        ongoingStillActive.Add(existingIncoming.Id);
                    }
                    else
                    {
                        string label = playerMerging && !playerStopped && !playerOffTrack
                            ? "⚠ CAR BEHIND"
                            : isAhead ? "⚠ CAR AHEAD" : "⚠ APPROACHING";
                        TryEmitOngoing(entry, NearbyEventType.IncomingFast,
                            label,
                            NearbyEventSeverity.Warning, 5.0f, ongoingStillActive);
                    }
                }
                else
                {
                    ClearOngoingEvent(ci, NearbyEventType.IncomingFast);
                }
            }
        }
        else
        {
            // Player moving at speed and not merging and not off-track — clear all incoming-fast events
            for (int ci = 0; ci < MAX_CARS; ci++)
                ClearOngoingEvent(ci, NearbyEventType.IncomingFast);
        }

        // Any ongoing events whose condition was NOT confirmed this tick → clear them
        foreach (var evt in _activeEvents)
        {
            if (evt.IsOngoing && !ongoingStillActive.Contains(evt.Id))
            {
                evt.IsOngoing = false;
                evt.ClearedAt = DateTime.UtcNow;
            }
        }

        // ── PACK DENSITY: detect chaotic pack when 3+ per-car events are active ──
        int perCarEventCount = _activeEvents.Count(e =>
            !e.IsExpired && e.CarIdx >= 0
            && e.EventType != NearbyEventType.IncomingFast);
        if (perCarEventCount >= 3 && !_activeEvents.Any(e =>
            e.EventType == NearbyEventType.LocalYellow && e.CarIdx == -2 && !e.IsExpired))
        {
            // Emit a session-level "PACK" warning (CarIdx=-2 to distinguish from normal local yellow)
            var packEvt = new NearbyEvent
            {
                Id = _nextEventId++,
                CarIdx = -2,
                EventType = NearbyEventType.LocalYellow,
                DisplayText = "⚡ CHAOS ZONE",
                Severity = NearbyEventSeverity.Danger,
                DisplayDuration = 5.0f,
                IntervalToPlayer = 0.01f,
                DriverName = $"{perCarEventCount} events",
                CarNumber = "",
                CreatedAt = DateTime.UtcNow,
                IsOngoing = true,
            };
            _activeEvents.Add(packEvt);
            ongoingStillActive.Add(packEvt.Id);
        }
        else if (perCarEventCount < 3)
        {
            // Clear pack density event when events drop below threshold
            var packEvt = _activeEvents.FirstOrDefault(e =>
                e.EventType == NearbyEventType.LocalYellow && e.CarIdx == -2 && e.IsOngoing);
            if (packEvt != null)
            {
                packEvt.IsOngoing = false;
                packEvt.ClearedAt = DateTime.UtcNow;
            }
        }
    }

    // ── Internal helpers ────────────────────────────────────────────

    private void TryEmit(RelativeEntry entry, NearbyEventType type,
        string text, NearbyEventSeverity severity, float duration)
    {
        var key = (entry.CarIdx, type);

        // Priority check: if a higher-priority active event exists for this car, skip
        int myPriority = GetEventPriority(type);
        if (HasHigherPriorityActive(entry.CarIdx, myPriority))
            return;

        // Dedup: skip if an active (non-expired) event already exists for this car + type
        for (int j = 0; j < _activeEvents.Count; j++)
        {
            var e = _activeEvents[j];
            if (e.CarIdx == entry.CarIdx && e.EventType == type && !e.IsExpired)
                return;
        }

        // Cooldown check
        if (_cooldowns.TryGetValue(key, out var lastEmit) &&
            (DateTime.UtcNow - lastEmit).TotalSeconds < EVENT_COOLDOWN)
            return;

        // Clear any lower-priority ongoing events for this car
        ClearLowerPriorityOngoing(entry.CarIdx, myPriority);

        // Cap active events
        if (_activeEvents.Count >= MAX_ACTIVE_EVENTS)
        {
            // Remove lowest severity oldest event
            var weakest = _activeEvents
                .OrderBy(e => e.Severity)
                .ThenBy(e => e.CreatedAt)
                .First();
            _activeEvents.Remove(weakest);
        }

        var newEvt = new NearbyEvent
        {
            Id = _nextEventId++,
            CarIdx = entry.CarIdx,
            DriverName = entry.DriverName,
            CarNumber = entry.CarNumber,
            CarClassId = entry.CarClassId,
            EventType = type,
            IntervalToPlayer = entry.IntervalToPlayer,
            DisplayText = text,
            Severity = severity,
            DisplayDuration = Math.Max(duration, MIN_DISPLAY_DURATION),
        };

        // Incident escalation: track dangerous events per car
        if (INCIDENT_EVENT_TYPES.Contains(type))
        {
            _recentDangerEvents[entry.CarIdx] ??= new List<DateTime>();
            var list = _recentDangerEvents[entry.CarIdx];
            list.Add(DateTime.UtcNow);
            // Prune old entries
            list.RemoveAll(t => (DateTime.UtcNow - t).TotalSeconds > INCIDENT_WINDOW_SECONDS);

            if (list.Count >= INCIDENT_THRESHOLD)
            {
                // Escalate: mark this event and any existing active events for this car
                bool isAhead = entry.IntervalToPlayer > 0;
                newEvt.IsIncident = true;
                newEvt.DisplayText = isAhead ? "⚠ INCIDENT AHEAD" : "⚠ INCIDENT";
                newEvt.Severity = isAhead ? NearbyEventSeverity.Critical : NearbyEventSeverity.Danger;

                // Also escalate any existing active events for this car
                foreach (var existing in _activeEvents)
                {
                    if (existing.CarIdx == entry.CarIdx && !existing.IsExpired
                        && INCIDENT_EVENT_TYPES.Contains(existing.EventType))
                    {
                        existing.IsIncident = true;
                    }
                }
            }
        }

        _activeEvents.Add(newEvt);

        _cooldowns[key] = DateTime.UtcNow;

        // Purge old cooldowns periodically
        if (_cooldowns.Count > MAX_CARS * 4)
        {
            var expired = _cooldowns
                .Where(kv => (DateTime.UtcNow - kv.Value).TotalSeconds > EVENT_COOLDOWN * 2)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var k in expired) _cooldowns.Remove(k);
        }
    }

    /// <summary>
    /// Emit an ongoing event (persists while condition is active).
    /// Bypasses cooldown for ongoing types — dedup by finding existing event instead.
    /// </summary>
    private void TryEmitOngoing(RelativeEntry entry, NearbyEventType type,
        string text, NearbyEventSeverity severity, float duration,
        HashSet<long> ongoingStillActive)
    {
        // Don't duplicate — check for existing ongoing event for this car + type
        var existing = FindOngoingEvent(entry.CarIdx, type);
        if (existing != null)
        {
            ongoingStillActive.Add(existing.Id);
            return;
        }

        // Check for recently-cleared (non-ongoing) event of same type still in list.
        // Re-activate it instead of creating a duplicate row for the same driver+event.
        for (int j = 0; j < _activeEvents.Count; j++)
        {
            var e = _activeEvents[j];
            if (e.CarIdx == entry.CarIdx && e.EventType == type && !e.IsExpired && !e.IsOngoing)
            {
                e.IsOngoing = true;
                e.ClearedAt = null;
                ongoingStillActive.Add(e.Id);
                return;
            }
        }

        // Priority check: if a higher-priority active event exists for this car, skip
        int myPriority = GetEventPriority(type);
        if (HasHigherPriorityActive(entry.CarIdx, myPriority))
            return;

        // Clear any lower-priority ongoing events for this car
        ClearLowerPriorityOngoing(entry.CarIdx, myPriority);

        // Cap active events
        if (_activeEvents.Count >= MAX_ACTIVE_EVENTS)
        {
            // Remove lowest severity non-ongoing oldest event first
            var weakest = _activeEvents
                .Where(e => !e.IsOngoing)
                .OrderBy(e => e.Severity)
                .ThenBy(e => e.CreatedAt)
                .FirstOrDefault()
                ?? _activeEvents.OrderBy(e => e.Severity).ThenBy(e => e.CreatedAt).First();
            _activeEvents.Remove(weakest);
        }

        var evt = new NearbyEvent
        {
            Id = _nextEventId++,
            CarIdx = entry.CarIdx,
            DriverName = entry.DriverName,
            CarNumber = entry.CarNumber,
            CarClassId = entry.CarClassId,
            EventType = type,
            IntervalToPlayer = entry.IntervalToPlayer,
            DisplayText = text,
            Severity = severity,
            DisplayDuration = Math.Max(duration, MIN_DISPLAY_DURATION),
            IsOngoing = true,
        };
        _activeEvents.Add(evt);
        ongoingStillActive.Add(evt.Id);
    }

    /// <summary>Find an active ongoing event for a specific car + event type.</summary>
    private NearbyEvent? FindOngoingEvent(int carIdx, NearbyEventType type)
    {
        for (int j = 0; j < _activeEvents.Count; j++)
        {
            var e = _activeEvents[j];
            if (e.CarIdx == carIdx && e.EventType == type && e.IsOngoing)
                return e;
        }
        return null;
    }

    /// <summary>
    /// Check if a higher-priority active (non-expired) event already exists for this car.
    /// Checks ALL events (ongoing and one-shot), not just ongoing.
    /// Session-level events (CarIdx=-1) are excluded from per-car priority checks.
    /// </summary>
    private bool HasHigherPriorityActive(int carIdx, int myPriority)
    {
        if (carIdx < 0) return false; // session-level events don't compete
        for (int j = 0; j < _activeEvents.Count; j++)
        {
            var e = _activeEvents[j];
            if (e.CarIdx == carIdx && !e.IsExpired && GetEventPriority(e.EventType) > myPriority)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Clear (mark done) any ongoing events for this car with LOWER priority.
    /// This ensures higher-priority events visually replace lower ones.
    /// </summary>
    private void ClearLowerPriorityOngoing(int carIdx, int myPriority)
    {
        if (carIdx < 0) return;
        for (int j = 0; j < _activeEvents.Count; j++)
        {
            var e = _activeEvents[j];
            if (e.CarIdx == carIdx && e.IsOngoing && GetEventPriority(e.EventType) < myPriority)
            {
                e.IsOngoing = false;
                e.ClearedAt = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Mark any ongoing event for car + type as no longer ongoing.
    /// The event will then expire via its normal DisplayDuration timer.
    /// </summary>
    private void ClearOngoingEvent(int carIdx, NearbyEventType type)
    {
        for (int j = 0; j < _activeEvents.Count; j++)
        {
            var e = _activeEvents[j];
            if (e.CarIdx == carIdx && e.EventType == type && e.IsOngoing)
            {
                e.IsOngoing = false;
                e.ClearedAt = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Instantly clear ALL events for a car (used when car leaves the world / tows).
    /// Sets a very short display duration so events fade quickly.
    /// </summary>
    private void ClearAllEventsForCar(int carIdx)
    {
        for (int j = _activeEvents.Count - 1; j >= 0; j--)
        {
            var e = _activeEvents[j];
            if (e.CarIdx == carIdx && !e.IsExpired)
            {
                e.IsOngoing = false;
                e.ClearedAt = DateTime.UtcNow;
                e.DisplayDuration = 0.5f; // fade out in 0.5s
            }
        }
    }

    // ── Session-level event detection ───────────────────────────────

    /// <summary>
    /// Detect session-wide events: safety car, start sequence, checkered flag, red flag.
    /// These are not per-car — they use global SessionFlags and SessionState.
    /// Uses a special CarIdx of -1 for display.
    /// </summary>
    private void DetectSessionLevelEvents(TelemetryData data)
    {
        uint sf = data.SessionFlags;
        int ss = data.SessionState;

        // ── SAFETY CAR / FULL-COURSE CAUTION (ongoing) ──────────
        bool cautionNow = data.IsCautionActive || (sf & SFLAG_CAUTION) != 0 || (sf & SFLAG_CAUTION_WAVING) != 0;
        if (cautionNow && !_cautionWasActive)
        {
            EmitSessionEvent(NearbyEventType.SafetyCar, "⚠ SAFETY CAR",
                NearbyEventSeverity.Danger, 6.0f, isOngoing: true);
        }
        else if (!cautionNow && _cautionWasActive)
        {
            ClearOngoingEvent(-1, NearbyEventType.SafetyCar);
        }
        _cautionWasActive = cautionNow;

        // ── RED FLAG (ongoing while active) ─────────────────────
        bool redNow = (sf & SFLAG_RED) != 0;
        if (redNow && !_redFlagActive)
        {
            EmitSessionEvent(NearbyEventType.RedFlag, "🔴 RED FLAG",
                NearbyEventSeverity.Critical, 8.0f, isOngoing: true);
        }
        else if (!redNow && _redFlagActive)
        {
            ClearOngoingEvent(-1, NearbyEventType.RedFlag);
        }
        _redFlagActive = redNow;

        // ── WHITE FLAG (ongoing while final lap) ────────────────
        bool whiteNow = (sf & SFLAG_WHITE) != 0;
        if (whiteNow && !_whiteFlagActive)
        {
            EmitSessionEvent(NearbyEventType.WhiteFlag, "🏳 WHITE FLAG",
                NearbyEventSeverity.Info, 6.0f, isOngoing: true);
        }
        else if (!whiteNow && _whiteFlagActive)
        {
            ClearOngoingEvent(-1, NearbyEventType.WhiteFlag);
        }
        _whiteFlagActive = whiteNow;

        // ── START SEQUENCE (green flag — 5s max, not ongoing) ───
        if (ss == SESSION_STATE_RACING && _prevSessionState == SESSION_STATE_PARADE_LAPS)
        {
            EmitSessionEvent(NearbyEventType.StartSequence, "🟢 GO GO GO!",
                NearbyEventSeverity.Info, 5.0f);
            // Begin grace period: suppress Stopped/SlowCar while grid accelerates
            _raceStartGraceRemaining = RACE_START_GRACE_SECONDS;
        }
        // Also activate grace when session jumps straight to RACING (e.g., practice → race without parade)
        else if (ss == SESSION_STATE_RACING && _prevSessionState > 0 && _prevSessionState != SESSION_STATE_RACING)
        {
            _raceStartGraceRemaining = RACE_START_GRACE_SECONDS;
        }

        // ── PACE LAPS (ongoing while in parade state) ───────────
        bool paceNow = ss == SESSION_STATE_PARADE_LAPS;
        if (paceNow && !_paceLapsActive)
        {
            EmitSessionEvent(NearbyEventType.StartSequence, "🟡 PACE LAPS",
                NearbyEventSeverity.Info, 6.0f, isOngoing: true);
        }
        else if (!paceNow && _paceLapsActive)
        {
            ClearOngoingEvent(-1, NearbyEventType.StartSequence);
        }
        _paceLapsActive = paceNow;

        // ── CHECKERED FLAG (ongoing while session state is checkered) ─
        bool checkeredNow = ss == SESSION_STATE_CHECKERED;
        if (checkeredNow && !_checkeredActive)
        {
            EmitSessionEvent(NearbyEventType.CheckeredFlag, "🏁 CHECKERED",
                NearbyEventSeverity.Info, 8.0f, isOngoing: true);
        }
        else if (!checkeredNow && _checkeredActive)
        {
            ClearOngoingEvent(-1, NearbyEventType.CheckeredFlag);
        }
        _checkeredActive = checkeredNow;

        // ── PLAYER'S OWN BLUE FLAG (relative to the driver using the widget) ──
        // This is the player-centric "you need to yield" alert.
        int playerIdx = data.PlayerCarIdx;
        bool playerHasBlue = data.CarIdxSessionFlags != null
            && playerIdx >= 0 && playerIdx < data.CarIdxSessionFlags.Length
            && (data.CarIdxSessionFlags[playerIdx] & FLAG_BLUE) != 0;

        if (playerHasBlue && !_playerBlueFlagActive)
        {
            EmitSessionEvent(NearbyEventType.BlueFlagged, "🔵 BLUE — YIELD",
                NearbyEventSeverity.Warning, 6.0f, isOngoing: true);
        }
        else if (!playerHasBlue && _playerBlueFlagActive)
        {
            ClearOngoingEvent(-1, NearbyEventType.BlueFlagged);
        }
        _playerBlueFlagActive = playerHasBlue;

        _prevSessionState = ss;
        _prevSessionFlags = sf;
    }

    /// <summary>
    /// Emit a session-level event (not tied to a specific car).
    /// Uses CarIdx = -1, empty driver/car info.
    /// </summary>
    private void EmitSessionEvent(NearbyEventType type, string text,
        NearbyEventSeverity severity, float duration, bool isOngoing = false)
    {
        // Dedup: check for existing same-type session event
        for (int j = 0; j < _activeEvents.Count; j++)
        {
            if (_activeEvents[j].CarIdx == -1 && _activeEvents[j].EventType == type && !_activeEvents[j].IsExpired)
                return; // already showing
        }

        // Cap active events
        if (_activeEvents.Count >= MAX_ACTIVE_EVENTS)
        {
            var weakest = _activeEvents
                .Where(e => !e.IsOngoing)
                .OrderBy(e => e.Severity)
                .ThenBy(e => e.CreatedAt)
                .FirstOrDefault()
                ?? _activeEvents.OrderBy(e => e.Severity).ThenBy(e => e.CreatedAt).First();
            _activeEvents.Remove(weakest);
        }

        _activeEvents.Add(new NearbyEvent
        {
            Id = _nextEventId++,
            CarIdx = -1,
            DriverName = string.Empty,
            CarNumber = string.Empty,
            CarClassId = 0,
            EventType = type,
            IntervalToPlayer = 0f,
            DisplayText = text,
            Severity = severity,
            DisplayDuration = Math.Max(duration, MIN_DISPLAY_DURATION),
            IsOngoing = isOngoing,
        });
    }
}
