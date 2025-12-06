using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using iRacingOverlay.Core.Services;
using iRacingOverlay.Core.Services.Tire;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Widgets.RaceStrategy;
using Microsoft.Extensions.Logging;

namespace iRacingOverlay.WPF.Services;

/// <summary>
/// Manages widget lifecycle: creation, destruction, layout save/load
/// </summary>
public class WidgetManager
{
    private readonly ITelemetryService _telemetryService;
    private readonly FuelCalculatorService _fuelCalculatorService;
    private readonly TireStrategyService _tireStrategyService;
    private readonly ILogger<WidgetManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Guid, WidgetBase> _activeWidgets = new();
    private readonly Dictionary<WidgetType, Func<ITelemetryService, WidgetConfig, WidgetBase>> _widgetFactories = new();
    
    // Special handling for standalone windows (non-overlay widgets)
    private RaceStrategyWidget? _raceStrategyWidget;

    public IReadOnlyDictionary<Guid, WidgetBase> ActiveWidgets => _activeWidgets;

    public event EventHandler<WidgetBase>? WidgetCreated;
    public event EventHandler<Guid>? WidgetRemoved;
    public event EventHandler? WidgetVisibilityChanged;

    public WidgetManager(ITelemetryService telemetryService, FuelCalculatorService fuelCalculatorService, TireStrategyService tireStrategyService, ILogger<WidgetManager> logger, IServiceProvider serviceProvider)
    {
        _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        _fuelCalculatorService = fuelCalculatorService ?? throw new ArgumentNullException(nameof(fuelCalculatorService));
        _tireStrategyService = tireStrategyService ?? throw new ArgumentNullException(nameof(tireStrategyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        RegisterWidgetFactories();
    }

    /// <summary>
    /// Register factory methods for each widget type
    /// </summary>
    private void RegisterWidgetFactories()
    {
        _widgetFactories[WidgetType.MRTOne] = (service, config) =>
            new Widgets.MRTOneWidget.MRTOneWidget(service, config);

        _widgetFactories[WidgetType.Fuel] = (service, config) =>
            new Widgets.FuelWidgets.FuelWidget(service, _fuelCalculatorService, config);

        // Future widgets: TelemetryTable, RPMGauge, Temperature, LapTimes, Gear, Inputs, Tires
        // Register additional widget factories here as new widgets are implemented
    }

    /// <summary>
    /// Create a new widget instance
    /// </summary>
    public WidgetBase CreateWidget(WidgetType type, WidgetConfig? config = null)
    {
        _logger.LogInformation("Creating widget of type: {Type}", type);

        // Special handling for RaceStrategy (standalone window, not WidgetBase)
        if (type == WidgetType.RaceStrategy)
        {
            ShowRaceStrategyWidget();
            
            // Return null but don't crash - caller should check for RaceStrategy type
            return null!;
        }

        if (!_widgetFactories.TryGetValue(type, out var factory))
        {
            throw new NotSupportedException($"Widget type '{type}' is not yet implemented");
        }

        // Create config if not provided
        config ??= new WidgetConfig { Type = type };

        // Create widget using factory
        // Widget will handle its own visibility based on connection state
        var widget = factory(_telemetryService, config);

        // Track the widget
        _activeWidgets[widget.WidgetId] = widget;

        // Hook up closing event to hide widget instead of removing it
        // This preserves the widget configuration when user clicks X
        widget.Closing += Widget_Closing;

        _logger.LogInformation("Widget created: {Type} (ID: {Id})", type, widget.WidgetId);

        WidgetCreated?.Invoke(this, widget);

        SaveCurrentLayout(); // Persist layout after widget creation

        return widget;
    }
    
    /// <summary>
    /// Show Race Strategy Widget (Phase 10)
    /// </summary>
    private void ShowRaceStrategyWidget()
    {
        try
        {
            // Ensure we're on the UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (_raceStrategyWidget == null)
                {
                    _logger.LogInformation("Creating new RaceStrategyWidget...");
                    // Get logger from service provider
                    var raceStrategyLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                        .GetRequiredService<ILogger<RaceStrategyWidget>>(_serviceProvider);
                    _raceStrategyWidget = new RaceStrategyWidget(_fuelCalculatorService, _tireStrategyService, raceStrategyLogger, _telemetryService);
                    _raceStrategyWidget.Closed += (s, e) =>
                    {
                        _logger.LogInformation("RaceStrategyWidget closed");
                        _raceStrategyWidget = null;
                        WidgetVisibilityChanged?.Invoke(this, EventArgs.Empty);
                    };
                }
                
                _logger.LogInformation("Showing RaceStrategyWidget...");
                _raceStrategyWidget.Show();
                _raceStrategyWidget.Activate();
                
                _logger.LogInformation("Race Strategy Widget shown successfully");
            });
            
            WidgetVisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Race Strategy Widget in invalid state: {Message}", ex.Message);
            throw;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid configuration for Race Strategy Widget: {Message}", ex.Message);
            throw;
        }
    }
    
