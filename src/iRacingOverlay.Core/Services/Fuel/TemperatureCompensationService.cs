using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services.History;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

namespace iRacingOverlay.Core.Services.Fuel
{
    /// <summary>
    /// Handles temperature-based fuel consumption correction for more accurate predictions
    /// Scientific basis: Hotter air = less dense = less power = richer mixture = more fuel
    /// Rule of thumb: +10°C = +2-3% fuel consumption
    /// 
    /// Optional Fine-Tuning: Extracted from FuelCalculatorService to isolate temperature correction logic
    /// </summary>
    public class TemperatureCompensationService
    {
        private readonly ILogger<TemperatureCompensationService> _logger;

        public TemperatureCompensationService(ILogger<TemperatureCompensationService>? logger = null)
        {
            _logger = logger ?? NullLogger<TemperatureCompensationService>.Instance;
        }

        /// <summary>
        /// Calculate temperature correction factor and apply to fuel averages
        /// </summary>
        /// <param name="telemetry">Current telemetry data</param>
        /// <param name="sessionStats">Historical session statistics (may be null)</param>
        /// <param name="currentData">Current fuel data to update with corrections</param>
        public void ApplyTemperatureCorrection(
            TelemetryData telemetry,
            SessionStatistics? sessionStats,
            FuelData currentData)
        {
            // Skip if no historical data available
            if (sessionStats == null || !sessionStats.HasSufficientData)
            {
                currentData.TemperatureCorrectionFactor = 1.0f;
                currentData.TemperatureCorrectionReason = "";
                return;
            }

            float currentAirTemp = telemetry.AirTemp;
            float historicalAirTemp = sessionStats.AvgAirTemp;
            float tempDelta = currentAirTemp - historicalAirTemp;

            // Apply correction: +10°C = +2.5% fuel consumption
            // Formula: 1.0 + (tempDelta * 0.0025)
            // Example: +12°C → 1.0 + (12 * 0.0025) = 1.03 (3% more fuel)
            float correctionFactor = 1.0f + (tempDelta * 0.0025f);

            // Limit correction to ±10% to avoid extreme values from sensor errors
            correctionFactor = Math.Clamp(correctionFactor, 0.9f, 1.1f);

            currentData.TemperatureCorrectionFactor = correctionFactor;

            if (Math.Abs(tempDelta) > 5f)
            {
                float correctionPct = (correctionFactor - 1.0f) * 100f;
                currentData.TemperatureCorrectionReason =
                    $"Air temp {tempDelta:+0.0;-0.0}°C vs historical avg ({correctionPct:+0.0;-0.0}% fuel)";
                
                _logger.LogDebug(
                    "TEMP CORRECTION: {CurrentTemp:F1}°C vs {HistoricalTemp:F1}°C → {Factor:F3}x factor ({Pct:+0.0;-0.0}%)",
                    currentAirTemp, historicalAirTemp, correctionFactor, correctionPct);

                // FIX #3 (CORRECTED): Apply temperature correction to stored averages ONCE per calculation cycle
                // This is called AFTER CalculateAverages() populates the values, so we modify them once
                // On next cycle, CalculateAverages() will recalculate from raw lap data, then this applies correction again
                // This prevents compounding because we always start from fresh raw averages each cycle
                if (currentData.AvgFuelPerLap_Last > 0)
                    currentData.AvgFuelPerLap_Last *= correctionFactor;
                if (currentData.AvgFuelPerLap_L5 > 0)
                    currentData.AvgFuelPerLap_L5 *= correctionFactor;
                if (currentData.AvgFuelPerLap_L10 > 0)
                    currentData.AvgFuelPerLap_L10 *= correctionFactor;
                if (currentData.AvgFuelPerLap_Session > 0)
                    currentData.AvgFuelPerLap_Session *= correctionFactor;
            }
            else
            {
                currentData.TemperatureCorrectionReason = "";
            }
        }
    }
}
