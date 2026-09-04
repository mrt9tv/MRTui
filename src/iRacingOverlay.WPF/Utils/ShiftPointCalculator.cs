using iRacingOverlay.Core.Models;

namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Legacy façade over the shift-point logic.
///
/// The real work now lives in <see cref="Core.Services.ShiftPointService"/>, which
/// runs once per tick on the telemetry thread and publishes its result on the frame
/// (<see cref="TelemetryData.ShiftZone"/> and friends). This type stays so existing
/// call sites keep compiling, and so there is still a usable answer for any caller
/// that only has an RPM and a gear to hand.
///
/// Prefer reading <c>data.ShiftZone</c> directly in new code.
/// </summary>
public static class ShiftPointCalculator
{
    /// <summary>
    /// RPM colour zone. Kept as an alias of the Core enum so old
    /// <c>ShiftPointCalculator.RPMZone.Optimal</c> references still resolve.
    /// </summary>
    public enum RPMZone
    {
        Safe,
        Optimal,
        Warning,
        Danger,
    }

    /// <summary>Map the Core zone onto the legacy enum.</summary>
    public static RPMZone FromShiftZone(ShiftZone zone) => zone switch
    {
        ShiftZone.Warning => RPMZone.Warning,
        ShiftZone.Optimal => RPMZone.Optimal,
        ShiftZone.Danger => RPMZone.Danger,
        _ => RPMZone.Safe,
    };

    /// <summary>
    /// Read this frame's zone. This is the path new code should use — the value was
    /// already computed on the telemetry thread, so there is nothing to recalculate.
    /// </summary>
    public static RPMZone GetZone(TelemetryData data) => FromShiftZone(data.ShiftZone);

    /// <summary>
    /// The optimal shift RPM for this frame, or 0 when not yet known.
    /// </summary>
    public static float GetOptimalShiftPoint(TelemetryData data) => data.ShiftOptimalRPM;

    /// <summary>
    /// Redline used for scaling the RPM ring.
    /// Falls back to a plausible default rather than returning zero, so callers that
    /// divide by it cannot produce infinity.
    /// </summary>
    public static float GetRedline(TelemetryData data) =>
        data.ShiftRedlineRPM > 1000f ? data.ShiftRedlineRPM : 8500f;

    /// <summary>
    /// RPM as a share of redline, 0-100.
    /// </summary>
    public static float GetRPMPercentage(TelemetryData data)
    {
        float redline = GetRedline(data);
        return redline <= 0 ? 0f : System.Math.Min(100f, data.RPM / redline * 100f);
    }

    /// <summary>
    /// Whether the driver should be shifting right now: in the shift-now window, on
    /// throttle, and in a forward gear.
    /// </summary>
    public static bool ShouldShift(TelemetryData data)
    {
        if (data.Gear <= 0) return false;
        if (data.Throttle < 0.5f) return false;

        return data.ShiftZone == ShiftZone.Optimal;
    }
}