    /// <summary>
    /// Hide Race Strategy Widget (Phase 10)
    /// </summary>
    public void HideRaceStrategyWidget()
    {
        if (_raceStrategyWidget != null)
        {
            _raceStrategyWidget.Close();
            _raceStrategyWidget = null;
            
            _logger.LogInformation("Race Strategy Widget hidden");
            WidgetVisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Handle widget closing - hide instead of close to preserve configuration
    /// </summary>
    private void Widget_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (sender is WidgetBase widget)
        {
            e.Cancel = true; // Prevent actual close
            widget.Hide(); // Just hide instead
            widget.Config.IsVisible = false; // Track hidden state
            SaveCurrentLayout(); // Save the hidden state
            _logger.LogInformation("Widget hidden: {Type} (ID: {Id})", widget.WidgetType, widget.WidgetId);
        }
    }

    /// <summary>
    /// Remove and close a widget
    /// </summary>
    /// <param name="widgetId">Widget ID to remove</param>
    /// <param name="saveLayout">Whether to save layout after removal (default true)</param>
    public void RemoveWidget(Guid widgetId, bool saveLayout = true)
    {
        if (_activeWidgets.TryGetValue(widgetId, out var widget))
        {
            _logger.LogInformation("Removing widget: {Type} (ID: {Id})", widget.WidgetType, widgetId);

            _activeWidgets.Remove(widgetId);
            
            // Unhook closing event so widget can actually close
            widget.Closing -= Widget_Closing;
            widget.Close(); // Now it will actually close

            WidgetRemoved?.Invoke(this, widgetId);
            
            // Only save if explicitly requested (skip during bulk operations like RemoveAllWidgets)
            if (saveLayout)
            {
                SaveCurrentLayout();
            }
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
            RemoveWidget(id, saveLayout: false); // Don't save on each removal during bulk operation
        }
    }

    /// <summary>
    /// Show all widgets (sets user preference, actual visibility depends on connection)
    /// </summary>
    public void ShowAllWidgets()
    {
        _logger.LogInformation("Showing all widgets");

        foreach (var widget in _activeWidgets.Values)
        {
            // Set user preference to visible
            // Widget will only actually show if telemetry is connected
            widget.SetUserVisibility(true);
        }

        // Notify listeners that visibility changed
        WidgetVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Hide all widgets (sets user preference to hidden)
    /// </summary>
    public void HideAllWidgets()
    {
        _logger.LogInformation("Hiding all widgets");

        foreach (var widget in _activeWidgets.Values)
        {
            widget.SetUserVisibility(false);
        }

        // Notify listeners that visibility changed
        WidgetVisibilityChanged?.Invoke(this, EventArgs.Empty);
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
        _logger.LogInformation("GetCurrentLayout called. Active widgets in dictionary: {Count}", _activeWidgets.Count);
        
        var layout = new LayoutConfig
        {
            Name = "Current Layout",
            Version = 1,
            AllVisible = _activeWidgets.Values.Any(w => w.IsVisible)
        };

        foreach (var widget in _activeWidgets.Values)
        {
            _logger.LogInformation("Getting config for widget: Type={Type}, ID={Id}", widget.WidgetType, widget.WidgetId);
            var config = widget.GetConfiguration();
            _logger.LogInformation("Widget config: X={X}, Y={Y}, Width={Width}, Height={Height}, Settings count={Count}", 
                config.X, config.Y, config.Width, config.Height, config.Settings.Count);
            layout.Widgets.Add(config);
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
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MRT-UI", "debug.log");
                var log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] Widget Type={widgetConfig.Type}, Settings keys={string.Join(",", widgetConfig.Settings.Keys)}";
                File.AppendAllText(logPath, log);
                
                _logger.LogInformation("Loading widget config: Type={Type}, Settings keys={Keys}", 
                    widgetConfig.Type, string.Join(",", widgetConfig.Settings.Keys));
                
                if (widgetConfig.Settings.ContainsKey("mrtone"))
                {
                    var mrtoneObj = widgetConfig.Settings["mrtone"];
                    log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] ✓ Found 'mrtone' settings, type: {mrtoneObj?.GetType().Name}";
                    File.AppendAllText(logPath, log);
                    _logger.LogInformation("mrtone settings object type: {Type}", mrtoneObj?.GetType().Name);
                }
                
                CreateWidget(widgetConfig.Type, widgetConfig);
            }
            catch (JsonException ex)
            {
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MRT-UI", "debug.log");
                var log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] ✗ JSON Error: {ex.Message}";
                File.AppendAllText(logPath, log);
                _logger.LogError(ex, "Failed to create widget - invalid JSON settings: {Type}", widgetConfig.Type);
            }
            catch (ArgumentException ex)
            {
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MRT-UI", "debug.log");
                var log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] ✗ Argument Error: {ex.Message}";
                File.AppendAllText(logPath, log);
                _logger.LogError(ex, "Failed to create widget - invalid arguments: {Type}", widgetConfig.Type);
            }
            catch (InvalidOperationException ex)
            {
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MRT-UI", "debug.log");
                var log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] ✗ State Error: {ex.Message}";
                File.AppendAllText(logPath, log);
                _logger.LogError(ex, "Failed to create widget - invalid state: {Type}", widgetConfig.Type);
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
        // Special handling for RaceStrategy
        if (type == WidgetType.RaceStrategy)
        {
            return _raceStrategyWidget != null && _raceStrategyWidget.IsVisible;
        }
        
        return _activeWidgets.Values.Any(w => w.WidgetType == type && w.IsVisible);
    }

    /// <summary>
    /// Get all widgets of a specific type
    /// </summary>
    public IEnumerable<WidgetBase> GetWidgetsByType(WidgetType type)
    {
        // Special handling for RaceStrategy (returns empty since it's not a WidgetBase)
        if (type == WidgetType.RaceStrategy)
        {
            return Enumerable.Empty<WidgetBase>();
        }
        
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

    #region Layout Persistence

    private static string LayoutFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MRT-UI",
            "layout.json");

    /// <summary>
    /// Save the current layout to disk
    /// </summary>
    public void SaveCurrentLayout()
    {
        try
        {
            _logger.LogInformation("SaveCurrentLayout called. Active widgets count: {Count}", _activeWidgets.Count);
            
            var layout = GetCurrentLayout();
            
            _logger.LogInformation("Layout captured with {Count} widgets", layout.Widgets.Count);
            
            var directory = Path.GetDirectoryName(LayoutFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                // This is critical for serializing Dictionary<string, object> with complex types
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            var json = JsonSerializer.Serialize(layout, options);
            
            _logger.LogInformation("Serialized JSON length: {Length}", json.Length);
            
            File.WriteAllText(LayoutFilePath, json);
            
            _logger.LogInformation("Layout saved to: {Path}", LayoutFilePath);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to save layout (I/O error): {Message}", ex.Message);
            Debug.WriteLine($"Failed to save layout (I/O error): {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to save layout (JSON serialization error): {Message}", ex.Message);
            Debug.WriteLine($"Failed to save layout (JSON error): {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Failed to save layout (access denied): {Path}", LayoutFilePath);
            Debug.WriteLine($"Failed to save layout (access denied): {ex.Message}");
        }
    }

    /// <summary>
    /// Load layout from disk if it exists
    /// </summary>
    /// <returns>True if layout was loaded, false otherwise</returns>
    public bool LoadSavedLayout()
    {
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MRT-UI", "debug.log");
        
        try
        {
            var log = $"\n\n[{DateTime.Now:HH:mm:ss}] ========== LOADING LAYOUT ==========";
            File.AppendAllText(logPath, log);
            
            log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] Checking for: {LayoutFilePath}";
            File.AppendAllText(logPath, log);
            
            _logger.LogInformation("LoadSavedLayout called. Checking for file: {Path}", LayoutFilePath);
            
            if (!File.Exists(LayoutFilePath))
            {
                log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] ✗ File does not exist";
                File.AppendAllText(logPath, log);
                _logger.LogInformation("Layout file does not exist");
                return false;
            }

            var json = File.ReadAllText(LayoutFilePath);
            log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] ✓ File loaded, length: {json.Length}";
            File.AppendAllText(logPath, log);
            _logger.LogInformation("Layout file loaded. JSON length: {Length}", json.Length);
            
            // CRITICAL: Use same options as save - including JsonStringEnumConverter
            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            var layout = JsonSerializer.Deserialize<LayoutConfig>(json, options);

            if (layout == null)
            {
                log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] ✗ Failed to deserialize";
                File.AppendAllText(logPath, log);
                _logger.LogWarning("Failed to deserialize layout");
                return false;
            }

            log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] ✓ Deserialized {layout.Widgets.Count} widgets";
            File.AppendAllText(logPath, log);
            _logger.LogInformation("Layout deserialized. Widgets to load: {Count}", layout.Widgets.Count);
            LoadLayout(layout);
            log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] ✓ Layout loaded successfully\n";
            File.AppendAllText(logPath, log);
            _logger.LogInformation("Layout loaded successfully");
            return true;
        }
        catch (FileNotFoundException ex)
        {
            var log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] ✗ File not found (first run): {ex.Message}";
            File.AppendAllText(logPath, log);
            _logger.LogInformation("Layout file not found (first run), starting fresh");
            return false;
        }
        catch (JsonException ex)
        {
            var log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] ✗ JSON Error: {ex.Message}\n{ex.StackTrace}";
            File.AppendAllText(logPath, log);
            _logger.LogError(ex, "Failed to load layout (corrupt JSON): {Message}", ex.Message);
            return false;
        }
        catch (IOException ex)
        {
            var log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] ✗ I/O Error: {ex.Message}";
            File.AppendAllText(logPath, log);
            _logger.LogError(ex, "Failed to load layout (I/O error): {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Get saved configuration for a widget type from the layout file
    /// </summary>
    /// <returns>Saved config if found, null otherwise</returns>
    public WidgetConfig? GetSavedWidgetConfig(WidgetType type)
    {
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MRT-UI", "debug.log");
        try
        {
            var log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] GetSavedWidgetConfig: Looking for type {type}";
            File.AppendAllText(logPath, log);
            
            if (!File.Exists(LayoutFilePath))
            {
                log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] GetSavedWidgetConfig: Layout file does not exist";
                File.AppendAllText(logPath, log);
                return null;
            }

            var json = File.ReadAllText(LayoutFilePath);
            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            var layout = JsonSerializer.Deserialize<LayoutConfig>(json, options);

            if (layout == null)
            {
                log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] GetSavedWidgetConfig: Failed to deserialize layout";
                File.AppendAllText(logPath, log);
                return null;
            }

            // Find the first widget config matching the requested type
            var config = layout.Widgets.FirstOrDefault(w => w.Type == type);
            
            if (config != null)
            {
                log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] GetSavedWidgetConfig: ✓ Found config, Settings keys: {string.Join(",", config.Settings.Keys)}";
                File.AppendAllText(logPath, log);
                
                if (config.Settings.ContainsKey("mrtone"))
                {
                    var mrtoneObj = config.Settings["mrtone"];
                    log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] GetSavedWidgetConfig: ✓ Has 'mrtone' settings, type: {mrtoneObj?.GetType().Name}";
                    File.AppendAllText(logPath, log);
                }
            }
            else
            {
                log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] GetSavedWidgetConfig: ✗ No config found for type {type}";
                File.AppendAllText(logPath, log);
            }
            
            return config;
        }
        catch (Exception ex)
        {
            var log = $"\n[{DateTime.Now:HH:mm:ss}] [WidgetManager] GetSavedWidgetConfig: ✗ Exception: {ex.Message}";
            File.AppendAllText(logPath, log);
            _logger.LogError(ex, "Failed to get saved widget config for type: {Type}", type);
            return null;
        }
    }

    #endregion
}
