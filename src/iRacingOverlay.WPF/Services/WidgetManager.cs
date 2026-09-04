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

    /// <summary>Widget types enabled in this build. Release restricts to shipped widgets only.</summary>
    public static readonly HashSet<WidgetType> SupportedWidgetTypes =
#if DEBUG
        new(Enum.GetValues<WidgetType>());
#else
        new() { WidgetType.MRTOne, WidgetType.ProximityFeed };
#endif

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
        
        _widgetFactories[WidgetType.FuelCalculator] = (service, config) =>
            new Widgets.FuelWidget.FuelWidget(service, config);
        
        _widgetFactories[WidgetType.Relative] = (service, config) =>
            new Widgets.RelativeWidget.RelativeWidget(service, config);
        
        _widgetFactories[WidgetType.ProximityFeed] = (service, config) =>
            new Widgets.ProximityFeedWidget.ProximityFeedWidget(service, config);

        _widgetFactories[WidgetType.Standings] = (service, config) =>
            new Widgets.StandingsWidget.StandingsWidget(service, config);
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

        // Match the edit outline to the current lock state so a widget created
        // while the user is arranging things shows up like the rest.
        widget.SetEditMode(!Models.AppSettings.Instance.LockWindows);

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

    /// <summary>Set of widget IDs that were visible before the last HideAllWidgets call.
    /// Used by ToggleAllWidgets to restore only previously-visible widgets.</summary>
    private readonly HashSet<Guid> _visibleBeforeHide = new();

    public void ShowAllWidgets()
    {
        // Only restore supported widgets that were visible before the hide
        foreach (var w in _activeWidgets.Values)
        {
            if (!SupportedWidgetTypes.Contains(w.WidgetType)) continue;
            if (_visibleBeforeHide.Count == 0 || _visibleBeforeHide.Contains(w.WidgetId))
                w.SetUserVisibility(true);
        }
        WidgetVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void HideAllWidgets()
    {
        // Remember which supported widgets are currently visible before hiding
        _visibleBeforeHide.Clear();
        foreach (var w in _activeWidgets.Values)
        {
            if (!SupportedWidgetTypes.Contains(w.WidgetType)) continue;
            if (w.IsVisible)
                _visibleBeforeHide.Add(w.WidgetId);
        }

        foreach (var w in _activeWidgets.Values)
        {
            if (!SupportedWidgetTypes.Contains(w.WidgetType)) continue;
            w.SetUserVisibility(false);
        }
        WidgetVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleAllWidgets()
    {
        if (_activeWidgets.Values.Any(w => SupportedWidgetTypes.Contains(w.WidgetType) && w.IsVisible))
            HideAllWidgets();
        else
            ShowAllWidgets();
    }

    public void LockAllWidgets(bool lockState)
    {
        foreach (var w in _activeWidgets.Values)
            w.SetLocked(lockState);

        // Unlocked means "the user is arranging things" — show the edit outlines so
        // widgets with no visible content can still be found and dragged.
        SetEditMode(!lockState);
    }

    /// <summary>Show or hide the labelled drag outline on every widget.</summary>
    public void SetEditMode(bool enabled)
    {
        foreach (var w in _activeWidgets.Values)
            w.SetEditMode(enabled);
    }

    public bool HasWidgetType(WidgetType type) =>
        _activeWidgets.Values.Any(w => w.WidgetType == type);

    public IEnumerable<WidgetBase> GetWidgetsByType(WidgetType type) =>
        _activeWidgets.Values.Where(w => w.WidgetType == type);

    public int GetWidgetCount() => _activeWidgets.Count;

    /// <summary>
    /// Apply a session preset: show/hide (or create) widgets by type.
    /// </summary>
    public void ApplySessionPreset(Models.SessionPreset preset)
    {
        foreach (var (widgetType, shouldBeVisible) in preset.WidgetVisibility)
        {
            if (!SupportedWidgetTypes.Contains(widgetType)) continue;

            bool exists = HasWidgetType(widgetType);
            if (shouldBeVisible)
            {
                if (!exists)
                    CreateWidget(widgetType);
                else
                    foreach (var w in GetWidgetsByType(widgetType))
                        w.SetUserVisibility(true);
            }
            else if (exists)
            {
                foreach (var w in GetWidgetsByType(widgetType))
                    w.SetUserVisibility(false);
            }
        }
        WidgetVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Layout persistence ──────────────────────────────────────────────

    private static string LayoutFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MRT-UI", "layout.json");

    /// <summary>Schema version this build writes and understands.</summary>
    public const int CurrentLayoutVersion = 1;

    /// <summary>
    /// Bring a loaded layout up to <see cref="CurrentLayoutVersion"/>.
    /// The Version field has always been written but never read; this is the hook
    /// to add migrations to before the first breaking change, not after.
    /// Returns false if the layout is from a newer build and cannot be used.
    /// </summary>
    private bool MigrateLayout(LayoutConfig layout)
    {
        if (layout.Version > CurrentLayoutVersion)
        {
            _logger.LogWarning(
                "layout.json is version {Found}, this build understands {Known} — ignoring it so a newer build's layout is not overwritten",
                layout.Version, CurrentLayoutVersion);
            return false;
        }

        // Migrations run in order, each bumping Version, e.g.:
        //   if (layout.Version == 1) { ...transform...; layout.Version = 2; }

        return true;
    }

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
            if (!SupportedWidgetTypes.Contains(wc.Type))
            {
                _logger.LogInformation("Skipping unsupported widget type from layout: {Type}", wc.Type);
                continue;
            }
            try { CreateWidget(wc.Type, wc); }
            catch (Exception ex) { _logger.LogError(ex, "Failed to create widget: {Type}", wc.Type); }
        }
    }

    private static readonly JsonSerializerOptions _layoutJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    /// <summary>
    /// Coalesces layout writes. Slider drags used to serialise every active widget
    /// and hit the disk on each value change; now a drag produces one write.
    /// </summary>
    private Utils.Debouncer? _layoutWriter;

    /// <summary>
    /// Request a layout save. The write is deferred and coalesced, and always
    /// snapshots widget state on the UI thread before handing it to the writer.
    /// </summary>
    public void SaveCurrentLayout()
    {
        _layoutWriter ??= new Utils.Debouncer(WritePendingLayout, delayMs: 700);

        // Snapshot now, on the caller's (UI) thread — WidgetBase.GetConfiguration
        // reads Left/Top/Width/Height, which are UI-thread-affine.
        try
        {
            _pendingLayout = GetCurrentLayout();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to snapshot layout");
            return;
        }

        _layoutWriter.Trigger();
    }

    private LayoutConfig? _pendingLayout;

    private void WritePendingLayout()
    {
        var layout = _pendingLayout;
        if (layout == null) return;

        try
        {
            Utils.AtomicFile.WriteAllText(
                LayoutFilePath,
                JsonSerializer.Serialize(layout, _layoutJsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save layout");
        }
    }

    /// <summary>Write any pending layout immediately. Call on shutdown.</summary>
    public void FlushLayout() => _layoutWriter?.Flush();

    public bool LoadSavedLayout()
    {
        try
        {
            var json = Utils.AtomicFile.ReadAllTextWithFallback(LayoutFilePath);
            if (string.IsNullOrWhiteSpace(json)) return false;

            var layout = JsonSerializer.Deserialize<LayoutConfig>(json, _layoutJsonOptions);
            if (layout == null) return false;

            if (!MigrateLayout(layout)) return false;

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
