using System;
using System.Globalization;
using System.Windows.Media;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Telemetry;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.MRTOneWidget;

/// <summary>
/// Pure formatting logic for MRT One widget telemetry data
/// Zero WPF dependencies (except Color struct) - fully unit testable
/// Extracted from MRTOneWidget to follow Single Responsibility Principle
/// </summary>
public static class MRTOneDataFormatter
{
    #region Value Validation

    /// <summary>
    /// Validate and sanitize telemetry values to prevent displaying nonsensical data
    /// Clamps values to reasonable ranges based on field type
    /// </summary>
    public static object ValidateValue(TelemetryField field, object value)
    {
        return field switch
        {
            // Speed: clamp to 0-600 km/h (reasonable max for racing)
            TelemetryField.Speed when value is float speed =>
                Math.Clamp(speed, 0f, 166.67f),  // 166.67 m/s = 600 km/h

            // RPM: clamp to 0-20000 (reasonable max for racing engines)
            TelemetryField.RPM when value is float rpm =>
                Math.Clamp(rpm, 0f, 20000f),

            // Temperatures: clamp to -50°C to 250°C
            TelemetryField.WaterTemp when value is float temp => Math.Clamp(temp, -50f, 250f),
            TelemetryField.OilTemp when value is float temp => Math.Clamp(temp, -50f, 250f),
            TelemetryField.AirTemp when value is float temp => Math.Clamp(temp, -50f, 100f),
            TelemetryField.TrackTemp when value is float temp => Math.Clamp(temp, -50f, 100f),

            // Fuel: clamp to 0-500 liters (reasonable tank size)
            TelemetryField.FuelLevel when value is float fuel => Math.Clamp(fuel, 0f, 500f),
            TelemetryField.FuelAvgLast when value is float fuel => Math.Clamp(fuel, 0f, 50f),
            TelemetryField.FuelAvgL5 when value is float fuel => Math.Clamp(fuel, 0f, 50f),
            TelemetryField.FuelAvgL10 when value is float fuel => Math.Clamp(fuel, 0f, 50f),
            TelemetryField.FuelAvgSession when value is float fuel => Math.Clamp(fuel, 0f, 50f),

            // Percentages: clamp to 0-1 range
            TelemetryField.Throttle when value is float throttle => Math.Clamp(throttle, 0f, 1f),
            TelemetryField.Brake when value is float brake => Math.Clamp(brake, 0f, 1f),
            TelemetryField.Clutch when value is float clutch => Math.Clamp(clutch, 0f, 1f),
            TelemetryField.FuelPercent when value is float pct => Math.Clamp(pct, 0f, 1f),

            // Brake bias: clamp to 0-100%
            TelemetryField.BrakeBias when value is float bias => Math.Clamp(bias, 0f, 100f),

            // Lap times: clamp to 0-600 seconds (10 minutes max)
            TelemetryField.LastLapTime when value is float time => Math.Clamp(time, 0f, 600f),
            TelemetryField.BestLapTime when value is float time => Math.Clamp(time, 0f, 600f),
            TelemetryField.CurrentLapTime when value is float time => Math.Clamp(time, 0f, 600f),

            // Position: clamp to 1-64 (max field size in iRacing)
            TelemetryField.Position when value is int pos => Math.Clamp(pos, 1, 64),
            TelemetryField.ClassPosition when value is int pos => Math.Clamp(pos, 1, 64),

            // Lap number: clamp to 0-9999
            TelemetryField.LapNumber when value is int lap => Math.Clamp(lap, 0, 9999),

            // Gear: clamp to -1 (reverse) to 10 (reasonable max)
            _ when field == TelemetryField.Gear && value is int gear => Math.Clamp(gear, -1, 10),

            // Default: return value unchanged (no validation needed or unknown type)
            _ => value
        };
    }

    #endregion

    #region Value Formatting

