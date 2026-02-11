using System;
using System.Collections.Generic;

namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Named widget configuration profile.
/// Captures an entire snapshot of per-widget settings (visibility + data toggles)
/// and can be bound to a session type, car class, or both.
/// 
/// Matching priority (most → least specific):
///   1. Session + CarClass match
///   2. Session match (any car)
///   3. CarClass match (any session)
///   4. Default (no binding)
/// </summary>
public class WidgetProfile
{
    /// <summary>Unique identifier for this profile.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable name, e.g. "GT3 Race", "Formula Qualifying".</summary>
    public string Name { get; set; } = "Default";

    /// <summary>
    /// Session category binding. Null = all sessions.
    /// </summary>
    public SessionCategory? SessionBinding { get; set; }

    /// <summary>
    /// Car class name binding (e.g. "GT3", "LMP2", "Formula Vee").
    /// Null = all car classes.
    /// Matched against TelemetryData.CarScreenName or iRacing car class string.
    /// </summary>
    public string? CarClassBinding { get; set; }

    /// <summary>
    /// Per-widget visibility (same as SessionPreset.WidgetVisibility).
    /// </summary>
    public Dictionary<WidgetType, bool> WidgetVisibility { get; set; } = new();

    /// <summary>
    /// Per-widget settings snapshot.
    /// Key = WidgetType, Value = dictionary of setting key → value.
    /// Stores ALL toggleable settings for each widget (columns, appearance, etc.).
    /// </summary>
    public Dictionary<WidgetType, Dictionary<string, object>> WidgetSettings { get; set; } = new();

    /// <summary>When this profile was last modified.</summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this is a built-in default profile that cannot be deleted.
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Compute match score against current session + car.
    /// Higher = better match.
    /// </summary>
    public int MatchScore(SessionCategory? session, string? carClass)
    {
        int score = 0;
        bool sessionMatch = SessionBinding == null || SessionBinding == session;
        bool carMatch = string.IsNullOrEmpty(CarClassBinding) ||
                        string.Equals(CarClassBinding, carClass, StringComparison.OrdinalIgnoreCase);

        if (!sessionMatch || !carMatch) return -1; // No match

        if (SessionBinding != null && SessionBinding == session) score += 2;
        if (!string.IsNullOrEmpty(CarClassBinding) &&
            string.Equals(CarClassBinding, carClass, StringComparison.OrdinalIgnoreCase)) score += 2;

        // Unbound dimensions: partial credit (allows catch-all profiles)
        if (SessionBinding == null) score += 0;
        if (string.IsNullOrEmpty(CarClassBinding)) score += 0;

        return score;
    }
}
