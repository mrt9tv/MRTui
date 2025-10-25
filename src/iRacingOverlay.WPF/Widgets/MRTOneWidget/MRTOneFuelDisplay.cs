using System;
using System.Text;
using System.Windows.Media;
using iRacingOverlay.Core.Models;

namespace iRacingOverlay.WPF.Widgets.MRTOneWidget;

/// <summary>
/// Generates comprehensive fuel display text for MRT One widget
/// Pure logic with no WPF dependencies - fully unit testable
/// Extracted from MRTOneWidget to isolate complex fuel formatting
/// </summary>
public static class MRTOneFuelDisplay
{
    /// <summary>
    /// Generate comprehensive fuel display with averages, laps remaining, and optional pit strategy
    /// Returns formatted text and foreground color
    /// </summary>
    /// <param name="fuelData">Fuel calculation data from service</param>
    /// <param name="showStrategy">Whether to show pit strategy section</param>
    /// <returns>FuelDisplayResult with text, color, and visibility</returns>
    public static FuelDisplayResult GenerateFuelDisplay(FuelData? fuelData, bool showStrategy)
    {
        // Early exit if no data
        if (fuelData == null || fuelData.CurrentFuel <= 0)
        {
            if (fuelData?.CurrentFuel > 0 && !fuelData.HasSufficientData)
            {
                // Show minimal info if we have fuel but not enough data for averages
                string minimalText = $"FUEL: {fuelData.CurrentFuel:F2}L / {fuelData.TankCapacity:F1}L\n(Need more laps for calculations)";
                return new FuelDisplayResult(minimalText, Color.FromArgb(150, 255, 255, 255), true);
            }

            return new FuelDisplayResult("", Colors.Transparent, false);
        }

        // Only show if we have sufficient data
        if (!fuelData.HasSufficientData)
        {
            return new FuelDisplayResult("", Colors.Transparent, false);
        }

        // Build comprehensive fuel display
        var fuelText = new StringBuilder();

        // Line 1: Current fuel and tank capacity
        fuelText.AppendLine($"FUEL: {fuelData.CurrentFuel:F2}L / {fuelData.TankCapacity:F1}L ({fuelData.FuelPct:F0}%)");

        // Line 2: Averages (Last, L5 with trend, L10 with trend, Session)
        // Calculate trend indicators for L5 and L10 (+ if increasing, - if decreasing)
        string l5Trend = "";
        string l10Trend = "";
        
        // Compare current with last lap to show trend
        if (fuelData.LapToLapDelta != 0)
        {
            // L5 trend: If last lap fuel is higher than L5 average, it means L5 is decreasing (good!)
            // If last lap fuel is lower than L5 average, it means L5 is increasing (bad!)
            float l5Delta = fuelData.AvgFuelPerLap_Last - fuelData.AvgFuelPerLap_L5;
            if (Math.Abs(l5Delta) > 0.01f) // Only show if meaningful difference
            {
                l5Trend = l5Delta > 0 ? " +" : " -";
            }
            
            // L10 trend: Same logic as L5
            float l10Delta = fuelData.AvgFuelPerLap_Last - fuelData.AvgFuelPerLap_L10;
            if (Math.Abs(l10Delta) > 0.01f) // Only show if meaningful difference
            {
                l10Trend = l10Delta > 0 ? " +" : " -";
            }
        }
        
        fuelText.AppendLine($"AVG: L:{fuelData.AvgFuelPerLap_Last:F2} | 5:{fuelData.AvgFuelPerLap_L5:F2}{l5Trend} | 10:{fuelData.AvgFuelPerLap_L10:F2}{l10Trend} | S:{fuelData.AvgFuelPerLap_Session:F2}");

        // Line 3: Min/Max and laps remaining with iRacing delta
        string lapsDeltaStr = fuelData.LapsDifference >= 0
            ? $"+{fuelData.LapsDifference:F1}"
            : $"{fuelData.LapsDifference:F1}";
        fuelText.AppendLine($"RANGE: {fuelData.MinFuelPerLap:F2}-{fuelData.MaxFuelPerLap:F2}L | LAPS: {fuelData.LapsRemaining:F1} (iR: {fuelData.IRacingLapsRemaining:F1}, Δ {lapsDeltaStr})");

        // Line 4: Delta to finish and fuel needed (race mode vs practice/qual mode)
        if (fuelData.RaceLapsRemaining > 0)
        {
            // RACE MODE: Show fuel needed with buffer
            string deltaStr = fuelData.FuelDeltaToFinish >= 0
                ? $"+{fuelData.FuelDeltaToFinish:F2}L"
                : $"{fuelData.FuelDeltaToFinish:F2}L";

            // Calculate fuel to add (minimum needed without buffer)
            float fuelNeededNoBuffer = (fuelData.RaceLapsRemaining * fuelData.AvgFuelPerLap_L5) + fuelData.FuelSputteringThreshold;
            float fuelToAddNoBuffer = Math.Max(0, fuelNeededNoBuffer - fuelData.CurrentFuel);

            string needStr = fuelToAddNoBuffer > 0.1f  // >0.1L threshold
                ? $"{fuelToAddNoBuffer:F2}L"
                : "OK";

            fuelText.AppendLine($"TO FINISH: {fuelData.FuelNeededToFinish:F2}L ({deltaStr}) | NEED {needStr}");
        }
        else
        {
            // QUALIFYING/PRACTICE MODE: 5-lap reference
            float fivelapFuel = fuelData.AvgFuelPerLap_L5 * 5;
            float deltaToFiveLaps = fuelData.CurrentFuel - fivelapFuel;
            string deltaStr = deltaToFiveLaps >= 0
                ? $"+{deltaToFiveLaps:F2}L"
                : $"{deltaToFiveLaps:F2}L";
            fuelText.AppendLine($"5-LAP REF: {fivelapFuel:F2}L ({deltaStr})");
        }

        // Line 5: Green/Yellow flag averages (if available)
        if (fuelData.GreenFlagLapCount > 0 || fuelData.YellowFlagLapCount > 0)
        {
            string flagInfo = "";
            if (fuelData.GreenFlagLapCount > 0)
                flagInfo += $"GREEN: {fuelData.GreenFlagAverage:F2}L ({fuelData.GreenFlagLapCount})";
            if (fuelData.YellowFlagLapCount > 0)
                flagInfo += (flagInfo.Length > 0 ? " | " : "") + $"YELLOW: {fuelData.YellowFlagAverage:F2}L ({fuelData.YellowFlagLapCount})";
            fuelText.AppendLine(flagInfo);
        }

        // Optional: Pit strategy section (toggleable)
        if (showStrategy && fuelData.RaceLapsRemaining > 0 && fuelData.AvgFuelPerLap_L5 > 0)
        {
            string strategySection = GeneratePitStrategy(fuelData);
            if (!string.IsNullOrEmpty(strategySection))
            {
                fuelText.AppendLine(""); // Blank line separator
                fuelText.Append(strategySection);
            }
        }

        // Determine color based on laps remaining
        Color fuelColor = fuelData.LapsRemaining >= 2
            ? Color.FromRgb(255, 128, 0)   // Orange - SOON (≥2 laps)
            : Colors.Red;                   // Red - URGENT (<2 laps)

        Color foregroundColor = Color.FromArgb(200, fuelColor.R, fuelColor.G, fuelColor.B);

        return new FuelDisplayResult(fuelText.ToString().TrimEnd(), foregroundColor, true);
    }

