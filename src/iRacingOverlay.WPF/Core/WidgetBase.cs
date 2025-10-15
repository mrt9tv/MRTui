using System;
using System.Windows;
using System.Windows.Input;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Core;

/// <summary>
/// Abstract base class for all overlay widgets
/// Provides common functionality: transparency, dragging, telemetry integration
/// </summary>
public abstract class WidgetBase : Window
{
    protected readonly ITelemetryService _telemetryService;
    protected TelemetryData? _lastTelemetryData;

    /// <summary>
    /// Unique identifier for this widget instance
    /// </summary>
    public Guid WidgetId { get; }

    /// <summary>
    /// Type of this widget
    /// </summary>
    public abstract WidgetType WidgetType { get; }

    /// <summary>
    /// Configuration for this widget instance
    /// </summary>
    public WidgetConfig Config { get; protected set; }

    /// <summary>
    /// Whether the widget is locked (prevents dragging, enables click-through)
    /// </summary>
    private bool _isLocked = false;

    protected WidgetBase(ITelemetryService telemetryService, WidgetConfig? config = null)
    {
        _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        
        // Use provided config or create new one
        Config = config ?? new WidgetConfig { Type = WidgetType };
        WidgetId = Config.Id;

        // Set up transparent overlay window
        InitializeWindowProperties();

        // Subscribe to telemetry updates
        SubscribeToTelemetry();

        // Enable dragging
        EnableDragging();

        // Apply saved position and size
        ApplyConfiguration();
    }

    /// <summary>
    /// Initialize common window properties for transparent overlay
    /// </summary>
    private void InitializeWindowProperties()
    {
        // Transparent overlay settings
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize; // Can be overridden by derived classes

        // Default size (will be overridden by config)
        Width = 300;
        Height = 200;
    }

    /// <summary>
    /// Enable window dragging via mouse
    /// </summary>
    private void EnableDragging()
    {
        MouseLeftButtonDown += (sender, e) =>
        {
            // Don't allow dragging if widget is locked
            if (_isLocked) return;
            
            try
            {
                DragMove();
                
                // Update config with new position after drag
                Config.X = Left;
                Config.Y = Top;
            }
            catch (InvalidOperationException)
            {
                // DragMove can only be called when mouse button is down
                // Swallow this exception
            }
        };
        
        // Subscribe to LocationChanged to update config when window moves
        LocationChanged += (sender, e) =>
        {
            Config.X = Left;
            Config.Y = Top;
        };
    }

    /// <summary>
    /// Lock or unlock this widget
    /// When locked: prevents dragging and enables click-through
    /// </summary>
    public void SetLocked(bool locked)
    {
        _isLocked = locked;
        
        // Save current position when locking
        if (locked)
        {
            Config.X = Left;
            Config.Y = Top;
            
            // Enable click-through by making window transparent to hit testing
            IsHitTestVisible = false;
        }
        else
        {
            // Disable click-through
            IsHitTestVisible = true;
        }
    }

    /// <summary>
    /// Apply configuration (position, size, visibility)
    /// </summary>
    private void ApplyConfiguration()
    {
        Left = Config.X;
        Top = Config.Y;
        Width = Config.Width;
        Height = Config.Height;
        Opacity = Config.Opacity;
        
        if (Config.IsVisible)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    /// <summary>
    /// Subscribe to telemetry service updates
    /// </summary>
    private void SubscribeToTelemetry()
    {
        _telemetryService.TelemetryUpdated += OnTelemetryUpdated;
        _telemetryService.StatusChanged += OnStatusChanged;
    }

    /// <summary>
    /// Handle telemetry updates (thread-safe via Dispatcher)
    /// </summary>
    private void OnTelemetryUpdated(object? sender, TelemetryData data)
    {
        _lastTelemetryData = data;

        // Update UI on UI thread
        Dispatcher.Invoke(() =>
        {
            UpdateUI(data);
        });
    }

    /// <summary>
    /// Handle connection status changes
    /// </summary>
    private void OnStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            OnConnectionStatusChanged(e.Status);
        });
    }

    /// <summary>
    /// Update widget UI with new telemetry data
    /// Called on UI thread - override in derived classes
    /// </summary>
    /// <param name="data">Latest telemetry data</param>
    protected abstract void UpdateUI(TelemetryData data);

    /// <summary>
    /// Handle connection status changes
    /// Called on UI thread - override in derived classes if needed
    /// </summary>
    /// <param name="status">New connection status</param>
    protected virtual void OnConnectionStatusChanged(ConnectionStatus status)
    {
        // Default: do nothing
        // Derived classes can override to show connection indicators
    }

    /// <summary>
    /// Get current widget configuration (for saving)
    /// </summary>
    public WidgetConfig GetConfiguration()
    {
        // Update config with current window state
        Config.X = Left;
        Config.Y = Top;
        Config.Width = Width;
        Config.Height = Height;
        Config.Opacity = Opacity;
        Config.IsVisible = IsVisible;

        return Config;
    }

    /// <summary>
    /// Update configuration and apply changes
    /// </summary>
    public void UpdateConfiguration(WidgetConfig config)
    {
        Config = config;
        ApplyConfiguration();
    }

    /// <summary>
    /// Toggle widget visibility
    /// </summary>
    public void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
            Config.IsVisible = false;
        }
        else
        {
            Show();
            Config.IsVisible = true;
        }
    }

    /// <summary>
    /// Clean up subscriptions
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        _telemetryService.TelemetryUpdated -= OnTelemetryUpdated;
        _telemetryService.StatusChanged -= OnStatusChanged;
        base.OnClosed(e);
    }
}
