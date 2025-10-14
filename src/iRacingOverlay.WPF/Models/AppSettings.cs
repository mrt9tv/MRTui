namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Global application settings for all widgets
/// </summary>
public class AppSettings
{
    private static AppSettings? _instance;
    
    /// <summary>
    /// Singleton instance
    /// </summary>
    public static AppSettings Instance => _instance ??= new AppSettings();
    
    /// <summary>
    /// Use metric units (km/h, L, °C) or imperial (mph, gal, °F)
    /// </summary>
    public bool UseMetricUnits { get; set; } = true;
    
    /// <summary>
    /// Primary theme color (teal)
    /// </summary>
    public string PrimaryColor { get; set; } = "#008080";
    
    /// <summary>
    /// Secondary theme color (orange)
    /// </summary>
    public string SecondaryColor { get; set; } = "#FF8000";
    
    /// <summary>
    /// Default widget background opacity (0.0 to 1.0)
    /// </summary>
    public double DefaultOpacity { get; set; } = 0.85;
    
    /// <summary>
    /// Custom short labels for telemetry fields (e.g., "THR" instead of "THRTL")
    /// </summary>
    public Dictionary<string, string> CustomLabels { get; set; } = new Dictionary<string, string>
    {
        { "Throttle", "THRTL" },
        { "Brake", "BRAKE" },
        { "Clutch", "CLUTCH" },
        { "FuelLevel", "FUEL" },
        { "WaterTemp", "WATER" },
        { "OilTemp", "OIL" },
        { "Speed", "SPEED" },
        { "RPM", "RPM" },
        { "Gear", "GEAR" }
    };
    
    // Fuel Widget Settings
    /// <summary>
    /// Fuel Widget X position
    /// </summary>
    public double? FuelWidgetX { get; set; }
    
    /// <summary>
    /// Fuel Widget Y position
    /// </summary>
    public double? FuelWidgetY { get; set; }
    
    /// <summary>
    /// Fuel Widget size (percentage, 50-150)
    /// </summary>
    public int? FuelWidgetSize { get; set; }
    
    /// <summary>
    /// Whether windows are locked (prevents dragging)
    /// </summary>
    public bool LockWindows { get; set; } = false;
    
    /// <summary>
    /// Global opacity for all widgets (0.0 to 1.0)
    /// </summary>
    public double Opacity { get; set; } = 0.9;
    
    /// <summary>
    /// Use metric units (true) or imperial (false)
    /// </summary>
    public bool UseMetric { get; set; } = true;
    
    /// <summary>
    /// Manager window opacity (0.2 to 1.0, enforced minimum 20%)
    /// </summary>
    public double ManagerOpacity { get; set; } = 0.95;
    
    /// <summary>
    /// Keep manager window always on top
    /// </summary>
    public bool AlwaysOnTop { get; set; } = false;
    
    /// <summary>
    /// Start manager window minimized to taskbar
    /// </summary>
    public bool StartMinimized { get; set; } = false;
    
    /// <summary>
    /// Event raised when settings change
    /// </summary>
    public event EventHandler? SettingsChanged;
    
    /// <summary>
    /// Notify listeners that settings have changed
    /// </summary>
    public void NotifyChanged()
    {
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Save settings to file
    /// </summary>
    public void Save()
    {
        // TODO: Implement JSON serialization to file
        // For now, just notify listeners
        NotifyChanged();
    }
}
