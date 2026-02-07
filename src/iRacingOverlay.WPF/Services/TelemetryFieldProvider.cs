using System;
using System.Collections.Generic;
using System.Linq;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Services;

/// <summary>
/// Central registry for telemetry field metadata.
/// Provides a single source of truth for field names, categories, and display info.
/// Any widget can use this to build field pickers, format values, or enumerate available data.
///
/// MODULAR DESIGN: This is the shared service layer. A single telemetry concept (e.g., RPM)
/// is defined once here and can be consumed by any widget — MRT One, standalone gauges, bar
/// widgets, or any future widget type.
/// </summary>
public static class TelemetryFieldProvider
{
    /// <summary>
    /// Metadata for a single telemetry field
    /// </summary>
    public record FieldInfo(
        TelemetryField Field,
        string DisplayName,
        string Category,
        string Description,
        bool IsAdvanced = false);

    /// <summary>
    /// All available telemetry fields with metadata, grouped by category.
    /// </summary>
    private static readonly List<FieldInfo> _fields = new()
    {
        // ── Speed & Motion ──────────────────────────────────────────────
        new(TelemetryField.Speed, "Speed", "Speed & Motion", "Vehicle speed (km/h or mph)"),
        new(TelemetryField.SpeedKmh, "Speed (km/h)", "Speed & Motion", "Speed in kilometres per hour", true),
        new(TelemetryField.SpeedMph, "Speed (mph)", "Speed & Motion", "Speed in miles per hour", true),
        new(TelemetryField.Gear, "Gear", "Speed & Motion", "Current gear (R, N, 1-7)"),
        new(TelemetryField.SteeringAngle, "Steering", "Speed & Motion", "Steering wheel angle", true),

        // ── Engine ──────────────────────────────────────────────────────
        new(TelemetryField.RPM, "RPM", "Engine", "Engine revolutions per minute"),
        new(TelemetryField.Throttle, "Throttle", "Engine", "Throttle position (0-100%)"),
        new(TelemetryField.Brake, "Brake", "Engine", "Brake pressure (0-100%)"),
        new(TelemetryField.Clutch, "Clutch", "Engine", "Clutch position (0-100%)", true),
        new(TelemetryField.BrakeBias, "Brake Bias", "Engine", "Front brake bias percentage"),
        new(TelemetryField.ABSActive, "ABS", "Engine", "ABS activity indicator"),
        new(TelemetryField.WheelLock, "Wheel Lock", "Engine", "Wheel lockup / grip loss indicator"),
        new(TelemetryField.TractionControl, "Traction Ctrl", "Engine", "Traction control level"),

        // ── Fuel ────────────────────────────────────────────────────────
        new(TelemetryField.FuelLevel, "Fuel Level", "Fuel", "Current fuel in tank (L or gal)"),
        new(TelemetryField.FuelPercent, "Fuel %", "Fuel", "Fuel tank percentage"),
        new(TelemetryField.FuelUsedLastLap, "Fuel Used Last", "Fuel", "Fuel consumed on last completed lap"),
        new(TelemetryField.FuelRemaining, "Fuel Remaining", "Fuel", "Remaining fuel in tank"),
        new(TelemetryField.FuelLapsRemaining, "Fuel Laps", "Fuel", "Estimated laps remaining on fuel"),
        new(TelemetryField.FuelToEnd, "Fuel To End", "Fuel", "Fuel needed to finish race/session"),
        new(TelemetryField.FuelAvgLast, "Fuel Avg Last", "Fuel", "Average fuel per lap (last lap)", true),
        new(TelemetryField.FuelAvgL5, "Fuel Avg L5", "Fuel", "Average fuel per lap (last 5)", true),
        new(TelemetryField.FuelAvgL10, "Fuel Avg L10", "Fuel", "Average fuel per lap (last 10)", true),
        new(TelemetryField.FuelAvgSession, "Fuel Avg Sess", "Fuel", "Average fuel per lap (session)", true),
        new(TelemetryField.FuelMinPerLap, "Fuel Min/Lap", "Fuel", "Minimum fuel per lap (best efficiency)", true),
        new(TelemetryField.FuelMaxPerLap, "Fuel Max/Lap", "Fuel", "Maximum fuel per lap (worst case)", true),
        new(TelemetryField.FuelLapsRemainingL5, "Fuel Laps L5", "Fuel", "Laps remaining based on L5 average", true),
        new(TelemetryField.FuelLapsRemainingL10, "Fuel Laps L10", "Fuel", "Laps remaining based on L10 average", true),
        new(TelemetryField.FuelNeededToFinish, "Fuel Needed", "Fuel", "Total fuel needed to finish race", true),
        new(TelemetryField.FuelDeltaToFinish, "Fuel Delta", "Fuel", "Surplus/deficit to finish race", true),
        new(TelemetryField.FuelGreenAvg, "Fuel Green Avg", "Fuel", "Average fuel per lap under green flag", true),
        new(TelemetryField.FuelYellowAvg, "Fuel Yellow Avg", "Fuel", "Average fuel per lap under yellow flag", true),
        new(TelemetryField.FuelGreenLapsRemain, "Green Laps Left", "Fuel", "Laps remaining under green flag", true),
        new(TelemetryField.FuelYellowLapsRemain, "Yellow Laps Left", "Fuel", "Laps remaining under yellow flag", true),

        // ── Temperatures ────────────────────────────────────────────────
        new(TelemetryField.WaterTemp, "Water Temp", "Temperatures", "Engine water temperature"),
        new(TelemetryField.OilTemp, "Oil Temp", "Temperatures", "Engine oil temperature"),
        new(TelemetryField.AirTemp, "Air Temp", "Temperatures", "Ambient air temperature"),
        new(TelemetryField.TrackTemp, "Track Temp", "Temperatures", "Track surface temperature"),

        // ── Lap & Timing ────────────────────────────────────────────────
        new(TelemetryField.LapNumber, "Lap", "Timing", "Current lap number"),
        new(TelemetryField.Position, "Position", "Timing", "Overall race position"),
        new(TelemetryField.ClassPosition, "Class Pos", "Timing", "Class position"),
        new(TelemetryField.LastLapTime, "Last Lap", "Timing", "Last completed lap time"),
        new(TelemetryField.BestLapTime, "Best Lap", "Timing", "Personal best lap time"),
        new(TelemetryField.CurrentLapTime, "Current Lap", "Timing", "Current lap elapsed time"),
        new(TelemetryField.DeltaToBestLap, "Delta Best", "Timing", "Delta to personal best"),
        new(TelemetryField.DeltaToSessionBest, "Delta Session", "Timing", "Delta to session best"),
        new(TelemetryField.LapDistPct, "Track Pos %", "Timing", "Track position as percentage", true),

        // ── Session ─────────────────────────────────────────────────────
        new(TelemetryField.SessionTime, "Session Time", "Session", "Current session elapsed time"),
        new(TelemetryField.SessionTimeRemaining, "Time Remain", "Session", "Session time remaining"),
        new(TelemetryField.SessionLaps, "Session Laps", "Session", "Total laps in session", true),
        new(TelemetryField.SessionLapsRemaining, "Laps Remain", "Session", "Laps remaining in session"),
        new(TelemetryField.SessionNum, "Session #", "Session", "Current session number", true),
        new(TelemetryField.IncidentCount, "Incidents", "Session", "Incident count (Nx)"),

        // ── G-Forces ────────────────────────────────────────────────────
        new(TelemetryField.CorneringG, "Lateral G", "G-Forces", "Lateral (cornering) G-force"),
        new(TelemetryField.AccelBrakingG, "Long. G", "G-Forces", "Longitudinal (accel/brake) G-force"),
        new(TelemetryField.SuspensionG, "Vertical G", "G-Forces", "Vertical (suspension) G-force", true),

        // ── Tires ───────────────────────────────────────────────────────
        new(TelemetryField.TireTempLF, "Tire Temp LF", "Tires", "Left front tire temperature"),
        new(TelemetryField.TireTempRF, "Tire Temp RF", "Tires", "Right front tire temperature"),
        new(TelemetryField.TireTempLR, "Tire Temp LR", "Tires", "Left rear tire temperature"),
        new(TelemetryField.TireTempRR, "Tire Temp RR", "Tires", "Right rear tire temperature"),
        new(TelemetryField.TireWearLF, "Tire Wear LF", "Tires", "Left front tire wear %", true),
        new(TelemetryField.TireWearRF, "Tire Wear RF", "Tires", "Right front tire wear %", true),
        new(TelemetryField.TireWearLR, "Tire Wear LR", "Tires", "Left rear tire wear %", true),
        new(TelemetryField.TireWearRR, "Tire Wear RR", "Tires", "Right rear tire wear %", true),

        // ── Flags & Status ──────────────────────────────────────────────
        new(TelemetryField.Flags, "Flags", "Status", "Session flags (green/yellow/etc)", true),
        new(TelemetryField.InPitLane, "In Pit Lane", "Status", "Whether car is in pit lane", true),
        new(TelemetryField.OnTrack, "On Track", "Status", "Whether car is on track", true),

        // ── Driver & Info ───────────────────────────────────────────────
        new(TelemetryField.DriverName, "Driver", "Info", "Driver name", true),
        new(TelemetryField.CarNumber, "Car #", "Info", "Car number", true),
        new(TelemetryField.TrackName, "Track", "Info", "Track name", true),
        
        // ── Turn Tracking ───────────────────────────────────────────────
        new(TelemetryField.TurnNumber, "Turn #", "Track", "Current turn number (1-based)"),
        new(TelemetryField.TurnName, "Turn Name", "Track", "Current turn name"),
        new(TelemetryField.TurnInfo, "Turn", "Track", "Combined turn info (T#: Name)"),
    };

