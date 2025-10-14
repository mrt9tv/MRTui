namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Defines how widget data should be bound to telemetry fields
/// </summary>
public class WidgetDataBinding
{
    /// <summary>
    /// Primary telemetry field to display
    /// </summary>
    public TelemetryField PrimaryField { get; set; } = TelemetryField.None;
    
    /// <summary>
    /// Optional secondary telemetry field
    /// </summary>
    public TelemetryField? SecondaryField { get; set; }
    
    /// <summary>
    /// Optional tertiary telemetry field
    /// </summary>
    public TelemetryField? TertiaryField { get; set; }
    
    /// <summary>
    /// Display formatting options for primary field
    /// </summary>
    public DisplayOptions PrimaryDisplayOptions { get; set; } = new DisplayOptions();
    
    /// <summary>
    /// Display formatting options for secondary field
    /// </summary>
    public DisplayOptions? SecondaryDisplayOptions { get; set; }
    
    /// <summary>
    /// Display formatting options for tertiary field
    /// </summary>
    public DisplayOptions? TertiaryDisplayOptions { get; set; }
    
    /// <summary>
    /// Additional status fields to display (e.g., indicators, flags)
    /// </summary>
    public List<TelemetryField> StatusFields { get; set; } = new List<TelemetryField>();
}
