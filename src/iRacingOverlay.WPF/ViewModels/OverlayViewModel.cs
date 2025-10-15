using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Services;

namespace iRacingOverlay.WPF.ViewModels;

public class OverlayViewModel : INotifyPropertyChanged
{
    private readonly WidgetManager _widgetManager;
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

    public OverlayViewModel(WidgetManager widgetManager)
    {
        _widgetManager = widgetManager;

        // Initialize commands
        SelectWidgetCommand = new RelayCommand<WidgetItemViewModel>(OnSelectWidget);

        // Initialize widget list with all available widget types
        Widgets = new ObservableCollection<WidgetItemViewModel>
        {
            new WidgetItemViewModel("MRT One", WidgetType.MRTOne, "🥇", _widgetManager)
            // DataWidget and FuelWidget hidden for now
            // new WidgetItemViewModel("Data Widget", WidgetType.Data, "📊", _widgetManager),
            // new WidgetItemViewModel("Fuel Calculator", WidgetType.Fuel, "⛽", _widgetManager)
        };

        // Subscribe to widget manager events
        _widgetManager.WidgetCreated += OnWidgetCreated;
        _widgetManager.WidgetRemoved += OnWidgetRemoved;

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

                // Toggle widget on/off
                if (value)
                {
                    _widgetManager.CreateWidget(Type);
                }
                else
                {
                    var widgets = _widgetManager.GetWidgetsByType(Type).ToList();
                    foreach (var widget in widgets)
                    {
                        _widgetManager.RemoveWidget(widget.WidgetId);
                    }
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
        "Clutch",
        "FuelLevel",
        "FuelPercent",
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
        "Clutch",
        "FuelLevel",
        "FuelPercent",
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
    
    /// <summary>
    /// Whether this is the MRT One widget (shows/hides settings UI)
    /// </summary>
    public bool IsMRTOneWidget => Type == WidgetType.MRTOne;

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
        _topSelectedField = "Speed";
        _centerSelectedField = "Gear";
        _bottomSelectedField = "RPM";
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
    }
    
    private void ToggleActive()
    {
        IsActive = !IsActive;
    }

    public void UpdateState()
    {
        // Check if widget is currently active
        var hasWidget = _widgetManager.HasWidgetType(Type);
        
        // Only update if state changed (prevents infinite loop)
        if (_isActive != hasWidget)
        {
            _isActive = hasWidget;
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
            
            // Read current widget size (width for square widgets like MRTOne)
            double currentSize = config.Width;
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
    }

    private void ApplySize()
    {
        var widgets = _widgetManager.GetWidgetsByType(Type);
        foreach (var widget in widgets)
        {
            // For square widgets (MRTOne), set both width and height to the same value
            widget.Width = WidgetSize;
            widget.Height = WidgetSize;
        }
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
    }

    private void ResetAll()
    {
        // Reset size to default
        WidgetSize = DefaultSize;
        
        // Reset opacity to 100%
        Opacity = 1.0;
        
        // Reset position
        ResetPosition();
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
                    EnableGlowEffects = _enableGlowEffects
                };
                
                // Apply to widget (this will update UI and save to config)
                mrtOneWidget.UpdateWidgetSettings(newSettings);
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
