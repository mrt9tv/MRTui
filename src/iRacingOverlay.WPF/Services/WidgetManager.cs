using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;
using Microsoft.Extensions.Logging;

namespace iRacingOverlay.WPF.Services;

/// <summary>
/// Manages widget lifecycle: creation, destruction, layout save/load.
/// Currently supports MRT One only.
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
    public event EventHandler? WidgetVisibilityChanged;

    public WidgetManager(ITelemetryService telemetryService, ILogger<WidgetManager> logger)
    {
        _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        RegisterWidgetFactories();
    }

    private void RegisterWidgetFactories()
    {
        _widgetFactories[WidgetType.MRTOne] = (service, config) =>
            new Widgets.MRTOneWidget.MRTOneWidget(service, config);
        
        _widgetFactories[WidgetType.TurnDisplay] = (service, config) =>
            new Widgets.TurnDisplayWidget.TurnDisplayWidget(service, config);
    }

    public WidgetBase CreateWidget(WidgetType type, WidgetConfig? config = null)
    {
        _logger.LogInformation("Creating widget: {Type}", type);

        if (!_widgetFactories.TryGetValue(type, out var factory))
            throw new NotSupportedException($"Widget type '{type}' is not implemented");

        config ??= new WidgetConfig { Type = type };
        var widget = factory(_telemetryService, config);

        _activeWidgets[widget.WidgetId] = widget;
        widget.Closing += Widget_Closing;

        WidgetCreated?.Invoke(this, widget);
        SaveCurrentLayout();
        return widget;
    }

    private void Widget_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (sender is WidgetBase widget)
        {
            e.Cancel = true;
            widget.Hide();
            widget.Config.IsVisible = false;
            SaveCurrentLayout();
        }
    }

    public void RemoveWidget(Guid widgetId, bool saveLayout = true)
    {
        if (_activeWidgets.TryGetValue(widgetId, out var widget))
        {
            _activeWidgets.Remove(widgetId);
            widget.Closing -= Widget_Closing;
            widget.Close();
            WidgetRemoved?.Invoke(this, widgetId);
            if (saveLayout) SaveCurrentLayout();
        }
    }

    public void RemoveAllWidgets()
    {
        foreach (var id in _activeWidgets.Keys.ToList())
            RemoveWidget(id, saveLayout: false);
    }

    public void ShowAllWidgets()
    {
        foreach (var w in _activeWidgets.Values)
            w.SetUserVisibility(true);
        WidgetVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void HideAllWidgets()
    {
        foreach (var w in _activeWidgets.Values)
            w.SetUserVisibility(false);
        WidgetVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleAllWidgets()
    {
        if (_activeWidgets.Values.Any(w => w.IsVisible))
            HideAllWidgets();
        else
            ShowAllWidgets();
    }

    public void LockAllWidgets(bool lockState)
    {
        foreach (var w in _activeWidgets.Values)
            w.SetLocked(lockState);
    }

    public bool HasWidgetType(WidgetType type) =>
        _activeWidgets.Values.Any(w => w.WidgetType == type && w.IsVisible);

    public IEnumerable<WidgetBase> GetWidgetsByType(WidgetType type) =>
        _activeWidgets.Values.Where(w => w.WidgetType == type);

    public int GetWidgetCount() => _activeWidgets.Count;

    // ── Layout persistence ──────────────────────────────────────────────

    private static string LayoutFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MRT-UI", "layout.json");

    public LayoutConfig GetCurrentLayout()
    {
        var layout = new LayoutConfig
        {
            Name = "Current Layout",
            Version = 1,
            AllVisible = _activeWidgets.Values.Any(w => w.IsVisible)
        };

        foreach (var widget in _activeWidgets.Values)
            layout.Widgets.Add(widget.GetConfiguration());

        return layout;
    }

    public void LoadLayout(LayoutConfig layout)
    {
        _logger.LogInformation("Loading layout with {Count} widgets", layout.Widgets.Count);
        RemoveAllWidgets();

        foreach (var wc in layout.Widgets)
        {
            try { CreateWidget(wc.Type, wc); }
            catch (Exception ex) { _logger.LogError(ex, "Failed to create widget: {Type}", wc.Type); }
        }
    }

    public void SaveCurrentLayout()
    {
        try
        {
            var layout = GetCurrentLayout();
            var directory = Path.GetDirectoryName(LayoutFilePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            File.WriteAllText(LayoutFilePath, JsonSerializer.Serialize(layout, options));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save layout");
        }
    }

    public bool LoadSavedLayout()
    {
        try
        {
            if (!File.Exists(LayoutFilePath)) return false;

            var json = File.ReadAllText(LayoutFilePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            var layout = JsonSerializer.Deserialize<LayoutConfig>(json, options);
            if (layout == null) return false;

            LoadLayout(layout);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load saved layout");
            return false;
        }
    }
}
