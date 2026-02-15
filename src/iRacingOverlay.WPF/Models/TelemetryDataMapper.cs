using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Maps TelemetryField enum values to actual TelemetryData properties
/// </summary>
public static class TelemetryDataMapper
{
    /// <summary>
    /// Get the value for a specific telemetry field from telemetry data
    /// </summary>
    public static object? GetValue(TelemetryField field, TelemetryData? data)
    {
        if (data == null) return null;
        
        return field switch
        {
            // Speed & Motion
            TelemetryField.Speed => data.Speed,
            TelemetryField.SpeedKmh => data.Speed, // Same as Speed (already in km/h)
            TelemetryField.SpeedMph => data.Speed * 0.621371, // Convert km/h to mph
            
            // Engine
            TelemetryField.RPM => data.RPM,
            TelemetryField.Gear => data.Gear,
            TelemetryField.Throttle => data.Throttle,
            TelemetryField.Brake => data.Brake,
            TelemetryField.ABSActive => data.BrakeABSactive ? 1 : 0, // Convert bool to 0/1 for display
            TelemetryField.WheelLock => WheelLockupDetector.DetectLockup(data).AnyWheelLocked ? 1 : 0, // Hybrid lockup detection
            TelemetryField.BrakeBias => data.BrakeBias, // Percentage (e.g., 55.5 = 55.5% front)
            TelemetryField.TractionControl => data.TractionControl, // TC level (0=OFF, >0=active)
            TelemetryField.Clutch => data.Clutch,
            
            // Temperature
            TelemetryField.WaterTemp => data.WaterTemp,
            TelemetryField.OilTemp => data.OilTemp,
            TelemetryField.AirTemp => data.AirTemp,
            TelemetryField.TrackTemp => data.TrackTempCrew, // Use crew chief track temp (more accurate)
            
            // Fuel - Current State
            TelemetryField.FuelLevel => data.FuelLevel,
            TelemetryField.FuelPercent => data.FuelLevelPct, // SDK provides as 0-100 percentage already
            TelemetryField.FuelUsedLastLap => 0f, // Requires FuelCalculatorService (use 3-param overload)
            TelemetryField.FuelRemaining => data.FuelLevel, // Alias for FuelLevel
            TelemetryField.FuelLapsRemaining => 0f, // Requires FuelCalculatorService (use 3-param overload)
            TelemetryField.FuelToEnd => 0f, // Requires FuelCalculatorService (use 3-param overload)
            
            // Fuel - Averages (Phase 2) - NOTE: Requires telemetryService parameter
            TelemetryField.FuelAvgLast => 0f,         // Use GetValue(field, data, telemetryService)
            TelemetryField.FuelAvgL5 => 0f,           // Use GetValue(field, data, telemetryService)
            TelemetryField.FuelAvgL10 => 0f,          // Use GetValue(field, data, telemetryService)
            TelemetryField.FuelAvgSession => 0f,      // Use GetValue(field, data, telemetryService)
            TelemetryField.FuelMinPerLap => 0f,       // Use GetValue(field, data, telemetryService)
            TelemetryField.FuelMaxPerLap => 0f,       // Use GetValue(field, data, telemetryService)
            
            // Fuel - Strategy (Phase 2) - NOTE: Requires telemetryService parameter
            TelemetryField.FuelLapsRemainingL5 => 0f,  // Use GetValue(field, data, telemetryService)
            TelemetryField.FuelLapsRemainingL10 => 0f, // Use GetValue(field, data, telemetryService)
            TelemetryField.FuelNeededToFinish => 0f,   // Use GetValue(field, data, telemetryService)
            TelemetryField.FuelDeltaToFinish => 0f,    // Use GetValue(field, data, telemetryService)
            
            // Fuel - Safety Car Analysis (Phase 2) - NOTE: Requires telemetryService parameter
            TelemetryField.FuelGreenAvg => 0f,         // Use GetValue(field, data, telemetryService)
            TelemetryField.FuelYellowAvg => 0f,        // Use GetValue(field, data, telemetryService)
            TelemetryField.FuelGreenLapsRemain => 0f,  // Use GetValue(field, data, telemetryService)
            TelemetryField.FuelYellowLapsRemain => 0f, // Use GetValue(field, data, telemetryService)
            
            // Fuel - Saving / Lift & Coast (Phase 3) - NOTE: Requires telemetryService parameter
            TelemetryField.FuelSavingTarget => 0f,     // Use GetValue(field, data, telemetryService)
            TelemetryField.FuelProjectedDelta => 0f,   // Use GetValue(field, data, telemetryService)
            TelemetryField.FuelIsPittingFaster => 0f,   // Use GetValue(field, data, telemetryService)
            
            // Lap & Timing
            TelemetryField.LapNumber => data.Lap,
            TelemetryField.Position => data.LivePosition,         // Live overall position (race/qual mode-aware, frozen on checkered)
            TelemetryField.ClassPosition => data.LiveClassPosition, // Live class position (race/qual mode-aware, frozen on checkered)
            TelemetryField.LastLapTime => data.LapLastLapTime,
            TelemetryField.BestLapTime => data.LapBestLapTime,
            TelemetryField.CurrentLapTime => data.CurrentLapTime,
            TelemetryField.DeltaToSessionBest => data.DeltaToSessionBest,
            TelemetryField.DeltaToBestLap => data.DeltaToBestLap,
            TelemetryField.LapDistPct => data.LapDistPct,
            
            // Session
            TelemetryField.SessionTime => data.SessionTime,
            TelemetryField.SessionTimeRemaining => data.SessionTimeRemain,
            TelemetryField.SessionLaps => data.SessionLaps, // Total laps in race/session (0 for time-based)
            TelemetryField.SessionLapsRemaining => Math.Max(0, data.SessionLaps - data.Lap), // Remaining laps (0 if time-based or finished)
            TelemetryField.SessionNum => data.SessionNum,
            TelemetryField.IncidentCount => data.PlayerCarMyIncidentCount,
            
            // Tires - Temperature (Using available temps)
            TelemetryField.TireTempLF => data.LFtempCL, 
            TelemetryField.TireTempRF => data.RFtempCL,
            TelemetryField.TireTempLR => data.LRtempCL,
            TelemetryField.TireTempRR => data.RRtempCL,
            TelemetryField.TireTempAll => FormatAllTireTemps(data), // Multi-value display
            
            // Tires - Wear (Converting to percentage: 1.0 = 100% remaining)
            TelemetryField.TireWearLF => data.LFwearL * 100f,
            TelemetryField.TireWearRF => data.RFwearL * 100f,
            TelemetryField.TireWearLR => data.LRwearL * 100f,
            TelemetryField.TireWearRR => data.RRwearL * 100f,
            TelemetryField.TireWearAll => FormatAllTireWear(data), // Multi-value display
            
            // G-Forces (iRacing SDK provides these in m/s²)
            TelemetryField.CorneringG => data.LatAccel / 9.8f, // Lateral G-force (cornering left/right)
            TelemetryField.AccelBrakingG => data.LongAccel / 9.8f, // Longitudinal G-force (accel/braking)
            TelemetryField.SuspensionG => (data.VertAccel - 9.8f) / 9.8f, // Suspension load (vertical compression/extension)
            
            // Flags & Status
            TelemetryField.Flags => GetSessionFlagsString(data.SessionFlags),
            TelemetryField.InPitLane => (data.SessionFlags & 0x10000000) != 0, // Bit 28 = OnPitRoad
            TelemetryField.OnTrack => (data.SessionFlags & 0x20000000) == 0, // Bit 29 = EndOfSession (inverted for OnTrack)
            
            // Driver & Session Info
            TelemetryField.DriverName => string.IsNullOrEmpty(data.DriverName) ? "N/A" : data.DriverName,
            TelemetryField.CarNumber => string.IsNullOrEmpty(data.CarNumber) ? "N/A" : data.CarNumber,
            TelemetryField.TrackName => string.IsNullOrEmpty(data.TrackName) ? "N/A" : data.TrackName,
            
            // Turn Tracking
            TelemetryField.TurnNumber => data.TurnNumber,
            TelemetryField.TurnName => string.IsNullOrEmpty(data.TurnName) ? "-" : data.TurnName,
            TelemetryField.TurnInfo => FormatTurnInfo(data),
            
            // Steering
            TelemetryField.SteeringAngle => data.SteeringWheelAngle,
            
            // Wind
            TelemetryField.WindSpeed => data.WindVel,
            TelemetryField.WindDirection => data.WindDir, // radians, formatted by display layer
            
            // Push-to-Pass / Boost
            TelemetryField.PushToPassCount => GetPlayerP2PCount(data),
            TelemetryField.PushToPassActive => GetPlayerP2PActive(data),
            
            // None
            TelemetryField.None => null,
            
            _ => null
        };
    }
    
