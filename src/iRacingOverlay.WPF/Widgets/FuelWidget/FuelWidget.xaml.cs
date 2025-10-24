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
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.FuelWidget
{
    /// <summary>
    /// Enhanced Fuel Widget with FuelCalculatorService integration
    /// Displays fuel level, consumption, and race strategy data
    /// Supports three layouts: Tower (vertical), Bar (horizontal), Grid (2x2)
    /// </summary>
    public partial class FuelWidget : WidgetBase
    {
        private readonly FuelCalculatorService _fuelCalculator;
        private DispatcherTimer? _blinkTimer;
        private bool _isBlinkVisible = true;
        private string _currentLayout = "Tower";
        
        // Sparkline data tracking
        private readonly System.Collections.Generic.Queue<float> _liveUsageHistory = new(10);  // 5 seconds at 0.5s interval = 10 data points
        private readonly System.Collections.Generic.Queue<float> _lapUsageHistory = new(5);    // Last 5 laps
        private float _lastUsageValue = 0f;
        private float _lastCurrentFuel = 0f;  // Track fuel level changes for live sparkline
        private DispatcherTimer? _liveUpdateTimer;  // Timer for live fuel updates (0.5 second interval)
        
        // Historical min/max tracking for sparklines (persists across updates)
        private float _liveSparklineMinEver = float.MaxValue;
        private float _liveSparklineMaxEver = float.MinValue;
        private float _lapSparklineMinEver = float.MaxValue;
        private float _lapSparklineMaxEver = float.MinValue;

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
                Interval = TimeSpan.FromMilliseconds(500) // 0.5 seconds for smoother real-time tracking
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
            Width = 240 * scale;  // Increased from 210 to 240 to properly show all labels and values
            MinHeight = 200 * scale;  // Minimum height for basic display
            MaxHeight = 600 * scale;  // Maximum height for all fields visible
            SizeToContent = SizeToContent.Height;
        }

        /// <summary>
        /// Handle settings changes
        /// </summary>
        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Check if layout changed
                if (_currentLayout != AppSettings.Instance.FuelWidget_Layout)
                {
                    SwitchLayout(AppSettings.Instance.FuelWidget_Layout);
                }

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
        /// Switch between Tower, Bar, and Grid layouts
        /// </summary>
        private void SwitchLayout(string newLayout)
        {
            _currentLayout = newLayout;

            // For now, just update dimensions based on layout
            // Full dynamic layout loading would require restructuring
            // So we'll keep the Tower layout as primary and adjust sizing
            UpdateLayoutDimensions();
        }

        /// <summary>
        /// Update widget dimensions based on selected layout
        /// </summary>
        private void UpdateLayoutDimensions()
        {
            double scale = AppSettings.Instance.FuelWidget_Scale;

            switch (_currentLayout)
            {
                case "Tower":
                    Width = 240 * scale;  // Increased from 210 to 240 to properly show all labels and values
                    Height = 280 * scale;
                    break;
                case "Bar":
                    Width = 440 * scale;  // Increased from 410 to 440 to match wider design
                    Height = 120 * scale;
                    break;
                case "Grid":
                    Width = 320 * scale;  // Increased from 290 to 320 to match wider design
                    Height = 200 * scale;
                    break;
                default:
                    Width = 240 * scale;  // Increased from 210 to 240 to properly show all labels and values
                    Height = 280 * scale;
                    break;
            }
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
            
            // Sparkline visibility (always visible for now)
            var sparklineGrid = FindName("SparklineGrid") as FrameworkElement;
            if (sparklineGrid != null)
            {
                sparklineGrid.Visibility = Visibility.Visible;
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
            var l10Grid = FindName("L10Grid") as FrameworkElement;
            var l10Row = FindName("L10Row") as RowDefinition;
            if (l10Grid != null && l10Row != null)
            {
                l10Grid.Visibility = settings.FuelWidget_ShowL10 ? Visibility.Visible : Visibility.Collapsed;
                l10Row.Height = settings.FuelWidget_ShowL10 ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            }

            var sessionGrid = FindName("SessionGrid") as FrameworkElement;
            var sessionRow = FindName("SessionRow") as RowDefinition;
            if (sessionGrid != null && sessionRow != null)
            {
                sessionGrid.Visibility = settings.FuelWidget_ShowSession ? Visibility.Visible : Visibility.Collapsed;
                sessionRow.Height = settings.FuelWidget_ShowSession ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
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

            // Fuel Pressure - Row 13 (can be toggled independently)
            var pressureGrid = FindName("PressureGrid") as FrameworkElement;
            var pressureRow = FindName("PressureRow") as RowDefinition;
            if (pressureGrid != null)
            {
                // Show pressure if both pit strategy AND pressure toggle are enabled
                bool showPressure = showStrategy && settings.FuelWidget_ShowFuelPressure;
                pressureGrid.Visibility = showPressure ? Visibility.Visible : Visibility.Collapsed;
                if (pressureRow != null)
                    pressureRow.Height = showPressure ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
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

            // Tank capacity with sputtering threshold info (Enhanced Phase 2.1 + Phase 2.2)
            var tankCapacityText = FindName("TankCapacityText") as TextBlock;
            if (tankCapacityText != null)
            {
                tankCapacityText.Text = FormatTankSize(data.TankCapacity);
                
                // Set tooltip with car-specific info + dynamic buffer details (Phase 2.2)
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
                    lastLapText.Foreground = new SolidColorBrush(Color.FromRgb(255, 128, 0)); // Orange
                    
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
                            > 0.02f => new SolidColorBrush(Color.FromRgb(255, 51, 51)),  // Red - using MORE fuel
                            < -0.02f => new SolidColorBrush(Color.FromRgb(0, 255, 0)),    // Green - using LESS fuel
                            _ => new SolidColorBrush(Color.FromRgb(170, 170, 170))   // Gray - stable
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
                    lastLapText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // Gray
                    if (lastLapTrendArrow != null)
                    {
                        lastLapTrendArrow.Visibility = Visibility.Collapsed;
                    }
                }
            }

            // L5 average (show -- if no data yet)
            var l5AvgText = FindName("L5AvgText") as TextBlock;
            if (l5AvgText != null)
            {
                if (data.AvgFuelPerLap_L5 > 0)
                {
                    l5AvgText.Text = $"{FormatFuelValue(data.AvgFuelPerLap_L5)}";
                    l5AvgText.Foreground = new SolidColorBrush(Color.FromRgb(0, 128, 128)); // Teal
                }
                else
                {
                    l5AvgText.Text = "--";
                    l5AvgText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // Gray
                }
            }

            // L10 average (optional, show -- if no data yet)
            var l10AvgText = FindName("L10AvgText") as TextBlock;
            if (l10AvgText != null)
            {
                if (data.AvgFuelPerLap_L10 > 0)
                {
                    l10AvgText.Text = $"{FormatFuelValue(data.AvgFuelPerLap_L10)}";
                    l10AvgText.Foreground = new SolidColorBrush(Color.FromRgb(0, 128, 128)); // Teal
                }
                else
                {
                    l10AvgText.Text = "--";
                    l10AvgText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // Gray
                }
            }

            // Session average (optional, show -- if no data yet)
            var sessionAvgText = FindName("SessionAvgText") as TextBlock;
            if (sessionAvgText != null)
            {
                if (data.AvgFuelPerLap_Session > 0)
                {
                    sessionAvgText.Text = $"{FormatFuelValue(data.AvgFuelPerLap_Session)}";
                    sessionAvgText.Foreground = new SolidColorBrush(Color.FromRgb(0, 128, 128)); // Teal
                }
                else
                {
                    sessionAvgText.Text = "--";
                    sessionAvgText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // Gray
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
                    lapsRemainingText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // Gray
                }
            }

            // Fuel needed to finish (TO GO) - shows liters needed to complete race/session
            // FUEL NEED: Shows total fuel needed for race or 5-lap reference (not delta/remaining)
            var canFinishText = FindName("CanFinishText") as TextBlock;
            if (canFinishText != null)
            {
                // Show if we have lap data, regardless of session type
                if (data.AvgFuelPerLap_L5 > 0)
                {
                    float fuelNeeded;
                    
                    if (data.RaceLapsRemaining > 0)
                    {
                        // RACE mode: Show fuel needed to finish (includes race laps + buffer + 0.3L sputtering threshold)
                        fuelNeeded = data.FuelNeededToFinish;
                    }
                    else
                    {
                        // QUALIFYING/PRACTICE mode: Show total fuel for 5-lap reference (not confusing delta)
                        fuelNeeded = data.AvgFuelPerLap_L5 * 5;
                    }
                    
                    canFinishText.Text = $"{FormatFuelValue(fuelNeeded)}";
                    
                    // Color based on whether we have enough fuel
                    // Compare current fuel to needed fuel
                    canFinishText.Foreground = data.CurrentFuel >= fuelNeeded
                        ? new SolidColorBrush(Color.FromRgb(0, 128, 128))  // Teal (have enough)
                        : new SolidColorBrush(Color.FromRgb(255, 51, 51)); // Red (not enough)
                }
                else
                {
                    canFinishText.Text = "--";
                    canFinishText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // Gray
                }
            }

            // iRacing delta - REMOVED per user request (not needed, causes confusion)

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
                        pitFuelText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // Gray
                    }
                    else if (fuelNeeded > 0)
                    {
                        // Show fuel to add with simplified color coding (removed yellow, kept orange/red)
                        pitFuelText.Text = $"{FormatFuelValue(fuelNeeded)}";
                        
                        // Simplified color coding: Red (<2 laps), Orange (≥2 laps)
                        pitFuelText.Foreground = data.LapsRemaining < 2.0f
                            ? new SolidColorBrush(Color.FromRgb(255, 51, 51))   // Red - PIT URGENT
                            : new SolidColorBrush(Color.FromRgb(255, 128, 0));  // Orange - PIT SOON
                    }
                    else
                    {
                        // Can finish without stop - show "OK" in green
                        pitFuelText.Text = "OK";
                        pitFuelText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)); // Green
                    }
                }
                else
                {
                    pitFuelText.Text = "--";  // No data yet
                    pitFuelText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // Gray
                }
            }

            // Pit window countdown (Phase 5.C Enhancement) - shows intelligent pit window with earliest/optimal/latest laps
            var pitWindowText = FindName("PitWindowText") as TextBlock;
            if (pitWindowText != null)
            {
                // VALIDATION: Reject inverted pit windows (PitWindowStart > PitWindowEnd = invalid data)
                bool hasValidPitWindow = data.OptimalPitLap > 0 && 
                                        data.PitWindowStart > 0 && 
                                        data.PitWindowEnd > 0 &&
                                        data.PitWindowStart <= data.PitWindowEnd; // CRITICAL: Reject inverted windows
                
                // Phase 5.C: Enhanced pit window display with multi-lap ranges
                if (hasValidPitWindow)
                {
                    // Show pit window range if we have Phase 5.B data
                    int currentLap = data.CurrentLap;
                    int lapsUntilWindow = Math.Max(0, data.PitWindowStart - currentLap);
                    int lapsUntilOptimal = Math.Max(0, data.OptimalPitLap - currentLap);
                    
                    // Color coding based on urgency
                    if (currentLap >= data.PitWindowStart && currentLap <= data.PitWindowEnd)
                    {
                        // IN OPTIMAL WINDOW - Green
                        pitWindowText.Text = $"L{data.PitWindowStart}-{data.PitWindowEnd} (NOW)";
                        pitWindowText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                    }
                    else if (currentLap < data.PitWindowStart)
                    {
                        // BEFORE WINDOW - Teal (shows laps until window opens)
                        pitWindowText.Text = $"L{data.PitWindowStart}-{data.PitWindowEnd} ({lapsUntilWindow}L)";
                        pitWindowText.Foreground = new SolidColorBrush(Color.FromRgb(0, 128, 128)); // Teal
                    }
                    else if (currentLap > data.PitWindowEnd && currentLap < data.LatestPitLap)
                    {
                        // AFTER OPTIMAL BUT BEFORE LATEST - Orange (getting urgent)
                        int lapsUntilLatest = data.LatestPitLap - currentLap;
                        pitWindowText.Text = $"Late ({lapsUntilLatest}L left)";
                        pitWindowText.Foreground = new SolidColorBrush(Color.FromRgb(255, 128, 0)); // Orange
                    }
                    else if (currentLap >= data.LatestPitLap)
                    {
                        // PAST LATEST - Red (critical)
                        pitWindowText.Text = $"CRITICAL (L{data.LatestPitLap})";
                        pitWindowText.Foreground = new SolidColorBrush(Color.FromRgb(255, 51, 51)); // Red
                    }
                    else
                    {
                        // Fallback: Show optimal lap
                        pitWindowText.Text = $"L{data.OptimalPitLap} ({lapsUntilOptimal}L)";
                        pitWindowText.Foreground = new SolidColorBrush(Color.FromRgb(0, 217, 255)); // Cyan
                    }
                }
                // Fallback: Show laps remaining if Phase 5.B data not available or invalid
                else if (data.LapsRemaining > 0 && data.AvgFuelPerLap_L5 > 0)
                {
                    // Hide PIT IN if very close to finish (<0.6L remaining or <0.3 laps)
                    bool nearFinish = data.CurrentFuel < 0.6f || data.LapsRemaining < 0.3f;
                    
                    if (nearFinish)
                    {
                        // Too close to finish - hide field
                        pitWindowText.Text = "--";
                        pitWindowText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // Gray
                    }
                    else
                    {
                        // Show laps remaining until fuel runs out (1 decimal for precision without clutter)
                        pitWindowText.Text = data.LapsRemaining.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                        
                        // Simplified color coding: Red (<2 laps), Orange (≥2 laps)
                        pitWindowText.Foreground = data.LapsRemaining < 2.0f
                            ? new SolidColorBrush(Color.FromRgb(255, 51, 51))   // Red - URGENT
                            : new SolidColorBrush(Color.FromRgb(255, 128, 0));  // Orange - SOON
                    }
                }
                else
                {
                    // No valid data yet
                    pitWindowText.Text = "--";
                    pitWindowText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // Gray
                }
            }
            
            // Phase 5D: Pit exit position prediction (Option C - inline with PIT WINDOW)
            var pitExitPositionText = FindName("PitExitPositionText") as TextBlock;
            if (pitExitPositionText != null)
            {
                // Check if feature is enabled and we have valid pit exit data
                bool showPitExit = settings.FuelWidget_ShowPitExitPosition && 
                                  data.PitExitPositionValid && 
                                  !string.IsNullOrEmpty(data.PitExitGapDescription);
                
                if (showPitExit)
                {
                    // Format with confidence icon: "↳ Exit P12 🟢: 4s to #14, 10s from #9"
                    string confidenceIcon = !string.IsNullOrEmpty(data.PitExitConfidenceIcon) 
                        ? $" {data.PitExitConfidenceIcon}" 
                        : "";
                    pitExitPositionText.Text = $"↳ Exit P{data.PitExitPosition}{confidenceIcon}: {data.PitExitGapDescription}";
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

            // Fuel pressure (optional) - Show bar value with color coding only (percentage removed per user request)
            var pressureText = FindName("PressureText") as TextBlock;
            if (pressureText != null)
            {
                // Show pressure value only (no percentage - it's broken and unnecessary for visual display)
                pressureText.Text = $"{data.FuelPressure:F2} bar";
                
                // Color coding based on pressure drop (Enhanced Phase 2.1)
                // Critical: >20% drop (red, imminent sputtering)
                // Warning: 10-20% drop (orange, low fuel risk)
                // Normal: <10% drop (green, safe)
                pressureText.Foreground = data.FuelPressureDropPct switch
                {
                    > 20f => new SolidColorBrush(Color.FromRgb(255, 51, 51)),   // Red - Critical
                    > 10f => new SolidColorBrush(Color.FromRgb(255, 128, 0)),   // Orange - Warning
                    _ => new SolidColorBrush(Color.FromRgb(0, 255, 0))          // Green - Normal
                };
            }

            // Update sparklines
            UpdateSparklines(data);

            // Handle blinking for critical fuel
            ManageBlinking(data.LapsRemaining);

            // Phase 3: Update fuel saving display
            UpdateFuelSavingDisplay(data);
        }

        /// <summary>
        /// Update Phase 3 fuel saving display elements
        /// </summary>
        private void UpdateFuelSavingDisplay(FuelData data)
        {
            var settings = AppSettings.Instance;

            // Check if pit strategy section is enabled
            // FIX: Show fuel saving when pit strategy is enabled (they're part of the same section)
            // User wants it to stay visible when "Show pit strategy section" is toggled on
            bool showFuelSaving = settings.FuelWidget_ShowPitStrategy;

            // Find all Phase 3 UI elements
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
                // Hide all Phase 3 elements
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
                        rect.Fill = new SolidColorBrush(Color.FromRgb(255, 111, 0)); // Orange - impossible target
                    }
                    else
                    {
                        // Target is achievable - color by progress: Green (≥70%), Yellow (30-70%), Red (<30%)
                        rect.Fill = data.SavingProgress switch
                        {
                            >= 70f => new SolidColorBrush(Color.FromRgb(76, 175, 80)),  // Green
                            >= 30f => new SolidColorBrush(Color.FromRgb(255, 193, 7)),  // Yellow
                            _ => new SolidColorBrush(Color.FromRgb(244, 67, 54))        // Red
                        };
                    }
                }
            }

            // Update current saving rate
            var currentSavingRateText = FindName("CurrentSavingRateText") as TextBlock;
            if (currentSavingRateText != null)
            {
                currentSavingRateText.Text = $"{data.CurrentSavingRate:F2}L/lap";
                currentSavingRateText.Foreground = data.SavingProgress >= 50f
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))  // Green
                    : new SolidColorBrush(Color.FromRgb(255, 128, 0)); // Orange
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
                        3 => new SolidColorBrush(Color.FromRgb(183, 28, 28)),    // Critical - Dark Red
                        2 => new SolidColorBrush(Color.FromRgb(255, 111, 0)),    // Warning - Orange
                        1 => new SolidColorBrush(Color.FromRgb(27, 94, 32)),     // Info - Dark Green
                        _ => new SolidColorBrush(Color.FromRgb(66, 66, 66))      // None - Gray
                    };
                }
            }
            else
            {
                if (strategicAlertBorder != null) strategicAlertBorder.Visibility = Visibility.Collapsed;
                if (fuelSavingAlertRow != null) fuelSavingAlertRow.Height = new GridLength(0);
            }

            // Show optimal pit lap if enabled and available (Phase 5.C Enhanced Display)
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
                        // Phase 5.C: Enhanced display with context
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
                        optimalPitLapText.Foreground = new SolidColorBrush(Color.FromRgb(255, 51, 51)); // Red - Critical
                    }
                    else if (data.FuelCriticalityScore > 80f)
                    {
                        optimalPitLapText.Foreground = new SolidColorBrush(Color.FromRgb(255, 128, 0)); // Orange - Urgent
                    }
                    else
                    {
                        optimalPitLapText.Foreground = new SolidColorBrush(Color.FromRgb(0, 217, 255)); // Cyan - Strategic
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
        /// Live update timer tick - updates live fuel consumption sparkline every 0.5s
        /// Always updates to show activity, even when stationary
        /// </summary>
        private void OnLiveUpdateTimerTick(object? sender, EventArgs e)
        {
            // Get current fuel from calculator service
            var fuelData = _fuelCalculator.CurrentData;
            if (fuelData == null)
                return;

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
                RenderSparklineAmplified(liveSparkline, liveCanvas, _liveUsageHistory);
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
                // Use special rendering for gradual lap buildup
                RenderLapSparkline(lapSparkline, lapCanvas, _lapUsageHistory);
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
        /// Render sparkline from data points
        /// </summary>
        private void RenderSparkline(System.Windows.Shapes.Polyline polyline, System.Windows.Controls.Canvas canvas, System.Collections.Generic.Queue<float> dataPoints)
        {
            if (canvas.ActualWidth == 0 || canvas.ActualHeight == 0)
                return;

            var points = new System.Windows.Media.PointCollection();
            var data = dataPoints.ToArray();
            
            if (data.Length < 2)
                return;

            // Find min/max for scaling
            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (var value in data)
            {
                if (value < min) min = value;
                if (value > max) max = value;
            }

            // Add small padding to prevent flat lines
            float range = max - min;
            if (range < 0.01f) range = 0.01f;
            
            // Generate points (left to right)
            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;
            double xStep = width / (data.Length - 1);

            for (int i = 0; i < data.Length; i++)
            {
                double x = i * xStep;
                // Invert Y so higher values are at top
                double y = height - ((data[i] - min) / range * (height - 4)) - 2; // 2px padding
                points.Add(new System.Windows.Point(x, y));
            }

            polyline.Points = points;
        }

        /// <summary>
        /// Renders sparkline with amplified values for better visibility of small fuel consumption
        /// </summary>
        private void RenderSparklineAmplified(System.Windows.Shapes.Polyline polyline, Canvas canvas, Queue<float> dataQueue, float amplificationFactor = 50f)
        {
            if (polyline == null || canvas == null || dataQueue == null || dataQueue.Count < 2)
                return;

            var data = dataQueue.ToArray();

            // Amplify data for visual rendering (but labels show actual values via separate UpdateSparklineMinMax call)
            float[] amplifiedData = data.Select(x => x * amplificationFactor).ToArray();
            
            // Calculate range for amplified values (min fixed at 0)
            float min = 0;
            float max = amplifiedData.Max();
            float range = max - min;

            if (range < 0.001f) range = 0.001f; // Prevent div by zero

            var points = new System.Windows.Media.PointCollection();

            // Generate points (left to right)
            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;
            double xStep = width / (amplifiedData.Length - 1);

            for (int i = 0; i < amplifiedData.Length; i++)
            {
                double x = i * xStep;
                // Invert Y so higher values are at top
                double y = height - ((amplifiedData[i] - min) / range * (height - 4)) - 2; // 2px padding
                points.Add(new System.Windows.Point(x, y));
            }

            polyline.Points = points;
        }

        /// <summary>
        /// Renders lap sparkline with gradual width buildup (grows 1/5th per lap until full at 5 laps)
        /// </summary>
        private void RenderLapSparkline(System.Windows.Shapes.Polyline polyline, Canvas canvas, Queue<float> dataQueue)
        {
            if (canvas.ActualWidth == 0 || canvas.ActualHeight == 0)
                return;

            var data = dataQueue.ToArray();
            var points = new System.Windows.Media.PointCollection();

            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;

            // Calculate the usable width based on lap count (1/5th per lap, max at 5 laps)
            int lapCount = data.Length;
            double widthFraction = Math.Min(lapCount / 5.0, 1.0); // 0.2, 0.4, 0.6, 0.8, 1.0
            double usableWidth = width * widthFraction;

            // Special case: Only 1 lap - show horizontal middle line at 1/5th width
            if (data.Length == 1)
            {
                double midY = height / 2.0;
                points.Add(new System.Windows.Point(0, midY));
                points.Add(new System.Windows.Point(usableWidth, midY));
                polyline.Points = points;
                return;
            }

            // 2+ laps: Render sparkline within the growing width
            float min = data.Min();
            float max = data.Max();
            float range = max - min;
            if (range < 0.01f) range = 0.01f; // Prevent flat lines

            // Calculate x-step within the usable width
            double xStep = usableWidth / (data.Length - 1);

            for (int i = 0; i < data.Length; i++)
            {
                double x = i * xStep;
                // Invert Y so higher values are at top
                double y = height - ((data[i] - min) / range * (height - 4)) - 2; // 2px padding
                points.Add(new System.Windows.Point(x, y));
            }

            polyline.Points = points;
        }

        /// <summary>
        /// Format fuel value with 2 decimals (no unit)
        /// </summary>
        private string FormatFuelValue(float value)
        {
            return value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Format fuel value with unit (L or gal) - 2 decimals
        /// </summary>
        private string FormatFuel(float value)
        {
            if (AppSettings.Instance.UseMetric)
            {
                return $"{value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}L";
            }
            else
            {
                // Convert liters to gallons (1 L = 0.264172 gal)
                double gallons = value * 0.264172;
                return $"{gallons.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}gal";
            }
        }
        
        /// <summary>
        /// Format fuel delta with sign and unit
        /// </summary>
        private string FormatFuelDelta(float value)
        {
            string sign = value >= 0 ? "+" : "";
            if (AppSettings.Instance.UseMetric)
            {
                return $"{sign}{value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}L";
            }
            else
            {
                double gallons = value * 0.264172;
                return $"{sign}{gallons.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}gal";
            }
        }

        /// <summary>
        /// Format tank size with unit (L or gal) - 2 decimals
        /// </summary>
        private string FormatTankSize(float value)
        {
            if (AppSettings.Instance.UseMetric)
            {
                return $"{value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}L";
            }
            else
            {
                // Convert liters to gallons (1 L = 0.264172 gal)
                double gallons = value * 0.264172;
                return $"{gallons.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}gal";
            }
        }

        /// <summary>
        /// Get color for fuel level based on percentage
        /// </summary>
        private Brush GetFuelLevelColor(float fuelPct)
        {
            return fuelPct switch
            {
                >= 0.5f => new SolidColorBrush(Color.FromRgb(0, 128, 128)),    // Teal (good)
                >= 0.25f => new SolidColorBrush(Color.FromRgb(255, 255, 0)),   // Yellow
                >= 0.1f => new SolidColorBrush(Color.FromRgb(255, 128, 0)),    // Orange
                _ => new SolidColorBrush(Color.FromRgb(255, 51, 51))           // Red
            };
        }

        /// <summary>
        /// Get color for laps remaining (simplified: orange ≥2 laps, red <2 laps)
        /// </summary>
        private Brush GetLapsRemainingColor(float laps)
        {
            return laps switch
            {
                >= 2.0f => new SolidColorBrush(Color.FromRgb(255, 128, 0)),    // Orange - SOON
                _ => new SolidColorBrush(Color.FromRgb(255, 51, 51))           // Red - URGENT
            };
        }

        /// <summary>
        /// Get color for iRacing delta
        /// </summary>
        private Brush GetDeltaColor(float delta)
        {
            return delta switch
            {
                > 0.5f => new SolidColorBrush(Color.FromRgb(0, 128, 128)),     // Teal (we have more)
                > -0.5f => new SolidColorBrush(Color.FromRgb(255, 255, 0)),    // Yellow (close)
                _ => new SolidColorBrush(Color.FromRgb(255, 128, 0))           // Orange (we have less)
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
            _lastUsageValue = 0f;
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
