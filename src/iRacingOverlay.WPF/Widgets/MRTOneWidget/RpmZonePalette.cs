using System.Windows.Media;
using iRacingOverlay.Core.Models;

namespace iRacingOverlay.WPF.Widgets.MRTOneWidget;

/// <summary>
/// The colour each RPM band paints. Shared by the gauge ring, the RPM value text
/// and the ring bead so the three can never drift apart.
/// </summary>
internal static class RpmZonePalette
{
    /// <param name="zone">This frame's band, resolved on the telemetry thread.</param>
    /// <param name="optimal">The shift-now colour — the widget's secondary (orange).</param>
    /// <param name="safe">The below-the-lights colour; the bead uses a fixed teal, the ring the theme primary.</param>
    public static Color For(ShiftZone zone, Color optimal, Color safe) => zone switch
    {
        ShiftZone.Danger => Colors.Red,       // past the window or over-revving
        ShiftZone.Optimal => optimal,         // shift now
        ShiftZone.Warning => Colors.Yellow,   // lights coming on
        _ => safe,
    };
}