    /// <summary>
    /// Get the value for a specific telemetry field with access to fuel calculations
    /// </summary>
    public static object? GetValue(TelemetryField field, TelemetryData? data, ITelemetryService? telemetryService)
    {
        // For fuel calculation fields, get data from FuelCalculatorService
        if (telemetryService != null)
        {
            var fuelData = telemetryService.CurrentFuelData;
            
            // Check if this is a fuel calculation field
            var isFuelCalcField = field switch
            {
                TelemetryField.FuelUsedLastLap => true,
                TelemetryField.FuelLapsRemaining => true,
                TelemetryField.FuelToEnd => true,
                TelemetryField.FuelAvgLast => true,
                TelemetryField.FuelAvgL5 => true,
                TelemetryField.FuelAvgL10 => true,
                TelemetryField.FuelAvgSession => true,
                TelemetryField.FuelMinPerLap => true,
                TelemetryField.FuelMaxPerLap => true,
                TelemetryField.FuelLapsRemainingL5 => true,
                TelemetryField.FuelLapsRemainingL10 => true,
                TelemetryField.FuelNeededToFinish => true,
                TelemetryField.FuelDeltaToFinish => true,
                TelemetryField.FuelGreenAvg => true,
                TelemetryField.FuelYellowAvg => true,
                TelemetryField.FuelGreenLapsRemain => true,
                TelemetryField.FuelYellowLapsRemain => true,
                TelemetryField.FuelSavingTarget => true,
                TelemetryField.FuelProjectedDelta => true,
                TelemetryField.FuelIsPittingFaster => true,
                _ => false
            };
            
            // If it's a fuel calculation field, return the value from FuelData
            if (isFuelCalcField && fuelData != null)
            {
                return field switch
                {
                    // Fuel - Basic calculated fields
                    TelemetryField.FuelUsedLastLap => fuelData.FuelUsedLastLap,
                    TelemetryField.FuelLapsRemaining => fuelData.LapsRemaining,
                    TelemetryField.FuelToEnd => fuelData.FuelNeededToFinish,
                    
                    // Fuel - Averages (Phase 2)
                    TelemetryField.FuelAvgLast => fuelData.AvgFuelPerLap_Last,
                    TelemetryField.FuelAvgL5 => fuelData.AvgFuelPerLap_L5,
                    TelemetryField.FuelAvgL10 => fuelData.AvgFuelPerLap_L10,
                    TelemetryField.FuelAvgSession => fuelData.AvgFuelPerLap_Session,
                    TelemetryField.FuelMinPerLap => fuelData.MinFuelPerLap,
                    TelemetryField.FuelMaxPerLap => fuelData.MaxFuelPerLap,
                    
                    // Fuel - Strategy (Phase 2)
                    TelemetryField.FuelLapsRemainingL5 => fuelData.LapsRemaining, // Uses L5 by default
                    TelemetryField.FuelLapsRemainingL10 => fuelData.CurrentFuel / Math.Max(fuelData.AvgFuelPerLap_L10, 0.001f),
                    TelemetryField.FuelNeededToFinish => fuelData.FuelNeededToFinish,
                    TelemetryField.FuelDeltaToFinish => fuelData.FuelDeltaToFinish,
                    
                    // Fuel - Safety Car Analysis (Phase 2)
                    TelemetryField.FuelGreenAvg => fuelData.GreenFlagAverage,
                    TelemetryField.FuelYellowAvg => fuelData.YellowFlagAverage,
                    TelemetryField.FuelGreenLapsRemain => fuelData.GreenFlagAverage > 0 
                        ? fuelData.CurrentFuel / fuelData.GreenFlagAverage 
                        : 0f,
                    TelemetryField.FuelYellowLapsRemain => fuelData.YellowFlagAverage > 0 
                        ? fuelData.CurrentFuel / fuelData.YellowFlagAverage 
                        : 0f,
                    
                    // Fuel - Saving / Lift & Coast (Phase 3)
                    TelemetryField.FuelSavingTarget => fuelData.FuelSavingTarget,
                    TelemetryField.FuelProjectedDelta => fuelData.ProjectedFuelDelta,
                    TelemetryField.FuelIsPittingFaster => fuelData.IsPittingFaster ? 1f : 0f,
                    
                    _ => 0f // Should never reach here
                };
            }
        }
        
