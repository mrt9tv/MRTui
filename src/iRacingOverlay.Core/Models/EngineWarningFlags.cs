using System;

namespace iRacingOverlay.Core.Models;

/// <summary>
/// Bit flags carried by <see cref="TelemetryData.EngineWarnings"/>.
///
/// Mirrors the SDK's EngineWarnings enum. Two of these the app previously derived
/// the hard way — the pit limiter had its own detection path and the rev limiter was
/// inferred from RPM zones — and the rest were simply not surfaced at all.
/// </summary>
[Flags]
public enum EngineWarningFlags
{
    None = 0,
    WaterTempWarning = 0x01,
    FuelPressureWarning = 0x02,
    OilPressureWarning = 0x04,
    EngineStalled = 0x08,
    PitSpeedLimiter = 0x10,
    RevLimiterActive = 0x20,
    OilTempWarning = 0x40,
}

/// <summary>
/// Track wetness, as reported by <see cref="TelemetryData.TrackWetness"/>.
/// Only meaningful in sessions with the rain system enabled.
/// </summary>
public enum TrackWetnessLevel
{
    Unknown = 0,
    Dry = 1,
    MostlyDry = 2,
    VeryLightlyWet = 3,
    LightlyWet = 4,
    ModeratelyWet = 5,
    VeryWet = 6,
    ExtremelyWet = 7,
}

/// <summary>Helpers for interpreting the newly subscribed status channels.</summary>
public static class TelemetryStatus
{
    /// <summary>Short label for a wetness level, for compact overlay display.</summary>
    public static string WetnessLabel(int wetness) => (TrackWetnessLevel)wetness switch
    {
        TrackWetnessLevel.Dry => "DRY",
        TrackWetnessLevel.MostlyDry => "MOSTLY DRY",
        TrackWetnessLevel.VeryLightlyWet => "DAMP",
        TrackWetnessLevel.LightlyWet => "LIGHT WET",
        TrackWetnessLevel.ModeratelyWet => "WET",
        TrackWetnessLevel.VeryWet => "VERY WET",
        TrackWetnessLevel.ExtremelyWet => "FLOODED",
        _ => "",
    };

    /// <summary>True once the surface is wet enough that dry tires are compromised.</summary>
    public static bool IsWet(int wetness) => wetness >= (int)TrackWetnessLevel.LightlyWet;

    /// <summary>
    /// The most urgent engine warning to display, or null when the engine is happy.
    /// Ordered by how quickly it ends your race.
    /// </summary>
    public static string? MostUrgentEngineWarning(int warnings)
    {
        var flags = (EngineWarningFlags)warnings;

        if (flags.HasFlag(EngineWarningFlags.EngineStalled)) return "STALLED";
        if (flags.HasFlag(EngineWarningFlags.OilPressureWarning)) return "OIL PRESS";
        if (flags.HasFlag(EngineWarningFlags.FuelPressureWarning)) return "FUEL PRESS";
        if (flags.HasFlag(EngineWarningFlags.WaterTempWarning)) return "WATER TEMP";
        if (flags.HasFlag(EngineWarningFlags.OilTempWarning)) return "OIL TEMP";

        // Rev limiter and pit limiter are normal operating states, not faults.
        return null;
    }

    /// <summary>
    /// Force-feedback clipping: torque at or beyond the configured maximum, where
    /// every signal pins and the driver stops feeling the front axle.
    /// </summary>
    public static bool IsFfbClipping(float pctTorque) => pctTorque >= 0.99f;

    /// <summary>Connection quality below this warrants warning the driver.</summary>
    public const float PoorConnectionQuality = 0.92f;
}
