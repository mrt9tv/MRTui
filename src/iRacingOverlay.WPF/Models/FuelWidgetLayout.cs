namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Fuel widget display layout options
/// </summary>
public enum FuelWidgetLayout
{
    /// <summary>
    /// Vertical tower layout (180x280) - Default
    /// Compact vertical design with all info stacked top-to-bottom
    /// </summary>
    Tower,

    /// <summary>
    /// Horizontal bar layout (380x120)
    /// Wide horizontal design with info spread left-to-right
    /// </summary>
    Bar,

    /// <summary>
    /// Grid layout (260x200)
    /// 2x2 grid with info organized in quadrants
    /// </summary>
    Grid
}
