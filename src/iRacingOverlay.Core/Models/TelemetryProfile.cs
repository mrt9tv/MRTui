namespace iRacingOverlay.Core.Models;

/// <summary>
/// Telemetry collection profile for mode-specific data recording
/// Each mode has different telemetry needs:
/// - Setup Engineering: High-frequency (60 Hz), full data, long-term storage
/// - Strategy Scouting: Low-frequency (10 Hz), aggregates only, session storage
/// - Driving: High-frequency (60 Hz), essential fields, current session only
/// </summary>
public class TelemetryProfile
{
    /// <summary>
    /// Sample rate in Hz (updates per second)
    /// Setup/Driving: 60 Hz (responsive)
    /// Strategy: 10 Hz (efficient)
    /// </summary>
    public int SampleRate { get; set; } = 60;
    
    /// <summary>
    /// Which telemetry fields to record
    /// ALL: All available telemetry data
    /// Essential: Only fields needed for real-time overlays
    /// Strategy: Fuel, tires, lap times, position
    /// </summary>
    public TelemetryFields RecordFields { get; set; } = TelemetryFields.Essential;
    
    /// <summary>
    /// How telemetry data should be stored
    /// FullHistory: Keep all laps, full 60Hz data (Setup Engineering)
    /// AggregatesOnly: Just lap summaries (Strategy Scouting)
    /// CurrentSession: Discard after session ends (Driving)
    /// </summary>
    public StorageMode StorageMode { get; set; } = StorageMode.CurrentSession;
    
    /// <summary>
    /// Whether to enable lap comparison features (Setup Engineering)
    /// </summary>
    public bool EnableComparison { get; set; } = false;
    
    /// <summary>
    /// Whether to enable multi-stint projection (Strategy Scouting)
    /// </summary>
    public bool EnableProjection { get; set; } = false;
    
    /// <summary>
    /// Whether to enable real-time overlays (Driving)
    /// </summary>
    public bool EnableOverlays { get; set; } = false;
    
    /// <summary>
    /// Compression level for stored telemetry (0 = none, 1-9 = Zlib compression)
    /// Setup Engineering uses medium compression to balance storage/performance
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.None;
}

/// <summary>
/// Telemetry field selection
/// </summary>
public enum TelemetryFields
{
    /// <summary>
    /// All available telemetry data (Setup Engineering)
    /// Includes: Speed, throttle, brake, steering, suspension, tire data, aero, etc.
    /// </summary>
    ALL,
    
    /// <summary>
    /// Essential overlay data only (Driving)
    /// Includes: Fuel, position, lap times, flags, radar data
    /// </summary>
    Essential,
    
    /// <summary>
    /// Strategy-relevant data (Strategy Scouting)
    /// Includes: Fuel consumption, tire temps/wear, lap times, pit stops
    /// </summary>
    Strategy
}

/// <summary>
/// Storage mode for telemetry data
/// </summary>
public enum StorageMode
{
    /// <summary>
    /// Full history with high-frequency data (Setup Engineering)
    /// ~2 MB/minute, persistent across sessions
    /// </summary>
    FullHistory,
    
    /// <summary>
    /// Lap aggregates only (Strategy Scouting)
    /// ~200 KB/minute, persistent across sessions
    /// </summary>
    AggregatesOnly,
    
    /// <summary>
    /// Current session only, discard after (Driving)
    /// In-memory only, no disk persistence
    /// </summary>
    CurrentSession
}

/// <summary>
/// Compression level for telemetry storage
/// </summary>
public enum CompressionLevel
{
    /// <summary>
    /// No compression (Driving mode - in-memory only)
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Fast compression (minimal CPU overhead)
    /// </summary>
    Low = 3,
    
    /// <summary>
    /// Balanced compression (Setup Engineering default)
    /// ~60% size reduction, <5ms compression time per lap
    /// </summary>
    Medium = 6,
    
    /// <summary>
    /// Maximum compression (archival storage)
    /// ~80% size reduction, slower but rarely used
    /// </summary>
    High = 9
}
