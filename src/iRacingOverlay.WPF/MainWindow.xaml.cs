using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Services;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Utils;
using MRTOne = iRacingOverlay.WPF.Widgets.MRTOneWidget.MRTOneWidget;

namespace iRacingOverlay.WPF;

public partial class MainWindow : Window
{
    private readonly WidgetManager _widgetManager;
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<MainWindow> _logger;
    private readonly DispatcherTimer _updateRateTimer;
    private GlobalHotkey? _toggleLockHotkey;
    private GlobalHotkey? _toggleVisibilityHotkey;

    /// <summary>Fuel alert settings (shared across widgets)</summary>
    private readonly FuelAlertSettings _fuelAlertSettings = FuelAlertSettings.Default;

    /// <summary>Suppress combo/slider events while syncing UI to settings</summary>
    private bool _suppressControlEvents = true; // Start suppressed — XAML init fires events

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();

        _widgetManager = services.GetRequiredService<WidgetManager>();
        _telemetryService = services.GetRequiredService<ITelemetryService>();
        _logger = services.GetRequiredService<ILogger<MainWindow>>();

        _telemetryService.StatusChanged += OnTelemetryStatusChanged;
        Closing += MainWindow_Closing;
        LocationChanged += MainWindow_LocationChanged;
        SizeChanged += MainWindow_SizeChanged;
        StateChanged += MainWindow_StateChanged;
        Loaded += MainWindow_Loaded;

        // Update rate display timer
        _updateRateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _updateRateTimer.Tick += (_, _) => UpdateUpdateRateDisplay();
        _updateRateTimer.Start();

        // Apply saved window settings and load widget layout
        ApplyWindowSettings();
        _widgetManager.LoadSavedLayout();

        // Populate UI controls
        PopulateWidgetSelector();
        PopulateFieldCombos();

        // Initialize status
        UpdateConnectionStatus(_telemetryService.Status);
        TitleVersionText.Text = VersionInfo.DisplayVersion;
        UpdateHotkeysDisplay();

