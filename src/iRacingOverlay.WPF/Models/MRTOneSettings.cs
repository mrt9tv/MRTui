using System.Text.Json.Serialization;

namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Configuration settings specific to the MRT One widget
/// Stored in WidgetConfig.Settings dictionary
/// </summary>
public class MRTOneSettings
{
    /// <summary>
    /// Telemetry field to display in top section
    /// </summary>
    [JsonPropertyName("topField")]
    public string? TopField { get; set; } = "Speed";
    
    /// <summary>
    /// Telemetry field to display in center section (large)
    /// </summary>
    [JsonPropertyName("centerField")]
    public string? CenterField { get; set; } = "Gear";
    
    /// <summary>
    /// Telemetry field to display in bottom section
    /// </summary>
    [JsonPropertyName("bottomField")]
    public string? BottomField { get; set; } = "RPM";
    
    /// <summary>
    /// Telemetry field to display in left side box
    /// </summary>
    [JsonPropertyName("leftField")]
    public string? LeftField { get; set; } = "FuelLevel";
    
    /// <summary>
    /// Telemetry field to display in right side box
    /// </summary>
    [JsonPropertyName("rightField")]
    public string? RightField { get; set; } = "Brake";
    
    /// <summary>
    /// Show/hide top section
    /// </summary>
    [JsonPropertyName("showTop")]
    public bool ShowTop { get; set; } = true;
    
    /// <summary>
    /// Show/hide center section
    /// </summary>
    [JsonPropertyName("showCenter")]
    public bool ShowCenter { get; set; } = true;
    
    /// <summary>
    /// Show/hide bottom section
    /// </summary>
    [JsonPropertyName("showBottom")]
    public bool ShowBottom { get; set; } = true;
    
    /// <summary>
    /// Show/hide left side box
    /// </summary>
    [JsonPropertyName("showLeft")]
    public bool ShowLeft { get; set; } = true;
    
    /// <summary>
    /// Show/hide right side box
    /// </summary>
    [JsonPropertyName("showRight")]
    public bool ShowRight { get; set; } = true;
    
    /// <summary>
    /// Create default settings
    /// </summary>
    public static MRTOneSettings Default => new()
    {
        TopField = "Speed",
        CenterField = "Gear",
        BottomField = "RPM",
        LeftField = "FuelLevel",
        RightField = "Brake",
        ShowTop = true,
        ShowCenter = true,
        ShowBottom = true,
        ShowLeft = true,
        ShowRight = true
    };
    
    /// <summary>
    /// Parse TelemetryField enum from string (null-safe)
    /// </summary>
    public TelemetryField? GetTelemetryField(string? fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
            return null;
        
        if (System.Enum.TryParse<TelemetryField>(fieldName, true, out var field))
            return field;
        
        return null;
    }
    
    /// <summary>
    /// Get top field as TelemetryField enum (nullable)
    /// </summary>
    [JsonIgnore]
    public TelemetryField? TopFieldEnum => GetTelemetryField(TopField);
    
    /// <summary>
    /// Get center field as TelemetryField enum (nullable)
    /// </summary>
    [JsonIgnore]
    public TelemetryField? CenterFieldEnum => GetTelemetryField(CenterField);
    
    /// <summary>
    /// Get bottom field as TelemetryField enum (nullable)
    /// </summary>
    [JsonIgnore]
    public TelemetryField? BottomFieldEnum => GetTelemetryField(BottomField);
    
    /// <summary>
    /// Get left field as TelemetryField enum (nullable)
    /// </summary>
    [JsonIgnore]
    public TelemetryField? LeftFieldEnum => GetTelemetryField(LeftField);
    
    /// <summary>
    /// Get right field as TelemetryField enum (nullable)
    /// </summary>
    [JsonIgnore]
    public TelemetryField? RightFieldEnum => GetTelemetryField(RightField);
}
