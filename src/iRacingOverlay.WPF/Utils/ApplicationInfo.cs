using System;

namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Centralized application runtime information.
/// Single source of truth for application lifecycle data.
/// </summary>
public static class ApplicationInfo
{
    /// <summary>
    /// Application start time (set once in App.xaml.cs).
    /// Used for calculating uptime consistently across all views.
    /// </summary>
    public static DateTime ApplicationStartTime { get; set; } = DateTime.Now;

    /// <summary>
    /// Calculate current application uptime.
    /// </summary>
    public static TimeSpan GetUptime() => DateTime.Now - ApplicationStartTime;

    /// <summary>
    /// Format uptime as HH:MM:SS string.
    /// </summary>
    public static string GetUptimeFormatted()
    {
        var uptime = GetUptime();
        return $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }
}
