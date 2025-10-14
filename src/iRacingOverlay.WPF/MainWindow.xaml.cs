using System;using System;

using System.Windows;using System.Linq;

using System.Windows.Controls;using System.Windows;

using System.Windows.Media;using System.Windows.Controls;

using Microsoft.Extensions.DependencyInjection;using System.Windows.Media;

using iRacingOverlay.Core.Models;using Microsoft.Extensions.DependencyInjection;

using iRacingOverlay.Core.Services;using iRacingOverlay.Core.Models;

using iRacingOverlay.WPF.Services;using iRacingOverlay.Core.Services;

using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF;using iRacingOverlay.WPF.Services;



/// <summary>namespace iRacingOverlay.WPF;

/// MRT UI Main Window - Modern navigation-based interface

/// </summary>/// <summary>

public partial class MainWindow : Window/// Main window for managing overlay widgets

{/// </summary>

    private readonly WidgetManager _widgetManager;public partial class MainWindow : Window

    private readonly ITelemetryService _telemetryService;{

    private readonly IServiceProvider _services;    private readonly WidgetManager _widgetManager;

        private readonly ITelemetryService _telemetryService;

    // Track active navigation button

    private Button? _activeNavButton;    public MainWindow(IServiceProvider services)

    {

    public MainWindow(IServiceProvider services)        InitializeComponent();

    {

        InitializeComponent();        _widgetManager = services.GetRequiredService<WidgetManager>();

        _telemetryService = services.GetRequiredService<ITelemetryService>();

        _services = services;

        _widgetManager = services.GetRequiredService<WidgetManager>();        // Subscribe to widget events

        _telemetryService = services.GetRequiredService<ITelemetryService>();        _widgetManager.WidgetCreated += OnWidgetCreated;

        _widgetManager.WidgetRemoved += OnWidgetRemoved;

        // Subscribe to telemetry connection status        

        _telemetryService.StatusChanged += OnTelemetryStatusChanged;        // Subscribe to telemetry connection status

        _telemetryService.StatusChanged += OnTelemetryStatusChanged;

        // Set up hotkey (F12 for lock/unlock - will implement later)

        KeyDown += MainWindow_KeyDown;        // Set up hotkey (F12)

        KeyDown += MainWindow_KeyDown;

        // Initialize with Dashboard view

        _activeNavButton = DashboardButton;        // Initialize Data Widget ComboBoxes

        NavigateToDashboard();        InitializeDataWidgetComboBoxes();

                

        UpdateConnectionStatus();        // Attach real-time event handlers for Driving Widget

    }        ShowTopSection.Checked += DrivingWidgetConfig_Changed;

        ShowTopSection.Unchecked += DrivingWidgetConfig_Changed;

    #region Navigation        ShowCenterSection.Checked += DrivingWidgetConfig_Changed;

        ShowCenterSection.Unchecked += DrivingWidgetConfig_Changed;

    private void DashboardButton_Click(object sender, RoutedEventArgs e)        ShowBottomSection.Checked += DrivingWidgetConfig_Changed;

    {        ShowBottomSection.Unchecked += DrivingWidgetConfig_Changed;

        NavigateToDashboard();        TopSectionField.SelectionChanged += DrivingWidgetConfig_Changed;

        SetActiveButton(DashboardButton);        CenterSectionField.SelectionChanged += DrivingWidgetConfig_Changed;

    }        BottomSectionField.SelectionChanged += DrivingWidgetConfig_Changed;

        LeftSideField.SelectionChanged += DrivingWidgetConfig_Changed;

    private void OverlayButton_Click(object sender, RoutedEventArgs e)        RightSideField.SelectionChanged += DrivingWidgetConfig_Changed;

    {        WidgetSizeSlider.ValueChanged += DrivingWidgetSize_Changed;

        NavigateToOverlay();        

        SetActiveButton(OverlayButton);        // Attach real-time event handlers for Data Widget

    }        DataCell1Field.SelectionChanged += DataWidgetConfig_Changed;

        DataCell2Field.SelectionChanged += DataWidgetConfig_Changed;

    private void SettingsButton_Click(object sender, RoutedEventArgs e)        DataCell3Field.SelectionChanged += DataWidgetConfig_Changed;

    {        DataCell4Field.SelectionChanged += DataWidgetConfig_Changed;

        NavigateToSettings();        DataCell5Field.SelectionChanged += DataWidgetConfig_Changed;

        SetActiveButton(SettingsButton);        DataCell6Field.SelectionChanged += DataWidgetConfig_Changed;

    }        

        // Attach real-time event handlers for Fuel Widget

    private void NavigateToDashboard()        FuelWidgetSizeSlider.ValueChanged += FuelWidgetSizeSlider_ValueChanged;

    {

        // TODO: Create DashboardView and navigate to it        UpdateStatus();

        // For now, show placeholder    }

        ShowPlaceholder("📊 Dashboard", "Connection status and quick stats coming soon...");    

        StatusInfoText.Text = "Dashboard";    private void InitializeDataWidgetComboBoxes()

    }    {

        // Get all telemetry field values

    private void NavigateToOverlay()        var fields = Enum.GetValues(typeof(TelemetryField)).Cast<TelemetryField>().ToList();

    {        

        // TODO: Create OverlayView and navigate to it        // Populate all 6 cell ComboBoxes

        // For now, show placeholder        DataCell1Field.ItemsSource = fields;

        ShowPlaceholder("🎮 Overlay Manager", "Widget management interface coming soon...");        DataCell2Field.ItemsSource = fields;

        StatusInfoText.Text = "Overlay Manager";        DataCell3Field.ItemsSource = fields;

    }        DataCell4Field.ItemsSource = fields;

        DataCell5Field.ItemsSource = fields;

    private void NavigateToSettings()        DataCell6Field.ItemsSource = fields;

    {        

        // TODO: Create SettingsView and navigate to it        // Set default selections (None for all cells)

        // For now, show placeholder        DataCell1Field.SelectedItem = TelemetryField.None;

        ShowPlaceholder("⚙️ Settings", "Global settings coming soon...");        DataCell2Field.SelectedItem = TelemetryField.None;

        StatusInfoText.Text = "Settings";        DataCell3Field.SelectedItem = TelemetryField.None;

    }        DataCell4Field.SelectedItem = TelemetryField.None;

        DataCell5Field.SelectedItem = TelemetryField.None;

    private void ShowPlaceholder(string title, string message)        DataCell6Field.SelectedItem = TelemetryField.None;

    {    }

        var placeholder = new Grid

        {    private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)

            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A))    {

        };        if (e.Key == System.Windows.Input.Key.F12)

        {

        var stack = new StackPanel            _widgetManager.ToggleAllWidgets();

        {            UpdateStatus();

            VerticalAlignment = VerticalAlignment.Center,        }

            HorizontalAlignment = HorizontalAlignment.Center    }

        };

