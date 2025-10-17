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
    public const string APP_VERSION = "0.6.3";

    /// <summary>
    /// Formatted version string with 'v' prefix for display.
    /// Example: "v0.5.0"
    /// </summary>
    public static string DisplayVersion => $"v{APP_VERSION}";

    /// <summary>
    /// Full version string with "Version" prefix for status bar.
    /// Example: "Version 0.5.0"
    /// </summary>
    public static string FullVersion => $"Version {APP_VERSION}";

    /// <summary>
    /// iRacing SDK version currently in use.
    /// </summary>
    public const string IRACING_SDK_VERSION = "2024.10.01.01";
}
