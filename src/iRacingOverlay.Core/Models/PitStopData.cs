using System;

namespace iRacingOverlay.Core.Models;

/// <summary>
/// Tracks individual pit stop timing and details for race strategy analysis
/// Captures entry, service, and exit timing to calculate real pit stop duration
/// </summary>
public class PitStopData
{
    /// <summary>Lap number when pit stop occurred</summary>
    public int LapNumber { get; set; }
    
    /// <summary>Timestamp when entering pit lane (crossing pit entry line)</summary>
    public DateTime PitEntryTime { get; set; }
    
    /// <summary>Timestamp when car came to complete stop in pit box (Speed < 0.5 m/s)</summary>
    public DateTime PitBoxArrivalTime { get; set; }
    
    /// <summary>Timestamp when refueling/service started (OnPitRoad=true, stopped)</summary>
    public DateTime ServiceStartTime { get; set; }
    
    /// <summary>Timestamp when refueling/service completed (fuel stopped increasing)</summary>
    public DateTime ServiceEndTime { get; set; }
    
    /// <summary>Timestamp when car began moving again (Speed > 0.5 m/s)</summary>
    public DateTime PitBoxDepartureTime { get; set; }
    
    /// <summary>Timestamp when exiting pit lane (crossing pit exit line)</summary>
    public DateTime PitExitTime { get; set; }
    
    /// <summary>Fuel level before pit stop (liters)</summary>
    public float FuelBefore { get; set; }
    
    /// <summary>Fuel level after pit stop (liters)</summary>
    public float FuelAfter { get; set; }
    
    /// <summary>Amount of fuel added during pit stop (liters)</summary>
    public float FuelAdded => FuelAfter - FuelBefore;
    
    /// <summary>Total pit entry time (pit entry to pit box arrival) in seconds</summary>
    public float PitEntryDuration => (float)(PitBoxArrivalTime - PitEntryTime).TotalSeconds;
    
    /// <summary>Service time (refueling/tire change duration) in seconds</summary>
    public float ServiceDuration => (float)(ServiceEndTime - ServiceStartTime).TotalSeconds;
    
    /// <summary>Total pit exit time (pit box departure to pit exit) in seconds</summary>
    public float PitExitDuration => (float)(PitExitTime - PitBoxDepartureTime).TotalSeconds;
    
    /// <summary>Total pit stop time (pit entry to pit exit) in seconds</summary>
    public float TotalPitStopTime => (float)(PitExitTime - PitEntryTime).TotalSeconds;
    
    /// <summary>Active pit stop time (entry + service + exit, excludes stationary time after service)</summary>
    public float ActivePitStopTime => PitEntryDuration + ServiceDuration + PitExitDuration;
    
    /// <summary>Was this a fuel-only pit stop? (no tire change)</summary>
    public bool WasFuelOnly { get; set; } = true;
    
    /// <summary>Track name for this pit stop</summary>
    public string TrackName { get; set; } = string.Empty;
    
    /// <summary>Car class for this pit stop</summary>
    public int CarClassId { get; set; }
    
    /// <summary>Session type when pit stop occurred (Practice/Qualifying/Warmup/Race)</summary>
    public string SessionType { get; set; } = string.Empty;
    
    // Environmental conditions at time of pit stop (affects pit crew efficiency, fuel flow rate)
    
    /// <summary>Track surface temperature (°C) during pit stop</summary>
    public float TrackTemp { get; set; }
    
    /// <summary>Ambient air temperature (°C) during pit stop</summary>
    public float AirTemp { get; set; }
    
    /// <summary>Weather condition (0=Clear, 1=PartlyCloudy, 2=MostlyCloudy, 3=Overcast)</summary>
    public int WeatherType { get; set; }
    
    /// <summary>Track condition/wetness level (0=Dry, 1=MostlyDry, 2=VeryLightlyWet, etc.)</summary>
    public int TrackWetness { get; set; }
    
    /// <summary>Is pit stop data complete and valid?</summary>
    public bool IsComplete => PitExitTime > PitEntryTime && ServiceDuration > 0 && FuelAdded > 0;
}

/// <summary>
/// Session-level statistics for fuel consumption and pit stop strategy
/// Persisted between sessions for the same track/car combination
/// </summary>
public class SessionStatistics
{
    /// <summary>Track name identifier</summary>
    public string TrackName { get; set; } = string.Empty;
    
    /// <summary>Car class ID</summary>
    public int CarClassId { get; set; }
    
