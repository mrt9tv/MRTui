using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using iRacingOverlay.Core.Models;
using iRacingOverlay.WPF.Models;
using Microsoft.Extensions.Logging;

namespace iRacingOverlay.WPF.Services;

/// <summary>
/// Detects session type changes from telemetry and applies per-session
/// widget configuration presets. Supports Practice, Qualifying, Race, and Warmup.
/// 
/// Thread safety: Call ApplySessionAware from UI thread only.
/// </summary>
public class SessionConfigService
{
    private readonly ILogger<SessionConfigService> _logger;
    private SessionCategory _currentCategory = SessionCategory.Unknown;
    private string _lastSessionType = string.Empty;

    /// <summary>Whether session-aware auto-configuration is enabled.</summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>Per-session-type presets.</summary>
    public Dictionary<SessionCategory, SessionPreset> Presets { get; set; } = new();

    /// <summary>Fires when the session category changes.</summary>
    public event EventHandler<SessionCategory>? SessionCategoryChanged;

    /// <summary>Current detected session category.</summary>
    public SessionCategory CurrentCategory => _currentCategory;

    public SessionConfigService(ILogger<SessionConfigService> logger)
    {
        _logger = logger;
        InitializeDefaultPresets();
    }

    /// <summary>
    /// Check the current telemetry for session type changes.
    /// Call this every update cycle (e.g. from MainWindow or a timer).
    /// </summary>
    public bool CheckSessionChange(TelemetryData data)
    {
        if (!IsEnabled) return false;

        string sessionType = data.SessionType ?? string.Empty;
        if (sessionType == _lastSessionType) return false;

        _lastSessionType = sessionType;
        var newCategory = MapSessionType(sessionType);

        if (newCategory == _currentCategory || newCategory == SessionCategory.Unknown)
            return false;

        _logger.LogInformation("Session type changed: {Old} → {New} ({SessionType})",
            _currentCategory, newCategory, sessionType);

        _currentCategory = newCategory;
        SessionCategoryChanged?.Invoke(this, newCategory);
        return true;
    }

    /// <summary>
    /// Get the preset for a given session category.
    /// Returns null if no preset configured.
    /// </summary>
    public SessionPreset? GetPreset(SessionCategory category)
    {
        return Presets.TryGetValue(category, out var preset) ? preset : null;
    }

    /// <summary>
    /// Map iRacing SessionType string to our SessionCategory enum.
    /// iRacing types: "Practice", "Open Practice", "Lone Qualifying",
    /// "Open Qualifying", "Race", "Warmup", "Heat Race", etc.
    /// </summary>
    public static SessionCategory MapSessionType(string sessionType)
    {
        if (string.IsNullOrWhiteSpace(sessionType))
            return SessionCategory.Unknown;

        var lower = sessionType.ToLowerInvariant().Trim();

        if (lower.Contains("race") || lower.Contains("heat"))
            return SessionCategory.Race;
        if (lower.Contains("qual"))
            return SessionCategory.Qualifying;
        if (lower.Contains("practice") || lower.Contains("test"))
            return SessionCategory.Practice;
        if (lower.Contains("warmup") || lower.Contains("warm"))
            return SessionCategory.Warmup;

        return SessionCategory.Unknown;
    }

    /// <summary>
    /// Initialize default presets with sensible defaults.
    /// Practice: all widgets visible, full info
    /// Qualifying: hide relative, show delta/lap times
    /// Race: show relative, hide some MRT One detail
    /// </summary>
    private void InitializeDefaultPresets()
    {
        Presets[SessionCategory.Practice] = new SessionPreset
        {
            Category = SessionCategory.Practice,
            WidgetVisibility = new()
            {
                { WidgetType.MRTOne, true },
                { WidgetType.TurnDisplay, true },
                { WidgetType.FuelCalculator, true },
                { WidgetType.Relative, true },
                { WidgetType.ProximityFeed, false },
            }
        };

        Presets[SessionCategory.Qualifying] = new SessionPreset
        {
            Category = SessionCategory.Qualifying,
            WidgetVisibility = new()
            {
                { WidgetType.MRTOne, true },
                { WidgetType.TurnDisplay, true },
                { WidgetType.FuelCalculator, false },
                { WidgetType.Relative, false },
                { WidgetType.ProximityFeed, false },
            }
        };

        Presets[SessionCategory.Race] = new SessionPreset
        {
            Category = SessionCategory.Race,
            WidgetVisibility = new()
            {
                { WidgetType.MRTOne, true },
                { WidgetType.TurnDisplay, true },
                { WidgetType.FuelCalculator, true },
                { WidgetType.Relative, true },
                { WidgetType.ProximityFeed, true },
            }
        };

        Presets[SessionCategory.Warmup] = new SessionPreset
        {
            Category = SessionCategory.Warmup,
            WidgetVisibility = new()
            {
                { WidgetType.MRTOne, true },
                { WidgetType.TurnDisplay, false },
                { WidgetType.FuelCalculator, false },
                { WidgetType.Relative, true },
                { WidgetType.ProximityFeed, false },
            }
        };
    }

    // ── Persistence ─────────────────────────────────────────────────

    private static string ConfigFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MRT-UI", "session-config.json");

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var data = new SessionConfigData
            {
                IsEnabled = IsEnabled,
                Presets = Presets.Values.Select(p => new SessionPresetData
                {
                    Category = p.Category.ToString(),
                    WidgetVisibility = p.WidgetVisibility.ToDictionary(
                        kv => kv.Key.ToString(), kv => kv.Value)
                }).ToList()
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(data, options));
            _logger.LogInformation("Session config saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save session config");
        }
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(ConfigFilePath)) return;

            var json = File.ReadAllText(ConfigFilePath);
            var data = JsonSerializer.Deserialize<SessionConfigData>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (data == null) return;

            IsEnabled = data.IsEnabled;

            foreach (var pd in data.Presets)
            {
                if (!Enum.TryParse<SessionCategory>(pd.Category, out var cat)) continue;
                if (!Presets.TryGetValue(cat, out var preset)) continue;

                foreach (var kv in pd.WidgetVisibility)
                {
                    if (Enum.TryParse<WidgetType>(kv.Key, out var wt))
                        preset.WidgetVisibility[wt] = kv.Value;
                }
            }

            _logger.LogInformation("Session config loaded (enabled={Enabled})", IsEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load session config");
        }
    }

    // JSON DTO
    private class SessionConfigData
    {
        public bool IsEnabled { get; set; }
        public List<SessionPresetData> Presets { get; set; } = new();
    }

    private class SessionPresetData
    {
        public string Category { get; set; } = string.Empty;
        public Dictionary<string, bool> WidgetVisibility { get; set; } = new();
    }
}
