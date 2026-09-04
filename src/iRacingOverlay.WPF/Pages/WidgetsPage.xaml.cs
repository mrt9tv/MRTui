using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Services;
using MRTOne = iRacingOverlay.WPF.Widgets.MRTOneWidget.MRTOneWidget;
using TurnDisplay = iRacingOverlay.WPF.Widgets.TurnDisplayWidget.TurnDisplayWidget;
using RelativeW = iRacingOverlay.WPF.Widgets.RelativeWidget.RelativeWidget;
using StandingsW = iRacingOverlay.WPF.Widgets.StandingsWidget.StandingsWidget;
using ProxFeedW = iRacingOverlay.WPF.Widgets.ProximityFeedWidget.ProximityFeedWidget;
using PitConfirmW = iRacingOverlay.WPF.Widgets.PitConfirmWidget.PitConfirmWidget;

namespace iRacingOverlay.WPF.Pages;

public partial class WidgetsPage : UserControl
{
    private readonly WidgetManager _widgetManager;
    private readonly ITelemetryService _telemetryService;

    /// <summary>Fuel alert settings (shared across widgets).</summary>
    private readonly FuelAlertSettings _fuelAlertSettings = FuelAlertSettings.Default;

    /// <summary>Suppress combo/slider events while syncing UI to settings.</summary>
    private bool _suppressControlEvents = true;

    private WidgetType _selectedWidgetType = WidgetType.MRTOne;

    private readonly Dictionary<string, WidgetType> _widgetButtonMap = new()
    {
        { "BtnWidgetMRTOne", WidgetType.MRTOne },
        { "BtnWidgetTurnDisplay", WidgetType.TurnDisplay },
        { "BtnWidgetFuel", WidgetType.FuelCalculator },
        { "BtnWidgetRelative", WidgetType.Relative },
        { "BtnWidgetProximityFeed", WidgetType.ProximityFeed },
        { "BtnWidgetStandings", WidgetType.Standings },
        { "BtnWidgetPitConfirm", WidgetType.PitConfirm },
    };

    public WidgetsPage(WidgetManager widgetManager, ITelemetryService telemetryService)
    {
        InitializeComponent();
        _widgetManager = widgetManager;
        _telemetryService = telemetryService;

        ApplySupportedWidgetVisibility();
        RestoreExpanderStates();
        PopulateFieldCombos();
        HighlightActiveMenuButton();
        SyncPanelToActiveWidget();

        _suppressControlEvents = false;
    }

    /// <summary>
    /// Show only the widgets this build actually supports.
    ///
    /// Visibility used to be hardcoded per button in XAML, in parallel with
    /// WidgetManager.SupportedWidgetTypes and a #if DEBUG list on the Dashboard —
    /// three sources of truth that had already drifted apart. Deriving it here
    /// means adding a widget to SupportedWidgetTypes is the only change needed.
    /// </summary>
    private void ApplySupportedWidgetVisibility()
    {
        WidgetType? firstSupported = null;

        foreach (var kvp in _widgetButtonMap)
        {
            if (FindName(kvp.Key) is not Button btn) continue;

            bool supported = WidgetManager.SupportedWidgetTypes.Contains(kvp.Value);
            btn.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;

            if (supported) firstSupported ??= kvp.Value;
        }

        // Never leave an unsupported widget selected — its settings panel would
        // be showing controls for something the user cannot enable.
        if (!WidgetManager.SupportedWidgetTypes.Contains(_selectedWidgetType) && firstSupported.HasValue)
            _selectedWidgetType = firstSupported.Value;
    }

    /// <summary>Expose fuel alert settings for MainWindow to read.</summary>
    public FuelAlertSettings FuelAlerts => _fuelAlertSettings;

