using System;
using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Works out the optimal shift point and the RPM colour zones around it.
///
/// SOURCE OF TRUTH, in priority order:
///
///   1. Session info (<c>DriverCarSLFirstRPM</c> / <c>SLShiftRPM</c> / <c>SLLastRPM</c> /
///      <c>SLBlinkRPM</c> and <c>DriverCarRedLine</c>). These are car-specific, come
///      from iRacing's own engine model, and are known the moment the driver is in
///      the car — before a single rev.
///   2. Live telemetry (<c>PlayerCarSL*RPM</c>). Same numbers, per frame; used when
///      session info has not arrived yet.
///   3. A learned estimate from the highest RPM observed. Last resort only.
///
/// Deliberately NOT used: <c>ShiftIndicatorPct</c>, which iRacing marks deprecated in
/// favour of <c>DriverCarSLBlinkRPM</c> — the family above — and <c>ShiftPowerPct</c>,
/// which despite the name is shift/grind friction torque, not a power-curve readout.
/// </summary>
public sealed class ShiftPointService
{
    // ── The one place to retune the shift window ──────────────────────
    //
    // Asymmetric on purpose: shifting slightly early costs almost nothing,
    // shifting late runs into the limiter. At a GT3's 8500 RPM optimal this is
    // roughly -170 / +43 RPM, a ~215 RPM band.

    /// <summary>How far below the optimal RPM the "shift now" window opens.</summary>
    public const float OptimalWindowBeforePct = 0.020f;

    /// <summary>How far above the optimal RPM the "shift now" window closes.</summary>
    public const float OptimalWindowAfterPct = 0.005f;

    /// <summary>Fallback redline when nothing at all is known yet.</summary>
    private const float DefaultRedline = 8500f;

    /// <summary>Below this, a reported redline is not believable.</summary>
    private const float MinPlausibleRedline = 1000f;

    // ── Car profile, from session info ────────────────────────────────

    private float _profileRedline;
    private float _profileFirstRpm;
    private float _profileShiftRpm;
    private float _profileLastRpm;
    private float _profileBlinkRpm;
    private int _profileForwardGears;

    // ── Learned fallback ──────────────────────────────────────────────

    private float _maxObservedRpm;

    /// <summary>Number of forward gears, or 0 when unknown.</summary>
    public int ForwardGears => _profileForwardGears;

    /// <summary>
    /// Apply the car's shift profile from session info. Values that arrive as zero
    /// (car not loaded, field absent) are ignored rather than overwriting good data.
    ///
    /// A genuinely different car resets the learned fallback: previously the learner
    /// was static, never reset, so a GT3's observed maximum kept scaling the RPM ring
    /// after switching to a Formula car and vice versa.
    /// </summary>
    public void ApplyCarProfile(float redline, float firstRpm, float shiftRpm,
                                float lastRpm, float blinkRpm, int forwardGears)
    {
        bool carChanged =
            (redline > 0 && Math.Abs(redline - _profileRedline) > 1f) ||
            (shiftRpm > 0 && Math.Abs(shiftRpm - _profileShiftRpm) > 1f);

        if (redline > 0) _profileRedline = redline;
        if (firstRpm > 0) _profileFirstRpm = firstRpm;
        if (shiftRpm > 0) _profileShiftRpm = shiftRpm;
        if (lastRpm > 0) _profileLastRpm = lastRpm;
        if (blinkRpm > 0) _profileBlinkRpm = blinkRpm;
        if (forwardGears > 0) _profileForwardGears = forwardGears;

        if (carChanged) _maxObservedRpm = 0f;
    }

    /// <summary>Forget everything. Call on disconnect.</summary>
    public void Reset()
    {
        _profileRedline = 0f;
        _profileFirstRpm = 0f;
        _profileShiftRpm = 0f;
        _profileLastRpm = 0f;
        _profileBlinkRpm = 0f;
        _profileForwardGears = 0;
        _maxObservedRpm = 0f;
    }

    /// <summary>
    /// Compute this frame's shift state and write it onto the telemetry frame.
    /// Runs once per tick on the telemetry thread.
    /// </summary>
    public void Update(TelemetryData data)
    {
        if (data.RPM > _maxObservedRpm) _maxObservedRpm = data.RPM;

        var points = ResolveShiftPoints(data);

        data.ShiftLightsOnRPM = points.First;
        data.ShiftOptimalRPM = points.Optimal;
        data.ShiftWindowStartRPM = points.WindowStart;
        data.ShiftWindowEndRPM = points.WindowEnd;
        data.ShiftRedlineRPM = points.Redline;
        data.ShiftZone = ClassifyZone(data, points);
        data.ShiftPointsAreAuthoritative = points.Authoritative;
    }

