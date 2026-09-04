using System.Windows.Controls;
using System.Windows.Media;

namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Change-guarded setters for the properties widgets write every frame.
///
/// Assigning <see cref="TextBlock.Text"/> invalidates measure and arrange even
/// when the new string is equal to the old one, because each frame formats a
/// fresh (non-reference-equal) string. At 60 Hz across a handful of elements per
/// widget that is a full layout pass per frame for values that mostly have not
/// moved. Comparing first turns most frames into no-ops.
///
/// Brushes behave differently: WPF's dependency property system already skips
/// invalidation when the value is reference-equal, and BrushCache hands out
/// shared frozen instances — so brush assignment is only guarded here for
/// symmetry and to avoid the property-system round trip.
/// </summary>
public static class UiUpdate
{
    /// <summary>Set text only if it differs from what is already displayed.</summary>
    public static void SetText(TextBlock target, string value)
    {
        if (!string.Equals(target.Text, value, System.StringComparison.Ordinal))
            target.Text = value;
    }

    /// <summary>Set foreground only if a different brush instance is being applied.</summary>
    public static void SetForeground(TextBlock target, Brush brush)
    {
        if (!ReferenceEquals(target.Foreground, brush))
            target.Foreground = brush;
    }

    /// <summary>Set a shape or control's fill only when it actually changes.</summary>
    public static void SetFill(System.Windows.Shapes.Shape target, Brush brush)
    {
        if (!ReferenceEquals(target.Fill, brush))
            target.Fill = brush;
    }

    /// <summary>Set a shape's stroke only when it actually changes.</summary>
    public static void SetStroke(System.Windows.Shapes.Shape target, Brush brush)
    {
        if (!ReferenceEquals(target.Stroke, brush))
            target.Stroke = brush;
    }

    /// <summary>Set visibility only when it actually changes.</summary>
    public static void SetVisibility(System.Windows.UIElement target, System.Windows.Visibility visibility)
    {
        if (target.Visibility != visibility)
            target.Visibility = visibility;
    }
}
