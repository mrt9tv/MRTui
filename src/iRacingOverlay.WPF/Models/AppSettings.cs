using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace iRacingOverlay.WPF.Models;

/// <summary>
/// Global application settings for all widgets
/// </summary>
public class AppSettings : INotifyPropertyChanged
{
    private static AppSettings? _instance;
    private bool _enableLateralSpotter = true;
    private bool _lockWindows = false;

    /// <summary>
    /// Singleton instance
    /// </summary>
    public static AppSettings Instance => _instance ??= new AppSettings();
    
    /// <summary>
    /// Use metric units (km/h, L, °C) or imperial (mph, gal, °F)
    /// </summary>
    public bool UseMetricUnits { get; set; } = true;
    
    /// <summary>
    /// Primary theme color (teal)
    /// </summary>
    public string PrimaryColor { get; set; } = "#008080";
    
    /// <summary>
    /// Secondary theme color (orange)
    /// </summary>
    public string SecondaryColor { get; set; } = "#FF8000";
    
    /// <summary>
    /// Default widget background opacity (0.0 to 1.0)
    /// </summary>
    public double DefaultOpacity { get; set; } = 0.85;
    
    /// <summary>
    /// Custom short labels for telemetry fields (e.g., "THR" instead of "THRTL")
    /// Note: Labels are now generated dynamically with units in GetFieldLabel()
    /// </summary>
    public Dictionary<string, string> CustomLabels { get; set; } = new Dictionary<string, string>();
    
    // Fuel Widget Settings
    /// <summary>
    /// Fuel Widget X position
    /// </summary>
    public double? FuelWidgetX { get; set; }
    
    /// <summary>
    /// Fuel Widget Y position
    /// </summary>
    public double? FuelWidgetY { get; set; }
    
    /// <summary>
    /// Fuel Widget size (percentage, 50-150)
    /// </summary>
    public int? FuelWidgetSize { get; set; }
    
    /// <summary>
    /// Whether windows are locked (prevents dragging)
    /// </summary>
    public bool LockWindows
    {
        get => _lockWindows;
        set
        {
            if (_lockWindows != value)
            {
                _lockWindows = value;
                OnPropertyChanged();
                NotifyChanged();
            }
        }
    }
    
    /// <summary>
    /// Global opacity for all widgets (0.0 to 1.0)
    /// </summary>
    public double Opacity { get; set; } = 0.9;
    
    /// <summary>
    /// Use metric units (true) or imperial (false)
    /// </summary>
    public bool UseMetric { get; set; } = true;
    
    /// <summary>
    /// Manager window opacity (kept for backward compatibility, always 1.0)
    /// </summary>
    public double ManagerOpacity { get; set; } = 1.0;
    
    /// <summary>
    /// Keep manager window always on top
    /// </summary>
    public bool AlwaysOnTop { get; set; } = false;
    
    /// <summary>
    /// Start manager window minimized to taskbar
    /// </summary>
    public bool StartMinimized { get; set; } = false;

    // Hotkey Settings
    /// <summary>
    /// Modifier key for toggle lock hotkey (Ctrl, Alt, Shift, or None)
    /// </summary>
    public string ToggleLockModifier { get; set; } = "Ctrl";

    /// <summary>
    /// Main key for toggle lock hotkey (default L for Lock)
    /// </summary>
    public string ToggleLockKey { get; set; } = "L";

    /// <summary>
    /// Modifier key for toggle visibility hotkey (Ctrl, Alt, Shift, or None)
    /// </summary>
    public string ToggleVisibilityModifier { get; set; } = "Ctrl";

    /// <summary>
    /// Main key for toggle visibility hotkey (default H for Hide)
    /// </summary>
    public string ToggleVisibilityKey { get; set; } = "H";

    // Window State Settings
    /// <summary>
    /// Manager window width (default 1280)
    /// </summary>
    public double WindowWidth { get; set; } = 1280;
    
    /// <summary>
    /// Manager window height (default 720)
    /// </summary>
    public double WindowHeight { get; set; } = 720;
    
    /// <summary>
    /// Manager window left position (null = center on first launch)
    /// </summary>
    public double? WindowLeft { get; set; }
    
    /// <summary>
    /// Manager window top position (null = center on first launch)
    /// </summary>
    public double? WindowTop { get; set; }
    
    /// <summary>
    /// Manager window maximized state
    /// </summary>
    public bool WindowMaximized { get; set; } = false;
    
    // ===== PHASE 1: 4-Way Proximity Radar Settings =====
    
    /// <summary>
    /// Enable 4-way radar spotter around MRT One circle.
    /// Shows simple colored squares for cars in all 4 directions:
    /// - LEFT/RIGHT: Red (car present) / Green (clear) based on CarLeftRight enum
    /// - FRONT/BACK: Red (car close) / Green (clear) based on CarIdxLapDistPct
    /// When disabled, hides all radar indicators.
    /// </summary>
    public bool EnableLateralSpotter
    {
        get => _enableLateralSpotter;
        set
        {
            if (_enableLateralSpotter != value)
            {
                _enableLateralSpotter = value;
                OnPropertyChanged();
                Save(); // Auto-save when changed
                NotifyChanged();
            }
        }
    }
    
    // Note: Events cannot be serialized and don't need [JsonIgnore] attribute
    public event EventHandler? SettingsChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    
    /// <summary>
    /// Settings file path
    /// </summary>
    [JsonIgnore]
    private static string SettingsFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MRT-UI",
            "settings.json");
    
    /// <summary>
    /// Notify listeners that settings have changed
    /// </summary>
    public void NotifyChanged()
    {
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Check if two hotkeys have the same binding
    /// </summary>
    public bool HotkeysConflict()
    {
        return ToggleLockModifier == ToggleVisibilityModifier &&
               ToggleLockKey == ToggleVisibilityKey;
    }
    
    /// <summary>
    /// Save settings to JSON file
    /// </summary>
    public void Save()
    {
        try
        {
            // Create directory if it doesn't exist
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Serialize settings to JSON with indentation
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };
            
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsFilePath, json);
            
            // Notify listeners after successful save
            NotifyChanged();
        }
        catch (Exception ex)
        {
            // Log error but don't crash the app
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Raise PropertyChanged event
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    /// <summary>
    /// Load settings from JSON file
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                
                if (settings != null)
                {
                    _instance = settings;
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but return default settings
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }
        
        // Return default settings if load failed
        return Instance;
    }
}
