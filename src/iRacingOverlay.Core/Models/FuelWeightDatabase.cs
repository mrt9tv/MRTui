namespace iRacingOverlay.Core.Models;

/// <summary>
/// Car-class-specific fuel weight penalty database
/// Based on real-world physics and iRacing telemetry analysis
/// Fuel weight affects lap times: more fuel = heavier car = slower laps
/// </summary>
public static class FuelWeightDatabase
{
    /// <summary>
    /// Default fuel weight penalty for unknown cars (conservative GT3-level)
    /// </summary>
    public const float DEFAULT_PENALTY = 0.03f;  // seconds per lap per liter

    /// <summary>
    /// Get fuel weight penalty for a specific car class
    /// </summary>
    /// <param name="carClassId">iRacing car class ID</param>
    /// <returns>Fuel weight penalty in seconds per lap per liter of fuel</returns>
    /// <remarks>
    /// Penalty represents how much slower a car goes per lap with 1 additional liter of fuel.
    /// Formula cars: Higher penalty (~0.06s/lap/L) due to low weight and high power-to-weight ratio
    /// GT3/GTE: Moderate penalty (~0.03s/lap/L) - balanced weight distribution
    /// NASCAR: Lower penalty (~0.015s/lap/L) due to high weight and low power-to-weight ratio
    /// </remarks>
    public static float GetFuelWeightPenalty(int carClassId)
    {
        // Known car-class-specific penalties (seconds per lap per liter)
        // Based on community testing and real-world motorsport data

        var penaltyMap = new Dictionary<int, float>
        {
            // ===== FORMULA CARS (High penalty - lightweight, high power-to-weight) =====
            { 1, 0.06f },   // Formula cars (generic)
            { 32, 0.06f },  // Dallara iR-01 (~680kg total weight, every kg counts)
            { 90, 0.055f }, // Formula Renault 2.0
            { 91, 0.05f },  // Skip Barber RT2000 (heavier formula car)
            { 126, 0.06f }, // Dallara F3

            // ===== GT3 / GTE CARS (Moderate penalty - mid-weight, balanced) =====
            { 81, 0.03f },  // BMW M4 GT3 (~1300kg)
            { 108, 0.03f }, // Mercedes AMG GT3
            { 110, 0.03f }, // Audi R8 LMS GT3
            { 111, 0.03f }, // Ferrari 488 GT3
            { 112, 0.03f }, // Lamborghini Huracan GT3
            { 113, 0.03f }, // McLaren MP4-12C GT3
            { 114, 0.03f }, // Porsche 911 GT3 R
            { 160, 0.03f }, // Ferrari 296 GT3

            // ===== PROTOTYPE CARS (Moderate-High penalty - lightweight but downforce) =====
            { 93, 0.04f },  // HPD ARX-01c (~950kg)
            { 94, 0.04f },  // Cadillac CTS-VR
            { 95, 0.04f },  // Riley MkXX Daytona Prototype
            { 135, 0.045f }, // Dallara P217 LMP2 (~930kg, very light)
            { 136, 0.045f }, // Ligier JS P217 LMP2

            // ===== NASCAR / STOCK CARS (Low penalty - heavy cars, low power-to-weight) =====
            { 3, 0.015f },  // NASCAR Cup Series (~1540kg)
            { 4, 0.015f },  // NASCAR Xfinity Series
            { 5, 0.02f },   // NASCAR Trucks (slightly lighter)
            { 123, 0.015f }, // NextGen NASCAR

            // ===== TOURING CARS (Moderate penalty - similar to GT3) =====
            { 70, 0.035f }, // TCR (~1200kg)
            { 71, 0.03f },  // Porsche 911 GT3 Cup
            { 89, 0.03f },  // BMW M4 GT4

            // ===== SPORTS CARS (Moderate-Low penalty - heavier than GT3) =====
            { 25, 0.025f }, // Mazda MX-5 Cup (~1100kg but low power)
            { 26, 0.03f },  // Spec Racer Ford
            { 40, 0.03f },  // Porsche 718 Cayman GT4 Clubsport

            // ===== DIRT CARS (Low-Moderate penalty - high weight, variable conditions) =====
            { 51, 0.02f },  // Sprint Car (~650kg but dirt traction limits)
            { 52, 0.018f }, // Late Model (~1180kg)
            { 53, 0.02f },  // Modified
            { 54, 0.022f }, // Street Stock

            // ===== RALLY CROSS (Moderate penalty - AWD, heavy) =====
            { 140, 0.028f }, // VW Beetle GRC
            { 141, 0.028f }, // Subaru WRX STI GRC
        };

        // Try to get car-specific penalty
        if (penaltyMap.TryGetValue(carClassId, out float penalty))
        {
            return penalty;
        }

        // Fallback: Estimate based on car category (by ID ranges)
        return carClassId switch
        {
            >= 1 and <= 32 => 0.055f,     // Formula cars
            >= 51 and <= 54 => 0.02f,      // Dirt cars
            >= 70 and <= 89 => 0.03f,      // Touring/GT4
            >= 90 and <= 95 => 0.04f,      // Prototype
            >= 108 and <= 114 => 0.03f,    // GT3
            >= 123 and <= 125 => 0.015f,   // NASCAR
            >= 135 and <= 136 => 0.045f,   // LMP2
            >= 140 and <= 141 => 0.028f,   // Rally Cross
            160 => 0.03f,                   // GT3 (Ferrari 296)
            _ => DEFAULT_PENALTY            // Unknown - use conservative GT3 value
        };
    }

