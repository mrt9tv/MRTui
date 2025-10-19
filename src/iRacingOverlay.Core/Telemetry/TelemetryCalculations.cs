using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Telemetry;

/// <summary>
/// Derived telemetry calculations and formulas
/// Complex calculations built from raw telemetry variables
/// </summary>
public static class TelemetryCalculations
{
    // ===== TIRE CALCULATIONS =====
    
    /// <summary>
    /// Calculate average tire temperature from 3-zone readings
    /// </summary>
    public static float AverageTireTemp(float tempLeft, float tempMiddle, float tempRight)
    {
        return (tempLeft + tempMiddle + tempRight) / 3f;
    }
    
    /// <summary>
    /// Calculate tire temperature imbalance (indicates camber/pressure issues)
    /// Returns absolute difference between left and right zones
    /// </summary>
    public static float TireTempImbalance(float tempLeft, float tempRight)
    {
        return MathF.Abs(tempLeft - tempRight);
    }
    
    /// <summary>
    /// Calculate average tire wear from 3-zone readings (0=new, 1=worn out)
    /// </summary>
    public static float AverageTireWear(float wearLeft, float wearMiddle, float wearRight)
    {
        return (wearLeft + wearMiddle + wearRight) / 3f;
    }
    
    /// <summary>
    /// Calculate remaining tire life as percentage (100% = new, 0% = worn out)
    /// </summary>
    public static float TireLifeRemaining(float avgWear)
    {
        return (1f - avgWear) * 100f;
    }
    
    /// <summary>
    /// Check if tire is critically worn (< 10% life remaining)
    /// </summary>
    public static bool IsTireCriticallyWorn(float avgWear)
    {
        return avgWear > 0.9f;
    }
    
    
    // ===== FUEL CALCULATIONS =====
    
    /// <summary>
    /// Calculate average fuel consumption per lap
    /// </summary>
    public static float FuelPerLap(float totalFuelUsed, int lapsCompleted)
    {
        if (lapsCompleted <= 0) return 0f;
        return totalFuelUsed / lapsCompleted;
    }
    
    /// <summary>
    /// Calculate laps remaining on current fuel
    /// </summary>
    public static float LapsRemainingOnFuel(float currentFuel, float fuelPerLap)
    {
        if (fuelPerLap <= 0) return float.MaxValue;
        return currentFuel / fuelPerLap;
    }
    
    /// <summary>
    /// Calculate fuel needed to finish race
    /// Returns 0 if enough fuel, positive if pit stop needed
    /// </summary>
    public static float FuelNeededForRace(float currentFuel, float fuelPerLap, int lapsRemaining)
    {
        float fuelNeeded = (lapsRemaining * fuelPerLap) - currentFuel;
        return MathF.Max(0f, fuelNeeded);
    }
    
    /// <summary>
    /// Check if pit stop is required for fuel
    /// </summary>
    public static bool RequiresFuelPitStop(float currentFuel, float fuelPerLap, int lapsRemaining)
    {
        return FuelNeededForRace(currentFuel, fuelPerLap, lapsRemaining) > 0f;
    }
    
    
    // ===== G-FORCE CALCULATIONS =====
    
    /// <summary>
    /// Calculate total G-force magnitude (combined lateral + longitudinal)
    /// Input: m/s², Output: G-units
    /// </summary>
    public static float TotalGForce(float latAccelMps, float longAccelMps)
    {
        float totalMps = MathF.Sqrt((latAccelMps * latAccelMps) + (longAccelMps * longAccelMps));
        return UnitConversions.MpsSquaredToGs(totalMps);
    }
    
    
    // ===== LAP TIME CALCULATIONS =====
    
    /// <summary>
    /// Format lap time as "M:SS.mmm" string
    /// </summary>
    public static string FormatLapTime(float seconds)
    {
        if (seconds <= 0) return "--:--.---";
        
        int minutes = (int)(seconds / 60);
        float remainingSeconds = seconds % 60;
        return $"{minutes}:{remainingSeconds.ToString("00.000", System.Globalization.CultureInfo.InvariantCulture)}";
    }
    
    /// <summary>
    /// Format delta time as "+/-S.mmm" string
    /// </summary>
    public static string FormatDeltaTime(float seconds)
    {
        if (seconds == 0) return "±0.000";
        
        string sign = seconds > 0 ? "+" : "";
        return $"{sign}{seconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}";
    }
    
    
    // ===== TRACK POSITION CALCULATIONS =====
    
    /// <summary>
    /// Calculate distance around track in meters from LapDistPct
    /// </summary>
    public static float TrackDistanceMeters(float lapDistPct, float trackLengthMeters)
    {
        return lapDistPct * trackLengthMeters;
    }
    
    /// <summary>
    /// Calculate distance to start/finish line
    /// </summary>
    public static float DistanceToStartFinish(float lapDistPct, float trackLengthMeters)
    {
        return (1f - lapDistPct) * trackLengthMeters;
    }
    
