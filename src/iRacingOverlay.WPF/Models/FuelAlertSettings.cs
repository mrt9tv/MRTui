using System.Text.Json.Serialization;
using System.Windows.Media;

namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Configurable fuel alert thresholds for the overlay.
/// Uses "doable laps remaining" — accounting for the sputtering threshold
/// so the driver knows how many laps they can actually complete,
/// not just a raw fuel / consumption number.
///
/// Example: 0.2L in tank with 2.5L/lap avg and 0.3L sputtering threshold
/// → doable laps = (0.2 - 0.3) / 2.5 = negative → effectively 0 doable laps.
/// The car will sputter and die before completing the lap.
/// </summary>
public class FuelAlertSettings
{
    // ── Enable/Disable individual alerts ───────────────────────────────

    /// <summary>Whether the yellow (caution) alert is enabled.</summary>
    [JsonPropertyName("enableYellowAlert")]
    public bool EnableYellowAlert { get; set; } = true;

    /// <summary>Whether the red (urgent) alert is enabled.</summary>
    [JsonPropertyName("enableRedAlert")]
    public bool EnableRedAlert { get; set; } = true;

    /// <summary>Whether the critical (blinking) alert is enabled.</summary>
    [JsonPropertyName("enableCriticalAlert")]
    public bool EnableCriticalAlert { get; set; } = true;

    /// <summary>Whether the sputtering threshold is applied to doable-lap calculations.</summary>
    [JsonPropertyName("enableSputterAlert")]
    public bool EnableSputterAlert { get; set; } = true;

    // ── Thresholds (in doable laps remaining) ─────────────────────────

    /// <summary>
    /// Below this many laps → yellow warning (caution, plan pit stop).
    /// Default: 5 laps remaining.
    /// </summary>
    [JsonPropertyName("yellowThresholdLaps")]
    public float YellowThresholdLaps { get; set; } = 5.0f;

    /// <summary>
    /// Below this many laps → red warning (urgent, pit immediately).
    /// Default: 2 laps remaining.
    /// </summary>
    [JsonPropertyName("redThresholdLaps")]
    public float RedThresholdLaps { get; set; } = 2.0f;

    /// <summary>
    /// Below this many laps → blinking red (critical, will run out).
    /// Default: 1 lap remaining. At this point the driver likely cannot
    /// make it back to pit lane on the next lap.
    /// </summary>
    [JsonPropertyName("criticalThresholdLaps")]
    public float CriticalThresholdLaps { get; set; } = 1.0f;

    // ── Sputtering threshold ────────────────────────────────────────

    /// <summary>
    /// Fuel level (in litres) below which the engine begins sputtering.
    /// Varies by car class — GT3 ~0.3L, Formula ~0.2L, Heavy cars ~0.5L.
    /// This is subtracted from CurrentFuel before dividing by avg consumption
    /// to get "doable laps" instead of theoretical laps.
    /// </summary>
    [JsonPropertyName("sputteringThresholdL")]
    public float SputteringThresholdL { get; set; } = 0.3f;

    // ── Alert state ─────────────────────────────────────────────────

    /// <summary>
    /// Calculate the current alert level for a given fuel state.
    /// </summary>
    /// <param name="currentFuel">Current fuel in tank (litres)</param>
    /// <param name="avgFuelPerLap">Average fuel consumption per lap (litres).
    /// Should use the best available average (L5, session, etc.).</param>
    /// <returns>The current alert level</returns>
    public FuelAlertLevel GetAlertLevel(float currentFuel, float avgFuelPerLap)
    {
        float doableLaps = CalculateDoableLaps(currentFuel, avgFuelPerLap);

        if (EnableCriticalAlert && doableLaps <= CriticalThresholdLaps)
            return FuelAlertLevel.Critical; // blinking red

        if (EnableRedAlert && doableLaps <= RedThresholdLaps)
            return FuelAlertLevel.Danger; // solid red

        if (EnableYellowAlert && doableLaps <= YellowThresholdLaps)
            return FuelAlertLevel.Warning; // yellow

        return FuelAlertLevel.Normal; // green / teal
    }

    /// <summary>
    /// Calculate doable laps = laps the car can realistically complete
    /// before sputtering. Always >= 0.
    /// When sputtering is disabled, uses raw fuel (theoretical laps).
    /// </summary>
    public float CalculateDoableLaps(float currentFuel, float avgFuelPerLap)
    {
        if (avgFuelPerLap <= 0.001f) return 0f;

        float sputterDeduction = EnableSputterAlert ? SputteringThresholdL : 0f;
        float usableFuel = currentFuel - sputterDeduction;
        if (usableFuel <= 0f) return 0f;

        return usableFuel / avgFuelPerLap;
    }

    /// <summary>
    /// Get the display colour for a given alert level.
    /// </summary>
    public static Color GetAlertColor(FuelAlertLevel level) => level switch
    {
        FuelAlertLevel.Critical => Colors.Red,
        FuelAlertLevel.Danger => Colors.Red,
        FuelAlertLevel.Warning => Colors.Yellow,
        _ => Color.FromRgb(0, 188, 212) // teal
    };

    /// <summary>
    /// Whether the current level should blink (only Critical).
    /// </summary>
    public static bool ShouldBlink(FuelAlertLevel level) =>
        level == FuelAlertLevel.Critical;

    /// <summary>
    /// Create default settings.
    /// </summary>
    public static FuelAlertSettings Default => new();
}

/// <summary>
/// Fuel alert severity levels.
/// </summary>
public enum FuelAlertLevel
{
    /// <summary>Fuel is fine — normal colour</summary>
    Normal,
    /// <summary>Getting low — yellow (plan your pit stop)</summary>
    Warning,
    /// <summary>Very low — solid red (pit immediately)</summary>
    Danger,
    /// <summary>About to run out — blinking red (≤1 doable lap)</summary>
    Critical
}
