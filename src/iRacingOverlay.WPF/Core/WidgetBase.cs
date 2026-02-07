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

    /// <summary>
    /// Whether the widget should be visible based on user configuration
    /// (separate from connection state)
    /// </summary>
    private bool _userWantsVisible = true;

    protected WidgetBase(ITelemetryService telemetryService, WidgetConfig? config = null)
    {
        _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));

        // Use provided config or create new one
        Config = config ?? new WidgetConfig { Type = WidgetType };
        WidgetId = Config.Id;

        // Track user's visibility preference
        _userWantsVisible = Config.IsVisible;

        // Set up transparent overlay window
        InitializeWindowProperties();

        // Subscribe to telemetry updates
        SubscribeToTelemetry();

        // Enable dragging
        EnableDragging();

        // Apply saved position and size (but NOT visibility yet - wait for connection)
        ApplyConfigurationWithoutVisibility();

        // Apply visibility based on current connection state
        UpdateVisibilityBasedOnConnection(_telemetryService.Status);

        // Apply current lock state from global settings
        var appSettings = Models.AppSettings.Instance;
        SetLocked(appSettings.LockWindows);
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
        ResizeMode = ResizeMode.NoResize;

        // Default size (will be overridden by config)
        Width = 280;
        Height = 280;
    }

    #region Screen Centering

    /// <summary>
    /// Center the widget horizontally on the current screen
    /// </summary>
    public void CenterHorizontally()
    {
        var screen = GetCurrentScreenBounds();
        Left = screen.Left + (screen.Width - ActualWidth) / 2;
        Config.X = Left;
    }

    /// <summary>
    /// Center the widget vertically on the current screen
    /// </summary>
    public void CenterVertically()
    {
        var screen = GetCurrentScreenBounds();
        Top = screen.Top + (screen.Height - ActualHeight) / 2;
        Config.Y = Top;
    }

    /// <summary>
    /// Center the widget both horizontally and vertically
    /// </summary>
    public void CenterBoth()
    {
        CenterHorizontally();
        CenterVertically();
    }

    /// <summary>
    /// Get the working area of the screen the widget is currently on
    /// </summary>
    private Rect GetCurrentScreenBounds()
    {
        // Use WPF SystemParameters for screen dimensions (no WinForms dependency)
        // This returns the primary screen work area (excludes taskbar)
        double workAreaWidth = SystemParameters.WorkArea.Width;
        double workAreaHeight = SystemParameters.WorkArea.Height;
        double workAreaLeft = SystemParameters.WorkArea.Left;
        double workAreaTop = SystemParameters.WorkArea.Top;

        return new Rect(workAreaLeft, workAreaTop, workAreaWidth, workAreaHeight);
    }

    #endregion

    /// <summary>
    /// Enable window dragging via mouse with snap-to-grid support
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
    /// Set widget size from the MRT UI overlay (proportional — width = height for square widgets)
    /// </summary>
    public virtual void SetSize(double size)
    {
        Width = size;
        Height = size;
        Config.Width = size;
        Config.Height = size;
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

        _userWantsVisible = Config.IsVisible;

        // Only show if user wants visible AND telemetry is connected
        UpdateVisibilityBasedOnConnection(_telemetryService.Status);
    }

    /// <summary>
    /// Apply configuration without changing visibility
    /// Used during initialization to avoid showing before connection check
    /// </summary>
    private void ApplyConfigurationWithoutVisibility()
    {
        Left = Config.X;
        Top = Config.Y;
        Width = Config.Width;
        Height = Config.Height;
        Opacity = Config.Opacity;
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
        // Update visibility based on connection state
        UpdateVisibilityBasedOnConnection(status);

        // Derived classes can override to show additional connection indicators
    }

    /// <summary>
    /// Update widget visibility based on connection state
    /// Only show widget if user wants it visible AND telemetry is connected
    /// </summary>
    private void UpdateVisibilityBasedOnConnection(ConnectionStatus status)
    {
        bool shouldBeVisible = _userWantsVisible && status == ConnectionStatus.Connected;

        if (shouldBeVisible && !IsVisible)
        {
            Show();
        }
        else if (!shouldBeVisible && IsVisible)
        {
            Hide();
        }
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
    /// Toggle widget visibility (user preference)
    /// Actual visibility also depends on connection state
    /// </summary>
    public void ToggleVisibility()
    {
        _userWantsVisible = !_userWantsVisible;
        Config.IsVisible = _userWantsVisible;

        // Update visibility based on both user preference and connection state
        UpdateVisibilityBasedOnConnection(_telemetryService.Status);
    }

    /// <summary>
    /// Set user visibility preference
    /// Actual visibility also depends on connection state
    /// </summary>
    public void SetUserVisibility(bool visible)
    {
        _userWantsVisible = visible;
        Config.IsVisible = visible;

        // Update visibility based on both user preference and connection state
        UpdateVisibilityBasedOnConnection(_telemetryService.Status);
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
