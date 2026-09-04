using System;

namespace iRacingOverlay.Core.Models;

/// <summary>
/// High-frequency telemetry fields that changed since the previous frame.
///
/// Only fields worth gating UI work on belong here — the point is to let a widget
/// skip formatting and assigning a value that has not moved, since assigning a
/// TextBlock's Text invalidates measure and arrange even when the new string is
/// equal to the old one.
/// </summary>
[Flags]
public enum TelemetryChanges
{
    None = 0,
    Speed = 1 << 0,
    RPM = 1 << 1,
    Gear = 1 << 2,
    Throttle = 1 << 3,
    Brake = 1 << 4,
    FuelLevel = 1 << 5,
    Lap = 1 << 6,
    LapDistPct = 1 << 7,
}
