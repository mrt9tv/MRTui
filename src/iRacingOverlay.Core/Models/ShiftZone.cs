namespace iRacingOverlay.Core.Models;

/// <summary>
/// RPM colour band, used by the gauge ring, the RPM bead and the RPM value text.
///
/// The bands are ordered by engine speed, not by severity: Warning sits below
/// Optimal because the yellow "lights are coming on" band comes before the orange
/// "shift now" window.
/// </summary>
public enum ShiftZone
{
    /// <summary>Below the shift lights. Theme colour.</summary>
    Safe,

    /// <summary>Shift lights illuminating — prepare to shift. Yellow.</summary>
    Warning,

    /// <summary>The shift-now window. Orange.</summary>
    Optimal,

    /// <summary>Past the window, or over-revving. Red.</summary>
    Danger,
}
