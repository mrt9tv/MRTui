using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Services;

namespace iRacingOverlay.WPF.ViewModels;

public class OverlayViewModel : INotifyPropertyChanged
{
    private readonly WidgetManager _widgetManager;
    private WidgetItemViewModel? _selectedWidget;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<WidgetItemViewModel> Widgets { get; }

    public WidgetItemViewModel? SelectedWidget
    {
        get => _selectedWidget;
        set
        {
            // Deselect previous widget
            if (_selectedWidget != null)
            {
                _selectedWidget.IsSelected = false;
            }
            
            _selectedWidget = value;
            
            // Select new widget
            if (_selectedWidget != null)
            {
                _selectedWidget.IsSelected = true;
            }
            
            OnPropertyChanged();
        }
    }

    public OverlayViewModel(WidgetManager widgetManager)
    {
        _widgetManager = widgetManager;

        // Initialize widget list with all available widget types
        Widgets = new ObservableCollection<WidgetItemViewModel>
        {
            new WidgetItemViewModel("The MRT Simplicity", WidgetType.GearGauge, "🎯", _widgetManager),
            new WidgetItemViewModel("Speed Widget", WidgetType.Speed, "🏎️", _widgetManager),
            new WidgetItemViewModel("Data Widget", WidgetType.Data, "📊", _widgetManager),
            new WidgetItemViewModel("Fuel Calculator", WidgetType.Fuel, "⛽", _widgetManager)
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
    private bool _isSelected;
    private double _opacity;
    private double _scale;
    private string _position;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }
    public WidgetType Type { get; }
    public string Icon { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged();
        }
    }

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

    public double Scale
    {
        get => _scale;
        set
        {
            if (Math.Abs(_scale - value) > 0.01)
            {
                _scale = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScalePercentage));

                // Apply scale to all widgets of this type
                ApplyScale();
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
    public string ScalePercentage => $"{(int)(Scale * 100)}%";
    
    public string ActivateButtonText => IsActive ? "Deactivate Widget" : "Activate Widget";
    public string ActivateButtonIcon => IsActive ? "🔴" : "🟢";
    
    public ICommand ToggleActiveCommand { get; }

    public WidgetItemViewModel(string name, WidgetType type, string icon, WidgetManager widgetManager)
    {
        Name = name;
        Type = type;
        Icon = icon;
        _widgetManager = widgetManager;
        _opacity = 1.0;
        _scale = 1.0;
        _position = "N/A";
        _isActive = false;
        _isSelected = false;
        
        ToggleActiveCommand = new RelayCommand(ToggleActive);
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

        // Update position and other properties from first widget of this type
        var widget = _widgetManager.GetWidgetsByType(Type).FirstOrDefault();
        if (widget != null)
        {
            var config = widget.GetConfiguration();
            Position = $"X: {(int)config.X}, Y: {(int)config.Y}";
            
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

    private void ApplyScale()
    {
        var widgets = _widgetManager.GetWidgetsByType(Type);
        foreach (var widget in widgets)
        {
            // Scale is applied via Width/Height
            var baseWidth = widget.Config.Width;
            var baseHeight = widget.Config.Height;
            
            widget.Width = baseWidth * Scale;
            widget.Height = baseHeight * Scale;
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Simple ICommand implementation for button click handling
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    
    public void Execute(object? parameter) => _execute();
}