    /// <summary>
    /// Calculate absolute distance between two cars (handles lap wrap-around)
    /// Returns shortest distance considering track is a loop
    /// </summary>
    public static float DistanceBetweenCars(float carAPct, float carBPct, float trackLengthMeters)
    {
        float diff = MathF.Abs(carAPct - carBPct);
        
        // If more than half track, use shorter distance going other way
        if (diff > 0.5f)
            diff = 1f - diff;
        
        return diff * trackLengthMeters;
    }
    
    /// <summary>
    /// Check if car is within proximity zone (±distance in meters)
    /// </summary>
    public static bool IsCarInProximity(float playerPct, float carPct, float proximityMeters, float trackLengthMeters)
    {
        float distance = DistanceBetweenCars(playerPct, carPct, trackLengthMeters);
        return distance < proximityMeters;
    }
    
    
    // ===== BRAKE CALCULATIONS =====
    
    /// <summary>
    /// Calculate average front brake pressure (bar)
    /// </summary>
    public static float AverageFrontBrakePressure(float lfPress, float rfPress)
    {
        return (lfPress + rfPress) / 2f;
    }
    
    /// <summary>
    /// Calculate average rear brake pressure (bar)
    /// </summary>
    public static float AverageRearBrakePressure(float lrPress, float rrPress)
    {
        return (lrPress + rrPress) / 2f;
    }
    
    /// <summary>
    /// Calculate actual brake bias ratio from pressure readings
    /// Returns front bias as percentage (0.0-1.0)
    /// </summary>
    public static float ActualBrakeBiasRatio(float frontAvgPress, float rearAvgPress)
    {
        float total = frontAvgPress + rearAvgPress;
        if (total <= 0) return 0.5f;  // Default to 50/50 if no pressure
        
        return frontAvgPress / total;
    }
    
    /// <summary>
    /// Check if brake bias setting matches actual pressure distribution
    /// </summary>
    public static bool BrakeBiasMatchesSetup(float dcBrakeBias, float actualBiasRatio, float tolerance = 0.05f)
    {
        float settingRatio = dcBrakeBias / 100f;
        float difference = MathF.Abs(actualBiasRatio - settingRatio);
        return difference < tolerance;
    }
    
    /// <summary>
    /// Detect possible wheel lock (indirect method, since wheel speeds unavailable)
    /// High brake pressure + no ABS + high speed = potential wheel lock
    /// </summary>
    public static bool PossibleWheelLock(float brake, bool absActive, float speed, float brakePressure)
    {
        return brake > 0.8f &&            // Heavy braking
               !absActive &&              // ABS not intervening
               speed > 20f &&             // High speed (>72 km/h)
               brakePressure > 15f;       // High brake pressure
    }
    
    
    // ===== RPM CALCULATIONS =====
    
    /// <summary>
    /// Calculate RPM percentage to redline
    /// </summary>
    public static float RpmPercentage(float currentRpm, float blinkRpm)
    {
        if (blinkRpm <= 0) return 0f;
        return (currentRpm / blinkRpm) * 100f;
    }
    
    /// <summary>
    /// Check if RPM is in shift zone (between first light and optimal shift point)
    /// </summary>
    public static bool InShiftZone(float currentRpm, float firstRpm, float shiftRpm)
    {
        return currentRpm >= firstRpm && currentRpm < shiftRpm;
    }
    
    /// <summary>
    /// Check if should shift now (at or above optimal shift point)
    /// </summary>
    public static bool ShouldShift(float currentRpm, float shiftRpm)
    {
        return currentRpm >= shiftRpm;
    }
    
    /// <summary>
    /// Check if over-revving (at or above blink RPM)
    /// </summary>
    public static bool IsOverRev(float currentRpm, float blinkRpm)
    {
        return currentRpm >= blinkRpm;
    }
    
    
    // ===== TEMPERATURE WARNING LEVELS =====
    
    /// <summary>
    /// Get water temperature status
    /// </summary>
    public static TemperatureStatus GetWaterTempStatus(float waterTemp)
    {
        if (waterTemp > 100f) return TemperatureStatus.Critical;
        if (waterTemp > 90f) return TemperatureStatus.Warning;
        return TemperatureStatus.Normal;
    }
    
    /// <summary>
    /// Get oil temperature status
    /// </summary>
    public static TemperatureStatus GetOilTempStatus(float oilTemp)
    {
        if (oilTemp > 120f) return TemperatureStatus.Critical;
        if (oilTemp > 110f) return TemperatureStatus.Warning;
        return TemperatureStatus.Normal;
    }
    
    /// <summary>
    /// Get fuel level status
    /// </summary>
    public static FuelStatus GetFuelStatus(float fuelLevel)
    {
        if (fuelLevel < 5f) return FuelStatus.Critical;   // < 5L
        if (fuelLevel < 10f) return FuelStatus.Low;       // < 10L
        return FuelStatus.Normal;
    }
}

/// <summary>Temperature warning levels</summary>
public enum TemperatureStatus
{
    Normal,
    Warning,
    Critical
}

/// <summary>Fuel level status</summary>
public enum FuelStatus
{
    Normal,
    Low,
    Critical
}
