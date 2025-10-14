using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Services;

namespace iRacingOverlay.WPF;

/// <summary>
/// Main window for managing overlay widgets
/// </summary>
public partial class MainWindow : Window
{
    private readonly WidgetManager _widgetManager;
    private readonly ITelemetryService _telemetryService;

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();

        _widgetManager = services.GetRequiredService<WidgetManager>();
        _telemetryService = services.GetRequiredService<ITelemetryService>();

        // Subscribe to widget events
        _widgetManager.WidgetCreated += OnWidgetCreated;
        _widgetManager.WidgetRemoved += OnWidgetRemoved;
        
        // Subscribe to telemetry connection status
        _telemetryService.StatusChanged += OnTelemetryStatusChanged;

        // Set up hotkey (F12)
        KeyDown += MainWindow_KeyDown;

        // Initialize Data Widget ComboBoxes
        InitializeDataWidgetComboBoxes();
        
        // Attach real-time event handlers for Driving Widget
        ShowTopSection.Checked += DrivingWidgetConfig_Changed;
        ShowTopSection.Unchecked += DrivingWidgetConfig_Changed;
        ShowCenterSection.Checked += DrivingWidgetConfig_Changed;
        ShowCenterSection.Unchecked += DrivingWidgetConfig_Changed;
        ShowBottomSection.Checked += DrivingWidgetConfig_Changed;
        ShowBottomSection.Unchecked += DrivingWidgetConfig_Changed;
        TopSectionField.SelectionChanged += DrivingWidgetConfig_Changed;
        CenterSectionField.SelectionChanged += DrivingWidgetConfig_Changed;
        BottomSectionField.SelectionChanged += DrivingWidgetConfig_Changed;
        LeftSideField.SelectionChanged += DrivingWidgetConfig_Changed;
        RightSideField.SelectionChanged += DrivingWidgetConfig_Changed;
        WidgetSizeSlider.ValueChanged += DrivingWidgetSize_Changed;
        
        // Attach real-time event handlers for Data Widget
        DataCell1Field.SelectionChanged += DataWidgetConfig_Changed;
        DataCell2Field.SelectionChanged += DataWidgetConfig_Changed;
        DataCell3Field.SelectionChanged += DataWidgetConfig_Changed;
        DataCell4Field.SelectionChanged += DataWidgetConfig_Changed;
        DataCell5Field.SelectionChanged += DataWidgetConfig_Changed;
        DataCell6Field.SelectionChanged += DataWidgetConfig_Changed;
        
        // Attach real-time event handlers for Fuel Widget
        FuelWidgetSizeSlider.ValueChanged += FuelWidgetSizeSlider_ValueChanged;