        // All event-triggering properties are now set — allow handlers to fire
        _suppressControlEvents = false;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RegisterGlobalHotkeys();
        // Sync panel to widget state (if widget was restored from saved layout)
        SyncPanelToActiveWidget();
    }

    // ── Widget selector ─────────────────────────────────────────────────

    /// <summary>
    /// Display item for the widget type selector combo box.
    /// </summary>
    private record WidgetTypeItem(string DisplayName, WidgetType Type);

    private void PopulateWidgetSelector()
    {
        // Register all known widget types (add new ones here as they're created)
        var items = new List<WidgetTypeItem>
        {
            new("MRT One", WidgetType.MRTOne),
            new("Turn Display", WidgetType.TurnDisplay),
            // Future: new("Fuel Monitor", WidgetType.FuelMonitor),
            // Future: new("Timing Board", WidgetType.TimingBoard),
        };

        CboActiveWidget.ItemsSource = items;
        CboActiveWidget.DisplayMemberPath = "DisplayName";

        // Select first by default
        if (items.Count > 0)
            CboActiveWidget.SelectedIndex = 0;
    }

    private void CboActiveWidget_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressControlEvents) return;
        SyncPanelToActiveWidget();
    }

    private WidgetType? GetSelectedWidgetType()
    {
        return (CboActiveWidget?.SelectedItem as WidgetTypeItem)?.Type;
    }

    // ── Field combo population ──────────────────────────────────────────

    private void PopulateFieldCombos()
    {
        var fields = TelemetryFieldProvider.GetAllFields(includeAdvanced: false);

        // Build display items: "(None)" + all fields grouped by display name
        var items = new List<FieldItem> { new("(None)", null) };
        items.AddRange(fields.Select(f => new FieldItem(f.DisplayName, f.Field)));

        foreach (var cbo in new[] { CboTopField, CboCenterField, CboBottomField, CboLeftField, CboRightField })
        {
            cbo.ItemsSource = items;
            cbo.DisplayMemberPath = "DisplayName";
            cbo.SelectedIndex = 0; // "(None)" by default
        }
    }

    /// <summary>Simple wrapper so ComboBox can bind DisplayName and we can read .Field</summary>
    private record FieldItem(string DisplayName, TelemetryField? Field);

    // ── Sync panel controls ↔ active widget ─────────────────────────────

    /// <summary>
    /// Sync the panel controls to the currently selected widget type.
    /// Reads the active widget's settings and pushes them into the panel.
    /// </summary>
    private void SyncPanelToActiveWidget()
    {
        var widgetType = GetSelectedWidgetType();
        if (widgetType == null) return;

        bool widgetExists = _widgetManager.HasWidgetType(widgetType.Value);
        BtnToggleWidget.Content = widgetExists ? "Hide" : "Show";

        if (widgetType == WidgetType.MRTOne)
            SyncPanelToMRTOne();
        // Future: else if (widgetType == WidgetType.FuelMonitor) SyncPanelToFuelMonitor();
    }

    private void SyncPanelToMRTOne()
    {
        var widget = GetActiveMRTOneWidget();
        if (widget == null) return;

        _suppressControlEvents = true;
        try
        {
            var s = widget.GetCurrentSettings();

            // Size
            SliderWidgetSize.Value = widget.Width;
            TxtSizeValue.Text = $"{(int)widget.Width}px";

            // Fields
            SetComboToField(CboTopField, s.TopFieldEnum);
            SetComboToField(CboCenterField, s.CenterFieldEnum);
            SetComboToField(CboBottomField, s.BottomFieldEnum);
            SetComboToField(CboLeftField, s.LeftFieldEnum);
            SetComboToField(CboRightField, s.RightFieldEnum);

            // Visual toggles
            ChkGradientBg.IsChecked = s.EnableGradientBackground;
            ChkShiftRing.IsChecked = s.EnableShiftPointRing;
            ChkGlow.IsChecked = s.EnableGlowEffects;
            ChkPitLimiter.IsChecked = s.EnablePitLimiterIndicator;
            ChkEnhancedRadar.IsChecked = s.EnableEnhancedRadar;

            // Fuel alert toggles and sliders
            ChkFuelYellow.IsChecked = _fuelAlertSettings.EnableYellowAlert;
            ChkFuelRed.IsChecked = _fuelAlertSettings.EnableRedAlert;
            ChkFuelCritical.IsChecked = _fuelAlertSettings.EnableCriticalAlert;
            ChkFuelSputter.IsChecked = _fuelAlertSettings.EnableSputterAlert;

            SliderFuelYellow.Value = _fuelAlertSettings.YellowThresholdLaps;
            SliderFuelRed.Value = _fuelAlertSettings.RedThresholdLaps;
            SliderFuelCritical.Value = _fuelAlertSettings.CriticalThresholdLaps;
            SliderFuelSputter.Value = _fuelAlertSettings.SputteringThresholdL;

            SliderFuelYellow.IsEnabled = _fuelAlertSettings.EnableYellowAlert;
            SliderFuelRed.IsEnabled = _fuelAlertSettings.EnableRedAlert;
            SliderFuelCritical.IsEnabled = _fuelAlertSettings.EnableCriticalAlert;
            SliderFuelSputter.IsEnabled = _fuelAlertSettings.EnableSputterAlert;
        }
        finally
        {
            _suppressControlEvents = false;
        }
    }

    private static void SetComboToField(ComboBox cbo, TelemetryField? field)
    {
        if (cbo.ItemsSource is not List<FieldItem> items) return;
        var match = items.FirstOrDefault(i => i.Field == field);
        cbo.SelectedItem = match ?? items[0]; // fallback to "(None)"
    }

    private MRTOne? GetActiveMRTOneWidget()
    {
        if (_widgetManager == null) return null;
        if (!_widgetManager.HasWidgetType(WidgetType.MRTOne)) return null;
        return _widgetManager.GetWidgetsByType(WidgetType.MRTOne)
                             .FirstOrDefault() as MRTOne;
    }

    // ── Button handlers ─────────────────────────────────────────────────

    private void BtnToggleWidget_Click(object sender, RoutedEventArgs e)
    {
        var widgetType = GetSelectedWidgetType();
        if (widgetType == null) return;

        if (_widgetManager.HasWidgetType(widgetType.Value))
        {
            // Remove existing widgets of this type
            var widgets = _widgetManager.GetWidgetsByType(widgetType.Value).ToList();
            foreach (var w in widgets)
                _widgetManager.RemoveWidget(w.WidgetId);
            BtnToggleWidget.Content = "Show";
            WidgetStatusText.Text = $"{widgetType.Value.GetDisplayName()} closed.";
        }
        else
        {
            _widgetManager.CreateWidget(widgetType.Value);
            BtnToggleWidget.Content = "Hide";
            WidgetStatusText.Text = $"{widgetType.Value.GetDisplayName()} active.";
            SyncPanelToActiveWidget();
        }
    }

    private void BtnLockWidgets_Click(object sender, RoutedEventArgs e) => ToggleWidgetLock();

    // ── Size slider ─────────────────────────────────────────────────────

    private void SliderWidgetSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents) return;
        if (TxtSizeValue == null) return; // XAML not loaded yet

        int size = (int)e.NewValue;
        TxtSizeValue.Text = $"{size}px";

        var widget = GetActiveMRTOneWidget();
        widget?.SetSize(size);
    }

    // ── Centering buttons ───────────────────────────────────────────────

    private void BtnCenterH_Click(object sender, RoutedEventArgs e)
        => GetActiveMRTOneWidget()?.CenterHorizontally();

    private void BtnCenterV_Click(object sender, RoutedEventArgs e)
        => GetActiveMRTOneWidget()?.CenterVertically();

    private void BtnCenterBoth_Click(object sender, RoutedEventArgs e)
        => GetActiveMRTOneWidget()?.CenterBoth();

    // ── Data field combos ───────────────────────────────────────────────

    private void FieldCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActiveMRTOneWidget();
        if (widget == null) return;

        var s = widget.GetCurrentSettings();

        // Read current combo selections
        TelemetryField? top = GetSelectedField(CboTopField);
        TelemetryField center = GetSelectedField(CboCenterField) ?? TelemetryField.Gear;
        TelemetryField? bottom = GetSelectedField(CboBottomField);
        TelemetryField? left = GetSelectedField(CboLeftField);
        TelemetryField? right = GetSelectedField(CboRightField);

        // Update persistent settings (string names)
        s.TopField = top?.ToString();
        s.CenterField = center.ToString();
        s.BottomField = bottom?.ToString();
        s.LeftField = left?.ToString();
        s.RightField = right?.ToString();

        // Push live to widget
        widget.UpdateDisplayFields(top, center, bottom);
        widget.UpdateSideBoxes(left, right);
        widget.UpdateWidgetSettings(s);
    }

    private static TelemetryField? GetSelectedField(ComboBox cbo)
    {
        if (cbo.SelectedItem is FieldItem fi) return fi.Field;
        return null;
    }

    // ── Visual toggles ──────────────────────────────────────────────────

    private void VisualToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActiveMRTOneWidget();
        if (widget == null) return;

        var s = widget.GetCurrentSettings();
        s.EnableGradientBackground = ChkGradientBg.IsChecked == true;
        s.EnableShiftPointRing = ChkShiftRing.IsChecked == true;
        s.EnableGlowEffects = ChkGlow.IsChecked == true;
        s.EnablePitLimiterIndicator = ChkPitLimiter.IsChecked == true;
        s.EnableEnhancedRadar = ChkEnhancedRadar.IsChecked == true;
        widget.UpdateWidgetSettings(s);
    }

    // ── Fuel alert thresholds ───────────────────────────────────────────

    private void FuelAlert_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents) return;
        // Guard against XAML init (elements not yet created)
        if (TxtFuelYellow == null || TxtFuelRed == null || TxtFuelCritical == null || TxtFuelSputter == null)
            return;

        _fuelAlertSettings.YellowThresholdLaps = (float)SliderFuelYellow.Value;
        _fuelAlertSettings.RedThresholdLaps = (float)SliderFuelRed.Value;
        _fuelAlertSettings.CriticalThresholdLaps = (float)SliderFuelCritical.Value;
        _fuelAlertSettings.SputteringThresholdL = (float)SliderFuelSputter.Value;

        TxtFuelYellow.Text = $"{_fuelAlertSettings.YellowThresholdLaps:F1}";
        TxtFuelRed.Text = $"{_fuelAlertSettings.RedThresholdLaps:F1}";
        TxtFuelCritical.Text = $"{_fuelAlertSettings.CriticalThresholdLaps:F1}";
        TxtFuelSputter.Text = $"{_fuelAlertSettings.SputteringThresholdL:F1}L";
    }

    private void FuelAlertToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;

        _fuelAlertSettings.EnableYellowAlert = ChkFuelYellow.IsChecked == true;
        _fuelAlertSettings.EnableRedAlert = ChkFuelRed.IsChecked == true;
        _fuelAlertSettings.EnableCriticalAlert = ChkFuelCritical.IsChecked == true;
        _fuelAlertSettings.EnableSputterAlert = ChkFuelSputter.IsChecked == true;

        // Disable slider when its alert is turned off for visual clarity
        SliderFuelYellow.IsEnabled = _fuelAlertSettings.EnableYellowAlert;
        SliderFuelRed.IsEnabled = _fuelAlertSettings.EnableRedAlert;
        SliderFuelCritical.IsEnabled = _fuelAlertSettings.EnableCriticalAlert;
        SliderFuelSputter.IsEnabled = _fuelAlertSettings.EnableSputterAlert;
    }

    /// <summary>
    /// Expose fuel alert settings so widgets can read them.
    /// </summary>
    public FuelAlertSettings FuelAlerts => _fuelAlertSettings;

    // ── Connection status ───────────────────────────────────────────────

    private void OnTelemetryStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        Dispatcher.Invoke(() => UpdateConnectionStatus(e.Status));
    }

    private void UpdateConnectionStatus(ConnectionStatus status)
    {
        ConnectionStatusText.Text = status switch
        {
            ConnectionStatus.Connected => "Connected",
            ConnectionStatus.Connecting => "Connecting...",
            ConnectionStatus.Disconnected => "Disconnected",
            _ => "Unknown"
        };

        ConnectionStatusText.Foreground = new SolidColorBrush(status switch
        {
            ConnectionStatus.Connected => Color.FromRgb(0, 188, 212),
            ConnectionStatus.Connecting => Color.FromRgb(255, 152, 0),
            _ => Color.FromRgb(136, 136, 136)
        });
    }

    private void UpdateUpdateRateDisplay()
    {
        var rate = _telemetryService.UpdateRate;
        UpdateRateText.Text = rate > 0 ? $"{rate:F0} Hz" : "0 Hz";
    }

    // ── Global hotkeys ──────────────────────────────────────────────────

    private void UpdateHotkeysDisplay()
    {
        var settings = AppSettings.Instance;
        string FormatMod(string m) => m == "None" ? "" : m;

        string lockMod = FormatMod(settings.ToggleLockModifier);
        string lockKey = settings.ToggleLockKey;
        string lockHk = string.IsNullOrEmpty(lockMod) ? lockKey : $"{lockMod}+{lockKey}";

        string visMod = FormatMod(settings.ToggleVisibilityModifier);
        string visKey = settings.ToggleVisibilityKey;
        string visHk = string.IsNullOrEmpty(visMod) ? visKey : $"{visMod}+{visKey}";

        HotkeysText.Text = $"{lockHk} Lock | {visHk} Show/Hide";
    }

    private void RegisterGlobalHotkeys()
    {
        var settings = AppSettings.Instance;

        if (settings.HotkeysConflict())
        {
            MessageBox.Show(
                "Toggle Lock and Toggle Visibility hotkeys conflict.\nPlease change one in settings.",
                "Hotkey Conflict", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _toggleLockHotkey = new GlobalHotkey(this, hotkeyId: 9001);
        _toggleLockHotkey.HotkeyPressed += (_, _) => Dispatcher.Invoke(ToggleWidgetLock);
        _toggleLockHotkey.Register(settings.ToggleLockModifier, settings.ToggleLockKey);

        _toggleVisibilityHotkey = new GlobalHotkey(this, hotkeyId: 9002);
        _toggleVisibilityHotkey.HotkeyPressed += (_, _) => Dispatcher.Invoke(ToggleWidgetVisibility);
        _toggleVisibilityHotkey.Register(settings.ToggleVisibilityModifier, settings.ToggleVisibilityKey);
    }

    private void ToggleWidgetLock()
    {
        var settings = AppSettings.Instance;
        settings.LockWindows = !settings.LockWindows;
        settings.Save();
        _widgetManager.LockAllWidgets(settings.LockWindows);
        BtnLockWidgets.Content = settings.LockWindows ? "Unlock Widgets" : "Lock Widgets";
    }

    private void ToggleWidgetVisibility()
    {
        _widgetManager.ToggleAllWidgets();
    }

    // ── Window state persistence ────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        _telemetryService.StatusChanged -= OnTelemetryStatusChanged;
        _toggleLockHotkey?.Dispose();
        _toggleVisibilityHotkey?.Dispose();
        _widgetManager.SaveCurrentLayout();
        _widgetManager.RemoveAllWidgets();
        System.Windows.Application.Current.Shutdown();
        base.OnClosed(e);
    }

    private void ApplyWindowSettings()
    {
        var settings = AppSettings.Instance;
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;

        if (settings.WindowLeft.HasValue && settings.WindowTop.HasValue &&
            IsPositionOnScreen(settings.WindowLeft.Value, settings.WindowTop.Value))
        {
            Left = settings.WindowLeft.Value;
            Top = settings.WindowTop.Value;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        if (settings.WindowMaximized) WindowState = WindowState.Maximized;
        if (settings.StartMinimized) WindowState = WindowState.Minimized;
        Topmost = settings.AlwaysOnTop;
    }

    private static bool IsPositionOnScreen(double left, double top)
    {
        return left >= SystemParameters.VirtualScreenLeft &&
               left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
               top >= SystemParameters.VirtualScreenTop &&
               top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e) => SaveWindowState();
    private void MainWindow_LocationChanged(object? sender, EventArgs e) { if (WindowState == WindowState.Normal) SaveWindowState(); }
    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e) { if (WindowState == WindowState.Normal) SaveWindowState(); }
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        var settings = AppSettings.Instance;
        settings.WindowMaximized = WindowState == WindowState.Maximized;
        settings.Save();
    }

    private void SaveWindowState()
    {
        if (!IsLoaded) return;
        var settings = AppSettings.Instance;
        if (WindowState == WindowState.Normal)
        {
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            settings.WindowLeft = Left;
            settings.WindowTop = Top;
        }
        settings.WindowMaximized = WindowState == WindowState.Maximized;
        settings.Save();
    }
}