    /// <summary>Session average fuel consumption (liters/lap)</summary>
    public float SessionAverageFuelPerLap { get; set; }
    
    /// <summary>Number of laps used to calculate session average</summary>
    public int LapsSampled { get; set; }
    
    /// <summary>Average pit entry time (seconds) from pit lane entry to pit box</summary>
    public float AveragePitEntryTime { get; set; }
    
    /// <summary>Average refueling service time (seconds) for fuel-only stops</summary>
    public float AverageRefuelServiceTime { get; set; }
    
    /// <summary>Average pit exit time (seconds) from pit box to pit lane exit</summary>
    public float AveragePitExitTime { get; set; }
    
    /// <summary>Average total pit stop time (seconds) - entry + service + exit</summary>
    public float AverageTotalPitTime { get; set; }
    
    /// <summary>Number of pit stops recorded for this track/car</summary>
    public int PitStopsRecorded { get; set; }
    
    /// <summary>Pit lane speed limit in m/s (from YAML TrackPitSpeedLimit)</summary>
    public float PitSpeedLimit { get; set; }
    
    /// <summary>Track length in meters (from YAML TrackLength)</summary>
    public float TrackLength { get; set; }
    
    /// <summary>Last updated timestamp</summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    // Environmental condition ranges (for filtering similar conditions)
    
    /// <summary>Minimum track temperature (°C) observed during pit stops</summary>
    public float MinTrackTemp { get; set; }
    
    /// <summary>Maximum track temperature (°C) observed during pit stops</summary>
    public float MaxTrackTemp { get; set; }
    
    /// <summary>Average track temperature (°C) across all pit stops</summary>
    public float AvgTrackTemp { get; set; }
    
    /// <summary>Minimum air temperature (°C) observed during pit stops</summary>
    public float MinAirTemp { get; set; }
    
    /// <summary>Maximum air temperature (°C) observed during pit stops</summary>
    public float MaxAirTemp { get; set; }
    
    /// <summary>Average air temperature (°C) across all pit stops</summary>
    public float AvgAirTemp { get; set; }
    
    // Session type breakdown (track pit stops across Practice/Qualifying/Warmup/Race)
    
    /// <summary>Number of pit stops from Practice sessions</summary>
    public int PracticeStops { get; set; }
    
    /// <summary>Number of pit stops from Qualifying sessions</summary>
    public int QualifyingStops { get; set; }
    
    /// <summary>Number of pit stops from Warmup sessions</summary>
    public int WarmupStops { get; set; }
    
    /// <summary>Number of pit stops from Race sessions</summary>
    public int RaceStops { get; set; }

    // ===== LEARNED PIT ENTRY LOCATION (DYNAMIC DETECTION) =====

    /// <summary>Learned pit entry location as percentage of track distance (0.0-1.0)</summary>
    /// <remarks>Dynamically learned from first pit stop. Defaults to 0.85 (85%) if not yet learned.</remarks>
    public float PitEntryPct { get; set; } = 0.85f;

    /// <summary>Average fuel flow rate during refueling (liters/second)</summary>
    /// <remarks>Used for partial refuel time optimization. Default 2.5 L/s is typical for most series.</remarks>
    public float AverageFuelFlowRate { get; set; } = 2.5f;

    /// <summary>Is session statistics data sufficient for predictions?</summary>
    public bool HasSufficientData => PitStopsRecorded >= 2 && LapsSampled >= 5;

    /// <summary>
    /// Check if current environmental conditions are similar to recorded data.
    /// Returns true if track/air temps are within reasonable range.
    /// </summary>
    public bool IsSimilarConditions(float currentTrackTemp, float currentAirTemp)
    {
        if (PitStopsRecorded == 0) return false;
        
        // Allow ±10°C variance for track temp, ±15°C for air temp
        const float TRACK_TEMP_TOLERANCE = 10f;
        const float AIR_TEMP_TOLERANCE = 15f;
        
        bool trackTempSimilar = currentTrackTemp >= (MinTrackTemp - TRACK_TEMP_TOLERANCE) 
                             && currentTrackTemp <= (MaxTrackTemp + TRACK_TEMP_TOLERANCE);
        
        bool airTempSimilar = currentAirTemp >= (MinAirTemp - AIR_TEMP_TOLERANCE)
                           && currentAirTemp <= (MaxAirTemp + AIR_TEMP_TOLERANCE);
        
        return trackTempSimilar && airTempSimilar;
    }
}
