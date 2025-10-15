using System;
using System.Collections.Generic;

namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Configuration for a single widget instance
/// Serializable to JSON for save/load
/// </summary>
public class WidgetConfig
{
    /// <summary>
    /// Unique identifier for this widget instance
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Type of widget
    /// </summary>
    public WidgetType Type { get; set; }

    /// <summary>
    /// Window position - X coordinate
    /// </summary>
    public double X { get; set; } = 100;

    /// <summary>
    /// Window position - Y coordinate
    /// </summary>
    public double Y { get; set; } = 100;

    /// <summary>
    /// Window width
    /// </summary>
    public double Width { get; set; } = 300;

    /// <summary>
    /// Window height
    /// </summary>
    public double Height { get; set; } = 200;

    /// <summary>
    /// Window opacity (0.0 to 1.0)
    /// </summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// Widget-specific settings (different for each widget type)
    /// Example: { "showKph": true, "showMph": false, "fontSize": 24 }
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = new();

    /// <summary>
    /// Whether the widget is currently visible
    /// </summary>
    public bool IsVisible { get; set; } = true;
}
