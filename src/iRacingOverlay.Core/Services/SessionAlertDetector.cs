using System;
using System.Collections.Generic;
using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Watches the player's own car and the session for conditions worth telling the
/// driver about, and emits them as <see cref="NearbyEvent"/>s.
///
/// These come from channels the app subscribed as a result of the telemetry
/// capability audit — track wetness, engine warnings, connection quality, FFB
/// clipping and tire set count — none of which were being read before.
///
/// They deliberately reuse the proximity feed rather than adding four more
/// widgets: the driver already watches that strip, and the settings surface is
/// dense enough without a widget per alert type.
///
/// Every alert is edge-triggered with a cooldown, so a condition that persists
/// (steady rain, a permanently poor connection) is announced once rather than
/// re-fired every frame.
/// </summary>
public sealed class SessionAlertDetector
{
    /// <summary>Session-level events carry no car index.</summary>
    private const int SessionCarIdx = -1;

    /// <summary>Seconds before the same alert may fire again.</summary>
    private const double CooldownSeconds = 45.0;

    /// <summary>FFB must clip for this long before it is worth mentioning.</summary>
    private const double FfbClipSustainSeconds = 2.0;

    /// <summary>Tire sets at or below this trigger a warning.</summary>
    private const int LowTireSetThreshold = 1;

    private readonly Dictionary<NearbyEventType, DateTime> _lastFired = new();

    // Previous values, for edge detection
    private int _prevWetness = -1;
    private bool _prevDeclaredWet;
    private int _prevEngineWarnings;
    private bool _prevPoorConnection;
    private int _prevTireSets = -1;

    private DateTime? _ffbClipStart;
    private bool _ffbAnnounced;

    // The player's own car. NearbyEventDetector skips the player when it walks
    // the field for flags and incidents, so nothing else reports these.
    private int _prevPlayerFlags;
    private int _prevIncidents = -1;
    private bool? _prevPitsOpen;

    // Per-car SessionFlags bits, as iRacing publishes them in CarIdxSessionFlags.
    private const int FlagBlack = 0x10000;
    private const int FlagDisqualify = 0x20000;
    private const int FlagRepair = 0x100000;

    /// <summary>
    /// Incidents in one hit at or above this get a warning rather than a note;
    /// 4x is iRacing's contact penalty and the first sign of a real accident.
    /// </summary>
    private const int IncidentWarningStep = 4;

    /// <summary>Reset all state. Call on disconnect or a new session.</summary>
    public void Reset()
    {
        _lastFired.Clear();
        _prevWetness = -1;
        _prevDeclaredWet = false;
        _prevEngineWarnings = 0;
        _prevPoorConnection = false;
        _prevTireSets = -1;
        _ffbClipStart = null;
        _ffbAnnounced = false;
        _prevPlayerFlags = 0;
        _prevIncidents = -1;
        _prevPitsOpen = null;
    }

    /// <summary>
    /// Evaluate the frame and append any new alerts to <paramref name="output"/>.
    /// </summary>
    public void Update(TelemetryData data, List<NearbyEvent> output, ref long nextEventId)
    {
        // Only meaningful while actually driving — not in the garage or a replay.
        if (!data.IsOnTrack && !data.OnPitRoad) return;

        var now = DateTime.UtcNow;

        CheckWeather(data, output, ref nextEventId, now);
        CheckEngine(data, output, ref nextEventId, now);
        CheckConnection(data, output, ref nextEventId, now);
        CheckFfbClipping(data, output, ref nextEventId, now);
        CheckTireSets(data, output, ref nextEventId, now);
        CheckPlayerFlags(data, output, ref nextEventId, now);
        CheckIncidents(data, output, ref nextEventId, now);
        CheckPitLane(data, output, ref nextEventId, now);
    }

    // ── The player's own flags ────────────────────────────────────────

