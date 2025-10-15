using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
/// Phase 2: Toggleable visual enhancements (gradient, shift ring, glow, typography, dynamic colors)
/// </summary>
public class MRTOneWidget : WidgetBase
{
    private readonly Grid _mainGrid;
    private readonly Ellipse _gaugeCircle;
    
    // PHASE 2: Visual Enhancement Elements (all optional/toggleable)
    private Ellipse? _rpmIndicatorBead;  // Small circle that travels on gauge showing current RPM
    private DispatcherTimer? _rpmBeadAnimationTimer;  // Animation timer for RPM bead
    
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
    
    // Widget-specific settings
    private MRTOneSettings _settings;
    
    // Blinking timer for critical warnings
    private readonly DispatcherTimer _blinkTimer;
    private bool _blinkState = false;
    
    // Theme colors
    private System.Windows.Media.Color _primaryColor;   // Teal #008080
    private System.Windows.Media.Color _secondaryColor; // Orange #FF8000
    
    // Configurable opacity
    private double _backgroundOpacity = 0.85;
    
    public MRTOneWidget(ITelemetryService telemetryService, WidgetConfig? config = null) : base(telemetryService, config)
    {
        // Load theme colors from settings
        _primaryColor = (Color)ColorConverter.ConvertFromString(AppSettings.Instance.PrimaryColor);
        _secondaryColor = (Color)ColorConverter.ConvertFromString(AppSettings.Instance.SecondaryColor);
        _backgroundOpacity = AppSettings.Instance.DefaultOpacity;
        
        // Load widget-specific settings from Config.Settings or use defaults
        _settings = LoadSettings();
        
        // Configure data binding based on loaded settings
        _dataBinding = new WidgetDataBinding
        {
            PrimaryField = _settings.CenterFieldEnum ?? TelemetryField.Gear,
            SecondaryField = _settings.TopFieldEnum,
            TertiaryField = _settings.BottomFieldEnum,
            PrimaryDisplayOptions = TelemetryDataMapper.GetDefaultDisplayOptions(_settings.CenterFieldEnum ?? TelemetryField.Gear),
            SecondaryDisplayOptions = _settings.TopFieldEnum.HasValue ? TelemetryDataMapper.GetDefaultDisplayOptions(_settings.TopFieldEnum.Value) : null,
            TertiaryDisplayOptions = _settings.BottomFieldEnum.HasValue ? TelemetryDataMapper.GetDefaultDisplayOptions(_settings.BottomFieldEnum.Value) : null
        };
        
        // Set left and right fields from settings
        _leftField = _settings.LeftFieldEnum;
        _rightField = _settings.RightFieldEnum;
        
        // Set window title (size is already set by base.ApplyConfiguration from config)
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
        
        // PHASE 2: Apply visual enhancements based on settings
        ApplyVisualEnhancements();
        
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
            Margin = new Thickness(30, -9, 0, 0), // 15% horizontal, -9px vertical to align value with center (label height + margin)
            Visibility = Visibility.Collapsed // Hidden by default
        };
        