        // Fall back to standard GetValue for non-fuel fields or if service not available
        return GetValue(field, data);
    }
    
    /// <summary>
    /// Get a formatted string representation of the value
    /// </summary>
    public static string GetFormattedValue(TelemetryField field, TelemetryData? data, DisplayOptions? options = null)
    {
        var value = GetValue(field, data);
        if (value == null) return "--";
        
        options ??= new DisplayOptions();
        
        // Handle time fields specially (SessionTimeRemaining, lap times, etc.)
        if (field == TelemetryField.SessionTimeRemaining || 
            field == TelemetryField.SessionTime ||
            field == TelemetryField.LastLapTime ||
            field == TelemetryField.BestLapTime ||
            field == TelemetryField.CurrentLapTime)
        {
            double seconds = value switch
            {
                float f => f,
                double d => d,
                _ => 0.0
            };
            
            return FormatTimeSpan(seconds);
        }
        
        // Handle different value types (use InvariantCulture for consistent dot decimal separator)
        if (value is float floatValue)
        {
            var formatString = $"{{0:F{options.DecimalPlaces}}}";
            var formatted = string.Format(System.Globalization.CultureInfo.InvariantCulture, formatString, floatValue);
            return string.IsNullOrEmpty(options.Unit) ? formatted : $"{formatted} {options.Unit}";
        }
        else if (value is double doubleValue)
        {
            var formatString = $"{{0:F{options.DecimalPlaces}}}";
            var formatted = string.Format(System.Globalization.CultureInfo.InvariantCulture, formatString, doubleValue);
            return string.IsNullOrEmpty(options.Unit) ? formatted : $"{formatted} {options.Unit}";
        }
        else if (value is int intValue)
        {
            var formatted = intValue.ToString();
            return string.IsNullOrEmpty(options.Unit) ? formatted : $"{formatted} {options.Unit}";
        }
        else if (value is bool boolValue)
        {
            return boolValue.ToString();
        }
        else if (value is string stringValue)
        {
            return stringValue;
        }
        
        return value.ToString() ?? "--";
    }
    
