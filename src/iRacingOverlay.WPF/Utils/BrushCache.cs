using System;
using System.Collections.Concurrent;
using System.Windows.Media;

namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Static cache for WPF Brush and Color objects to reduce GC pressure
/// Reuses brushes instead of creating new ones on every update (60Hz telemetry)
/// Thread-safe using ConcurrentDictionary
/// </summary>
public static class BrushCache
{
    // Thread-safe brush cache using color ARGB as key
    private static readonly ConcurrentDictionary<uint, SolidColorBrush> _brushCache = new();

    /// <summary>
    /// Get a cached SolidColorBrush for the specified color
    /// Creates and caches the brush if it doesn't exist
    /// </summary>
    /// <param name="color">Color to get brush for</param>
    /// <returns>Cached SolidColorBrush instance</returns>
    public static SolidColorBrush Get(Color color)
    {
        // Use ARGB value as key for fast lookups
        uint key = GetColorKey(color);

        return _brushCache.GetOrAdd(key, _ =>
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze(); // Freeze brush for better performance and thread safety
            return brush;
        });
    }

    /// <summary>
    /// Get a cached SolidColorBrush from RGB values
    /// </summary>
    public static SolidColorBrush Get(byte r, byte g, byte b)
    {
        return Get(Color.FromRgb(r, g, b));
    }

    /// <summary>
    /// Get a cached SolidColorBrush from ARGB values
    /// </summary>
    public static SolidColorBrush Get(byte a, byte r, byte g, byte b)
    {
        return Get(Color.FromArgb(a, r, g, b));
    }

    /// <summary>
    /// Clear the brush cache (useful for memory management or theme changes)
    /// </summary>
    public static void Clear()
    {
        _brushCache.Clear();
    }

    /// <summary>
    /// Get cache statistics for monitoring
    /// </summary>
    public static int CacheSize => _brushCache.Count;

    /// <summary>
    /// Generate a unique key from Color's ARGB components
    /// </summary>
    private static uint GetColorKey(Color color)
    {
        return (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
    }
}

/// <summary>
/// Commonly used brush constants for quick access
/// All brushes are frozen for performance
/// </summary>
public static class CommonBrushes
{
    // MRT Theme Colors
    public static readonly SolidColorBrush TealPrimary = BrushCache.Get(0, 240, 240);
    public static readonly SolidColorBrush OrangeAccent = BrushCache.Get(255, 153, 0);

    // Status Colors
    public static readonly SolidColorBrush Green = BrushCache.Get(0, 255, 0);
    public static readonly SolidColorBrush Yellow = BrushCache.Get(255, 255, 0);
    public static readonly SolidColorBrush Red = BrushCache.Get(255, 0, 0);
    public static readonly SolidColorBrush LimeGreen = BrushCache.Get(50, 205, 50);

    // Grayscale
    public static readonly SolidColorBrush White = BrushCache.Get(255, 255, 255);
    public static readonly SolidColorBrush Gray = BrushCache.Get(128, 128, 128);
    public static readonly SolidColorBrush DarkGray = BrushCache.Get(64, 64, 64);
    public static readonly SolidColorBrush Black = BrushCache.Get(0, 0, 0);

    // Semi-transparent versions (alpha = 180 for overlays)
    public static readonly SolidColorBrush GreenTransparent = BrushCache.Get(180, 0, 255, 0);
    public static readonly SolidColorBrush YellowTransparent = BrushCache.Get(180, 255, 255, 0);
    public static readonly SolidColorBrush RedTransparent = BrushCache.Get(180, 255, 0, 0);
    public static readonly SolidColorBrush LimeGreenTransparent = BrushCache.Get(180, 50, 205, 50);
}