        UpdateStatus();
    }
    
    private void InitializeDataWidgetComboBoxes()
    {
        // Get all telemetry field values
        var fields = Enum.GetValues(typeof(TelemetryField)).Cast<TelemetryField>().ToList();
        
        // Populate all 6 cell ComboBoxes
        DataCell1Field.ItemsSource = fields;
        DataCell2Field.ItemsSource = fields;
        DataCell3Field.ItemsSource = fields;
        DataCell4Field.ItemsSource = fields;
        DataCell5Field.ItemsSource = fields;
        DataCell6Field.ItemsSource = fields;
        
        // Set default selections (None for all cells)
        DataCell1Field.SelectedItem = TelemetryField.None;
        DataCell2Field.SelectedItem = TelemetryField.None;
        DataCell3Field.SelectedItem = TelemetryField.None;
        DataCell4Field.SelectedItem = TelemetryField.None;
        DataCell5Field.SelectedItem = TelemetryField.None;
        DataCell6Field.SelectedItem = TelemetryField.None;
    }

    private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F12)
        {
            _widgetManager.ToggleAllWidgets();
            UpdateStatus();
        }
    }

    private void MetricRadio_Checked(object sender, RoutedEventArgs e)
    {
        AppSettings.Instance.UseMetricUnits = true;
        AppSettings.Instance.NotifyChanged();
    }

    private void ImperialRadio_Checked(object sender, RoutedEventArgs e)
    {
        AppSettings.Instance.UseMetricUnits = false;
        AppSettings.Instance.NotifyChanged();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Update opacity in settings
        AppSettings.Instance.DefaultOpacity = e.NewValue;
        AppSettings.Instance.NotifyChanged();
        
        // Update the display text
        if (OpacityValueText != null)
        {
            OpacityValueText.Text = $"{(int)(e.NewValue * 100)}%";
        }
    }

    private void LockWindows_Checked(object sender, RoutedEventArgs e)
    {
        _widgetManager.LockAllWidgets(true);
    }

    private void LockWindows_Unchecked(object sender, RoutedEventArgs e)
    {
        _widgetManager.LockAllWidgets(false);
    }

    // Driving Widget Activation Handlers
    private void ActivateDrivingWidget_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_widgetManager.HasWidgetType(WidgetType.GearGauge))
            {
                // Widget already exists, just show it
                var widgets = _widgetManager.GetWidgetsByType(WidgetType.GearGauge);
                foreach (var widget in widgets)
                {
                    widget.Show();
                }
                return;
            }
            
            // Create new widget
            _widgetManager.CreateWidget(WidgetType.GearGauge);
            
            // Apply current configuration immediately
            ApplyDrivingWidgetConfiguration();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            ActivateDrivingWidget.IsChecked = false;
        }
    }
    
    private void ActivateDrivingWidget_Unchecked(object sender, RoutedEventArgs e)
    {
        var widgets = _widgetManager.GetWidgetsByType(WidgetType.GearGauge);
        foreach (var widget in widgets)
        {
            _widgetManager.RemoveWidget(widget.WidgetId);
        }
    }
    
    // Real-time Driving Widget configuration update
    private void DrivingWidgetConfig_Changed(object sender, EventArgs e)
    {
        ApplyDrivingWidgetConfiguration();
    }
    
    private void DrivingWidgetSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Update the label showing current size
        if (WidgetSizeValue != null)
        {
            WidgetSizeValue.Text = $"{(int)e.NewValue}px";
        }
        
        ApplyDrivingWidgetConfiguration();
    }
    
    private void ApplyDrivingWidgetConfiguration()
    {
        var drivingWidgets = _widgetManager?.GetWidgetsByType(WidgetType.GearGauge);
        if (drivingWidgets == null || !drivingWidgets.Any())
            return;
        
        foreach (var widget in drivingWidgets)
        {
            if (widget is Widgets.GearGaugeWidget.GearGaugeWidget drivingWidget)
            {
                // Apply widget size
                double size = WidgetSizeSlider?.Value ?? 200;
                drivingWidget.UpdateSize(size);
                
                // Apply section visibility
                bool showTop = ShowTopSection?.IsChecked ?? true;
                bool showCenter = ShowCenterSection?.IsChecked ?? true;
                bool showBottom = ShowBottomSection?.IsChecked ?? true;
                drivingWidget.UpdateSectionVisibility(showTop, showCenter, showBottom);
                
                // Apply circle field selections
                var topField = GetTelemetryFieldFromComboBox(TopSectionField);
                var centerField = GetTelemetryFieldFromComboBox(CenterSectionField) ?? TelemetryField.Gear;
                var bottomField = GetTelemetryFieldFromComboBox(BottomSectionField);
                drivingWidget.UpdateDisplayFields(topField, centerField, bottomField);
                
                // Apply side box field selections
                var leftField = GetTelemetryFieldFromComboBox(LeftSideField);
                var rightField = GetTelemetryFieldFromComboBox(RightSideField);
                drivingWidget.UpdateSideBoxes(leftField, rightField);
            }
        }
    }
    
    // Data Widget Activation Handlers
    private void ActivateDataWidget_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            // Create new widget
            _widgetManager.CreateWidget(WidgetType.Data);
            
            // Apply current configuration immediately
            ApplyDataWidgetConfiguration();
            
            StatusText.Text = "Status: Data widget activated!";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            ActivateDataWidget.IsChecked = false;
        }
    }
    
    private void ActivateDataWidget_Unchecked(object sender, RoutedEventArgs e)
    {
        var widgets = _widgetManager.GetWidgetsByType(WidgetType.Data);
        foreach (var widget in widgets)
        {
            _widgetManager.RemoveWidget(widget.WidgetId);
        }
        StatusText.Text = "Status: Data widget deactivated";
    }
    
    // Real-time Data Widget configuration update
    private void DataWidgetConfig_Changed(object sender, EventArgs e)
    {
        ApplyDataWidgetConfiguration();
    }
    
    private void DataWidgetSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Update size text display (maintain 4:3 aspect ratio)
        if (DataWidgetSizeText != null)
        {
            double width = e.NewValue;
            double height = width * 0.75; // 4:3 aspect ratio
            DataWidgetSizeText.Text = $"{(int)width} x {(int)height}";
        }
        
        // Apply size to all Data Widgets in real-time
        ApplyDataWidgetConfiguration();
    }
    
    private void ApplyDataWidgetConfiguration()
    {
        var dataWidgets = _widgetManager?.GetWidgetsByType(WidgetType.Data);
        if (dataWidgets == null || !dataWidgets.Any())
            return;
        
        // Get selected fields from ComboBoxes
        var field1 = GetTelemetryFieldFromDataComboBox(DataCell1Field);
        var field2 = GetTelemetryFieldFromDataComboBox(DataCell2Field);
        var field3 = GetTelemetryFieldFromDataComboBox(DataCell3Field);
        var field4 = GetTelemetryFieldFromDataComboBox(DataCell4Field);
        var field5 = GetTelemetryFieldFromDataComboBox(DataCell5Field);
        var field6 = GetTelemetryFieldFromDataComboBox(DataCell6Field);
        
        // Get size from slider
        double width = DataWidgetSizeSlider?.Value ?? 400;
        double height = width * 0.75; // 4:3 aspect ratio
        
        foreach (var widget in dataWidgets)
        {
            if (widget is Widgets.DataWidget.DataWidget dataWidget)
            {
                dataWidget.UpdateCellFields(field1, field2, field3, field4, field5, field6);
                dataWidget.UpdateSize(width, height);
            }
        }
    }
    
    private TelemetryField GetTelemetryFieldFromDataComboBox(ComboBox? comboBox)
    {
        if (comboBox == null || comboBox.SelectedItem == null)
            return TelemetryField.None;
        
        var selectedText = comboBox.SelectedItem.ToString();
        
        // Try to parse the enum directly
        if (Enum.TryParse<TelemetryField>(selectedText, out var field))
        {
            return field;
        }
        
        return TelemetryField.None;
    }
    
    private TelemetryField? GetTelemetryFieldFromComboBox(ComboBox? comboBox)
    {
        if (comboBox == null || comboBox.SelectedItem == null)
            return null;
        
        var selectedText = ((ComboBoxItem)comboBox.SelectedItem).Content.ToString();
        
        return selectedText switch
        {
            "None" => null,
            "Gear" => TelemetryField.Gear,
            "Speed" => TelemetryField.Speed,
            "RPM" => TelemetryField.RPM,
            "Throttle %" => TelemetryField.Throttle,
            "Brake %" => TelemetryField.Brake,
            "Clutch %" => TelemetryField.Clutch,
            "Fuel Level" => TelemetryField.FuelLevel,
            "Water Temp" => TelemetryField.WaterTemp,
            "Oil Temp" => TelemetryField.OilTemp,
            _ => null
        };
    }
    
    // Fuel Widget Activation Handlers
    private void ActivateFuelWidget_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Creating Fuel Calculator widget...";
            
            // Create new widget
            var widget = _widgetManager.CreateWidget(WidgetType.Fuel);
            
            StatusText.Text = $"Fuel widget created (ID: {widget.WidgetId})";
            
            // Apply current size configuration immediately
            ApplyFuelWidgetConfiguration();
            
            StatusText.Text = "Status: Fuel Calculator widget activated!";
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error creating Fuel widget: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
            StatusText.Text = $"Error: {ex.Message}";
            MessageBox.Show(errorMsg, "Widget Creation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            ActivateFuelWidget.IsChecked = false;
        }
    }
    
    private void ActivateFuelWidget_Unchecked(object sender, RoutedEventArgs e)
    {
        var widgets = _widgetManager.GetWidgetsByType(WidgetType.Fuel);
        foreach (var widget in widgets)
        {
            _widgetManager.RemoveWidget(widget.WidgetId);
        }
        StatusText.Text = "Status: Fuel Calculator widget deactivated";
    }
    
    private void FuelWidgetSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Update size text display
        if (FuelWidgetSizeText != null)
        {
            FuelWidgetSizeText.Text = $"{(int)e.NewValue}%";
        }
        
        // Apply size to all Fuel Widgets in real-time
        ApplyFuelWidgetConfiguration();
    }
    
    private void ResetFuelCalculations_Click(object sender, RoutedEventArgs e)
    {
        var fuelWidgets = _widgetManager?.GetWidgetsByType(WidgetType.Fuel);
        if (fuelWidgets == null || !fuelWidgets.Any())
        {
            StatusText.Text = "Status: No Fuel widget active to reset";
            return;
        }
        
        foreach (var widget in fuelWidgets)
        {
            if (widget is Widgets.FuelWidget fuelWidget)
            {
                fuelWidget.ResetCalculations();
            }
        }
        
        StatusText.Text = "Status: Fuel calculations reset!";
    }
    
    private void ApplyFuelWidgetConfiguration()
    {
        var fuelWidgets = _widgetManager?.GetWidgetsByType(WidgetType.Fuel);
        if (fuelWidgets == null || !fuelWidgets.Any())
            return;
        
        // Get size percentage from slider
        double sizePercent = FuelWidgetSizeSlider?.Value ?? 100;
        
        // Save to AppSettings for persistence
        AppSettings.Instance.FuelWidgetSize = (int)sizePercent;
        
        foreach (var widget in fuelWidgets)
        {
            if (widget is Widgets.FuelWidget fuelWidget)
            {
                // Apply size scaling
                double scale = sizePercent / 100.0;
                fuelWidget.Width = 280 * scale;
                fuelWidget.Height = 220 * scale;
            }
        }
    }

    private void ToggleAllButton_Click(object sender, RoutedEventArgs e)
    {
        _widgetManager.ToggleAllWidgets();
        UpdateStatus();
    }

    private void RemoveAllButton_Click(object sender, RoutedEventArgs e)
    {
        _widgetManager.RemoveAllWidgets();
        StatusText.Text = "Status: All widgets removed";
    }

    private void OnWidgetCreated(object? sender, Core.WidgetBase widget)
    {
        UpdateStatus();
    }

    private void OnWidgetRemoved(object? sender, Guid widgetId)
    {
        UpdateStatus();
    }
    
    private void OnTelemetryStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateConnectionStatus(e.Status);
        });
    }
    
    private void UpdateConnectionStatus(ConnectionStatus status)
    {
        string statusText;
        Brush statusColor;
        
        switch (status)
        {
            case ConnectionStatus.Connected:
                statusText = "🟢 iRacing Connected";
                statusColor = new SolidColorBrush(Color.FromRgb(0, 200, 0));
                break;
            case ConnectionStatus.Connecting:
                statusText = "🟡 Connecting to iRacing...";
                statusColor = new SolidColorBrush(Color.FromRgb(255, 200, 0));
                break;
            case ConnectionStatus.Disconnected:
                statusText = "🔴 iRacing Not Running";
                statusColor = new SolidColorBrush(Color.FromRgb(200, 0, 0));
                break;
            default:
                statusText = "⚪ Unknown Status";
                statusColor = new SolidColorBrush(Colors.Gray);
                break;
        }
        
        // Update status text with connection info
        var widgetCount = _widgetManager.GetWidgetCount();
        StatusText.Text = $"{statusText} | Active Widgets: {widgetCount}";
        StatusText.Foreground = statusColor;
    }

    private void UpdateStatus()
    {
        var count = _widgetManager.GetWidgetCount();
        WidgetCountText.Text = $"Active Widgets: {count}";
        
        if (count == 0)
        {
            // Keep connection status, just update widget count
            var currentStatus = StatusText.Text.Split('|')[0].Trim();
            StatusText.Text = $"{currentStatus} | No widgets active";
        }
        else
        {
            var currentStatus = StatusText.Text.Split('|')[0].Trim();
            StatusText.Text = $"{currentStatus} | {count} widget(s) active - Press F12 to toggle";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Clean up event subscriptions
        _widgetManager.WidgetCreated -= OnWidgetCreated;
        _widgetManager.WidgetRemoved -= OnWidgetRemoved;
        _telemetryService.StatusChanged -= OnTelemetryStatusChanged;
        
        // Remove all widgets
        _widgetManager.RemoveAllWidgets();
        
        // Disconnect telemetry service
        _ = _telemetryService.DisconnectAsync();
        
        // Ensure application exits completely
        Application.Current.Shutdown();
        
        base.OnClosed(e);
    }
}