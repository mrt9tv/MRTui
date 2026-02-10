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
    public string? TopField { get; set; } = "RPM";
    
    /// <summary>
    /// Telemetry field to display in center section (large)
    /// </summary>
    [JsonPropertyName("centerField")]
    public string? CenterField { get; set; } = "Gear";
    
    /// <summary>
    /// Telemetry field to display in bottom section
    /// </summary>
    [JsonPropertyName("bottomField")]
    public string? BottomField { get; set; } = "Speed";
    
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
    
    // ============================================
    // PHASE 2: Visual Enhancement Toggles
    // ============================================
    
    /// <summary>
    /// Enable gradient background (radial gradient for depth effect) - ON by default
    /// </summary>
    [JsonPropertyName("enableGradientBackground")]
    public bool EnableGradientBackground { get; set; } = true;
    
    /// <summary>
    /// Enable animated shift point ring (arc fills as RPM approaches shift point)
    /// </summary>
    [JsonPropertyName("enableShiftPointRing")]
    public bool EnableShiftPointRing { get; set; } = false;
    
    /// <summary>
    /// Enable glow effects (drop shadows on critical elements)
    /// </summary>
    [JsonPropertyName("enableGlowEffects")]
    public bool EnableGlowEffects { get; set; } = false;

    /// <summary>
    /// Enable pit limiter indicator (fast blinking white/yellow border when limiter active) - ON by default for safety
    /// </summary>
    [JsonPropertyName("enablePitLimiterIndicator")]
    public bool EnablePitLimiterIndicator { get; set; } = true;

    /// <summary>
    /// Enable fuel display below gauge (comprehensive fuel data with laps remaining) - ON by default
    /// </summary>
    [JsonPropertyName("enableFuelDisplay")]
    public bool EnableFuelDisplay { get; set; } = true;

    /// <summary>
    /// Enable multi-stint pit strategy display (1-stop, 2-stop scenarios, optimal windows) - OFF by default
    /// </summary>
    [JsonPropertyName("enableFuelStrategy")]
    public bool EnableFuelStrategy { get; set; } = false;

    /// <summary>
    /// Enable enhanced radar visibility (larger squares 16px vs 12px, thicker borders 2px vs 1px) - OFF by default (legacy mode)
    /// </summary>
    [JsonPropertyName("enableEnhancedRadar")]
    public bool EnableEnhancedRadar { get; set; } = false;

    // ============================================
    // Contextual Center Auto-Swap (Priority Engine)
    // ============================================

    /// <summary>
    /// Master toggle for contextual center auto-swap — when enabled, a priority engine
    /// temporarily overrides the center field based on race conditions.
    /// </summary>
    [JsonPropertyName("enableAutoSwap")]
    public bool EnableAutoSwap { get; set; } = false;

    /// <summary>
    /// Auto-swap trigger: show "PIT NOW" when fuel is critical (below threshold laps)
    /// </summary>
    [JsonPropertyName("autoSwapFuelCritical")]
    public bool AutoSwapFuelCritical { get; set; } = true;

    /// <summary>
    /// Auto-swap trigger: show fuel remaining during yellow/caution flag
    /// </summary>
    [JsonPropertyName("autoSwapYellowFlag")]
    public bool AutoSwapYellowFlag { get; set; } = true;

    /// <summary>
    /// Auto-swap trigger: show lateral indicator (LEFT/RIGHT/BOTH) when car alongside
    /// </summary>
    [JsonPropertyName("autoSwapCarAlongside")]
    public bool AutoSwapCarAlongside { get; set; } = true;

    /// <summary>
    /// Auto-swap trigger: show pit service status when in pit stall
    /// </summary>
    [JsonPropertyName("autoSwapPitService")]
    public bool AutoSwapPitService { get; set; } = true;

    /// <summary>
    /// Fuel critical threshold in doable laps for PIT NOW trigger (default 1.5L)
    /// </summary>
    [JsonPropertyName("autoSwapFuelThreshold")]
    public float AutoSwapFuelThreshold { get; set; } = 1.5f;

    /// <summary>
    /// Hold time in seconds before reverting to default after trigger clears
    /// </summary>
    [JsonPropertyName("autoSwapHoldSeconds")]
    public float AutoSwapHoldSeconds { get; set; } = 2.0f;

    /// <summary>
    /// Create default settings
    /// </summary>
    public static MRTOneSettings Default => new()
    {
        TopField = "RPM",
        CenterField = "Gear",
        BottomField = "Speed",
        LeftField = "FuelLevel",
        RightField = "Brake",
        ShowTop = true,
        ShowCenter = true,
        ShowBottom = true,
        ShowLeft = true,
        ShowRight = true,
        // Visual enhancements
        EnableGradientBackground = true,  // ON by default
        EnableShiftPointRing = false,
        EnableGlowEffects = false,
        EnablePitLimiterIndicator = true,  // ON by default for safety
        EnableFuelDisplay = true,  // ON by default
        EnableFuelStrategy = false,  // OFF by default (advanced feature)
        EnableEnhancedRadar = false,  // OFF by default (legacy mode 12px squares)
        // Auto-swap settings
        EnableAutoSwap = false,  // OFF by default (must opt-in)
        AutoSwapFuelCritical = true,
        AutoSwapYellowFlag = true,
        AutoSwapCarAlongside = true,
        AutoSwapPitService = true,
        AutoSwapFuelThreshold = 1.5f,
        AutoSwapHoldSeconds = 2.0f
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
