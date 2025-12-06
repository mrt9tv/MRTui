namespace iRacingOverlay.Core.Models;

/// <summary>
/// Telemetry channel definitions for iRacing SDK
/// Organized by functional category for Setup Engineering and Strategy Scouting modes
/// </summary>
public static class TelemetryChannels
{
    /// <summary>
    /// Setup Engineering Mode channels (48 total, 60Hz sampling)
    /// High-frequency data for detailed car setup analysis
    /// </summary>
    public static class SetupEngineering
    {
        /// <summary>
        /// Suspension channels (16 total)
        /// Shock deflection, velocity, and ride height for chassis dynamics analysis
        /// </summary>
        public static readonly string[] Suspension = new[]
        {
            // Shock deflection (meters)
            "LFshockDefl", "RFshockDefl", "LRshockDefl", "RRshockDefl",
            
            // Shock velocity (m/s)
            "LFshockVel", "RFshockVel", "LRshockVel", "RRshockVel",
            
            // Ride height (meters)
            "LFrideHeight", "RFrideHeight", "LRrideHeight", "RRrideHeight",
            
            // Chassis attitude (radians)
            "Roll", "RollRate", "Pitch", "PitchRate"
        };
        
        /// <summary>
        /// Tire analysis channels (24 total)
        /// Wear, temperature, and pressure for tire optimization
        /// </summary>
        public static readonly string[] Tires = new[]
        {
            // Tire wear (% remaining, 3 points per tire)
            "LFwearL", "LFwearM", "LFwearR",
            "RFwearL", "RFwearM", "RFwearR",
            "LRwearL", "LRwearM", "LRwearR",
            "RRwearL", "RRwearM", "RRwearR",
            
            // Surface temps (°C, 3 points per tire)
            "LFtempL", "LFtempM", "LFtempR",
            "RFtempL", "RFtempM", "RFtempR",
            "LRtempL", "LRtempM", "LRtempR",
            "RRtempL", "RRtempM", "RRtempR",
            
            // Tire pressure (kPa)
            "LFpressure", "RFpressure", "LRpressure", "RRpressure"
        };
        
        /// <summary>
        /// Forces and dynamics channels (8 total)
        /// G-forces, velocity components, and driver inputs
        /// </summary>
        public static readonly string[] Dynamics = new[]
        {
            // G-forces (m/s²)
            "LongAccel", "LatAccel", "VertAccel",
            
            // Velocity components (m/s)
            "VelocityX", "VelocityY", "VelocityZ",
            
            // Core telemetry
            "Speed", "Throttle"
        };
        
        /// <summary>
        /// All Setup Engineering channels combined (48 total)
        /// </summary>
        public static readonly string[] All = Suspension
            .Concat(Tires)
            .Concat(Dynamics)
            .ToArray();
    }
    
    /// <summary>
    /// Strategy Scouting Mode channels (25 total, 10Hz sampling)
    /// Aggregate data for fuel strategy and tire degradation analysis
    /// </summary>
    public static class StrategyScouting
    {
        /// <summary>
        /// Fuel and pit strategy channels (11 total)
        /// Fuel consumption, pit road status, pit service state
        /// </summary>
        public static readonly string[] FuelAndPit = new[]
        {
            // Fuel state
            "FuelLevel", "FuelLevelPct", "FuelUsePerHour",
            
            // Pit detection
            "OnPitRoad",
            
            // Pit service (what's being serviced)
            "PitSvFlags", "PitSvFuel",
            
            // Tire pressure adjustments during pit
            "PitSvLFP", "PitSvRFP", "PitSvLRP", "PitSvRRP",
            
            // Pit repair times
            "PitOptRepairLeft"
        };
        
        /// <summary>
        /// Tire degradation channels (8 total)
        /// Middle wear/temp points for average degradation tracking
        /// </summary>
        public static readonly string[] TireDegradation = new[]
        {
            // Tire wear (middle point = average across tire)
            "LFwearM", "RFwearM", "LRwearM", "RRwearM",
            
            // Carcass temps (more stable than surface temps)
            "LFtempCM", "RFtempCM", "LRtempCM", "RRtempCM"
        };
        
        /// <summary>
        /// Lap timing and position channels (6 total)
        /// Timing data for stint simulation and traffic analysis
        /// </summary>
        public static readonly string[] Timing = new[]
        {
            // Lap times
            "LapCurrentLapTime", "LapLastLapTime", "LapBestLapTime",
            
            // Session state
            "SessionTimeRemain", "LapDistPct", "Lap"
        };
        
        /// <summary>
        /// Competitor intelligence channels (arrays for all cars)
        /// Used for pit strategy analysis and traffic prediction
        /// </summary>
        public static readonly string[] Competitors = new[]
        {
            "CarIdxLapDistPct",  // float[64] - Track positions
            "CarIdxOnPitRoad"    // bool[64] - Pit road status
        };
        
        /// <summary>
        /// All Strategy Scouting channels combined (25 total)
        /// </summary>
        public static readonly string[] All = FuelAndPit
            .Concat(TireDegradation)
            .Concat(Timing)
            .Concat(Competitors)
            .ToArray();
    }
    
    /// <summary>
    /// Driving Mode channels (current implementation)
    /// Essential overlay data only, no special channel list needed
    /// Uses existing telemetry service configuration
    /// </summary>
    public static class Driving
    {
        // Note: Driving mode uses existing RequiredTelemetryVars attribute
        // on IRacingTelemetryService.cs - no changes needed for Phase 2
    }
}
