using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.Core.Telemetry;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.FuelWidgets
{
    /// <summary>
    /// Enhanced Fuel Widget with FuelCalculatorService integration
    /// Displays fuel level, consumption, and race strategy data
    /// Supports three layouts: Tower (vertical), Bar (horizontal), Grid (2x2)
    /// </summary>
    public partial class FuelWidget : WidgetBase
    {
        #region Layout Constants

        /// <summary>
        /// Layout and sizing constants for the Fuel Widget
        /// </summary>
        private static class LayoutConstants
        {
            // === Widget Dimensions ===
            /// <summary>Base widget width</summary>
            public const double BASE_WIDTH = 240;
            /// <summary>Minimum widget height</summary>
            public const double MIN_HEIGHT = 200;
            /// <summary>Maximum widget height</summary>
            public const double MAX_HEIGHT = 600;

            // === UI Element Heights ===
            /// <summary>Fuel bar graph height</summary>
            public const double FUEL_BAR_HEIGHT = 18;
            /// <summary>Sparkline display height</summary>
            public const double SPARKLINE_HEIGHT = 24;

            // === Timing Constants (milliseconds) ===
            /// <summary>Fuel warning blink timer interval</summary>
            public const int FUEL_BLINK_INTERVAL_MS = 250;
            /// <summary>Live fuel update timer interval (0.5 second)</summary>
            public const int LIVE_UPDATE_INTERVAL_MS = 500;

            // === Threshold Values ===
            /// <summary>Critical laps remaining threshold (hide PIT IN when below)</summary>
            public const float CRITICAL_LAPS_THRESHOLD = 0.3f;
            /// <summary>Near finish fuel threshold (hide PIT IN when below)</summary>
            public const float NEAR_FINISH_FUEL_THRESHOLD = 0.6f;
            /// <summary>Urgent laps remaining threshold (red color)</summary>
            public const float URGENT_LAPS_THRESHOLD = 2.0f;
        }

        #endregion

        #region Cached Brushes

        /// <summary>
        /// Static cached brushes for improved performance and memory usage
        /// Frozen for better rendering performance (shared across all widget instances)
        /// </summary>
        private static class CachedBrushes
        {
            public static readonly Brush Teal;
            public static readonly Brush Orange;
            public static readonly Brush Red;
            public static readonly Brush Green;
            public static readonly Brush Gray;
            public static readonly Brush Yellow;
            public static readonly Brush Cyan;
            public static readonly Brush GreenDark;
            public static readonly Brush GrayLight;
            public static readonly Brush RedDark;
            public static readonly Brush OrangeDark;
            public static readonly Brush GreenVeryDark;
            public static readonly Brush GrayDark;
            public static readonly Brush YellowLight;
            public static readonly Brush RedLight;

            static CachedBrushes()
            {
                // Create and freeze all brushes for better performance
                Teal = CreateFrozenBrush(0, 128, 128);
                Orange = CreateFrozenBrush(255, 128, 0);
                Red = CreateFrozenBrush(255, 51, 51);
                Green = CreateFrozenBrush(0, 255, 0);
                Gray = CreateFrozenBrush(102, 102, 102);
                Yellow = CreateFrozenBrush(255, 255, 0);
                Cyan = CreateFrozenBrush(0, 217, 255);
                GreenDark = CreateFrozenBrush(76, 175, 80);
                GrayLight = CreateFrozenBrush(170, 170, 170);
                RedDark = CreateFrozenBrush(183, 28, 28);
                OrangeDark = CreateFrozenBrush(255, 111, 0);
                GreenVeryDark = CreateFrozenBrush(27, 94, 32);
                GrayDark = CreateFrozenBrush(66, 66, 66);
                YellowLight = CreateFrozenBrush(255, 193, 7);
                RedLight = CreateFrozenBrush(244, 67, 54);
            }

            private static Brush CreateFrozenBrush(byte r, byte g, byte b)
            {
                var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
                brush.Freeze(); // Freeze for better performance
                return brush;
            }
        }

        #endregion

        private readonly FuelCalculatorService _fuelCalculator;
        private DispatcherTimer? _blinkTimer;
        private bool _isBlinkVisible = true;

        // Sparkline data tracking
        private readonly System.Collections.Generic.Queue<float> _liveUsageHistory = new(10);  // 5 seconds at 0.5s interval = 10 data points
        private readonly System.Collections.Generic.Queue<float> _lapUsageHistory = new(5);    // Last 5 laps
        private float _lastCurrentFuel = 0f;  // Track fuel level changes for live sparkline
        private DispatcherTimer? _liveUpdateTimer;  // Timer for live fuel updates (0.5 second interval)

        // Historical min/max tracking for sparklines (persists across updates)
        private float _liveSparklineMinEver = float.MaxValue;
        private float _liveSparklineMaxEver = float.MinValue;
        private float _lapSparklineMinEver = float.MaxValue;
        private float _lapSparklineMaxEver = float.MinValue;

        // Reset tracking for sparkline scaling (prevents scale degradation over long sessions)
        private int _lastResetLap = 0;
        private DateTime _lastResetTime = DateTime.UtcNow;
        private const int SPARKLINE_RESET_LAP_INTERVAL = 10;  // Reset every 10 laps
        private const int SPARKLINE_RESET_TIME_MINUTES = 30;  // Reset every 30 minutes

        public override WidgetType WidgetType => WidgetType.Fuel;

        public FuelWidget(ITelemetryService telemetryService, FuelCalculatorService fuelCalculator, WidgetConfig? config = null)
            : base(telemetryService, config)
        {
            _fuelCalculator = fuelCalculator ?? throw new ArgumentNullException(nameof(fuelCalculator));

            InitializeComponent();

            // Apply fuel calculation method from settings
            ApplyFuelCalculationMethod();

            // Subscribe to fuel data updates
            _fuelCalculator.FuelDataUpdated += OnFuelDataUpdated;

            // Subscribe to settings changes
            AppSettings.Instance.SettingsChanged += OnSettingsChanged;

            // Subscribe to SizeChanged to update scale transform
            SizeChanged += OnWidgetSizeChanged;

            // Make click-through when locked
            IsHitTestVisible = false;

            // Apply saved position
            if (config != null)
            {
                Left = config.X;
                Top = config.Y;
            }
            else
            {
                // Use default position from AppSettings
                Left = AppSettings.Instance.FuelWidget_X;
                Top = AppSettings.Instance.FuelWidget_Y;
            }

            // Apply initial scale
            ApplyScale();

            // Apply initial visibility settings
            UpdateFieldVisibility();
            
            // Setup live fuel update timer (0.5 second interval for live sparkline - shows 5 seconds of data)
            _liveUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(LayoutConstants.LIVE_UPDATE_INTERVAL_MS)
            };
            _liveUpdateTimer.Tick += OnLiveUpdateTimerTick;
            _liveUpdateTimer.Start();
        }

        /// <summary>
        /// Handle widget resize by scaling the content
        /// </summary>
        private void OnWidgetSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyScale();
        }

        /// <summary>
        /// Apply scale transform based on AppSettings
        /// </summary>
        private void ApplyScale()
        {
            double scale = AppSettings.Instance.FuelWidget_Scale;
            var mainGrid = FindName("MainGrid") as FrameworkElement;
            if (mainGrid != null)
            {
                mainGrid.LayoutTransform = new ScaleTransform(scale, scale);
            }

            // Width is fixed, height auto-adjusts to fit all content
            Width = LayoutConstants.BASE_WIDTH * scale;
            MinHeight = LayoutConstants.MIN_HEIGHT * scale;
            MaxHeight = LayoutConstants.MAX_HEIGHT * scale;
            SizeToContent = SizeToContent.Height;
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateFieldVisibility();
                ApplyScale();
                ApplyFuelCalculationMethod();
            });
        }
        
        /// <summary>
        /// Apply fuel calculation method from AppSettings to FuelCalculatorService
        /// </summary>
        private void ApplyFuelCalculationMethod()
        {
            var methodName = AppSettings.Instance.FuelWidget_Method;
            _fuelCalculator.CurrentData.SelectedMethod = methodName switch
            {
                "Current" => FuelAveragingMethod.Current,
                "Last" => FuelAveragingMethod.Last,
                "Last5" => FuelAveragingMethod.Last5,
                "Last10" => FuelAveragingMethod.Last10,
                "Session" => FuelAveragingMethod.Session,
                "Max" => FuelAveragingMethod.Max,
                "EMA" => FuelAveragingMethod.EMA,
                "GreenOnly" => FuelAveragingMethod.GreenFlagOnly,
                "Stint" => FuelAveragingMethod.StintAverage,
                "Adaptive" => FuelAveragingMethod.Adaptive,
                _ => FuelAveragingMethod.Session  // Default to Session if unknown
            };
        }

        /// <summary>
        /// Update field visibility based on AppSettings (height auto-adjusts via SizeToContent)
        /// </summary>
        private void UpdateFieldVisibility()
        {
            var settings = AppSettings.Instance;

            // Fuel Bar visibility (Row 2 - can be toggled off for minimal display)
            var fuelBarGrid = FindName("FuelBarGrid") as FrameworkElement;
            var fuelBarRow = FindName("FuelBarRow") as RowDefinition;
            if (fuelBarGrid != null)
            {
                fuelBarGrid.Visibility = settings.FuelWidget_ShowBar ? Visibility.Visible : Visibility.Collapsed;
                if (fuelBarRow != null)
                    fuelBarRow.Height = settings.FuelWidget_ShowBar ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            }
            
            // Sparkline visibility - individual control for Live (5s) and Laps (5)
            
            // Live Sparkline (5 seconds) - Row 0 & 1
            var liveSparklineLabel = FindName("LiveSparklineLabel") as FrameworkElement;
            var liveSparklineChart = FindName("LiveSparklineChart") as FrameworkElement;
            var liveSparklineRow = FindName("LiveSparklineRow") as RowDefinition;
            var liveSparklineChartRow = FindName("LiveSparklineChartRow") as RowDefinition;
            bool showLiveSparkline = settings.FuelWidget_ShowLiveSparkline;
            
            if (liveSparklineLabel != null)
                liveSparklineLabel.Visibility = showLiveSparkline ? Visibility.Visible : Visibility.Collapsed;
            if (liveSparklineChart != null)
                liveSparklineChart.Visibility = showLiveSparkline ? Visibility.Visible : Visibility.Collapsed;
            if (liveSparklineRow != null)
                liveSparklineRow.Height = showLiveSparkline ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            if (liveSparklineChartRow != null)
                liveSparklineChartRow.Height = showLiveSparkline ? new GridLength(24) : new GridLength(0);
            
            // Lap Sparkline (5 laps) - Row 3 & 4
            var lapSparklineLabel = FindName("LapSparklineLabel") as FrameworkElement;
            var lapSparklineChart = FindName("LapSparklineChart") as FrameworkElement;
            var lapSparklineRow = FindName("LapSparklineRow") as RowDefinition;
            var lapSparklineChartRow = FindName("LapSparklineChartRow") as RowDefinition;
            bool showLapSparkline = settings.FuelWidget_ShowLapSparkline;
            
            if (lapSparklineLabel != null)
                lapSparklineLabel.Visibility = showLapSparkline ? Visibility.Visible : Visibility.Collapsed;
            if (lapSparklineChart != null)
                lapSparklineChart.Visibility = showLapSparkline ? Visibility.Visible : Visibility.Collapsed;
            if (lapSparklineRow != null)
                lapSparklineRow.Height = showLapSparkline ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            if (lapSparklineChartRow != null)
                lapSparklineChartRow.Height = showLapSparkline ? new GridLength(24) : new GridLength(0);
            
            // Hide entire sparkline grid if both sparklines are hidden
            var sparklineGrid = FindName("SparklineGrid") as FrameworkElement;
            if (sparklineGrid != null)
            {
                sparklineGrid.Visibility = (showLiveSparkline || showLapSparkline) ? Visibility.Visible : Visibility.Collapsed;
            }

            // Core consumption fields (always visible)
            // LastLapGrid, L5Grid, LapsRemainingGrid - these are core fields, always shown
            
            // MASTER TOGGLE: Pit Strategy Section (Rows 11-16)
            // Controls: LAPS, TO GO, PRESS, PIT, PIT IN
            // Single toggle - no individual field controls (legacy toggles removed)
            bool showStrategy = settings.FuelWidget_ShowPitStrategy;
            
            var lapsRemainingGrid = FindName("LapsRemainingGrid") as FrameworkElement;
            var lapsRemainingRow = FindName("LapsRemainingRow") as RowDefinition;
            if (lapsRemainingGrid != null)
            {
                lapsRemainingGrid.Visibility = showStrategy ? Visibility.Visible : Visibility.Collapsed;
                if (lapsRemainingRow != null)
                    lapsRemainingRow.Height = showStrategy ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            }
            
            // Optional consumption fields
            // RANGE display (replaces L10/SESSION)
            var rangeGrid = FindName("RangeGrid") as FrameworkElement;
            var rangeRow = FindName("L10Row") as RowDefinition;  // Reusing L10Row for RANGE
            if (rangeGrid != null && rangeRow != null)
            {
                rangeGrid.Visibility = settings.FuelWidget_ShowRange ? Visibility.Visible : Visibility.Collapsed;
                rangeRow.Height = settings.FuelWidget_ShowRange ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            }

            // New UI elements - controlled by individual toggles
            // Strategy Recommendation
            var strategyRecommendationGrid = FindName("StrategyRecommendationGrid") as FrameworkElement;
            var strategyRecommendationRow = FindName("StrategyRecommendationRow") as RowDefinition;
            if (strategyRecommendationGrid != null)
            {
                // Only show if pit strategy is enabled AND the toggle is on
                bool showStrategyRec = showStrategy && settings.FuelWidget_ShowStrategyRecommendation;
                strategyRecommendationGrid.Visibility = showStrategyRec ? Visibility.Visible : Visibility.Collapsed;
                if (strategyRecommendationRow != null)
                    strategyRecommendationRow.Height = showStrategyRec ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            }

            // Fuel Saving Badge
            var fuelSavingBadge = FindName("FuelSavingBadge") as Border;
            var fuelSavingBadgeRow = FindName("FuelSavingBadgeRow") as RowDefinition;
            if (fuelSavingBadge != null && fuelSavingBadgeRow != null)
            {
                // CRITICAL: Check setting here too to collapse row height when disabled
                // Visibility is also controlled in UpdateFuelSavingBadge based on NeedsFuelSaving
                bool showBadge = settings.FuelWidget_ShowSavingBadge;
                fuelSavingBadgeRow.Height = showBadge ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
                
                // Also set visibility here based on setting (UpdateFuelSavingBadge refines this further based on data)
                if (!showBadge)
                {
                    fuelSavingBadge.Visibility = Visibility.Collapsed;
                }
            }

            // Pit Strategy fields - controlled by MASTER TOGGLE ONLY
            // All pit strategy fields (TO GO, PRESS, PIT, PIT IN) shown/hidden together
            // Legacy individual toggles have been removed for simplicity

            // TO GO (Can Finish) - Row 12
            var canFinishGrid = FindName("CanFinishGrid") as FrameworkElement;
            var canFinishRow = FindName("CanFinishRow") as RowDefinition;
            if (canFinishGrid != null)
            {
                canFinishGrid.Visibility = showStrategy ? Visibility.Visible : Visibility.Collapsed;
                if (canFinishRow != null)
                    canFinishRow.Height = showStrategy ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            }

            // REMOVED: Fuel Pressure - not available in iRacing SDK
            // var pressureGrid = FindName("PressureGrid") as FrameworkElement;
            // var pressureRow = FindName("PressureRow") as RowDefinition;
            // Always hide pressure field
            var pressureGrid = FindName("PressureGrid") as FrameworkElement;
            var pressureRow = FindName("PressureRow") as RowDefinition;
            if (pressureGrid != null)
            {
                pressureGrid.Visibility = Visibility.Collapsed;
                if (pressureRow != null)
                    pressureRow.Height = new GridLength(0);
            }

            // PIT (Fuel to Add) - Row 14
            var pitFuelGrid = FindName("PitFuelGrid") as FrameworkElement;
            var pitFuelRow = FindName("PitFuelRow") as RowDefinition;
            if (pitFuelGrid != null)
            {
                pitFuelGrid.Visibility = showStrategy ? Visibility.Visible : Visibility.Collapsed;
                if (pitFuelRow != null)
                    pitFuelRow.Height = showStrategy ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            }

            // PIT IN (Pit Window) - Row 15
            var pitWindowGrid = FindName("PitWindowGrid") as FrameworkElement;
            var pitWindowRow = FindName("PitWindowRow") as RowDefinition;
            if (pitWindowGrid != null)
            {
                pitWindowGrid.Visibility = showStrategy ? Visibility.Visible : Visibility.Collapsed;
                if (pitWindowRow != null)
                    pitWindowRow.Height = showStrategy ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            }
        }

        /// <summary>
        /// Handle fuel data updates from FuelCalculatorService
        /// </summary>
        private void OnFuelDataUpdated(object? sender, FuelData fuelData)
        {
            Dispatcher.Invoke(() => UpdateUI(fuelData));
        }

        /// <summary>
        /// Update UI with new telemetry data (legacy method from WidgetBase)
        /// </summary>
        protected override void UpdateUI(TelemetryData data)
        {
            // Update handled by FuelCalculatorService via OnFuelDataUpdated
            // This method is called by base class but we don't need it
        }

        /// <summary>
        /// Update UI with fuel data
        /// </summary>
        private void UpdateUI(FuelData data)
        {
            var settings = AppSettings.Instance;

            // Configure dynamic buffer settings in FuelCalculatorService (called before each update)
            _fuelCalculator.ConfigureBufferSettings(settings.FuelWidget_BufferLaps, settings.FuelWidget_EnableDynamicBuffer);
            
            // Note: FuelBufferLaps is now calculated dynamically by FuelCalculatorService if enabled
            // data.FuelBufferLaps will be set by CalculateDynamicBufferLaps() based on race conditions

            // Current fuel level (using Run elements in XAML)
            var currentFuelRun = FindName("CurrentFuelRun") as Run;
            if (currentFuelRun != null)
            {
                currentFuelRun.Text = FormatFuel(data.CurrentFuel);
                currentFuelRun.Foreground = GetFuelLevelColor(data.FuelPct);
            }

            // Fuel percentage (only show if setting is enabled)
            var fuelPercentageRun = FindName("FuelPercentageRun") as Run;
            if (fuelPercentageRun != null)
            {
                if (settings.FuelWidget_ShowPercentage)
                {
                    fuelPercentageRun.Text = $"({(data.FuelPct * 100):F0}%)";
                }
                else
                {
                    fuelPercentageRun.Text = string.Empty; // Hide percentage
                }
            }

            // Tank capacity with sputtering threshold info
            var tankCapacityText = FindName("TankCapacityText") as TextBlock;
            if (tankCapacityText != null)
            {
                tankCapacityText.Text = FormatTankSize(data.TankCapacity);
                
                // Set tooltip with car-specific info + dynamic buffer details
                string carCategory = FuelSputteringDatabase.GetCarCategory(data.CarClassId);
                string tooltip = $"Sputtering threshold: {data.FuelSputteringThreshold:F2}L\n" +
                                 $"Car category: {carCategory}\n" +
                                 $"Buffer laps: {data.FuelBufferLaps:F1}\n" +
                                 $"Buffer reason: {data.BufferLapReason}";
                
                // Add fuel consistency info if available
                if (data.FuelConsistencyVariance > 0)
                {
                    tooltip += $"\nFuel variance: ±{data.FuelConsistencyVariance:F3}L";
                }
                
                // Add yellow flag probability if available
                if (data.YellowFlagProbability > 0)
                {
                    tooltip += $"\nYellow flag probability: {data.YellowFlagProbability * 100:F0}%";
                }
                
                tankCapacityText.ToolTip = tooltip;
            }

            // Smooth fuel bar (rectangle)
            var fuelBarFill = FindName("FuelBarFill") as FrameworkElement;
            if (fuelBarFill != null && fuelBarFill.Parent is Grid barGrid)
            {
                double maxWidth = barGrid.ActualWidth > 0 ? barGrid.ActualWidth : 140; // Fallback width
                fuelBarFill.Width = maxWidth * Math.Clamp(data.FuelPct, 0f, 1f);

                if (fuelBarFill is System.Windows.Shapes.Rectangle rect)
                {
                    rect.Fill = GetFuelLevelColor(data.FuelPct);
                }
            }

            // Last lap fuel usage (show -- if no data yet)
            var lastLapText = FindName("LastLapText") as TextBlock;
            var lastLapTrendArrow = FindName("LastLapTrendArrow") as TextBlock;
            if (lastLapText != null)
            {
                if (data.FuelUsedLastLap > 0)
                {
                    lastLapText.Text = $"{FormatFuelValue(data.FuelUsedLastLap)}";
                    lastLapText.Foreground = CachedBrushes.Orange;
                    
                    // Highlight if this is the active averaging method
                    lastLapText.FontWeight = (data.SelectedMethod == FuelAveragingMethod.Last) 
                        ? FontWeights.Bold 
                        : FontWeights.Normal;

                    // Show delta value (+/- amount) if enabled and we have lap-to-lap data
                    if (settings.FuelWidget_ShowTrends && lastLapTrendArrow != null && data.LapToLapDelta != 0)
                    {
                        // Format with + or - prefix and 2 decimals
                        string sign = data.LapToLapDelta > 0 ? "+" : "";
                        lastLapTrendArrow.Text = $"{sign}{data.LapToLapDelta.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";
                        lastLapTrendArrow.Visibility = Visibility.Visible;

                        // Color code: Red (using more fuel), Green (using less fuel), Gray (stable ±0.02)
                        lastLapTrendArrow.Foreground = data.LapToLapDelta switch
                        {
                            > 0.02f => CachedBrushes.Red,      // Red - using MORE fuel
                            < -0.02f => CachedBrushes.Green,   // Green - using LESS fuel
                            _ => CachedBrushes.GrayLight       // Gray - stable
                        };
                    }
                    else if (lastLapTrendArrow != null)
                    {
                        lastLapTrendArrow.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    lastLapText.Text = "--";
                    lastLapText.Foreground = CachedBrushes.Gray;
                    if (lastLapTrendArrow != null)
                    {
                        lastLapTrendArrow.Visibility = Visibility.Collapsed;
                    }
                }
            }

            // L5 average (show -- if no data yet)
            var l5AvgText = FindName("L5AvgText") as TextBlock;
            var l5TrendText = FindName("L5TrendText") as TextBlock;
            
            if (l5AvgText != null)
            {
                if (data.AvgFuelPerLap_L5 > 0)
                {
                    l5AvgText.Text = $"{FormatFuelValue(data.AvgFuelPerLap_L5)}";
                    l5AvgText.Foreground = CachedBrushes.Teal;
                    
                    // Highlight if this is the active averaging method
                    l5AvgText.FontWeight = (data.SelectedMethod == FuelAveragingMethod.Last5) 
                        ? FontWeights.Bold 
                        : FontWeights.Normal;
                    
                    // Show trend indicator if enabled and we have lap-to-lap delta data
                    if (l5TrendText != null && settings.FuelWidget_ShowTrendIndicator)
                    {
                        float trendDelta = data.LapToLapDelta;
                        
                        // Only show trend if we have meaningful data (not first few laps)
                        if (data.CurrentLap >= 5 && Math.Abs(trendDelta) >= 0.01f)
                        {
                            // Format: +0.15 or -0.10
                            string sign = trendDelta >= 0 ? "+" : "";
                            l5TrendText.Text = $"{sign}{trendDelta:F2}";
                            
                            // Color: Negative (using less) = Teal (good), Positive (using more) = Orange (bad)
                            l5TrendText.Foreground = trendDelta < 0 
                                ? CachedBrushes.Teal    // Improving efficiency (using less fuel)
                                : CachedBrushes.Orange; // Worsening efficiency (using more fuel)
                            
                            l5TrendText.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            l5TrendText.Visibility = Visibility.Collapsed;
                        }
                    }
                }
                else
                {
                    l5AvgText.Text = "--";
                    l5AvgText.Foreground = CachedBrushes.Gray;
                    
                    if (l5TrendText != null)
                    {
                        l5TrendText.Visibility = Visibility.Collapsed;
                    }
                }
            }

            // Fuel Range (Min-Max) - replaces L10/SESSION
            var rangeText = FindName("RangeText") as TextBlock;
            if (rangeText != null)
            {
                if (data.MinFuelPerLap > 0 && data.MaxFuelPerLap > 0)
                {
                    rangeText.Text = $"{data.MinFuelPerLap:F2}-{data.MaxFuelPerLap:F2}";
                    rangeText.Foreground = CachedBrushes.Teal; // Primary teal color
                }
                else
                {
                    rangeText.Text = "--";
                    rangeText.Foreground = CachedBrushes.Gray;
                }
            }

            // Laps remaining (1 decimal for precision without clutter, show -- if no data yet)
            var lapsRemainingText = FindName("LapsRemainingText") as TextBlock;
            if (lapsRemainingText != null)
            {
                if (data.LapsRemaining > 0 && data.AvgFuelPerLap_L5 > 0)
                {
                    lapsRemainingText.Text = data.LapsRemaining.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    lapsRemainingText.Foreground = GetLapsRemainingColor(data.LapsRemaining);
                }
                else
                {
                    lapsRemainingText.Text = "--";
                    lapsRemainingText.Foreground = CachedBrushes.Gray;
                }
            }

            // Fuel to add at next pit stop - shows amount to FINISH RACE (matches iRacing)
            var canFinishText = FindName("CanFinishText") as TextBlock;
            if (canFinishText != null)
            {
                // Show if we have lap data, regardless of session type
                if (data.AvgFuelPerLap_L5 > 0)
                {
                    float fuelToAdd;

                    if (data.RaceLapsRemaining > 0)
                    {
                        // RACE mode: Calculate fuel needed to FINISH race from current position
                        // This matches iRacing's "Add: X.XL" display
                        float fuelNeededToFinish = data.FuelNeededToFinish;  // Total fuel needed to finish
                        fuelToAdd = Math.Max(0, fuelNeededToFinish - data.CurrentFuel);  // How much MORE we need
                        
                        // Round up to nearest 0.5L for safety (iRacing does this too)
                        fuelToAdd = (float)Math.Ceiling(fuelToAdd * 2) / 2;
                    }
                    else
                    {
                        // QUALIFYING/PRACTICE mode: Show total fuel for 5-lap reference
                        fuelToAdd = data.AvgFuelPerLap_L5 * 5;
                    }

                    canFinishText.Text = $"{FormatFuelValue(fuelToAdd)}";

                    // Color: teal if reasonable amount, orange if very high (more than double current fuel)
                    canFinishText.Foreground = fuelToAdd > (data.CurrentFuel * 2) ? CachedBrushes.Orange : CachedBrushes.Teal;
                }
                else
                {
                    canFinishText.Text = "--";
                    canFinishText.Foreground = CachedBrushes.Gray;
                }
            }

            // Pit fuel amount (optional) - show in all session types
            // FIXED: Don't show label if near finish, don't use RefuelCount for label (it's historical pit stops)
            var pitFuelLabel = FindName("PitFuelLabel") as TextBlock;
            var pitFuelText = FindName("PitFuelText") as TextBlock;
            if (pitFuelText != null)
            {
                // Simple label - "PIT" only (RefuelCount is historical pit stops, not useful for label)
                if (pitFuelLabel != null)
                {
                    pitFuelLabel.Text = "PIT";
                }

                // Show if we have lap data (any session type)
                if (data.AvgFuelPerLap_L5 > 0)
                {
                    float fuelNeeded;
                    bool nearFinish = false;

                    if (data.RaceLapsRemaining > 0)
                    {
                        // RACE mode: Use backend calculation (already accounts for laps remaining + buffer + 0.3L finish threshold)
                        // FuelToAddAtPit is calculated from FuelDeltaToFinish and includes all safety margins
                        fuelNeeded = data.FuelToAddAtPit;

                        // Don't show PIT if very close to finish (<0.3 laps remaining)
                        nearFinish = data.LapsRemaining < 0.3f;
                    }
                    else
                    {
                        // QUALIFYING/PRACTICE mode: Fuel needed for 5 laps
                        fuelNeeded = Math.Max(0, (data.AvgFuelPerLap_L5 * 5) - data.CurrentFuel);
                    }

                    if (nearFinish)
                    {
                        // Very close to finish - hide PIT field (will finish on current fuel or already too late)
                        pitFuelText.Text = "--";
                        pitFuelText.Foreground = CachedBrushes.Gray;
                    }
                    else if (fuelNeeded > 0)
                    {
                        // Show fuel to add with simplified color coding (removed yellow, kept orange/red)
                        pitFuelText.Text = $"{FormatFuelValue(fuelNeeded)}";

                        // Simplified color coding: Red (<2 laps), Orange (≥2 laps)
                        pitFuelText.Foreground = data.LapsRemaining < LayoutConstants.URGENT_LAPS_THRESHOLD ? CachedBrushes.Red : CachedBrushes.Orange;
                    }
                    else
                    {
                        // Can finish without stop - show "OK" in green
                        pitFuelText.Text = "OK";
                        pitFuelText.Foreground = CachedBrushes.Green;
                    }
                }
                else
                {
                    pitFuelText.Text = "--";  // No data yet
                    pitFuelText.Foreground = CachedBrushes.Gray;
                }
            }

            // Pit window countdown - shows intelligent pit window with earliest/optimal/latest laps
            var pitWindowText = FindName("PitWindowText") as TextBlock;
            if (pitWindowText != null)
            {
                // VALIDATION: Reject inverted pit windows (PitWindowStart > PitWindowEnd = invalid data)
                bool hasValidPitWindow = data.OptimalPitLap > 0 &&
                                        data.PitWindowStart > 0 &&
                                        data.PitWindowEnd > 0 &&
                                        data.PitWindowStart <= data.PitWindowEnd; // CRITICAL: Reject inverted windows

                // Enhanced pit window display with multi-lap ranges
                if (hasValidPitWindow)
                {
                    // Show pit window range
                    int currentLap = data.CurrentLap;
                    int lapsUntilWindow = Math.Max(0, data.PitWindowStart - currentLap);
                    int lapsUntilOptimal = Math.Max(0, data.OptimalPitLap - currentLap);

                    // Always show the window range with optimal lap highlighted
                    // Format: "L8-12 (opt L10)" or "L8-12 (NOW at L9)"
                    
                    // Color coding based on urgency
                    if (currentLap >= data.PitWindowStart && currentLap <= data.PitWindowEnd)
                    {
                        // IN OPTIMAL WINDOW - Green - Show where we are in window
                        pitWindowText.Text = $"L{data.PitWindowStart}-{data.PitWindowEnd} (NOW L{currentLap})";
                        pitWindowText.Foreground = CachedBrushes.GreenDark;
                    }
                    else if (currentLap < data.PitWindowStart)
                    {
                        // BEFORE WINDOW - Teal - Show optimal lap in window
                        pitWindowText.Text = $"L{data.PitWindowStart}-{data.PitWindowEnd} (opt L{data.OptimalPitLap})";
                        pitWindowText.Foreground = CachedBrushes.Teal;
                    }
                    else if (currentLap > data.PitWindowEnd && currentLap < data.LatestPitLap)
                    {
                        // AFTER OPTIMAL BUT BEFORE LATEST - Orange (getting urgent)
                        int lapsUntilLatest = data.LatestPitLap - currentLap;
                        pitWindowText.Text = $"Late L{data.LatestPitLap} ({lapsUntilLatest}L left)";
                        pitWindowText.Foreground = CachedBrushes.Orange;
                    }
                    else if (currentLap >= data.LatestPitLap)
                    {
                        // PAST LATEST - Red (critical)
                        pitWindowText.Text = $"CRITICAL L{data.LatestPitLap}";
                        pitWindowText.Foreground = CachedBrushes.Red;
                    }
                    else
                    {
                        // Fallback: Show window with optimal lap
                        pitWindowText.Text = $"L{data.PitWindowStart}-{data.PitWindowEnd} (opt L{data.OptimalPitLap})";
                        pitWindowText.Foreground = CachedBrushes.Cyan;
                    }
                }
                // Fallback: Show "---" if pit window data not available or invalid
                else
                {
                    // No valid pit window data (practice/qualifying, early in race, or no fuel data)
                    pitWindowText.Text = "---";
                    pitWindowText.Foreground = CachedBrushes.Gray;
                }
            }
            
            // Pit exit position prediction (inline with PIT WINDOW)
            var pitExitPositionText = FindName("PitExitPositionText") as TextBlock;
            if (pitExitPositionText != null)
            {
                // Check if feature is enabled and we have valid pit exit data
                bool showPitExit = settings.FuelWidget_ShowPitExitPosition && 
                                  data.PitExitPositionValid && 
                                  !string.IsNullOrEmpty(data.PitExitGapDescription);
                
                if (showPitExit)
                {
                    // Format abbreviated: "↳ P12 | +4s #14 | -10s #9" or "↳ P12 🟢 | +4s #14 | -10s #9"
                    string confidenceIcon = !string.IsNullOrEmpty(data.PitExitConfidenceIcon) 
                        ? $" {data.PitExitConfidenceIcon}" 
                        : "";
                    pitExitPositionText.Text = $"↳ P{data.PitExitPosition}{confidenceIcon} | {data.PitExitGapDescription}";
                    pitExitPositionText.Visibility = Visibility.Visible;
                }
                else
                {
                    pitExitPositionText.Visibility = Visibility.Collapsed;
                }
            }
            
            // Pit status indicator (Option B - separate line showing pitting cars)
            var pittingCarsText = FindName("PittingCarsText") as TextBlock;
            if (pittingCarsText != null)
            {
                // Show if we have cars pitting in player's class
                if (data.CarsPitting.Count > 0)
                {
                    // Format: "Pitting: #14, #23, #8" (max 5 cars to avoid clutter)
                    var displayCars = data.CarsPitting.Take(5).Select(car => $"#{car}");
                    string moreIndicator = data.CarsPitting.Count > 5 ? $" +{data.CarsPitting.Count - 5}" : "";
                    pittingCarsText.Text = $"Pitting: {string.Join(", ", displayCars)}{moreIndicator}";
                    pittingCarsText.Visibility = Visibility.Visible;
                }
                else
                {
                    pittingCarsText.Visibility = Visibility.Collapsed;
                }
            }

            // Fuel pressure (optional) - Show bar value with color coding only
            var pressureText = FindName("PressureText") as TextBlock;
            if (pressureText != null)
            {
                // Show pressure value only (no percentage - it's broken and unnecessary for visual display)
                pressureText.Text = $"{data.FuelPressure:F2} bar";

                // Color coding based on pressure drop
                // Critical: >20% drop (red, imminent sputtering)
                // Warning: 10-20% drop (orange, low fuel risk)
                // Normal: <10% drop (green, safe)
                pressureText.Foreground = data.FuelPressureDropPct switch
                {
                    > 20f => CachedBrushes.Red,      // Red - Critical
                    > 10f => CachedBrushes.Orange,   // Orange - Warning
                    _ => CachedBrushes.Green         // Green - Normal
                };
            }

            // Update sparklines
            UpdateSparklines(data);

            // Handle blinking for critical fuel
            ManageBlinking(data.LapsRemaining);

            // Update new UI elements
            UpdateFuelSavingBadge(data);
            UpdateStrategyRecommendation(data);

            // Update fuel saving display
            UpdateFuelSavingDisplay(data);

            // Update live lap delta display
            UpdateLapDeltaDisplay(data);

            // Update historical confidence display
            UpdateHistoricalConfidenceDisplay(data);
        }

        /// <summary>
        /// Update fuel saving display elements
        /// </summary>
        private void UpdateFuelSavingDisplay(FuelData data)
        {
            var settings = AppSettings.Instance;

            // Check if pit strategy section is enabled
            // FIX: Show fuel saving when pit strategy is enabled (they're part of the same section)
            // User wants it to stay visible when "Show pit strategy section" is toggled on
            bool showFuelSaving = settings.FuelWidget_ShowPitStrategy;

            // Find all fuel saving UI elements
            var fuelSavingHeader = FindName("FuelSavingHeader") as TextBlock;
            var fuelSavingTargetGrid = FindName("FuelSavingTargetGrid") as Grid;
            var fuelSavingLiftGrid = FindName("FuelSavingLiftGrid") as Grid;
            var strategicAlertBorder = FindName("StrategicAlertBorder") as Border;
            var optimalPitLapGrid = FindName("OptimalPitLapGrid") as Grid;

            // Find row definitions for showing/hiding
            var fuelSavingSeparatorRow = FindName("FuelSavingSeparatorRow") as RowDefinition;
            var fuelSavingHeaderRow = FindName("FuelSavingHeaderRow") as RowDefinition;
            var fuelSavingTargetRow = FindName("FuelSavingTargetRow") as RowDefinition;
            var fuelSavingLiftRow = FindName("FuelSavingLiftRow") as RowDefinition;
            var fuelSavingAlertRow = FindName("FuelSavingAlertRow") as RowDefinition;
            var optimalPitLapRow = FindName("OptimalPitLapRow") as RowDefinition;

            if (!showFuelSaving)
            {
                // Hide all fuel saving UI elements
                if (fuelSavingHeader != null) fuelSavingHeader.Visibility = Visibility.Collapsed;
                if (fuelSavingTargetGrid != null) fuelSavingTargetGrid.Visibility = Visibility.Collapsed;
                if (fuelSavingLiftGrid != null) fuelSavingLiftGrid.Visibility = Visibility.Collapsed;
                if (strategicAlertBorder != null) strategicAlertBorder.Visibility = Visibility.Collapsed;
                if (optimalPitLapGrid != null) optimalPitLapGrid.Visibility = Visibility.Collapsed;

                // Collapse rows to save space
                if (fuelSavingSeparatorRow != null) fuelSavingSeparatorRow.Height = new GridLength(0);
                if (fuelSavingHeaderRow != null) fuelSavingHeaderRow.Height = new GridLength(0);
                if (fuelSavingTargetRow != null) fuelSavingTargetRow.Height = new GridLength(0);
                if (fuelSavingLiftRow != null) fuelSavingLiftRow.Height = new GridLength(0);
                if (fuelSavingAlertRow != null) fuelSavingAlertRow.Height = new GridLength(0);
                if (optimalPitLapRow != null) optimalPitLapRow.Height = new GridLength(0);

                return;
            }

            // Show fuel saving section
            if (fuelSavingHeader != null) fuelSavingHeader.Visibility = Visibility.Visible;
            if (fuelSavingTargetGrid != null) fuelSavingTargetGrid.Visibility = Visibility.Visible;

            // Restore row heights
            if (fuelSavingSeparatorRow != null) fuelSavingSeparatorRow.Height = new GridLength(8);
            if (fuelSavingHeaderRow != null) fuelSavingHeaderRow.Height = GridLength.Auto;
            if (fuelSavingTargetRow != null) fuelSavingTargetRow.Height = GridLength.Auto;

            // Update fuel saving target
            var fuelSavingTargetText = FindName("FuelSavingTargetText") as TextBlock;
            if (fuelSavingTargetText != null)
            {
                fuelSavingTargetText.Text = $"{data.FuelSavingTarget:F2}L/lap";
            }

            // Update target lap time
            var targetLapTimeText = FindName("TargetLapTimeText") as TextBlock;
            if (targetLapTimeText != null && data.TargetLapTime > 0)
            {
                int minutes = (int)(data.TargetLapTime / 60);
                float seconds = data.TargetLapTime % 60;
                targetLapTimeText.Text = $"{minutes}:{seconds:00.0}";
                
                // Add tooltip explaining what target lap time means
                float timeDelta = data.TargetLapTime - data.AverageLapTime;
                if (timeDelta > 0)
                {
                    targetLapTimeText.ToolTip = $"Target pace for fuel saving\n" +
                                               $"(+{timeDelta:F1}s slower than avg lap time)\n" +
                                               $"Lift/coast to achieve this pace";
                }
                else
                {
                    targetLapTimeText.ToolTip = "Target pace for fuel saving";
                }
            }

            // Update progress bar
            var savingProgressBar = FindName("SavingProgressBar") as FrameworkElement;
            if (savingProgressBar != null && savingProgressBar.Parent is Grid progressGrid)
            {
                double maxWidth = progressGrid.ActualWidth > 0 ? progressGrid.ActualWidth : 200;
                double progressPercent = Math.Clamp(data.SavingProgress / 100f, 0f, 1f);
                savingProgressBar.Width = maxWidth * progressPercent;

                // FIXED: Color based on target achievability first, then progress
                // If target is impossible (>15% reduction), show warning colors regardless of progress
                // If target is achievable, use progress-based colors
                if (savingProgressBar is System.Windows.Shapes.Rectangle rect)
                {
                    if (!data.CanSaveFuelToFinish)
                    {
                        // Target is impossible (>15% reduction needed) - always show warning
                        rect.Fill = CachedBrushes.OrangeDark;
                    }
                    else
                    {
                        // Target is achievable - color by progress: Green (≥70%), Yellow (30-70%), Red (<30%)
                        rect.Fill = data.SavingProgress switch
                        {
                            >= 70f => CachedBrushes.GreenDark,
                            >= 30f => CachedBrushes.YellowLight,
                            _ => CachedBrushes.RedLight
                        };
                    }
                }
            }

            // Update current saving rate
            var currentSavingRateText = FindName("CurrentSavingRateText") as TextBlock;
            if (currentSavingRateText != null)
            {
                currentSavingRateText.Text = $"{data.CurrentSavingRate:F2}L/lap";
                currentSavingRateText.Foreground = data.SavingProgress >= 50f ? CachedBrushes.GreenDark : CachedBrushes.Orange;
            }

            // Update saving progress percentage
            var savingProgressText = FindName("SavingProgressText") as TextBlock;
            if (savingProgressText != null)
            {
                savingProgressText.Text = $"({data.SavingProgress:F0}%)";
            }

            // Show lift points if enabled and available
            if (settings.FuelWidget_ShowLiftPoints && !string.IsNullOrEmpty(data.LiftPoints))
            {
                if (fuelSavingLiftGrid != null) fuelSavingLiftGrid.Visibility = Visibility.Visible;
                if (fuelSavingLiftRow != null) fuelSavingLiftRow.Height = GridLength.Auto;

                var liftPointsText = FindName("LiftPointsText") as TextBlock;
                if (liftPointsText != null)
                {
                    liftPointsText.Text = data.LiftPoints;
                }
            }
            else
            {
                if (fuelSavingLiftGrid != null) fuelSavingLiftGrid.Visibility = Visibility.Collapsed;
                if (fuelSavingLiftRow != null) fuelSavingLiftRow.Height = new GridLength(0);
            }

            // Show strategic alerts if enabled and available
            if (settings.FuelWidget_ShowSavingAlerts && !string.IsNullOrEmpty(data.StrategicAlert))
            {
                if (strategicAlertBorder != null) strategicAlertBorder.Visibility = Visibility.Visible;
                if (fuelSavingAlertRow != null) fuelSavingAlertRow.Height = GridLength.Auto;

                var strategicAlertText = FindName("StrategicAlertText") as TextBlock;
                if (strategicAlertText != null)
                {
                    strategicAlertText.Text = data.StrategicAlert;
                }

                // Color code alert based on severity
                if (strategicAlertBorder != null)
                {
                    strategicAlertBorder.Background = data.AlertSeverity switch
                    {
                        3 => CachedBrushes.RedDark,          // Critical - Dark Red
                        2 => CachedBrushes.OrangeDark,       // Warning - Orange
                        1 => CachedBrushes.GreenVeryDark,    // Info - Dark Green
                        _ => CachedBrushes.GrayDark          // None - Gray
                    };
                }
            }
            else
            {
                if (strategicAlertBorder != null) strategicAlertBorder.Visibility = Visibility.Collapsed;
                if (fuelSavingAlertRow != null) fuelSavingAlertRow.Height = new GridLength(0);
            }

            // Show optimal pit lap if enabled and available
            if (settings.FuelWidget_OptimalPitCalculator && data.OptimalPitLap > 0)
            {
                if (optimalPitLapGrid != null) optimalPitLapGrid.Visibility = Visibility.Visible;
                if (optimalPitLapRow != null) optimalPitLapRow.Height = GridLength.Auto;

                var optimalPitLapText = FindName("OptimalPitLapText") as TextBlock;
                if (optimalPitLapText != null)
                {
                    // REACHABILITY CHECK: Validate optimal pit lap is actually achievable with current fuel
                    // Use 0.5 lap grace buffer (hysteresis) to prevent sudden changes at half-lap boundaries
                    int currentLap = data.CurrentLap;
                    float lapsReachable = currentLap + data.LapsRemaining - 0.5f; // Grace buffer for smooth transitions
                    bool isPitLapReachable = data.OptimalPitLap > 0 && 
                                            data.OptimalPitLap > currentLap && 
                                            data.OptimalPitLap <= lapsReachable;
                    
                    if (isPitLapReachable)
                    {
                        // Enhanced display with context
                        string pitText = $"Lap {data.OptimalPitLap}";
                        
                        // Add fuel criticality indicator if critical (<2 laps)
                        if (data.FuelCriticalityScore > 80f)
                        {
                            pitText += " 🔴"; // Red circle for critical fuel
                        }
                        else if (data.FuelCriticalityScore > 50f)
                        {
                            pitText += " 🟡"; // Yellow circle for moderate urgency
                        }
                        
                        // Add position indicator for strategy context
                        if (data.RacePosition > 0 && data.TotalCars > 1)
                        {
                            float percentile = (float)data.RacePosition / data.TotalCars;
                            if (percentile <= 0.25f)
                            {
                                pitText += $" (P{data.RacePosition} 🏆)"; // Top 25% - trophy
                            }
                            else if (percentile >= 0.75f)
                            {
                                pitText += $" (P{data.RacePosition} ⚡)"; // Bottom 25% - undercut opportunity
                            }
                            else
                            {
                                pitText += $" (P{data.RacePosition})"; // Mid-pack
                            }
                        }
                        
                        optimalPitLapText.Text = pitText;
                    }
                    else
                    {
                        // Optimal pit lap is unreachable (in past or beyond fuel range)
                        optimalPitLapText.Text = "--";
                    }
                    
                    // Color based on urgency
                    if (data.FuelCriticalityScore > 95f)
                    {
                        optimalPitLapText.Foreground = CachedBrushes.Red; // Red - Critical
                    }
                    else if (data.FuelCriticalityScore > 80f)
                    {
                        optimalPitLapText.Foreground = CachedBrushes.Orange; // Orange - Urgent
                    }
                    else
                    {
                        optimalPitLapText.Foreground = CachedBrushes.Cyan; // Cyan - Strategic
                    }
                }

                var optimalPitReasonText = FindName("OptimalPitReasonText") as TextBlock;
                if (optimalPitReasonText != null && !string.IsNullOrEmpty(data.OptimalPitReason))
                {
                    // Truncate long reasons for display
                    string reason = data.OptimalPitReason;
                    if (reason.Length > 35)
                    {
                        reason = reason.Substring(0, 32) + "...";
                    }
                    optimalPitReasonText.Text = $"({reason})";
                }
            }
            else
            {
                if (optimalPitLapGrid != null) optimalPitLapGrid.Visibility = Visibility.Collapsed;
                if (optimalPitLapRow != null) optimalPitLapRow.Height = new GridLength(0);
            }
        }

        /// <summary>
        /// Update live lap delta display
        /// Shows real-time delta vs target pace for fuel saving
        /// </summary>
        private void UpdateLapDeltaDisplay(FuelData data)
        {
            var settings = AppSettings.Instance;
            
            // Only show when pit strategy (including fuel saving) is enabled and delta is valid
            bool showLapDelta = settings.FuelWidget_ShowPitStrategy && 
                               data.LiveDeltaValid && 
                               data.NeedsFuelSaving;
            
            var lapDeltaGrid = FindName("LapDeltaGrid") as Grid;
            var lapDeltaText = FindName("LapDeltaText") as TextBlock;
            
            if (!showLapDelta)
            {
                if (lapDeltaGrid != null) lapDeltaGrid.Visibility = Visibility.Collapsed;
                return;
            }
            
            if (lapDeltaGrid != null) lapDeltaGrid.Visibility = Visibility.Visible;
            
            if (lapDeltaText != null)
            {
                // Format delta: +0.5s (faster), -0.3s (slower)
                string sign = data.LiveDeltaToTarget >= 0 ? "+" : "";
                lapDeltaText.Text = $"{sign}{data.LiveDeltaToTarget:F1}s";
                
                // Color code: Green (faster/on pace), Orange (slightly slow), Red (too slow)
                if (data.LiveDeltaToTarget >= -0.1f)
                {
                    // On pace or faster
                    lapDeltaText.Foreground = CachedBrushes.GreenDark;
                }
                else if (data.LiveDeltaToTarget >= -0.5f)
                {
                    // Slightly slower but manageable
                    lapDeltaText.Foreground = CachedBrushes.YellowLight;
                }
                else
                {
                    // Too slow - won't meet fuel saving target
                    lapDeltaText.Foreground = CachedBrushes.RedLight;
                }
                
                // Update tooltip with prediction
                if (data.PredictedLapTime > 0)
                {
                    int predMin = (int)(data.PredictedLapTime / 60);
                    float predSec = data.PredictedLapTime % 60;
                    string predDeltaSign = data.PredictedDelta >= 0 ? "+" : "";
                    lapDeltaText.ToolTip = $"Live: {sign}{data.LiveDeltaToTarget:F1}s\n" +
                                          $"Predicted lap: {predMin}:{predSec:00.1} ({predDeltaSign}{data.PredictedDelta:F1}s)";
                }
            }
        }

        /// <summary>
        /// Update historical confidence display
        /// Shows when predictions are based on historical data
        /// </summary>
        private void UpdateHistoricalConfidenceDisplay(FuelData data)
        {
            var historicalText = FindName("HistoricalConfidenceText") as TextBlock;
            
            if (historicalText == null)
                return;
            
            // Only show when using historical predictions with meaningful confidence
            bool showHistorical = data.UsingHistoricalPredictions && 
                                 data.HistoricalConfidence > 0 && 
                                 data.HistoricalSessionCount > 0;
            
            if (!showHistorical)
            {
                historicalText.Visibility = Visibility.Collapsed;
                return;
            }
            
            historicalText.Visibility = Visibility.Visible;
            
            // Format: "📊 Historical: 85% (12 sessions)"
            historicalText.Text = $"📊 Historical: {data.HistoricalConfidence:F0}% ({data.HistoricalSessionCount} sessions)";
            
            // Color code by confidence level
            if (data.HistoricalConfidence >= 80f)
            {
                // High confidence - green
                historicalText.Foreground = CachedBrushes.GreenDark;
            }
            else if (data.HistoricalConfidence >= 50f)
            {
                // Medium confidence - teal
                historicalText.Foreground = CachedBrushes.Teal;
            }
            else
            {
                // Low confidence - gray
                historicalText.Foreground = CachedBrushes.Gray;
            }
        }

        /// <summary>
        /// Update fuel saving mode badge
        /// </summary>
        private void UpdateFuelSavingBadge(FuelData data)
        {
            var settings = AppSettings.Instance;
            var fuelSavingBadge = FindName("FuelSavingBadge") as Border;
            var fuelSavingDeltaText = FindName("FuelSavingDeltaText") as TextBlock;

            if (fuelSavingBadge == null || fuelSavingDeltaText == null)
                return;

            // Check setting FIRST - if disabled, hide immediately
            if (!settings.FuelWidget_ShowSavingBadge)
            {
                fuelSavingBadge.Visibility = Visibility.Collapsed;
                return;
            }

            // Only show if fuel saving is actually needed
            if (!data.NeedsFuelSaving || data.FuelSavingTarget <= 0)
            {
                fuelSavingBadge.Visibility = Visibility.Collapsed;
                return;
            }

            // Setting is enabled AND fuel saving is needed - show it
            fuelSavingBadge.Visibility = Visibility.Visible;
            fuelSavingDeltaText.Text = $"-{data.FuelSavingTarget:F2}L/lap needed";

            // Color based on achievability
            fuelSavingBadge.Background = data.CanSaveFuelToFinish
                ? CachedBrushes.GreenDark
                : CachedBrushes.RedDark;
        }

        /// <summary>
        /// Update strategy recommendation display
        /// </summary>
        private void UpdateStrategyRecommendation(FuelData data)
        {
            var settings = AppSettings.Instance;
            var strategyRecommendationText = FindName("StrategyRecommendationText") as TextBlock;

            if (strategyRecommendationText == null)
                return;

            // Only show if setting is enabled and in race mode
            if (!settings.FuelWidget_ShowStrategyRecommendation || data.RaceLapsRemaining <= 0)
            {
                // Visibility already handled by UpdateFieldVisibility
                return;
            }

            // Determine active strategy with dynamic display
            string strategyText = "";
            Brush strategyColor = CachedBrushes.Teal; // Primary teal as default

            int currentLap = data.CurrentLap;
            int lapsRemaining = data.RaceLapsRemaining;
            
            if (data.CanFinishWithoutStop)
            {
                // NO-STOP - Show confidence with laps to spare
                float lapsToSpare = data.LapsRemaining - lapsRemaining;
                if (lapsToSpare > 2)
                {
                    strategyText = $"✓ NO-STOP (+{lapsToSpare:F1}L)";
                    strategyColor = CachedBrushes.Green;
                }
                else if (lapsToSpare > 0.5f)
                {
                    strategyText = "✓ NO-STOP (tight)";
                    strategyColor = CachedBrushes.Teal;
                }
                else
                {
                    strategyText = "⚠ NO-STOP (margin!)";
                    strategyColor = CachedBrushes.Orange;
                }
            }
            else if (data.OptimalPitLap > 0)
            {
                // 1-STOP strategy - show lap countdown and context
                int lapsUntilPit = data.OptimalPitLap - currentLap;
                
                if (currentLap >= data.PitWindowStart && currentLap <= data.PitWindowEnd)
                {
                    // In pit window - urgent
                    strategyText = $"⭐ PIT NOW (L{data.PitWindowStart}-{data.PitWindowEnd})";
                    strategyColor = CachedBrushes.Green;
                }
                else if (lapsUntilPit > 0 && lapsUntilPit <= 3)
                {
                    // Approaching pit window
                    strategyText = $"⭐ 1-STOP in {lapsUntilPit}L (L{data.OptimalPitLap})";
                    strategyColor = CachedBrushes.Teal;
                }
                else if (lapsUntilPit > 0)
                {
                    // Future pit stop
                    strategyText = $"⭐ 1-STOP @ L{data.OptimalPitLap}";
                    strategyColor = CachedBrushes.Teal;
                }
                else if (currentLap > data.OptimalPitLap)
                {
                    // Missed optimal - show urgency
                    int lapsOverdue = currentLap - data.OptimalPitLap;
                    strategyText = $"⚠ PIT OVERDUE +{lapsOverdue}L";
                    strategyColor = CachedBrushes.Orange;
                }
            }
            else
            {
                // No strategy available - early race
                if (currentLap < 3)
                {
                    strategyText = "⏳ Calculating...";
                    strategyColor = CachedBrushes.Gray;
                }
                else
                {
                    strategyText = "⚠ STRATEGY TBD";
                    strategyColor = CachedBrushes.Orange;
                }
            }

            strategyRecommendationText.Text = strategyText;
            strategyRecommendationText.Foreground = strategyColor;
        }

        /// <summary>
        /// Live update timer tick - updates live fuel consumption sparkline every 0.5s
        /// Always updates to show activity, even when stationary
        /// </summary>
        private void OnLiveUpdateTimerTick(object? sender, EventArgs e)
        {
            // Get current fuel from calculator service
            var fuelData = _fuelCalculator.CurrentData;
            if (fuelData == null)
                return;

            // Check if sparkline min/max should be reset (every 10 laps or 30 minutes)
            bool shouldReset = false;
            if (fuelData.CurrentLap > 0 && fuelData.CurrentLap - _lastResetLap >= SPARKLINE_RESET_LAP_INTERVAL)
            {
                shouldReset = true;
                _lastResetLap = fuelData.CurrentLap;
            }
            else if ((DateTime.UtcNow - _lastResetTime).TotalMinutes >= SPARKLINE_RESET_TIME_MINUTES)
            {
                shouldReset = true;
                _lastResetTime = DateTime.UtcNow;
            }

            if (shouldReset)
            {
                _liveSparklineMinEver = float.MaxValue;
                _liveSparklineMaxEver = float.MinValue;
                _lapSparklineMinEver = float.MaxValue;
                _lapSparklineMaxEver = float.MinValue;
            }

            // Calculate fuel consumed since last update (absolute difference)
            float fuelDelta = 0f;
            
            if (_lastCurrentFuel > 0)
            {
                fuelDelta = Math.Abs(fuelData.CurrentFuel - _lastCurrentFuel);
                
                // Ignore massive jumps (refueling >5L)
                if (fuelDelta > 5.0f)
                    fuelDelta = 0f;
            }
            
            // ALWAYS add a data point to keep sparkline updating (shows 0 when stationary)
            _liveUsageHistory.Enqueue(fuelDelta);
            if (_liveUsageHistory.Count > 10) // Keep 10 data points = 5 seconds at 0.5s interval
                _liveUsageHistory.Dequeue();
            
            _lastCurrentFuel = fuelData.CurrentFuel;
            
            // Update live sparkline rendering
            var liveSparkline = FindName("LiveSparkline") as System.Windows.Shapes.Polyline;
            var liveCanvas = FindName("LiveSparklineCanvas") as System.Windows.Controls.Canvas;
            if (liveSparkline != null && liveCanvas != null && _liveUsageHistory.Count > 1)
            {
                UpdateSparklineGuideLines("Live", liveCanvas);
                RenderSparkline(liveSparkline, liveCanvas, _liveUsageHistory, amplificationFactor: 50f);
                UpdateSparklineMinMax("LiveMinText", "LiveMaxText", _liveUsageHistory, ref _liveSparklineMinEver, ref _liveSparklineMaxEver);
            }
        }

        /// <summary>
        /// Update sparkline visualizations
        /// FIX #2: Show ALL laps (including pit laps, formation laps) for visual feedback
        /// Filtering for averages is separate - sparklines show actual consumption reality
        /// </summary>
        private void UpdateSparklines(FuelData data)
        {
            // Get ALL lap history from fuel calculator (unfiltered for visual representation)
            var allLapHistory = _fuelCalculator.GetLapHistory();
            
            // Update lap history sparkline with LAST 5 laps (including pit/formation laps)
            // This shows the actual fuel consumption pattern, even if some laps are excluded from averages
            var last5Laps = allLapHistory.TakeLast(5).ToList();
            
            // Update sparkline queue with actual fuel usage from all laps
            if (last5Laps.Count > 0)
            {
                // Rebuild queue from last 5 laps (whether valid for averaging or not)
                _lapUsageHistory.Clear();
                foreach (var lap in last5Laps)
                {
                    _lapUsageHistory.Enqueue(lap.FuelUsed); // Show actual usage, even pit laps (may show 0 or refuel)
                }
            }

            // Render lap history sparkline
            var lapSparkline = FindName("LapSparkline") as System.Windows.Shapes.Polyline;
            var lapCanvas = FindName("LapSparklineCanvas") as System.Windows.Controls.Canvas;
            if (lapSparkline != null && lapCanvas != null && _lapUsageHistory.Count >= 1)
            {
                UpdateSparklineGuideLines("Lap", lapCanvas);
                // Use gradual width buildup for lap sparkline (1/5th per lap until full at 5 laps)
                RenderSparkline(lapSparkline, lapCanvas, _lapUsageHistory, amplificationFactor: 1.0f, useGradualWidth: true);
                if (_lapUsageHistory.Count > 1)
                {
                    UpdateSparklineMinMax("LapMinText", "LapMaxText", _lapUsageHistory, ref _lapSparklineMinEver, ref _lapSparklineMaxEver);
                }
            }
        }

        /// <summary>
        /// Update min/max labels for sparklines with historical tracking
        /// </summary>
        private void UpdateSparklineMinMax(string minName, string maxName, System.Collections.Generic.Queue<float> dataPoints, ref float minEver, ref float maxEver)
        {
            if (dataPoints.Count < 1)
                return;

            var data = dataPoints.ToArray();
            
            // Update historical min/max with current data
            foreach (var value in data)
            {
                if (value < minEver) minEver = value;
                if (value > maxEver) maxEver = value;
            }

            var minText = FindName(minName) as TextBlock;
            var maxText = FindName(maxName) as TextBlock;

            if (minText != null)
                minText.Text = minEver.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            if (maxText != null)
                maxText.Text = maxEver.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Update sparkline guide lines (floor, ceiling, middle) to match canvas width
        /// </summary>
        private void UpdateSparklineGuideLines(string prefix, Canvas canvas)
        {
            if (canvas.ActualWidth == 0 || canvas.ActualHeight == 0)
                return;

            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;

            // Update floor line (bottom)
            var floorLine = FindName($"{prefix}FloorLine") as System.Windows.Shapes.Line;
            if (floorLine != null)
            {
                floorLine.X2 = width;
                floorLine.Y1 = height - 2;
                floorLine.Y2 = height - 2;
            }

            // Update middle line
            var middleLine = FindName($"{prefix}MiddleLine") as System.Windows.Shapes.Line;
            if (middleLine != null)
            {
                middleLine.X2 = width;
                middleLine.Y1 = height / 2.0;
                middleLine.Y2 = height / 2.0;
            }

            // Update ceiling line (top)
            var ceilingLine = FindName($"{prefix}CeilingLine") as System.Windows.Shapes.Line;
            if (ceilingLine != null)
            {
                ceilingLine.X2 = width;
                ceilingLine.Y1 = 2;
                ceilingLine.Y2 = 2;
            }
        }

        /// <summary>
        /// Unified sparkline rendering with support for amplification and gradual width buildup
        /// </summary>
        /// <param name="polyline">Polyline element to render into</param>
        /// <param name="canvas">Canvas containing the polyline</param>
        /// <param name="dataPoints">Data points to render</param>
        /// <param name="amplificationFactor">Optional amplification factor (e.g., 50.0 for live fuel sparkline). Use 1.0 for no amplification.</param>
        /// <param name="useGradualWidth">If true, width grows 1/5th per data point until full at 5 points (lap sparkline behavior)</param>
        private void RenderSparkline(System.Windows.Shapes.Polyline polyline, System.Windows.Controls.Canvas canvas, 
            System.Collections.Generic.Queue<float> dataPoints, float amplificationFactor = 1.0f, bool useGradualWidth = false)
        {
            if (polyline == null || canvas == null || dataPoints == null || canvas.ActualWidth == 0 || canvas.ActualHeight == 0)
                return;

            var data = dataPoints.ToArray();
            var points = new System.Windows.Media.PointCollection();

            if (data.Length < 1)
                return;

            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;

            // Calculate usable width (gradual width for lap sparkline, full width otherwise)
            double usableWidth = width;
            if (useGradualWidth)
            {
                int lapCount = data.Length;
                double widthFraction = Math.Min(lapCount / 5.0, 1.0); // 0.2, 0.4, 0.6, 0.8, 1.0
                usableWidth = width * widthFraction;
            }

            // Special case: Only 1 data point - show horizontal middle line
            if (data.Length == 1)
            {
                double midY = height / 2.0;
                points.Add(new System.Windows.Point(0, midY));
                points.Add(new System.Windows.Point(usableWidth, midY));
                polyline.Points = points;
                return;
            }

            // Apply amplification if specified (for live fuel sparkline)
            float[] processedData = amplificationFactor != 1.0f 
                ? data.Select(x => x * amplificationFactor).ToArray() 
                : data;

            // Find min/max for scaling
            float min = amplificationFactor != 1.0f ? 0 : float.MaxValue; // Amplified sparklines use 0 as floor
            float max = float.MinValue;

            foreach (var value in processedData)
            {
                if (amplificationFactor == 1.0f && value < min) min = value;
                if (value > max) max = value;
            }

            // Add small padding to prevent flat lines
            float range = max - min;
            if (range < 0.01f) range = 0.01f;

            // Generate points (left to right)
            double xStep = usableWidth / (processedData.Length - 1);

            for (int i = 0; i < processedData.Length; i++)
            {
                double x = i * xStep;
                // Invert Y so higher values are at top
                double y = height - ((processedData[i] - min) / range * (height - 4)) - 2; // 2px padding
                points.Add(new System.Windows.Point(x, y));
            }

            polyline.Points = points;
        }

        /// <summary>
        /// Format fuel value with 2 decimals (no unit) - delegates to TelemetryCalculations
        /// </summary>
        private string FormatFuelValue(float value)
        {
            return TelemetryCalculations.FormatFuelValue(value);
        }

        /// <summary>
        /// Format fuel value with unit (L or gal) - delegates to TelemetryCalculations
        /// </summary>
        private string FormatFuel(float value)
        {
            return TelemetryCalculations.FormatFuel(value, AppSettings.Instance.UseMetric);
        }

        /// <summary>
        /// Format fuel delta with sign and unit - delegates to TelemetryCalculations
        /// </summary>
        private string FormatFuelDelta(float value)
        {
            return TelemetryCalculations.FormatFuelDelta(value, AppSettings.Instance.UseMetric);
        }

        /// <summary>
        /// Format tank size with unit (L or gal) - delegates to TelemetryCalculations
        /// </summary>
        private string FormatTankSize(float value)
        {
            return TelemetryCalculations.FormatFuel(value, AppSettings.Instance.UseMetric);
        }

        /// <summary>
        /// Get color for fuel level based on percentage
        /// </summary>
        private Brush GetFuelLevelColor(float fuelPct)
        {
            return fuelPct switch
            {
                >= 0.5f => CachedBrushes.Teal,      // Teal (good)
                >= 0.25f => CachedBrushes.Yellow,   // Yellow
                >= 0.1f => CachedBrushes.Orange,    // Orange
                _ => CachedBrushes.Red              // Red
            };
        }

        /// <summary>
        /// Get color for laps remaining (simplified: orange ≥2 laps, red <2 laps)
        /// </summary>
        private Brush GetLapsRemainingColor(float laps)
        {
            return laps switch
            {
                >= LayoutConstants.URGENT_LAPS_THRESHOLD => CachedBrushes.Orange,    // Orange - SOON
                _ => CachedBrushes.Red                                                 // Red - URGENT
            };
        }

        /// <summary>
        /// Get color for iRacing delta
        /// </summary>
        private Brush GetDeltaColor(float delta)
        {
            return delta switch
            {
                > 0.5f => CachedBrushes.Teal,       // Teal (we have more)
                > -0.5f => CachedBrushes.Yellow,    // Yellow (close)
                _ => CachedBrushes.Orange           // Orange (we have less)
            };
        }

        /// <summary>
        /// Manage blinking animation for critical fuel
        /// </summary>
        private void ManageBlinking(float laps)
        {
            var settings = AppSettings.Instance;
            bool shouldBlink = settings.FuelWidget_BlinkCritical && laps < settings.FuelWidget_CriticalThreshold;

            if (shouldBlink && _blinkTimer == null)
            {
                // Start blinking
                _blinkTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _blinkTimer.Tick += BlinkTimer_Tick;
                _blinkTimer.Start();
            }
            else if (!shouldBlink && _blinkTimer != null)
            {
                // Stop blinking
                _blinkTimer.Stop();
                _blinkTimer.Tick -= BlinkTimer_Tick;
                _blinkTimer = null;
                var lapsRemainingText = FindName("LapsRemainingText") as TextBlock;
                if (lapsRemainingText != null)
                {
                    lapsRemainingText.Opacity = 1.0;
                }
                _isBlinkVisible = true;
            }
        }

        /// <summary>
        /// Blink timer tick handler
        /// </summary>
        private void BlinkTimer_Tick(object? sender, EventArgs e)
        {
            _isBlinkVisible = !_isBlinkVisible;
            var lapsRemainingText = FindName("LapsRemainingText") as TextBlock;
            if (lapsRemainingText != null)
            {
                lapsRemainingText.Opacity = _isBlinkVisible ? 1.0 : 0.3;
            }
        }

        /// <summary>
        /// Reset fuel calculations (call on session change)
        /// </summary>
        public void ResetCalculations()
        {
            _fuelCalculator.Reset();

            // Reset sparkline historical min/max tracking
            _liveSparklineMinEver = float.MaxValue;
            _liveSparklineMaxEver = float.MinValue;
            _lapSparklineMinEver = float.MaxValue;
            _lapSparklineMaxEver = float.MinValue;

            // Clear sparkline data
            _liveUsageHistory.Clear();
            _lapUsageHistory.Clear();
            _lastCurrentFuel = 0f;
        }

        /// <summary>
        /// Cleanup resources when widget is closed
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _fuelCalculator.FuelDataUpdated -= OnFuelDataUpdated;
            AppSettings.Instance.SettingsChanged -= OnSettingsChanged;

            if (_blinkTimer != null)
            {
                _blinkTimer.Stop();
                _blinkTimer.Tick -= BlinkTimer_Tick;
                _blinkTimer = null;
            }

            if (_liveUpdateTimer != null)
            {
                _liveUpdateTimer.Stop();
                _liveUpdateTimer.Tick -= OnLiveUpdateTimerTick;
                _liveUpdateTimer = null;
            }

            base.OnClosed(e);
        }
    }
}
