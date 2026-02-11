using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Detects race events near the player (14s ahead, 7s behind on track).
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

    /// <summary>Detection radius in seconds AHEAD of the player.</summary>
    private const float DETECTION_AHEAD_SECONDS = 14.0f;

    /// <summary>Detection radius in seconds BEHIND the player.</summary>
    private const float DETECTION_BEHIND_SECONDS = 7.0f;

    /// <summary>Minimum speed (m/s) below which a car on track is considered "slow".</summary>
    private const float SLOW_SPEED_THRESHOLD = 5.0f;

    /// <summary>Speed below which a car on track is "stopped" (m/s).</summary>
    private const float STOPPED_SPEED_THRESHOLD = 1.0f;

    /// <summary>Minimum seconds off-track before emitting event (avoids curb touches).</summary>
    private const float OFF_TRACK_MIN_DURATION = 1.5f;

    /// <summary>Seconds a "slow car" must persist before emitting.</summary>
    private const float SLOW_CAR_MIN_DURATION = 2.0f;

    /// <summary>Minimum display duration for any event (seconds).</summary>
    private const float MIN_DISPLAY_DURATION = 3.0f;

    /// <summary>Cooldown per car per event type (seconds) to prevent spam.</summary>
    private const float EVENT_COOLDOWN = 8.0f;

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

    // ── Per-car tracking state ──────────────────────────────────────
    private readonly int[] _prevTrackSurface = new int[MAX_CARS];
    private readonly bool[] _prevOnPitRoad = new bool[MAX_CARS];
    private readonly int[] _prevIncidentCount = new int[MAX_CARS]; // from CarIdxSessionFlags incident bits
    private readonly float[] _offTrackDuration = new float[MAX_CARS];
    private readonly float[] _slowDuration = new float[MAX_CARS];
    private readonly bool[] _wasInPitStall = new bool[MAX_CARS];
    private readonly int[] _prevFlags = new int[MAX_CARS];
    private readonly float[] _prevLapDistPct = new float[MAX_CARS];

    // ── Cooldown tracking (per car × event type) ────────────────────
    private readonly Dictionary<(int carIdx, NearbyEventType type), DateTime> _cooldowns = new();

    // ── Active event list ───────────────────────────────────────────
    private readonly List<NearbyEvent> _activeEvents = new(MAX_ACTIVE_EVENTS * 2);
    private long _nextEventId = 1;
    private DateTime _lastUpdateTime = DateTime.UtcNow;

    /// <summary>Read-only snapshot of currently active (non-expired) events.</summary>
    public IReadOnlyList<NearbyEvent> ActiveEvents => _activeEvents;

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
        _cooldowns.Clear();
        _activeEvents.Clear();
        _nextEventId = 1;
        _lastUpdateTime = DateTime.UtcNow;
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

        // Clamp dt to avoid huge jumps after pause/alt-tab
        if (dt > 1.0f) dt = 1.0f / 60f;

        // Expire old events
        _activeEvents.RemoveAll(e => e.IsExpired);

        if (relativeEntries == null || relativeEntries.Count == 0) return;
        if (data.CarIdxTrackSurface == null || data.CarIdxLapDistPct == null) return;

        int playerIdx = data.PlayerCarIdx;

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

        // Only process cars within detection range (14s ahead, 7s behind)
        foreach (var entry in relativeEntries)
        {
            int i = entry.CarIdx;
            if (i == playerIdx) continue;
            if (i < 0 || i >= MAX_CARS) continue;
            if (!entry.IsConnected) continue;

            float interval = entry.IntervalToPlayer;
            // Asymmetric detection: 14s ahead (positive), 7s behind (negative)
            if (interval > DETECTION_AHEAD_SECONDS || interval < -DETECTION_BEHIND_SECONDS) continue;

            int surface = data.CarIdxTrackSurface != null && i < data.CarIdxTrackSurface.Length
                ? data.CarIdxTrackSurface[i] : SURFACE_NOT_IN_WORLD;
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
            if (onPitRoad && !_prevOnPitRoad[i] && surface != SURFACE_IN_PIT_STALL)
            {
                TryEmit(entry, NearbyEventType.Pitting, "PITTING",
                    NearbyEventSeverity.Info, 3.0f);
            }

            // ── IN BOX (entered pit stall) ──────────────────────
            if (surface == SURFACE_IN_PIT_STALL && !_wasInPitStall[i])
            {
                TryEmit(entry, NearbyEventType.InBox, "IN BOX",
                    NearbyEventSeverity.Info, 3.0f);
            }
            _wasInPitStall[i] = surface == SURFACE_IN_PIT_STALL;

            // ── PIT EXIT (left pit road back to track) ──────────
            if (!onPitRoad && _prevOnPitRoad[i] && surface == SURFACE_ON_TRACK)
            {
                TryEmit(entry, NearbyEventType.PitExit, "PIT EXIT",
                    NearbyEventSeverity.Info, 3.0f);
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

            // ── LOCAL YELLOW FLAG ───────────────────────────────
            if ((flags & FLAG_YELLOW) != 0 && (_prevFlags[i] & FLAG_YELLOW) == 0)
            {
                bool isAhead = interval > 0;
                TryEmit(entry, NearbyEventType.LocalYellow,
                    isAhead ? "⚑ YELLOW AHEAD" : "⚑ YELLOW",
                    NearbyEventSeverity.Danger, 4.0f);
            }

            // ── SLOW CAR / STOPPED (ongoing) ────────────────────
            if (surface == SURFACE_ON_TRACK && !onPitRoad)
            {
                float prevPct = _prevLapDistPct[i];
                float curPct = data.CarIdxLapDistPct![i];
                float pctDelta = curPct - prevPct;
                // Handle wrap-around
                if (pctDelta < -0.5f) pctDelta += 1.0f;
                if (pctDelta > 0.5f) pctDelta -= 1.0f;

                // Convert pctDelta to approximate m/s (trackLength * pctDelta / dt)
                float trackLen = data.TrackLength > 0 ? data.TrackLength : 4000f;
                float approxSpeed = dt > 0 ? (Math.Abs(pctDelta) * trackLen / dt) : 999f;

                if (approxSpeed < STOPPED_SPEED_THRESHOLD)
                {
                    _slowDuration[i] += dt;
                    if (_slowDuration[i] >= SLOW_CAR_MIN_DURATION)
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

            // Update previous state
            _prevTrackSurface[i] = surface;
            _prevOnPitRoad[i] = onPitRoad;
            _prevFlags[i] = flags;
            if (data.CarIdxLapDistPct != null && i < data.CarIdxLapDistPct.Length)
                _prevLapDistPct[i] = data.CarIdxLapDistPct[i];
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
    }

    // ── Internal helpers ────────────────────────────────────────────

    private void TryEmit(RelativeEntry entry, NearbyEventType type,
        string text, NearbyEventSeverity severity, float duration)
    {
        var key = (entry.CarIdx, type);

        // Cooldown check
        if (_cooldowns.TryGetValue(key, out var lastEmit) &&
            (DateTime.UtcNow - lastEmit).TotalSeconds < EVENT_COOLDOWN)
            return;

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

        _activeEvents.Add(new NearbyEvent
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
        });

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
}
