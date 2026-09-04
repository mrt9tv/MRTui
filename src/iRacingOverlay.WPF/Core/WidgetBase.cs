using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
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

    /// <summary>
    /// The user's visibility preference, independent of whether iRacing is connected.
    /// This — not <see cref="UIElement.IsVisible"/> — is what gets persisted: the window
    /// is hidden whenever telemetry is disconnected, so saving the live value would
    /// mark every widget hidden any time the layout was saved outside a session.
    /// </summary>
    public bool UserWantsVisible => _userWantsVisible;

    /// <summary>
    /// Background-only opacity (0.0–1.0). Affects only the widget background, not text/content.
    /// </summary>
    public double BackgroundOpacity
    {
        get => Config.BackgroundOpacity;
        set
        {
            Config.BackgroundOpacity = Math.Clamp(value, 0.0, 1.0);
            OnBackgroundOpacityChanged(Config.BackgroundOpacity);
        }
    }

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
    /// Initialize common window properties for transparent overlay.
    /// Uses DWM composited transparency (GPU-accelerated) instead of
    /// AllowsTransparency=true which forces software rendering.
    /// </summary>
    private void InitializeWindowProperties()
    {
        // Transparent overlay settings — NO AllowsTransparency (forces software rendering)
        WindowStyle = WindowStyle.None;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;

        // Default size (will be overridden by config)
        Width = 280;
        Height = 280;
    }

    /// <summary>
    /// After the Win32 window handle is created, enable per-pixel alpha via DWM.
    /// This gives us true transparency with GPU-accelerated (hardware) rendering,
    /// avoiding the massive performance penalty of AllowsTransparency=true.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        EnableDwmTransparency();

        // The lock state is applied in the constructor, before the HWND exists,
        // so the extended style has to be re-applied now that there is a handle.
        SetClickThrough(_isLocked);
    }

    private void EnableDwmTransparency()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var hwndSource = HwndSource.FromHwnd(hwnd);
        if (hwndSource?.CompositionTarget != null)
            hwndSource.CompositionTarget.BackgroundColor = Colors.Transparent;

        // Extend glass frame into entire client area for per-pixel alpha
        var margins = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }

    #region DWM Interop

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    #endregion

    #region Click-through Interop

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    private static void SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value)
    {
        if (IntPtr.Size == 8) SetWindowLongPtr64(hWnd, nIndex, value);
        else SetWindowLong32(hWnd, nIndex, value.ToInt32());
    }

    /// <summary>
    /// Turn real (Win32) click-through on or off.
    ///
    /// <c>IsHitTestVisible = false</c> only stops WPF routing the click to a control
    /// inside the window — the window itself still claims the pixel, so a locked
    /// overlay swallowed mouse input meant for iRacing in windowed/borderless mode.
    /// WS_EX_TRANSPARENT is what actually passes the click through to what is behind.
    /// </summary>
    private void SetClickThrough(bool enabled)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return; // Applied again from OnSourceInitialized once the handle exists.

        try
        {
            long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();

            if (enabled) exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
            else exStyle &= ~(long)WS_EX_TRANSPARENT;

            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
        }
        catch (Exception ex)
        {
            Utils.AppLog.Warn($"Could not set click-through on {GetType().Name}", ex);
        }
    }

    #endregion

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
    /// Get the working area of the virtual screen (all monitors combined).
    /// Uses VirtualScreen parameters to support multi-monitor setups.
    /// </summary>
    private static Rect GetCurrentScreenBounds()
    {
        return new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
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
        }

        // Two complementary halves of click-through:
        //  - IsHitTestVisible stops WPF routing clicks to controls inside the window
        //  - WS_EX_TRANSPARENT stops the window claiming the pixel at all, so the
        //    click reaches iRacing behind it
        IsHitTestVisible = !locked;
        SetClickThrough(locked);
    }

    #region Edit mode

    private System.Windows.Shapes.Rectangle? _editOutline;
    private TextBlock? _editLabel;
    private Grid? _editLayer;

    /// <summary>
    /// Draw a labelled outline over the widget so it can be found and dragged.
    ///
    /// Unlocked widgets are invisible when their content happens to be blank — no
    /// telemetry, an empty feed — so "unlock and drag it" meant hunting for a window
    /// that renders nothing. This makes every widget visible and named while
    /// arranging them, and pairs with the click-through the lock now applies.
    /// </summary>
    public void SetEditMode(bool enabled)
    {
        if (!enabled)
        {
            if (_editLayer != null) _editLayer.Visibility = Visibility.Collapsed;
            return;
        }

        if (_editLayer == null) BuildEditOverlay();
        if (_editLayer != null) _editLayer.Visibility = Visibility.Visible;
    }

    private void BuildEditOverlay()
    {
        // Wrap the existing content so the outline sits above it without
        // disturbing whatever layout the widget already built.
        var existing = Content as UIElement;
        var root = new Grid();
        if (existing != null)
        {
            Content = null;
            root.Children.Add(existing);
        }

        _editOutline = new System.Windows.Shapes.Rectangle
        {
            Stroke = Utils.BrushCache.Get(0, 240, 240),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = Utils.BrushCache.Get(28, 0, 240, 240),
            RadiusX = 4,
            RadiusY = 4,
            IsHitTestVisible = false,
        };

        _editLabel = new TextBlock
        {
            Text = WidgetType.ToString(),
            Foreground = Utils.BrushCache.Get(0, 240, 240),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(6, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
        };

        _editLayer = new Grid { Visibility = Visibility.Collapsed };
        _editLayer.Children.Add(_editOutline);
        _editLayer.Children.Add(_editLabel);

        root.Children.Add(_editLayer);
        Content = root;
    }

    #endregion

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
        OnBackgroundOpacityChanged(Config.BackgroundOpacity);

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
        OnBackgroundOpacityChanged(Config.BackgroundOpacity);
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
    /// How many 60Hz telemetry ticks to skip between UpdateUI calls.
    /// Override in derived classes to reduce update frequency for slow-changing data.
    /// 1 = every tick (60 Hz), 2 = every other (30 Hz), 3 = 20 Hz, etc.
    /// </summary>
    protected virtual int UpdateIntervalTicks => 1;

    private int _frameCounter;

    /// <summary>
    /// 0 = no render queued, 1 = one queued. Guards against the dispatcher queue
    /// growing without limit when the UI thread falls behind the 60 Hz feed.
    /// </summary>
    private int _renderQueued;

    /// <summary>Number of telemetry frames skipped because a render was still pending.</summary>
    public long DroppedFrames { get; private set; }

    /// <summary>
    /// Handle telemetry updates (thread-safe via Dispatcher).
    ///
    /// Latest-wins: the newest frame is stored and at most ONE render is queued at
    /// a time. Previously every tick queued its own closure, so a brief UI stall
    /// built a backlog of hundreds of callbacks that then rendered stale frames —
    /// a freeze that "unfreezes" into a fast-forward. Dropping intermediate frames
    /// is always correct here: the widget only ever shows the most recent values.
    /// </summary>
    private void OnTelemetryUpdated(object? sender, TelemetryData data)
    {
        _lastTelemetryData = data;

        // Per-widget throttle: skip ticks to reduce update rate for slow-changing widgets
        if (++_frameCounter % UpdateIntervalTicks != 0) return;

        // Already have a render in flight — it will pick up this frame instead.
        if (Interlocked.CompareExchange(ref _renderQueued, 1, 0) != 0)
        {
            DroppedFrames++;
            return;
        }

        // Queue UI update asynchronously at Render priority (high but below Input)
        // so the telemetry thread never blocks on a widget.
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            Interlocked.Exchange(ref _renderQueued, 0);

            var latest = _lastTelemetryData;
            if (latest == null) return;

            try
            {
                UpdateUI(latest);
            }
            catch (Exception ex)
            {
                // One widget throwing must not take down the render pass for the rest.
                Utils.AppLog.Error($"{GetType().Name}.UpdateUI failed", ex);
            }
        });
    }

    /// <summary>
    /// Handle connection status changes.
    /// Non-blocking: this fires on the SDK's callback thread, and a blocking
    /// Invoke here stalls telemetry for every widget whenever the UI thread is busy.
    /// </summary>
    private void OnStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
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
    /// Called before layout save to persist widget-specific settings into Config.Settings.
    /// Override in derived classes to save custom settings.
    /// </summary>
    protected virtual void SaveWidgetSettings() { }

    /// <summary>
    /// Called when BackgroundOpacity changes. Override in derived classes
    /// to adjust the background panel/brush alpha independently of content.
    /// </summary>
    protected virtual void OnBackgroundOpacityChanged(double opacity) { }

    /// <summary>
    /// Get current widget configuration (for saving)
    /// </summary>
    public virtual WidgetConfig GetConfiguration()
    {
        // Persist widget-specific settings first
        SaveWidgetSettings();

        // Update config with current window state
        Config.X = Left;
        Config.Y = Top;
        Config.Width = Width;
        Config.Height = Height;
        Config.Opacity = Opacity;
        // Persist the user's preference, not the live window state, which is
        // false whenever iRacing is disconnected.
        Config.IsVisible = _userWantsVisible;

        return Config;
    }

    /// <summary>
    /// Update configuration and apply changes
    /// </summary>
    public virtual void UpdateConfiguration(WidgetConfig config)
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