    /// <summary>
    /// Calculate total lap time penalty for given fuel load
    /// </summary>
    /// <param name="carClassId">iRacing car class ID</param>
    /// <param name="fuelLiters">Fuel load in liters</param>
    /// <returns>Total lap time penalty in seconds</returns>
    public static float CalculateLapTimePenalty(int carClassId, float fuelLiters)
    {
        float penaltyPerLiter = GetFuelWeightPenalty(carClassId);
        return fuelLiters * penaltyPerLiter;
    }

    /// <summary>
    /// Calculate stint time advantage of lighter fuel load
    /// </summary>
    /// <param name="carClassId">iRacing car class ID</param>
    /// <param name="currentFuel">Current fuel in liters</param>
    /// <param name="targetFuel">Target fuel after pit in liters</param>
    /// <param name="stintLaps">Number of laps in stint</param>
    /// <returns>Total time advantage in seconds over the stint</returns>
    public static float CalculateStintAdvantage(int carClassId, float currentFuel, float targetFuel, int stintLaps)
    {
        float penaltyPerLiter = GetFuelWeightPenalty(carClassId);

        // Average fuel penalty: starts at targetFuel, decreases to 0 linearly
        float avgFuelDifference = (currentFuel + targetFuel) / 2f;
        float avgPenaltyPerLap = avgFuelDifference * penaltyPerLiter;

        return avgPenaltyPerLap * stintLaps;
    }

    /// <summary>
    /// Get car category name for display purposes
    /// </summary>
    public static string GetCarCategoryDescription(int carClassId)
    {
        return carClassId switch
        {
            >= 1 and <= 32 => "Formula (High fuel sensitivity)",
            >= 51 and <= 54 => "Dirt (Low fuel sensitivity)",
            >= 70 and <= 89 => "Touring/GT4 (Moderate fuel sensitivity)",
            >= 90 and <= 95 => "Prototype (Moderate-High fuel sensitivity)",
            >= 108 and <= 114 => "GT3 (Moderate fuel sensitivity)",
            >= 123 and <= 125 => "NASCAR (Low fuel sensitivity)",
            >= 135 and <= 136 => "LMP2 (High fuel sensitivity)",
            >= 140 and <= 141 => "Rally Cross (Moderate fuel sensitivity)",
            160 => "GT3 (Moderate fuel sensitivity)",
            _ => "Unknown (Using GT3 baseline)"
        };
    }
}