    /// <summary>
    /// Get the numeric value for threshold comparisons
    /// </summary>
    public static double? GetNumericValue(TelemetryField field, TelemetryData? data)
    {
        var value = GetValue(field, data);
        
        return value switch
        {
            float f => f,
            double d => d,
            int i => i,
            bool b => b ? 1.0 : 0.0,
            _ => null
        };
    }
    
    /// <summary>
    /// Get default display options for a specific field
    /// </summary>
    public static DisplayOptions GetDefaultDisplayOptions(TelemetryField field)
    {
        return field switch
        {
            TelemetryField.Speed or TelemetryField.SpeedKmh => new DisplayOptions
            {
                Unit = "km/h",
                DecimalPlaces = 0,
                FontSize = 48,
                NormalColor = "#00FF00"
            },
            TelemetryField.SpeedMph => new DisplayOptions
            {
                Unit = "mph",
                DecimalPlaces = 0,
                FontSize = 48,
                NormalColor = "#00FF00"
            },
            TelemetryField.RPM => new DisplayOptions
            {
                Unit = "RPM",
                DecimalPlaces = 0,
                FontSize = 24,
                NormalColor = "#00FF00"
            },
            TelemetryField.Gear => new DisplayOptions
            {
                Unit = "",
                DecimalPlaces = 0,
                FontSize = 64,
                NormalColor = "#00FF00"
            },
            TelemetryField.Throttle or TelemetryField.Brake or TelemetryField.Clutch => new DisplayOptions
            {
                Unit = "%",
                DecimalPlaces = 0,
                FontSize = 18,
                NormalColor = "#00FF00"
            },
            TelemetryField.ABSActive => new DisplayOptions
            {
                Unit = "",
                DecimalPlaces = 0,
                FontSize = 18,
                NormalColor = "#00BCD4",  // Teal when inactive (0)
                WarningThreshold = 0.5f,  // Yellow when active (1)
                WarningColor = "#FFFF00", // Explicit yellow color
                DangerThreshold = 1.5f    // Never reaches this (bool is 0 or 1)
            },
            TelemetryField.BrakeBias => new DisplayOptions
            {
                Unit = "%",
                DecimalPlaces = 1,  // Show XX.X%
                FontSize = 18,
                NormalColor = "#00BCD4"  // Teal
            },
            TelemetryField.TractionControl => new DisplayOptions
            {
                Unit = "",
                DecimalPlaces = 1,  // Show level with 1 decimal (e.g., 5.0)
                FontSize = 18,
                NormalColor = "#00BCD4"  // Teal when active (>0)
                // Orange color applied in GetValueColor when OFF (0)
            },
            TelemetryField.WaterTemp or TelemetryField.OilTemp or 
            TelemetryField.AirTemp or TelemetryField.TrackTemp => new DisplayOptions
            {
                Unit = "°C",
                DecimalPlaces = 1,
                FontSize = 18,
                NormalColor = "#00FF00",
                WarningThreshold = 90,
                DangerThreshold = 100
            },
            TelemetryField.TireTempLF or TelemetryField.TireTempRF or 
            TelemetryField.TireTempLR or TelemetryField.TireTempRR => new DisplayOptions
            {
                Unit = "°C",
                DecimalPlaces = 1,
                FontSize = 18,
                NormalColor = "#00FF00"
            },
            TelemetryField.FuelLevel => new DisplayOptions
            {
                Unit = "L",
                DecimalPlaces = 2,
                FontSize = 24,
                NormalColor = "#00FF00",
                WarningThreshold = 10,
                DangerThreshold = 5,
                InvertThresholds = true
            },
            TelemetryField.FuelPercent => new DisplayOptions
            {
                Unit = "%",
                DecimalPlaces = 1,
                FontSize = 24,
                NormalColor = "#00FF00",
                WarningThreshold = 20,
                DangerThreshold = 10,
                InvertThresholds = true
            },
            _ => new DisplayOptions
            {
                Unit = "",
                DecimalPlaces = 1,
                FontSize = 18,
                NormalColor = "#00FF00"
            }
        };
    }

