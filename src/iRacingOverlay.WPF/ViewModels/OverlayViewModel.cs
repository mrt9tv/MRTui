using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Services;
using iRacingOverlay.WPF.Utils;
using Microsoft.Extensions.Logging;

namespace iRacingOverlay.WPF.ViewModels;

public class OverlayViewModel : INotifyPropertyChanged
{
    private readonly WidgetManager _widgetManager;
    private readonly ILogger<OverlayViewModel>? _logger;
    private readonly DispatcherTimer _updateTimer;
    private WidgetItemViewModel? _selectedWidget;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<WidgetItemViewModel> Widgets { get; }
    
    public ICommand SelectWidgetCommand { get; }

    public WidgetItemViewModel? SelectedWidget
    {
        get => _selectedWidget;
        set
        {
            if (_selectedWidget != value)
            {
                _selectedWidget = value;
                OnPropertyChanged();
            }
        }
    }

    public OverlayViewModel(WidgetManager widgetManager, ILogger<OverlayViewModel>? logger = null)
    {
        _widgetManager = widgetManager ?? throw new ArgumentNullException(nameof(widgetManager));
        _logger = logger;

        // Initialize commands
        SelectWidgetCommand = new RelayCommand<WidgetItemViewModel>(OnSelectWidget);

        // Initialize widget list with all available widget types
        Widgets = new ObservableCollection<WidgetItemViewModel>
        {
            new WidgetItemViewModel("MRT One", WidgetType.MRTOne, "🥇", _widgetManager),
            new WidgetItemViewModel("Fuel Assist", WidgetType.Fuel, "⛽", _widgetManager),
            new WidgetItemViewModel("Race Strategy", WidgetType.RaceStrategy, "🏁", _widgetManager)
        };

        // Subscribe to widget manager events
        _widgetManager.WidgetCreated += OnWidgetCreated;
        _widgetManager.WidgetRemoved += OnWidgetRemoved;
        _widgetManager.WidgetVisibilityChanged += OnWidgetVisibilityChanged;

        // Update initial state for each widget
        foreach (var widget in Widgets)
        {
            widget.UpdateState();
        }

        // Select first widget by default
        SelectedWidget = Widgets.FirstOrDefault();
        
        // Setup timer to update selected widget position periodically
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500) // Update every 500ms
        };
        _updateTimer.Tick += (s, e) => SelectedWidget?.UpdateState();
        _updateTimer.Start();
    }

    private void OnSelectWidget(WidgetItemViewModel? widget)
    {
        SelectedWidget = widget;
    }

    private void OnWidgetCreated(object? sender, Core.WidgetBase e)
    {
        // Update the corresponding widget item's state
        var widgetItem = Widgets.FirstOrDefault(w => w.Type == e.WidgetType);
        widgetItem?.UpdateState();
    }

    private void OnWidgetRemoved(object? sender, Guid e)
    {
        // Update all widgets' states (simpler than tracking which one was removed)
        foreach (var widget in Widgets)
        {
            widget.UpdateState();
        }
    }

    private void OnWidgetVisibilityChanged(object? sender, EventArgs e)
    {
        // Update RaceStrategy widget state when visibility changes
        var raceStrategyWidget = Widgets.FirstOrDefault(w => w.Type == WidgetType.RaceStrategy);
        raceStrategyWidget?.UpdateState();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// ViewModel for a single widget item in the list
/// </summary>
public class WidgetItemViewModel : INotifyPropertyChanged
{
    private readonly WidgetManager _widgetManager;
    private bool _isActive;
    private double _opacity;
    private double _widgetSize;
    private string _position;
    
    // Default and min/max sizes for each widget type
    private const double DEFAULT_MRTONE_SIZE = 200;
    private const double MIN_MRTONE_SIZE = 100;
    private const double MAX_MRTONE_SIZE = 400;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }
    public WidgetType Type { get; }
    public string Icon { get; }
    
    // Expose AppSettings singleton for data binding (e.g., EnableLateralSpotter checkbox)
    public AppSettings Settings => AppSettings.Instance;
    
    // Size constraints based on widget type
    public double MinSize => Type == WidgetType.MRTOne ? MIN_MRTONE_SIZE : 100;
    public double MaxSize => Type == WidgetType.MRTOne ? MAX_MRTONE_SIZE : 400;
    public double DefaultSize => Type == WidgetType.MRTOne ? DEFAULT_MRTONE_SIZE : 200;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActivateButtonText));
                OnPropertyChanged(nameof(ActivateButtonIcon));

                // Special handling for RaceStrategy (standalone window)
                if (Type == WidgetType.RaceStrategy)
                {
                    try
                    {
                        if (value)
                        {
                            // Create/show RaceStrategy widget
                            _widgetManager.CreateWidget(Type);
                        }
                        else
                        {
                            // Hide RaceStrategy widget
                            _widgetManager.HideRaceStrategyWidget();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Note: WidgetItemViewModel is a nested class without access to ILogger
                        System.Diagnostics.Debug.WriteLine($"Error toggling RaceStrategy: {ex}");
                        System.Windows.MessageBox.Show($"Failed to open Race Strategy Widget: {ex.Message}", 
                            "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        
                        // Revert toggle state
                        _isActive = !value;
                        OnPropertyChanged(nameof(IsActive));
                    }
                    return; // Don't process as normal WidgetBase
                }

                // Toggle widget on/off
                if (value)
                {
                    // Check if widget already exists
                    var existingWidgets = _widgetManager.GetWidgetsByType(Type).ToList();
                    
                    var logPath = Utils.LoggingPaths.GetLogPath("debug.log");
                    
                    if (existingWidgets.Any())
                    {
                        // Widget exists, just show it
                        var log = $"\n[{DateTime.Now:HH:mm:ss}] [OverlayVM] IsActive: Widget exists, showing it";
                        File.AppendAllText(logPath, log);
                        
                        foreach (var widget in existingWidgets)
                        {
                            widget.Show();
                            widget.Config.IsVisible = true; // Update config to track visibility
                        }
                        
                        // Save layout to persist IsVisible = true
                        _widgetManager.SaveCurrentLayout();
                    }
                    else
                    {
                        // Widget doesn't exist, create it with saved config if available
                        var savedConfig = _widgetManager.GetSavedWidgetConfig(Type);
                        
                        if (savedConfig != null)
                        {
                            var log = $"\n[{DateTime.Now:HH:mm:ss}] [OverlayVM] IsActive: Creating widget with saved config ({savedConfig.Settings.Count} settings)";
                            File.AppendAllText(logPath, log);
                            _widgetManager.CreateWidget(Type, savedConfig);
                        }
                        else
                        {
                            var log = $"\n[{DateTime.Now:HH:mm:ss}] [OverlayVM] IsActive: Creating widget with defaults";
                            File.AppendAllText(logPath, log);
                            _widgetManager.CreateWidget(Type);
                        }
                    }
                }
                else
                {
                    // Hide widgets (don't remove them to preserve settings)
                    var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MRT-UI", "debug.log");
                    var log = $"\n[{DateTime.Now:HH:mm:ss}] [OverlayVM] IsActive: Hiding widgets";
                    File.AppendAllText(logPath, log);
                    
                    var widgets = _widgetManager.GetWidgetsByType(Type).ToList();
                    foreach (var widget in widgets)
                    {
                        widget.Hide();
                        widget.Config.IsVisible = false; // Update config to track visibility
                    }
                    
                    // Save layout to persist IsVisible = false
                    _widgetManager.SaveCurrentLayout();
                }
            }
        }
    }

    public double Opacity
    {
        get => _opacity;
        set
        {
            if (Math.Abs(_opacity - value) > 0.01)
            {
                _opacity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OpacityPercentage));

                // Apply opacity to all widgets of this type
                ApplyOpacity();
            }
        }
    }

    public double WidgetSize
    {
        get => _widgetSize;
        set
        {
            // Clamp to min/max bounds
            double clampedValue = Math.Max(MinSize, Math.Min(MaxSize, value));
            
            if (Math.Abs(_widgetSize - clampedValue) > 0.01)
            {
                _widgetSize = clampedValue;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SizeDisplay));

                // Apply size to all widgets of this type
                ApplySize();
            }
        }
    }

    public string Position
    {
        get => _position;
        set
        {
            _position = value;
            OnPropertyChanged();
        }
    }

    public string OpacityPercentage => $"{(int)(Opacity * 100)}%";
    public string SizeDisplay => $"{(int)WidgetSize}px";
    
    public string ActivateButtonText => IsActive ? "Deactivate Widget" : "Activate Widget";
    public string ActivateButtonIcon => IsActive ? "🔴" : "🟢";
    
    public ICommand ToggleActiveCommand { get; }
    public ICommand ResetPositionCommand { get; }
    public ICommand ResetAllCommand { get; }
    public ICommand CenterHorizontallyCommand { get; }
    public ICommand CenterVerticallyCommand { get; }
    public ICommand ApplySettingsCommand { get; }
    
    // MRT One Widget Settings (only applicable when Type == WidgetType.MRTOne)
    private string? _topSelectedField;
    private string? _centerSelectedField;
    private string? _bottomSelectedField;
    private string? _leftSelectedField;
    private string? _rightSelectedField;
    private bool _showTop;
    private bool _showCenter;
    private bool _showBottom;
    private bool _showLeft;
    private bool _showRight;
    private bool _isLoadingSettings; // Flag to prevent applying settings during load
    
    /// <summary>
    /// Available telemetry fields for dropdown selection (includes "None" for hiding sections)
    /// </summary>
    public List<string> AvailableTelemetryFields { get; } = new List<string>
    {
        "None",  // Special option to hide section
        "Speed",
        "RPM",
        "Gear",
        "Throttle",
        "Brake",
        "ABSActive",
        "BrakeBias",
        "TractionControl",
        "Clutch",
        "FuelLevel",
        "FuelPercent",
        "FuelAvgLast",        // Phase 2: Last lap fuel usage
        "FuelAvgL5",          // Phase 2: L5 average fuel usage
        "FuelAvgL10",         // Phase 2: L10 average fuel usage
        "FuelAvgSession",     // Phase 2: Session average fuel usage
        "FuelMinPerLap",      // Phase 2: Minimum fuel per lap
        "FuelMaxPerLap",      // Phase 2: Maximum fuel per lap
        "FuelLapsRemainingL5",   // Phase 2: Laps remaining (L5 avg)
        "FuelLapsRemainingL10",  // Phase 2: Laps remaining (L10 avg)
        "FuelNeededToFinish",    // Phase 2: Fuel needed to finish race
        "FuelDeltaToFinish",     // Phase 2: Fuel surplus/deficit
        "FuelGreenAvg",       // Phase 2: Green flag average
        "FuelYellowAvg",      // Phase 2: Yellow flag average
        "FuelGreenLapsRemain",   // Phase 2: Laps remaining at green pace
        "FuelYellowLapsRemain",  // Phase 2: Laps remaining at yellow pace
        "WaterTemp",
        "OilTemp",
        "LapNumber",
        "Position",
        "LastLapTime",
        "BestLapTime"
    };
    
    /// <summary>
    /// Available telemetry fields for side boxes (excludes lap times for better formatting)
    /// </summary>
    public List<string> AvailableSideBoxFields { get; } = new List<string>
    {
        "None",  // Special option to hide section
        "Speed",
        "RPM",
        "Gear",
        "Throttle",
        "Brake",
        "ABSActive",
        "BrakeBias",
        "TractionControl",
        "WheelLock",
        "Clutch",
        "FuelLevel",
        "FuelPercent",
        "FuelAvgLast",        // Phase 2: Last lap fuel usage
        "FuelAvgL5",          // Phase 2: L5 average fuel usage
        "FuelAvgL10",         // Phase 2: L10 average fuel usage
        "FuelAvgSession",     // Phase 2: Session average fuel usage
        "FuelMinPerLap",      // Phase 2: Minimum fuel per lap
        "FuelMaxPerLap",      // Phase 2: Maximum fuel per lap
        "FuelLapsRemainingL5",   // Phase 2: Laps remaining (L5 avg)
        "FuelLapsRemainingL10",  // Phase 2: Laps remaining (L10 avg)
        "FuelNeededToFinish",    // Phase 2: Fuel needed to finish race
        "FuelDeltaToFinish",     // Phase 2: Fuel surplus/deficit
        "FuelGreenAvg",       // Phase 2: Green flag average
        "FuelYellowAvg",      // Phase 2: Yellow flag average
        "FuelGreenLapsRemain",   // Phase 2: Laps remaining at green pace
        "FuelYellowLapsRemain",  // Phase 2: Laps remaining at yellow pace
        "WaterTemp",
        "OilTemp",
        "LapNumber",
        "Position"
    };
    
    public string? TopSelectedField
    {
        get => _topSelectedField;
        set
        {
            if (_topSelectedField != value)
            {
                _topSelectedField = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly when user changes
            }
        }
    }
    
    public string? CenterSelectedField
    {
        get => _centerSelectedField;
        set
        {
            if (_centerSelectedField != value)
            {
                _centerSelectedField = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly when user changes
            }
        }
    }
    
    public string? BottomSelectedField
    {
        get => _bottomSelectedField;
        set
        {
            if (_bottomSelectedField != value)
            {
                _bottomSelectedField = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly when user changes
            }
        }
    }
    
    public string? LeftSelectedField
    {
        get => _leftSelectedField;
        set
        {
            if (_leftSelectedField != value)
            {
                _leftSelectedField = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly when user changes
            }
        }
    }
    
    public string? RightSelectedField
    {
        get => _rightSelectedField;
        set
        {
            if (_rightSelectedField != value)
            {
                _rightSelectedField = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly when user changes
            }
        }
    }
    
    public bool ShowTop
    {
        get => _showTop;
        set
        {
            if (_showTop != value)
            {
                _showTop = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly when user changes
            }
        }
    }
    
    public bool ShowCenter
    {
        get => _showCenter;
        set
        {
            if (_showCenter != value)
            {
                _showCenter = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly when user changes
            }
        }
    }
    
    public bool ShowBottom
    {
        get => _showBottom;
        set
        {
            if (_showBottom != value)
            {
                _showBottom = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly when user changes
            }
        }
    }
    
    public bool ShowLeft
    {
        get => _showLeft;
        set
        {
            if (_showLeft != value)
            {
                _showLeft = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly when user changes
            }
        }
    }
    
    public bool ShowRight
    {
        get => _showRight;
        set
        {
            if (_showRight != value)
            {
                _showRight = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly when user changes
            }
        }
    }
    
    // PHASE 2: Visual Enhancement Properties (all toggleable)
    private bool _enableGradientBackground;
    private bool _enableShiftPointRing;
    private bool _enableGlowEffects;
    private bool _enableFuelDisplay;
    private bool _enableEnhancedRadar;  // Phase 4.2

    public bool EnableGradientBackground
    {
        get => _enableGradientBackground;
        set
        {
            if (_enableGradientBackground != value)
            {
                _enableGradientBackground = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly
            }
        }
    }
    
    public bool EnableShiftPointRing
    {
        get => _enableShiftPointRing;
        set
        {
            if (_enableShiftPointRing != value)
            {
                _enableShiftPointRing = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly
            }
        }
    }
    
    public bool EnableGlowEffects
    {
        get => _enableGlowEffects;
        set
        {
            if (_enableGlowEffects != value)
            {
                _enableGlowEffects = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly
            }
        }
    }
    
    public bool EnableFuelDisplay
    {
        get => _enableFuelDisplay;
        set
        {
            if (_enableFuelDisplay != value)
            {
                _enableFuelDisplay = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly
            }
        }
    }

    public bool EnableEnhancedRadar
    {
        get => _enableEnhancedRadar;
        set
        {
            if (_enableEnhancedRadar != value)
            {
                _enableEnhancedRadar = value;
                OnPropertyChanged();
                if (!_isLoadingSettings) ApplySettings(); // Apply instantly
            }
        }
    }

    /// <summary>
    /// Whether this is the MRT One widget (shows/hides settings UI)
    /// </summary>
    public bool IsMRTOneWidget => Type == WidgetType.MRTOne;

    /// <summary>
    /// Whether this is the Fuel Assist widget (shows/hides Fuel settings UI)
    /// </summary>
    public bool IsFuelAssistWidget => Type == WidgetType.Fuel;

    // Fuel Assist Widget Properties
    
    /// <summary>
    /// Available layout options for Fuel Widget ComboBox
    /// </summary>
    public List<string> FuelWidgetAvailableLayouts { get; } = new List<string>
    {
        "Tower (180x280)",
        "Bar (380x120)",
        "Grid (260x200)"
    };

    /// <summary>
    /// Selected layout for Fuel Widget (bound to ComboBox)
    /// </summary>
    public string FuelWidgetSelectedLayout
    {
        get
        {
            // Map internal layout value to display string
            return AppSettings.Instance.FuelWidget_Layout switch
            {
                "Tower" => "Tower (180x280)",
                "Bar" => "Bar (380x120)",
                "Grid" => "Grid (260x200)",
                _ => "Tower (180x280)"
            };
        }
        set
        {
            // Map display string to internal value
            var layoutValue = value switch
            {
                "Tower (180x280)" => "Tower",
                "Bar (380x120)" => "Bar",
                "Grid (260x200)" => "Grid",
                _ => "Tower"
            };
            
            if (AppSettings.Instance.FuelWidget_Layout != layoutValue)
            {
                AppSettings.Instance.FuelWidget_Layout = layoutValue;
                AppSettings.Instance.Save();
                OnPropertyChanged();
            }
        }
    }

    // Legacy radio button properties - kept for backward compatibility
    public bool FuelWidgetLayoutTower
    {
        get => AppSettings.Instance.FuelWidget_Layout == "Tower";
        set
        {
            if (value)
            {
                AppSettings.Instance.FuelWidget_Layout = "Tower";
                AppSettings.Instance.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(FuelWidgetLayoutBar));
                OnPropertyChanged(nameof(FuelWidgetLayoutGrid));
                OnPropertyChanged(nameof(FuelWidgetSelectedLayout));
            }
        }
    }

    public bool FuelWidgetLayoutBar
    {
        get => AppSettings.Instance.FuelWidget_Layout == "Bar";
        set
        {
            if (value)
            {
                AppSettings.Instance.FuelWidget_Layout = "Bar";
                AppSettings.Instance.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(FuelWidgetLayoutTower));
                OnPropertyChanged(nameof(FuelWidgetLayoutGrid));
                OnPropertyChanged(nameof(FuelWidgetSelectedLayout));
            }
        }
    }

    public bool FuelWidgetLayoutGrid
    {
        get => AppSettings.Instance.FuelWidget_Layout == "Grid";
        set
        {
            if (value)
            {
                AppSettings.Instance.FuelWidget_Layout = "Grid";
                AppSettings.Instance.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(FuelWidgetLayoutTower));
                OnPropertyChanged(nameof(FuelWidgetLayoutBar));
                OnPropertyChanged(nameof(FuelWidgetSelectedLayout));
            }
        }
    }

    public bool FuelWidget_ShowPercentage
    {
        get => AppSettings.Instance.FuelWidget_ShowPercentage;
        set { AppSettings.Instance.FuelWidget_ShowPercentage = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    public bool FuelWidget_ShowBar
    {
        get => AppSettings.Instance.FuelWidget_ShowBar;
        set { AppSettings.Instance.FuelWidget_ShowBar = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    public bool FuelWidget_ShowRange
    {
        get => AppSettings.Instance.FuelWidget_ShowRange;
        set { AppSettings.Instance.FuelWidget_ShowRange = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    public bool FuelWidget_ShowSavingBadge
    {
        get => AppSettings.Instance.FuelWidget_ShowSavingBadge;
        set { AppSettings.Instance.FuelWidget_ShowSavingBadge = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    public bool FuelWidget_ShowStrategyRecommendation
    {
        get => AppSettings.Instance.FuelWidget_ShowStrategyRecommendation;
        set { AppSettings.Instance.FuelWidget_ShowStrategyRecommendation = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    public bool FuelWidget_ShowLiveSparkline
    {
        get => AppSettings.Instance.FuelWidget_ShowLiveSparkline;
        set { AppSettings.Instance.FuelWidget_ShowLiveSparkline = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    public bool FuelWidget_ShowLapSparkline
    {
        get => AppSettings.Instance.FuelWidget_ShowLapSparkline;
        set { AppSettings.Instance.FuelWidget_ShowLapSparkline = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    public bool FuelWidget_ShowPitExitPosition
    {
        get => AppSettings.Instance.FuelWidget_ShowPitExitPosition;
        set { AppSettings.Instance.FuelWidget_ShowPitExitPosition = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    public bool FuelWidget_ShowLiftPoints
    {
        get => AppSettings.Instance.FuelWidget_ShowLiftPoints;
        set { AppSettings.Instance.FuelWidget_ShowLiftPoints = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    public bool FuelWidget_ShowSavingAlerts
    {
        get => AppSettings.Instance.FuelWidget_ShowSavingAlerts;
        set { AppSettings.Instance.FuelWidget_ShowSavingAlerts = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    public bool FuelWidget_EnableDynamicBuffer
    {
        get => AppSettings.Instance.FuelWidget_EnableDynamicBuffer;
        set { AppSettings.Instance.FuelWidget_EnableDynamicBuffer = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    // Legacy properties removed - individual field toggles no longer used
    // Use FuelWidget_ShowPitStrategy master toggle instead

    public string FuelWidget_Method
    {
        get => AppSettings.Instance.FuelWidget_Method;
        set { AppSettings.Instance.FuelWidget_Method = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    public float FuelWidget_BufferLaps
    {
        get => AppSettings.Instance.FuelWidget_BufferLaps;
        set { AppSettings.Instance.FuelWidget_BufferLaps = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    public bool FuelWidget_BlinkCritical
    {
        get => AppSettings.Instance.FuelWidget_BlinkCritical;
        set { AppSettings.Instance.FuelWidget_BlinkCritical = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    public bool FuelWidget_ShowTrends
    {
        get => AppSettings.Instance.FuelWidget_ShowTrends;
        set { AppSettings.Instance.FuelWidget_ShowTrends = value; AppSettings.Instance.Save(); OnPropertyChanged(); }
    }

    public WidgetItemViewModel(string name, WidgetType type, string icon, WidgetManager widgetManager)
    {
        Name = name;
        Type = type;
        Icon = icon;
        _widgetManager = widgetManager;
        _opacity = 1.0;
        _widgetSize = DefaultSize; // Initialize to default size based on widget type
        _position = "N/A";
        _isActive = false;
        
        ToggleActiveCommand = new RelayCommand(ToggleActive);
        ResetPositionCommand = new RelayCommand(ResetPosition);
        ResetAllCommand = new RelayCommand(ResetAll);
        CenterHorizontallyCommand = new RelayCommand(CenterHorizontally);
        CenterVerticallyCommand = new RelayCommand(CenterVertically);
        ApplySettingsCommand = new RelayCommand(ApplySettings);
        
        // Initialize MRT One settings with defaults
        _topSelectedField = "RPM";
        _centerSelectedField = "Gear";
        _bottomSelectedField = "Speed";
        _leftSelectedField = "FuelLevel";
        _rightSelectedField = "Brake";
        _showTop = true;
        _showCenter = true;
        _showBottom = true;
        _showLeft = true;
        _showRight = true;
        
        // PHASE 2: Initialize visual enhancement settings
        _enableGradientBackground = true;  // ON by default
        _enableShiftPointRing = false;
        _enableGlowEffects = false;
        _enableFuelDisplay = true;         // ON by default
    }
    
    private void ToggleActive()
    {
        IsActive = !IsActive;
    }

    public void UpdateState()
    {
        // Special handling for RaceStrategy (standalone window, not WidgetBase)
        if (Type == WidgetType.RaceStrategy)
        {
            // For RaceStrategy, just check if it exists, don't try to read widget properties
            var hasWidget = _widgetManager.HasWidgetType(Type);
            if (_isActive != hasWidget)
            {
                _isActive = hasWidget;
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(ActivateButtonText));
                OnPropertyChanged(nameof(ActivateButtonIcon));
            }
            return; // Don't try to read position/size/opacity from non-existent WidgetBase
        }
        
        // Check if widget is currently active
        var hasWidget2 = _widgetManager.HasWidgetType(Type);
        
        // Only update if state changed (prevents infinite loop)
        if (_isActive != hasWidget2)
        {
            _isActive = hasWidget2;
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(ActivateButtonText));
            OnPropertyChanged(nameof(ActivateButtonIcon));
        }

        // Update position, size, and opacity from first widget of this type
        var widget = _widgetManager.GetWidgetsByType(Type).FirstOrDefault();
        if (widget != null)
        {
            var config = widget.GetConfiguration();
            Position = $"X: {(int)config.X}, Y: {(int)config.Y}";
            
            // Read current widget size
            double currentSize;
            if (Type == WidgetType.Fuel)
            {
                // Fuel widget uses scale transform - convert scale to size value
                currentSize = AppSettings.Instance.FuelWidget_Scale * 200.0; // 200 is baseline
            }
            else
            {
                // For square widgets like MRTOne, use width directly
                currentSize = config.Width;
            }
            
            if (Math.Abs(_widgetSize - currentSize) > 0.01)
            {
                _widgetSize = currentSize;
                OnPropertyChanged(nameof(WidgetSize));
                OnPropertyChanged(nameof(SizeDisplay));
            }
            
            // Update opacity from window property
            if (Math.Abs(_opacity - widget.Opacity) > 0.01)
            {
                _opacity = widget.Opacity;
                OnPropertyChanged(nameof(Opacity));
                OnPropertyChanged(nameof(OpacityPercentage));
            }
            
            // Load MRT One specific settings if applicable
            if (Type == WidgetType.MRTOne && widget is Widgets.MRTOneWidget.MRTOneWidget mrtOneWidget)
            {
                var settings = mrtOneWidget.GetCurrentSettings();
                
                // Set flag to prevent ApplySettings during load
                _isLoadingSettings = true;
                
                _topSelectedField = settings.TopField;
                _centerSelectedField = settings.CenterField;
                _bottomSelectedField = settings.BottomField;
                _leftSelectedField = settings.LeftField;
                _rightSelectedField = settings.RightField;
                _showTop = settings.ShowTop;
                _showCenter = settings.ShowCenter;
                _showBottom = settings.ShowBottom;
                _showLeft = settings.ShowLeft;
                _showRight = settings.ShowRight;
                
                // PHASE 2: Load visual enhancement settings
                _enableGradientBackground = settings.EnableGradientBackground;
                _enableShiftPointRing = settings.EnableShiftPointRing;
                _enableGlowEffects = settings.EnableGlowEffects;
                _enableFuelDisplay = settings.EnableFuelDisplay;
                _enableEnhancedRadar = settings.EnableEnhancedRadar;  // Phase 4.2

                // Notify all MRT One properties changed
                OnPropertyChanged(nameof(TopSelectedField));
                OnPropertyChanged(nameof(CenterSelectedField));
                OnPropertyChanged(nameof(BottomSelectedField));
                OnPropertyChanged(nameof(LeftSelectedField));
                OnPropertyChanged(nameof(RightSelectedField));
                OnPropertyChanged(nameof(ShowTop));
                OnPropertyChanged(nameof(ShowCenter));
                OnPropertyChanged(nameof(ShowBottom));
                OnPropertyChanged(nameof(ShowLeft));
                OnPropertyChanged(nameof(ShowRight));
                
                // PHASE 2: Notify visual enhancement properties changed
                OnPropertyChanged(nameof(EnableGradientBackground));
                OnPropertyChanged(nameof(EnableShiftPointRing));
                OnPropertyChanged(nameof(EnableGlowEffects));
                OnPropertyChanged(nameof(EnableEnhancedRadar));  // Phase 4.2

                // Clear flag after loading complete
                _isLoadingSettings = false;
            }
        }
        else
        {
            Position = "N/A";
            // Reset to default size when widget is removed
            _widgetSize = DefaultSize;
            OnPropertyChanged(nameof(WidgetSize));
            OnPropertyChanged(nameof(SizeDisplay));
        }
    }

    private void ApplyOpacity()
    {
        var widgets = _widgetManager.GetWidgetsByType(Type);
        foreach (var widget in widgets)
        {
            widget.Opacity = Opacity;
        }
        
        // Save layout to persist opacity changes
        _widgetManager.SaveCurrentLayout();
    }

    private void ApplySize()
    {
        var widgets = _widgetManager.GetWidgetsByType(Type);
        
        if (Type == WidgetType.Fuel)
        {
            // Fuel widget uses scale transform instead of direct size
            // Map WidgetSize (100-400) to scale (0.5-2.0)
            double scale = WidgetSize / 200.0; // 200 is baseline
            AppSettings.Instance.FuelWidget_Scale = scale;
            AppSettings.Instance.Save();
            // Trigger settings changed event to update widget
            AppSettings.Instance.NotifyChanged();
        }
        else
        {
            // For square widgets (MRTOne), set both width and height to the same value
            foreach (var widget in widgets)
            {
                widget.Width = WidgetSize;
                widget.Height = WidgetSize;
            }
        }
        
        // Save layout to persist size changes
        _widgetManager.SaveCurrentLayout();
    }

    private void ResetPosition()
    {
        var widgets = _widgetManager.GetWidgetsByType(Type);
        foreach (var widget in widgets)
        {
            // Reset to top-left corner with small offset
            widget.Left = 50;
            widget.Top = 50;
        }
        UpdateState(); // Update position display
        
        // Save layout to persist position changes
        _widgetManager.SaveCurrentLayout();
    }

    private void ResetAll()
    {
        // Reset size to default
        WidgetSize = DefaultSize;
        
        // Reset opacity to 100%
        Opacity = 1.0;
        
        // Reset position
        ResetPosition();
        
        // Reset MRT-1 specific settings to defaults
        if (Type == WidgetType.MRTOne)
        {
            // Reset Phase 2 visual enhancements to defaults
            EnableGradientBackground = true;  // ON by default
            EnableShiftPointRing = false;     // OFF by default
            EnableGlowEffects = false;        // OFF by default
            EnableEnhancedRadar = false;      // OFF by default (Phase 4.2)

            // Reset field selections to defaults
            TopSelectedField = "Speed";
            CenterSelectedField = "Gear";
            BottomSelectedField = "RPM";
            LeftSelectedField = "FuelLevel";
            RightSelectedField = "Brake";
            
            // Reset field visibility to defaults (all shown)
            ShowTop = true;
            ShowCenter = true;
            ShowBottom = true;
            ShowLeft = true;
            ShowRight = true;
            
            // Apply the reset settings to the widget
            ApplySettings();
        }
        
        // Save layout to persist all reset changes
        _widgetManager.SaveCurrentLayout();
    }

    private void CenterHorizontally()
    {
        var widgets = _widgetManager.GetWidgetsByType(Type);
        foreach (var widget in widgets)
        {
            // Get primary screen dimensions
            var screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
            
            // Center horizontally: (screenWidth - widgetWidth) / 2
            widget.Left = (screenWidth - widget.Width) / 2;
        }
        UpdateState(); // Update position display
        
        // Save layout to persist position changes
        _widgetManager.SaveCurrentLayout();
    }

    private void CenterVertically()
    {
        var widgets = _widgetManager.GetWidgetsByType(Type);
        foreach (var widget in widgets)
        {
            // Get primary screen dimensions
            var screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
            
            // Center vertically: (screenHeight - widgetHeight) / 2
            widget.Top = (screenHeight - widget.Height) / 2;
        }
        UpdateState(); // Update position display
        
        // Save layout to persist position changes
        _widgetManager.SaveCurrentLayout();
    }
    
    private void ApplySettings()
    {
        // Only apply if this is the MRT One widget
        if (Type != WidgetType.MRTOne || !IsActive)
            return;
        
        var widgets = _widgetManager.GetWidgetsByType(Type);
        foreach (var widget in widgets)
        {
            if (widget is Widgets.MRTOneWidget.MRTOneWidget mrtOneWidget)
            {
                // Create new settings from ViewModel properties
                // "None" selections hide sections, otherwise show them
                var newSettings = new MRTOneSettings
                {
                    TopField = (_topSelectedField == "None" || string.IsNullOrEmpty(_topSelectedField)) ? null : _topSelectedField,
                    CenterField = (_centerSelectedField == "None" || string.IsNullOrEmpty(_centerSelectedField)) ? null : _centerSelectedField,
                    BottomField = (_bottomSelectedField == "None" || string.IsNullOrEmpty(_bottomSelectedField)) ? null : _bottomSelectedField,
                    LeftField = (_leftSelectedField == "None" || string.IsNullOrEmpty(_leftSelectedField)) ? null : _leftSelectedField,
                    RightField = (_rightSelectedField == "None" || string.IsNullOrEmpty(_rightSelectedField)) ? null : _rightSelectedField,
                    ShowTop = !string.IsNullOrEmpty(_topSelectedField) && _topSelectedField != "None",
                    ShowCenter = !string.IsNullOrEmpty(_centerSelectedField) && _centerSelectedField != "None",
                    ShowBottom = !string.IsNullOrEmpty(_bottomSelectedField) && _bottomSelectedField != "None",
                    ShowLeft = !string.IsNullOrEmpty(_leftSelectedField) && _leftSelectedField != "None",
                    ShowRight = !string.IsNullOrEmpty(_rightSelectedField) && _rightSelectedField != "None",
                    // PHASE 2: Visual Enhancement Settings
                    EnableGradientBackground = _enableGradientBackground,
                    EnableShiftPointRing = _enableShiftPointRing,
                    EnableGlowEffects = _enableGlowEffects,
                    EnableFuelDisplay = _enableFuelDisplay,
                    EnableEnhancedRadar = _enableEnhancedRadar  // Phase 4.2
                };

                // Apply to widget (this will update UI and save to config)
                mrtOneWidget.UpdateWidgetSettings(newSettings);
                
                // Persist layout to disk
                _widgetManager.SaveCurrentLayout();
            }
        }
        
        // Refresh state to show updated values
        UpdateState();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
