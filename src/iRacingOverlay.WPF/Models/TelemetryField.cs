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
    
    // Fuel - Saving / Lift & Coast (Phase 3)
    FuelSavingTarget,     // L/lap reduction needed to finish without stopping
    FuelProjectedDelta,   // Projected fuel surplus/deficit at finish (liters)
    FuelIsPittingFaster,  // Whether pitting is faster than fuel saving
    
    // Lap & Timing
    LapNumber,
    Position,
    ClassPosition,
    LastLapTime,
    BestLapTime,
    CurrentLapTime,
    DeltaToSessionBest,
    DeltaToBestLap,
    LapDistPct,          // Track position as percentage (0.0-1.0)
    
    // Session
    SessionTime,
    SessionTimeRemaining,
    SessionLaps,
    SessionLapsRemaining,
    SessionNum,
    IncidentCount,       // Player's incident count (for safety rating)
    
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
    
    // Turn Tracking
    TurnNumber,          // Current turn number (1-based), or 0 if not in a turn
    TurnName,            // Current turn name (e.g., "Eau Rouge"), or empty if not in a turn
    TurnInfo,            // Combined turn display (e.g., "T3: Casino Square" or "T5")
    
    // Steering
    SteeringAngle,
    
    // Wind
    WindSpeed,           // Wind speed (m/s or km/h based on unit setting)
    WindDirection,       // Wind direction as compass arrow (↑ N, → E, etc.)
    
    // Push-to-Pass / Boost
    PushToPassCount,     // Remaining P2P activations (0 if N/A)
    PushToPassActive,    // Whether P2P is currently active (bool → ON/OFF)
    
    // Relative / Gap
    RelativeGapAhead,    // Time gap to nearest car ahead (seconds)
    RelativeGapBehind,   // Time gap to nearest car behind (seconds)
    RelativeDistAhead,   // Distance to nearest car ahead (meters)
    RelativeDistBehind,  // Distance to nearest car behind (meters)

    // ── Telemetry capability audit: newly available data ──────────────
    // Exposed as selectable fields so every existing widget picker gets them
    // without any new UI.

    // Weather
    TrackWetness,        // Dry → Flooded, from the sim's 8-step wetness scale
    Precipitation,       // Live rain rate
    TimeOfDay,           // In-sim clock

    // Force feedback
    FfbTorquePct,        // Wheel torque as a share of maximum (clipping at 100%)
    FfbMaxForce,         // Configured wheel maximum force (Nm)

    // Sim performance and network
    SimFrameRate,        // The SIMULATOR's frame rate, not the overlay's
    ConnectionQuality,   // Network quality (0-100%)
    ConnectionLatency,   // Network latency (ms)

    // Tire strategy
    TireSetsLeft,        // Tire sets remaining
    TireCompound,        // Compound currently fitted
    ColdPressureLF,      // Cold pressures — hot pressures are disk-logging-only
    ColdPressureRF,
    ColdPressureLR,
    ColdPressureRR,

    // Incidents and penalties
    TeamIncidents,       // Shared team incident count (endurance)
    WeightPenalty,       // Success ballast carried

    // Timing
    DeltaToOptimal,      // Delta to the theoretical best from your own best sectors

    // Engine
    BatteryVoltage,      // Battery voltage
    ShiftIndicator,      // Shift indicator fill (deprecated by iRacing — display only)
    OptimalShiftRPM,     // The car's optimal shift RPM
    Redline,             // Engine redline

    // SDK 2.x — hybrid / DRS cars only, zero elsewhere
    DrsStatus,           // DRS state as reported by the car
    DrsCount,            // DRS activations remaining
    ErsBattery,          // ERS battery charge %

    // None (default)
    None
}
