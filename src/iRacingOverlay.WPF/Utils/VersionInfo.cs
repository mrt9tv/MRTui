namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Centralized version information for the application.
/// Single source of truth for version strings across all views.
/// </summary>
public static class VersionInfo
{
    // ── Versioning Scheme ────────────────────────────────────────
    // Format: X.Y.ZZZ  (displayed as vX.Y.ZZZ)
    //   X = Major  — breaking changes, major milestones
    //   Y = Minor  — new features, significant improvements
    //   ZZZ = Patch — zero-padded 3-digit (bug fixes, tweaks)
    //
    // Examples: 0.1.000 → 0.1.001 → 0.2.000 → 1.0.000
    // Roadmap:
    //   0.1.xxx  Management Window Phase 1 (current)
    //   0.2.xxx  Widget Phase 2 (MRT One v2, Turn Display)
    //   0.3.xxx  Management Window Phase 2 (Profiles, Import/Export)
    //   0.4.xxx  Widget Phase 3 (Fuel Calculator, Standings v2)
    //   0.5.xxx  Management Window Phase 3 (Advanced Settings, Theming)
    //   0.6.xxx  Widget Phase 4 (Track Map, Pit Strategy)
    //   0.7.xxx  Management Window Phase 4 (Plugin system)
    //   1.0.000  First stable public release
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Major version number.
    /// </summary>
    public const int VERSION_MAJOR = 0;

    /// <summary>
    /// Minor version number.
    /// </summary>
    public const int VERSION_MINOR = 2;

    /// <summary>
    /// Patch version number (zero-padded to 3 digits in display).
    /// </summary>
    public const int VERSION_PATCH = 0;

    /// <summary>
    /// Current application version string (e.g., "0.1.000").
    /// Update VERSION_MAJOR/MINOR/PATCH when releasing new versions.
    /// </summary>
    public static string APP_VERSION => $"{VERSION_MAJOR}.{VERSION_MINOR}.{VERSION_PATCH:D3}";

    /// <summary>
    /// SemVer-compatible version for Velopack/NuGet (e.g., "0.1.0").
    /// </summary>
    public static string SemVer => $"{VERSION_MAJOR}.{VERSION_MINOR}.{VERSION_PATCH}";

    /// <summary>
    /// Formatted version string with 'v' prefix for display.
    /// Example: "v0.1.001"
    /// </summary>
    public static string DisplayVersion => $"v{APP_VERSION}";

    /// <summary>
    /// Full version string with "Version" prefix for status bar.
    /// Example: "Version 0.1.000"
    /// </summary>
    public static string FullVersion => $"Version {APP_VERSION}";

    /// <summary>
    /// iRacing SDK version currently in use.
    /// </summary>
    public const string IRACING_SDK_VERSION = "2024.10.01.01";
}