    private void CheckPlayerFlags(TelemetryData data, List<NearbyEvent> output, ref long nextId, DateTime now)
    {
        var flags = data.CarIdxSessionFlags;
        int idx = data.PlayerCarIdx;
        if (flags == null || idx < 0 || idx >= flags.Length) return;

        int current = flags[idx];
        int raised = current & ~_prevPlayerFlags;
        _prevPlayerFlags = current;

        if (raised == 0) return;

        // Each of these is a race-changing instruction; none may be lost to the
        // cooldown another flag started.
        if ((raised & FlagDisqualify) != 0)
            Emit(output, ref nextId, now, NearbyEventType.Disqualified,
                 NearbyEventSeverity.Critical, "DISQUALIFIED", cooldownSeconds: 0);

        if ((raised & FlagBlack) != 0)
            Emit(output, ref nextId, now, NearbyEventType.BlackFlag,
                 NearbyEventSeverity.Critical, "BLACK FLAG — SERVE PENALTY", cooldownSeconds: 0);

        if ((raised & FlagRepair) != 0)
            Emit(output, ref nextId, now, NearbyEventType.MeatballFlag,
                 NearbyEventSeverity.Critical, "MEATBALL — PIT FOR REPAIRS", cooldownSeconds: 0);
    }

    // ── Incidents ─────────────────────────────────────────────────────

    private void CheckIncidents(TelemetryData data, List<NearbyEvent> output, ref long nextId, DateTime now)
    {
        int count = data.PlayerCarMyIncidentCount;

        // Seed silently: joining mid-session with 6x already is not news.
        if (_prevIncidents < 0) { _prevIncidents = count; return; }

        int gained = count - _prevIncidents;
        _prevIncidents = count;

        if (gained <= 0) return;

        // No cooldown — two incidents thirty seconds apart are two pieces of news.
        Emit(output, ref nextId, now, NearbyEventType.IncidentGained,
             gained >= IncidentWarningStep ? NearbyEventSeverity.Warning : NearbyEventSeverity.Info,
             $"+{gained}x  ({count}x)", cooldownSeconds: 0);
    }

    // ── Pit lane ──────────────────────────────────────────────────────

    private void CheckPitLane(TelemetryData data, List<NearbyEvent> output, ref long nextId, DateTime now)
    {
        bool open = data.PitsOpen;

        if (_prevPitsOpen == null) { _prevPitsOpen = open; return; }
        if (open == _prevPitsOpen) return;
        _prevPitsOpen = open;

        // Closing matters more than opening: a planned stop just became a
        // drive-through risk.
        Emit(output, ref nextId, now, NearbyEventType.PitLaneStatus,
             open ? NearbyEventSeverity.Info : NearbyEventSeverity.Warning,
             open ? "PITS OPEN" : "PITS CLOSED", cooldownSeconds: 0);
    }

    // ── Weather ───────────────────────────────────────────────────────

    private void CheckWeather(TelemetryData data, List<NearbyEvent> output, ref long nextId, DateTime now)
    {
        // TrackWetness is only populated in sessions with the rain system enabled;
        // Unknown (0) means "no rain system", not "dry".
        if (data.TrackWetness > (int)TrackWetnessLevel.Unknown)
        {
            if (_prevWetness < 0) _prevWetness = data.TrackWetness;

            if (data.TrackWetness != _prevWetness)
            {
                bool worsening = data.TrackWetness > _prevWetness;
                _prevWetness = data.TrackWetness;

                var severity = TelemetryStatus.IsWet(data.TrackWetness)
                    ? NearbyEventSeverity.Warning
                    : NearbyEventSeverity.Info;

                Emit(output, ref nextId, now, NearbyEventType.WeatherChange, severity,
                     $"{(worsening ? "▲" : "▼")} {TelemetryStatus.WetnessLabel(data.TrackWetness)}");
            }
        }

        // The moment wet tires become legal is the one that changes strategy.
        if (data.WeatherDeclaredWet && !_prevDeclaredWet)
        {
            Emit(output, ref nextId, now, NearbyEventType.DeclaredWet,
                 NearbyEventSeverity.Danger, "SESSION DECLARED WET");
        }
        _prevDeclaredWet = data.WeatherDeclaredWet;
    }

