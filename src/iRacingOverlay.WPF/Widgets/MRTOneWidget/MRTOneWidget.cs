using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Utils;
using ConnectionStatus = iRacingOverlay.Core.Models.ConnectionStatus;

namespace iRacingOverlay.WPF.Widgets.MRTOneWidget;

/// <summary>
/// MRT One - Compact circular gauge widget showing Gear (center), Speed (top), and RPM (bottom)
/// Racelabs-inspired design with intelligent shift point detection and theme colors
/// </summary>
public class MRTOneWidget : WidgetBase
{
    private readonly Grid _mainGrid;
    private readonly Ellipse _gaugeCircle;
    
    // Dynamic text displays for top/center/bottom
    private readonly StackPanel _topStack;
    private readonly TextBlock _topValueText;
    private readonly TextBlock _topLabelText;
    private readonly TextBlock _centerValueText;
    private readonly StackPanel _bottomStack;
    private readonly TextBlock _bottomValueText;
    private readonly TextBlock _bottomLabelText;
    
    // Left and right side data boxes
    private readonly StackPanel _leftBox;
    private readonly TextBlock _leftLabelText;
    private readonly TextBlock _leftValueText;
    private readonly StackPanel _rightBox;
    private readonly TextBlock _rightLabelText;
    private readonly TextBlock _rightValueText;
    
    // Data bindings
    private readonly WidgetDataBinding _dataBinding;
    private TelemetryField? _leftField = null;
    private TelemetryField? _rightField = null;
    
    // Blinking timer for critical warnings
    private readonly DispatcherTimer _blinkTimer;
    private bool _blinkState = false;
    
    // Theme colors
    private System.Windows.Media.Color _primaryColor;   // Teal #008080
    private System.Windows.Media.Color _secondaryColor; // Orange #FF8000
    
    // Configurable opacity
    private double _backgroundOpacity = 0.85;
    
    public MRTOneWidget(ITelemetryService telemetryService) : base(telemetryService)
    {
        // Load theme colors from settings
        _primaryColor = (Color)ColorConverter.ConvertFromString(AppSettings.Instance.PrimaryColor);
        _secondaryColor = (Color)ColorConverter.ConvertFromString(AppSettings.Instance.SecondaryColor);
        _backgroundOpacity = AppSettings.Instance.DefaultOpacity;
        
        // Configure default data binding
        _dataBinding = new WidgetDataBinding
        {
            PrimaryField = TelemetryField.Gear,
            SecondaryField = TelemetryField.Speed,
            TertiaryField = TelemetryField.RPM,
            PrimaryDisplayOptions = TelemetryDataMapper.GetDefaultDisplayOptions(TelemetryField.Gear),
            SecondaryDisplayOptions = TelemetryDataMapper.GetDefaultDisplayOptions(TelemetryField.Speed),
            TertiaryDisplayOptions = TelemetryDataMapper.GetDefaultDisplayOptions(TelemetryField.RPM)
        };
        
        // Set window properties (base size - will be scaled via LayoutTransform)
        Width = 200;
        Height = 200;
        Title = "MRT One";
        
        // Create main grid (no background - transparent)
        // Use fixed size of 200x200 for content, then scale via LayoutTransform
        _mainGrid = new Grid
        {
            Background = Brushes.Transparent,
            Width = 200,
            Height = 200
        };
        
        // Create circular gauge that fills the entire window
        _gaugeCircle = new Ellipse
        {
            Stroke = new SolidColorBrush(_primaryColor), // Teal border
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.FromArgb(
                (byte)(255 * _backgroundOpacity), 20, 20, 20)), // Dark gray with opacity
            Margin = new Thickness(5) // Small margin for stroke
        };
        _mainGrid.Children.Add(_gaugeCircle);
        
