namespace iRacingOverlay.Core.Models;

/// <summary>
/// Operational modes for MRT Overlay
/// Each mode serves a distinct phase of the race preparation → execution workflow
/// </summary>
public enum OperationalMode
{
    /// <summary>
    /// Setup Engineering Mode: Optimize car setup through data-driven testing
    /// Use Case: Practice sessions, test days
    /// Features: Lap comparison, setup change tracking, ML recommendations
    /// </summary>
    SetupEngineering = 1,
    
    /// <summary>
    /// Strategy Scouting Mode: Gather intelligence for race planning
    /// Use Case: Practice/qualifying before race, pre-race preparation
    /// Features: Fuel profiling, tire degradation tracking, multi-stint simulation
    /// </summary>
    StrategyScouting = 2,
    
    /// <summary>
    /// Driving Mode: Real-time overlay assistance during racing
    /// Use Case: Qualifying, race
    /// Features: Real-time fuel calculations, radar, delta tracking, pit strategy
    /// </summary>
    Driving = 3
}
