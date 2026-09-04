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
                      NearbyEventType type, NearbyEventSeverity severity, string text)
    {
        if (_lastFired.TryGetValue(type, out var last)
            && (now - last).TotalSeconds < CooldownSeconds)
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