    /// <summary>Resolved shift geometry for a frame.</summary>
    public readonly record struct ShiftPoints(
        float First,
        float Optimal,
        float Blink,
        float Redline,
        float WindowStart,
        float WindowEnd,
        bool Authoritative);

    /// <summary>
    /// Pick the best available source and build a consistently ordered set of
    /// thresholds from it.
    /// </summary>
    public ShiftPoints ResolveShiftPoints(TelemetryData data)
    {
        // 1 & 2 — session-info profile, else live telemetry. Both are iRacing's own
        // shift-light values; the profile is simply available sooner.
        float first = _profileFirstRpm > 0 ? _profileFirstRpm : data.PlayerCarSLFirstRPM;
        float optimal = _profileShiftRpm > 0 ? _profileShiftRpm : data.PlayerCarSLShiftRPM;
        float last = _profileLastRpm > 0 ? _profileLastRpm : data.PlayerCarSLLastRPM;
        float blink = _profileBlinkRpm > 0 ? _profileBlinkRpm : data.PlayerCarSLBlinkRPM;

        bool authoritative = optimal > 0;

        if (!authoritative)
        {
            // 3 — learned fallback. Only reached when neither source has produced a
            // shift point yet, which in practice means the first moments of a session.
            float estimated = EstimateRedline();
            optimal = estimated * 0.93f;
            first = estimated * 0.90f;
            last = estimated * 0.96f;
            blink = estimated * 0.98f;
        }

        float redline = ResolveRedline(data, blink);

        // ── Build the "shift now" window ──────────────────────────────
        //
        // The previous implementation set the window end to
        //   Math.Max(optimal + 0.5%, LastRPM)
        // which stretched it all the way to the last shift light. On most cars
        // LastRPM sits well above ShiftRPM, so the window that was documented as
        // "at most 33-75 RPM past optimal" was routinely 300-600 RPM wide — the
        // opposite of a precise shift target. LastRPM is no longer used to size it.
        float windowStart = optimal * (1f - OptimalWindowBeforePct);
        float windowEnd = optimal * (1f + OptimalWindowAfterPct);

        // Keep the bands in a sane order regardless of what the car reports.
        // Some cars report FirstRPM at or above the window start; the yellow band
        // then has nowhere to live, so pull it below.
        if (first <= 0 || first >= windowStart)
            first = windowStart * 0.97f;

        // Never let the shift-now window reach into the over-rev blink zone.
        if (blink > 0 && windowEnd >= blink)
            windowEnd = Math.Max(optimal, blink * 0.999f);

        return new ShiftPoints(first, optimal, blink, redline, windowStart, windowEnd, authoritative);
    }

    /// <summary>
    /// Redline for scaling the RPM ring. Prefers the car's reported value, then the
    /// blink RPM, then what has actually been observed.
    /// </summary>
    private float ResolveRedline(TelemetryData data, float blink)
    {
        if (_profileRedline >= MinPlausibleRedline) return _profileRedline;
        if (data.EngineRedlineRPM >= MinPlausibleRedline) return data.EngineRedlineRPM;

        // Blink is the over-rev threshold, a little under the true limiter.
        if (blink >= MinPlausibleRedline) return blink * 1.02f;

        return EstimateRedline();
    }

    private float EstimateRedline()
    {
        if (_maxObservedRpm < MinPlausibleRedline) return DefaultRedline;

        // Assume the driver has been close to, but not exactly at, the limiter.
        return _maxObservedRpm * 1.02f;
    }

    /// <summary>
    /// Which colour band the current RPM falls in.
    /// </summary>
    private static ShiftZone ClassifyZone(TelemetryData data, in ShiftPoints p)
    {
        // Neutral and reverse never prompt.
        if (data.Gear <= 0) return ShiftZone.Safe;
        if (p.Optimal <= 0) return ShiftZone.Safe;

        float rpm = data.RPM;

        // Over-rev always shows, in every gear including top — being on the limiter
        // is worth seeing whether or not another gear is available.
        if (p.Blink > 0 && rpm >= p.Blink) return ShiftZone.Danger;
        if (rpm >= p.WindowEnd) return ShiftZone.Danger;

        if (rpm >= p.WindowStart) return ShiftZone.Optimal;
        if (rpm >= p.First) return ShiftZone.Warning;

        return ShiftZone.Safe;
    }
}
