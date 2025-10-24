namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Defines all available telemetry fields that can be displayed in widgets
/// </summary>
public enum TelemetryField
{
    // Speed & Motion
    Speed,
    SpeedKmh,
    SpeedMph,
    
    // Engine
    RPM,
    Gear,
    Throttle,
    Brake,
    ABSActive,       // ABS system active indicator
    WheelLock,       // Wheel lockup detection (hybrid system)
    BrakeBias,       // Brake bias adjustment (percentage, front bias)
    TractionControl, // Traction control level (0=OFF, >0=active)
    Clutch,
    
    // Temperatures
    WaterTemp,
    OilTemp,
    AirTemp,
    TrackTemp,
    
    // Fuel - Current State
    FuelLevel,
    FuelPercent,
    FuelUsedLastLap,
    FuelRemaining,
    
    // Fuel - Averages (Phase 2)
    FuelAvgLast,          // Average fuel per lap - Last completed lap
    FuelAvgL5,            // Average fuel per lap - Last 5 laps (recommended)
    FuelAvgL10,           // Average fuel per lap - Last 10 laps
    FuelAvgSession,       // Average fuel per lap - Entire session
    FuelMinPerLap,        // Minimum fuel per lap (best efficiency)
    FuelMaxPerLap,        // Maximum fuel per lap (worst case)
    
    // Fuel - Strategy (Phase 2)
    FuelLapsRemainingL5,  // Laps remaining based on L5 average
    FuelLapsRemainingL10, // Laps remaining based on L10 average
    FuelNeededToFinish,   // Total fuel needed to finish race
    FuelDeltaToFinish,    // Fuel surplus/deficit (+ = have extra, - = need more)
    
    // Fuel - Safety Car Analysis (Phase 2)
    FuelGreenAvg,         // Average fuel per lap under green flag
    FuelYellowAvg,        // Average fuel per lap under yellow flag
    FuelGreenLapsRemain,  // Laps remaining if green flag racing
    FuelYellowLapsRemain, // Laps remaining if yellow flag continues
    
    // Lap & Timing
    LapNumber,
    Position,
    ClassPosition,
    LastLapTime,
    BestLapTime,
    CurrentLapTime,
    DeltaToSessionBest,
    DeltaToBestLap,
    
    // Session
    SessionTime,
    SessionTimeRemaining,
    SessionLaps,
    SessionLapsRemaining,
    SessionNum,
    
    // Tires - Temperature (average per tire)
    TireTempLF,
    TireTempRF,
    TireTempLR,
    TireTempRR,
    TireTempAll,         // All tire temps in 2x2 grid format
    
    // Tires - Wear
    TireWearLF,
    TireWearRF,
    TireWearLR,
    TireWearRR,
    TireWearAll,         // All tire wear in 2x2 grid format
    
    // Fuel Calculations
    FuelLapsRemaining,    // Calculated: laps remaining based on avg consumption
    FuelToEnd,            // Fuel needed to finish race/session
    
    // G-Forces
    CorneringG,          // Lateral G-force (left/right cornering)
    AccelBrakingG,       // Longitudinal G-force (acceleration/braking)
    SuspensionG,         // Suspension load (vertical compression/extension)
    
    // Flags & Status
    Flags,
    InPitLane,
    OnTrack,
    
    // Driver & Session Info
    DriverName,
    CarNumber,
    TrackName,
    
    // Steering
    SteeringAngle,
    
    // None (default)
    None
}
