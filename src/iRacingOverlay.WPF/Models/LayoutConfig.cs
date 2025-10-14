using System.Collections.Generic;

namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Complete layout configuration containing all widgets
/// Serializable to JSON for save/load
/// </summary>
public class LayoutConfig
{
    /// <summary>
    /// Layout name/description
    /// </summary>
    public string Name { get; set; } = "Default Layout";

    /// <summary>
    /// Layout version for future compatibility
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// All widget configurations in this layout
    /// </summary>
    public List<WidgetConfig> Widgets { get; set; } = new();

    /// <summary>
    /// Global hotkey to show/hide all widgets (e.g., "F12")
    /// </summary>
    public string ToggleHotkey { get; set; } = "F12";

    /// <summary>
    /// Whether all widgets are currently visible
    /// </summary>
    public bool AllVisible { get; set; } = true;
}