        // Top section: Value + Label (positioned absolutely in top portion of circle)
        _topStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 30, 0, 0) // Position inside circle (15% of default 200px size)
        };
        
        _topValueText = new TextBlock
        {
            Text = "0",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(_primaryColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        _topStack.Children.Add(_topValueText);
        
        _topLabelText = new TextBlock
        {
            Text = "km/h",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            FontWeight = FontWeights.Normal,
            Foreground = new SolidColorBrush(_primaryColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        _topStack.Children.Add(_topLabelText);
        
        _mainGrid.Children.Add(_topStack);
        
        // Center section: Value only (typically Gear - no label needed)
        _centerValueText = new TextBlock
        {
            Text = "N",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 56,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(_primaryColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        _mainGrid.Children.Add(_centerValueText);
        
        // Bottom section: Value + Label (positioned absolutely in bottom portion of circle)
        _bottomStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 30) // Position inside circle (15% of default 200px size)
        };
        
        _bottomValueText = new TextBlock
        {
            Text = "0",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(_primaryColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        _bottomStack.Children.Add(_bottomValueText);
        
        _bottomLabelText = new TextBlock
        {
            Text = "RPM",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            FontWeight = FontWeights.Normal,
            Foreground = new SolidColorBrush(_primaryColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        _bottomStack.Children.Add(_bottomLabelText);
        
        _mainGrid.Children.Add(_bottomStack);
        
        // Left side data box (optional)
        _leftBox = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(10, 0, 0, 0),
            Visibility = Visibility.Collapsed // Hidden by default
        };
        
        _leftLabelText = new TextBlock
        {
            Text = "",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 9,
            FontWeight = FontWeights.Normal,
            Foreground = new SolidColorBrush(_primaryColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        _leftBox.Children.Add(_leftLabelText);
        
        _leftValueText = new TextBlock
        {
            Text = "--",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        _leftBox.Children.Add(_leftValueText);
        
        _mainGrid.Children.Add(_leftBox);
        
        // Right side data box (optional)
        _rightBox = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 10, 0),
            Visibility = Visibility.Collapsed // Hidden by default
        };
        
        _rightLabelText = new TextBlock
        {
            Text = "",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 9,
            FontWeight = FontWeights.Normal,
            Foreground = new SolidColorBrush(_primaryColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        _rightBox.Children.Add(_rightLabelText);
        
        _rightValueText = new TextBlock
        {
            Text = "--",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        _rightBox.Children.Add(_rightValueText);
        
        _mainGrid.Children.Add(_rightBox);
        
        // No border - just the content
        Content = _mainGrid;
        
        // Subscribe to SizeChanged to update scale transform
        SizeChanged += OnWidgetSizeChanged;
        
        // Setup blinking timer for critical warnings (500ms interval)
        _blinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _blinkTimer.Tick += OnBlinkTimerTick;
        _blinkTimer.Start();
        
        // Subscribe to settings changes
        AppSettings.Instance.SettingsChanged += OnSettingsChanged;
    }
    
    public override WidgetType WidgetType => WidgetType.MRTOne;
    
    /// <summary>
    /// Handle widget resize by scaling the content via LayoutTransform.
    /// This prevents content shift by maintaining relative positions of all elements.
    /// </summary>
    private void OnWidgetSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Calculate scale factors based on original 200x200 design
        double scaleX = ActualWidth / 200.0;
        double scaleY = ActualHeight / 200.0;
        
        // Use uniform scale (smallest of the two to maintain aspect ratio)
        double scale = Math.Min(scaleX, scaleY);
        
        // Apply scale transform to the entire grid
        _mainGrid.LayoutTransform = new ScaleTransform(scale, scale);
    }
    
    private void OnBlinkTimerTick(object? sender, EventArgs e)
    {
        _blinkState = !_blinkState;
        
        // Apply blink effect to left box if showing critical fuel
        if (_leftField == TelemetryField.FuelLevel && _leftValueText.Foreground is SolidColorBrush leftBrush && leftBrush.Color == Colors.Red)
        {
            _leftValueText.Opacity = _blinkState ? 1.0 : 0.3;
        }
        else
        {
            _leftValueText.Opacity = 1.0;
        }
        
        // Apply blink effect to right box if showing critical fuel
        if (_rightField == TelemetryField.FuelLevel && _rightValueText.Foreground is SolidColorBrush rightBrush && rightBrush.Color == Colors.Red)
        {
            _rightValueText.Opacity = _blinkState ? 1.0 : 0.3;
        }
        else
        {
            _rightValueText.Opacity = 1.0;
        }
    }
    
    /// <summary>
    /// Update which telemetry fields are displayed in circle sections (live update)
    /// </summary>
    public void UpdateDisplayFields(TelemetryField? topField, TelemetryField centerField, TelemetryField? bottomField)
    {
        _dataBinding.SecondaryField = topField;  // Top section (speed)
        _dataBinding.PrimaryField = centerField;  // Center section (gear)
        _dataBinding.TertiaryField = bottomField; // Bottom section (RPM)
        
        // Force immediate UI refresh with last telemetry data
        if (_lastTelemetryData != null)
        {
            UpdateUI(_lastTelemetryData);
        }
    }
    
    /// <summary>
    /// Update left and right side data boxes (live update)
    /// </summary>
    public void UpdateSideBoxes(TelemetryField? leftField, TelemetryField? rightField)
    {
        _leftField = leftField;
        _rightField = rightField;
        
        // Show/hide boxes based on field selection
        _leftBox.Visibility = _leftField.HasValue ? Visibility.Visible : Visibility.Collapsed;
        _rightBox.Visibility = _rightField.HasValue ? Visibility.Visible : Visibility.Collapsed;
        
        // Update labels
        if (_leftField.HasValue)
            _leftLabelText.Text = GetFieldLabel(_leftField.Value);
        if (_rightField.HasValue)
            _rightLabelText.Text = GetFieldLabel(_rightField.Value);
        
        // Force immediate UI refresh
        if (_lastTelemetryData != null)
        {
            UpdateUI(_lastTelemetryData);
        }
    }
    
    private string GetFieldLabel(TelemetryField field)
    {
        string fieldKey = field.ToString();
        
        // Use custom label from settings if available, otherwise use default
        if (AppSettings.Instance.CustomLabels.TryGetValue(fieldKey, out string? customLabel))
        {
            return customLabel;
        }
        
        // Fallback to default labels
        return field switch
        {
            TelemetryField.Throttle => "THRTL",
            TelemetryField.Brake => "BRAKE",
            TelemetryField.Clutch => "CLUTCH",
            TelemetryField.FuelLevel => "FUEL",
            TelemetryField.WaterTemp => "WATER",
            TelemetryField.OilTemp => "OIL",
            TelemetryField.Speed => "SPEED",
            TelemetryField.RPM => "RPM",
            TelemetryField.Gear => "GEAR",
            _ => field.ToString().ToUpper()
        };
    }
    
    private string FormatFieldValue(TelemetryField field, object value, TelemetryData data)
    {
        return field switch
        {
            TelemetryField.Throttle when value is float throttle => $"{(int)(throttle * 100)}%",
            TelemetryField.Brake when value is float brake => $"{(int)(brake * 100)}%",
            TelemetryField.Clutch when value is float clutch => $"{(int)(clutch * 100)}%",
            TelemetryField.FuelLevel when value is float fuel => 
                AppSettings.Instance.UseMetricUnits 
                    ? $"{fuel.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} L" 
                    : $"{(fuel * 0.264172f).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} gal",
            TelemetryField.WaterTemp when value is float temp => 
                AppSettings.Instance.UseMetricUnits ? $"{(int)temp}°C" : $"{(int)(temp * 9 / 5 + 32)}°F",
            TelemetryField.OilTemp when value is float temp => 
                AppSettings.Instance.UseMetricUnits ? $"{(int)temp}°C" : $"{(int)(temp * 9 / 5 + 32)}°F",
            TelemetryField.Speed when value is float speedMs =>
                AppSettings.Instance.UseMetricUnits ? $"{(int)(speedMs * 3.6f)}" : $"{(int)(speedMs * 2.23694f)}",
            TelemetryField.RPM when value is float rpm => $"{(int)rpm}",
            TelemetryField.Gear => data.Gear switch
            {
                -1 => "R",
                0 => "N",
                _ => data.Gear.ToString()
            },
            _ => value?.ToString() ?? "-"
        };
    }
    
    private System.Windows.Media.Color GetValueColor(TelemetryField field, object value, TelemetryData data)
    {
        if (value is not float floatValue)
            return _primaryColor; // Default to teal
        
        return field switch
        {
            // Water temp: Red >100°C, Yellow >90°C (or Red >212°F, Yellow >194°F)
            TelemetryField.WaterTemp when floatValue > 100 => Colors.Red,
            TelemetryField.WaterTemp when floatValue > 90 => Colors.Yellow,
            
            // Oil temp: Red >120°C, Yellow >110°C (or Red >248°F, Yellow >230°F)
            TelemetryField.OilTemp when floatValue > 120 => Colors.Red,
            TelemetryField.OilTemp when floatValue > 110 => Colors.Yellow,
            
            // Fuel: Red <5L (1.3gal), Yellow <10L (2.6gal)
            // Note: floatValue is always in liters from SDK
            TelemetryField.FuelLevel when floatValue < 5 => Colors.Red,
            TelemetryField.FuelLevel when floatValue < 10 => Colors.Yellow,
            
            _ => _primaryColor // Default to teal
        };
    }
    
    private void UpdateCenterFontSize()
    {
        if (string.IsNullOrEmpty(_centerValueText.Text))
            return;
        
        double scale = Width / 200.0;
        bool hasSideBoxes = (_leftField.HasValue || _rightField.HasValue);
        double baseSize = hasSideBoxes ? 42 : 56;
        
        // Adjust based on text length
        int textLength = _centerValueText.Text.Length;
        double sizeMultiplier = textLength switch
        {
            1 => 1.0,      // Single char (gear "5", "R", "N") - full size
            2 => 0.85,     // Two chars (gear "10") - 85% size
            3 => 0.70,     // Three chars (speed "247") - 70% size
            4 => 0.60,     // Four chars (RPM "8500") - 60% size
            >= 5 => 0.50,  // Five+ chars - 50% size
            _ => 1.0
        };
        
        _centerValueText.FontSize = baseSize * scale * sizeMultiplier;
    }
    
    /// <summary>
    /// Update widget size and scale all elements proportionally (live update)
    /// </summary>
    public void UpdateSize(double size)
    {
        Width = size;
        Height = size;
        
        // Calculate scale factor based on default size of 200px
        double scale = size / 200.0;
        
        // Scale margins (default 30px = 15% of 200px)
        double margin = size * 0.15;
        _topStack.Margin = new Thickness(0, margin, 0, 0);
        _bottomStack.Margin = new Thickness(0, 0, 0, margin);
        
        // Scale side box margins (default 20px = 10% of 200px)
        double sideMargin = size * 0.10;
        _leftBox.Margin = new Thickness(sideMargin, 0, 0, 0);
        _rightBox.Margin = new Thickness(0, 0, sideMargin, 0);
        
        // Scale font sizes proportionally
        // Top/Bottom values: default 18px
        _topValueText.FontSize = 18 * scale;
        _bottomValueText.FontSize = 18 * scale;
        
        // Top/Bottom labels: default 10px
        _topLabelText.FontSize = 10 * scale;
        _bottomLabelText.FontSize = 10 * scale;
        
        // Center text: Dynamic sizing based on content and side box presence
        // Will be updated in UpdateUI based on actual content
        bool hasSideBoxes = (_leftField.HasValue || _rightField.HasValue);
        double baseCenterSize = hasSideBoxes ? 42 : 56;
        // Actual font size set in UpdateCenterFontSize based on text content
        _centerValueText.FontSize = baseCenterSize * scale;
        
        // Side box fonts
        _leftValueText.FontSize = 14 * scale;
        _rightValueText.FontSize = 14 * scale;
        _leftLabelText.FontSize = 9 * scale;
        _rightLabelText.FontSize = 9 * scale;
        
        // Scale stroke thickness proportionally (default 3px)
        _gaugeCircle.StrokeThickness = 3 * scale;
        
        // Scale margin around circle (default 5px)
        _gaugeCircle.Margin = new Thickness(5 * scale);
    }
    
    /// <summary>
    /// Update section visibility (live update)
    /// </summary>
    public void UpdateSectionVisibility(bool showTop, bool showCenter, bool showBottom)
    {
        if (_topValueText != null)
        {
            _topValueText.Visibility = showTop ? Visibility.Visible : Visibility.Collapsed;
            _topLabelText.Visibility = showTop ? Visibility.Visible : Visibility.Collapsed;
        }
        
        if (_centerValueText != null)
        {
            _centerValueText.Visibility = showCenter ? Visibility.Visible : Visibility.Collapsed;
        }
        
        if (_bottomValueText != null)
        {
            _bottomValueText.Visibility = showBottom ? Visibility.Visible : Visibility.Collapsed;
            _bottomLabelText.Visibility = showBottom ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    
    protected override void UpdateUI(TelemetryData data)
    {
        // Update shift point calculator with current data (including SDK redline if available)
        ShiftPointCalculator.UpdateTracking(data.RPM, data.Throttle, data.Gear, data.EngineRedlineRPM);
        
        // Update TOP section (SecondaryField - typically Speed or RPM)
        if (_dataBinding.SecondaryField.HasValue)
        {
            UpdateSection(_topValueText, _topLabelText, _dataBinding.SecondaryField.Value, data);
        }
        
        // Update CENTER section (PrimaryField - typically Gear)
        UpdateSection(_centerValueText, null, _dataBinding.PrimaryField, data);
        
        // Dynamically adjust center font size based on content length
        UpdateCenterFontSize();
        
        // Update BOTTOM section (TertiaryField - typically RPM or Speed)
        if (_dataBinding.TertiaryField.HasValue)
        {
            UpdateSection(_bottomValueText, _bottomLabelText, _dataBinding.TertiaryField.Value, data);
        }
        
        // Update LEFT side box
        if (_leftField.HasValue)
        {
            var leftValue = TelemetryDataMapper.GetValue(_leftField.Value, data) ?? 0;
            _leftValueText.Text = FormatFieldValue(_leftField.Value, leftValue, data);
            _leftValueText.Foreground = new SolidColorBrush(GetValueColor(_leftField.Value, leftValue, data));
        }
        
        // Update RIGHT side box
        if (_rightField.HasValue)
        {
            var rightValue = TelemetryDataMapper.GetValue(_rightField.Value, data) ?? 0;
            _rightValueText.Text = FormatFieldValue(_rightField.Value, rightValue, data);
            _rightValueText.Foreground = new SolidColorBrush(GetValueColor(_rightField.Value, rightValue, data));
        }
        
        // Update gauge circle color based on RPM zones
        var rpm = data.RPM;
        var zone = ShiftPointCalculator.GetRPMZone(rpm, data.Gear);
        
        var rpmColor = zone switch
        {
            ShiftPointCalculator.RPMZone.Danger => Colors.Red,         // RED - at limiter
            ShiftPointCalculator.RPMZone.Optimal => _secondaryColor,   // ORANGE - optimal shift
            ShiftPointCalculator.RPMZone.Warning => Colors.Yellow,     // YELLOW - approaching shift
            _ => _primaryColor                                          // TEAL - safe range
        };
        
        _gaugeCircle.Stroke = new SolidColorBrush(rpmColor);
    }
    
    /// <summary>
    /// Update a section (value + label) dynamically based on field type
    /// </summary>
    private void UpdateSection(TextBlock valueText, TextBlock? labelText, TelemetryField field, TelemetryData data)
    {
        var value = TelemetryDataMapper.GetValue(field, data);
        
        switch (field)
        {
            case TelemetryField.Gear:
                if (value is int gear)
                {
                    valueText.Text = gear switch
                    {
                        -1 => "R",
                        0 => "N",
                        _ => gear.ToString()
                    };
                    
                    // Change color based on gear
                    var color = gear switch
                    {
                        -1 => Colors.Red,           // Red for reverse
                        0 => Colors.Gray,           // Gray for neutral
                        _ => _primaryColor          // Teal for forward gears
                    };
                    valueText.Foreground = new SolidColorBrush(color);
                }
                // Gear doesn't need a label
                if (labelText != null)
                    labelText.Visibility = Visibility.Collapsed;
                break;
                
            case TelemetryField.Speed:
                if (value is float speedMs)
                {
                    // Speed from iRacing is in m/s
                    if (AppSettings.Instance.UseMetricUnits)
                    {
                        float speedKmh = speedMs * 3.6f;
                        valueText.Text = $"{(int)speedKmh}";
                        if (labelText != null)
                        {
                            labelText.Text = "km/h";
                            labelText.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        float speedMph = speedMs * 2.23694f;
                        valueText.Text = $"{(int)speedMph}";
                        if (labelText != null)
                        {
                            labelText.Text = "MPH";
                            labelText.Visibility = Visibility.Visible;
                        }
                    }
                    valueText.Foreground = new SolidColorBrush(_primaryColor);
                }
                break;
                
            case TelemetryField.RPM:
                if (value is float rpm)
                {
                    valueText.Text = ((int)rpm).ToString();
                    
                    // Get RPM zone from shift point calculator
                    var zone = ShiftPointCalculator.GetRPMZone(rpm, data.Gear);
                    
                    var rpmColor = zone switch
                    {
                        ShiftPointCalculator.RPMZone.Danger => Colors.Red,
                        ShiftPointCalculator.RPMZone.Optimal => _secondaryColor, // Orange
                        ShiftPointCalculator.RPMZone.Warning => Colors.Yellow,
                        _ => _primaryColor // Teal
                    };
                    
                    valueText.Foreground = new SolidColorBrush(rpmColor);
                    
                    // Show "RPM" label
                    if (labelText != null)
                    {
                        labelText.Text = "RPM";
                        labelText.Foreground = new SolidColorBrush(_primaryColor);
                        labelText.Visibility = Visibility.Visible;
                    }
                }
                break;
                
            default:
                // For other fields, format value and hide label
                valueText.Text = value?.ToString() ?? "--";
                valueText.Foreground = new SolidColorBrush(_primaryColor);
                if (labelText != null)
                    labelText.Visibility = Visibility.Collapsed;
                break;
        }
    }
    
    protected override void OnConnectionStatusChanged(ConnectionStatus status)
    {
        // Update circle stroke color based on connection status
        var color = status switch
        {
            ConnectionStatus.Connected => _primaryColor,    // Teal when connected
            ConnectionStatus.Connecting => Colors.Yellow,   // Yellow when connecting
            _ => Colors.Red                                  // Red when disconnected
        };
        
        _gaugeCircle.Stroke = new SolidColorBrush(color);
    }
    
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Reload theme colors when settings change
        _primaryColor = (Color)ColorConverter.ConvertFromString(AppSettings.Instance.PrimaryColor);
        _secondaryColor = (Color)ColorConverter.ConvertFromString(AppSettings.Instance.SecondaryColor);
        _backgroundOpacity = AppSettings.Instance.DefaultOpacity;
        
        // Update circle fill opacity
        _gaugeCircle.Fill = new SolidColorBrush(Color.FromArgb(
            (byte)(255 * _backgroundOpacity), 20, 20, 20));
        
        // Force UI refresh with last telemetry data
        if (_lastTelemetryData != null)
        {
            UpdateUI(_lastTelemetryData);
        }
    }
    
    public new WidgetConfig GetConfiguration()
    {
        var config = base.GetConfiguration();
        
        // Store data binding configuration in settings
        config.Settings["PrimaryField"] = _dataBinding.PrimaryField.ToString();
        config.Settings["SecondaryField"] = _dataBinding.SecondaryField?.ToString() ?? "Speed";
        config.Settings["TertiaryField"] = _dataBinding.TertiaryField?.ToString() ?? "RPM";
        config.Settings["BackgroundOpacity"] = _backgroundOpacity;
        
        return config;
    }
    
    public new void UpdateConfiguration(WidgetConfig config)
    {
        base.UpdateConfiguration(config);
        
        // Restore data binding configuration from settings
        if (config.Settings.TryGetValue("PrimaryField", out var primaryField) && primaryField is string primaryStr)
        {
            if (Enum.TryParse<TelemetryField>(primaryStr, out var primary))
            {
                _dataBinding.PrimaryField = primary;
            }
        }
        
        if (config.Settings.TryGetValue("SecondaryField", out var secondaryField) && secondaryField is string secondaryStr)
        {
            if (Enum.TryParse<TelemetryField>(secondaryStr, out var secondary))
            {
                _dataBinding.SecondaryField = secondary;
            }
        }
        
        if (config.Settings.TryGetValue("TertiaryField", out var tertiaryField) && tertiaryField is string tertiaryStr)
        {
            if (Enum.TryParse<TelemetryField>(tertiaryStr, out var tertiary))
            {
                _dataBinding.TertiaryField = tertiary;
            }
        }
        
        if (config.Settings.TryGetValue("BackgroundOpacity", out var opacity) && opacity is double opacityValue)
        {
            _backgroundOpacity = opacityValue;
            _gaugeCircle.Fill = new SolidColorBrush(Color.FromArgb(
                (byte)(255 * _backgroundOpacity), 20, 20, 20));
        }
    }
    
    protected override void OnClosed(EventArgs e)
    {
        AppSettings.Instance.SettingsChanged -= OnSettingsChanged;
        base.OnClosed(e);
    }
}
