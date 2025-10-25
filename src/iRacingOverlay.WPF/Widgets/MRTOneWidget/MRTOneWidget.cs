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
using iRacingOverlay.Core.Telemetry;
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
    #region Layout Constants

    /// <summary>
    /// Layout and sizing constants for the MRT One widget
    /// Extracted from magic numbers for maintainability and documentation
    /// </summary>
    private static class LayoutConstants
    {
        // === Canvas and Grid Dimensions ===
        /// <summary>Canvas size including radar square spacing (228px = 200px grid + 14px each side for radar)</summary>
        public const double CANVAS_WIDTH = 228;
        /// <summary>Canvas height including fuel display space below (308px = 228px top section + 80px fuel area)</summary>
        public const double CANVAS_HEIGHT = 308;
        /// <summary>Main grid size for circular gauge content</summary>
        public const double GRID_SIZE = 200;
        /// <summary>Offset from canvas edge to center grid (accounts for radar square space)</summary>
        public const double RADAR_OFFSET = 14;

        // === Radar Square Specifications ===
        /// <summary>Width of radar spotter squares</summary>
        public const double RADAR_SQUARE_SIZE = 12;
        /// <summary>Radar square stroke thickness</summary>
        public const double RADAR_STROKE_THICKNESS = 1;
        /// <summary>Radar square corner radius for slight rounding</summary>
        public const double RADAR_CORNER_RADIUS = 2;
        /// <summary>Distance from edge of canvas to radar squares (provides spacing from circle)</summary>
        public const double RADAR_EDGE_OFFSET = 2;
        /// <summary>Center position for vertical radar squares (canvas center minus half square size)</summary>
        public const double RADAR_CENTER_OFFSET = 108; // 114 (canvas center) - 6 (half square)
        /// <summary>Bottom position for rear radar square</summary>
        public const double RADAR_BACK_TOP = 214;

        // === Gauge Circle ===
        /// <summary>Gauge circle stroke thickness</summary>
        public const double GAUGE_STROKE_THICKNESS = 3;
        /// <summary>Gauge circle margin (spacing from grid edges)</summary>
        public const double GAUGE_MARGIN = 5;

        // === Text Positioning ===
        /// <summary>Top section margin from top (15% of grid size)</summary>
        public const double TOP_SECTION_MARGIN = 30;
        /// <summary>Bottom section margin from bottom (15% of grid size)</summary>
        public const double BOTTOM_SECTION_MARGIN = 30;
        /// <summary>Side box horizontal margin (15% of grid size)</summary>
        public const double SIDE_BOX_MARGIN_H = 30;
        /// <summary>Side box vertical margin (negative to align value with center text)</summary>
        public const double SIDE_BOX_MARGIN_V = -9;

        // === Font Sizes ===
        /// <summary>Top/bottom value text font size</summary>
        public const double FIELD_VALUE_FONT_SIZE = 18;
        /// <summary>Top/bottom label text font size</summary>
        public const double FIELD_LABEL_FONT_SIZE = 10;
        /// <summary>Center value (gear) font size</summary>
        public const double CENTER_VALUE_FONT_SIZE = 56;
        /// <summary>Brake bias overlay font size</summary>
        public const double BRAKE_BIAS_FONT_SIZE = 46;
        /// <summary>Side box label font size</summary>
        public const double SIDE_BOX_LABEL_FONT_SIZE = 8;
        /// <summary>Side box value font size</summary>
        public const double SIDE_BOX_VALUE_FONT_SIZE = 13;
        /// <summary>Fuel display font size</summary>
        public const double FUEL_DISPLAY_FONT_SIZE = 8;
        /// <summary>Fuel display line height (compact spacing)</summary>
        public const double FUEL_DISPLAY_LINE_HEIGHT = 11;

        // === Fuel Display Positioning ===
        /// <summary>Vertical position of fuel display below gauge (canvas Y coordinate)</summary>
        public const double FUEL_DISPLAY_TOP = 240;

        // === RPM Indicator Bead ===
        /// <summary>Size of RPM indicator bead (circular dot on gauge)</summary>
        public const double RPM_BEAD_SIZE = 12;
        /// <summary>RPM angle threshold for horizontal centering (degrees)</summary>
        public const double RPM_CENTER_ANGLE_THRESHOLD = 260;
        /// <summary>RPM centering factor divisor (smooths centering transition)</summary>
        public const double RPM_CENTER_SMOOTHING = 10.0;

        // === Timing Constants (milliseconds) ===
        /// <summary>Fuel warning blink timer interval (slow blink)</summary>
        public const int FUEL_BLINK_INTERVAL_MS = 250;
        /// <summary>Radar critical proximity blink timer interval (fast blink)</summary>
        public const int RADAR_BLINK_INTERVAL_MS = 125;
        /// <summary>Pit limiter blink timer interval (fast blink)</summary>
        public const int PIT_LIMITER_BLINK_INTERVAL_MS = 125;
        /// <summary>RPM bead animation timer interval (~30 FPS for smooth animation)</summary>
        public const int RPM_ANIMATION_INTERVAL_MS = 33;

        // === Visual Effects ===
        /// <summary>Glow effect blur radius for center text</summary>
        public const double GLOW_BLUR_RADIUS_CENTER = 15;
        /// <summary>Glow effect blur radius for gauge circle</summary>
        public const double GLOW_BLUR_RADIUS_GAUGE = 10;
        /// <summary>Glow effect opacity for center text</summary>
        public const double GLOW_OPACITY_CENTER = 0.8;
        /// <summary>Glow effect opacity for gauge circle</summary>
        public const double GLOW_OPACITY_GAUGE = 0.6;
    }

    #endregion

    private readonly Grid _mainGrid;
    private readonly Ellipse _gaugeCircle;
    
    // PHASE 1: 4-Way Radar Spotter Squares (outside circle)
    private readonly Rectangle _radarFront;   // Top (cars ahead)
    private readonly Rectangle _radarBack;    // Bottom (cars behind)
    private readonly Rectangle _radarLeft;    // Left (cars on left)
    private readonly Rectangle _radarRight;   // Right (cars on right)
    
    // PHASE 1: Proximity detection services
    private readonly ProximityCalculator _proximityCalculator;
    private readonly LateralSpotter _lateralSpotter;

    // PHASE 2: Helper classes for clean architecture
    private readonly MRTOneStateManager _stateManager;
    private MRTOneVisualEffects? _visualEffects;

    // PHASE 2: Visual Enhancement Elements (managed by VisualEffects helper)
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
    
    // PHASE 2 FUEL CALCULATOR: Simple 1-line fuel display below gauge
    private readonly TextBlock _fuelDisplay;
    
    // Brake bias transient overlay (appears temporarily when changed)
    private readonly Border _brakeBiasOverlay;
    private readonly TextBlock _brakeBiasLabel;
    private readonly TextBlock _brakeBiasValue;
    
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
    
    // Blinking timers for critical warnings
    private readonly DispatcherTimer _blinkTimer;           // 250ms for fuel (slow blink)
    private readonly DispatcherTimer _radarBlinkTimer;      // 125ms for radar (fast blink)
    private bool _blinkState = false;
    private bool _radarBlinkState = false;
    
    // Brake bias overlay UI state (not in StateManager - widget-specific)
    private DispatcherTimer? _brakeBiasHideTimer;       // Auto-hide timer
    private bool _brakeBiasVisible = false;             // Current visibility state

    // Pit limiter blink UI state (actual state tracked in StateManager)
    private readonly DispatcherTimer _pitLimiterBlinkTimer;
    private bool _pitLimiterBlinkState = false;
    
    // State tracking for special fields (ABS, TC, Wheel Lock, Brake Bias)
    private int _lastLeftABSValue = -1;
    private int _lastRightABSValue = -1;
    private int _lastLeftTCValue = -999;
    private int _lastRightTCValue = -999;
    private int _lastLeftLockupValue = -1;
    private int _lastRightLockupValue = -1;
    private bool _brakeBiasInitialized = false;
    private float _lastBrakeBias = -1f;
    
    // Radar proximity zone tracking
    private ProximityZone _currentFrontZone = ProximityZone.Clear;
    private ProximityZone _currentRearZone = ProximityZone.Clear;
    
    // Pit limiter active state
    private bool _isPitLimiterActive = false;

    // Theme colors
    private System.Windows.Media.Color _primaryColor;   // Teal #008080
    private System.Windows.Media.Color _secondaryColor; // Orange #FF8000
    
    // Configurable opacity
    private double _backgroundOpacity = 0.85;
    
    public MRTOneWidget(ITelemetryService telemetryService, WidgetConfig? config = null) : base(telemetryService, config)
    {
        // Initialize PHASE 2 helper classes
        _stateManager = new MRTOneStateManager();

        // Initialize proximity detection services
        _proximityCalculator = new ProximityCalculator();
        _lateralSpotter = new LateralSpotter();
        
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
        
        // Create outer canvas to hold everything (allows positioning radar outside grid)
        var outerCanvas = new Canvas
        {
            Background = Brushes.Transparent,
            Width = LayoutConstants.CANVAS_WIDTH,
            Height = LayoutConstants.CANVAS_HEIGHT
        };

        // Create main grid (no background - transparent)
        // Use fixed size for content, then scale via LayoutTransform
        _mainGrid = new Grid
        {
            Background = Brushes.Transparent,
            Width = LayoutConstants.GRID_SIZE,
            Height = LayoutConstants.GRID_SIZE
        };

        // Position main grid centered in canvas (offset for radar space)
        Canvas.SetLeft(_mainGrid, LayoutConstants.RADAR_OFFSET);
        Canvas.SetTop(_mainGrid, LayoutConstants.RADAR_OFFSET);
        outerCanvas.Children.Add(_mainGrid);

        // Create circular gauge that fills the entire window
        _gaugeCircle = new Ellipse
        {
            Stroke = new SolidColorBrush(_primaryColor), // Teal border
            StrokeThickness = LayoutConstants.GAUGE_STROKE_THICKNESS,
            Fill = new SolidColorBrush(Color.FromArgb(
                (byte)(255 * _backgroundOpacity), 20, 20, 20)), // Dark gray with opacity
            Margin = new Thickness(LayoutConstants.GAUGE_MARGIN)
        };
        _mainGrid.Children.Add(_gaugeCircle);
        
        // PHASE 1: Create 4-way radar spotter squares (positioned OUTSIDE circle on canvas)
        // Canvas is 228x228, grid is 200x200 centered (14px offset)
        // Circle has 5px margin, so radius ~95px, center at (114, 114) in canvas coords
        // Position squares 4px further outside circle boundary for better visibility
        
        // Front radar (top - cars ahead)
        _radarFront = new Rectangle
        {
            Width = LayoutConstants.RADAR_SQUARE_SIZE,
            Height = LayoutConstants.RADAR_SQUARE_SIZE,
            Fill = Brushes.Green, // Default: clear/far
            Stroke = new SolidColorBrush(_primaryColor),
            StrokeThickness = LayoutConstants.RADAR_STROKE_THICKNESS,
            RadiusX = LayoutConstants.RADAR_CORNER_RADIUS,
            RadiusY = LayoutConstants.RADAR_CORNER_RADIUS,
            Visibility = AppSettings.Instance.EnableLateralSpotter ? Visibility.Visible : Visibility.Collapsed
        };
        Canvas.SetLeft(_radarFront, LayoutConstants.RADAR_CENTER_OFFSET);
        Canvas.SetTop(_radarFront, LayoutConstants.RADAR_EDGE_OFFSET);
        outerCanvas.Children.Add(_radarFront);
        
        // Back radar (bottom - cars behind)
        _radarBack = new Rectangle
        {
            Width = LayoutConstants.RADAR_SQUARE_SIZE,
            Height = LayoutConstants.RADAR_SQUARE_SIZE,
            Fill = Brushes.Green, // Default: clear/far
            Stroke = new SolidColorBrush(_primaryColor),
            StrokeThickness = LayoutConstants.RADAR_STROKE_THICKNESS,
            RadiusX = LayoutConstants.RADAR_CORNER_RADIUS,
            RadiusY = LayoutConstants.RADAR_CORNER_RADIUS,
            Visibility = AppSettings.Instance.EnableLateralSpotter ? Visibility.Visible : Visibility.Collapsed
        };
        Canvas.SetLeft(_radarBack, LayoutConstants.RADAR_CENTER_OFFSET);
        Canvas.SetTop(_radarBack, LayoutConstants.RADAR_BACK_TOP);
        outerCanvas.Children.Add(_radarBack);

        // Left radar (left side - cars on left)
        _radarLeft = new Rectangle
        {
            Width = LayoutConstants.RADAR_SQUARE_SIZE,
            Height = LayoutConstants.RADAR_SQUARE_SIZE,
            Fill = Brushes.Green, // Default: clear
            Stroke = new SolidColorBrush(_primaryColor),
            StrokeThickness = LayoutConstants.RADAR_STROKE_THICKNESS,
            RadiusX = LayoutConstants.RADAR_CORNER_RADIUS,
            RadiusY = LayoutConstants.RADAR_CORNER_RADIUS,
            Visibility = AppSettings.Instance.EnableLateralSpotter ? Visibility.Visible : Visibility.Collapsed
        };
        Canvas.SetLeft(_radarLeft, LayoutConstants.RADAR_EDGE_OFFSET);
        Canvas.SetTop(_radarLeft, LayoutConstants.RADAR_CENTER_OFFSET);
        outerCanvas.Children.Add(_radarLeft);

        // Right radar (right side - cars on right)
        _radarRight = new Rectangle
        {
            Width = LayoutConstants.RADAR_SQUARE_SIZE,
            Height = LayoutConstants.RADAR_SQUARE_SIZE,
            Fill = Brushes.Green, // Default: clear
            Stroke = new SolidColorBrush(_primaryColor),
            StrokeThickness = LayoutConstants.RADAR_STROKE_THICKNESS,
            RadiusX = LayoutConstants.RADAR_CORNER_RADIUS,
            RadiusY = LayoutConstants.RADAR_CORNER_RADIUS,
            Visibility = AppSettings.Instance.EnableLateralSpotter ? Visibility.Visible : Visibility.Collapsed
        };
        Canvas.SetLeft(_radarRight, LayoutConstants.RADAR_BACK_TOP);
        Canvas.SetTop(_radarRight, LayoutConstants.RADAR_CENTER_OFFSET);
        outerCanvas.Children.Add(_radarRight);
        
        // PHASE 2: Apply visual enhancements based on settings
        ApplyVisualEnhancements();
        
        // Top section: Value + Label (positioned absolutely in top portion of circle)
        _topStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, LayoutConstants.TOP_SECTION_MARGIN, 0, 0)
        };

        _topValueText = new TextBlock
        {
            Text = "0",
            FontFamily = new FontFamily("Consolas"),
            FontSize = LayoutConstants.FIELD_VALUE_FONT_SIZE,
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
            FontSize = LayoutConstants.FIELD_LABEL_FONT_SIZE,
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
            FontSize = LayoutConstants.CENTER_VALUE_FONT_SIZE,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(_primaryColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        _mainGrid.Children.Add(_centerValueText);

        // Brake bias transient overlay (hidden by default, appears when value changes)
        // Shows only the value (no label) with transparent background to avoid visual conflicts
        _brakeBiasLabel = new TextBlock
        {
            Text = "", // No label - hide it
            Visibility = Visibility.Collapsed
        };

        _brakeBiasValue = new TextBlock
        {
            Text = "50.0%",
            FontFamily = new FontFamily("Consolas"),
            FontSize = LayoutConstants.BRAKE_BIAS_FONT_SIZE,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(_secondaryColor),  // Orange
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        _brakeBiasOverlay = new Border
        {
            Background = Brushes.Transparent, // Transparent to match existing circle background
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Child = _brakeBiasValue
        };
        _mainGrid.Children.Add(_brakeBiasOverlay);

        // Bottom section: Value + Label (positioned absolutely in bottom portion of circle)
        _bottomStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, LayoutConstants.BOTTOM_SECTION_MARGIN)
        };

        _bottomValueText = new TextBlock
        {
            Text = "0",
            FontFamily = new FontFamily("Consolas"),
            FontSize = LayoutConstants.FIELD_VALUE_FONT_SIZE,
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
            FontSize = LayoutConstants.FIELD_LABEL_FONT_SIZE,
            FontWeight = FontWeights.Normal,
            Foreground = new SolidColorBrush(_primaryColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        _bottomStack.Children.Add(_bottomLabelText);

        _mainGrid.Children.Add(_bottomStack);

        // PHASE 2 FUEL CALCULATOR: Comprehensive multi-line fuel display below gauge
        // Shows: Current Fuel, Averages, Laps Remaining, Strategy Info
        // Position on CANVAS (not grid) to avoid clipping issues with negative margins
        _fuelDisplay = new TextBlock
        {
            Text = "",
            FontFamily = new FontFamily("Consolas"),
            FontSize = LayoutConstants.FUEL_DISPLAY_FONT_SIZE,
            FontWeight = FontWeights.Normal,
            Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), // Semi-transparent white
            TextAlignment = TextAlignment.Center,
            Visibility = Visibility.Collapsed, // Hidden by default, shown when fuel data available
            LineHeight = LayoutConstants.FUEL_DISPLAY_LINE_HEIGHT,
            TextWrapping = TextWrapping.NoWrap,
            Width = LayoutConstants.GRID_SIZE // Match grid width for proper centering
        };
        // Position fuel display on canvas below the gauge
        Canvas.SetLeft(_fuelDisplay, LayoutConstants.RADAR_OFFSET); // Align with grid left edge
        Canvas.SetTop(_fuelDisplay, LayoutConstants.FUEL_DISPLAY_TOP);
        outerCanvas.Children.Add(_fuelDisplay);

        // Left side data box (optional)
        _leftBox = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(LayoutConstants.SIDE_BOX_MARGIN_H, LayoutConstants.SIDE_BOX_MARGIN_V, 0, 0),
            Visibility = Visibility.Collapsed // Hidden by default
        };

        _leftLabelText = new TextBlock
        {
            Text = "",
            FontFamily = new FontFamily("Consolas"),
            FontSize = LayoutConstants.SIDE_BOX_LABEL_FONT_SIZE,
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
            FontSize = LayoutConstants.SIDE_BOX_VALUE_FONT_SIZE,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)
        };
        _leftBox.Children.Add(_leftValueText);

        _mainGrid.Children.Add(_leftBox);

        // Right side data box (optional)
        _rightBox = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, LayoutConstants.SIDE_BOX_MARGIN_V, LayoutConstants.SIDE_BOX_MARGIN_H, 0),
            Visibility = Visibility.Collapsed // Hidden by default
        };
        
        _rightLabelText = new TextBlock
        {
            Text = "",
            FontFamily = new FontFamily("Consolas"),
            FontSize = LayoutConstants.SIDE_BOX_LABEL_FONT_SIZE,
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
            FontSize = LayoutConstants.SIDE_BOX_VALUE_FONT_SIZE,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)
        };
        _rightBox.Children.Add(_rightValueText);

        _mainGrid.Children.Add(_rightBox);

        // No border - just the content (outer canvas with radar squares)
        Content = outerCanvas;

        // Subscribe to SizeChanged to update scale transform
        SizeChanged += OnWidgetSizeChanged;

        // Apply initial scale transform based on loaded size from config
        // (SizeChanged event won't fire if size was set before event handler was attached)
        if (Width > 0 && Height > 0)
        {
            double scale = Math.Min(Width / LayoutConstants.CANVAS_WIDTH, Height / LayoutConstants.CANVAS_WIDTH);
            outerCanvas.LayoutTransform = new ScaleTransform(scale, scale);
        }

        // Setup blinking timer for fuel warnings (slow blink)
        _blinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(LayoutConstants.FUEL_BLINK_INTERVAL_MS)
        };
        _blinkTimer.Tick += OnBlinkTimerTick;
        _blinkTimer.Start();

        // Setup radar blinking timer for front/back critical proximity (fast blink)
        _radarBlinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(LayoutConstants.RADAR_BLINK_INTERVAL_MS)
        };
        _radarBlinkTimer.Tick += OnRadarBlinkTimerTick;
        _radarBlinkTimer.Start();

        // Setup pit limiter blinking timer (fast blink) - PHASE 2: Enhancement #4
        _pitLimiterBlinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(LayoutConstants.PIT_LIMITER_BLINK_INTERVAL_MS)
        };
        _pitLimiterBlinkTimer.Tick += OnPitLimiterBlinkTimerTick;
        _pitLimiterBlinkTimer.Start(); // Always running, only acts when limiter active AND feature enabled

        // Setup brake bias hide timer (auto-hides overlay after configurable duration)
        double hideSeconds = AppSettings.Instance.BrakeBiasDisplayDuration;
        _brakeBiasHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(hideSeconds)
        };
        _brakeBiasHideTimer.Tick += OnBrakeBiasHideTimerTick;
        // Don't start timer yet - it starts when brake bias changes
        
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
        try
        {
            if (Config.Settings.TryGetValue("mrtone", out var settingsObj))
            {
                var json = JsonSerializer.Serialize(settingsObj);
                var settings = JsonSerializer.Deserialize<MRTOneSettings>(json);
                
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // Ignore errors, return defaults
        }
        
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
            _leftLabelText.Text = MRTOneDataFormatter.GetLabel(_leftField.Value, AppSettings.Instance.UseMetricUnits, AppSettings.Instance.CustomLabels);
        if (_rightField.HasValue)
            _rightLabelText.Text = MRTOneDataFormatter.GetLabel(_rightField.Value, AppSettings.Instance.UseMetricUnits, AppSettings.Instance.CustomLabels);
        
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
        // Calculate scale factors based on original 228x228 design (200 + 28 for radar boxes)
        double scaleX = ActualWidth / 228.0;
        double scaleY = ActualHeight / 228.0;
        
        // Use uniform scale (smallest of the two to maintain aspect ratio)
        double scale = Math.Min(scaleX, scaleY);
        
        // Apply scale transform to the outer canvas
        if (Content is Canvas canvas)
        {
            canvas.LayoutTransform = new ScaleTransform(scale, scale);
        }
    }
    
    private void OnBlinkTimerTick(object? sender, EventArgs e)
    {
        // Null guard: Ensure UI elements are initialized before accessing
        if (_leftValueText == null || _rightValueText == null)
            return;

        _blinkState = !_blinkState;

        // Apply blink effect to left box if showing critical fuel
        if (_leftField == TelemetryField.FuelLevel && _leftValueText.Foreground is SolidColorBrush leftBrush && leftBrush.Color == Colors.Red)
        {
            _leftValueText.Opacity = _blinkState ? 1.0 : 0.3;
        }
        else if (_leftField != TelemetryField.ABSActive && _leftField != TelemetryField.WheelLock && _leftField != TelemetryField.TractionControl) // Don't reset opacity for fields with state-based opacity
        {
            _leftValueText.Opacity = 1.0;
        }

        // Apply blink effect to right box if showing critical fuel
        if (_rightField == TelemetryField.FuelLevel && _rightValueText.Foreground is SolidColorBrush rightBrush && rightBrush.Color == Colors.Red)
        {
            _rightValueText.Opacity = _blinkState ? 1.0 : 0.3;
        }
        else if (_rightField != TelemetryField.ABSActive && _rightField != TelemetryField.WheelLock && _rightField != TelemetryField.TractionControl) // Don't reset opacity for fields with state-based opacity
        {
            _rightValueText.Opacity = 1.0;
        }
    }
    
    private void OnRadarBlinkTimerTick(object? sender, EventArgs e)
    {
        // Null guard: Ensure radar elements are initialized before accessing
        if (_radarFront == null || _radarBack == null)
            return;

        _radarBlinkState = !_radarBlinkState;

        // Apply FAST blink effect to FRONT radar square if VeryClose (<4m)
        var (frontZone, rearZone) = _stateManager.GetCurrentZones(); if (frontZone == ProximityZone.VeryClose)
        {
            _radarFront.Opacity = _radarBlinkState ? 1.0 : 0.3;
        }
        else
        {
            _radarFront.Opacity = 1.0;
        }

        // Apply FAST blink effect to REAR radar square if VeryClose (<4m)
        if (rearZone == ProximityZone.VeryClose)
        {
            _radarBack.Opacity = _radarBlinkState ? 1.0 : 0.3;
        }
        else
        {
            _radarBack.Opacity = 1.0;
        }
    }
    
    private void OnPitLimiterBlinkTimerTick(object? sender, EventArgs e)
    {
        // Null guard: Ensure gauge circle is initialized before accessing
        if (_gaugeCircle == null)
            return;

        // Only blink if feature is enabled AND pit limiter is active
        if (!_settings.EnablePitLimiterIndicator || !_stateManager.IsPitLimiterActive)
            return;

        _pitLimiterBlinkState = !_pitLimiterBlinkState;

        // Alternate: White → Orange → White... (MRT theme colors for high visibility)
        Color borderColor = _pitLimiterBlinkState ? Colors.White : _secondaryColor;
        _gaugeCircle.Stroke = new SolidColorBrush(borderColor);
    }

    private void OnBrakeBiasHideTimerTick(object? sender, EventArgs e)
    {
        // Null guard: Ensure UI elements are initialized before accessing
        if (_brakeBiasOverlay == null || _centerValueText == null || _leftBox == null || _rightBox == null)
            return;

        _brakeBiasHideTimer?.Stop();

        // Hide brake bias overlay and restore center + left/right sections
        _brakeBiasOverlay.Visibility = Visibility.Collapsed;
        _centerValueText.Visibility = Visibility.Visible;  // Restore center section (Gear)

        // Restore left/right boxes if they were configured (check if fields are set)
        if (_leftField.HasValue)
            _leftBox.Visibility = Visibility.Visible;
        if (_rightField.HasValue)
            _rightBox.Visibility = Visibility.Visible;

        _brakeBiasVisible = false;
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
        
        // Initialize ABS opacity to inactive state (0.3) when field is assigned
        // This ensures correct initial display before first telemetry update
        if (_leftField == TelemetryField.ABSActive)
        {
            _leftValueText.Opacity = 0.3; // Start dimmed (inactive state)
            _leftLabelText.Opacity = 0.3;
            _lastLeftABSValue = -1; // Reset cache to force update on first telemetry
        }
        else if (_leftField == TelemetryField.WheelLock)
        {
            _leftValueText.Opacity = 0.3; // Start dimmed (no lockup state)
            _leftLabelText.Opacity = 0.3;
            _leftValueText.Foreground = new SolidColorBrush(_primaryColor); // Start with teal (inactive)
            _lastLeftLockupValue = -1; // Reset cache to force update on first telemetry
        }
        else
        {
            _leftValueText.Opacity = 1.0; // Normal opacity for other fields
            _leftLabelText.Opacity = 1.0;
        }
        
        if (_rightField == TelemetryField.ABSActive)
        {
            _rightValueText.Opacity = 0.3; // Start dimmed (inactive state)
            _rightLabelText.Opacity = 0.3;
            _lastRightABSValue = -1; // Reset cache to force update on first telemetry
        }
        else if (_rightField == TelemetryField.WheelLock)
        {
            _rightValueText.Opacity = 0.3; // Start dimmed (no lockup state)
            _rightLabelText.Opacity = 0.3;
            _rightValueText.Foreground = new SolidColorBrush(_primaryColor); // Start with teal (inactive)
            _lastRightLockupValue = -1; // Reset cache to force update on first telemetry
        }
        else
        {
            _rightValueText.Opacity = 1.0; // Normal opacity for other fields
            _rightLabelText.Opacity = 1.0;
        }
        
        // Update labels
        if (_leftField.HasValue)
            _leftLabelText.Text = MRTOneDataFormatter.GetLabel(_leftField.Value, AppSettings.Instance.UseMetricUnits, AppSettings.Instance.CustomLabels);
        if (_rightField.HasValue)
            _rightLabelText.Text = MRTOneDataFormatter.GetLabel(_rightField.Value, AppSettings.Instance.UseMetricUnits, AppSettings.Instance.CustomLabels);
        
        // Force immediate UI refresh
        if (_lastTelemetryData != null)
        {
            UpdateUI(_lastTelemetryData);
        }
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
        
        // PHASE 2 FUEL CALCULATOR: Update fuel display (simple 1-line below gauge)
        UpdateFuelDisplay();
        
        // BRAKE BIAS OVERLAY: Show temporarily when value changes (if enabled in settings)
        // Only trigger after initialization to prevent showing on connection/getting in car
        if (AppSettings.Instance.ShowBrakeBiasOverlay)
        {
            float currentBrakeBias = data.BrakeBias;
            
            // First time seeing brake bias value - just initialize, don't show overlay
            var biasChange = _stateManager.CheckBrakeBiasChange(currentBrakeBias); if (biasChange.ShowOverlay)
            {
                
                
            }
            // Subsequent changes - only show if value actually changed (user adjusted it)
            else if (Math.Abs(currentBrakeBias - biasChange.Value) > 0.01f) // Changed by >0.01%
            {
                
                
                // Update display value (use InvariantCulture to ensure "." decimal separator)
                _brakeBiasValue.Text = currentBrakeBias.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "%";
                
                // Show overlay and hide center + left/right sections (keep top/bottom visible)
                if (!_brakeBiasVisible)
                {
                    _brakeBiasOverlay.Visibility = Visibility.Visible;
                    _centerValueText.Visibility = Visibility.Collapsed;  // Hide center section (gear)
                    _leftBox.Visibility = Visibility.Collapsed;          // Hide left side box
                    _rightBox.Visibility = Visibility.Collapsed;         // Hide right side box
                    // Keep top and bottom sections visible
                    _brakeBiasVisible = true;
                }
                
                // Reset hide timer (keep visible while adjusting)
                _brakeBiasHideTimer?.Stop();
                _brakeBiasHideTimer?.Start();
            }
        }
        
        // Update LEFT side box
        if (_leftField.HasValue)
        {
            var leftValue = TelemetryDataMapper.GetValue(_leftField.Value, data, _telemetryService) ?? 0;
            _leftValueText.Text = MRTOneDataFormatter.FormatValue(_leftField.Value, leftValue, data, AppSettings.Instance.UseMetricUnits);
            _leftValueText.Foreground = new SolidColorBrush(MRTOneDataFormatter.GetValueColor(_leftField.Value, leftValue, data, _primaryColor, _secondaryColor));
            
            // ABS special handling: Fade to background when inactive, bright when active
            // Use StateManager to prevent flicker with cached state comparison
            if (_leftField.Value == TelemetryField.ABSActive && leftValue is int absValue)
            {
                var absState = _stateManager.GetABSOpacity(absValue, 0);
                if (absState.HasChanged)
                {
                    _leftValueText.Opacity = absState.LeftOpacity;
                    _leftLabelText.Opacity = absState.LeftOpacity;
                }
            }
            // Traction Control special handling: 
            // - Dim when N/A (car doesn't have TC) or OFF (TC = 0)
            // - Orange when TC > 0 (enabled and potentially active)
            // Only update when TC state changes (prevents flicker)
            // BACKWARD COMPAT: Handle both int (new) and float (old binary during hot reload)
            else if (_leftField.Value == TelemetryField.TractionControl)
            {
                int tcValue = leftValue switch
                {
                    int i => i,
                    float f => (int)f, // Backward compat during hot reload
                    _ => -1
                };
                
                var tcState = _stateManager.GetTCOpacity(tcValue, 0);
                if (tcState.HasChanged)
                {
                    _leftValueText.Opacity = tcState.LeftOpacity;
                    _leftLabelText.Opacity = tcState.LeftOpacity;

                    // Color: Orange when TC > 0 (enabled), Teal when OFF/N/A
                    Color tcColor = tcValue > 0 ? _secondaryColor : _primaryColor;
                    _leftValueText.Foreground = new SolidColorBrush(tcColor);
                }
                // Note: If state hasn't changed, keep current opacity (don't reset)
            }
            // Wheel Lockup special handling: Dim when OK, bright RED when locked
            // Only update opacity when state changes (prevents flicker from rapid oscillation)
            else if (_leftField.Value == TelemetryField.WheelLock && leftValue is int lockupValue)
            {
                var lockupState = _stateManager.GetLockupOpacity(lockupValue, 0);
                if (lockupState.HasChanged)
                {
                    _leftValueText.Opacity = lockupState.LeftOpacity;
                    _leftLabelText.Opacity = lockupState.LeftOpacity;
                }
            }
            else if (_leftField.Value != TelemetryField.ABSActive && _leftField.Value != TelemetryField.FuelLevel && _leftField.Value != TelemetryField.TractionControl && _leftField.Value != TelemetryField.WheelLock)
            {
                // Only reset opacity for fields that don't have special blink handling
                // (FuelLevel has blink timer, ABSActive has state-based opacity)
                _leftValueText.Opacity = 1.0;
                _leftLabelText.Opacity = 1.0;
            }
            
            // Update label text with units (e.g., "FUEL (L)", "OIL (°C)")
            _leftLabelText.Text = MRTOneDataFormatter.GetLabel(_leftField.Value, AppSettings.Instance.UseMetricUnits, AppSettings.Instance.CustomLabels);
        }
        
        // Update RIGHT side box
        if (_rightField.HasValue)
        {
            var rightValue = TelemetryDataMapper.GetValue(_rightField.Value, data, _telemetryService) ?? 0;
            _rightValueText.Text = MRTOneDataFormatter.FormatValue(_rightField.Value, rightValue, data, AppSettings.Instance.UseMetricUnits);
            _rightValueText.Foreground = new SolidColorBrush(MRTOneDataFormatter.GetValueColor(_rightField.Value, rightValue, data, _primaryColor, _secondaryColor));
            
            // ABS special handling: Fade to background when inactive, bright when active
            // Use StateManager to prevent flicker with cached state comparison
            if (_rightField.Value == TelemetryField.ABSActive && rightValue is int absValue)
            {
                var absStateRight = _stateManager.GetABSOpacity(0, absValue);
                if (absStateRight.HasChanged)
                {
                    _rightValueText.Opacity = absStateRight.RightOpacity;
                    _rightLabelText.Opacity = absStateRight.RightOpacity;
                }
            }
            // Traction Control special handling:
            // - Dim when N/A (car doesn't have TC) or OFF (TC = 0)
            // - Orange when TC > 0 (enabled and potentially active)
            // Only update when TC state changes (prevents flicker)
            // BACKWARD COMPAT: Handle both int (new) and float (old binary during hot reload)
            else if (_rightField.Value == TelemetryField.TractionControl)
            {
                int tcValue = rightValue switch
                {
                    int i => i,
                    float f => (int)f, // Backward compat during hot reload
                    _ => -1
                };
                
                var tcStateRight = _stateManager.GetTCOpacity(0, tcValue);
                if (tcStateRight.HasChanged)
                {
                    _rightValueText.Opacity = tcStateRight.RightOpacity;
                    _rightLabelText.Opacity = tcStateRight.RightOpacity;

                    // Color: Orange when TC > 0 (enabled), Teal when OFF/N/A
                    Color tcColor = tcValue > 0 ? _secondaryColor : _primaryColor;
                    _rightValueText.Foreground = new SolidColorBrush(tcColor);
                }
                // Note: If state hasn't changed, keep current opacity (don't reset)
            }
            // Wheel Lockup special handling: Dim when OK, bright RED when locked
            // Only update opacity when state changes (prevents flicker from rapid oscillation)
            else if (_rightField.Value == TelemetryField.WheelLock && rightValue is int lockupValue)
            {
                if (lockupValue != _lastRightLockupValue)
                {
                    // Opacity: 0.3 when no lockup (value=0), 1.0 when locked (value=1)
                    _rightValueText.Opacity = lockupValue == 1 ? 1.0 : 0.3; // Bright when LOCKED, dim when OK
                    _rightLabelText.Opacity = lockupValue == 1 ? 1.0 : 0.3; // Sync label opacity
                    _lastRightLockupValue = lockupValue; // Cache to prevent redundant updates
                }
            }
            else if (_rightField.Value != TelemetryField.ABSActive && _rightField.Value != TelemetryField.FuelLevel && _rightField.Value != TelemetryField.TractionControl && _rightField.Value != TelemetryField.WheelLock)
            {
                // Only reset opacity for fields that don't have special blink handling
                // (FuelLevel has blink timer, ABSActive has state-based opacity)
                _rightValueText.Opacity = 1.0;
                _rightLabelText.Opacity = 1.0;
            }
            
            // Update label text with units (e.g., "FUEL (L)", "OIL (°C)")
            _rightLabelText.Text = MRTOneDataFormatter.GetLabel(_rightField.Value, AppSettings.Instance.UseMetricUnits, AppSettings.Instance.CustomLabels);
        }
        
        // PRIORITY 1: Pit limiter (if enabled) overrides RPM zone colors - PHASE 2: Enhancement #4
        bool pitLimiterActive = data.PitSpeedLimiterActive;
        var pitLimiterUpdate = _stateManager.UpdatePitLimiter(pitLimiterActive);
        if (pitLimiterUpdate.Changed && !pitLimiterUpdate.IsActive)
        {
            // Pit limiter deactivated - reset blink state (color will restore below)
            _pitLimiterBlinkState = false;
        }

        // Only update RPM zone color if pit limiter is NOT active (or feature is disabled)
        // When pit limiter is active and feature enabled, the blink timer handles the color
        if (!_settings.EnablePitLimiterIndicator || !_stateManager.IsPitLimiterActive)
        {
            // Update gauge circle color based on RPM zone
            var rpm = data.RPM;
            var zone = ShiftPointCalculator.GetRPMZone(
                rpm, 
                data.Gear, 
                data.PlayerCarSLFirstRPM, 
                data.PlayerCarSLShiftRPM, 
                data.PlayerCarSLLastRPM, 
                data.PlayerCarSLBlinkRPM);
            
            Color borderColor = zone switch
            {
                ShiftPointCalculator.RPMZone.Danger => Colors.Red,         // RED - at limiter
                ShiftPointCalculator.RPMZone.Optimal => _secondaryColor,   // ORANGE - optimal shift
                ShiftPointCalculator.RPMZone.Warning => Colors.Yellow,     // YELLOW - approaching shift
                _ => _primaryColor                                          // TEAL - safe range
            };
            
            _gaugeCircle.Stroke = new SolidColorBrush(borderColor);
        }


















        
        // PHASE 1: Update 4-way radar spotter squares
        if (AppSettings.Instance.EnableLateralSpotter)
        {
            UpdateRadarSquares(data);
        }
    }
    
    /// <summary>
    /// Update a section (value + label) dynamically based on field type
    /// Uses consistent formatting with side boxes
    /// </summary>
    private void UpdateSection(TextBlock valueText, TextBlock? labelText, TelemetryField field, TelemetryData data)
    {
        // Use telemetry service overload for fuel calculation fields
        var value = TelemetryDataMapper.GetValue(field, data, _telemetryService);
        
        // Use consistent formatting functions
        valueText.Text = MRTOneDataFormatter.FormatValue(field, value ?? 0, data, AppSettings.Instance.UseMetricUnits);
        
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
            var zone = ShiftPointCalculator.GetRPMZone(
                rpm, 
                data.Gear, 
                data.PlayerCarSLFirstRPM, 
                data.PlayerCarSLShiftRPM, 
                data.PlayerCarSLLastRPM, 
                data.PlayerCarSLBlinkRPM);
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
            valueText.Foreground = new SolidColorBrush(MRTOneDataFormatter.GetValueColor(field, value ?? 0, data, _primaryColor, _secondaryColor));
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
                labelText.Text = MRTOneDataFormatter.GetLabel(field, AppSettings.Instance.UseMetricUnits, AppSettings.Instance.CustomLabels);
                labelText.Foreground = new SolidColorBrush(_primaryColor);
                labelText.Visibility = Visibility.Visible;
            }
        }
    }
    
    /// <summary>
    /// PHASE 1: Update 4-way radar spotter squares based on proximity detection
    /// </summary>
    private void UpdateRadarSquares(TelemetryData data)
    {
        // Update LEFT/RIGHT squares using LateralSpotter
        // TRUST THE SDK: CarLeftRight uses 3D position data we don't have access to
        // It knows true lateral positioning (left/right), whereas LapDistPct only knows ahead/behind
        // The SDK internally validates when cars are truly BESIDE the player (not ahead/behind)
        // We should NOT second-guess this with our own distance validation
        //
        // CRITICAL FIX: SDK enum is 0=Off, 1=Clear, 2=CarLeft, 3=CarRight, 4=CarBothSides, 5=TwoCarsLeft, 6=TwoCarsRight
        // (NOT 0=Clear as originally assumed!)
        var lateralPosition = _lateralSpotter.GetLateralPosition(data);
        
        // Set left/right based on SDK lateral position (includes single and double car detections)
        bool hasLeft = lateralPosition == LateralPosition.CarLeft || 
                       lateralPosition == LateralPosition.CarBothSides ||
                       lateralPosition == LateralPosition.TwoCarsLeft;
        
        bool hasRight = lateralPosition == LateralPosition.CarRight || 
                        lateralPosition == LateralPosition.CarBothSides ||
                        lateralPosition == LateralPosition.TwoCarsRight;
        
        _radarLeft.Fill = hasLeft
            ? Brushes.Red    // Car(s) present on left
            : Brushes.Green; // Clear on left
        
        _radarRight.Fill = hasRight
            ? Brushes.Red    // Car(s) present on right
            : Brushes.Green; // Clear on right
        
        // Update FRONT/BACK squares using ProximityCalculator
        var frontZone = _proximityCalculator.GetFrontZone(data);
        var rearZone = _proximityCalculator.GetRearZone(data);
        
        // Track current zones for blinking animation
        _stateManager.UpdateProximityZones(frontZone, rearZone);
        
        
        _radarFront.Fill = GetZoneColor(frontZone);
        _radarBack.Fill = GetZoneColor(rearZone);
    }
    
    /// <summary>
    /// Get color for proximity zone (RACING-TIGHT thresholds)
    /// </summary>
    private Brush GetZoneColor(ProximityZone zone)
    {
        return zone switch
        {
            ProximityZone.VeryClose => Brushes.Red,      // <4m - CRITICAL (will blink)
            ProximityZone.Close => Brushes.Red,          // 4-7m - WARNING (solid red)
            ProximityZone.Near => Brushes.Orange,        // 7-12m - CAUTION
            ProximityZone.Careful => Brushes.Yellow,     // 12-16m - CAREFUL
            ProximityZone.Far => Brushes.Green,          // >16m - SAFE
            _ => Brushes.Green                           // Clear (no cars detected)
        };
    }
    
    /// <summary>
    /// PHASE 2 FUEL CALCULATOR: Update comprehensive fuel display below gauge.
    /// Shows: Current Fuel, Last/L5/L10 Averages, Laps Remaining, Min/Max, Delta to Finish
    /// </summary>
    private void UpdateFuelDisplay()
    {
        try
        {
            // Early exit if fuel display is disabled
            if (!_settings.EnableFuelDisplay)
            {
                if (_fuelDisplay != null)
                    _fuelDisplay.Visibility = Visibility.Collapsed;
                return;
            }
            
            // Safety check: Ensure fuel display element is initialized
            if (_fuelDisplay == null)
                return;
            
            // Get fuel data from telemetry service (with null safety)
            var fuelData = _telemetryService?.CurrentFuelData;
            
            // Additional null check for fuel data
            if (fuelData == null)
            {
                _fuelDisplay.Visibility = Visibility.Collapsed;
                return;
            }
        
        // Show fuel display as soon as we have ANY data (even if CurrentFuel is 0)
        // This fixes the issue where display never appeared while driving
        if (fuelData.HasSufficientData)
        {
            // Build comprehensive fuel display (compact multi-line format)
            var fuelText = new System.Text.StringBuilder();
            
            // Line 1: Current fuel and tank capacity
            fuelText.AppendLine($"FUEL: {fuelData.CurrentFuel:F2}L / {fuelData.TankCapacity:F1}L ({fuelData.FuelPct:F0}%)");
            
            // Line 2: Averages (Last, L5 with trend, L10 with trend, Session)
            // Calculate trend indicators for L5 and L10 (+ if increasing, - if decreasing)
            string l5Trend = "";
            string l10Trend = "";
            
            // Compare current with last lap to show trend
            if (fuelData.LapToLapDelta != 0)
            {
                // L5 trend: If last lap fuel is higher than L5 average, it means L5 is decreasing (good!)
                // If last lap fuel is lower than L5 average, it means L5 is increasing (bad!)
                float l5Delta = fuelData.AvgFuelPerLap_Last - fuelData.AvgFuelPerLap_L5;
                if (Math.Abs(l5Delta) > 0.01f) // Only show if meaningful difference
                {
                    l5Trend = l5Delta > 0 ? " +" : " -";
                }
                
                // L10 trend: Same logic as L5
                float l10Delta = fuelData.AvgFuelPerLap_Last - fuelData.AvgFuelPerLap_L10;
                if (Math.Abs(l10Delta) > 0.01f) // Only show if meaningful difference
                {
                    l10Trend = l10Delta > 0 ? " +" : " -";
                }
            }
            
            fuelText.AppendLine($"AVG: L:{fuelData.AvgFuelPerLap_Last:F2} | 5:{fuelData.AvgFuelPerLap_L5:F2}{l5Trend} | 10:{fuelData.AvgFuelPerLap_L10:F2}{l10Trend} | S:{fuelData.AvgFuelPerLap_Session:F2}");
            
            // Line 3: Min/Max and laps remaining (1 decimal for precision) with iRacing delta
            string lapsDeltaStr = fuelData.LapsDifference >= 0 
                ? $"+{fuelData.LapsDifference:F1}" 
                : $"{fuelData.LapsDifference:F1}";
            fuelText.AppendLine($"RANGE: {fuelData.MinFuelPerLap:F2}-{fuelData.MaxFuelPerLap:F2}L | LAPS: {fuelData.LapsRemaining:F1} (iR: {fuelData.IRacingLapsRemaining:F1}, Δ {lapsDeltaStr})");
            
            // Line 4: Delta to finish and fuel needed (TO GO + NEED - prominently shown)
            if (fuelData.RaceLapsRemaining > 0)
            {
                // RACE MODE: TO GO = FuelDeltaToFinish (accounts for race laps + buffer + 0.3L finish threshold)
                string deltaStr = fuelData.FuelDeltaToFinish >= 0 
                    ? $"+{fuelData.FuelDeltaToFinish:F2}L" 
                    : $"{fuelData.FuelDeltaToFinish:F2}L";
                
                // NEED: Calculate fuel needed WITHOUT buffer, just race laps + 0.3L finish threshold
                // This gives the minimum fuel to finish (no safety buffer)
                float fuelNeededNoBuffer = (fuelData.RaceLapsRemaining * fuelData.AvgFuelPerLap_L5) + fuelData.FuelSputteringThreshold;
                float fuelToAddNoBuffer = Math.Max(0, fuelNeededNoBuffer - fuelData.CurrentFuel);
                
                string needStr = fuelToAddNoBuffer > 0.1f  // >0.1L threshold to avoid showing 0.0L
                    ? $"{fuelToAddNoBuffer:F2}L" 
                    : "OK";
                
                fuelText.AppendLine($"TO FINISH: {fuelData.FuelNeededToFinish:F2}L ({deltaStr}) | NEED {needStr}");
            }
            else
            {
                // QUALIFYING/PRACTICE MODE: 5-lap reference for TO GO
                float fivelapFuel = fuelData.AvgFuelPerLap_L5 * 5;
                float deltaToFiveLaps = fuelData.CurrentFuel - fivelapFuel;
                string deltaStr = deltaToFiveLaps >= 0 
                    ? $"+{deltaToFiveLaps:F2}L" 
                    : $"{deltaToFiveLaps:F2}L";
                fuelText.AppendLine($"5-LAP REF: {fivelapFuel:F2}L ({deltaStr})");
            }
            
            // Line 5: Green/Yellow flag averages (if available)
            if (fuelData.GreenFlagLapCount > 0 || fuelData.YellowFlagLapCount > 0)
            {
                string flagInfo = "";
                if (fuelData.GreenFlagLapCount > 0)
                    flagInfo += $"GREEN: {fuelData.GreenFlagAverage:F2}L ({fuelData.GreenFlagLapCount})";
                if (fuelData.YellowFlagLapCount > 0)
                    flagInfo += (flagInfo.Length > 0 ? " | " : "") + $"YELLOW: {fuelData.YellowFlagAverage:F2}L ({fuelData.YellowFlagLapCount})";
                fuelText.AppendLine(flagInfo);
            }
            
            // PHASE 2: Multi-Stint Strategy (Toggle-able via Visual Settings)
            if (_settings.EnableFuelStrategy && fuelData.RaceLapsRemaining > 0 && fuelData.AvgFuelPerLap_L5 > 0)
            {
                fuelText.AppendLine(""); // Blank line separator
                fuelText.AppendLine("═══ PIT STRATEGY ═══");
                
                // Calculate stint scenarios
                float avgFuel = fuelData.AvgFuelPerLap_L5;
                float tankCap = fuelData.TankCapacity;
                int totalLaps = fuelData.RaceLapsRemaining;
                float currentFuel = fuelData.CurrentFuel;
                
                // NO-STOP Strategy (if possible)
                if (fuelData.CanFinishWithoutStop)
                {
                    fuelText.AppendLine($"✓ NO-STOP: Current fuel sufficient ({fuelData.FuelDeltaToFinish:+0.0;-0.0}L surplus)");
                }
                
                // 1-STOP Strategy
                float lapsOnCurrentFuel = currentFuel / avgFuel;
                float lapsOnFullTank = tankCap / avgFuel;
                
                if (lapsOnCurrentFuel + lapsOnFullTank >= totalLaps)
                {
                    // Can finish with 1 stop
                    int optimalPitLap = (int)Math.Floor(lapsOnCurrentFuel);
                    int lapsAfterPit = totalLaps - optimalPitLap;
                    float fuelToAdd = lapsAfterPit * avgFuel;
                    fuelText.AppendLine($"1-STOP: Pit @ L{optimalPitLap} → Add {fuelToAdd:F1}L");
                }
                else
                {
                    fuelText.AppendLine($"1-STOP: NOT POSSIBLE (need {(totalLaps * avgFuel - currentFuel - tankCap):F1}L more capacity)");
                }
                
                // 2-STOP Strategy
                if (lapsOnFullTank * 2 >= totalLaps)
                {
                    // Calculate optimal 2-stop windows
                    float lapsPerStint = totalLaps / 3.0f; // Divide race into 3 stints
                    int firstPit = (int)Math.Min(lapsOnCurrentFuel, lapsPerStint);
                    int secondPit = firstPit + (int)lapsOnFullTank;
                    float firstStopFuel = Math.Min(tankCap, lapsPerStint * avgFuel);
                    float secondStopFuel = Math.Min(tankCap, (totalLaps - secondPit) * avgFuel);
                    fuelText.AppendLine($"2-STOP: L{firstPit} ({firstStopFuel:F1}L), L{secondPit} ({secondStopFuel:F1}L)");
                }
                else
                {
                    fuelText.AppendLine($"2-STOP: NOT POSSIBLE (tank too small for race distance)");
                }
                
                // Pit Window (show as range if available, otherwise show conservative pit lap)
                if (fuelData.PitWindowStart > 0 && fuelData.PitWindowEnd > 0 && fuelData.PitWindowStart <= fuelData.PitWindowEnd)
                {
                    // Show window as range (e.g., "PIT WINDOW 10-15")
                    fuelText.AppendLine($"⚠ PIT WINDOW: L{fuelData.PitWindowStart}-{fuelData.PitWindowEnd}");
                }
                else
                {
                    // Fallback: Calculate conservative pit lap if window not available
                    int conservativePitLap = (int)Math.Floor(lapsOnCurrentFuel * 0.9f); // 10% safety margin
                    fuelText.AppendLine($"⚠ SAFE WINDOW: Pit by L{conservativePitLap} (90% fuel buffer)");
                }
            }
            
            _fuelDisplay.Text = fuelText.ToString().TrimEnd();
            _fuelDisplay.Visibility = Visibility.Visible;
            
            // Simplified color-code based on laps remaining (orange ≥2 laps, red <2 laps)
            Color fuelColor = fuelData.LapsRemaining >= 2
                ? Color.FromRgb(255, 128, 0)   // Orange - SOON
                : Colors.Red;                   // Red - URGENT
            
            _fuelDisplay.Foreground = new SolidColorBrush(Color.FromArgb(200, fuelColor.R, fuelColor.G, fuelColor.B));
        }
        else if (fuelData.CurrentFuel > 0 || fuelData.TankCapacity > 0)
        {
            // Show minimal info if we have fuel but not enough data for averages
            // OR if tank capacity is known but fuel reading isn't available yet
            _fuelDisplay.Text = $"FUEL: {fuelData.CurrentFuel:F2}L / {fuelData.TankCapacity:F1}L\n(Need more laps for calculations)";
            _fuelDisplay.Visibility = Visibility.Visible;
            _fuelDisplay.Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)); // Dimmed white
        }
        else
        {
            // Hide fuel display if no valid data yet
            _fuelDisplay.Visibility = Visibility.Collapsed;
        }
        }
        catch (Exception ex)
        {
            // Log exception for debugging instead of silently swallowing errors
            System.Diagnostics.Debug.WriteLine($"[MRTOne] Fuel display update error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[MRTOne] Stack trace: {ex.StackTrace}");

            // Hide fuel display on error to prevent widget crash
            if (_fuelDisplay != null)
                _fuelDisplay.Visibility = Visibility.Collapsed;
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

        // Reset all cached state when disconnecting to prevent stale data on reconnect
        if (status == ConnectionStatus.Disconnected)
        {
            // Reset brake bias state
        }
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
        
        // PHASE 1: Update radar squares visibility based on EnableLateralSpotter setting
        var radarVisibility = AppSettings.Instance.EnableLateralSpotter ? Visibility.Visible : Visibility.Collapsed;
        _radarFront.Visibility = radarVisibility;
        _radarBack.Visibility = radarVisibility;
        _radarLeft.Visibility = radarVisibility;
        _radarRight.Visibility = radarVisibility;
        
        // Update brake bias overlay timer interval if duration setting changed
        if (_brakeBiasHideTimer != null)
        {
            _brakeBiasHideTimer.Interval = TimeSpan.FromSeconds(AppSettings.Instance.BrakeBiasDisplayDuration);
        }
        
        // CRITICAL FIX: Reload MRTOne-specific settings and reapply visual enhancements
        _settings = LoadSettings();
        ApplyVisualEnhancements();
        
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
        
        // CRITICAL FIX: Reload MRTOne settings and reapply visual enhancements
        // This ensures gradient background, shift ring, and other visual features
        // are properly restored when locking/unlocking or loading saved layouts
        _settings = LoadSettings();
        ApplyVisualEnhancements();
    }
    
    // ============================================
    // PHASE 2: TOGGLEABLE VISUAL ENHANCEMENTS
    // All features can be enabled/disabled via settings
    // Easy rollback: Set all Enable* flags to false
    // ============================================
    
    /// <summary>
    /// Apply all visual enhancements based on current settings
    /// Call this whenever settings change to update visual features
    /// Enhancement 1: Gradient Background
    /// Enhancement 2: Shift Point Ring
    /// Enhancement 3: Glow Effects
    /// Enhancement 4: Pit Limiter Indicator (handled in UpdateUI, enabled/disabled via settings)
    /// </summary>
    private void ApplyVisualEnhancements()
    {
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
        
        // Enhancement 4: Fuel Display - trigger update instead of just toggling visibility
        // Let UpdateFuelDisplay() handle visibility based on both settings AND data availability
        UpdateFuelDisplay();
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
            Width = LayoutConstants.RPM_BEAD_SIZE,
            Height = LayoutConstants.RPM_BEAD_SIZE,
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
            Interval = TimeSpan.FromMilliseconds(LayoutConstants.RPM_ANIMATION_INTERVAL_MS)
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
        var zone = ShiftPointCalculator.GetRPMZone(
            rpm, 
            _lastTelemetryData.Gear, 
            _lastTelemetryData.PlayerCarSLFirstRPM, 
            _lastTelemetryData.PlayerCarSLShiftRPM, 
            _lastTelemetryData.PlayerCarSLLastRPM, 
            _lastTelemetryData.PlayerCarSLBlinkRPM);
        
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
        if (angle >= LayoutConstants.RPM_CENTER_ANGLE_THRESHOLD)  // Above 94.4% RPM, start centering horizontally
        {
            // Gradually reduce horizontal offset as we approach 270°
            // This prevents flickering at high RPM
            double centeringFactor = Math.Min(1.0, (angle - LayoutConstants.RPM_CENTER_ANGLE_THRESHOLD) / LayoutConstants.RPM_CENTER_SMOOTHING);  // 0 at threshold, 1 at 270°
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
                BlurRadius = LayoutConstants.GLOW_BLUR_RADIUS_CENTER,
                ShadowDepth = 0,
                Opacity = LayoutConstants.GLOW_OPACITY_CENTER
            };
        }

        // Add subtle glow to gauge circle border - with null check
        if (_gaugeCircle != null)
        {
            _gaugeCircle.Effect = new DropShadowEffect
            {
                Color = _primaryColor,
                BlurRadius = LayoutConstants.GLOW_BLUR_RADIUS_GAUGE,
                ShadowDepth = 0,
                Opacity = LayoutConstants.GLOW_OPACITY_GAUGE
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
        // Clean up timers
        _blinkTimer?.Stop();
        _radarBlinkTimer?.Stop();
        _pitLimiterBlinkTimer?.Stop();
        
        // Clean up Phase 2 resources
        RemoveShiftPointRing();
        
        AppSettings.Instance.SettingsChanged -= OnSettingsChanged;
        base.OnClosed(e);
    }
}
