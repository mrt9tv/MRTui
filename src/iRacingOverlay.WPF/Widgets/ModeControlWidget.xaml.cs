using System;
using System.Windows;
using System.Windows.Input;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets;

/// <summary>
/// Mode Control Widget - Master control panel for switching operational modes
/// 
/// Features:
/// - Visual mode indicator (Setup Engineering, Strategy Scouting, Driving)
/// - Quick-switch buttons with hover effects
/// - Session info display (track, lap count)
/// - Sector learning status (tracks analyzed)
/// - Draggable, lockable overlay
/// </summary>
public partial class ModeControlWidget : WidgetBase
{
    private readonly ModeController _modeController;
    private readonly SectorTelemetryAnalyzer _sectorAnalyzer;
    
    public override WidgetType WidgetType => WidgetType.ModeControl;
    
    public ModeControlWidget(
        ITelemetryService telemetryService,
        ModeController modeController,
        SectorTelemetryAnalyzer sectorAnalyzer,
        WidgetConfig? config = null)
        : base(telemetryService, config)
    {
        _modeController = modeController ?? throw new ArgumentNullException(nameof(modeController));
        _sectorAnalyzer = sectorAnalyzer ?? throw new ArgumentNullException(nameof(sectorAnalyzer));
        
        InitializeComponent();
        
        // Subscribe to mode changes
        _modeController.ModeChanged += OnModeChanged;
        
        // Initialize UI with current mode
        UpdateModeIndicator(_modeController.CurrentMode);
        UpdateButtonStates(_modeController.CurrentMode);
    }
    
    /// <summary>
    /// Update UI with telemetry data
    /// </summary>
    protected override void UpdateUI(TelemetryData telemetry)
    {
        _lastTelemetryData = telemetry;
        
        // Update session info
        if (!string.IsNullOrEmpty(telemetry.TrackName))
        {
            SessionInfo.Text = $"Session: {telemetry.TrackName} | Lap {telemetry.Lap}";
        }
    }
    
    /// <summary>
    /// Setup Engineering button clicked
    /// </summary>
    private void SetupButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Check if transition is allowed
            if (!_modeController.CanTransitionTo(OperationalMode.SetupEngineering))
            {
                ShowTransitionWarning(OperationalMode.SetupEngineering);
                return;
            }
            
            // Confirm transition if in Driving mode during session
            if (_modeController.CurrentMode == OperationalMode.Driving && IsInActiveSession())
            {
                var result = MessageBox.Show(
                    "Switching to Setup Engineering will disable real-time overlays.\nContinue?",
                    "Mode Switch Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            
            // Switch mode
            _modeController.SwitchMode(OperationalMode.SetupEngineering);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show($"Cannot switch to Setup Engineering:\n{ex.Message}", "Mode Switch Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    /// <summary>
    /// Strategy Scouting button clicked
    /// </summary>
    private void StrategyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_modeController.CanTransitionTo(OperationalMode.StrategyScouting))
            {
                ShowTransitionWarning(OperationalMode.StrategyScouting);
                return;
            }
            
            _modeController.SwitchMode(OperationalMode.StrategyScouting);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show($"Cannot switch to Strategy Scouting:\n{ex.Message}", "Mode Switch Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    /// <summary>
    /// Driving Mode button clicked
    /// </summary>
    private void DrivingButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_modeController.CanTransitionTo(OperationalMode.Driving))
            {
                ShowTransitionWarning(OperationalMode.Driving);
                return;
            }
            
            _modeController.SwitchMode(OperationalMode.Driving);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show($"Cannot switch to Driving Mode:\n{ex.Message}", "Mode Switch Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    /// <summary>
    /// Handle mode change event
    /// </summary>
    private void OnModeChanged(object? sender, ModeChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateModeIndicator(e.NewMode);
            UpdateButtonStates(e.NewMode);
            
            // Show transition notification
            ShowModeChangeNotification(e.PreviousMode, e.NewMode);
        });
    }
    
    /// <summary>
    /// Update mode indicator text and color
    /// </summary>
    private void UpdateModeIndicator(OperationalMode mode)
    {
        switch (mode)
        {
            case OperationalMode.SetupEngineering:
                ModeIndicator.Text = "🔧 SETUP ENGINEERING";
                ModeIndicator.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 107, 0));
                break;
            case OperationalMode.StrategyScouting:
                ModeIndicator.Text = "📊 STRATEGY SCOUTING";
                ModeIndicator.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 102, 255));
                break;
            case OperationalMode.Driving:
                ModeIndicator.Text = "🏁 DRIVING";
                ModeIndicator.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 255, 0));
                break;
        }
    }
    
    /// <summary>
    /// Update button visual states (highlight active mode)
    /// </summary>
    private void UpdateButtonStates(OperationalMode mode)
    {
        // Reset all buttons to semi-transparent
        SetupButton.Opacity = 0.6;
        StrategyButton.Opacity = 0.6;
        DrivingButton.Opacity = 0.6;
        
        // Highlight active mode
        switch (mode)
        {
            case OperationalMode.SetupEngineering:
                SetupButton.Opacity = 1.0;
                break;
            case OperationalMode.StrategyScouting:
                StrategyButton.Opacity = 1.0;
                break;
            case OperationalMode.Driving:
                DrivingButton.Opacity = 1.0;
                break;
        }
    }
    
    /// <summary>
    /// Show transition warning dialog
    /// </summary>
    private void ShowTransitionWarning(OperationalMode targetMode)
    {
        string reason = _modeController.GetTransitionBlockReason(_modeController.CurrentMode, targetMode);
        MessageBox.Show(
            $"Cannot switch to {targetMode}:\n\n{reason}",
            "Mode Transition Blocked",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
    
    /// <summary>
    /// Show mode change notification (brief toast)
    /// </summary>
    private void ShowModeChangeNotification(OperationalMode previous, OperationalMode current)
    {
        // TODO: Implement toast notification system
        // For now, just log
        Console.WriteLine($"[Mode Switch] {previous} → {current}");
    }
    
    /// <summary>
    /// Check if currently in an active session
    /// </summary>
    private bool IsInActiveSession()
    {
        return _lastTelemetryData != null && 
               _lastTelemetryData.Lap > 0 &&
               _lastTelemetryData.SessionTimeRemain > 0;
    }
    
    /// <summary>
    /// Cleanup
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        _modeController.ModeChanged -= OnModeChanged;
        base.OnClosed(e);
    }
}
