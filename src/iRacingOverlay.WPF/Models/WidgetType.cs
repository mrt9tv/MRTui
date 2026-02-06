namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Available widget types in the overlay system.
/// </summary>
public enum WidgetType
{
    /// <summary>
    /// MRT One — circular gauge with gear, speed, RPM, 4-way radar, and fuel display.
    /// </summary>
    MRTOne
}

/// <summary>
/// Display-name helper for WidgetType.
/// </summary>
public static class WidgetTypeExtensions
{
    public static string GetDisplayName(this WidgetType type) => type switch
    {
        WidgetType.MRTOne => "MRT One",
        _ => type.ToString()
    };
}