    /// <summary>
    /// Format seconds into HH:mm:ss or mm:ss.xxx format
    /// </summary>
    private static string FormatTimeSpan(double totalSeconds)
    {
        if (totalSeconds <= 0) return "--";
        
        var timeSpan = TimeSpan.FromSeconds(totalSeconds);
        
        // For lap times (under 10 minutes), show mm:ss.xxx
        if (totalSeconds < 600)
        {
            return $"{(int)timeSpan.TotalMinutes}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds:D3}";
        }
        
        // For session time, show HH:mm:ss
        if (timeSpan.TotalHours >= 1)
        {
            return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }
        
        // For times under an hour, show mm:ss
        return $"{(int)timeSpan.TotalMinutes}:{timeSpan.Seconds:D2}";
    }

    /// <summary>
    /// Format all tire temperatures in a 2x2 grid layout
    /// </summary>
    /// <returns>String formatted as "FL xx xx RF\nLR xx xx RR"</returns>
    private static string FormatAllTireTemps(TelemetryData data)
    {
        // Format: "FL 85 87 RF\nLR 82 84 RR"
        // Row 1: FL [left-front] [right-front] RF
        // Row 2: LR [left-rear] [right-rear] RR
        var lf = (int)data.LFtempCL;
        var rf = (int)data.RFtempCL;
        var lr = (int)data.LRtempCL;
        var rr = (int)data.RRtempCL;
        
        return $"FL {lf} {rf} RF\nLR {lr} {rr} RR";
    }

    /// <summary>
    /// Format all tire wear percentages in a 2x2 grid layout
    /// </summary>
    /// <returns>String formatted as "FL xx% xx% RF\nLR xx% xx% RR"</returns>
    private static string FormatAllTireWear(TelemetryData data)
    {
        // Format: "FL 95% 93% RF\nLR 91% 92% RR"
        // Row 1: FL [left-front%] [right-front%] RF
        // Row 2: LR [left-rear%] [right-rear%] RR
        var lf = (int)(data.LFwearL * 100f);
        var rf = (int)(data.RFwearL * 100f);
        var lr = (int)(data.LRwearL * 100f);
        var rr = (int)(data.RRwearL * 100f);
        
        return $"FL {lf}% {rf}% RF\nLR {lr}% {rr}% RR";
    }

