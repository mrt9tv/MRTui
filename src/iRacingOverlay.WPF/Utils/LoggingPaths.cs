using System;
using System.IO;

namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Centralized logging path management
/// Provides consistent log file locations across the application
/// </summary>
public static class LoggingPaths
{
    private static readonly string _baseDirectory;

    static LoggingPaths()
    {
        _baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MRT-UI"
        );

        // Ensure directory exists
        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
        }
    }

    /// <summary>
    /// Get the full path to a log file in the MRT-UI documents folder
    /// </summary>
    /// <param name="logFileName">Name of the log file (e.g., "fuel_debug.log")</param>
    /// <returns>Full path to the log file</returns>
    public static string GetLogPath(string logFileName)
    {
        if (string.IsNullOrWhiteSpace(logFileName))
            throw new ArgumentException("Log file name cannot be null or whitespace", nameof(logFileName));

        return Path.Combine(_baseDirectory, logFileName);
    }

    /// <summary>
    /// Base directory for all MRT-UI logs (Documents\MRT-UI)
    /// </summary>
    public static string BaseDirectory => _baseDirectory;

    /// <summary>
    /// Ensure the base logging directory exists
    /// </summary>
    public static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
        }
    }
}