    /// <summary>
    /// Restore the "More options" disclosures the user had open, and keep them
    /// persisted — reopening the same section on every visit is exactly the kind of
    /// friction that makes a dense settings page feel worse than it is.
    /// </summary>
    private void RestoreExpanderStates()
    {
        var saved = AppSettings.Instance.ExpandedSections;

        foreach (var expander in FindExpanders(this))
        {
            if (string.IsNullOrEmpty(expander.Name)) continue;

            if (saved.TryGetValue(expander.Name, out bool wasOpen))
                expander.IsExpanded = wasOpen;

            expander.Expanded += OnExpanderStateChanged;
            expander.Collapsed += OnExpanderStateChanged;
        }
    }

    private void OnExpanderStateChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not Expander expander || string.IsNullOrEmpty(expander.Name)) return;

        AppSettings.Instance.ExpandedSections[expander.Name] = expander.IsExpanded;
        AppSettings.Instance.SaveQuiet();
    }

    /// <summary>
    /// Walk the logical tree, not the visual one: the per-widget panels start
    /// collapsed, so their visual children do not exist yet when this runs.
    /// </summary>
    private static IEnumerable<Expander> FindExpanders(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject node) continue;

            if (node is Expander expander) yield return expander;

            foreach (var nested in FindExpanders(node))
                yield return nested;
        }
    }

    // ── Widget selector ─────────────────────────────────────────────

    /// <summary>
    /// Select a widget from outside this page (the dashboard's Settings button).
    /// </summary>
    public void SelectWidget(WidgetType type)
    {
        if (!WidgetManager.SupportedWidgetTypes.Contains(type)) return;

        _selectedWidgetType = type;
        HighlightActiveMenuButton();
        SyncPanelToActiveWidget();
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

    private static readonly SolidColorBrush BRUSH_TEAL =
        new(Color.FromRgb(0, 128, 128));

    private void HighlightActiveMenuButton()
    {
        var tealBrush = FindResource("TealPrimary") as SolidColorBrush ?? BRUSH_TEAL;
        var normalBrush = FindResource("LightText") as SolidColorBrush ?? new SolidColorBrush(Colors.White);
        foreach (var kvp in _widgetButtonMap)
        {
            var btn = FindName(kvp.Key) as Button;
            if (btn != null)
                btn.Foreground = kvp.Value == _selectedWidgetType ? tealBrush : normalBrush;
        }
    }

    // ── Show / Hide / Lock ──────────────────────────────────────────

    /// <summary>
    /// Show or hide the selected widget.
    ///
    /// This used to call RemoveWidget, which closed the window, dropped it from the
    /// active set and then rewrote layout.json — erasing that widget's position,
    /// size, opacity and field selections. Pressing "Show" afterwards built a fresh
    /// widget with defaults, so the button labelled "Hide" silently destroyed the
    /// user's configuration. Hiding now only flips the visibility preference;
    /// destruction lives behind the explicit Remove button.
    /// </summary>
    private void BtnToggleWidget_Click(object sender, RoutedEventArgs e)
    {
        var name = _selectedWidgetType.GetDisplayName();

        if (!_widgetManager.HasWidgetType(_selectedWidgetType))
        {
            _widgetManager.CreateWidget(_selectedWidgetType);
            WidgetStatusText.Text = $"{name} shown.";
            SyncPanelToActiveWidget();
            return;
        }

        bool nowVisible = !IsSelectedWidgetVisible();
        foreach (var w in _widgetManager.GetWidgetsByType(_selectedWidgetType))
            w.SetUserVisibility(nowVisible);

        _widgetManager.SaveCurrentLayout();
        WidgetStatusText.Text = nowVisible ? $"{name} shown." : $"{name} hidden — settings kept.";
        UpdateToggleButtonLabel();
    }

    /// <summary>Remove the selected widget entirely, discarding its saved settings.</summary>
    private void BtnRemoveWidget_Click(object sender, RoutedEventArgs e)
    {
        if (!_widgetManager.HasWidgetType(_selectedWidgetType)) return;

        var name = _selectedWidgetType.GetDisplayName();
        var confirm = MessageBox.Show(
            $"Remove {name}?\n\nIts position, size and settings will be discarded. " +
            "To keep them, use Hide instead.",
            "Remove widget", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.OK) return;

        foreach (var w in _widgetManager.GetWidgetsByType(_selectedWidgetType).ToList())
            _widgetManager.RemoveWidget(w.WidgetId);

        WidgetStatusText.Text = $"{name} removed.";
        SyncPanelToActiveWidget();
    }

    /// <summary>Whether the selected widget type is currently set to be visible by the user.</summary>
    private bool IsSelectedWidgetVisible() =>
        _widgetManager.GetWidgetsByType(_selectedWidgetType).Any(w => w.UserWantsVisible);

    private void UpdateToggleButtonLabel()
    {
        bool exists = _widgetManager.HasWidgetType(_selectedWidgetType);
        BtnToggleWidget.Content = exists && IsSelectedWidgetVisible() ? "Hide" : "Show";
        BtnRemoveWidget.IsEnabled = exists;
    }

    private void BtnLockWidgets_Click(object sender, RoutedEventArgs e)
    {
        var settings = AppSettings.Instance;
        settings.LockWindows = !settings.LockWindows;
        settings.Save();
        _widgetManager.LockAllWidgets(settings.LockWindows);
        BtnLockWidgets.Content = settings.LockWindows ? "Unlock" : "Lock";
    }

    // ── Panel sync ──────────────────────────────────────────────────

    public void SyncPanelToActiveWidget()
    {
        UpdateToggleButtonLabel();

        MRTOnePanel.Visibility = _selectedWidgetType == WidgetType.MRTOne ? Visibility.Visible : Visibility.Collapsed;
        MRTOneRightPanel.Visibility = _selectedWidgetType == WidgetType.MRTOne ? Visibility.Visible : Visibility.Collapsed;
        TurnDisplayPanel.Visibility = _selectedWidgetType == WidgetType.TurnDisplay ? Visibility.Visible : Visibility.Collapsed;
        FuelCalculatorPanel.Visibility = _selectedWidgetType == WidgetType.FuelCalculator ? Visibility.Visible : Visibility.Collapsed;
        FuelCalcRightPanel.Visibility = _selectedWidgetType == WidgetType.FuelCalculator ? Visibility.Visible : Visibility.Collapsed;
        RelativePanel.Visibility = _selectedWidgetType == WidgetType.Relative ? Visibility.Visible : Visibility.Collapsed;
        RelativeRightPanel.Visibility = _selectedWidgetType == WidgetType.Relative ? Visibility.Visible : Visibility.Collapsed;
        ProximityFeedPanel.Visibility = _selectedWidgetType == WidgetType.ProximityFeed ? Visibility.Visible : Visibility.Collapsed;
        ProximityFeedRightPanel.Visibility = _selectedWidgetType == WidgetType.ProximityFeed ? Visibility.Visible : Visibility.Collapsed;
        StandingsPanel.Visibility = _selectedWidgetType == WidgetType.Standings ? Visibility.Visible : Visibility.Collapsed;
        PitConfirmPanel.Visibility = _selectedWidgetType == WidgetType.PitConfirm ? Visibility.Visible : Visibility.Collapsed;

        if (_selectedWidgetType == WidgetType.MRTOne) SyncPanelToMRTOne();
        else if (_selectedWidgetType == WidgetType.TurnDisplay) SyncPanelToTurnDisplay();
        else if (_selectedWidgetType == WidgetType.FuelCalculator) SyncPanelToFuelCalculator();
        else if (_selectedWidgetType == WidgetType.Relative) SyncPanelToRelative();
        else if (_selectedWidgetType == WidgetType.Standings) SyncPanelToStandings();
        else if (_selectedWidgetType == WidgetType.ProximityFeed) SyncPanelToProximityFeed();
        else if (_selectedWidgetType == WidgetType.PitConfirm) SyncPanelToPitConfirm();
    }

    // ── Pit Confirm ─────────────────────────────────────────────────

    private PitConfirmW? GetActivePitConfirmWidget()
    {
        if (!_widgetManager.HasWidgetType(WidgetType.PitConfirm)) return null;
        return _widgetManager.GetWidgetsByType(WidgetType.PitConfirm).FirstOrDefault() as PitConfirmW;
    }

    private void SyncPanelToPitConfirm()
    {
        var widget = GetActivePitConfirmWidget();
        if (widget == null) return;

        _suppressControlEvents = true;
        try
        {
            ChkPitConfirmGarage.IsChecked = widget.ShowInGarage;
            SliderPitConfirmOpacity.Value = widget.Opacity * 100;
            TxtPitConfirmOpacity.Text = $"{(int)(widget.Opacity * 100)}%";
        }
        finally { _suppressControlEvents = false; }
    }

    private void PitConfirmToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActivePitConfirmWidget();
        if (widget == null) return;

        widget.ShowInGarage = ChkPitConfirmGarage.IsChecked == true;
        widget.SaveSettings();
        _widgetManager.SaveCurrentLayout();
    }

    private void PitConfirmOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents || TxtPitConfirmOpacity == null) return;
        int pct = (int)e.NewValue;
        TxtPitConfirmOpacity.Text = $"{pct}%";
        var widget = GetActivePitConfirmWidget();
        if (widget != null) widget.Opacity = pct / 100.0;
    }

    // ── MRT One sync ────────────────────────────────────────────────

    private MRTOne? GetActiveMRTOneWidget()
    {
        if (!_widgetManager.HasWidgetType(WidgetType.MRTOne)) return null;
        return _widgetManager.GetWidgetsByType(WidgetType.MRTOne).FirstOrDefault() as MRTOne;
    }

    private void SyncPanelToMRTOne()
    {
        var widget = GetActiveMRTOneWidget();
        if (widget == null) return;

        _suppressControlEvents = true;
        try
        {
            var s = widget.GetCurrentSettings();
            SliderWidgetSize.Value = widget.Width;
            TxtSizeValue.Text = $"{(int)widget.Width}px";
            SetComboToField(CboTopField, s.TopFieldEnum);
            SetComboToField(CboCenterField, s.CenterFieldEnum);
            SetComboToField(CboBottomField, s.BottomFieldEnum);
            SetComboToField(CboLeftField, s.LeftFieldEnum);
            SetComboToField(CboRightField, s.RightFieldEnum);
            ChkGradientBg.IsChecked = s.EnableGradientBackground;
            ChkShiftRing.IsChecked = s.EnableShiftPointRing;
            ChkGlow.IsChecked = s.EnableGlowEffects;
            ChkPitLimiter.IsChecked = s.EnablePitLimiterIndicator;
            ChkEnhancedRadar.IsChecked = s.EnableEnhancedRadar;
            ChkAutoSwap.IsChecked = s.EnableAutoSwap;
            SliderMRTOneOpacity.Value = widget.Opacity * 100;
            TxtMRTOneOpacity.Text = $"{(int)(widget.Opacity * 100)}%";
            SliderMRTOneBgOpacity.Value = widget.BackgroundOpacity * 100;
            TxtMRTOneBgOpacity.Text = $"{(int)(widget.BackgroundOpacity * 100)}%";

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
        finally { _suppressControlEvents = false; }
    }

    // ── MRT One handlers ────────────────────────────────────────────

    private void SliderWidgetSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents || TxtSizeValue == null) return;
        int size = (int)e.NewValue;
        TxtSizeValue.Text = $"{size}px";
        GetActiveMRTOneWidget()?.SetSize(size);
    }

    private void BtnCenterH_Click(object sender, RoutedEventArgs e) => GetActiveMRTOneWidget()?.CenterHorizontally();
    private void BtnCenterV_Click(object sender, RoutedEventArgs e) => GetActiveMRTOneWidget()?.CenterVertically();
    private void BtnCenterBoth_Click(object sender, RoutedEventArgs e) => GetActiveMRTOneWidget()?.CenterBoth();

    private void FieldCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActiveMRTOneWidget();
        if (widget == null) { WidgetStatusText.Text = "⚠ No active widget — click Show first"; return; }

        var s = widget.GetCurrentSettings();
        TelemetryField? top = GetSelectedField(CboTopField);
        TelemetryField center = GetSelectedField(CboCenterField) ?? TelemetryField.Gear;
        TelemetryField? bottom = GetSelectedField(CboBottomField);
        TelemetryField? left = GetSelectedField(CboLeftField);
        TelemetryField? right = GetSelectedField(CboRightField);

        s.TopField = top?.ToString();
        s.CenterField = center.ToString();
        s.BottomField = bottom?.ToString();
        s.LeftField = left?.ToString();
        s.RightField = right?.ToString();

        widget.UpdateDisplayFields(top, center, bottom);
        widget.UpdateSideBoxes(left, right);
        widget.UpdateWidgetSettings(s);
        _widgetManager.SaveCurrentLayout();
    }

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
        s.EnableAutoSwap = ChkAutoSwap.IsChecked == true;
        widget.UpdateWidgetSettings(s);
        _widgetManager.SaveCurrentLayout();
    }

    private void MRTOneOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents || TxtMRTOneOpacity == null) return;
        int pct = (int)e.NewValue;
        TxtMRTOneOpacity.Text = $"{pct}%";
        var widget = GetActiveMRTOneWidget();
        if (widget != null) widget.Opacity = pct / 100.0;
    }

    private void MRTOneBgOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents || TxtMRTOneBgOpacity == null) return;
        int pct = (int)e.NewValue;
        TxtMRTOneBgOpacity.Text = $"{pct}%";
        var widget = GetActiveMRTOneWidget();
        if (widget != null) widget.BackgroundOpacity = pct / 100.0;
    }

    // ── Fuel alert handlers ─────────────────────────────────────────

    private void FuelAlert_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents) return;
        if (TxtFuelYellow == null || TxtFuelRed == null || TxtFuelCritical == null || TxtFuelSputter == null) return;

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
        SliderFuelYellow.IsEnabled = _fuelAlertSettings.EnableYellowAlert;
        SliderFuelRed.IsEnabled = _fuelAlertSettings.EnableRedAlert;
        SliderFuelCritical.IsEnabled = _fuelAlertSettings.EnableCriticalAlert;
        SliderFuelSputter.IsEnabled = _fuelAlertSettings.EnableSputterAlert;
    }

    // ── Turn Display ────────────────────────────────────────────────

    private TurnDisplay? GetActiveTurnDisplayWidget()
    {
        if (!_widgetManager.HasWidgetType(WidgetType.TurnDisplay)) return null;
        return _widgetManager.GetWidgetsByType(WidgetType.TurnDisplay).FirstOrDefault() as TurnDisplay;
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
        finally { _suppressControlEvents = false; }
    }

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

    private void TurnOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents || TxtTurnOpacity == null) return;
        int pct = (int)e.NewValue;
        TxtTurnOpacity.Text = $"{pct}%";
        var widget = GetActiveTurnDisplayWidget();
        if (widget != null) widget.Opacity = pct / 100.0;
    }

    // ── Fuel Calculator ─────────────────────────────────────────────

    private Widgets.FuelWidget.FuelWidget? GetActiveFuelWidget()
    {
        if (!_widgetManager.HasWidgetType(WidgetType.FuelCalculator)) return null;
        return _widgetManager.GetWidgetsByType(WidgetType.FuelCalculator).FirstOrDefault()
               as Widgets.FuelWidget.FuelWidget;
    }

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
        finally { _suppressControlEvents = false; }
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

    private void FuelOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents || TxtFuelOpacity == null) return;
        int pct = (int)e.NewValue;
        TxtFuelOpacity.Text = $"{pct}%";
        var widget = GetActiveFuelWidget();
        if (widget != null) widget.Opacity = pct / 100.0;
    }

    // ── Relative ────────────────────────────────────────────────────

    private RelativeW? GetActiveRelativeWidget()
    {
        if (!_widgetManager.HasWidgetType(WidgetType.Relative)) return null;
        return _widgetManager.GetWidgetsByType(WidgetType.Relative).FirstOrDefault() as RelativeW;
    }

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
            ChkUseFullIRating.IsChecked = widget.UseFullIRating;
            ChkShowLappedDim.IsChecked = widget.ShowLappedDim;
            ChkShowDangerGlow.IsChecked = widget.ShowDangerGlow;
            ChkEnableRowAnimation.IsChecked = widget.EnableRowAnimation;
            CboNameFormat.SelectedIndex = (int)widget.DriverNameFormat;
            SliderRelativeOpacity.Value = widget.Opacity * 100;
            TxtRelativeOpacity.Text = $"{(int)(widget.Opacity * 100)}%";
        }
        finally { _suppressControlEvents = false; }
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
        widget.UseFullIRating = ChkUseFullIRating.IsChecked == true;
        widget.ShowLappedDim = ChkShowLappedDim.IsChecked == true;
        widget.ShowDangerGlow = ChkShowDangerGlow.IsChecked == true;
        widget.EnableRowAnimation = ChkEnableRowAnimation.IsChecked == true;
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
        if (TxtMaxAhead != null) TxtMaxAhead.Text = widget.MaxAhead.ToString();
        if (TxtMaxBehind != null) TxtMaxBehind.Text = widget.MaxBehind.ToString();
        widget.SaveSettings();
        _widgetManager.SaveCurrentLayout();
    }

    private void RelativeOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents || TxtRelativeOpacity == null) return;
        int pct = (int)e.NewValue;
        TxtRelativeOpacity.Text = $"{pct}%";
        var widget = GetActiveRelativeWidget();
        if (widget != null) widget.Opacity = pct / 100.0;
    }

    private void CboNameFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActiveRelativeWidget();
        if (widget == null) return;
        if (CboNameFormat.SelectedItem is ComboBoxItem item &&
            item.Tag is string tagStr && int.TryParse(tagStr, out var idx))
        {
            widget.DriverNameFormat = (RelativeW.NameFormat)idx;
            widget.SaveSettings();
            _widgetManager.SaveCurrentLayout();
        }
    }

    // ── Field combos ────────────────────────────────────────────────

    private record FieldItem(string DisplayName, TelemetryField? Field);

    // ── Standings sync ────────────────────────────────────────────────

    private StandingsW? GetActiveStandingsWidget()
    {
        if (!_widgetManager.HasWidgetType(WidgetType.Standings)) return null;
        return _widgetManager.GetWidgetsByType(WidgetType.Standings).FirstOrDefault() as StandingsW;
    }

    private void SyncPanelToStandings()
    {
        var widget = GetActiveStandingsWidget();
        if (widget == null) return;

        _suppressControlEvents = true;
        try
        {
            SliderStandingsRows.Value = widget.MaxVisibleRows;
            TxtStandingsRows.Text = widget.MaxVisibleRows.ToString();
            ChkStdCarNumber.IsChecked = widget.ShowCarNumber;
            ChkStdInterval.IsChecked = widget.ShowInterval;
            ChkStdGapLeader.IsChecked = widget.ShowGapToLeader;
            ChkStdLastLap.IsChecked = widget.ShowLastLap;
            ChkStdBestLap.IsChecked = widget.ShowBestLap;
            ChkStdCurrentLap.IsChecked = widget.ShowCurrentLap;
            ChkStdPitCount.IsChecked = widget.ShowPitStopCount;
            ChkStdIRating.IsChecked = widget.ShowIRating;
            ChkStdLicense.IsChecked = widget.ShowLicense;
            ChkStdCarModel.IsChecked = widget.ShowCarModel;
            ChkStdPosDelta.IsChecked = widget.ShowPositionChange;
            ChkStdNationality.IsChecked = widget.ShowNationality;
            ChkStdClassPos.IsChecked = widget.ShowClassPosition;
            ChkStdAltRows.IsChecked = widget.ShowAlternateRowShading;
            ChkStdHighlight.IsChecked = widget.HighlightPlayer;
            ChkStdDimLapped.IsChecked = widget.DimLappedCars;
        }
        finally { _suppressControlEvents = false; }
    }

    private void StandingsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActiveStandingsWidget();
        if (widget == null) return;

        widget.ShowCarNumber = ChkStdCarNumber.IsChecked == true;
        widget.ShowInterval = ChkStdInterval.IsChecked == true;
        widget.ShowGapToLeader = ChkStdGapLeader.IsChecked == true;
        widget.ShowLastLap = ChkStdLastLap.IsChecked == true;
        widget.ShowBestLap = ChkStdBestLap.IsChecked == true;
        widget.ShowCurrentLap = ChkStdCurrentLap.IsChecked == true;
        widget.ShowPitStopCount = ChkStdPitCount.IsChecked == true;
        widget.ShowIRating = ChkStdIRating.IsChecked == true;
        widget.ShowLicense = ChkStdLicense.IsChecked == true;
        widget.ShowCarModel = ChkStdCarModel.IsChecked == true;
        widget.ShowPositionChange = ChkStdPosDelta.IsChecked == true;
        widget.ShowNationality = ChkStdNationality.IsChecked == true;
        widget.ShowClassPosition = ChkStdClassPos.IsChecked == true;
        widget.ShowAlternateRowShading = ChkStdAltRows.IsChecked == true;
        widget.HighlightPlayer = ChkStdHighlight.IsChecked == true;
        widget.DimLappedCars = ChkStdDimLapped.IsChecked == true;

        widget.RecalcLayout();
        widget.RecalcHeight();
        widget.SaveSettings();
    }

    private void StandingsControl_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActiveStandingsWidget();
        if (widget == null) return;

        widget.MaxVisibleRows = (int)SliderStandingsRows.Value;
        TxtStandingsRows.Text = widget.MaxVisibleRows.ToString();
        widget.RecalcHeight();
        widget.SaveSettings();
    }

    // ── Proximity Feed sync ─────────────────────────────────────────

    private ProxFeedW? GetActiveProximityFeedWidget()
    {
        return _widgetManager.GetWidgetsByType(WidgetType.ProximityFeed)
            .FirstOrDefault() as ProxFeedW;
    }

    private void SyncPanelToProximityFeed()
    {
        var widget = GetActiveProximityFeedWidget();
        if (widget == null) return;

        _suppressControlEvents = true;
        try
        {
            ChkProxDirection.IsChecked = widget.ShowDirection;
            ChkProxInterval.IsChecked = widget.ShowInterval;
            ChkProxGrowUp.IsChecked = widget.GrowUpward;
            ChkProxOvertaking.IsChecked = widget.ShowOvertakingAlert;
            ChkProxStartSequence.IsChecked = widget.ShowStartSequence;
            ChkProxCheckered.IsChecked = widget.ShowCheckeredFlag;
            ChkProxPaceFlags.IsChecked = widget.ShowPaceFlags;
            ChkProxDragMode.IsChecked = false; // always start unlocked

            // Detection range + max rows sliders
            SliderProxAhead.Value = widget.DetectionAheadSeconds;
            TxtProxAhead.Text = $"{(int)widget.DetectionAheadSeconds}s";
            SliderProxBehind.Value = widget.DetectionBehindSeconds;
            TxtProxBehind.Text = $"{(int)widget.DetectionBehindSeconds}s";
            SliderProxMaxRows.Value = widget.MaxVisibleEvents;
            TxtProxMaxRows.Text = $"{widget.MaxVisibleEvents}";

            // Opacity sliders (right panel)
            SliderProxFeedOpacity.Value = widget.Opacity * 100;
            TxtProxFeedOpacity.Text = $"{(int)(widget.Opacity * 100)}%";
            SliderProxFeedBgOpacity.Value = widget.BackgroundOpacity * 100;
            TxtProxFeedBgOpacity.Text = $"{(int)(widget.BackgroundOpacity * 100)}%";
        }
        finally { _suppressControlEvents = false; }
    }

    private void ProximityFeedToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActiveProximityFeedWidget();
        if (widget == null) return;

        widget.ShowDirection = ChkProxDirection.IsChecked == true;
        widget.ShowInterval = ChkProxInterval.IsChecked == true;
        widget.GrowUpward = ChkProxGrowUp.IsChecked == true;
        widget.ShowOvertakingAlert = ChkProxOvertaking.IsChecked == true;
        widget.ShowStartSequence = ChkProxStartSequence.IsChecked == true;
        widget.ShowCheckeredFlag = ChkProxCheckered.IsChecked == true;
        widget.ShowPaceFlags = ChkProxPaceFlags.IsChecked == true;
        widget.SaveSettings();
    }

    private void ProximityFeedDragToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActiveProximityFeedWidget();
        if (widget == null) return;

        bool enableDrag = ChkProxDragMode.IsChecked == true;
        // When drag mode is ON: unlock widget (IsHitTestVisible=true, _isLocked=false), show drag handle
        // When drag mode is OFF: lock widget back (click-through), hide drag handle
        widget.SetLocked(!enableDrag);
        widget.UpdateDragHandleVisibility(enableDrag);
    }

    private void ProximityFeedSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents) return;
        var widget = GetActiveProximityFeedWidget();
        if (widget == null) return;

        widget.DetectionAheadSeconds = (float)SliderProxAhead.Value;
        widget.DetectionBehindSeconds = (float)SliderProxBehind.Value;
        widget.MaxVisibleEvents = (int)SliderProxMaxRows.Value;

        TxtProxAhead.Text = $"{(int)SliderProxAhead.Value}s";
        TxtProxBehind.Text = $"{(int)SliderProxBehind.Value}s";
        TxtProxMaxRows.Text = $"{(int)SliderProxMaxRows.Value}";

        widget.SaveSettings();
    }
    private void ProxFeedOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents || TxtProxFeedOpacity == null) return;
        int pct = (int)e.NewValue;
        TxtProxFeedOpacity.Text = $"{pct}%";
        var widget = GetActiveProximityFeedWidget();
        if (widget != null) widget.Opacity = pct / 100.0;
    }

    private void ProxFeedBgOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents || TxtProxFeedBgOpacity == null) return;
        int pct = (int)e.NewValue;
        TxtProxFeedBgOpacity.Text = $"{pct}%";
        var widget = GetActiveProximityFeedWidget();
        if (widget != null) widget.BackgroundOpacity = pct / 100.0;
    }
    // ── Field combos ────────────────────────────────────────────────

    private void PopulateFieldCombos()
    {
        var fields = TelemetryFieldProvider.GetAllFields(includeAdvanced: false);
        var items = new List<FieldItem> { new("(None)", null) };
        items.AddRange(fields.Select(f => new FieldItem(f.DisplayName, f.Field)));

        foreach (var cbo in new[] { CboTopField, CboCenterField, CboBottomField, CboLeftField, CboRightField })
        {
            cbo.ItemsSource = items;
            cbo.DisplayMemberPath = "DisplayName";
            cbo.SelectedIndex = 0;
        }
    }

    private static void SetComboToField(ComboBox cbo, TelemetryField? field)
    {
        if (cbo.ItemsSource is not List<FieldItem> items) return;
        var match = items.FirstOrDefault(i => i.Field == field);
        cbo.SelectedItem = match ?? items[0];
    }

    private static TelemetryField? GetSelectedField(ComboBox cbo)
    {
        if (cbo.SelectedItem is FieldItem fi) return fi.Field;
        return null;
    }
}
