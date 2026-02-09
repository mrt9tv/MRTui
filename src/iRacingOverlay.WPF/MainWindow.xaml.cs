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
using TurnDisplay = iRacingOverlay.WPF.Widgets.TurnDisplayWidget.TurnDisplayWidget;
using RelativeW = iRacingOverlay.WPF.Widgets.RelativeWidget.RelativeWidget;

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

    /// <summary>Currently selected widget type from menu buttons.</summary>
    private WidgetType _selectedWidgetType = WidgetType.MRTOne;

    /// <summary>Map menu buttons to widget types.</summary>
    private readonly Dictionary<string, WidgetType> _widgetButtonMap = new()
    {
        { "BtnWidgetMRTOne", WidgetType.MRTOne },
        { "BtnWidgetTurnDisplay", WidgetType.TurnDisplay },
        { "BtnWidgetFuel", WidgetType.FuelCalculator },
        { "BtnWidgetRelative", WidgetType.Relative },
    };

    private void PopulateWidgetSelector()
    {
        // Highlight the default button
        HighlightActiveMenuButton();
    }

    private void WidgetMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (_widgetButtonMap.TryGetValue(btn.Name, out var type))
        {
            _selectedWidgetType = type;
            HighlightActiveMenuButton();
            SyncPanelToActiveWidget();
        }
    }

    /// <summary>Highlight the active widget menu button with teal foreground.</summary>
    private void HighlightActiveMenuButton()
    {
        var tealBrush = FindResource("TealPrimary") as SolidColorBrush ?? BRUSH_TEAL_STATIC;
        var normalBrush = FindResource("LightText") as SolidColorBrush ?? new SolidColorBrush(Colors.White);
        foreach (var kvp in _widgetButtonMap)
        {
            var btn = FindName(kvp.Key) as Button;
            if (btn != null)
                btn.Foreground = kvp.Value == _selectedWidgetType ? tealBrush : normalBrush;
        }
    }

    private static readonly SolidColorBrush BRUSH_TEAL_STATIC = new(Color.FromRgb(0, 128, 128));

    private WidgetType? GetSelectedWidgetType()
    {
        return _selectedWidgetType;
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

        // Show/hide widget-specific panels (left + right columns)
        MRTOnePanel.Visibility = widgetType == WidgetType.MRTOne ? Visibility.Visible : Visibility.Collapsed;
        MRTOneRightPanel.Visibility = widgetType == WidgetType.MRTOne ? Visibility.Visible : Visibility.Collapsed;
        TurnDisplayPanel.Visibility = widgetType == WidgetType.TurnDisplay ? Visibility.Visible : Visibility.Collapsed;
        FuelCalculatorPanel.Visibility = widgetType == WidgetType.FuelCalculator ? Visibility.Visible : Visibility.Collapsed;
        FuelCalcRightPanel.Visibility = widgetType == WidgetType.FuelCalculator ? Visibility.Visible : Visibility.Collapsed;
        RelativePanel.Visibility = widgetType == WidgetType.Relative ? Visibility.Visible : Visibility.Collapsed;
        RelativeRightPanel.Visibility = widgetType == WidgetType.Relative ? Visibility.Visible : Visibility.Collapsed;

        if (widgetType == WidgetType.MRTOne)
            SyncPanelToMRTOne();
        else if (widgetType == WidgetType.TurnDisplay)
            SyncPanelToTurnDisplay();
        else if (widgetType == WidgetType.FuelCalculator)
            SyncPanelToFuelCalculator();
        else if (widgetType == WidgetType.Relative)
            SyncPanelToRelative();
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

            // Opacity
            SliderMRTOneOpacity.Value = widget.Opacity * 100;
            TxtMRTOneOpacity.Text = $"{(int)(widget.Opacity * 100)}%";

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
        if (_widgetManager == null)
        {
            System.Diagnostics.Debug.WriteLine("[MRT-UI] GetActiveMRTOneWidget: _widgetManager is null");
            return null;
        }
        bool exists = _widgetManager.HasWidgetType(WidgetType.MRTOne);
        System.Diagnostics.Debug.WriteLine($"[MRT-UI] GetActiveMRTOneWidget: HasWidgetType={exists}, ActiveCount={_widgetManager.GetWidgetCount()}");
        if (!exists) return null;
        return _widgetManager.GetWidgetsByType(WidgetType.MRTOne)
                             .FirstOrDefault() as MRTOne;
    }

    private void SyncPanelToTurnDisplay()
    {
        var widget = GetActiveTurnDisplayWidget();
        if (widget == null) return;

        _suppressControlEvents = true;
        try
        {
            ChkShowTurnNames.IsChecked = widget.ShowTurnName;
            ChkAnimateBorder.IsChecked = widget.AnimateBorder;
            SliderTurnOpacity.Value = widget.Opacity * 100;
            TxtTurnOpacity.Text = $"{(int)(widget.Opacity * 100)}%";
        }
        finally
        {
            _suppressControlEvents = false;
        }
    }

    private TurnDisplay? GetActiveTurnDisplayWidget()
    {
        if (_widgetManager == null) return null;
        if (!_widgetManager.HasWidgetType(WidgetType.TurnDisplay)) return null;
        return _widgetManager.GetWidgetsByType(WidgetType.TurnDisplay)
                             .FirstOrDefault() as TurnDisplay;
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
        if (_suppressControlEvents)
        {
            System.Diagnostics.Debug.WriteLine("[MRT-UI] FieldCombo changed but suppressed");
            return;
        }

        var widget = GetActiveMRTOneWidget();
        if (widget == null)
        {
            System.Diagnostics.Debug.WriteLine("[MRT-UI] FieldCombo changed but widget is null");
            _logger.LogWarning("Data field change ignored — no active MRT One widget found");
            WidgetStatusText.Text = "⚠ No active widget — click Show first";
            return;
        }

        var s = widget.GetCurrentSettings();

        // Read current combo selections
        TelemetryField? top = GetSelectedField(CboTopField);
        TelemetryField center = GetSelectedField(CboCenterField) ?? TelemetryField.Gear;
        TelemetryField? bottom = GetSelectedField(CboBottomField);
        TelemetryField? left = GetSelectedField(CboLeftField);
        TelemetryField? right = GetSelectedField(CboRightField);

        System.Diagnostics.Debug.WriteLine($"[MRT-UI] Field change: T={top} C={center} B={bottom} L={left} R={right}");

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

        // Persist layout so changes survive restart
        _widgetManager.SaveCurrentLayout();
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
        _widgetManager.SaveCurrentLayout();
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

    // ── Turn Display toggles ────────────────────────────────────────────

    private void TurnDisplayToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActiveTurnDisplayWidget();
        if (widget == null) return;

        widget.ShowTurnName = ChkShowTurnNames.IsChecked == true;
        widget.AnimateBorder = ChkAnimateBorder.IsChecked == true;
        widget.SaveSettings();
        _widgetManager.SaveCurrentLayout();
    }

    // ── Fuel Calculator panel sync ────────────────────────────────────

    private void SyncPanelToFuelCalculator()
    {
        var widget = GetActiveFuelWidget();
        if (widget == null) return;

        _suppressControlEvents = true;
        try
        {
            ChkFuelL3.IsChecked = widget.ShowL3Average;
            ChkFuelL5.IsChecked = widget.ShowL5Average;
            ChkFuelSaving.IsChecked = widget.ShowSavingSection;
            ChkFuelPitLap.IsChecked = widget.ShowPitLap;
            ChkFuelFillAmount.IsChecked = widget.ShowFillAmount;
            SliderFuelOpacity.Value = widget.Opacity * 100;
            TxtFuelOpacity.Text = $"{(int)(widget.Opacity * 100)}%";
        }
        finally
        {
            _suppressControlEvents = false;
        }
    }

    private Widgets.FuelWidget.FuelWidget? GetActiveFuelWidget()
    {
        if (_widgetManager == null) return null;
        if (!_widgetManager.HasWidgetType(WidgetType.FuelCalculator)) return null;
        return _widgetManager.GetWidgetsByType(WidgetType.FuelCalculator)
                             .FirstOrDefault() as Widgets.FuelWidget.FuelWidget;
    }

    private void FuelCalculatorToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActiveFuelWidget();
        if (widget == null) return;

        widget.ShowL3Average = ChkFuelL3.IsChecked == true;
        widget.ShowL5Average = ChkFuelL5.IsChecked == true;
        widget.ShowSavingSection = ChkFuelSaving.IsChecked == true;
        widget.ShowPitLap = ChkFuelPitLap.IsChecked == true;
        widget.ShowFillAmount = ChkFuelFillAmount.IsChecked == true;
        widget.ApplyToggles();
        widget.SaveSettings();
        _widgetManager.SaveCurrentLayout();
    }

    // ── Relative panel sync ─────────────────────────────────────────────

    private void SyncPanelToRelative()
    {
        var widget = GetActiveRelativeWidget();
        if (widget == null) return;

        _suppressControlEvents = true;
        try
        {
            SliderMaxAhead.Value = widget.MaxAhead;
            SliderMaxBehind.Value = widget.MaxBehind;
            TxtMaxAhead.Text = widget.MaxAhead.ToString();
            TxtMaxBehind.Text = widget.MaxBehind.ToString();
            ChkSmartRowCount.IsChecked = widget.UseSmartRowCount;
            ChkShowClassPosition.IsChecked = widget.ShowClassPosition;
            ChkShowDriverInfo.IsChecked = widget.ShowDriverInfo;
            ChkShowCarNumber.IsChecked = widget.ShowCarNumber;
            ChkShowCarModel.IsChecked = widget.ShowCarModel;
            ChkShowInterval.IsChecked = widget.ShowInterval;
            ChkShowLastLap.IsChecked = widget.ShowLastLap;
            ChkShowAltRowShading.IsChecked = widget.ShowAlternateRowShading;
            ChkShowInfoBar.IsChecked = widget.ShowInfoBar;
            ChkShowClosingRate.IsChecked = widget.ShowClosingRate;
            ChkShowPitStopCount.IsChecked = widget.ShowPitStopCount;
            ChkShowPositionChange.IsChecked = widget.ShowPositionChange;
            ChkShowSectorDelta.IsChecked = widget.ShowSectorDelta;
            ChkShowNationality.IsChecked = widget.ShowNationality;
            ChkShowClassLegend.IsChecked = widget.ShowClassLegend;
            CboNameFormat.SelectedIndex = (int)widget.DriverNameFormat;
            SliderRelativeOpacity.Value = widget.Opacity * 100;
            TxtRelativeOpacity.Text = $"{(int)(widget.Opacity * 100)}%";
        }
        finally
        {
            _suppressControlEvents = false;
        }
    }

    private RelativeW? GetActiveRelativeWidget()
    {
        if (_widgetManager == null) return null;
        if (!_widgetManager.HasWidgetType(WidgetType.Relative)) return null;
        return _widgetManager.GetWidgetsByType(WidgetType.Relative)
                             .FirstOrDefault() as RelativeW;
    }

    private void RelativeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActiveRelativeWidget();
        if (widget == null) return;

        widget.UseSmartRowCount = ChkSmartRowCount.IsChecked == true;
        widget.ShowClassPosition = ChkShowClassPosition.IsChecked == true;
        widget.ShowDriverInfo = ChkShowDriverInfo.IsChecked == true;
        widget.ShowCarNumber = ChkShowCarNumber.IsChecked == true;
        widget.ShowCarModel = ChkShowCarModel.IsChecked == true;
        widget.ShowInterval = ChkShowInterval.IsChecked == true;
        widget.ShowLastLap = ChkShowLastLap.IsChecked == true;
        widget.ShowAlternateRowShading = ChkShowAltRowShading.IsChecked == true;
        widget.ShowInfoBar = ChkShowInfoBar.IsChecked == true;
        widget.ShowClosingRate = ChkShowClosingRate.IsChecked == true;
        widget.ShowPitStopCount = ChkShowPitStopCount.IsChecked == true;
        widget.ShowPositionChange = ChkShowPositionChange.IsChecked == true;
        widget.ShowSectorDelta = ChkShowSectorDelta.IsChecked == true;
        widget.ShowNationality = ChkShowNationality.IsChecked == true;
        widget.ShowClassLegend = ChkShowClassLegend.IsChecked == true;
        widget.RecalcLayout();
        widget.RecalcHeight();
        widget.SaveSettings();
        _widgetManager.SaveCurrentLayout();
    }

    private void RelativeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActiveRelativeWidget();
        if (widget == null) return;

        widget.MaxAhead = (int)SliderMaxAhead.Value;
        widget.MaxBehind = (int)SliderMaxBehind.Value;

        // Update display labels
        if (TxtMaxAhead != null) TxtMaxAhead.Text = widget.MaxAhead.ToString();
        if (TxtMaxBehind != null) TxtMaxBehind.Text = widget.MaxBehind.ToString();

        widget.SaveSettings();
        _widgetManager.SaveCurrentLayout();
    }

    private void CboNameFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActiveRelativeWidget();
        if (widget == null) return;

        if (CboNameFormat.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out var idx))
        {
            widget.DriverNameFormat = (RelativeW.NameFormat)idx;
            widget.SaveSettings();
            _widgetManager.SaveCurrentLayout();
        }
    }

    // ── Per-widget opacity sliders ──────────────────────────────────────

    private void MRTOneOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents) return;
        if (TxtMRTOneOpacity == null) return;
        int pct = (int)e.NewValue;
        TxtMRTOneOpacity.Text = $"{pct}%";
        var widget = GetActiveMRTOneWidget();
        if (widget != null) widget.Opacity = pct / 100.0;
    }

    private void TurnOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents) return;
        if (TxtTurnOpacity == null) return;
        int pct = (int)e.NewValue;
        TxtTurnOpacity.Text = $"{pct}%";
        var widget = GetActiveTurnDisplayWidget();
        if (widget != null) widget.Opacity = pct / 100.0;
    }

    private void FuelOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents) return;
        if (TxtFuelOpacity == null) return;
        int pct = (int)e.NewValue;
        TxtFuelOpacity.Text = $"{pct}%";
        var widget = GetActiveFuelWidget();
        if (widget != null) widget.Opacity = pct / 100.0;
    }

    private void RelativeOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents) return;
        if (TxtRelativeOpacity == null) return;
        int pct = (int)e.NewValue;
        TxtRelativeOpacity.Text = $"{pct}%";
        var widget = GetActiveRelativeWidget();
        if (widget != null) widget.Opacity = pct / 100.0;
    }

    // ── Menu column collapse ────────────────────────────────────────────

    private bool _menuCollapsed = false;

    private void BtnToggleMenu_Click(object sender, RoutedEventArgs e)
    {
        _menuCollapsed = !_menuCollapsed;
        if (_menuCollapsed)
        {
            MenuColumn.Width = new GridLength(0);
            MenuBorder.Visibility = Visibility.Collapsed;
        }
        else
        {
            MenuColumn.Width = new GridLength(160);
            MenuBorder.Visibility = Visibility.Visible;
        }
    }

    // ── Connection status ───────────────────────────────────────────────

    private void OnTelemetryStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        Dispatcher.Invoke(() => UpdateConnectionStatus(e.Status));
    }

    private void UpdateConnectionStatus(ConnectionStatus status)
    {
        var text = status switch
        {
            ConnectionStatus.Connected => "Connected",
            ConnectionStatus.Connecting => "Connecting...",
            ConnectionStatus.Disconnected => "Disconnected",
            _ => "Unknown"
        };

        var brush = new SolidColorBrush(status switch
        {
            ConnectionStatus.Connected => Color.FromRgb(0, 188, 212),
            ConnectionStatus.Connecting => Color.FromRgb(255, 152, 0),
            _ => Color.FromRgb(136, 136, 136)
        });

        ConnectionStatusText.Text = text;
        ConnectionStatusText.Foreground = brush;

        // Mirror to sidebar
        MenuConnectionText.Text = text;
        MenuConnectionText.Foreground = brush;
    }

    private void UpdateUpdateRateDisplay()
    {
        var rate = _telemetryService.UpdateRate;
        var rateText = rate > 0 ? $"{rate:F0} Hz" : "0 Hz";
        UpdateRateText.Text = rateText;
        MenuRateText.Text = rateText;
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
