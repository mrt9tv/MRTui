using System;
using System.Windows;

namespace iRacingOverlay.WPF.Utilities;

/// <summary>
/// Helper for snapping widget positions to a grid
/// Provides alignment and positioning assistance during widget drag operations
/// </summary>
public static class SnapToGridHelper
{
    /// <summary>
    /// Default grid size in pixels (can be overridden by settings)
    /// </summary>
    public const int DEFAULT_GRID_SIZE = 20;

    /// <summary>
    /// Distance threshold for snapping (pixels)
    /// If widget is within this distance of a grid line, it will snap
    /// </summary>
    public const int SNAP_THRESHOLD = 10;

    /// <summary>
    /// Snap a position to the nearest grid point
    /// </summary>
    /// <param name="value">Current position value (Left or Top)</param>
    /// <param name="gridSize">Grid size in pixels</param>
    /// <param name="snapEnabled">Whether snapping is enabled</param>
    /// <returns>Snapped position value</returns>
    public static double SnapToGrid(double value, int gridSize = DEFAULT_GRID_SIZE, bool snapEnabled = true)
    {
        if (!snapEnabled || gridSize <= 0)
            return value;

        // Calculate nearest grid point
        double snappedValue = Math.Round(value / gridSize) * gridSize;

        // Only snap if within threshold
        if (Math.Abs(value - snappedValue) <= SNAP_THRESHOLD)
            return snappedValue;

        return value;
    }

    /// <summary>
    /// Snap a point (X, Y coordinates) to the nearest grid point
    /// </summary>
    /// <param name="point">Current position</param>
    /// <param name="gridSize">Grid size in pixels</param>
    /// <param name="snapEnabled">Whether snapping is enabled</param>
    /// <returns>Snapped position</returns>
    public static Point SnapPointToGrid(Point point, int gridSize = DEFAULT_GRID_SIZE, bool snapEnabled = true)
    {
        return new Point(
            SnapToGrid(point.X, gridSize, snapEnabled),
            SnapToGrid(point.Y, gridSize, snapEnabled)
        );
    }

    /// <summary>
    /// Snap window position (Left, Top) to grid
    /// </summary>
    /// <param name="left">Current Left position</param>
    /// <param name="top">Current Top position</param>
    /// <param name="gridSize">Grid size in pixels</param>
    /// <param name="snapEnabled">Whether snapping is enabled</param>
    /// <returns>Tuple of snapped (Left, Top) positions</returns>
    public static (double Left, double Top) SnapWindowPosition(double left, double top, int gridSize = DEFAULT_GRID_SIZE, bool snapEnabled = true)
    {
        return (
            SnapToGrid(left, gridSize, snapEnabled),
            SnapToGrid(top, gridSize, snapEnabled)
        );
    }

    /// <summary>
    /// Calculate grid lines for visual feedback overlay
    /// </summary>
    /// <param name="screenWidth">Screen width in pixels</param>
    /// <param name="screenHeight">Screen height in pixels</param>
    /// <param name="gridSize">Grid size in pixels</param>
    /// <returns>Array of grid line positions (X coordinates, then Y coordinates)</returns>
    public static (double[] VerticalLines, double[] HorizontalLines) CalculateGridLines(
        double screenWidth, 
        double screenHeight, 
        int gridSize = DEFAULT_GRID_SIZE)
    {
        if (gridSize <= 0)
            return (Array.Empty<double>(), Array.Empty<double>());

        // Calculate vertical grid lines (X positions)
        int verticalCount = (int)Math.Ceiling(screenWidth / gridSize) + 1;
        double[] verticalLines = new double[verticalCount];
        for (int i = 0; i < verticalCount; i++)
            verticalLines[i] = i * gridSize;

        // Calculate horizontal grid lines (Y positions)
        int horizontalCount = (int)Math.Ceiling(screenHeight / gridSize) + 1;
        double[] horizontalLines = new double[horizontalCount];
        for (int i = 0; i < horizontalCount; i++)
            horizontalLines[i] = i * gridSize;

        return (verticalLines, horizontalLines);
    }

    /// <summary>
    /// Check if a position is near a grid line (for visual feedback)
    /// </summary>
    /// <param name="value">Position value to check</param>
    /// <param name="gridSize">Grid size in pixels</param>
    /// <returns>True if near a grid line</returns>
    public static bool IsNearGridLine(double value, int gridSize = DEFAULT_GRID_SIZE)
    {
        if (gridSize <= 0)
            return false;

        double nearestGridPoint = Math.Round(value / gridSize) * gridSize;
        return Math.Abs(value - nearestGridPoint) <= SNAP_THRESHOLD;
    }
}