    /// <summary>
    /// Format telemetry field value for display
    /// Returns formatted string with proper units, decimals, and special cases
    /// </summary>
    public static string FormatValue(TelemetryField field, object value, TelemetryData data, bool useMetricUnits)
    {
        // Validate and sanitize value before formatting
        value = ValidateValue(field, value);

        return field switch
        {
            // Percentages (0-1 scale → 0-100%)
            TelemetryField.Throttle when value is float throttle => $"{(int)(throttle * 100)}%",
            TelemetryField.Brake when value is float brake => $"{(int)(brake * 100)}%",
            TelemetryField.ABSActive when value is int abs => "ABS",
            TelemetryField.WheelLock when value is int lockup => "WHEEL\nLOCKUP",
            TelemetryField.BrakeBias when value is float bias => $"{bias:F1}%",

            // Traction Control (car-specific scales)
            TelemetryField.TractionControl => value switch
            {
                int tc => tc < 0 ? "N/A" : tc == 0 ? "OFF" : $"{tc}",
                float tcf => tcf < 0 ? "N/A" : tcf == 0 ? "OFF" : $"{(int)tcf}",
                _ => "---"
            },

            TelemetryField.Clutch when value is float clutch => $"{(int)(clutch * 100)}%",
            TelemetryField.FuelPercent when value is float fuelPct => $"{(int)(fuelPct * 100)}%",

            // Fuel (no units - in label)
            TelemetryField.FuelLevel when value is float fuel =>
                useMetricUnits
                    ? fuel.ToString("F2", CultureInfo.InvariantCulture)
                    : (fuel * 0.264172f).ToString("F2", CultureInfo.InvariantCulture),

            // Fuel calculation fields
            TelemetryField.FuelAvgLast when value is float fuel =>
                FormatFuelValue(fuel, useMetricUnits),
            TelemetryField.FuelAvgL5 when value is float fuel =>
                FormatFuelValue(fuel, useMetricUnits),
            TelemetryField.FuelAvgL10 when value is float fuel =>
                FormatFuelValue(fuel, useMetricUnits),
            TelemetryField.FuelAvgSession when value is float fuel =>
                FormatFuelValue(fuel, useMetricUnits),
            TelemetryField.FuelMinPerLap when value is float fuel =>
                FormatFuelValue(fuel, useMetricUnits),
            TelemetryField.FuelMaxPerLap when value is float fuel =>
                FormatFuelValue(fuel, useMetricUnits),
            TelemetryField.FuelGreenAvg when value is float fuel =>
                FormatFuelValue(fuel, useMetricUnits),
            TelemetryField.FuelYellowAvg when value is float fuel =>
                FormatFuelValue(fuel, useMetricUnits),

            // Fuel laps remaining (1 decimal)
            TelemetryField.FuelLapsRemainingL5 when value is float laps =>
                laps.ToString("F1", CultureInfo.InvariantCulture),
            TelemetryField.FuelLapsRemainingL10 when value is float laps =>
                laps.ToString("F1", CultureInfo.InvariantCulture),
            TelemetryField.FuelGreenLapsRemain when value is float laps =>
                laps.ToString("F1", CultureInfo.InvariantCulture),
            TelemetryField.FuelYellowLapsRemain when value is float laps =>
                laps.ToString("F1", CultureInfo.InvariantCulture),

            // Fuel strategy (delta can be negative)
            TelemetryField.FuelNeededToFinish when value is float fuel =>
                FormatFuelValue(fuel, useMetricUnits),
            TelemetryField.FuelDeltaToFinish when value is float fuel =>
                (fuel >= 0 ? "+" : "") + FormatFuelValue(fuel, useMetricUnits),

            // Temperatures (no decimals, units in label)
            TelemetryField.WaterTemp when value is float temp =>
                FormatTemperature(temp, useMetricUnits),
            TelemetryField.OilTemp when value is float temp =>
                FormatTemperature(temp, useMetricUnits),
            TelemetryField.AirTemp when value is float temp =>
                FormatTemperature(temp, useMetricUnits),
            TelemetryField.TrackTemp when value is float temp =>
                FormatTemperature(temp, useMetricUnits),

            // Speed (no units - in label)
            TelemetryField.Speed when value is float speedMs =>
                useMetricUnits
                    ? $"{(int)UnitConversions.MpsToKmh(speedMs)}"
                    : $"{(int)UnitConversions.MpsToMph(speedMs)}",

            // RPM
            TelemetryField.RPM when value is float rpm => $"{(int)rpm}",

            // Gear (special handling for R/N)
            TelemetryField.Gear => data.Gear switch
            {
                -1 => "R",
                0 => "N",
                _ => data.Gear.ToString()
            },

            // Position (with P prefix)
            TelemetryField.Position when value is int pos => $"P{pos}",
            TelemetryField.ClassPosition when value is int pos => $"P{pos}",

            // Lap times (mm:ss.xxx format)
            TelemetryField.LastLapTime when value is float time => FormatLapTime(time),
            TelemetryField.BestLapTime when value is float time => FormatLapTime(time),
            TelemetryField.CurrentLapTime when value is float time => FormatLapTime(time),
            
            // Delta times (with +/- sign, max 3 decimals)
            TelemetryField.DeltaToBestLap when value is float delta => 
                delta >= 0 ? $"+{delta:F3}" : $"{delta:F3}",
            TelemetryField.DeltaToSessionBest when value is float delta => 
                delta >= 0 ? $"+{delta:F3}" : $"{delta:F3}",
            
            // Track position percentage
            TelemetryField.LapDistPct when value is float pct => $"{(pct * 100):F1}%",

            // Lap numbers and counts
            TelemetryField.LapNumber when value is int lap => $"{lap}",
            TelemetryField.IncidentCount when value is int count => $"{count}x",
            
            // Session time remaining (format as mm:ss or hh:mm:ss)
            TelemetryField.SessionTimeRemaining when value is double seconds => FormatSessionTime(seconds),
            TelemetryField.SessionTime when value is double seconds => FormatSessionTime(seconds),
            
            // Session laps
            TelemetryField.SessionLaps when value is int laps => $"{laps}",
            TelemetryField.SessionLapsRemaining when value is int laps => $"{laps}",
            TelemetryField.SessionNum when value is int num => $"{num}",
            
            // Fuel laps remaining (1 decimal — the basic one, not L5/L10)
            TelemetryField.FuelLapsRemaining when value is float laps =>
                laps.ToString("F1", CultureInfo.InvariantCulture),
            TelemetryField.FuelToEnd when value is float fuel =>
                FormatFuelValue(fuel, useMetricUnits),
            TelemetryField.FuelUsedLastLap when value is float fuel =>
                FormatFuelValue(fuel, useMetricUnits),
            TelemetryField.FuelRemaining when value is float fuel =>
                FormatFuelValue(fuel, useMetricUnits),
            
            // G-Forces (max 3 decimal places)
            TelemetryField.CorneringG when value is float g =>
                g.ToString("F2", CultureInfo.InvariantCulture),
            TelemetryField.AccelBrakingG when value is float g =>
                g.ToString("F2", CultureInfo.InvariantCulture),
            TelemetryField.SuspensionG when value is float g =>
                g.ToString("F2", CultureInfo.InvariantCulture),
            
            // Tire temperatures (integer, no decimals needed for display)
            TelemetryField.TireTempLF when value is float temp => $"{(int)temp}",
            TelemetryField.TireTempRF when value is float temp => $"{(int)temp}",
            TelemetryField.TireTempLR when value is float temp => $"{(int)temp}",
            TelemetryField.TireTempRR when value is float temp => $"{(int)temp}",
            
            // Tire wear (1 decimal %)
            TelemetryField.TireWearLF when value is float wear => $"{wear:F1}%",
            TelemetryField.TireWearRF when value is float wear => $"{wear:F1}%",
            TelemetryField.TireWearLR when value is float wear => $"{wear:F1}%",
            TelemetryField.TireWearRR when value is float wear => $"{wear:F1}%",
            
            // Steering angle (1 decimal, in degrees — iRacing gives radians)
            TelemetryField.SteeringAngle when value is float rad =>
                $"{(rad * 57.2958f):F1}°",
            
            // Speed variants
            TelemetryField.SpeedKmh when value is float speedMs =>
                $"{(int)UnitConversions.MpsToKmh(speedMs)}",
            TelemetryField.SpeedMph when value is float speedMs =>
                $"{(int)UnitConversions.MpsToMph(speedMs)}",
            
            // Boolean / status fields
            TelemetryField.InPitLane when value is bool inPit => inPit ? "IN PIT" : "ON TRK",
            TelemetryField.OnTrack when value is bool onTrack => onTrack ? "ON TRK" : "OFF",
            TelemetryField.Flags when value is string flags => flags,
            
            // String info fields
            TelemetryField.DriverName when value is string name => name,
            TelemetryField.CarNumber when value is string num => $"#{num}",
            TelemetryField.TrackName when value is string track => track,

            // Default fallback
            _ => value?.ToString() ?? "-"
        };
    }