        _leftLabelText = new TextBlock
        {
            Text = "",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 8,  // Reduced from 9 to 8 for better fit
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
            FontSize = 13,  // Reduced from 14 to 13 for better proportions
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)  // Reduced gap from 2 to 1
        };
        _leftBox.Children.Add(_leftValueText);
        
        _mainGrid.Children.Add(_leftBox);
        
        // Right side data box (optional)
        _rightBox = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, -9, 30, 0), // 15% horizontal, -9px vertical to align value with center (label height + margin)
            Visibility = Visibility.Collapsed // Hidden by default
        };
        
        _rightLabelText = new TextBlock
        {
            Text = "",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 8,  // Reduced from 9 to 8 for better fit
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
            FontSize = 13,  // Reduced from 14 to 13 for better proportions
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)  // Reduced gap from 2 to 1
        };
        _rightBox.Children.Add(_rightValueText);
        
        _mainGrid.Children.Add(_rightBox);
        
        // No border - just the content
        Content = _mainGrid;
        
        // Subscribe to SizeChanged to update scale transform
        SizeChanged += OnWidgetSizeChanged;
        
        // Apply initial scale transform based on loaded size from config
        // (SizeChanged event won't fire if size was set before event handler was attached)
        if (Width > 0 && Height > 0)
        {
            double scale = Math.Min(Width / 200.0, Height / 200.0);
            _mainGrid.LayoutTransform = new ScaleTransform(scale, scale);
        }
        
        // Setup blinking timer for critical warnings (500ms interval)
        _blinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _blinkTimer.Tick += OnBlinkTimerTick;
        _blinkTimer.Start();
        
        // Subscribe to settings changes
        AppSettings.Instance.SettingsChanged += OnSettingsChanged;
        
        // Apply visibility settings
        ApplyVisibilitySettings();
    }
    
    public override WidgetType WidgetType => WidgetType.MRTOne;
    
    /// <summary>
    /// Load MRTOneSettings from Config.Settings dictionary, or return defaults
    /// </summary>
    private MRTOneSettings LoadSettings()
    {
        var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MRT-UI", "debug.log");
        try
        {
            var log = $"\n[{DateTime.Now:HH:mm:ss}] [MRTOne] LoadSettings: Config.Settings has {Config.Settings.Count} entries";
            System.IO.File.AppendAllText(logPath, log);
            
            if (Config.Settings.TryGetValue("mrtone", out var settingsObj))
            {
                log = $"\n[{DateTime.Now:HH:mm:ss}] [MRTOne] LoadSettings: Found 'mrtone' entry, type: {settingsObj?.GetType().Name}";
                System.IO.File.AppendAllText(logPath, log);
                
                var json = JsonSerializer.Serialize(settingsObj);
                log = $"\n[{DateTime.Now:HH:mm:ss}] [MRTOne] LoadSettings: JSON length: {json.Length}";
                System.IO.File.AppendAllText(logPath, log);
                log = $"\n[{DateTime.Now:HH:mm:ss}] [MRTOne] LoadSettings: JSON: {json.Substring(0, Math.Min(300, json.Length))}";
                System.IO.File.AppendAllText(logPath, log);
                
                var settings = JsonSerializer.Deserialize<MRTOneSettings>(json);
                
                if (settings != null)
                {
                    log = $"\n[{DateTime.Now:HH:mm:ss}] [MRTOne] LoadSettings: ✓ SUCCESS - ShiftRing={settings.EnableShiftPointRing}, Glow={settings.EnableGlowEffects}, Gradient={settings.EnableGradientBackground}";
                    System.IO.File.AppendAllText(logPath, log);
                    return settings;
                }
            }
            else
            {
                log = $"\n[{DateTime.Now:HH:mm:ss}] [MRTOne] LoadSettings: ✗ 'mrtone' entry NOT found in Config.Settings";
                System.IO.File.AppendAllText(logPath, log);
            }
        }
        catch (Exception ex)
        {
            var log = $"\n[{DateTime.Now:HH:mm:ss}] [MRTOne] LoadSettings: ✗ Exception: {ex.Message}";
            System.IO.File.AppendAllText(logPath, log);
        }
        
        var finalLog = $"\n[{DateTime.Now:HH:mm:ss}] [MRTOne] LoadSettings: Returning DEFAULTS (Gradient=ON, ShiftRing=OFF, Glow=OFF)";
        System.IO.File.AppendAllText(logPath, finalLog);
        return MRTOneSettings.Default;
    }
    
    /// <summary>
    /// Save current settings to Config.Settings dictionary
    /// </summary>
    private void SaveSettings()
    {
        try
        {
            // Serialize to JSON then deserialize to JsonElement for proper Dictionary<string,object> storage
            var json = JsonSerializer.Serialize(_settings);
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            Config.Settings["mrtone"] = element;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Apply visibility settings to UI sections
    /// </summary>
    private void ApplyVisibilitySettings()
    {
        _topStack.Visibility = _settings.ShowTop ? Visibility.Visible : Visibility.Collapsed;
        _centerValueText.Visibility = _settings.ShowCenter ? Visibility.Visible : Visibility.Collapsed;
        _bottomStack.Visibility = _settings.ShowBottom ? Visibility.Visible : Visibility.Collapsed;
        _leftBox.Visibility = (_settings.ShowLeft && _leftField.HasValue) ? Visibility.Visible : Visibility.Collapsed;
        _rightBox.Visibility = (_settings.ShowRight && _rightField.HasValue) ? Visibility.Visible : Visibility.Collapsed;
        
        // Adjust center font size based on whether side boxes are visible
        UpdateCenterFontSize();
    }
    
    /// <summary>
    /// Update widget settings (called from OverlayViewModel)
    /// </summary>
    public void UpdateWidgetSettings(MRTOneSettings newSettings)
    {
        _settings = newSettings;
        
        // Update data binding fields
        _dataBinding.SecondaryField = _settings.TopFieldEnum;  // Top
        _dataBinding.PrimaryField = _settings.CenterFieldEnum ?? TelemetryField.None;  // Center
        _dataBinding.TertiaryField = _settings.BottomFieldEnum; // Bottom
        
        // Update side fields
        _leftField = _settings.LeftFieldEnum;
        _rightField = _settings.RightFieldEnum;
        
        // Update labels for side boxes
        if (_leftField.HasValue)
            _leftLabelText.Text = GetFieldLabel(_leftField.Value);
        if (_rightField.HasValue)
            _rightLabelText.Text = GetFieldLabel(_rightField.Value);
        
        // Apply visibility
        ApplyVisibilitySettings();
        
        // PHASE 2: Apply visual enhancements based on updated settings
        ApplyVisualEnhancements();
        
        // Save to config
        SaveSettings();
        
        // Force immediate UI refresh
        if (_lastTelemetryData != null)
        {
            UpdateUI(_lastTelemetryData);
        }
    }
    
    /// <summary>
    /// Get current widget settings (called from OverlayViewModel)
    /// </summary>
    public MRTOneSettings GetCurrentSettings()
    {
        return _settings;
    }
    
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
        
        // Fallback to short, clean labels with units where appropriate
        return field switch
        {
            // Engine & Controls
            TelemetryField.Throttle => "THRTL",
            TelemetryField.Brake => "BRAKE",
            TelemetryField.Clutch => "CLUTCH",
            TelemetryField.RPM => "RPM",
            TelemetryField.Gear => "GEAR",
            
            // Fuel (include units in label)
            TelemetryField.FuelLevel => AppSettings.Instance.UseMetricUnits ? "FUEL (L)" : "FUEL (gal)",
            TelemetryField.FuelPercent => "FUEL%",
            
            // Temperatures (include units in label)
            TelemetryField.WaterTemp => AppSettings.Instance.UseMetricUnits ? "H₂O (°C)" : "H₂O (°F)",
            TelemetryField.OilTemp => AppSettings.Instance.UseMetricUnits ? "OIL (°C)" : "OIL (°F)",
            TelemetryField.AirTemp => AppSettings.Instance.UseMetricUnits ? "AIR (°C)" : "AIR (°F)",
            TelemetryField.TrackTemp => AppSettings.Instance.UseMetricUnits ? "TRACK (°C)" : "TRACK (°F)",
            
            // Speed
            TelemetryField.Speed => AppSettings.Instance.UseMetricUnits ? "km/h" : "mph",
            
            // Timing
            TelemetryField.LapNumber => "LAP",
            TelemetryField.Position => "POS",
            TelemetryField.ClassPosition => "P/C",
            TelemetryField.LastLapTime => "LAST",
            TelemetryField.BestLapTime => "BEST",
            TelemetryField.CurrentLapTime => "CUR",
            
            _ => field.ToString().ToUpper()
        };
    }
    
    private string FormatFieldValue(TelemetryField field, object value, TelemetryData data)
    {
        return field switch
        {
            // Percentages (0-1 scale → 0-100%)
            TelemetryField.Throttle when value is float throttle => $"{(int)(throttle * 100)}%",
            TelemetryField.Brake when value is float brake => $"{(int)(brake * 100)}%",
            TelemetryField.Clutch when value is float clutch => $"{(int)(clutch * 100)}%",
            TelemetryField.FuelPercent when value is float fuelPct => $"{(int)(fuelPct * 100)}%",
            
            // Fuel (NO units - they're in the label now)
            // Force invariant culture to always use "." as decimal separator
            TelemetryField.FuelLevel when value is float fuel => 
                AppSettings.Instance.UseMetricUnits 
                    ? fuel.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                    : (fuel * 0.264172f).ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            
            // Temperatures (NO units or decimals - units are in label)
            TelemetryField.WaterTemp when value is float temp => 
                AppSettings.Instance.UseMetricUnits ? $"{(int)temp}" : $"{(int)(temp * 9 / 5 + 32)}",
            TelemetryField.OilTemp when value is float temp => 
                AppSettings.Instance.UseMetricUnits ? $"{(int)temp}" : $"{(int)(temp * 9 / 5 + 32)}",
            TelemetryField.AirTemp when value is float temp => 
                AppSettings.Instance.UseMetricUnits ? $"{(int)temp}" : $"{(int)(temp * 9 / 5 + 32)}",
            TelemetryField.TrackTemp when value is float temp => 
                AppSettings.Instance.UseMetricUnits ? $"{(int)temp}" : $"{(int)(temp * 9 / 5 + 32)}",
            
            // Speed (no units needed - handled by label)
            TelemetryField.Speed when value is float speedMs =>
                AppSettings.Instance.UseMetricUnits ? $"{(int)(speedMs * 3.6f)}" : $"{(int)(speedMs * 2.23694f)}",
            
            // RPM (no units needed - handled by label)
            TelemetryField.RPM when value is float rpm => $"{(int)rpm}",
            
            // Gear (special handling)
            TelemetryField.Gear => data.Gear switch
            {
                -1 => "R",
                0 => "N",
                _ => data.Gear.ToString()
            },
            
            // Position (with "P" prefix, +1 offset since iRacing uses 0-based indexing)
            TelemetryField.Position when value is int pos => $"P{pos + 1}",
            TelemetryField.ClassPosition when value is int pos => $"P{pos + 1}",
            
            // Lap times (formatted as mm:ss.xxx)
            TelemetryField.LastLapTime when value is float time => FormatLapTime(time),
            TelemetryField.BestLapTime when value is float time => FormatLapTime(time),
            TelemetryField.CurrentLapTime when value is float time => FormatLapTime(time),
            
            // Lap numbers
            TelemetryField.LapNumber when value is int lap => $"{lap}",
            
            // Default fallback
            _ => value?.ToString() ?? "-"
        };
    }
    
    /// <summary>
    /// Format lap time as mm:ss.xxx
    /// </summary>
    private string FormatLapTime(float seconds)
    {
        if (seconds <= 0) return "--:--.---";
        
        int minutes = (int)(seconds / 60);
        float remainingSeconds = seconds % 60;
        // Use invariant culture to ensure period (.) separator instead of comma (,)
        return $"{minutes}:{remainingSeconds.ToString("00.000", System.Globalization.CultureInfo.InvariantCulture)}";
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
        
        // Scale side box margins (match top/bottom: 15% of size)
        _leftBox.Margin = new Thickness(margin, 0, 0, 0);
        _rightBox.Margin = new Thickness(0, 0, margin, 0);
        
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
        if (_dataBinding.PrimaryField != TelemetryField.None)
        {
            UpdateSection(_centerValueText, null, _dataBinding.PrimaryField, data);
        }
        
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
            
            // Update label text with units (e.g., "FUEL (L)", "OIL (°C)")
            _leftLabelText.Text = GetFieldLabel(_leftField.Value);
        }
        
        // Update RIGHT side box
        if (_rightField.HasValue)
        {
            var rightValue = TelemetryDataMapper.GetValue(_rightField.Value, data) ?? 0;
            _rightValueText.Text = FormatFieldValue(_rightField.Value, rightValue, data);
            _rightValueText.Foreground = new SolidColorBrush(GetValueColor(_rightField.Value, rightValue, data));
            
            // Update label text with units (e.g., "FUEL (L)", "OIL (°C)")
            _rightLabelText.Text = GetFieldLabel(_rightField.Value);
        }
        
        // Update gauge circle color based on RPM zone
        var rpm = data.RPM;
        var zone = ShiftPointCalculator.GetRPMZone(rpm, data.Gear);
        
        Color borderColor = zone switch
        {
            ShiftPointCalculator.RPMZone.Danger => Colors.Red,         // RED - at limiter
            ShiftPointCalculator.RPMZone.Optimal => _secondaryColor,   // ORANGE - optimal shift
            ShiftPointCalculator.RPMZone.Warning => Colors.Yellow,     // YELLOW - approaching shift
            _ => _primaryColor                                          // TEAL - safe range
        };
        
        _gaugeCircle.Stroke = new SolidColorBrush(borderColor);
    }
    
    /// <summary>
    /// Update a section (value + label) dynamically based on field type
    /// Uses consistent formatting with side boxes
    /// </summary>
    private void UpdateSection(TextBlock valueText, TextBlock? labelText, TelemetryField field, TelemetryData data)
    {
        var value = TelemetryDataMapper.GetValue(field, data);
        
        // Use consistent formatting functions
        valueText.Text = FormatFieldValue(field, value ?? 0, data);
        
        // Special color handling for Gear (R=Red, N=Gray, forward gears=Teal)
        if (field == TelemetryField.Gear && value is int gear)
        {
            valueText.Foreground = new SolidColorBrush(gear switch
            {
                -1 => Colors.Red,           // Reverse
                0 => Colors.Gray,           // Neutral
                _ => _primaryColor          // Forward gears (Teal)
            });
        }
        // Special color handling for RPM (shift point zones)
        else if (field == TelemetryField.RPM && value is float rpm)
        {
            var zone = ShiftPointCalculator.GetRPMZone(rpm, data.Gear);
            valueText.Foreground = new SolidColorBrush(zone switch
            {
                ShiftPointCalculator.RPMZone.Danger => Colors.Red,         // At limiter
                ShiftPointCalculator.RPMZone.Optimal => _secondaryColor,   // Optimal shift (Orange)
                ShiftPointCalculator.RPMZone.Warning => Colors.Yellow,     // Approaching shift
                _ => _primaryColor                                          // Safe range (Teal)
            });
        }
        else
        {
            valueText.Foreground = new SolidColorBrush(GetValueColor(field, value ?? 0, data));
        }
        
        // Update label if present
        if (labelText != null)
        {
            // Gear doesn't need a label (just shows R, N, 1, 2, etc.)
            if (field == TelemetryField.Gear)
            {
                labelText.Visibility = Visibility.Collapsed;
            }
            else
            {
                labelText.Text = GetFieldLabel(field);
                labelText.Foreground = new SolidColorBrush(_primaryColor);
                labelText.Visibility = Visibility.Visible;
            }
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
        
        // Ensure latest settings are saved to Config.Settings before retrieving
        SaveSettings();
        
        // The SaveSettings() method already stored MRTOneSettings in Config.Settings["mrtone"]
        // No need to manually add individual fields - they're all in the mrtone settings object
        
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
    
    // ============================================
    // PHASE 2: TOGGLEABLE VISUAL ENHANCEMENTS
    // All features can be enabled/disabled via settings
    // Easy rollback: Set all Enable* flags to false
    // ============================================
    
    /// <summary>
    /// Apply all visual enhancements based on current settings
    /// Call this whenever settings change to update visual features
    /// </summary>
    private void ApplyVisualEnhancements()
    {
        var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MRT-UI", "debug.log");
        var log = $"\n[{DateTime.Now:HH:mm:ss}] [MRTOne] ApplyVisualEnhancements: Gradient={_settings.EnableGradientBackground}, ShiftRing={_settings.EnableShiftPointRing}, Glow={_settings.EnableGlowEffects}";
        System.IO.File.AppendAllText(logPath, log);
        
        // Enhancement 1: Gradient Background
        if (_settings.EnableGradientBackground)
        {
            ApplyGradientBackground();
        }
        else
        {
            // Revert to solid background
            _gaugeCircle.Fill = new SolidColorBrush(Color.FromArgb(
                (byte)(255 * _backgroundOpacity), 20, 20, 20));
        }
        
        // Enhancement 2: Shift Point Ring
        if (_settings.EnableShiftPointRing)
        {
            log = $"\n[{DateTime.Now:HH:mm:ss}] [MRTOne] Creating shift point ring...";
            System.IO.File.AppendAllText(logPath, log);
            CreateShiftPointRing();
        }
        else
        {
            RemoveShiftPointRing();
        }
        
        // Enhancement 3: Glow Effects
        if (_settings.EnableGlowEffects)
        {
            ApplyGlowEffects();
        }
        else
        {
            RemoveGlowEffects();
        }
    }
    
    // ============================================
    // Enhancement 1: Gradient Background
    // ============================================
    private void ApplyGradientBackground()
    {
        if (_gaugeCircle == null) return; // Guard against early call
        
        var gradient = new RadialGradientBrush();
        gradient.GradientOrigin = new Point(0.5, 0.5);
        gradient.Center = new Point(0.5, 0.5);
        gradient.RadiusX = 0.5;
        gradient.RadiusY = 0.5;
        
        // Center (lighter) to edge (darker) gradient
        gradient.GradientStops.Add(new GradientStop(
            Color.FromArgb((byte)(255 * _backgroundOpacity), 30, 30, 30), 0.0));
        gradient.GradientStops.Add(new GradientStop(
            Color.FromArgb((byte)(255 * _backgroundOpacity), 10, 10, 10), 1.0));
        
        _gaugeCircle.Fill = gradient;
    }
    
    // ============================================
    // Enhancement 2: Animated RPM Indicator Bead
    // Small circle that travels ON the gauge circle showing current RPM
    // Animates from bottom (6 o'clock) to top (12 o'clock) = 180° arc
    // ============================================
    private void CreateShiftPointRing()
    {
        // Remove existing elements if present
        RemoveShiftPointRing();
        
        // Create small circular bead that travels on the gauge circle
        // Moves from bottom (0% RPM) to top center (100% RPM)
        _rpmIndicatorBead = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = new SolidColorBrush(_secondaryColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = new TranslateTransform(),
            Visibility = Visibility.Collapsed  // Start hidden, will show after first position update
        };
        
        // Add bead to grid (above gauge circle but below text)
        _mainGrid.Children.Insert(1, _rpmIndicatorBead);
        
        // Create animation timer (smoother at 30 FPS)
        _rpmBeadAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS for smooth animation
        };
        _rpmBeadAnimationTimer.Tick += UpdateShiftPointRing;
        _rpmBeadAnimationTimer.Start();
        
        // Immediately update position to avoid flash at center
        UpdateShiftPointRing(null, EventArgs.Empty);
    }
    
    private void RemoveShiftPointRing()
    {
        if (_rpmIndicatorBead != null)
        {
            _mainGrid.Children.Remove(_rpmIndicatorBead);
            _rpmIndicatorBead = null;
        }
        
        if (_rpmBeadAnimationTimer != null)
        {
            _rpmBeadAnimationTimer.Stop();
            _rpmBeadAnimationTimer.Tick -= UpdateShiftPointRing;
            _rpmBeadAnimationTimer = null;
        }
    }
    
    // Update bead position based on current RPM
    // Bead travels from bottom (90° = 0% RPM) to top center (270° = 100% RPM)
    private void UpdateShiftPointRing(object? sender, EventArgs e)
    {
        if (_rpmIndicatorBead == null || _lastTelemetryData == null || _gaugeCircle == null)
            return;
        
        var rpm = _lastTelemetryData.RPM;
        var zone = ShiftPointCalculator.GetRPMZone(rpm, _lastTelemetryData.Gear);
        
        // Use actual engine redline from telemetry if available (most accurate)
        // Fall back to estimated redline only if telemetry doesn't provide it
        double redline = _lastTelemetryData.EngineRedlineRPM > 0 
            ? _lastTelemetryData.EngineRedlineRPM 
            : ShiftPointCalculator.GetEstimatedRedline();
        
        // Safety check: ensure redline is reasonable (not too low)
        if (redline < 3000)
        {
            redline = 8000; // Use safe default if redline seems invalid
        }
        
        // Don't show bead if we don't have valid RPM data
        if (rpm <= 0 || redline <= 0)
        {
            _rpmIndicatorBead.Visibility = Visibility.Collapsed;
            return;
        }
        
        // Map 0-100% RPM to 0-100% of arc (FULL RANGE, not compressed)
        double percentage = Math.Min(rpm / redline, 1.0);  // Clamp to 100%
        
        // Map percentage to angle on RIGHT HALF of circle (90° to 270°)
        // 0% RPM = 90° (bottom), 50% RPM = 180° (right), 100% RPM = 270° (top center)
        double angle = 90 + (percentage * 180);
        double angleRad = angle * Math.PI / 180;
        
        // Position bead ON the gauge circle using RenderTransform
        double centerX = _gaugeCircle.ActualWidth / 2;
        double centerY = _gaugeCircle.ActualHeight / 2;
        double gaugeRadius = (Math.Min(_gaugeCircle.ActualWidth, _gaugeCircle.ActualHeight) / 2);
        
        // Adjust radius slightly inward to center bead ON the stroke
        // This prevents clipping and ensures perfect alignment
        double adjustedRadius = gaugeRadius - (_gaugeCircle.StrokeThickness / 2);
        
        // Calculate bead position on circle
        double beadX = adjustedRadius * Math.Cos(angleRad);
        double beadY = adjustedRadius * Math.Sin(angleRad);
        
        // Round to nearest pixel for crisp rendering and force perfect centering at top
        // Smoothly transition to centered as we approach top (no flickering)
        if (angle >= 260)  // Above 94.4% RPM, start centering horizontally
        {
            // Gradually reduce horizontal offset as we approach 270°
            // This prevents flickering at high RPM
            double centeringFactor = Math.Min(1.0, (angle - 260) / 10.0);  // 0 at 260°, 1 at 270°
            beadX = beadX * (1.0 - centeringFactor);  // Smoothly approach X=0
            beadX = Math.Round(beadX);
        }
        else
        {
            beadX = Math.Round(beadX);
        }
        beadY = Math.Round(beadY);
        
        // Apply transform to move bead from center to calculated position
        if (_rpmIndicatorBead.RenderTransform is TranslateTransform transform)
        {
            transform.X = beadX;
            transform.Y = beadY;
        }
        
        // Make bead visible after positioning (prevents flash at center on creation)
        _rpmIndicatorBead.Visibility = Visibility.Visible;
        
        // Color based on zone (matches gauge circle colors)
        Color beadColor = zone switch
        {
            ShiftPointCalculator.RPMZone.Danger => Colors.Red,
            ShiftPointCalculator.RPMZone.Optimal => _secondaryColor, // Orange
            ShiftPointCalculator.RPMZone.Warning => Colors.Yellow,
            _ => _primaryColor // Teal for safe zone
        };
        _rpmIndicatorBead.Fill = new SolidColorBrush(beadColor);
    }
    
    // ============================================
    // Enhancement 3: Glow Effects
    // ============================================
    private void ApplyGlowEffects()
    {
        // Add subtle glow to center value (gear) - with null check
        if (_centerValueText != null)
        {
            _centerValueText.Effect = new DropShadowEffect
            {
                Color = _primaryColor,
                BlurRadius = 15,
                ShadowDepth = 0,
                Opacity = 0.8
            };
        }
        
        // Add subtle glow to gauge circle border - with null check
        if (_gaugeCircle != null)
        {
            _gaugeCircle.Effect = new DropShadowEffect
            {
                Color = _primaryColor,
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.6
            };
        }
    }
    
    private void RemoveGlowEffects()
    {
        if (_centerValueText != null)
            _centerValueText.Effect = null;
        if (_gaugeCircle != null)
            _gaugeCircle.Effect = null;
    }
    
    protected override void OnClosed(EventArgs e)
    {
        // Clean up Phase 2 resources
        RemoveShiftPointRing();
        
        AppSettings.Instance.SettingsChanged -= OnSettingsChanged;
        base.OnClosed(e);
    }
}
