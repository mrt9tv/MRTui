namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Types of widgets available in the overlay system
/// </summary>
public enum WidgetType
{
    /// <summary>
    /// MRT One - Circular gauge showing Gear (center) with Speed and RPM indicators
    /// </summary>
    MRTOne,

    /// <summary>
    /// Table showing multiple telemetry values
    /// </summary>
    TelemetryTable,

    /// <summary>
    /// RPM gauge (circular dial) - Future MVP 3
    /// </summary>
    RPMGauge,

    /// <summary>
    /// Fuel level and consumption widget - Future MVP 3
    /// </summary>
    Fuel,

    /// <summary>
    /// Water and oil temperature gauges - Future MVP 3
    /// </summary>
    Temperature,

    /// <summary>
    /// Current, best, and delta lap times - Future MVP 3
    /// </summary>
    LapTimes,

    /// <summary>
    /// Gear indicator - Future MVP 3
    /// </summary>
    Gear,

    /// <summary>
    /// Input bars (throttle, brake, clutch, steering) - Future MVP 3
    /// </summary>
    Inputs,

    /// <summary>
    /// Tire temperatures and wear - Future MVP 3+
    /// </summary>
    Tires,

    /// <summary>
    /// Race Strategy Widget - Phase 10 (Multi-stint planning, what-if scenarios)
    /// </summary>
    RaceStrategy,

    /// <summary>
    /// Mode Control Widget - Master control panel for switching operational modes
    /// </summary>
    ModeControl
}

/// <summary>
/// Helper class for WidgetType display names
/// </summary>
public static class WidgetTypeExtensions
{
    /// <summary>
    /// Get user-friendly display name for a widget type
    /// </summary>
    public static string GetDisplayName(this WidgetType type)
    {
        return type switch
        {
            WidgetType.MRTOne => "MRT One",
            WidgetType.Fuel => "Fuel Calculator",
            WidgetType.RaceStrategy => "Race Strategy",
            WidgetType.TelemetryTable => "Telemetry Table",
            WidgetType.RPMGauge => "RPM Gauge",
            WidgetType.Temperature => "Temperature",
            WidgetType.LapTimes => "Lap Times",
            WidgetType.Gear => "Gear Indicator",
            WidgetType.Inputs => "Input Bars",
            WidgetType.Tires => "Tire Monitor",
            WidgetType.ModeControl => "Mode Control",
            _ => type.ToString()
        };
    }
}
