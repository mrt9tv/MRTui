namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Available widget types in the overlay system.
/// </summary>
public enum WidgetType
{
    /// <summary>
    /// MRT One — circular gauge with gear, speed, RPM, 4-way radar, and fuel display.
    /// </summary>
    MRTOne,
    
    /// <summary>
    /// Turn Display — compact horizontal bar showing current turn number and name.
    /// </summary>
    TurnDisplay,
    
    /// <summary>
    /// Fuel Calculator — compact overlay showing fuel saving / lift &amp; coast data.
    /// </summary>
    FuelCalculator,

    /// <summary>
    /// Relative — proximity-sorted competitor table with time intervals and class colors.
    /// </summary>
    Relative
}

/// <summary>
/// Display-name helper for WidgetType.
/// </summary>
public static class WidgetTypeExtensions
{
    public static string GetDisplayName(this WidgetType type) => type switch
    {
        WidgetType.MRTOne => "MRT One",
        WidgetType.TurnDisplay => "Turn Display",
        WidgetType.FuelCalculator => "Fuel Calculator",
        WidgetType.Relative => "Relative",
        _ => type.ToString()
    };
}