    /// <summary>
    /// Get all available fields (optionally include advanced ones)
    /// </summary>
    public static IReadOnlyList<FieldInfo> GetAllFields(bool includeAdvanced = false)
    {
        return includeAdvanced
            ? _fields.AsReadOnly()
            : _fields.Where(f => !f.IsAdvanced).ToList().AsReadOnly();
    }

    /// <summary>
    /// Get fields grouped by category (for building submenus)
    /// </summary>
    public static IReadOnlyDictionary<string, List<FieldInfo>> GetFieldsByCategory(bool includeAdvanced = false)
    {
        var source = includeAdvanced ? _fields : _fields.Where(f => !f.IsAdvanced);
        return source
            .GroupBy(f => f.Category)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Get all available categories
    /// </summary>
    public static IReadOnlyList<string> GetCategories()
    {
        return _fields.Select(f => f.Category).Distinct().ToList().AsReadOnly();
    }

    /// <summary>
    /// Get metadata for a specific field
    /// </summary>
    public static FieldInfo? GetFieldInfo(TelemetryField field)
    {
        return _fields.FirstOrDefault(f => f.Field == field);
    }

    /// <summary>
    /// Get the display name for a telemetry field.
    /// Falls back to the enum name if not found in the registry.
    /// </summary>
    public static string GetDisplayName(TelemetryField field)
    {
        return GetFieldInfo(field)?.DisplayName ?? field.ToString();
    }

    /// <summary>
    /// Get a telemetry value from any widget context.
    /// This is the modular entry point — any widget calls this to read a field.
    /// </summary>
    public static object? GetValue(TelemetryField field, TelemetryData? data, ITelemetryService? service = null)
    {
        return service != null
            ? TelemetryDataMapper.GetValue(field, data, service)
            : TelemetryDataMapper.GetValue(field, data);
    }

    /// <summary>
    /// Format a telemetry value for display.
    /// Uses MRTOneDataFormatter for now; can be extended per-widget in the future.
    /// </summary>
    public static string FormatValue(TelemetryField field, object value, TelemetryData data, bool useMetricUnits)
    {
        return Widgets.MRTOneWidget.MRTOneDataFormatter.FormatValue(field, value, data, useMetricUnits);
    }
}