    /// <summary>
    /// Convert SessionFlags bitmask to human-readable string (using short names for display)
    /// </summary>
    private static string GetSessionFlagsString(uint flags)
    {
        var flagList = new List<string>();
        
        // iRacing SessionFlags bit definitions (using shorter names to fit display)
        if ((flags & 0x00000001) != 0) flagList.Add("🏁"); // Checkered
        if ((flags & 0x00000002) != 0) flagList.Add("⚪"); // White
        if ((flags & 0x00000004) != 0) flagList.Add("🟢"); // Green
        if ((flags & 0x00000008) != 0) flagList.Add("🟡"); // Yellow
        if ((flags & 0x00000010) != 0) flagList.Add("🔴"); // Red
        if ((flags & 0x00000020) != 0) flagList.Add("🔵"); // Blue
        if ((flags & 0x00000040) != 0) flagList.Add("Debris");
        if ((flags & 0x00000080) != 0) flagList.Add("Crossed");
        if ((flags & 0x00000100) != 0) flagList.Add("Yellow⚠");
        if ((flags & 0x00000200) != 0) flagList.Add("1ToGo");
        if ((flags & 0x00000400) != 0) flagList.Add("GreenHeld");
        if ((flags & 0x00000800) != 0) flagList.Add("10ToGo");
        if ((flags & 0x00001000) != 0) flagList.Add("5ToGo");
        if ((flags & 0x00002000) != 0) flagList.Add("Wave");
        if ((flags & 0x00004000) != 0) flagList.Add("Caution");
        if ((flags & 0x00008000) != 0) flagList.Add("Caution⚠");
        if ((flags & 0x00010000) != 0) flagList.Add("⚫"); // Black flag
        if ((flags & 0x00020000) != 0) flagList.Add("DSQ");
        if ((flags & 0x00040000) != 0) flagList.Add("PitOpen");
        if ((flags & 0x00080000) != 0) flagList.Add("Furled");
        if ((flags & 0x00100000) != 0) flagList.Add("Repair");
        if ((flags & 0x01000000) != 0) flagList.Add("Start...");
        if ((flags & 0x02000000) != 0) flagList.Add("Ready");
        if ((flags & 0x04000000) != 0) flagList.Add("Set");
        if ((flags & 0x08000000) != 0) flagList.Add("GO!");
        if ((flags & 0x10000000) != 0) flagList.Add("InPits");
        if ((flags & 0x20000000) != 0) flagList.Add("SessionEnd");
        
        return flagList.Count > 0 ? string.Join(", ", flagList) : "Green";
    }
    
    /// <summary>
    /// Format turn info as compact display (e.g., "T3: Eau Rouge" or "T5" or "-")
    /// </summary>
    private static string FormatTurnInfo(TelemetryData data)
    {
        // Not in a turn
        if (data.TurnNumber == 0)
            return "-";
        
        // In a turn with name
        if (!string.IsNullOrEmpty(data.TurnName) && data.TurnName != "-")
            return $"T{data.TurnNumber}: {data.TurnName}";
        
        // In a turn without name
        return $"T{data.TurnNumber}";
    }

    /// <summary>
    /// Get player's push-to-pass remaining count from per-car array.
    /// Returns 0 if the car doesn't support P2P.
    /// </summary>
    private static int GetPlayerP2PCount(TelemetryData data)
    {
        int idx = data.PlayerCarIdx;
        if (data.CarIdxP2P_Count != null && idx >= 0 && idx < data.CarIdxP2P_Count.Length)
            return data.CarIdxP2P_Count[idx];
        return 0;
    }

    /// <summary>
    /// Get whether player's push-to-pass is currently active.
    /// </summary>
    private static bool GetPlayerP2PActive(TelemetryData data)
    {
        int idx = data.PlayerCarIdx;
        if (data.CarIdxP2P_Status != null && idx >= 0 && idx < data.CarIdxP2P_Status.Length)
            return data.CarIdxP2P_Status[idx];
        return false;
    }
}