    // ── Engine ────────────────────────────────────────────────────────

    private void CheckEngine(TelemetryData data, List<NearbyEvent> output, ref long nextId, DateTime now)
    {
        // Mask off the two flags that are normal operating states rather than faults.
        const int faultMask = ~(int)(EngineWarningFlags.PitSpeedLimiter | EngineWarningFlags.RevLimiterActive);

        int faults = data.EngineWarnings & faultMask;
        int newFaults = faults & ~_prevEngineWarnings;
        _prevEngineWarnings = faults;

        if (newFaults == 0) return;

        var message = TelemetryStatus.MostUrgentEngineWarning(newFaults);
        if (message == null) return;

        Emit(output, ref nextId, now, NearbyEventType.EngineWarning,
             NearbyEventSeverity.Critical, message);
    }

    // ── Connection ────────────────────────────────────────────────────

    private void CheckConnection(TelemetryData data, List<NearbyEvent> output, ref long nextId, DateTime now)
    {
        // ChanQuality reads 0 before the first network sample; do not warn on that.
        if (data.ChanQuality <= 0f) return;

        bool poor = data.ChanQuality < TelemetryStatus.PoorConnectionQuality;

        if (poor && !_prevPoorConnection)
        {
            Emit(output, ref nextId, now, NearbyEventType.PoorConnection,
                 NearbyEventSeverity.Warning,
                 $"CONNECTION {data.ChanQuality * 100:F0}%");
        }
        _prevPoorConnection = poor;
    }

    // ── Force feedback ────────────────────────────────────────────────

    private void CheckFfbClipping(TelemetryData data, List<NearbyEvent> output, ref long nextId, DateTime now)
    {
        // No wheel configured, or torque not reported.
        if (data.SteeringWheelMaxForceNm <= 0f) return;

        if (!TelemetryStatus.IsFfbClipping(data.SteeringWheelPctTorque))
        {
            _ffbClipStart = null;
            _ffbAnnounced = false;
            return;
        }

        _ffbClipStart ??= now;

        // Momentary clipping over a kerb is normal; sustained clipping is a setup problem.
        if (_ffbAnnounced) return;
        if ((now - _ffbClipStart.Value).TotalSeconds < FfbClipSustainSeconds) return;

        _ffbAnnounced = true;
        Emit(output, ref nextId, now, NearbyEventType.FfbClipping,
             NearbyEventSeverity.Info, "FFB CLIPPING");
    }

    // ── Tire sets ─────────────────────────────────────────────────────

    private void CheckTireSets(TelemetryData data, List<NearbyEvent> output, ref long nextId, DateTime now)
    {
        // Only meaningful in series that actually limit sets.
        if (data.DryTireSetLimit <= 0) return;
        if (data.TireSetsAvailable < 0) return;

        if (_prevTireSets < 0) { _prevTireSets = data.TireSetsAvailable; return; }

        bool dropped = data.TireSetsAvailable < _prevTireSets;
        _prevTireSets = data.TireSetsAvailable;

        if (!dropped || data.TireSetsAvailable > LowTireSetThreshold) return;

        Emit(output, ref nextId, now, NearbyEventType.LowTireSets,
             NearbyEventSeverity.Warning,
             data.TireSetsAvailable == 0 ? "NO TYRE SETS LEFT" : $"{data.TireSetsAvailable} TYRE SET LEFT");
    }

    // ── Emission ──────────────────────────────────────────────────────

    private void Emit(List<NearbyEvent> output, ref long nextId, DateTime now,
                      NearbyEventType type, NearbyEventSeverity severity, string text,
                      double cooldownSeconds = CooldownSeconds)
    {
        if (_lastFired.TryGetValue(type, out var last)
            && (now - last).TotalSeconds < cooldownSeconds)
        {
            return;
        }

        _lastFired[type] = now;

        output.Add(new NearbyEvent
        {
            Id = nextId++,
            CarIdx = SessionCarIdx,
            EventType = type,
            DisplayText = text,
            Severity = severity,
            DisplayDuration = 6.0f,
        });
    }
}
