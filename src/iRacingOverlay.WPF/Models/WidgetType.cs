namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Types of widgets available in the overlay system
/// </summary>
public enum WidgetType
{
    /// <summary>
    /// Simple speed display widget
    /// </summary>
    Speed,

    /// <summary>
    /// Circular gauge showing Gear (center) with Speed and RPM indicators
    /// </summary>
    GearGauge,

    /// <summary>
    /// Table showing multiple telemetry values
    /// </summary>
    TelemetryTable,

    /// <summary>
    /// 2x3 grid of customizable telemetry data cells
    /// </summary>
    Data,

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
    Tires
}
