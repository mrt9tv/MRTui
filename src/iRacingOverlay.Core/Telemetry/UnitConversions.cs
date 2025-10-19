namespace iRacingOverlay.Core.Telemetry;

/// <summary>
/// Unit conversion utilities for telemetry values
/// Handles metric/imperial conversions and derived calculations
/// </summary>
public static class UnitConversions
{
    // ===== SPEED CONVERSIONS =====
    
    /// <summary>Convert meters per second to kilometers per hour</summary>
    public static float MpsToKmh(float mps) => mps * 3.6f;
    
    /// <summary>Convert meters per second to miles per hour</summary>
    public static float MpsToMph(float mps) => mps * 2.23694f;
    
    /// <summary>Convert kilometers per hour to meters per second</summary>
    public static float KmhToMps(float kmh) => kmh / 3.6f;
    
    /// <summary>Convert miles per hour to meters per second</summary>
    public static float MphToMps(float mph) => mph / 2.23694f;
    
    
    // ===== TEMPERATURE CONVERSIONS =====
    
    /// <summary>Convert Celsius to Fahrenheit</summary>
    public static float CelsiusToFahrenheit(float celsius) => (celsius * 9f / 5f) + 32f;
    
    /// <summary>Convert Fahrenheit to Celsius</summary>
    public static float FahrenheitToCelsius(float fahrenheit) => (fahrenheit - 32f) * 5f / 9f;
    
    
    // ===== VOLUME CONVERSIONS =====
    
    /// <summary>Convert liters to US gallons</summary>
    public static float LitersToGallons(float liters) => liters * 0.264172f;
    
    /// <summary>Convert US gallons to liters</summary>
    public static float GallonsToLiters(float gallons) => gallons / 0.264172f;
    
    
    // ===== ANGULAR CONVERSIONS =====
    
    /// <summary>Convert radians to degrees</summary>
    public static float RadiansToDegrees(float radians) => radians * (180f / MathF.PI);
    
    /// <summary>Convert degrees to radians</summary>
    public static float DegreesToRadians(float degrees) => degrees * (MathF.PI / 180f);
    
    
    // ===== ACCELERATION CONVERSIONS =====
    
    /// <summary>Convert m/s² to G-units (1G = 9.81 m/s²)</summary>
    public static float MpsSquaredToGs(float mpsSquared) => mpsSquared / 9.81f;
    
    /// <summary>Convert G-units to m/s²</summary>
    public static float GsToMpsSquared(float gs) => gs * 9.81f;
    
    
    // ===== PRESSURE CONVERSIONS =====
    
    /// <summary>Convert bar to PSI</summary>
    public static float BarToPsi(float bar) => bar * 14.5038f;
    
    /// <summary>Convert PSI to bar</summary>
    public static float PsiToBar(float psi) => psi / 14.5038f;
    
    /// <summary>Convert kPa to PSI</summary>
    public static float KpaToPsi(float kpa) => kpa * 0.145038f;
    
    /// <summary>Convert PSI to kPa</summary>
    public static float PsiToKpa(float psi) => psi / 0.145038f;
    
    
    // ===== DISTANCE CONVERSIONS =====
    
    /// <summary>Convert meters to feet</summary>
    public static float MetersToFeet(float meters) => meters * 3.28084f;
    
    /// <summary>Convert feet to meters</summary>
    public static float FeetToMeters(float feet) => feet / 3.28084f;
    
    /// <summary>Convert meters to miles</summary>
    public static float MetersToMiles(float meters) => meters * 0.000621371f;
    
    /// <summary>Convert miles to meters</summary>
    public static float MilesToMeters(float miles) => miles / 0.000621371f;
    
    
    // ===== FUEL CONVERSIONS =====
    
    /// <summary>
    /// Convert fuel consumption from kg/hr to L/hr
    /// Assumes gasoline density of 0.75 kg/L (typical racing fuel)
    /// </summary>
    public static float FuelKgPerHourToLitersPerHour(float kgPerHour, float fuelDensity = 0.75f)
    {
        return kgPerHour / fuelDensity;
    }
    
    /// <summary>
    /// Convert fuel consumption from L/hr to gallons/hr
    /// </summary>
    public static float FuelLitersPerHourToGallonsPerHour(float litersPerHour)
    {
        return LitersToGallons(litersPerHour);
    }
}
