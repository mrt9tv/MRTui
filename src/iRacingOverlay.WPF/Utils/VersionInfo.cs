namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Centralized version information for the application.
/// Single source of truth for version strings across all views.
/// </summary>
public static class VersionInfo
{
    /// <summary>
    /// Current application version.
    /// Update this constant when releasing new versions.
    /// </summary>
    public const string APP_VERSION = "0.5.0";

    /// <summary>
    /// Formatted version string with 'v' prefix for display.
    /// </summary>
    public static string DisplayVersion => $"v{APP_VERSION}";

    /// <summary>
    /// Short version string for status bar (no 'v' prefix, no patch).
    /// </summary>
    public static string ShortVersion => $"Version {APP_VERSION.Substring(0, 3)}"; // "Version 0.5"

    /// <summary>
    /// iRacing SDK version currently in use.
    /// </summary>
    public const string IRACING_SDK_VERSION = "2024.10.01.01";
}
