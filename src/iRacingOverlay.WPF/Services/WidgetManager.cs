using System;
using System.Collections.Generic;
using System.Linq;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;
using Microsoft.Extensions.Logging;

namespace iRacingOverlay.WPF.Services;

/// <summary>
/// Manages widget lifecycle: creation, destruction, layout save/load
/// </summary>
public class WidgetManager
{
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<WidgetManager> _logger;
    private readonly Dictionary<Guid, WidgetBase> _activeWidgets = new();
    private readonly Dictionary<WidgetType, Func<ITelemetryService, WidgetConfig, WidgetBase>> _widgetFactories = new();

    public IReadOnlyDictionary<Guid, WidgetBase> ActiveWidgets => _activeWidgets;

    public event EventHandler<WidgetBase>? WidgetCreated;
    public event EventHandler<Guid>? WidgetRemoved;

    public WidgetManager(ITelemetryService telemetryService, ILogger<WidgetManager> logger)
    {
        _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RegisterWidgetFactories();
    }

    /// <summary>
    /// Register factory methods for each widget type
    /// </summary>
    private void RegisterWidgetFactories()
    {
        // MVP 2 widgets
        _widgetFactories[WidgetType.Speed] = (service, config) =>
            new Widgets.SpeedWidget.SpeedWidget(service, config);

        _widgetFactories[WidgetType.GearGauge] = (service, config) =>
            new Widgets.GearGaugeWidget.GearGaugeWidget(service);

        _widgetFactories[WidgetType.Data] = (service, config) =>
            new Widgets.DataWidget.DataWidget(service, config);

        _widgetFactories[WidgetType.Fuel] = (service, config) =>
            new Widgets.FuelWidget(service, config);

        // TODO: Uncomment as we create more widgets
        // _widgetFactories[WidgetType.TelemetryTable] = (service, config) =>
        //     new Widgets.TelemetryTableWidget.TelemetryTableWidget(service, config);

        // MVP 3+ widgets will be registered here as they're created
        // _widgetFactories[WidgetType.RPMGauge] = ...
        // etc.
    }

    /// <summary>
    /// Create a new widget instance
    /// </summary>
    public WidgetBase CreateWidget(WidgetType type, WidgetConfig? config = null)
    {
        _logger.LogInformation("Creating widget of type: {Type}", type);

        if (!_widgetFactories.TryGetValue(type, out var factory))
        {
            throw new NotSupportedException($"Widget type '{type}' is not yet implemented");
        }

        // Create config if not provided
        config ??= new WidgetConfig { Type = type };

        // Create widget using factory
        var widget = factory(_telemetryService, config);
        
        // Explicitly show the widget
        widget.Show();

        // Track the widget
        _activeWidgets[widget.WidgetId] = widget;

        // Hook up close event to remove from tracking
        widget.Closed += (s, e) => RemoveWidget(widget.WidgetId);

        _logger.LogInformation("Widget created: {Type} (ID: {Id})", type, widget.WidgetId);

        WidgetCreated?.Invoke(this, widget);

        return widget;
    }

    /// <summary>
    /// Remove and close a widget
    /// </summary>
    public void RemoveWidget(Guid widgetId)
    {
        if (_activeWidgets.TryGetValue(widgetId, out var widget))
        {
            _logger.LogInformation("Removing widget: {Type} (ID: {Id})", widget.WidgetType, widgetId);

            _activeWidgets.Remove(widgetId);
            widget.Close();

            WidgetRemoved?.Invoke(this, widgetId);
        }
    }

    /// <summary>
    /// Remove all widgets
    /// </summary>
    public void RemoveAllWidgets()
    {
        _logger.LogInformation("Removing all widgets");

        var widgetIds = _activeWidgets.Keys.ToList();
        foreach (var id in widgetIds)
        {
            RemoveWidget(id);
        }
    }

    /// <summary>
    /// Show all widgets
    /// </summary>
    public void ShowAllWidgets()
    {
        _logger.LogInformation("Showing all widgets");

        foreach (var widget in _activeWidgets.Values)
        {
            widget.Show();
            widget.Config.IsVisible = true;
        }
    }

    /// <summary>
    /// Hide all widgets
    /// </summary>
    public void HideAllWidgets()
    {
        _logger.LogInformation("Hiding all widgets");

        foreach (var widget in _activeWidgets.Values)
        {
            widget.Hide();
            widget.Config.IsVisible = false;
        }
    }

    /// <summary>
    /// Toggle visibility of all widgets
    /// </summary>
    public void ToggleAllWidgets()
    {
        if (_activeWidgets.Values.Any(w => w.IsVisible))
        {
            HideAllWidgets();
        }
        else
        {
            ShowAllWidgets();
        }
    }

    /// <summary>
    /// Get current layout configuration
    /// </summary>
    public LayoutConfig GetCurrentLayout()
    {
        var layout = new LayoutConfig
        {
            Name = "Current Layout",
            Version = 1,
            AllVisible = _activeWidgets.Values.Any(w => w.IsVisible)
        };

        foreach (var widget in _activeWidgets.Values)
        {
            layout.Widgets.Add(widget.GetConfiguration());
        }

        _logger.LogInformation("Retrieved layout with {Count} widgets", layout.Widgets.Count);

        return layout;
    }

    /// <summary>
    /// Load a layout (removes existing widgets and creates new ones)
    /// </summary>
    public void LoadLayout(LayoutConfig layout)
    {
        _logger.LogInformation("Loading layout: {Name} with {Count} widgets", layout.Name, layout.Widgets.Count);

        // Remove existing widgets
        RemoveAllWidgets();

        // Create widgets from layout
        foreach (var widgetConfig in layout.Widgets)
        {
            try
            {
                CreateWidget(widgetConfig.Type, widgetConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create widget: {Type}", widgetConfig.Type);
            }
        }

        _logger.LogInformation("Layout loaded successfully");
    }

    /// <summary>
    /// Get count of active widgets
    /// </summary>
    public int GetWidgetCount() => _activeWidgets.Count;

    /// <summary>
    /// Check if a specific widget type is already active
    /// </summary>
    public bool HasWidgetType(WidgetType type)
    {
        return _activeWidgets.Values.Any(w => w.WidgetType == type);
    }

    /// <summary>
    /// Get all widgets of a specific type
    /// </summary>
    public IEnumerable<WidgetBase> GetWidgetsByType(WidgetType type)
    {
        return _activeWidgets.Values.Where(w => w.WidgetType == type);
    }

    /// <summary>
    /// Lock or unlock all widgets (prevents dragging, enables click-through when locked)
    /// </summary>
    public void LockAllWidgets(bool lockState)
    {
        foreach (var widget in _activeWidgets.Values)
        {
            widget.SetLocked(lockState);
        }
    }
}
