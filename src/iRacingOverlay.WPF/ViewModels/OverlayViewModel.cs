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

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