    /// <summary>
    /// Generate pit strategy section (1-stop, 2-stop scenarios, safe window)
    /// Only shown when fuel strategy toggle is enabled AND in race mode
    /// </summary>
    private static string GeneratePitStrategy(FuelData fuelData)
    {
        var strategy = new StringBuilder();
        strategy.AppendLine("═══ PIT STRATEGY ═══");

        float avgFuel = fuelData.AvgFuelPerLap_L5;
        float tankCap = fuelData.TankCapacity;
        int totalLaps = fuelData.RaceLapsRemaining;
        float currentFuel = fuelData.CurrentFuel;

        // NO-STOP Strategy (if possible)
        if (fuelData.CanFinishWithoutStop)
        {
            strategy.AppendLine($"✓ NO-STOP: Current fuel sufficient ({fuelData.FuelDeltaToFinish:+0.0;-0.0}L surplus)");
        }

        // 1-STOP Strategy
        float lapsOnCurrentFuel = currentFuel / avgFuel;
        float lapsOnFullTank = tankCap / avgFuel;

        if (lapsOnCurrentFuel + lapsOnFullTank >= totalLaps)
        {
            // Can finish with 1 stop
            int optimalPitLap = (int)Math.Floor(lapsOnCurrentFuel);
            int lapsAfterPit = totalLaps - optimalPitLap;
            float fuelToAdd = lapsAfterPit * avgFuel;
            strategy.AppendLine($"1-STOP: Pit @ L{optimalPitLap} → Add {fuelToAdd:F1}L");
        }
        else
        {
            float shortfall = totalLaps * avgFuel - currentFuel - tankCap;
            strategy.AppendLine($"1-STOP: NOT POSSIBLE (need {shortfall:F1}L more capacity)");
        }

        // 2-STOP Strategy
        if (lapsOnFullTank * 2 >= totalLaps)
        {
            // Calculate optimal 2-stop windows
            float lapsPerStint = totalLaps / 3.0f; // Divide race into 3 stints
            int firstPit = (int)Math.Min(lapsOnCurrentFuel, lapsPerStint);
            int secondPit = firstPit + (int)lapsOnFullTank;
            float firstStopFuel = Math.Min(tankCap, lapsPerStint * avgFuel);
            float secondStopFuel = Math.Min(tankCap, (totalLaps - secondPit) * avgFuel);
            strategy.AppendLine($"2-STOP: L{firstPit} ({firstStopFuel:F1}L), L{secondPit} ({secondStopFuel:F1}L)");
        }
        else
        {
            strategy.AppendLine($"2-STOP: NOT POSSIBLE (tank too small for race distance)");
        }

        // Pit Window (show as range if available, otherwise show conservative pit lap)
        if (fuelData.PitWindowStart > 0 && fuelData.PitWindowEnd > 0 && fuelData.PitWindowStart <= fuelData.PitWindowEnd)
        {
            // Show window as range (e.g., "PIT WINDOW 10-15")
            strategy.AppendLine($"⚠ PIT WINDOW: L{fuelData.PitWindowStart}-{fuelData.PitWindowEnd}");
        }
        else
        {
            // Fallback: Calculate conservative pit lap if window not available
            int conservativePitLap = (int)Math.Floor(lapsOnCurrentFuel * 0.9f);
            strategy.AppendLine($"⚠ SAFE WINDOW: Pit by L{conservativePitLap} (90% fuel buffer)");
        }

        return strategy.ToString();
    }
}

/// <summary>
/// Result of fuel display generation
/// </summary>
/// <param name="Text">Formatted multi-line fuel display text</param>
/// <param name="ForegroundColor">Color for text (orange/red based on urgency)</param>
/// <param name="IsVisible">Whether display should be shown</param>
public record FuelDisplayResult(string Text, Color ForegroundColor, bool IsVisible);
