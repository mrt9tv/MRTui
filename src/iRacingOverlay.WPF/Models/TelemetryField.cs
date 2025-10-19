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
    
    // Fuel
    FuelLevel,
    FuelPercent,
    FuelUsedLastLap,
    FuelRemaining,
    
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
