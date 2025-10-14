using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.FuelWidget
{
    /// <summary>
    /// Fuel Calculator Widget - displays fuel level, usage, and laps remaining
    /// </summary>
    public partial class FuelWidget : WidgetBase
    {
        private double _lastFuelLevel = 0;
        private double _fuelUsedLastLap = 0;
        private double _totalFuelUsed = 0;
        private int _lapsCompleted = 0;
        private int _lastLapNumber = -1;

        public override WidgetType WidgetType => WidgetType.Fuel;

        public FuelWidget(ITelemetryService telemetryService, WidgetConfig? config = null) 
            : base(telemetryService, config)
        {
            InitializeComponent();
            
            // Subscribe to SizeChanged to update scale transform
            SizeChanged += OnWidgetSizeChanged;
            
            // Apply default size if not specified in config
            if (Config.Width == 0 || Config.Height == 0)
            {
                Width = 280;
                Height = 220;
                Config.Width = 280;
                Config.Height = 220;
            }
        }
        
        /// <summary>
        /// Handle widget resize by scaling the content via LayoutTransform.
        /// This prevents content shift by maintaining relative positions of all elements.
        /// </summary>
        private void OnWidgetSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Calculate scale factors based on original 280x220 design
            double scaleX = ActualWidth / 280.0;
            double scaleY = ActualHeight / 220.0;
            
            // Use uniform scale (smallest of the two to maintain aspect ratio)
            double scale = Math.Min(scaleX, scaleY);
            
            // Apply scale transform to the entire grid
            MainGrid.LayoutTransform = new ScaleTransform(scale, scale);
        }

        protected override void UpdateUI(TelemetryData data)
        {
            // Update fuel level
            double currentFuel = data.FuelLevel;
            CurrentFuelText.Text = FormatFuelValue(currentFuel);

            // Detect lap completion and calculate fuel used
            if (data.Lap > _lastLapNumber && _lastLapNumber >= 0)
            {
                // Lap completed
                _fuelUsedLastLap = _lastFuelLevel - currentFuel;
                if (_fuelUsedLastLap > 0) // Only count positive fuel usage
                {
                    _totalFuelUsed += _fuelUsedLastLap;
                    _lapsCompleted++;
                }
                _lastLapNumber = data.Lap;
            }
            else if (_lastLapNumber < 0)
            {
                // First lap initialization
                _lastLapNumber = data.Lap;
            }

            _lastFuelLevel = currentFuel;

            // Update fuel used last lap
            UsedLastLapText.Text = FormatFuelValue(_fuelUsedLastLap);

            // Calculate and display average fuel per lap
            double avgFuelPerLap = 0;
            if (_lapsCompleted > 0)
            {
                avgFuelPerLap = _totalFuelUsed / _lapsCompleted;
            }
            else if (_fuelUsedLastLap > 0)
            {
                // Use last lap as estimate if no average yet
                avgFuelPerLap = _fuelUsedLastLap;
            }

            AvgPerLapText.Text = FormatFuelValue(avgFuelPerLap);

            // Calculate laps remaining
            double lapsRemaining = 0;
            if (avgFuelPerLap > 0)
            {
                lapsRemaining = currentFuel / avgFuelPerLap;
            }

            LapsRemainingText.Text = $"{lapsRemaining:F1}";

            // Color code laps remaining based on urgency (Yellow > Orange > Red)
            if (lapsRemaining < 2.0)
            {
                LapsRemainingText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 51, 51)); // Red #FF3333
            }
            else if (lapsRemaining < 5.0)
            {
                LapsRemainingText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 128, 0)); // Orange #FF8000
            }
            else
            {
                LapsRemainingText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 255, 0)); // Yellow #FFFF00
            }
        }

        private string FormatFuelValue(double value)
        {
            if (AppSettings.Instance.UseMetric)
            {
                return $"{value:F2} L";
            }
            else
            {
                // Convert liters to gallons (1 L = 0.264172 gal)
                double gallons = value * 0.264172;
                return $"{gallons:F2} gal";
            }
        }

        /// <summary>
        /// Reset fuel calculations (useful when starting a new session)
        /// </summary>
        public void ResetCalculations()
        {
            _lastFuelLevel = 0;
            _fuelUsedLastLap = 0;
            _totalFuelUsed = 0;
            _lapsCompleted = 0;
            _lastLapNumber = -1;
        }
    }
}