    private void MetricRadio_Checked(object sender, RoutedEventArgs e)

        var titleBlock = new TextBlock    {

        {        AppSettings.Instance.UseMetricUnits = true;

            Text = title,        AppSettings.Instance.NotifyChanged();

            FontSize = 32,    }

            FontWeight = FontWeights.Bold,

            Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0x80)), // Teal    private void ImperialRadio_Checked(object sender, RoutedEventArgs e)

            HorizontalAlignment = HorizontalAlignment.Center,    {

            Margin = new Thickness(0, 0, 0, 20)        AppSettings.Instance.UseMetricUnits = false;

        };        AppSettings.Instance.NotifyChanged();

    }

        var messageBlock = new TextBlock

        {    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)

            Text = message,    {

            FontSize = 18,        // Update opacity in settings

            Foreground = Brushes.White,        AppSettings.Instance.DefaultOpacity = e.NewValue;

            HorizontalAlignment = HorizontalAlignment.Center        AppSettings.Instance.NotifyChanged();

        };        

        // Update the display text

        stack.Children.Add(titleBlock);        if (OpacityValueText != null)

        stack.Children.Add(messageBlock);        {

        placeholder.Children.Add(stack);            OpacityValueText.Text = $"{(int)(e.NewValue * 100)}%";

        }

        ContentFrame.Content = placeholder;    }

    }

    private void LockWindows_Checked(object sender, RoutedEventArgs e)

    private void SetActiveButton(Button button)    {

    {        _widgetManager.LockAllWidgets(true);

        // Reset previous active button    }

        if (_activeNavButton != null)

        {    private void LockWindows_Unchecked(object sender, RoutedEventArgs e)

            _activeNavButton.Style = (Style)FindResource("MRT.Button.Navigation");    {

        }        _widgetManager.LockAllWidgets(false);

    }

        // Set new active button

        _activeNavButton = button;    // Driving Widget Activation Handlers

        button.Style = (Style)FindResource("MRT.Button.Navigation.Active");    private void ActivateDrivingWidget_Checked(object sender, RoutedEventArgs e)

    }    {

        try

    #endregion        {

            if (_widgetManager.HasWidgetType(WidgetType.GearGauge))

    #region Connection Status            {

                // Widget already exists, just show it

    private void OnTelemetryStatusChanged(object? sender, ConnectionStatus status)                var widgets = _widgetManager.GetWidgetsByType(WidgetType.GearGauge);

    {                foreach (var widget in widgets)

        Dispatcher.Invoke(() =>                {

        {                    widget.Show();

            UpdateConnectionStatus();                }

        });                return;

    }            }

            

    private void UpdateConnectionStatus()            // Create new widget

    {            _widgetManager.CreateWidget(WidgetType.GearGauge);

        var status = _telemetryService.GetStatus();            

                    // Apply current configuration immediately

        switch (status)            ApplyDrivingWidgetConfiguration();

        {        }

            case ConnectionStatus.Connected:        catch (Exception ex)

                ConnectionStatusText.Text = "🟢 Connected";        {

                ConnectionStatusText.Foreground = (Brush)FindResource("MRT.Status.Connected");            StatusText.Text = $"Error: {ex.Message}";

                break;            ActivateDrivingWidget.IsChecked = false;

            case ConnectionStatus.Connecting:        }

                ConnectionStatusText.Text = "🟡 Connecting...";    }

                ConnectionStatusText.Foreground = (Brush)FindResource("MRT.Status.Connecting");    

                break;    private void ActivateDrivingWidget_Unchecked(object sender, RoutedEventArgs e)

            case ConnectionStatus.Disconnected:    {

                ConnectionStatusText.Text = "🔴 Disconnected";        var widgets = _widgetManager.GetWidgetsByType(WidgetType.GearGauge);

                ConnectionStatusText.Foreground = (Brush)FindResource("MRT.Status.Disconnected");        foreach (var widget in widgets)

                break;        {

            default: // NotConnected            _widgetManager.RemoveWidget(widget.WidgetId);

                ConnectionStatusText.Text = "🔴 Not Connected";        }

                ConnectionStatusText.Foreground = (Brush)FindResource("MRT.Status.Disconnected");    }

                break;    

        }    // Real-time Driving Widget configuration update

    }    private void DrivingWidgetConfig_Changed(object sender, EventArgs e)

    {

    #endregion        ApplyDrivingWidgetConfiguration();

    }

    #region Hotkeys    

    private void DrivingWidgetSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)

    private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)    {

    {        // Update the label showing current size

        // F12 - Toggle lock/unlock widgets (will implement later)        if (WidgetSizeValue != null)

        if (e.Key == System.Windows.Input.Key.F12)        {

        {            WidgetSizeValue.Text = $"{(int)e.NewValue}px";

            // TODO: Implement widget lock toggle        }

            StatusInfoText.Text = "F12 pressed - Widget lock toggle (coming soon)";        

        }        ApplyDrivingWidgetConfiguration();

    }    }

    

    #endregion    private void ApplyDrivingWidgetConfiguration()

    {

    #region Cleanup        var drivingWidgets = _widgetManager?.GetWidgetsByType(WidgetType.GearGauge);

        if (drivingWidgets == null || !drivingWidgets.Any())

    protected override void OnClosed(EventArgs e)            return;

    {        

        // Clean up event subscriptions        foreach (var widget in drivingWidgets)

        _telemetryService.StatusChanged -= OnTelemetryStatusChanged;        {

                    if (widget is Widgets.GearGaugeWidget.GearGaugeWidget drivingWidget)

        // Remove all widgets            {

        _widgetManager.RemoveAllWidgets();                // Apply widget size

                        double size = WidgetSizeSlider?.Value ?? 200;

        // Disconnect telemetry service                drivingWidget.UpdateSize(size);

        _ = _telemetryService.DisconnectAsync();                

                        // Apply section visibility

        // Ensure application exits completely                bool showTop = ShowTopSection?.IsChecked ?? true;

        Application.Current.Shutdown();                bool showCenter = ShowCenterSection?.IsChecked ?? true;

                        bool showBottom = ShowBottomSection?.IsChecked ?? true;

        base.OnClosed(e);                drivingWidget.UpdateSectionVisibility(showTop, showCenter, showBottom);

    }                

                // Apply circle field selections

    #endregion                var topField = GetTelemetryFieldFromComboBox(TopSectionField);

}                var centerField = GetTelemetryFieldFromComboBox(CenterSectionField) ?? TelemetryField.Gear;

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