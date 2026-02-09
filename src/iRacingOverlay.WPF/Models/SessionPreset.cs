namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Represents a per-session-type widget configuration preset.
/// Each session type (Practice, Qualifying, Race) can have its own
/// set of visible widgets and toggle overrides.
/// </summary>
public class SessionPreset
{
    /// <summary>The session type this preset applies to.</summary>
    public SessionCategory Category { get; set; }

    /// <summary>Which widgets should be visible in this session type.</summary>
    public Dictionary<WidgetType, bool> WidgetVisibility { get; set; } = new();

    /// <summary>
    /// Per-widget settings overrides (optional).
    /// Key = WidgetType, Value = dictionary of setting key → value.
    /// Only settings present here will be overridden; others keep their current values.
    /// </summary>
    public Dictionary<WidgetType, Dictionary<string, object>> SettingOverrides { get; set; } = new();
}

/// <summary>
/// Session category for auto-configuration.
/// Mapped from iRacing's SessionType string.
/// </summary>
public enum SessionCategory
{
    /// <summary>Unknown or unmapped session type</summary>
    Unknown,

    /// <summary>Practice or Open Practice</summary>
    Practice,

    /// <summary>Lone Qualifying or Open Qualifying</summary>
    Qualifying,

    /// <summary>Race session (green flag to checkered)</summary>
    Race,

    /// <summary>Warmup session</summary>
    Warmup
}
