using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using iRacingOverlay.WPF.Models;
using Microsoft.Extensions.Logging;

namespace iRacingOverlay.WPF.Services;

/// <summary>
/// Manages named widget configuration profiles.
/// Profiles capture per-widget visibility + per-widget data settings,
/// optionally bound to session type and/or car class.
/// 
/// Storage: ~/Documents/MRT-UI/profiles.json
/// </summary>
public class ProfileStorageService
{
    private readonly ILogger<ProfileStorageService> _logger;

    /// <summary>All stored profiles.</summary>
    public List<WidgetProfile> Profiles { get; set; } = new();

    /// <summary>The ID of the currently active profile (null = none).</summary>
    public Guid? ActiveProfileId { get; set; }

    /// <summary>Whether profile auto-switching is enabled.</summary>
    public bool AutoSwitch { get; set; } = false;

    /// <summary>Fires when the active profile changes.</summary>
    public event EventHandler<WidgetProfile?>? ActiveProfileChanged;

    public ProfileStorageService(ILogger<ProfileStorageService> logger)
    {
        _logger = logger;
        Load();
        if (Profiles.Count == 0)
            InitializeDefaults();
    }

    // ── Profile CRUD ────────────────────────────────────────────────

    /// <summary>Get a profile by ID.</summary>
    public WidgetProfile? GetProfile(Guid id) =>
        Profiles.FirstOrDefault(p => p.Id == id);

    /// <summary>Get the currently active profile.</summary>
    public WidgetProfile? GetActiveProfile() =>
        ActiveProfileId.HasValue ? GetProfile(ActiveProfileId.Value) : null;

    /// <summary>Create a new profile with the given name. Returns the new profile.</summary>
    public WidgetProfile CreateProfile(string name)
    {
        var profile = new WidgetProfile
        {
            Name = name,
            LastModified = DateTime.UtcNow,
        };
        Profiles.Add(profile);
        Save();
        _logger.LogInformation("Created profile: {Name} ({Id})", name, profile.Id);
        return profile;
    }

    /// <summary>Delete a profile by ID. Default profiles cannot be deleted.</summary>
    public bool DeleteProfile(Guid id)
    {
        var profile = GetProfile(id);
        if (profile == null) return false;
        if (profile.IsDefault)
        {
            _logger.LogWarning("Cannot delete default profile: {Name} ({Id})", profile.Name, id);
            return false;
        }

        var removed = Profiles.RemoveAll(p => p.Id == id);
        if (removed > 0)
        {
            if (ActiveProfileId == id) ActiveProfileId = null;
            Save();
            _logger.LogInformation("Deleted profile: {Id}", id);
        }
        return removed > 0;
    }

    /// <summary>Rename a profile.</summary>
    public void RenameProfile(Guid id, string newName)
    {
        var profile = GetProfile(id);
        if (profile == null) return;
        profile.Name = newName;
        profile.LastModified = DateTime.UtcNow;
        Save();
    }

    /// <summary>
    /// Set the active profile by ID. Fires ActiveProfileChanged.
    /// Pass null to clear the active selection.
    /// </summary>
    public void SetActiveProfile(Guid? id)
    {
        ActiveProfileId = id;
        var profile = id.HasValue ? GetProfile(id.Value) : null;
        ActiveProfileChanged?.Invoke(this, profile);
        Save();
        _logger.LogInformation("Active profile set to: {Name}", profile?.Name ?? "(none)");
    }

    // ── Snapshot capture/apply ──────────────────────────────────────

    /// <summary>
    /// Capture the current widget settings into a profile's widget settings snapshot.
    /// Call this to "save current state" into a profile.
    /// </summary>
    public void CaptureToProfile(Guid profileId, WidgetManager widgetManager)
    {
        var profile = GetProfile(profileId);
        if (profile == null) return;

        profile.WidgetVisibility.Clear();
        profile.WidgetSettings.Clear();

        foreach (WidgetType wt in Enum.GetValues<WidgetType>())
        {
            bool exists = widgetManager.HasWidgetType(wt);
            profile.WidgetVisibility[wt] = exists;

            if (exists)
            {
                var widget = widgetManager.GetWidgetsByType(wt).FirstOrDefault();
                if (widget != null)
                {
                    // Copy the widget's current Config.Settings as the profile snapshot
                    profile.WidgetSettings[wt] = new Dictionary<string, object>(
                        widget.Config.Settings ?? new Dictionary<string, object>());
                }
            }
        }

        profile.LastModified = DateTime.UtcNow;
        Save();
        _logger.LogInformation("Captured state into profile: {Name}", profile.Name);
    }

    /// <summary>
    /// Apply a profile's widget settings to the WidgetManager.
    /// This toggles widget visibility AND pushes stored settings into each widget.
    /// </summary>
    public void ApplyProfile(Guid profileId, WidgetManager widgetManager)
    {
        var profile = GetProfile(profileId);
        if (profile == null) return;

        foreach (var (wt, visible) in profile.WidgetVisibility)
        {
            bool exists = widgetManager.HasWidgetType(wt);

            if (visible && !exists)
            {
                widgetManager.CreateWidget(wt);
            }
            else if (!visible && exists)
            {
                var widgets = widgetManager.GetWidgetsByType(wt).ToList();
                foreach (var w in widgets)
                    widgetManager.RemoveWidget(w.WidgetId);
            }

            // Push stored settings into the widget
            if (visible && profile.WidgetSettings.TryGetValue(wt, out var settings))
            {
                var widget = widgetManager.GetWidgetsByType(wt).FirstOrDefault();
                if (widget?.Config != null)
                {
                    widget.Config.Settings = new Dictionary<string, object>(settings);
                    // Widgets will pick up new settings on next UpdateUI cycle or via explicit sync
                }
            }
        }

        SetActiveProfile(profileId);
        _logger.LogInformation("Applied profile: {Name}", profile.Name);
    }

