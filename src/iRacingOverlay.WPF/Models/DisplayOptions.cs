namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Display formatting options for widget values
/// </summary>
public class DisplayOptions
{
    /// <summary>
    /// Unit of measurement to display (e.g., "km/h", "L", "°C")
    /// </summary>
    public string Unit { get; set; } = "";
    
    /// <summary>
    /// Number of decimal places to show
    /// </summary>
    public int DecimalPlaces { get; set; } = 0;
    
    /// <summary>
    /// Format string for the value (e.g., "{0:F1} L", "P{0}")
    /// </summary>
    public string Format { get; set; } = "{0}";
    
    /// <summary>
    /// Font size for the displayed value
    /// </summary>
    public double FontSize { get; set; } = 24;
    
    /// <summary>
    /// Font family name
    /// </summary>
    public string FontFamily { get; set; } = "Consolas";
    
    /// <summary>
    /// Normal color (hex format)
    /// </summary>
    public string NormalColor { get; set; } = "#00FF00";
    
    /// <summary>
    /// Warning color (hex format)
    /// </summary>
    public string WarningColor { get; set; } = "#FFFF00";
    
    /// <summary>
    /// Danger/critical color (hex format)
    /// </summary>
    public string DangerColor { get; set; } = "#FF0000";
    
    /// <summary>
    /// Value threshold for warning color (optional)
    /// </summary>
    public double? WarningThreshold { get; set; }
    
    /// <summary>
    /// Value threshold for danger color (optional)
    /// </summary>
    public double? DangerThreshold { get; set; }
    
    /// <summary>
    /// If true, thresholds work in reverse (lower is bad, higher is good)
    /// </summary>
    public bool InvertThresholds { get; set; } = false;
    
    /// <summary>
    /// Determine the appropriate color based on value and thresholds
    /// </summary>
    public string GetColorForValue(double value)
    {
        if (DangerThreshold.HasValue)
        {
            if (InvertThresholds)
            {
                if (value <= DangerThreshold.Value) return DangerColor;
            }
            else
            {
                if (value >= DangerThreshold.Value) return DangerColor;
            }
        }
        
        if (WarningThreshold.HasValue)
        {
            if (InvertThresholds)
            {
                if (value <= WarningThreshold.Value) return WarningColor;
            }
            else
            {
                if (value >= WarningThreshold.Value) return WarningColor;
            }
        }
        
        return NormalColor;
    }
}
