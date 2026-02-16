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
    
    // ===== FUEL WIDGET SETTINGS =====

    // Fuel Widget Layout
    /// <summary>
    /// Fuel widget layout type (Tower, Bar, Grid)
    /// </summary>
    public string FuelWidget_Layout { get; set; } = "Tower";

    // Fuel Widget Visibility
    /// <summary>
    /// Enable fuel widget display
    /// </summary>
    public bool FuelWidget_Enabled { get; set; } = true;

    /// <summary>
    /// Show fuel percentage indicator
    /// </summary>
    public bool FuelWidget_ShowPercentage { get; set; } = true;

    /// <summary>
    /// Show visual fuel bar graph
    /// </summary>
    public bool FuelWidget_ShowBar { get; set; } = true;

    /// <summary>
    /// Show fuel range (min-max consumption) instead of L10/SESSION averages
    /// Displays: "RANGE: 2.50-3.10L" showing consumption variance
    /// </summary>
    public bool FuelWidget_ShowRange { get; set; } = true;

    /// <summary>
    /// Show fuel saving mode badge when active
    /// Displays: "🔋 SAVING: -0.15L/lap needed"
    /// </summary>
    public bool FuelWidget_ShowSavingBadge { get; set; } = true;

    /// <summary>
    /// Show pit strategy recommendation (1-STOP/2-STOP/NO-STOP) with optimal lap
    /// Displays: "⭐ STRATEGY: 1-STOP @ L12"
    /// </summary>
    public bool FuelWidget_ShowStrategyRecommendation { get; set; } = false;

    /// <summary>
    /// Show live fuel consumption sparkline (5 seconds at 0.5s interval)
    /// Displays: Real-time fuel flow sparkline labeled "LIVE (5s)"
    /// </summary>
    public bool FuelWidget_ShowLiveSparkline { get; set; } = true;

    /// <summary>
    /// Show last 5 laps fuel consumption sparkline
    /// Displays: Historical lap fuel usage labeled "LAPS (5)"
    /// </summary>
    public bool FuelWidget_ShowLapSparkline { get; set; } = true;

    /// <summary>
    /// Show fuel consumption trend indicator on L5 average
    /// Displays: "+0.15" (using more, orange) or "-0.10" (improving, teal) next to L5 value
    /// </summary>
    public bool FuelWidget_ShowTrendIndicator { get; set; } = true;

    /// <summary>
    /// Show entire pit strategy section (LAPS, TO GO, PRESS, PIT, PIT IN)
    /// MASTER TOGGLE: When ON, shows all pit strategy fields. When OFF, hides entire section for minimal widget.
    /// This single toggle controls all pit strategy visibility (individual field toggles have been removed).
    /// </summary>
    public bool FuelWidget_ShowPitStrategy { get; set; } = true;

    // Fuel Widget Strategy
    /// <summary>
    /// Fuel averaging method (Last, Last5, Last10, Session, Max)
    /// </summary>
    public string FuelWidget_Method { get; set; } = "Session";  // Default to Session Average for more stable predictions

    /// <summary>
    /// Buffer laps for fuel calculations (safety margin)
    /// </summary>
    public float FuelWidget_BufferLaps { get; set; } = 1.0f;

    /// <summary>
    /// Enable dynamic buffer laps based on race conditions (consistency, position, weather, yellows)
    /// </summary>
    public bool FuelWidget_EnableDynamicBuffer { get; set; } = true;

    /// <summary>
    /// Low fuel warning threshold (laps)
    /// </summary>
    public float FuelWidget_LowFuelThreshold { get; set; } = 5.0f;

    /// <summary>
    /// Critical fuel warning threshold (laps) - blinking red below this value
    /// </summary>
    public float FuelWidget_CriticalThreshold { get; set; } = 1.2f;

    // Fuel Widget Visual
    /// <summary>
    /// Widget scale multiplier (0.7 to 1.5)
    /// </summary>
    public double FuelWidget_Scale { get; set; } = 1.0;

    /// <summary>
    /// Enable blinking animation on critical fuel
    /// </summary>
    public bool FuelWidget_BlinkCritical { get; set; } = true;

    /// <summary>
    /// Show trend arrows for lap-to-lap deltas
    /// </summary>
    public bool FuelWidget_ShowTrends { get; set; } = true;

    // Fuel Widget Position
    /// <summary>
    /// Fuel Widget X position
    /// </summary>
    public double FuelWidget_X { get; set; } = 1650;

    /// <summary>
    /// Fuel Widget Y position
    /// </summary>
    public double FuelWidget_Y { get; set; } = 50;
    
    // Fuel Widget Phase 3: Fuel Saving Mode
    /// <summary>
    /// Show lift point suggestions for fuel saving (disabled by default - generic suggestions not track-specific)
    /// </summary>
    public bool FuelWidget_ShowLiftPoints { get; set; } = false; // Changed from true to false
    
    /// <summary>
    /// Show strategic alerts (PIT THIS LAP, FUEL SAVING WORKING, etc.)
    /// </summary>
    public bool FuelWidget_ShowSavingAlerts { get; set; } = true;
    
    /// <summary>
    /// Enable optimal pit calculator (minimize time loss)
    /// </summary>
    public bool FuelWidget_OptimalPitCalculator { get; set; } = true;
    
    /// <summary>
    /// Show pit exit position prediction (class-filtered track position after pit stop)
    /// </summary>
    public bool FuelWidget_ShowPitExitPosition { get; set; } = true;
    
    /// <summary>
    /// Show pit strategy window
    /// </summary>
    public bool ShowPitStrategyWindow { get; set; } = false;
    
    /// <summary>
    /// Pit Strategy Window X position
    /// </summary>
    public double PitStrategyWindow_X { get; set; } = 100;
    
    /// <summary>
    /// Pit Strategy Window Y position
    /// </summary>
    public double PitStrategyWindow_Y { get; set; } = 100;
    
    /// <summary>
    /// Pit Strategy Window width
    /// </summary>
    public double PitStrategyWindow_Width { get; set; } = 800;
    
    /// <summary>
    /// Pit Strategy Window height
    /// </summary>
    public double PitStrategyWindow_Height { get; set; } = 600;
    
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
    /// Last selected navigation page in MainWindow (Dashboard, Widgets, Sessions, Settings, About).
    /// Restored on startup so the user returns to where they left off.
    /// </summary>
    public string LastNavPage { get; set; } = "Dashboard";
    
    /// <summary>
    /// Start manager window minimized to taskbar
    /// </summary>
    public bool StartMinimized { get; set; } = false;

    /// <summary>
    /// Minimize to system tray instead of taskbar
    /// </summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>
    /// When true, clicking the window close button (X) minimizes to tray
    /// instead of exiting the application (requires MinimizeToTray = true).
    /// </summary>
    public bool CloseToTray { get; set; } = false;

    // ===== AUTO-HIDE IN PITS =====

    /// <summary>
    /// When true, automatically hide configured widgets when the player enters pit lane.
    /// Widgets are restored when the player exits pit lane.
    /// </summary>
    public bool AutoHideInPitsEnabled { get; set; } = false;

    /// <summary>
    /// Per-widget-type auto-hide setting. Key = WidgetType name, Value = true to auto-hide.
    /// Default: ProximityFeed hidden in pits, MRTOne kept visible.
    /// </summary>
    public Dictionary<string, bool> AutoHideInPitsWidgets { get; set; } = new()
    {
        { "MRTOne", false },
        { "ProximityFeed", true },
    };

    // ===== GLOBAL FONT SCALE =====
    
    /// <summary>
    /// Global font size multiplier applied to all overlays (0.7 to 1.5, default 1.0).
    /// Each widget multiplies its base font sizes by this value.
    /// </summary>
    public double GlobalFontScale { get; set; } = 1.0;

    // ===== SNAP-TO-GRID SETTINGS (Phase 3.1) =====
    
    /// <summary>
    /// Enable snap-to-grid when dragging widgets
    /// </summary>
    public bool SnapToGridEnabled { get; set; } = true;
    
    /// <summary>
    /// Grid size in pixels for snap-to-grid (default: 20px)
    /// </summary>
    public int SnapToGridSize { get; set; } = 20;
    
    /// <summary>
    /// Show visual grid overlay when dragging widgets
    /// </summary>
    public bool ShowGridOverlay { get; set; } = true;

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
    
    private bool _showBrakeBiasOverlay = true;
    private double _brakeBiasDisplayDuration = 1.0;
    private bool _showMRTOneFuelDisplay = true;
    
    /// <summary>
    /// Show transient brake bias overlay in center section when value changes
    /// </summary>
    public bool ShowBrakeBiasOverlay
    {
        get => _showBrakeBiasOverlay;
        set
        {
            if (_showBrakeBiasOverlay != value)
            {
                _showBrakeBiasOverlay = value;
                OnPropertyChanged();
                Save();
                NotifyChanged();
            }
        }
    }
    
    /// <summary>
    /// Show comprehensive fuel display below MRT One circular gauge
    /// </summary>
    public bool ShowMRTOneFuelDisplay
    {
        get => _showMRTOneFuelDisplay;
        set
        {
            if (_showMRTOneFuelDisplay != value)
            {
                _showMRTOneFuelDisplay = value;
                OnPropertyChanged();
                Save();
                NotifyChanged();
            }
        }
    }
    
    /// <summary>
    /// Duration (in seconds) that brake bias overlay stays visible after last change
    /// Supports decimal values (e.g., 1.5 seconds)
    /// </summary>
    public double BrakeBiasDisplayDuration
    {
        get => _brakeBiasDisplayDuration;
        set
        {
            if (Math.Abs(_brakeBiasDisplayDuration - value) > 0.01)
            {
                _brakeBiasDisplayDuration = value;
                OnPropertyChanged();
                Save();
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
        catch (IOException ex)
        {
            // File I/O error (disk full, permissions, locked file)
            System.Diagnostics.Debug.WriteLine($"Failed to save settings (I/O error): {ex.Message}");
        }
        catch (JsonException ex)
        {
            // JSON serialization error (should not happen with valid model)
            System.Diagnostics.Debug.WriteLine($"Failed to save settings (JSON error): {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            // Insufficient permissions to write file
            System.Diagnostics.Debug.WriteLine($"Failed to save settings (access denied): {ex.Message}");
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
        catch (FileNotFoundException ex)
        {
            // Settings file doesn't exist yet (first run) - return defaults
            System.Diagnostics.Debug.WriteLine($"Settings file not found (first run): {ex.Message}");
        }
        catch (JsonException ex)
        {
            // JSON deserialization error (corrupted settings file) - return defaults
            System.Diagnostics.Debug.WriteLine($"Failed to load settings (corrupt JSON): {ex.Message}");
        }
        catch (IOException ex)
        {
            // File I/O error - return defaults
            System.Diagnostics.Debug.WriteLine($"Failed to load settings (I/O error): {ex.Message}");
        }
        
        // Return default settings if load failed
        return Instance;
    }
}