    // ── Auto-match ──────────────────────────────────────────────────

    /// <summary>
    /// Find the best matching profile for the given session + car class.
    /// Returns null if no profile matches.
    /// </summary>
    public WidgetProfile? FindBestMatch(SessionCategory? session, string? carClass)
    {
        WidgetProfile? best = null;
        int bestScore = -1;

        foreach (var p in Profiles)
        {
            int score = p.MatchScore(session, carClass);
            if (score > bestScore)
            {
                bestScore = score;
                best = p;
            }
        }

        return bestScore > 0 ? best : null;
    }

    // ── Default profiles ────────────────────────────────────────────

    private void InitializeDefaults()
    {
        // Default Race profile: full complement
        var raceProfile = new WidgetProfile
        {
            Name = "Race (Default)",
            IsDefault = true,
            SessionBinding = SessionCategory.Race,
            WidgetVisibility = new()
            {
                { WidgetType.MRTOne, true },
                { WidgetType.TurnDisplay, true },
                { WidgetType.FuelCalculator, true },
                { WidgetType.Relative, true },
                { WidgetType.ProximityFeed, true },
                { WidgetType.Standings, true },
            },
        };

        // Default Practice profile: minimal
        var practiceProfile = new WidgetProfile
        {
            Name = "Practice (Default)",
            IsDefault = true,
            SessionBinding = SessionCategory.Practice,
            WidgetVisibility = new()
            {
                { WidgetType.MRTOne, true },
                { WidgetType.TurnDisplay, true },
                { WidgetType.FuelCalculator, true },
                { WidgetType.Relative, true },
                { WidgetType.ProximityFeed, false },
                { WidgetType.Standings, false },
            },
        };

        // Default Qualifying profile
        var qualiProfile = new WidgetProfile
        {
            Name = "Qualifying (Default)",
            IsDefault = true,
            SessionBinding = SessionCategory.Qualifying,
            WidgetVisibility = new()
            {
                { WidgetType.MRTOne, true },
                { WidgetType.TurnDisplay, true },
                { WidgetType.FuelCalculator, false },
                { WidgetType.Relative, false },
                { WidgetType.ProximityFeed, false },
                { WidgetType.Standings, false },
            },
        };

        Profiles.AddRange(new[] { raceProfile, practiceProfile, qualiProfile });
        Save();
    }

    // ── Persistence ─────────────────────────────────────────────────

    private static string ConfigFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MRT-UI", "profiles.json");

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var data = new ProfileStorageData
            {
                AutoSwitch = AutoSwitch,
                ActiveProfileId = ActiveProfileId?.ToString(),
                Profiles = Profiles.Select(p => new ProfileData
                {
                    Id = p.Id.ToString(),
                    Name = p.Name,
                    IsDefault = p.IsDefault,
                    SessionBinding = p.SessionBinding?.ToString(),
                    CarClassBinding = p.CarClassBinding,
                    LastModified = p.LastModified,
                    WidgetVisibility = p.WidgetVisibility.ToDictionary(
                        kv => kv.Key.ToString(), kv => kv.Value),
                    WidgetSettings = p.WidgetSettings.ToDictionary(
                        kv => kv.Key.ToString(),
                        kv => kv.Value.ToDictionary(s => s.Key, s => s.Value)),
                }).ToList()
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(data, options));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save profiles");
        }
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(ConfigFilePath)) return;

            var json = File.ReadAllText(ConfigFilePath);
            var data = JsonSerializer.Deserialize<ProfileStorageData>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (data == null) return;

            AutoSwitch = data.AutoSwitch;
            if (Guid.TryParse(data.ActiveProfileId, out var activeId))
                ActiveProfileId = activeId;

            Profiles.Clear();
            foreach (var pd in data.Profiles)
            {
                if (!Guid.TryParse(pd.Id, out var id)) continue;

                SessionCategory? session = null;
                if (!string.IsNullOrEmpty(pd.SessionBinding) &&
                    Enum.TryParse<SessionCategory>(pd.SessionBinding, out var sc))
                    session = sc;

                var profile = new WidgetProfile
                {
                    Id = id,
                    Name = pd.Name,
                    IsDefault = pd.IsDefault,
                    SessionBinding = session,
                    CarClassBinding = pd.CarClassBinding,
                    LastModified = pd.LastModified,
                };

                foreach (var kv in pd.WidgetVisibility)
                {
                    if (Enum.TryParse<WidgetType>(kv.Key, out var wt))
                        profile.WidgetVisibility[wt] = kv.Value;
                }

                if (pd.WidgetSettings != null)
                {
                    foreach (var kv in pd.WidgetSettings)
                    {
                        if (Enum.TryParse<WidgetType>(kv.Key, out var wt))
                            profile.WidgetSettings[wt] = new Dictionary<string, object>(kv.Value);
                    }
                }

                Profiles.Add(profile);
            }

            _logger.LogInformation("Loaded {Count} profiles", Profiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load profiles");
        }
    }

    // ── JSON DTOs ───────────────────────────────────────────────────

    private class ProfileStorageData
    {
        public bool AutoSwitch { get; set; }
        public string? ActiveProfileId { get; set; }
        public List<ProfileData> Profiles { get; set; } = new();
    }

    private class ProfileData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public string? SessionBinding { get; set; }
        public string? CarClassBinding { get; set; }
        public DateTime LastModified { get; set; }
        public Dictionary<string, bool> WidgetVisibility { get; set; } = new();
        public Dictionary<string, Dictionary<string, object>>? WidgetSettings { get; set; }
    }
}
