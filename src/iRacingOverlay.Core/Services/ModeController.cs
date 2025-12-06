using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Mode controller with state machine for three operational modes
/// Manages transitions between Setup Engineering, Strategy Scouting, and Driving modes
/// Each mode has distinct telemetry collection profiles and feature sets
/// </summary>
public class ModeController
{
    private OperationalMode _currentMode = OperationalMode.Driving;  // Default to Driving mode
    private readonly object _modeLock = new object();
    
    /// <summary>
    /// Current operational mode
    /// </summary>
    public OperationalMode CurrentMode
    {
        get
        {
            lock (_modeLock)
            {
                return _currentMode;
            }
        }
    }
    
    /// <summary>
    /// Event fired when mode changes
    /// </summary>
    public event EventHandler<ModeChangedEventArgs>? ModeChanged;
    
    /// <summary>
    /// Switch to a new operational mode with validation
    /// </summary>
    /// <param name="newMode">Target mode</param>
    /// <exception cref="InvalidOperationException">If transition is not allowed</exception>
    public void SwitchMode(OperationalMode newMode)
    {
        lock (_modeLock)
        {
            if (_currentMode == newMode)
            {
                // Already in requested mode, no-op
                return;
            }
            
            // Validate transition
            if (!CanTransitionTo(newMode))
            {
                throw new InvalidOperationException(
                    $"Cannot switch from {_currentMode} to {newMode}. " +
                    GetTransitionBlockReason(_currentMode, newMode));
            }
            
            var previousMode = _currentMode;
            
            // Save current mode state before switching
            SaveModeState(_currentMode);
            
            // Update mode
            _currentMode = newMode;
            
            // Load new mode state
            LoadModeState(newMode);
            
            // Fire mode changed event
            ModeChanged?.Invoke(this, new ModeChangedEventArgs(previousMode, newMode));
        }
    }
    
    /// <summary>
    /// Check if transition to target mode is allowed
    /// </summary>
    public bool CanTransitionTo(OperationalMode targetMode)
    {
        lock (_modeLock)
        {
            // Most transitions are allowed
            // Blocked transitions will be added as needed (e.g., during active setup comparison)
            return true;  // For now, all transitions allowed
        }
    }
    
    /// <summary>
    /// Get telemetry collection profile for the current mode
    /// </summary>
    public TelemetryProfile GetCollectionProfile()
    {
        return GetCollectionProfile(_currentMode);
    }
    
    /// <summary>
    /// Get telemetry collection profile for a specific mode
    /// </summary>
    public TelemetryProfile GetCollectionProfile(OperationalMode mode)
    {
        return mode switch
        {
            OperationalMode.SetupEngineering => new TelemetryProfile
            {
                SampleRate = 60,    // Hz - high frequency for detailed analysis
                RecordFields = TelemetryFields.ALL,  // Capture everything
                StorageMode = StorageMode.FullHistory,  // Keep all laps
                EnableComparison = true,
                EnableProjection = false,
                EnableOverlays = false,
                CompressionLevel = CompressionLevel.Medium,  // Balance storage/performance
                RequiredChannels = TelemetryChannels.SetupEngineering.All  // 48 channels
            },
            
            OperationalMode.StrategyScouting => new TelemetryProfile
            {
                SampleRate = 10,    // Hz - lower frequency, just aggregates
                RecordFields = TelemetryFields.Strategy,  // Fuel, tires, lap times
                StorageMode = StorageMode.AggregatesOnly,
                EnableComparison = false,
                EnableProjection = true,
                EnableOverlays = false,
                CompressionLevel = CompressionLevel.None,  // Aggregates are small
                RequiredChannels = TelemetryChannels.StrategyScouting.All  // 25 channels
            },
            
            OperationalMode.Driving => new TelemetryProfile
            {
                SampleRate = 60,    // Hz - real-time responsiveness
                RecordFields = TelemetryFields.Essential,  // Just overlay data
                StorageMode = StorageMode.CurrentSession,  // Discard after session
                EnableComparison = false,
                EnableProjection = false,
                EnableOverlays = true,
                CompressionLevel = CompressionLevel.None,  // In-memory only
                RequiredChannels = null  // Use existing IRacingTelemetryService channels
            },
            
            _ => throw new ArgumentException($"Unknown mode: {mode}")
        };
    }
    
    /// <summary>
    /// Get human-readable reason why transition is blocked (if blocked)
    /// </summary>
    public string GetTransitionBlockReason(OperationalMode from, OperationalMode to)
    {
        // Future: Add specific blocking reasons
        // Example: "Cannot switch during active setup comparison - finish current test first"
        return "Transition not allowed at this time.";
    }
    
    /// <summary>
    /// Save current mode state before switching
    /// Future: Persist mode-specific data to SQLite
    /// </summary>
    private void SaveModeState(OperationalMode mode)
    {
        // TODO: Implement state persistence
        // - Save setup session data (Setup Engineering)
        // - Save strategy profile (Strategy Scouting)
        // - Save current session data (Driving)
    }
    
    /// <summary>
    /// Load mode state after switching
    /// Future: Load mode-specific data from SQLite
    /// </summary>
    private void LoadModeState(OperationalMode mode)
    {
        // TODO: Implement state loading
        // - Load setup session history (Setup Engineering)
        // - Load strategy profiles (Strategy Scouting)
        // - Load active session (Driving)
    }
}
