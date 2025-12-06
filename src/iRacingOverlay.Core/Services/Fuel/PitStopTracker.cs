using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Tracks pit stop state transitions and timing
/// Extracted from FuelCalculatorService Phase 6 refactoring
/// </summary>
public class PitStopTracker
{
    private PitStopState _pitState = PitStopState.NotOnPitRoad;
    private PitStopData? _currentPitStop = null;
    private float _lastFuelLevel = 0f;

    /// <summary>
    /// Pit stop state machine states
    /// </summary>
    private enum PitStopState
    {
        NotOnPitRoad,
        Entering,       // Crossed pit entry line, heading to pit box
        InPitBox,       // Stopped in pit box, service starting
        Servicing,      // Refueling/tires
        Departing,      // Service complete, leaving pit box
        Exiting         // Leaving pit lane
    }

    /// <summary>
    /// Update pit stop tracking with current telemetry
    /// Returns completed pit stop data if pit stop just finished, null otherwise
    /// </summary>
    public PitStopData? Update(TelemetryData telemetry)
    {
        bool onPitRoad = telemetry.OnPitRoad;
        float speed = telemetry.Speed; // m/s
        float fuelLevel = telemetry.FuelLevel;
        PitStopData? completedPitStop = null;

        // State machine transitions
        switch (_pitState)
        {
            case PitStopState.NotOnPitRoad:
                if (onPitRoad)
                {
                    // Entered pit lane - start new pit stop
                    _currentPitStop = new PitStopData
                    {
                        PitEntryTime = DateTime.UtcNow,
                        LapNumber = telemetry.LapsCompleted + 1,
                        TrackName = telemetry.TrackName,
                        CarClassId = telemetry.PlayerCarClass,
                        SessionType = telemetry.SessionType,
                        TrackTemp = telemetry.TrackTemp,
                        AirTemp = telemetry.AirTemp,
                        WeatherType = telemetry.WeatherType,
                        TrackWetness = 0, // Track wetness not exposed by iRacing SDK (WeatherType provides dry/wet)
                        FuelBefore = fuelLevel
                    };
                    _pitState = PitStopState.Entering;
                    LogDebug($"PIT STOP: Entry detected (Lap {_currentPitStop.LapNumber}, Session={_currentPitStop.SessionType}, Track={_currentPitStop.TrackTemp:F1}°C, Air={_currentPitStop.AirTemp:F1}°C)");
                }
                break;

            case PitStopState.Entering:
                if (speed < 0.5f) // Stopped in pit box (< 0.5 m/s = ~1 mph)
                {
                    if (_currentPitStop != null)
                    {
                        _currentPitStop.PitBoxArrivalTime = DateTime.UtcNow;
                        _currentPitStop.ServiceStartTime = DateTime.UtcNow;
                        _pitState = PitStopState.InPitBox;
                        LogDebug($"PIT STOP: Arrived in pit box (Entry duration: {_currentPitStop.PitEntryDuration:F1}s)");
                    }
                }
                else if (!onPitRoad)
                {
                    // Aborted pit entry - reset
                    LogDebug("PIT STOP: Entry aborted (left pit road before stopping)");
                    _currentPitStop = null;
                    _pitState = PitStopState.NotOnPitRoad;
                }
                break;

            case PitStopState.InPitBox:
                // Wait for fuel to stop increasing (service complete)
                if (_currentPitStop != null && fuelLevel > _currentPitStop.FuelBefore + 0.1f)
                {
                    // Fuel is increasing - service in progress
                    _pitState = PitStopState.Servicing;
                }
                else if (speed > 0.5f)
                {
                    // Started moving without refuel - likely damage repair or quick stop
                    if (_currentPitStop != null)
                    {
                        _currentPitStop.ServiceEndTime = DateTime.UtcNow;
                        _currentPitStop.PitBoxDepartureTime = DateTime.UtcNow;
                        _pitState = PitStopState.Departing;
                        LogDebug("PIT STOP: Departing pit box (no refuel detected)");
                    }
                }
                break;

            case PitStopState.Servicing:
                if (_currentPitStop != null)
                {
                    float fuelDelta = fuelLevel - _currentPitStop.FuelBefore;

                    // Detect service end: Fuel stopped increasing (delta < 0.05L over last update)
                    if (Math.Abs(fuelLevel - _lastFuelLevel) < 0.05f && fuelDelta > 0.5f)
                    {
                        _currentPitStop.ServiceEndTime = DateTime.UtcNow;
                        LogDebug($"PIT STOP: Service complete (Duration: {_currentPitStop.ServiceDuration:F1}s, Fuel added: {fuelDelta:F1}L)");
                    }

                    // Detect departure: Car started moving again
                    if (speed > 0.5f && _currentPitStop.ServiceEndTime > _currentPitStop.ServiceStartTime)
                    {
                        _currentPitStop.PitBoxDepartureTime = DateTime.UtcNow;
                        _pitState = PitStopState.Departing;
                        LogDebug($"PIT STOP: Departing pit box");
                    }
                }
                break;

            case PitStopState.Departing:
                if (!onPitRoad)
                {
                    // Exited pit lane - complete pit stop
                    if (_currentPitStop != null)
                    {
                        _currentPitStop.PitExitTime = DateTime.UtcNow;
                        _currentPitStop.FuelAfter = fuelLevel;

                        if (_currentPitStop.IsComplete)
                        {
                            LogDebug($"PIT STOP COMPLETE: Total={_currentPitStop.TotalPitStopTime:F1}s (Entry={_currentPitStop.PitEntryDuration:F1}s, Service={_currentPitStop.ServiceDuration:F1}s, Exit={_currentPitStop.PitExitDuration:F1}s, Active={_currentPitStop.ActivePitStopTime:F1}s)");
                            LogDebug($"  Fuel: Before={_currentPitStop.FuelBefore:F1}L, After={_currentPitStop.FuelAfter:F1}L, Added={_currentPitStop.FuelAdded:F1}L");

                            completedPitStop = _currentPitStop;
                        }
                        else
                        {
                            LogDebug("PIT STOP: Incomplete data (missing timestamps or fuel data)");
                        }
                    }

                    _currentPitStop = null;
                    _pitState = PitStopState.NotOnPitRoad;
                }
                break;
        }

        _lastFuelLevel = fuelLevel;
        return completedPitStop;
    }

    /// <summary>
    /// Get current pit stop state
    /// </summary>
    public bool IsOnPitRoad => _pitState != PitStopState.NotOnPitRoad;

    /// <summary>
    /// Get current pit stop data (if in progress)
    /// </summary>
    public PitStopData? CurrentPitStop => _currentPitStop;

    /// <summary>
    /// Reset pit stop tracking
    /// </summary>
    public void Reset()
    {
        _pitState = PitStopState.NotOnPitRoad;
        _currentPitStop = null;
        _lastFuelLevel = 0f;
    }

    private void LogDebug(string message)
    {
        try
        {
            // Note: Cannot use LoggingPaths utility here as it's in WPF project, Core project should not reference WPF
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MRT-UI",
                "fuel_debug.log"
            );

            var directory = System.IO.Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
            System.IO.File.AppendAllText(logPath, logMessage);
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