    /// <summary>
    /// Format lap time from seconds to mm:ss.xxx
    /// </summary>
    public static string FormatLapTime(float seconds)
    {
        if (seconds <= 0) return "--:--.---";

        int minutes = (int)(seconds / 60);
        float remainingSeconds = seconds % 60;
        return $"{minutes}:{remainingSeconds:00.000}";
    }
    
    /// <summary>
    /// Format session time remaining from seconds to readable format
    /// Shows mm:ss for times under 1 hour, hh:mm:ss for longer sessions
    /// </summary>
    private static string FormatSessionTime(double seconds)
    {
        if (seconds <= 0) return "0:00";
        
        int hours = (int)(seconds / 3600);
        int minutes = (int)((seconds % 3600) / 60);
        int secs = (int)(seconds % 60);
        
        if (hours > 0)
            return $"{hours}:{minutes:00}:{secs:00}";
        else
            return $"{minutes}:{secs:00}";
    }

    /// <summary>
    /// Format fuel value with metric/imperial conversion
    /// </summary>
    private static string FormatFuelValue(float liters, bool useMetric)
    {
        return useMetric
            ? liters.ToString("F2", CultureInfo.InvariantCulture)
            : (liters * 0.264172f).ToString("F2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Format temperature with C/F conversion
    /// </summary>
    private static string FormatTemperature(float celsius, bool useMetric)
    {
        return useMetric
            ? $"{(int)celsius}"
            : $"{(int)UnitConversions.CelsiusToFahrenheit(celsius)}";
    }

    #endregion

    #region Field Labels

    /// <summary>
    /// Get display label for telemetry field (short, with units)
    /// Checks custom labels first, then returns default
    /// </summary>
    public static string GetLabel(TelemetryField field, bool useMetricUnits, System.Collections.Generic.Dictionary<string, string>? customLabels = null)
    {
        // Check for custom label override
        if (customLabels != null && customLabels.TryGetValue(field.ToString(), out var customLabel))
            return customLabel;

        // Default labels with units
        return field switch
        {
            TelemetryField.Speed => useMetricUnits ? "km/h" : "mph",
            TelemetryField.RPM => "RPM",
            TelemetryField.Gear => "GEAR",
            TelemetryField.Throttle => "THR",
            TelemetryField.Brake => "BRK",
            TelemetryField.Clutch => "CLU",
            TelemetryField.BrakeBias => "BIAS",

            TelemetryField.FuelLevel => useMetricUnits ? "FUEL L" : "FUEL gal",
            TelemetryField.FuelPercent => "FUEL %",
            TelemetryField.FuelUsedLastLap => useMetricUnits ? "USED L" : "USED gal",
            TelemetryField.FuelRemaining => useMetricUnits ? "REM L" : "REM gal",
            TelemetryField.FuelLapsRemaining => "LAPS",
            TelemetryField.FuelToEnd => useMetricUnits ? "END L" : "END gal",
            TelemetryField.FuelAvgLast => useMetricUnits ? "AVG L" : "AVG gal",
            TelemetryField.FuelAvgL5 => useMetricUnits ? "L5 L" : "L5 gal",
            TelemetryField.FuelAvgL10 => useMetricUnits ? "L10 L" : "L10 gal",
            TelemetryField.FuelAvgSession => useMetricUnits ? "SESS L" : "SESS gal",
            TelemetryField.FuelMinPerLap => useMetricUnits ? "MIN L" : "MIN gal",
            TelemetryField.FuelMaxPerLap => useMetricUnits ? "MAX L" : "MAX gal",
            TelemetryField.FuelLapsRemainingL5 => "LAPS",
            TelemetryField.FuelLapsRemainingL10 => "LAPS",
            TelemetryField.FuelGreenAvg => useMetricUnits ? "GRN L" : "GRN gal",
            TelemetryField.FuelYellowAvg => useMetricUnits ? "YEL L" : "YEL gal",
            TelemetryField.FuelGreenLapsRemain => "GRN LP",
            TelemetryField.FuelYellowLapsRemain => "YEL LP",
            TelemetryField.FuelNeededToFinish => useMetricUnits ? "NEED L" : "NEED gal",
            TelemetryField.FuelDeltaToFinish => useMetricUnits ? "DELTA L" : "DELTA gal",

            TelemetryField.WaterTemp => useMetricUnits ? "H2O °C" : "H2O °F",
            TelemetryField.OilTemp => useMetricUnits ? "OIL °C" : "OIL °F",
            TelemetryField.AirTemp => useMetricUnits ? "AIR °C" : "AIR °F",
            TelemetryField.TrackTemp => useMetricUnits ? "TRK °C" : "TRK °F",

            TelemetryField.Position => "POS",
            TelemetryField.ClassPosition => "CLS",
            TelemetryField.LapNumber => "LAP",
            TelemetryField.LapDistPct => "TRACK %",
            TelemetryField.IncidentCount => "INC",

            TelemetryField.LastLapTime => "LAST",
            TelemetryField.BestLapTime => "BEST",
            TelemetryField.CurrentLapTime => "CUR",
            TelemetryField.DeltaToBestLap => "Δ BEST",
            TelemetryField.DeltaToSessionBest => "Δ SES",
            
            TelemetryField.SessionTimeRemaining => "TIME",
            TelemetryField.SessionTime => "SESS",
            TelemetryField.SessionLaps => "TOTAL",
            TelemetryField.SessionLapsRemaining => "REMAIN",
            TelemetryField.SessionNum => "SESS #",

            TelemetryField.CorneringG => "LAT G",
            TelemetryField.AccelBrakingG => "LON G",
            TelemetryField.SuspensionG => "VER G",
            
            TelemetryField.TireTempLF => "LF °C",
            TelemetryField.TireTempRF => "RF °C",
            TelemetryField.TireTempLR => "LR °C",
            TelemetryField.TireTempRR => "RR °C",
            TelemetryField.TireWearLF => "LF WR",
            TelemetryField.TireWearRF => "RF WR",
            TelemetryField.TireWearLR => "LR WR",
            TelemetryField.TireWearRR => "RR WR",
            
            TelemetryField.SteeringAngle => "STEER",
            TelemetryField.SpeedKmh => "km/h",
            TelemetryField.SpeedMph => "mph",
            
            TelemetryField.InPitLane => "PIT",
            TelemetryField.OnTrack => "TRK",
            TelemetryField.Flags => "FLAG",
            TelemetryField.DriverName => "DRIVER",
            TelemetryField.CarNumber => "CAR #",
            TelemetryField.TrackName => "TRACK",

            TelemetryField.ABSActive => "",  // No label (value is "ABS")
            TelemetryField.TractionControl => "TC",
            TelemetryField.WheelLock => "",  // No label (value is "WHEEL LOCKUP")

            _ => field.ToString().ToUpper()
        };
    }

    #endregion

    #region Value Colors

    /// <summary>
    /// Get color for telemetry value based on thresholds and state
    /// Returns color appropriate for current value (e.g., red for critical temp)
    /// </summary>
    /// <param name="field">Telemetry field type</param>
    /// <param name="value">Current value</param>
    /// <param name="data">Full telemetry data (for context like gear)</param>
    /// <param name="primaryColor">Theme primary color (teal by default)</param>
    /// <param name="secondaryColor">Theme secondary color (orange by default)</param>
    /// <param name="fuelAlertSettings">Optional fuel alert settings for threshold colours</param>
    /// <param name="avgFuelPerLap">Average fuel per lap (for fuel alert calculations)</param>
    public static Color GetValueColor(
        TelemetryField field, object value, TelemetryData data,
        Color primaryColor, Color secondaryColor,
        Models.FuelAlertSettings? fuelAlertSettings = null,
        float avgFuelPerLap = 0f)
    {
        // Handle ABS (int value: 0 or 1)
        if (field == TelemetryField.ABSActive && value is int absValue)
        {
            return absValue == 1 ? Colors.Yellow : primaryColor; // Yellow when ON, theme color when OFF
        }

        // Handle Wheel Lockup (int value: 0 = no lockup, 1 = lockup detected)
        if (field == TelemetryField.WheelLock && value is int lockupValue)
        {
            return lockupValue == 1 ? Colors.Red : primaryColor; // Red when LOCKED, theme color when inactive
        }

        // Handle Traction Control (float value: -1 = N/A, 0 = OFF, >0 = active level)
        if (field == TelemetryField.TractionControl && value is float tcValue)
        {
            if (tcValue < 0) return primaryColor;      // Theme color when N/A
            return tcValue == 0 ? secondaryColor : primaryColor; // Secondary when OFF, primary when active
        }

        // Handle fuel fields with FuelAlertSettings (doable laps threshold system)
        if (fuelAlertSettings != null && IsFuelAlertField(field))
        {
            float currentFuel = data.FuelLevel;
            float avg = avgFuelPerLap > 0 ? avgFuelPerLap : 0;
            var level = fuelAlertSettings.GetAlertLevel(currentFuel, avg);
            return FuelAlertSettings.GetAlertColor(level);
        }

        if (value is not float floatValue)
            return primaryColor; // Default to theme primary

        return field switch
        {
            // Water temp: Red >100°C, Yellow >90°C
            TelemetryField.WaterTemp when floatValue > 100 => Colors.Red,
            TelemetryField.WaterTemp when floatValue > 90 => Colors.Yellow,

            // Oil temp: Red >120°C, Yellow >110°C
            TelemetryField.OilTemp when floatValue > 120 => Colors.Red,
            TelemetryField.OilTemp when floatValue > 110 => Colors.Yellow,

            // Fuel: Red <5L, Yellow <10L
            TelemetryField.FuelLevel when floatValue < 5 => Colors.Red,
            TelemetryField.FuelLevel when floatValue < 10 => Colors.Yellow,

            // Default: theme primary color
            _ => primaryColor
        };
    }

    #endregion

    #region Fuel Alert Helpers

    /// <summary>
    /// Whether a field should use the fuel alert colour system.
    /// </summary>
    private static bool IsFuelAlertField(TelemetryField field) => field switch
    {
        TelemetryField.FuelLevel => true,
        TelemetryField.FuelRemaining => true,
        TelemetryField.FuelLapsRemaining => true,
        TelemetryField.FuelLapsRemainingL5 => true,
        TelemetryField.FuelLapsRemainingL10 => true,
        TelemetryField.FuelPercent => true,
        TelemetryField.FuelToEnd => true,
        TelemetryField.FuelDeltaToFinish => true,
        _ => false
    };

    #endregion
}
