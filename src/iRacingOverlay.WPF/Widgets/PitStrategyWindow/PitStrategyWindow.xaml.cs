using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.PitStrategyWindow
{
    public partial class PitStrategyWindow : Window, INotifyPropertyChanged
    {
        private readonly FuelCalculatorService _fuelService;
        private FuelData? _currentData;
        private double _bufferLaps = 1.5;

        public ObservableCollection<TimelineLapItem> TimelineLaps { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public PitStrategyWindow(FuelCalculatorService fuelService)
        {
            InitializeComponent();
            DataContext = this;

            _fuelService = fuelService;
            TimelineLaps = new ObservableCollection<TimelineLapItem>();
            TimelineItemsControl.ItemsSource = TimelineLaps;

            // Subscribe to fuel data updates
            _fuelService.FuelDataUpdated += OnFuelDataUpdated;

            // Load saved position and size
            LoadWindowSettings();
        }

        private void OnFuelDataUpdated(object? sender, FuelData data)
        {
            Dispatcher.Invoke(() =>
            {
                _currentData = data;
                UpdateDisplay(data);
            });
        }

        private void UpdateDisplay(FuelData data)
        {
            // Update current strategy summary
            CurrentLapText.Text = $"{data.CurrentLap} / {data.SessionLaps}";
            FuelRemainingText.Text = $"{data.CurrentFuel:F1}L";
            LapsRemainingText.Text = $"({data.LapsRemaining:F1} laps)";
            
            OptimalPitText.Text = data.OptimalPitLap > 0 ? $"Lap {data.OptimalPitLap}" : "No pit needed";
            OptimalPitReasonText.Text = data.OptimalPitReason ?? string.Empty;
            
            // Update position and cost
            PositionText.Text = data.RacePosition > 0 ? $"P{data.RacePosition} / {data.TotalCars}" : "--";
            PositionCostText.Text = data.TrackPositionCost > 0 ? $"({data.TrackPositionCost:F0}s pit cost)" : string.Empty;

            // Update pit window visualization
            UpdatePitWindowVisualization(data);

            // Update multi-stint timeline
            UpdateTimeline(data);

            // Update what-if scenarios
            UpdateScenarios(data, _bufferLaps);

            // Update lap time projections
            UpdateProjections(data);
        }

        private void UpdatePitWindowVisualization(FuelData data)
        {
            if (data.RaceLapsRemaining <= 0 || data.CanFinishWithoutStop)
            {
                // Hide pit window if no pit stop needed
                OptimalWindowRect.Visibility = Visibility.Collapsed;
                EarliestLine.Visibility = Visibility.Collapsed;
                LatestLine.Visibility = Visibility.Collapsed;
                CurrentLapLine.Visibility = Visibility.Collapsed;
                OptimalPitMarker.Visibility = Visibility.Collapsed;
                EarliestLapLabel.Visibility = Visibility.Collapsed;
                OptimalLapLabel.Visibility = Visibility.Collapsed;
                LatestLapLabel.Visibility = Visibility.Collapsed;
                CurrentLapLabel.Visibility = Visibility.Collapsed;
                PitDeltaText.Text = "No pit needed";
                PitDeltaText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                return;
            }

            // Show pit window elements
            OptimalWindowRect.Visibility = Visibility.Visible;
            EarliestLine.Visibility = Visibility.Visible;
            LatestLine.Visibility = Visibility.Visible;
            CurrentLapLine.Visibility = Visibility.Visible;
            OptimalPitMarker.Visibility = Visibility.Visible;
            EarliestLapLabel.Visibility = Visibility.Visible;
            OptimalLapLabel.Visibility = Visibility.Visible;
            LatestLapLabel.Visibility = Visibility.Visible;
            CurrentLapLabel.Visibility = Visibility.Visible;

            // Calculate visualization positions (based on race length)
            int sessionLaps = data.SessionLaps > 0 ? data.SessionLaps : (int)Math.Ceiling((double)data.RaceLapsRemaining);
            if (sessionLaps == 0) return;

            int currentLap = data.CurrentLap;
            int earliestPit = data.EarliestPitLap;
            int latestPit = data.LatestPitLap;
            int optimalPit = data.OptimalPitLap;

            // Bar width (use ActualWidth, fallback to 400 if not rendered yet)
            double barWidth = OptimalWindowRect.ActualWidth > 0 ? OptimalWindowRect.ActualWidth : 400;

            // Calculate X positions as percentages
            double currentX = ((double)currentLap / sessionLaps) * barWidth;
            double earliestX = ((double)earliestPit / sessionLaps) * barWidth;
            double latestX = ((double)latestPit / sessionLaps) * barWidth;
            double optimalX = ((double)optimalPit / sessionLaps) * barWidth;

            // Position current lap line
            CurrentLapLine.Margin = new Thickness(currentX, 0, 0, 0);
            CurrentLapLabel.Margin = new Thickness(currentX + 2, 0, 0, 2);
            CurrentLapLabel.Text = $"NOW: L{currentLap}";

            // Position earliest/latest lines
            EarliestLine.Margin = new Thickness(earliestX, 0, 0, 0);
            EarliestLapLabel.Margin = new Thickness(earliestX + 2, 2, 0, 0);
            EarliestLapLabel.Text = $"L{earliestPit}";

            LatestLine.Margin = new Thickness(latestX, 0, 0, 0);
            LatestLapLabel.Margin = new Thickness(latestX + 2, 2, 0, 0);
            LatestLapLabel.Text = $"L{latestPit}";

            // Position optimal window (green rectangle)
            double windowWidth = latestX - earliestX;
            OptimalWindowRect.Margin = new Thickness(earliestX, 0, 0, 0);
            OptimalWindowRect.Width = Math.Max(10, windowWidth); // Min 10px width

            // Position optimal pit marker
            OptimalPitMarker.Margin = new Thickness(optimalX - 6, 0, 0, 0); // -6 for half width centering
            OptimalLapLabel.Text = $"L{optimalPit}";
            Canvas.SetLeft(OptimalLapLabel, optimalX - 15); // Center roughly

            // Calculate pit delta (pit now vs +5 laps)
            CalculatePitDelta(data);
        }

        private void CalculatePitDelta(FuelData data)
        {
            // Estimate time cost of pitting NOW vs waiting 5 laps
            // Factors: Track position cost, fuel weight savings, tire wear

            float positionCost = data.TrackPositionCost; // Seconds lost per pit stop at current position
            int currentLap = data.CurrentLap;
            int optimalPit = data.OptimalPitLap;
            float avgFuelPerLap = data.AvgFuelPerLap_L5 > 0 ? data.AvgFuelPerLap_L5 : data.AvgFuelPerLap_Session;

            // Base pit time cost (based on field position)
            float pitNowCost = positionCost;

            // If we pit 5 laps later:
            // - Fuel weight advantage: ~0.7kg/L × 5 laps × fuel/lap = lighter car (faster lap times)
            // - Position may change (could be better or worse)
            float fuelUsed5Laps = avgFuelPerLap * 5f;
            float fuelWeightSaving = fuelUsed5Laps * 0.7f; // kg
            float lapTimeAdvantage = fuelWeightSaving * 0.04f; // ~0.04s per kg lighter
            float totalLapTimeAdvantage = lapTimeAdvantage * 5f; // Over 5 laps

            // Pit 5 laps later has same position cost but gained time from lighter car
            float pitLaterCost = positionCost - totalLapTimeAdvantage;

            // Delta = pit later - pit now (positive = pitting later is better)
            float delta = pitLaterCost - pitNowCost;

            // Display delta
            if (Math.Abs(delta) < 0.5f)
            {
                PitDeltaText.Text = "~Even";
                PitDeltaText.Foreground = new SolidColorBrush(Color.FromRgb(169, 169, 169)); // Gray
            }
            else if (delta > 0)
            {
                // Pitting later is better (saves time)
                PitDeltaText.Text = $"+{delta:F1}s (wait)";
                PitDeltaText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            }
            else
            {
                // Pitting now is better
                PitDeltaText.Text = $"{delta:F1}s (pit now)";
                PitDeltaText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
            }
        }

        private void UpdateTimeline(FuelData data)
        {
            TimelineLaps.Clear();

            int sessionLaps = data.SessionLaps > 0 ? data.SessionLaps : (int)Math.Ceiling((double)data.RaceLapsRemaining);
            if (sessionLaps == 0) return;

            int currentLap = data.CurrentLap;
            int optimalPitLap = data.OptimalPitLap;

            for (int lap = 1; lap <= sessionLaps; lap++)
            {
                var item = new TimelineLapItem
                {
                    LapNumber = lap,
                    Width = 40,
                    IsPitStop = (lap == optimalPitLap),
                    Tooltip = $"Lap {lap}"
                };

                // Color coding
                if (lap < currentLap)
                {
                    // Completed laps - gray
                    item.Background = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                }
                else if (lap == currentLap)
                {
                    // Current lap - bright green
                    item.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    item.Tooltip += " (Current)";
                }
                else if (lap <= currentLap + data.LapsRemaining)
                {
                    // Current stint - green
                    item.Background = new SolidColorBrush(Color.FromRgb(56, 142, 60));
                }
                else
                {
                    // Next stint - blue
                    item.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                }

                // Yellow flag detection (placeholder - would need telemetry)
                // if (data.SessionFlags.Contains("Yellow"))
                // {
                //     item.Background = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                // }

                if (item.IsPitStop)
                {
                    item.Tooltip += " (Pit Stop)";
                }

                TimelineLaps.Add(item);
            }
        }

        private void UpdateScenarios(FuelData data, double bufferLaps)
        {
            // Calculate strategies with fuel buffer
            float avgFuelPerLap = data.AvgFuelPerLap_L5 > 0 ? data.AvgFuelPerLap_L5 : data.AvgFuelPerLap_Session;
            float lapsRemaining = data.RaceLapsRemaining;
            float currentFuel = data.CurrentFuel;
            float tankCapacity = data.TankCapacity;

            if (avgFuelPerLap <= 0 || lapsRemaining <= 0)
            {
                // Hide all strategies if no valid data
                OneStopPitLapText.Text = "--";
                TwoStopPitLapsText.Text = "--";
                ThreeStopPitLapsText.Text = "--";
                return;
            }

            // 1-Stop Strategy
            float fuelNeeded = (lapsRemaining + (float)bufferLaps) * avgFuelPerLap;
            float fuelToAdd = Math.Max(0, fuelNeeded - currentFuel);
            int oneStopPitLap = CalculateOptimalPitLap(data, fuelToAdd, 1);

            bool oneStopPossible = fuelToAdd <= tankCapacity;
            OneStopPitLapText.Text = oneStopPossible ? oneStopPitLap.ToString() : "N/A";
            OneStopFuelText.Text = oneStopPossible ? $"{fuelToAdd:F1}L" : "Tank too small";
            OneStopTimeText.Text = oneStopPossible ? $"~{EstimatePitTime(data, 1):F0}s" : "--";

            // 2-Stop Strategy
            float lapsPerStint2 = lapsRemaining / 2f;
            float fuelPerStint2 = (lapsPerStint2 + (float)bufferLaps / 2f) * avgFuelPerLap;
            int twoStopPit1 = CalculateOptimalPitLap(data, fuelPerStint2, 2);
            int twoStopPit2 = twoStopPit1 + (int)lapsPerStint2;

            bool twoStopPossible = fuelPerStint2 <= tankCapacity;
            TwoStopPitLapsText.Text = twoStopPossible ? $"{twoStopPit1}, {twoStopPit2}" : "N/A";
            TwoStopFuelText.Text = twoStopPossible ? $"{fuelPerStint2:F1}L" : "Tank too small";
            TwoStopTimeText.Text = twoStopPossible ? $"~{EstimatePitTime(data, 2):F0}s" : "--";

            // 3-Stop Strategy
            float lapsPerStint3 = lapsRemaining / 3f;
            float fuelPerStint3 = (lapsPerStint3 + (float)bufferLaps / 3f) * avgFuelPerLap;
            int threeStopPit1 = (int)(data.CurrentLap + lapsPerStint3);
            int threeStopPit2 = (int)(threeStopPit1 + lapsPerStint3);
            int threeStopPit3 = (int)(threeStopPit2 + lapsPerStint3);

            bool threeStopPossible = fuelPerStint3 <= tankCapacity;
            ThreeStopPitLapsText.Text = threeStopPossible ? $"{threeStopPit1}, {threeStopPit2}, {threeStopPit3}" : "N/A";
            ThreeStopFuelText.Text = threeStopPossible ? $"{fuelPerStint3:F1}L" : "Tank too small";
            ThreeStopTimeText.Text = threeStopPossible ? $"~{EstimatePitTime(data, 3):F0}s" : "--";

            // Recommend best strategy (1-stop is typically fastest if possible)
            OneStopRecommended.Visibility = oneStopPossible ? Visibility.Visible : Visibility.Collapsed;
            TwoStopRecommended.Visibility = (!oneStopPossible && twoStopPossible) ? Visibility.Visible : Visibility.Collapsed;
            ThreeStopRecommended.Visibility = (!oneStopPossible && !twoStopPossible && threeStopPossible) ? Visibility.Visible : Visibility.Collapsed;

            // Update undercut/overcut recommendations
            UpdatePositionStrategy(data);
        }

        private float EstimatePitTime(FuelData data, int numStops)
        {
            // Estimate total time lost to pit stops
            // Base pit time (entry + service + exit) = ~45s per stop
            // Adjusted by track position cost
            float basePitTime = 45f;
            float positionCost = data.TrackPositionCost > 0 ? data.TrackPositionCost : 15f;
            
            // Total pit time = (base time + position cost) × number of stops
            return (basePitTime + positionCost) * numStops;
        }

        private void UpdatePositionStrategy(FuelData data)
        {
            // Generate undercut/overcut recommendations based on position
            int position = data.RacePosition;
            int totalCars = data.TotalCars;
            float positionCost = data.TrackPositionCost;

            if (position <= 0 || totalCars <= 0)
            {
                UndercutText.Text = "Position data unavailable";
                OvercutText.Text = "";
                return;
            }

            float percentile = (float)position / totalCars;

            if (percentile <= 0.25f) // Top 25% - fighting for podium
            {
                // High position cost - stay out longer (overcut)
                UndercutText.Text = $"OVERCUT: High position value (P{position}) - extend stint for track position";
                OvercutText.Text = $"Stay out {CalculateOvercutLaps(data)} laps longer than leaders to maintain position";
            }
            else if (percentile <= 0.50f) // Mid-pack
            {
                // Moderate cost - flexible strategy
                UndercutText.Text = $"FLEXIBLE: Mid-pack (P{position}) - undercut slower cars ahead or overcut faster behind";
                OvercutText.Text = $"Pit 2-3 laps before/after nearby competitors for tire advantage";
            }
            else // Back half - aggressive undercut
            {
                // Low position cost - pit early (undercut)
                UndercutText.Text = $"UNDERCUT: Low position cost (P{position}) - pit early for fresh tire advantage";
                OvercutText.Text = $"Pit {CalculateUndercutLaps(data)} laps before car ahead (P{position - 1}) to gain position";
            }
        }

        private int CalculateUndercutLaps(FuelData data)
        {
            // Calculate how many laps before opponent to pit for undercut
            // Based on tire degradation and track characteristics
            // Typical range: 2-4 laps early
            float positionCost = data.TrackPositionCost;

            if (positionCost <= 10f) // Low time loss tracks
                return 2; // Shorter undercut window
            else if (positionCost <= 20f) // Moderate
                return 3; // Standard undercut
            else // High time loss
                return 4; // Longer undercut window
        }

        private int CalculateOvercutLaps(FuelData data)
        {
            // Calculate how many laps after leaders to pit for overcut
            // Gain track position by staying out on worn tires
            float positionCost = data.TrackPositionCost;

            if (positionCost >= 20f) // High value position
                return 5; // Stay out 5+ laps longer
            else if (positionCost >= 15f) // Moderate
                return 3; // Standard overcut
            else
                return 2; // Short overcut
        }

        private int CalculateOptimalPitLap(FuelData data, float fuelToAdd, int stopNumber)
        {
            float avgFuelPerLap = data.AvgFuelPerLap_L5 > 0 ? data.AvgFuelPerLap_L5 : data.AvgFuelPerLap_Session;
            if (avgFuelPerLap <= 0) return 0;

            float currentFuel = data.CurrentFuel;
            float lapsOnCurrentFuel = currentFuel / avgFuelPerLap;

            // For 1-stop: pit when fuel runs low
            if (stopNumber == 1)
            {
                int pitLap = data.CurrentLap + (int)Math.Floor(lapsOnCurrentFuel - 1); // Leave 1 lap buffer
                return Math.Max(data.CurrentLap + 1, pitLap);
            }
            // For 2-stop: pit earlier to balance stints
            else
            {
                float lapsRemaining = data.RaceLapsRemaining;
                int pit1Lap = data.CurrentLap + (int)(lapsRemaining / 3f);
                return Math.Max(data.CurrentLap + 1, pit1Lap);
            }
        }

        private void UpdateProjections(FuelData data)
        {
            // Stint 1: Current lap to pit lap
            float avgLapTime = data.AverageLapTime > 0 ? data.AverageLapTime : 90f; // Default 1:30 if unknown
            int optimalPitLap = data.OptimalPitLap > 0 ? data.OptimalPitLap : data.CurrentLap + (int)data.LapsRemaining;
            
            int stint1Laps = optimalPitLap - data.CurrentLap;
            Stint1AvgTimeText.Text = FormatLapTime(avgLapTime);
            
            // Stint 2: After pit to finish (lighter fuel, faster pace)
            float stint2AvgTime = avgLapTime * 0.98f; // ~2% faster with lighter fuel
            int stint2Laps = data.SessionLaps - optimalPitLap;
            Stint2AvgTimeText.Text = FormatLapTime(stint2AvgTime);

            // Total race time
            float stint1Time = stint1Laps * avgLapTime;
            float stint2Time = stint2Laps * stint2AvgTime;
            float pitStopTime = 45f; // Seconds
            float totalRaceTime = stint1Time + pitStopTime + stint2Time;

            TotalRaceTimeText.Text = FormatTotalTime(totalRaceTime);

            // Time margin to session end
            if (data.SessionTimeRemaining > 0)
            {
                float margin = (float)data.SessionTimeRemaining - totalRaceTime;
                TimeMarginText.Text = margin > 0 
                    ? $"(+{FormatTotalTime(margin)} to session end)" 
                    : $"({FormatTotalTime(Math.Abs(margin))} over limit)";
            }
            else
            {
                TimeMarginText.Text = string.Empty;
            }
        }

        private string FormatLapTime(float seconds)
        {
            if (seconds <= 0) return "--:--";
            
            int minutes = (int)(seconds / 60);
            float secs = seconds % 60;
            return $"{minutes}:{secs:00.0}";
        }

        private string FormatTotalTime(float seconds)
        {
            if (seconds <= 0) return "--:--:--";
            
            int hours = (int)(seconds / 3600);
            int minutes = (int)((seconds % 3600) / 60);
            int secs = (int)(seconds % 60);
            
            if (hours > 0)
                return $"{hours}:{minutes:00}:{secs:00}";
            else
                return $"{minutes}:{secs:00}";
        }

        private void BufferLapsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _bufferLaps = e.NewValue;
            BufferLapsText.Text = _bufferLaps.ToString("F1");

            if (_currentData != null)
            {
                UpdateScenarios(_currentData, _bufferLaps);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SaveWindowSettings();
            Hide(); // Don't close, just hide so we can reopen
        }

        private void LoadWindowSettings()
        {
            var settings = AppSettings.Instance;
            
            Left = settings.PitStrategyWindow_X;
            Top = settings.PitStrategyWindow_Y;
            Width = settings.PitStrategyWindow_Width;
            Height = settings.PitStrategyWindow_Height;
        }

        private void SaveWindowSettings()
        {
            var settings = AppSettings.Instance;
            
            settings.PitStrategyWindow_X = Left;
            settings.PitStrategyWindow_Y = Top;
            settings.PitStrategyWindow_Width = Width;
            settings.PitStrategyWindow_Height = Height;
            
            settings.Save();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Don't actually close, just hide
            e.Cancel = true;
            SaveWindowSettings();
            Hide();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Unsubscribe from events
            _fuelService.FuelDataUpdated -= OnFuelDataUpdated;
            base.OnClosed(e);
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Timeline lap item model
    public class TimelineLapItem : INotifyPropertyChanged
    {
        private int _lapNumber;
        private Brush? _background;
        private double _width;
        private bool _isPitStop;
        private string? _tooltip;

        public int LapNumber
        {
            get => _lapNumber;
            set { _lapNumber = value; OnPropertyChanged(); }
        }

        public Brush? Background
        {
            get => _background;
            set { _background = value; OnPropertyChanged(); }
        }

        public double Width
        {
            get => _width;
            set { _width = value; OnPropertyChanged(); }
        }

        public bool IsPitStop
        {
            get => _isPitStop;
            set { _isPitStop = value; OnPropertyChanged(); }
        }

        public string? Tooltip
        {
            get => _tooltip;
            set { _tooltip = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
