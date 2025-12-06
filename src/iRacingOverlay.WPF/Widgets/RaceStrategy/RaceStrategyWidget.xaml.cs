using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.Core.Services.Tire;
using iRacingOverlay.Core.Services.ValueSmoothing;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Controls;
using Microsoft.Extensions.Logging;

namespace iRacingOverlay.WPF.Widgets.RaceStrategy
{
    /// <summary>
    /// Phase 10: Comprehensive Race Strategy Widget
    /// 800x600 command center for multi-stint planning, what-if scenarios, and position tracking
    /// </summary>
    public partial class RaceStrategyWidget : Window
    {
        private readonly FuelCalculatorService _fuelCalculator;
        private readonly TireStrategyService _tireStrategy;
        private readonly CompetitorIntelligenceService _competitorIntelligence;
        private readonly WeatherTrackService _weatherTrack;
        private readonly PitIntelligenceService _pitIntelligence;
        private readonly ITelemetryService? _telemetryService;
        private readonly ValueSmoothingService _valueSmoothing;
        private readonly ILogger<RaceStrategyWidget> _logger; // Anti-flicker smoothing for gap values
        // REMOVED: Drag functionality not implemented yet
        // private bool _isDragging;
        // private Point _dragStartPoint;
        private TelemetryData? _latestTelemetry; // Store latest telemetry for competitor intelligence
        private DateTime _lastUIUpdate = DateTime.MinValue;
        private const int UI_UPDATE_THROTTLE_MS = 500; // Update UI max once per 500ms
        
        // Performance: Cache competitor intelligence to avoid expensive LINQ queries every 500ms
        private int _lastCompetitorUpdateLap = -1;

        public RaceStrategyWidget(FuelCalculatorService fuelCalculator, TireStrategyService tireStrategy, ILogger<RaceStrategyWidget> logger, ITelemetryService? telemetryService = null)
        {
            _fuelCalculator = fuelCalculator ?? throw new ArgumentNullException(nameof(fuelCalculator));
            _tireStrategy = tireStrategy ?? throw new ArgumentNullException(nameof(tireStrategy));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _competitorIntelligence = new CompetitorIntelligenceService();
            _weatherTrack = new WeatherTrackService();
            _pitIntelligence = new PitIntelligenceService();
            _telemetryService = telemetryService;
            _valueSmoothing = new ValueSmoothingService(); // Initialize smoothing service for gap values

            InitializeComponent();

            // Subscribe to data updates
            _fuelCalculator.FuelDataUpdated += OnFuelDataUpdated;
            
            // Subscribe to telemetry updates for competitor intelligence
            if (_telemetryService != null)
            {
                _telemetryService.TelemetryUpdated += OnTelemetryUpdated;
            }

            // Apply saved position and size
            LoadWindowState();

            // Handle ESC key to close
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    Close();
            };

            // Initial update
            UpdateUI(_fuelCalculator.CurrentData);
        }

        /// <summary>
        /// Load window position and size from settings
        /// </summary>
        private void LoadWindowState()
        {
            var settings = AppSettings.Instance;
            
            Left = settings.PitStrategyWindow_X;
            Top = settings.PitStrategyWindow_Y;
            Width = settings.PitStrategyWindow_Width;
            Height = settings.PitStrategyWindow_Height;
        }

        /// <summary>
        /// Save window position and size to settings
        /// </summary>
        private void SaveWindowState()
        {
            var settings = AppSettings.Instance;
            
            settings.PitStrategyWindow_X = Left;
            settings.PitStrategyWindow_Y = Top;
            settings.PitStrategyWindow_Width = Width;
            settings.PitStrategyWindow_Height = Height;
            
            settings.Save();
        }

        /// <summary>
        /// Calculate time gap to a specific position using telemetry arrays
        /// Returns gap in seconds, or -1 if calculation not possible
        /// </summary>
        private float CalculateGapToPosition(TelemetryData? data, int targetPosition)
        {
            if (data == null || targetPosition < 1 || data.CarIdxPosition == null || data.CarIdxLapDistPct == null)
                return -1f;

            // Find car in target position
            int targetCarIdx = -1;
            for (int i = 0; i < data.CarIdxPosition.Length; i++)
            {
                if (data.CarIdxPosition[i] == targetPosition)
                {
                    targetCarIdx = i;
                    break;
                }
            }

            if (targetCarIdx == -1 || data.PlayerCarIdx < 0)
                return -1f;

            // Get lap distance percentages
            float playerPct = data.LapDistPct;
            float targetPct = data.CarIdxLapDistPct[targetCarIdx];

            // Calculate distance difference (in track percentage)
            float distDiff = Math.Abs(playerPct - targetPct);

            // Convert to time using last lap time as estimate
            if (data.LapLastLapTime > 0)
            {
                return distDiff * data.LapLastLapTime;
            }

            return -1f;
        }

        /// <summary>
        /// Handle fuel data updates (throttled for performance)
        /// </summary>
        private void OnFuelDataUpdated(object? sender, FuelData data)
        {
            // Throttle updates to prevent excessive Dispatcher.Invoke calls
            var now = DateTime.UtcNow;
            if ((now - _lastUIUpdate).TotalMilliseconds < UI_UPDATE_THROTTLE_MS)
                return;
            
            _lastUIUpdate = now;
            Dispatcher.Invoke(() => UpdateUI(data));
        }

        /// <summary>
        /// Handle telemetry updates for competitor intelligence (throttled for performance)
        /// </summary>
        private void OnTelemetryUpdated(object? sender, TelemetryData data)
        {
            _latestTelemetry = data;
            
            // Update weather tracking (lightweight, no UI)
            _weatherTrack.Update(data);
            
            // Throttle UI updates to prevent performance issues
            var now = DateTime.UtcNow;
            if ((now - _lastUIUpdate).TotalMilliseconds < UI_UPDATE_THROTTLE_MS)
                return;
            
            // Update pit activity tracking with throttled UI updates
            Dispatcher.Invoke(() =>
            {
                if (_latestTelemetry != null)
                {
                    _competitorIntelligence.UpdatePitActivity(_latestTelemetry, _latestTelemetry.Lap);
                    UpdateCompetitorIntelligence();
                    UpdateWeatherAndConditions();
                }
            });
        }

        /// <summary>
        /// Update UI with latest data
        /// </summary>
        private void UpdateUI(FuelData data)
        {
            // Status Bar
            // For time-based sessions, show estimated end lap instead of infinity
            // FIX: Fixed Laps ALWAYS Trump Estimates
            // RULE: If SessionLaps > 0, ALWAYS use it (never fall through to time estimates)
            if (data.SessionLaps > 0)
            {
                // Lap-based session: show actual total laps (fixed, not estimated)
                CurrentLapText.Text = $"{data.CurrentLap} / {data.SessionLaps}";
                // LogDebug: "Using fixed session laps"
            }
            else if (!data.IsTimedSession && data.RaceLapsRemaining > 0 && data.RaceLapsRemaining < 2000)
            {
                // Lap-based session but SessionLaps not yet available
                // Use RaceLapsRemaining from FuelCalculatorService (calculated from leader pace)
                int estimatedEndLap = data.CurrentLap + data.RaceLapsRemaining;
                CurrentLapText.Text = $"{data.CurrentLap} / ~{estimatedEndLap}";
                // LogDebug: "Using RaceLapsRemaining (SessionLaps not yet set)"
            }
            else if (data.IsTimedSession)
            {
                // Time-based session: calculate from time remaining and leader pace
                // Early in session (lap 0-1), no valid data yet
                if (data.CurrentLap <= 1 || data.AverageLapTime <= 0)
                {
                    CurrentLapText.Text = $"{data.CurrentLap} / ~?";
                }
                else if (data.RaceLapsRemaining > 0 && data.RaceLapsRemaining < 2000)
                {
                    // Valid RaceLapsRemaining from FuelCalculatorService (uses leader pace)
                    int estimatedEndLap = data.CurrentLap + data.RaceLapsRemaining;
                    CurrentLapText.Text = $"{data.CurrentLap} / ~{estimatedEndLap}";
                }
                else if (data.SessionTimeRemaining > 0 && data.AverageLapTime > 0)
                {
                    // Fallback: Calculate from time remaining and average lap time
                    int estimatedRemainingLaps = (int)(data.SessionTimeRemaining / data.AverageLapTime);
                    int estimatedEndLap = data.CurrentLap + estimatedRemainingLaps;
                    CurrentLapText.Text = $"{data.CurrentLap} / ~{estimatedEndLap}";
                }
                else
                {
                    // No valid data yet
                    CurrentLapText.Text = $"{data.CurrentLap} / ~?";
                }
            }
            else
            {
                // Practice/Qualify/Unknown session - show infinity
                CurrentLapText.Text = $"{data.CurrentLap} / ∞";
            }

            if (data.RacePosition > 0 && data.TotalCars > 0)
                PositionText.Text = $"P{data.RacePosition} / {data.TotalCars}";
            else
                PositionText.Text = "--";

            FuelRemainingText.Text = FormatFuel(data.CurrentFuel);

            // Tire life (placeholder - will be integrated with TireStrategyService)
            TireLifeText.Text = "--";

            // Overview Tab
            if (data.PitWindowStart > 0 && data.PitWindowEnd > 0)
                PitWindowOverviewText.Text = $"L{data.PitWindowStart}-{data.PitWindowEnd} (opt L{data.OptimalPitLap})";
            else
                PitWindowOverviewText.Text = "--";

            // Laps on Fuel (driver's fuel capacity) - CRITICAL distinction from RaceLapsRemaining
            if (data.LapsRemaining > 0)
            {
                int lapsOnFuel = (int)Math.Floor(data.LapsRemaining);
                LapsOnFuelText.Text = $"{lapsOnFuel} laps";
                
                // Color code by urgency relative to race end
                if (data.RaceLapsRemaining > 0 && data.RaceLapsRemaining < 2000)
                {
                    int shortfall = data.RaceLapsRemaining - lapsOnFuel;
                    if (shortfall > 5)
                        LapsOnFuelText.Foreground = (System.Windows.Media.Brush)FindResource("BrushRed");  // Critical: need pit
                    else if (shortfall > 0)
                        LapsOnFuelText.Foreground = (System.Windows.Media.Brush)FindResource("BrushOrange");  // Warning: tight
                    else
                        LapsOnFuelText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen");  // Safe: can finish
                }
                else
                {
                    LapsOnFuelText.Foreground = (System.Windows.Media.Brush)FindResource("BrushTeal");  // Default
                }
            }
            else
                LapsOnFuelText.Text = "--";

            if (data.FuelToAddAtPit > 0)
                FuelAtPitText.Text = $"Add {FormatFuel(data.FuelToAddAtPit)}";
            else if (data.CanFinishWithoutStop)
                FuelAtPitText.Text = "No pit required";
            else
                FuelAtPitText.Text = "--";

            // Strategy description (placeholder for multi-stop comparison)
            if (data.CanFinishWithoutStop)
                StrategyDescriptionText.Text = "0-stop: Can finish on current fuel";
            else if (data.OptimalPitLap > 0)
                StrategyDescriptionText.Text = $"1-stop recommended at lap {data.OptimalPitLap}";
            else
                StrategyDescriptionText.Text = "Calculating optimal strategy...";

            // Status indicator
            if (data.HasSufficientData)
            {
                StatusText.Text = "● Active";
                StatusText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen");
            }
            else
            {
                StatusText.Text = "● Warming up";
                StatusText.Foreground = (System.Windows.Media.Brush)FindResource("BrushOrange");
            }

            // Last update timestamp
            LastUpdateText.Text = $"Last update: {data.LastUpdate:HH:mm:ss}";
            
            // Update stint timeline (Phase 10.2)
            UpdateStintTimeline(data);
            
            // Update what-if scenarios (Phase 10.3)
            UpdateScenarios(data);
            
            // Update position tracking (Phase 10.4)
            UpdatePositionTracking(data);
        }
        
        /// <summary>
        /// Calculate estimated total laps for the session
        /// For lap-based sessions: uses SessionLaps
        /// For time-based sessions: uses current lap + estimated remaining laps from leader pace
        /// FIX: Improved early-session handling - don't return estimates until we have valid data
        /// </summary>
        private int GetEstimatedTotalLaps(FuelData data)
        {
            if (data.SessionLaps > 0)
            {
                // Lap-based session: use actual total laps
                return data.SessionLaps;
            }
            else if (data.IsTimedSession && data.CurrentLap > 1)
            {
                // FIX: Time-based session - only estimate if we have valid lap data
                // Require lap 2+ to have established pace
                if (data.RaceLapsRemaining > 0 && data.RaceLapsRemaining < 2000)
                {
                    // Valid RaceLapsRemaining from FuelCalculatorService (uses leader pace)
                    return data.CurrentLap + data.RaceLapsRemaining;
                }
                else if (data.SessionTimeRemaining > 0 && data.AverageLapTime > 0)
                {
                    // Fallback: Calculate from time remaining and average lap time
                    int estimatedRemainingLaps = (int)(data.SessionTimeRemaining / data.AverageLapTime);
                    return data.CurrentLap + estimatedRemainingLaps;
                }
            }
            
            // Fallback: return reasonable minimum (don't try to estimate without data)
            return Math.Max(20, data.CurrentLap + 10);
        }

        /// <summary>
        /// Update stint timeline with projections
        /// </summary>
        private void UpdateStintTimeline(FuelData data)
        {
            int totalLaps = GetEstimatedTotalLaps(data);
            var stints = GenerateStintProjections(data, totalLaps);

            StintTimeline.UpdateTimeline(data, totalLaps, stints);
        }
        
        /// <summary>
        /// Generate stint projections based on current fuel/tire data
        /// </summary>
        private List<StintProjection> GenerateStintProjections(FuelData data, int totalLaps)
        {
            var stints = new List<StintProjection>();
            
            if (!data.HasSufficientData || data.CurrentLap < 2)
                return stints;
            
            // Calculate fuel consumption per lap with weather adjustments
            float baseFuelPerLap = data.AvgFuelPerLap > 0 ? data.AvgFuelPerLap : 2.5f;
            
            // FIX: Rain effects - LESS fuel per lap (slower pace means less throttle)
            // Rain = slower lap times = reduced fuel consumption (typically 5-15% less)
            // Also better tire degradation in rain (cooler temps, less aggressive)
            float weatherFuelMultiplier = GetWeatherFuelMultiplier();
            float fuelPerLap = baseFuelPerLap * weatherFuelMultiplier;
            
            float currentFuel = data.CurrentFuel;
            int currentLap = data.CurrentLap;
            
            // Estimate tire wear with weather effects
            // Rain = BETTER tire life (cooler temps, less aggressive driving)
            float baseTireWearPerLap = 1.5f; // Base 1.5% wear per lap
            float weatherTireMultiplier = GetWeatherTireMultiplier();
            float tireWearPerLap = baseTireWearPerLap * weatherTireMultiplier;
            float currentTireLife = 100f; // Start at 100% for now
            
            int stintNumber = 1;
            
            while (currentLap < totalLaps)
            {
                var stint = new StintProjection
                {
                    StintNumber = stintNumber,
                    StartLap = currentLap,
                    StartFuelPercent = Math.Min(100, (currentFuel / 100f) * 100), // Assume 100L tank
                    StartTirePercent = currentTireLife
                };
                
                // Calculate how far we can go on current fuel
                int lapsOnFuel = (int)(currentFuel / fuelPerLap);
                
                // Calculate how far we can go on current tires (assume 35 laps max on a set)
                int lapsOnTires = (int)((100 - currentTireLife) / tireWearPerLap);
                if (lapsOnTires <= 0) lapsOnTires = 35; // Fresh tires
                
                // Stint ends at the earlier of: fuel empty, tires worn, or race end
                int stintLength = Math.Min(lapsOnFuel, Math.Min(lapsOnTires, totalLaps - currentLap));
                
                // If we can finish on current stint, extend to race end
                if (currentLap + stintLength >= totalLaps)
                {
                    stint.EndLap = totalLaps;
                    stint.EndFuelPercent = Math.Max(0, stint.StartFuelPercent - (totalLaps - currentLap) * (fuelPerLap / 100f) * 100);
                    stint.EndTirePercent = Math.Max(0, currentTireLife - (totalLaps - currentLap) * tireWearPerLap);
                    stint.IsPitStopPlanned = false;
                    stints.Add(stint);
                    break;
                }
                
                // Plan pit stop
                stint.EndLap = currentLap + stintLength;
                stint.EndFuelPercent = 5; // Leave 5% safety margin
                stint.EndTirePercent = Math.Max(0, currentTireLife - stintLength * tireWearPerLap);
                stint.IsPitStopPlanned = true;
                stints.Add(stint);
                
                // Next stint starts after pit
                currentLap = stint.EndLap;
                currentFuel = 100f; // Assume full refuel
                currentTireLife = 100f; // Fresh tires
                stintNumber++;
                
                // Safety: prevent infinite loop
                if (stintNumber > 10)
                    break;
            }
            
            return stints;
        }

        /// <summary>
        /// Format fuel value with unit
        /// </summary>
        private string FormatFuel(float value)
        {
            bool useMetric = AppSettings.Instance.UseMetricUnits;
            if (useMetric)
                return $"{value:F1}L";
            else
                return $"{value * 0.264172f:F2}gal";
        }
        
        /// <summary>
        /// Update what-if scenario calculations (Phase 10.3)
        /// </summary>
        private void UpdateScenarios(FuelData data)
        {
            if (!data.HasSufficientData || data.CurrentLap < 2)
                return;

            // Use intelligent total laps calculation (handles time-based sessions)
            int totalLaps = GetEstimatedTotalLaps(data);
            int remainingLaps = Math.Max(0, totalLaps - data.CurrentLap);
            
            // Calculate base fuel per lap from averages
            float baseFuelPerLap = data.AvgFuelPerLap > 0 ? data.AvgFuelPerLap : 2.5f;
            
            // FIX: Apply weather effects - Rain = LESS fuel per lap (slower pace)
            // Scientific basis: Slower lap times = reduced fuel burn rate
            float weatherFuelMultiplier = GetWeatherFuelMultiplier();
            float fuelPerLap = baseFuelPerLap * weatherFuelMultiplier;
            
            float currentFuel = data.CurrentFuel;

            // Early exit if race is nearly over (less than 2 laps remaining)
            if (remainingLaps < 2)
            {
                // Show only NO-STOP when race is ending
                UpdateNoStopOnly(data);
                return;
            }

            // Calculate all potential strategies dynamically
            var strategies = new List<ScenarioResult>();

            // NO-STOP: Always calculate (might be feasible)
            var noStop = CalculateNoStopStrategy(data, remainingLaps, fuelPerLap, currentFuel);
            strategies.Add(noStop);

            // 1-STOP: Calculate if we have enough laps remaining
            ScenarioResult? oneStopResult = null;
            if (remainingLaps >= 5) // Need at least 5 laps for meaningful 1-stop
            {
                oneStopResult = CalculateOneStopStrategy(data, remainingLaps, fuelPerLap, currentFuel, totalLaps);
                strategies.Add(oneStopResult);
            }

            // 2-STOP: Only calculate if race is long enough
            float tankCapacity = data.TankCapacity > 0 ? data.TankCapacity : 100f;
            int maxLapsPerTank = (int)(tankCapacity / fuelPerLap);

            ScenarioResult? twoStopResult = null;
            if (remainingLaps >= maxLapsPerTank * 1.5f) // Need at least 1.5x tank range for 2-stop
            {
                twoStopResult = CalculateTwoStopStrategy(data, remainingLaps, fuelPerLap, totalLaps);
                strategies.Add(twoStopResult);
            }

            // 3-STOP: Only for very long races (rare)
            ScenarioResult? threeStopResult = null;
            if (remainingLaps >= maxLapsPerTank * 2.5f) // Need at least 2.5x tank range for 3-stop
            {
                threeStopResult = CalculateThreeStopStrategy(data, remainingLaps, fuelPerLap, totalLaps);
                strategies.Add(threeStopResult);
            }

            // Determine optimal (lowest time) from calculated strategies
            float minTime = strategies.Count > 0 ? strategies.Min(s => s.TimeDelta) : 0;

            // Update UI - NO-STOP (always shown)
            UpdateNoStopUI(noStop, minTime);

            // Update UI - 1-STOP (use calculated or default values)
            if (oneStopResult != null)
            {
                UpdateOneStopUI(oneStopResult, minTime);
            }
            else
            {
                // Show placeholder for unavailable strategy
                var placeholderOneStop = new ScenarioResult
                {
                    PitLap1 = data.CurrentLap + 5,
                    FuelToAdd1 = 0,
                    TotalPitTime = 0,
                    TimeDelta = 999f,
                    Notes = "Not applicable for this race length"
                };
                UpdateOneStopUI(placeholderOneStop, minTime);
            }

            // Update UI - 2-STOP
            if (twoStopResult != null)
            {
                UpdateTwoStopUI(twoStopResult, minTime);
            }
            else
            {
                var placeholderTwoStop = new ScenarioResult
                {
                    PitLap1 = data.CurrentLap + 3,
                    PitLap2 = data.CurrentLap + 6,
                    FuelToAdd1 = 0,
                    TotalPitTime = 0,
                    TimeDelta = 999f,
                    Notes = "Not applicable for this race length"
                };
                UpdateTwoStopUI(placeholderTwoStop, minTime);
            }

            // Update UI - 3-STOP
            if (threeStopResult != null)
            {
                UpdateThreeStopUI(threeStopResult, minTime);
            }
            else
            {
                var placeholderThreeStop = new ScenarioResult
                {
                    PitLap1 = data.CurrentLap + 2,
                    PitLap2 = data.CurrentLap + 4,
                    PitLap3 = data.CurrentLap + 6,
                    FuelToAdd1 = 0,
                    TotalPitTime = 0,
                    TimeDelta = 999f,
                    Notes = "Not applicable for this race length"
                };
                UpdateThreeStopUI(placeholderThreeStop, minTime);
            }
        }

        private void UpdateNoStopOnly(FuelData data)
        {
            // Simple display when race is ending - only show NO-STOP
            var noStop = new ScenarioResult
            {
                IsFeasible = true,
                TimeDelta = 0,
                Notes = "Race ending - finish with current fuel"
            };

            UpdateNoStopUI(noStop, 0);

            // Show placeholders for other strategies
            var placeholder = new ScenarioResult
            {
                PitLap1 = data.CurrentLap,
                TimeDelta = 999f,
                Notes = "Race ending soon"
            };
            UpdateOneStopUI(placeholder, 0);
            UpdateTwoStopUI(placeholder, 0);
            UpdateThreeStopUI(placeholder, 0);
        }

        private void UpdateNoStopUI(ScenarioResult noStop, float minTime)
        {
            NoStopTime.Text = FormatTimeDelta(noStop.TimeDelta, minTime);
            NoStopTime.Foreground = GetTimeDeltaBrush(noStop.TimeDelta, minTime);
            NoStopFuelSaving.Text = noStop.IsFeasible ? $"Required: {noStop.FuelSavingRequired:F2}L/lap" : "Not feasible";
            NoStopFeasibility.Text = noStop.IsFeasible ? "Possible" : "Not possible";
            NoStopFeasibility.Foreground = (System.Windows.Media.Brush)FindResource(noStop.IsFeasible ? "BrushGreen" : "BrushRed");
            NoStopNotes.Text = noStop.Notes;
            NoStopBadge.Visibility = (noStop.TimeDelta == minTime) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateOneStopUI(ScenarioResult oneStop, float minTime)
        {
            OneStopTime.Text = FormatTimeDelta(oneStop.TimeDelta, minTime);
            OneStopTime.Foreground = GetTimeDeltaBrush(oneStop.TimeDelta, minTime);
            OneStopLap.Text = $"L{oneStop.PitLap1}";
            OneStopFuel.Text = FormatFuel(oneStop.FuelToAdd1);
            OneStopPitTime.Text = $"{oneStop.TotalPitTime:F1}s";
            OneStopNotes.Text = oneStop.Notes;
            OneStopBadge.Visibility = (oneStop.TimeDelta == minTime) ? Visibility.Visible : Visibility.Collapsed;
            if (oneStop.TimeDelta == minTime)
                OneStopBadge.Text = "OPTIMAL";
        }

        private void UpdateTwoStopUI(ScenarioResult twoStop, float minTime)
        {
            TwoStopTime.Text = FormatTimeDelta(twoStop.TimeDelta, minTime);
            TwoStopTime.Foreground = GetTimeDeltaBrush(twoStop.TimeDelta, minTime);
            TwoStopLaps.Text = $"L{twoStop.PitLap1}, L{twoStop.PitLap2}";
            TwoStopFuel.Text = $"{FormatFuel(twoStop.FuelToAdd1)} each";
            TwoStopPitTime.Text = $"{twoStop.TotalPitTime:F1}s";
            TwoStopNotes.Text = twoStop.Notes;
        }

        private void UpdateThreeStopUI(ScenarioResult threeStop, float minTime)
        {
            ThreeStopTime.Text = FormatTimeDelta(threeStop.TimeDelta, minTime);
            ThreeStopTime.Foreground = GetTimeDeltaBrush(threeStop.TimeDelta, minTime);
            ThreeStopLaps.Text = $"L{threeStop.PitLap1}, L{threeStop.PitLap2}, L{threeStop.PitLap3}";
            ThreeStopFuel.Text = $"{FormatFuel(threeStop.FuelToAdd1)} each";
            ThreeStopPitTime.Text = $"{threeStop.TotalPitTime:F1}s";
            ThreeStopNotes.Text = threeStop.Notes;
        }
        
        private ScenarioResult CalculateNoStopStrategy(FuelData data, int remainingLaps, float fuelPerLap, float currentFuel)
        {
            float fuelNeeded = remainingLaps * fuelPerLap;
            float deficit = fuelNeeded - currentFuel;
            
            // FIX: Check tank capacity for endurance races
            // If fuel needed exceeds tank capacity, NO-STOP is impossible (need pit stop to refuel)
            float tankCapacity = data.TankCapacity > 0 ? data.TankCapacity : 100f;
            bool exceedsTankCapacity = fuelNeeded > tankCapacity;
            
            // FIX: Use realistic fuel saving capability
            // Maximum achievable fuel saving: 10% (conservative driver technique)
            // Extreme maximum: 15% (professional-level lift-and-coast, short-shifting, lean mixture)
            // Conservative approach: Use 10% as reliable threshold for NO-STOP feasibility
            // Reference: FuelCalculatorService uses 15% as absolute max with "Can Save Fuel To Finish" flag
            float maxFuelSavingPercent = 0.10f; // 10% realistic for most drivers
            bool canSaveFuel = deficit > 0 && (deficit / remainingLaps) <= (fuelPerLap * maxFuelSavingPercent);
            
            // Feasible if: (no deficit OR can save enough fuel) AND doesn't exceed tank capacity
            bool feasible = (deficit <= 0 || canSaveFuel) && !exceedsTankCapacity;
            
            string notes;
            if (exceedsTankCapacity)
                notes = $"Impossible: Need {fuelNeeded:F1}L but tank capacity is {tankCapacity:F1}L. Must pit.";
            else if (feasible && deficit > 0)
            {
                float savingRequired = deficit / remainingLaps;
                float savingPercent = (savingRequired / fuelPerLap) * 100f;
                notes = $"Requires {savingPercent:F1}% fuel saving ({savingRequired:F2}L/lap). Lift-and-coast, short-shift.";
            }
            else if (feasible)
                notes = "No fuel saving required. Maintain pace.";
            else
            {
                float savingRequired = deficit / remainingLaps;
                float savingPercent = (savingRequired / fuelPerLap) * 100f;
                notes = $"Impossible: Requires {savingPercent:F1}% fuel saving (max {maxFuelSavingPercent * 100}%). Must pit.";
            }
            
            return new ScenarioResult
            {
                IsFeasible = feasible,
                FuelSavingRequired = feasible && deficit > 0 ? deficit / remainingLaps : 0,
                TimeDelta = feasible ? 0 : 999.9f,
                Notes = notes
            };
        }
        
        private ScenarioResult CalculateOneStopStrategy(FuelData data, int remainingLaps, float fuelPerLap, float currentFuel, int totalLaps)
        {
            // Optimal pit lap is already calculated by FuelCalculatorService
            int pitLap = data.OptimalPitLap > 0 ? data.OptimalPitLap : data.CurrentLap + remainingLaps / 2;
            int lapsUntilPit = pitLap - data.CurrentLap;
            int lapsAfterPit = totalLaps - pitLap;
            
            float fuelToAdd = lapsAfterPit * fuelPerLap;
            float pitTime = 45.0f + (fuelToAdd / 10f) * 2.5f;
            
            return new ScenarioResult
            {
                IsFeasible = true,
                PitLap1 = pitLap,
                FuelToAdd1 = fuelToAdd,
                TotalPitTime = pitTime,
                TimeDelta = 0, // Baseline
                Notes = "Best balance of speed and consistency. Minimal fuel saving required."
            };
        }
        
        private ScenarioResult CalculateTwoStopStrategy(FuelData data, int remainingLaps, float fuelPerLap, int totalLaps)
        {
            // Split race into thirds
            int lap1 = data.CurrentLap + remainingLaps / 3;
            int lap2 = data.CurrentLap + (remainingLaps * 2 / 3);
            
            float fuelPerStop = (remainingLaps / 3f + 2) * fuelPerLap; // Slight buffer
            float pitTime1 = 45.0f + (fuelPerStop / 10f) * 2.5f;
            float pitTime2 = 45.0f + (fuelPerStop / 10f) * 2.5f;
            float totalPitTime = pitTime1 + pitTime2;
            
            // Penalty: extra pit stop time minus potential tire advantage
            float timeDelta = 45.0f - 5.0f; // Assume 5s lap time gain from fresher tires
            
            return new ScenarioResult
            {
                IsFeasible = true,
                PitLap1 = lap1,
                PitLap2 = lap2,
                FuelToAdd1 = fuelPerStop,
                TotalPitTime = totalPitTime,
                TimeDelta = timeDelta,
                Notes = "More flexibility but slower overall. Consider for tire management."
            };
        }
        
        private ScenarioResult CalculateThreeStopStrategy(FuelData data, int remainingLaps, float fuelPerLap, int totalLaps)
        {
            // Split race into quarters
            int lap1 = data.CurrentLap + remainingLaps / 4;
            int lap2 = data.CurrentLap + (remainingLaps / 2);
            int lap3 = data.CurrentLap + (remainingLaps * 3 / 4);
            
            float fuelPerStop = (remainingLaps / 4f + 2) * fuelPerLap;
            float pitTimeEach = 45.0f + (fuelPerStop / 10f) * 2.5f;
            float totalPitTime = pitTimeEach * 3;
            
            // Penalty: two extra pit stops minus tire advantage
            float timeDelta = 90.0f - 12.0f; // Assume 12s total lap time gain from tires
            
            return new ScenarioResult
            {
                IsFeasible = true,
                PitLap1 = lap1,
                PitLap2 = lap2,
                PitLap3 = lap3,
                FuelToAdd1 = fuelPerStop,
                TotalPitTime = totalPitTime,
                TimeDelta = timeDelta,
                Notes = "Not recommended for this race. Only viable with multiple cautions."
            };
        }
        
        private string FormatTimeDelta(float delta, float minTime)
        {
            if (delta >= 999f)
                return "N/A";
            
            float relative = delta - minTime;
            if (Math.Abs(relative) < 0.1f)
                return "BEST";
                
            return $"+ {relative:F1}s";
        }
        
        private System.Windows.Media.Brush GetTimeDeltaBrush(float delta, float minTime)
        {
            if (delta >= 999f)
                return (System.Windows.Media.Brush)FindResource("BrushGray");
                
            float relative = delta - minTime;
            if (Math.Abs(relative) < 0.1f)
                return (System.Windows.Media.Brush)FindResource("BrushTeal");
            else if (relative < 20f)
                return (System.Windows.Media.Brush)FindResource("BrushOrange");
            else
                return (System.Windows.Media.Brush)FindResource("BrushRed");
        }
        
        /// <summary>
        /// Update position tracking and pit exit predictions (Phase 10.4)
        /// </summary>
        private void UpdatePositionTracking(FuelData data)
        {
            if (!data.HasSufficientData)
            {
                YourPositionText.Text = "--";
                PositionChangeText.Text = "Waiting for data...";
                GapAheadText.Text = "--";
                GapBehindText.Text = "--";
                CarAheadText.Text = "";
                CarBehindText.Text = "";
                return;
            }
            
            // Your current position
            if (data.RacePosition > 0 && data.TotalCars > 0)
            {
                YourPositionText.Text = $"P{data.RacePosition}";
                
                // Position change tracking requires race start position history (future enhancement)
                PositionChangeText.Text = "Monitoring";
                PositionChangeText.Foreground = (System.Windows.Media.Brush)FindResource("BrushTeal");
            }
            else
            {
                YourPositionText.Text = "--";
                PositionChangeText.Text = "Not in race";
            }
            
            // Gap ahead (calculated from telemetry)
            var gapAhead = CalculateGapToPosition(_latestTelemetry, data.RacePosition - 1);
            if (data.RacePosition > 1 && gapAhead >= 0)
            {
                GapAheadText.Text = $"{gapAhead:F1}s";
                CarAheadText.Text = $"to P{data.RacePosition - 1}";
            }
            else if (data.RacePosition == 1)
            {
                GapAheadText.Text = "--";
                CarAheadText.Text = "Leading";
            }
            else
            {
                GapAheadText.Text = "--";
                CarAheadText.Text = "--";
            }
            
            // Gap behind (calculated from telemetry)
            var gapBehind = CalculateGapToPosition(_latestTelemetry, data.RacePosition + 1);
            if (gapBehind >= 0)
            {
                GapBehindText.Text = $"{gapBehind:F1}s";
            }
            else
            {
                GapBehindText.Text = "--";
            }
            CarBehindText.Text = data.RacePosition < data.TotalCars ? $"to P{data.RacePosition + 1}" : "Last";
            
            // Pit strategy impact predictions
            if (data.OptimalPitLap > 0)
            {
                OptimalPitLapLabel.Text = $"If you pit at lap {data.OptimalPitLap}:";
                
                // Calculate pit now position (lose ~2 positions on average)
                int pitNowPosition = Math.Min(data.TotalCars, data.RacePosition + 2);
                PitNowPositionText.Text = $"P{pitNowPosition}";
                int pitNowChange = pitNowPosition - data.RacePosition;
                
                if (pitNowChange > 0)
                {
                    PitNowChangeText.Text = $" (▼ {pitNowChange})";
                    PitNowChangeText.Foreground = (System.Windows.Media.Brush)FindResource("BrushRed");
                    PitNowPositionText.Foreground = (System.Windows.Media.Brush)FindResource("BrushRed");
                }
                else
                {
                    PitNowChangeText.Text = " (same)";
                    PitNowChangeText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen");
                    PitNowPositionText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen");
                }
                
                // Calculate optimal pit position (likely maintain position or gain 1)
                int optimalPosition = Math.Max(1, data.RacePosition - 1);
                OptimalPitPositionText.Text = $"P{optimalPosition}";
                int optimalChange = optimalPosition - data.RacePosition;
                
                if (optimalChange < 0)
                {
                    OptimalPitChangeText.Text = $" (▲ {Math.Abs(optimalChange)})";
                    OptimalPitChangeText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen");
                    OptimalPitPositionText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen");
                }
                else if (optimalChange > 0)
                {
                    OptimalPitChangeText.Text = $" (▼ {optimalChange})";
                    OptimalPitChangeText.Foreground = (System.Windows.Media.Brush)FindResource("BrushRed");
                    OptimalPitPositionText.Foreground = (System.Windows.Media.Brush)FindResource("BrushRed");
                }
                else
                {
                    OptimalPitChangeText.Text = " (same)";
                    OptimalPitChangeText.Foreground = (System.Windows.Media.Brush)FindResource("BrushTeal");
                    OptimalPitPositionText.Foreground = (System.Windows.Media.Brush)FindResource("BrushTeal");
                }
                
                // Strategic notes
                if (data.RacePosition > 1)
                {
                    if (data.CurrentLap < data.OptimalPitLap)
                    {
                        StrategicNoteText.Text = $"Cars ahead may pit soon. Early stop on lap {data.OptimalPitLap - 2} could give undercut advantage.";
                    }
                    else if (data.CurrentLap > data.OptimalPitLap)
                    {
                        StrategicNoteText.Text = "Running longer than optimal. Consider overcut strategy if others pit.";
                    }
                    else
                    {
                        StrategicNoteText.Text = "Currently in optimal pit window. Monitor nearby car strategies.";
                    }
                }
                else
                {
                    StrategicNoteText.Text = "Leading the race. Pit strategy should focus on maintaining track position.";
                }
            }
            else
            {
                OptimalPitLapLabel.Text = "Calculating optimal pit lap...";
                PitNowPositionText.Text = "--";
                PitNowChangeText.Text = "";
                OptimalPitPositionText.Text = "--";
                OptimalPitChangeText.Text = "";
                StrategicNoteText.Text = "Gathering data to predict position changes...";
            }
        }

        /// <summary>
        /// Update weather and track conditions display (Phase 10.7 & 10.8)
        /// </summary>
        private void UpdateWeatherAndConditions()
        {
            if (_latestTelemetry == null)
                return;

            // Get weather conditions
            var weather = _weatherTrack.GetCurrentConditions();
            
            // Get damage assessment
            var damage = _pitIntelligence.AssessDamage(_latestTelemetry);
            
            // Update UI on dispatcher thread
            Dispatcher.Invoke(() =>
            {
                // Find UI elements
                var airTempText = FindName("AirTempText") as TextBlock;
                var trackTempText = FindName("TrackTempText") as TextBlock;
                var tempTrendText = FindName("TempTrendText") as TextBlock;
                var gripLevelText = FindName("GripLevelText") as TextBlock;
                var skyConditionText = FindName("SkyConditionText") as TextBlock;
                var trackWetnessText = FindName("TrackWetnessText") as TextBlock;
                var windText = FindName("WindText") as TextBlock;
                var humidityText = FindName("HumidityText") as TextBlock;
                var pressureText = FindName("PressureText") as TextBlock;
                var airDensityText = FindName("AirDensityText") as TextBlock;
                var damageStatusText = FindName("DamageStatusText") as TextBlock;
                var damageRecommendationText = FindName("DamageRecommendationText") as TextBlock;
                var pitFuelTimeText = FindName("PitFuelTimeText") as TextBlock;
                var pitTotalTimeText = FindName("PitTotalTimeText") as TextBlock;
                var pitActionText = FindName("PitActionText") as TextBlock;
                
                if (airTempText == null) return; // CONDITIONS tab not loaded yet
                
                // FIX: Update temperature displays with baseline delta
                // Format: "22.5°C (+3.2°C)" - shows current temp and change from session start
                if (weather.BaselineAirTemp.HasValue && Math.Abs(weather.AirTempDelta) >= 0.1f)
                {
                    string airDeltaSign = weather.AirTempDelta > 0 ? "+" : "";
                    airTempText.Text = $"{weather.CurrentAirTemp:F1}°C ({airDeltaSign}{weather.AirTempDelta:F1}°C)";
                }
                else
                {
                    airTempText.Text = $"{weather.CurrentAirTemp:F1}°C";
                }
                
                if (weather.BaselineTrackTemp.HasValue && Math.Abs(weather.TrackTempDelta) >= 0.1f)
                {
                    string trackDeltaSign = weather.TrackTempDelta > 0 ? "+" : "";
                    trackTempText!.Text = $"{weather.CurrentTrackTemp:F1}°C ({trackDeltaSign}{weather.TrackTempDelta:F1}°C)";
                }
                else
                {
                    trackTempText!.Text = $"{weather.CurrentTrackTemp:F1}°C";
                }
                
                // FIX: ALWAYS show weather trend (user requested to see trend at all times)
                // Display format: "Track warming +4.7°C (trend: +0.8°C/10min)" or "Stable (trend: +0.0°C/10min)"
                // CRITICAL FIX: Check if we have enough history samples, NOT if trend values are non-zero
                // Bug was: hasTrendData = (AirTempTrend != 0 || TrackTempTrend != 0) → fails when perfectly stable (0.0°C change)
                // Fixed: Check HistorySampleCount >= 600 (100 seconds of data collected)
                bool hasTrendData = weather.HistorySampleCount >= 600;
                
                if (hasTrendData)
                {
                    // Show current trend rate (always visible now)
                    string trackTrendSign = weather.TrackTempTrend >= 0 ? "+" : "";
                    string trackTrendArrow = weather.TrackTempTrend > 0.2f ? "↑" : weather.TrackTempTrend < -0.2f ? "↓" : "→";
                    
                    // Show overall status if significant total change
                    bool hasSignificantTotalChange = Math.Abs(weather.TrackTempDelta) > 1.0f;
                    if (hasSignificantTotalChange)
                    {
                        string trackDeltaSign = weather.TrackTempDelta > 0 ? "+" : "";
                        string trackStatus = weather.TrackTempDelta > 0 ? "warming" : "cooling";
                        tempTrendText!.Text = $"Track {trackStatus} {trackDeltaSign}{Math.Abs(weather.TrackTempDelta):F1}°C " +
                                            $"(trend: {trackTrendArrow}{trackTrendSign}{Math.Abs(weather.TrackTempTrend):F1}°C/10min)";
                    }
                    else
                    {
                        // No significant total change, just show current trend
                        string trendStatus = Math.Abs(weather.TrackTempTrend) < 0.2f ? "Stable" : 
                                           weather.TrackTempTrend > 0 ? "Track warming" : "Track cooling";
                        tempTrendText!.Text = $"{trendStatus} (trend: {trackTrendArrow}{trackTrendSign}{Math.Abs(weather.TrackTempTrend):F1}°C/10min)";
                    }
                    
                    // Color based on trend direction
                    if (Math.Abs(weather.TrackTempTrend) < 0.2f)
                        tempTrendText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen");
                    else if (weather.TrackTempTrend > 0)
                        tempTrendText.Foreground = (System.Windows.Media.Brush)FindResource("BrushRed");
                    else
                        tempTrendText.Foreground = (System.Windows.Media.Brush)FindResource("BrushBlue");
                }
                else
                {
                    // Not enough data yet - show how many samples collected
                    int secondsCollected = weather.HistorySampleCount / 6; // Assuming 6Hz telemetry
                    tempTrendText!.Text = $"Calculating trend... ({secondsCollected}s / 100s needed)";
                    tempTrendText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGray");
                    
                    // Log why trend calculation is blocked
                    if (weather.HistorySampleCount > 0 && weather.HistorySampleCount < 600)
                    {
                        _logger.LogDebug("Weather trend blocked: {SampleCount} samples ({SecondsCollected}s), need 600 (100s)", weather.HistorySampleCount, secondsCollected);
                    }
                }
                
                // Update grip level
                string gripEstimate = _weatherTrack.GetGripEstimate(weather.CurrentTrackTemp);
                gripLevelText!.Text = gripEstimate;
                
                // Color-code grip level
                if (gripEstimate.Contains("Optimal"))
                    gripLevelText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen");
                else if (gripEstimate.Contains("Good"))
                    gripLevelText.Foreground = (System.Windows.Media.Brush)FindResource("BrushTeal");
                else if (gripEstimate.Contains("Poor"))
                    gripLevelText.Foreground = (System.Windows.Media.Brush)FindResource("BrushOrange");
                else
                    gripLevelText.Foreground = (System.Windows.Media.Brush)FindResource("BrushRed");
                
                // Update sky conditions
                skyConditionText!.Text = weather.SkyCondition;
                
                // FIX: Enhanced track wetness display with rain tire indicator and fog warning
                // User reported "ever so slightly wet and slippery" not detected properly
                // iRacing SDK: TrackWetnessLevel 0=Dry, 1=MostlyDry, 2=VeryLightlyWet, 3=LightlyWet, 4=ModeratelyWet, 5=VeryWet, 6=ExtremelyWet
                // IsWetTrack flag (WeatherDeclaredWet) = rain tires allowed
                string wetnessDisplay = weather.TrackWetnessDescription;
                
                // CRITICAL: If rain tires allowed but shows "Dry", add warning
                if (weather.IsWetTrack && weather.TrackWetnessLevel == 0)
                {
                    wetnessDisplay = "Dry (⚠️ Rain tires allowed)";
                    trackWetnessText!.Foreground = (System.Windows.Media.Brush)FindResource("BrushOrange");
                }
                else if (weather.IsWetTrack)
                {
                    // Add rain tire indicator to wetness description
                    wetnessDisplay = $"{weather.TrackWetnessDescription} 🌧️";
                }
                
                // Add fog warning if present
                if (weather.FogLevel > 0.1f)
                {
                    wetnessDisplay += $" (Fog {weather.FogLevel * 100:F0}%)";
                }
                
                trackWetnessText!.Text = wetnessDisplay;
                
                // Color code by wetness level
                if (weather.TrackWetnessLevel == 0 && !weather.IsWetTrack) // Dry
                {
                    trackWetnessText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen");
                }
                else if (weather.TrackWetnessLevel <= 2) // Mostly Dry to Very Lightly Wet
                {
                    trackWetnessText.Foreground = (System.Windows.Media.Brush)FindResource("BrushTeal");
                }
                else if (weather.TrackWetnessLevel <= 4) // Lightly Wet to Moderately Wet
                {
                    trackWetnessText.Foreground = (System.Windows.Media.Brush)FindResource("BrushOrange");
                }
                else // Very Wet to Extremely Wet
                {
                    trackWetnessText.Foreground = (System.Windows.Media.Brush)FindResource("BrushRed");
                }
                
                // Update wind information
                if (weather.WindSpeed > 0.1f)
                {
                    // Convert m/s to km/h for display
                    float windKmh = weather.WindSpeed * 3.6f;
                    windText!.Text = $"{windKmh:F1} km/h from {weather.WindDirectionCardinal}";
                    
                    // Color code by wind strength
                    if (windKmh < 10f)
                        windText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen");
                    else if (windKmh < 25f)
                        windText.Foreground = (System.Windows.Media.Brush)FindResource("BrushTeal");
                    else if (windKmh < 40f)
                        windText.Foreground = (System.Windows.Media.Brush)FindResource("BrushOrange");
                    else
                        windText.Foreground = (System.Windows.Media.Brush)FindResource("BrushRed");
                }
                else
                {
                    windText!.Text = "Calm";
                    windText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen");
                }
                
                // Update humidity
                if (weather.RelativeHumidity > 0)
                {
                    humidityText!.Text = $"{weather.RelativeHumidity:F0}%";
                    
                    // Color code by comfort level
                    if (weather.RelativeHumidity < 30f)
                        humidityText.Foreground = (System.Windows.Media.Brush)FindResource("BrushTeal"); // Low
                    else if (weather.RelativeHumidity < 60f)
                        humidityText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen"); // Comfortable
                    else if (weather.RelativeHumidity < 80f)
                        humidityText.Foreground = (System.Windows.Media.Brush)FindResource("BrushOrange"); // High
                    else
                        humidityText.Foreground = (System.Windows.Media.Brush)FindResource("BrushRed"); // Very High
                }
                else
                {
                    humidityText!.Text = "--";
                    humidityText.Foreground = System.Windows.Media.Brushes.Gray;
                }
                
                // Update pressure with percentage vs sea level standard
                if (weather.AirPressure > 0)
                {
                    // Convert Pa to hPa (millibars) for display
                    float pressureHPa = weather.AirPressure / 100f;
                    
                    // Standard sea level pressure: 1013.25 hPa
                    float pressurePercent = (pressureHPa / 1013.25f) * 100f;
                    
                    pressureText!.Text = $"{pressureHPa:F1} hPa ({pressurePercent:F0}%)";
                    
                    // Color code by pressure (weather indicator)
                    if (pressureHPa < 1000f)
                        pressureText.Foreground = (System.Windows.Media.Brush)FindResource("BrushRed"); // Low (stormy)
                    else if (pressureHPa < 1013f)
                        pressureText.Foreground = (System.Windows.Media.Brush)FindResource("BrushOrange"); // Below normal
                    else if (pressureHPa < 1023f)
                        pressureText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen"); // Normal
                    else
                        pressureText.Foreground = (System.Windows.Media.Brush)FindResource("BrushTeal"); // High (fair)
                }
                else
                {
                    pressureText!.Text = "--";
                    pressureText.Foreground = System.Windows.Media.Brushes.Gray;
                }
                
                // FIX: Update air density (affects downforce)
                if (weather.AirDensity > 0)
                {
                    // Standard air density at sea level: 1.225 kg/m³
                    // Higher altitude = lower density = less downforce
                    // Format: "1.18 kg/m³ (96%)" - shows density and percentage vs standard
                    float densityPercent = (weather.AirDensity / 1.225f) * 100f;
                    airDensityText!.Text = $"{weather.AirDensity:F3} kg/m³ ({densityPercent:F0}%)";
                    
                    // Color code: Low density (<95%) = orange, Normal (95-105%) = green, High (>105%) = teal
                    if (densityPercent < 95f)
                        airDensityText.Foreground = (System.Windows.Media.Brush)FindResource("BrushOrange");
                    else if (densityPercent > 105f)
                        airDensityText.Foreground = (System.Windows.Media.Brush)FindResource("BrushTeal");
                    else
                        airDensityText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen");
                }
                else
                {
                    airDensityText!.Text = "--";
                    airDensityText.Foreground = System.Windows.Media.Brushes.Gray;
                }
                
                // Log additional info for fog and rain tires
                if (weather.FogLevel > 0)
                {
                    _logger.LogDebug("Weather fog level: {FogLevel:F0}%", weather.FogLevel);
                }
                
                if (weather.IsWetTrack)
                {
                    _logger.LogDebug("Weather: Rain tires ALLOWED");
                }
                
                // Update damage assessment
                if (damage.HasAeroDamage || damage.HasSuspensionDamage || damage.HasEngineDamage)
                {
                    damageStatusText!.Text = damage.DamageDescription;
                    damageStatusText.Foreground = (System.Windows.Media.Brush)FindResource("BrushOrange");
                    damageRecommendationText!.Text = damage.RepairRecommendation;
                    damageRecommendationText.Foreground = damage.ShouldRepair ? 
                        (System.Windows.Media.Brush)FindResource("BrushRed") : 
                        (System.Windows.Media.Brush)FindResource("BrushTeal");
                }
                else
                {
                    damageStatusText!.Text = "No damage detected";
                    damageStatusText.Foreground = (System.Windows.Media.Brush)FindResource("BrushGreen");
                    damageRecommendationText!.Text = "Vehicle in good condition";
                    damageRecommendationText.Foreground = System.Windows.Media.Brushes.Gray;
                }
                
                // Estimate pit service (use current fuel level as estimate)
                float estimatedFuelNeeded = Math.Max(_latestTelemetry.FuelLevel, 20f);
                bool needsTires = false; // Could be enhanced to check tire wear percentage
                float optRepairTime = _latestTelemetry.PitOptRepairLeft; // Use actual SDK repair time
                
                var pitEstimate = _pitIntelligence.EstimatePitService(estimatedFuelNeeded, needsTires, optRepairTime);
                
                pitFuelTimeText!.Text = $"{pitEstimate.FuelTime:F1}s";
                pitTotalTimeText!.Text = $"{pitEstimate.TotalPitTime:F1}s";
                pitActionText!.Text = pitEstimate.RecommendedAction;
            });
            
            // Log weather changes
            if (weather.IsWeatherChanging)
            {
                _logger.LogInformation("Weather change: {ChangeDescription}", weather.ChangeDescription);
            }
        }

        /// <summary>
        /// Update competitor intelligence displays (Phase 10.6)
        /// </summary>
        private void UpdateCompetitorIntelligence()
        {
            if (_latestTelemetry == null)
                return;

            // PERFORMANCE: Only update competitor lists when lap changes
            // Gap displays update every call, but expensive LINQ queries only on lap change
            bool shouldUpdateCompetitorLists = _latestTelemetry.Lap != _lastCompetitorUpdateLap;

            // Get gap to leader
            float gapToLeader = _competitorIntelligence.GetGapToLeader(_latestTelemetry);
            var leader = _competitorIntelligence.GetLeader(_latestTelemetry, sameClassOnly: true);
            
            // Update OVERVIEW tab - Gap to Leader
            if (leader != null && _latestTelemetry.LiveClassPosition > 1)
            {
                GapToLeaderText.Text = $"+ {gapToLeader:F1}s";
                LeaderNameText.Text = $"P1: {leader.DriverName}";
                
                if (leader.LastLapTime > 0)
                {
                    TimeSpan lastLap = TimeSpan.FromSeconds(leader.LastLapTime);
                    LeaderLastLapText.Text = $"Last lap: {lastLap:m\\:ss\\.fff}";
                }
                else
                {
                    LeaderLastLapText.Text = "Last lap: --";
                }
            }
            else if (_latestTelemetry.LiveClassPosition == 1)
            {
                GapToLeaderText.Text = "LEADING";
                LeaderNameText.Text = "You are P1 in class";
                LeaderLastLapText.Text = $"Lap {_latestTelemetry.Lap}";
            }
            else
            {
                GapToLeaderText.Text = "--";
                LeaderNameText.Text = "Leader data unavailable";
                LeaderLastLapText.Text = "";
            }
            
            // Get competitor ahead
            var carAhead = _competitorIntelligence.GetCompetitorAhead(_latestTelemetry, sameClassOnly: true);
            float gapAheadRaw = Math.Abs(_competitorIntelligence.GetGapToCarAhead(_latestTelemetry));

            // Get competitor behind
            var carBehind = _competitorIntelligence.GetCompetitorBehind(_latestTelemetry, sameClassOnly: true);
            float gapBehindRaw = _competitorIntelligence.GetGapToCarBehind(_latestTelemetry);

            // ANTI-FLICKER FIX: Smooth gap values using exponential moving average
            // This prevents rapid flickering from telemetry noise (60 Hz updates)
            // Only updates UI when gap changes by >0.1s (threshold in config)
            float gapAhead = _valueSmoothing.Smooth("gap_ahead", gapAheadRaw, ValueSmoothingService.GapSmoothingConfig);
            float gapBehind = _valueSmoothing.Smooth("gap_behind", gapBehindRaw, ValueSmoothingService.GapSmoothingConfig);

            // Update POSITIONS tab displays
            // Gap Ahead
            if (carAhead != null && gapAhead > 0)
            {
                GapAheadText.Text = $"{gapAhead:F1}s";
                CarAheadText.Text = $"to P{carAhead.Position}: {carAhead.DriverName}";
            }
            else if (_latestTelemetry.LiveClassPosition == 1)
            {
                GapAheadText.Text = "--";
                CarAheadText.Text = "LEADING CLASS";
                _valueSmoothing.Reset("gap_ahead"); // Reset smoothing when leading
            }
            else
            {
                GapAheadText.Text = "--";
                CarAheadText.Text = "to P?: --";
                _valueSmoothing.Reset("gap_ahead"); // Reset smoothing when no car ahead
            }

            // Gap Behind
            if (carBehind != null && gapBehind > 0)
            {
                GapBehindText.Text = $"{gapBehind:F1}s";
                CarBehindText.Text = $"to P{carBehind.Position}: {carBehind.DriverName}";
            }
            else
            {
                GapBehindText.Text = "--";
                CarBehindText.Text = "to P?: --";
                _valueSmoothing.Reset("gap_behind"); // Reset smoothing when no car behind
            }
            
            // PERFORMANCE: Only update expensive lists when lap changes
            if (shouldUpdateCompetitorLists)
            {
                // Get recent pit activity
                var recentPits = _competitorIntelligence.GetRecentPitActivity(5);
                
                // Update Recent Pit Activity list in POSITIONS tab
                RecentPitActivityList.ItemsSource = recentPits;
                
                // Get fastest competitors for lap time comparison
                var fastestCompetitors = _competitorIntelligence.GetCompetitors(_latestTelemetry, sameClassOnly: true)
                    .Where(c => c.BestLapTime > 0)
                    .OrderBy(c => c.BestLapTime)
                    .Take(10)
                    .Select(c => new LapTimeViewModel
                    {
                        Position = c.Position,
                        DriverName = c.DriverName,
                        LastLapTime = FormatLapTime(c.LastLapTime),
                        BestLapTime = FormatLapTime(c.BestLapTime)
                    })
                    .ToList();
                
                // Update Lap Time Comparison list
                LapTimeComparisonList.ItemsSource = fastestCompetitors;
                
                // Log pit activity
                if (recentPits.Count > 0 && recentPits[0].PitLap == _latestTelemetry.Lap)
                {
                    var pit = recentPits[0];
                    _logger.LogDebug("Competitor Intelligence - P{Position}: {DriverName} ({CarNumber}) - {Status} on Lap {PitLap}", pit.Position, pit.DriverName, pit.CarNumber, pit.Status, pit.PitLap);
                }
                
                _lastCompetitorUpdateLap = _latestTelemetry.Lap;
            }
            
            // Phase 10.6 Complete: Gap displays, pit activity tracking, lap time comparison
        }

        /// <summary>
        /// Handle header drag to move window
        /// </summary>
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>
        /// Handle close button click
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Save state when closing
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            // Unsubscribe from events
            _fuelCalculator.FuelDataUpdated -= OnFuelDataUpdated;
            
            if (_telemetryService != null)
            {
                _telemetryService.TelemetryUpdated -= OnTelemetryUpdated;
            }

            // Save window state
            SaveWindowState();

            // Update AppSettings to mark window as closed
            AppSettings.Instance.ShowPitStrategyWindow = false;
            AppSettings.Instance.Save();

            base.OnClosed(e);
        }

        /// <summary>
        /// Get weather-based fuel consumption multiplier
        /// Rain = LESS fuel per lap (slower pace, less throttle application)
        /// Scientific basis: Slower lap times = reduced fuel burn rate
        /// </summary>
        private float GetWeatherFuelMultiplier()
        {
            if (_weatherTrack == null) return 1.0f;
            
            // Check if rain is likely using WeatherTrackService
            bool isRaining = _weatherTrack.IsRainLikely();
            
            // Also check current conditions for weather status
            var conditions = _weatherTrack.GetCurrentConditions();
            if (conditions != null && conditions.WeatherStatus != null)
            {
                // Check for explicit rain indicators in weather status
                isRaining |= conditions.WeatherStatus.Contains("Rain", StringComparison.OrdinalIgnoreCase) ||
                            conditions.WeatherStatus.Contains("Wet", StringComparison.OrdinalIgnoreCase);
            }
            
            // Rain multiplier: 0.88 = 12% less fuel (typical rain pace reduction)
            // Range: 0.85-0.92 (8-15% reduction depending on rain intensity)
            return isRaining ? 0.88f : 1.0f;
        }

        /// <summary>
        /// Get weather-based tire degradation multiplier
        /// Rain = BETTER tire life (cooler temps, less aggressive driving)
        /// Exception: Wet tires on dry track = significantly worse wear
        /// </summary>
        private float GetWeatherTireMultiplier()
        {
            if (_weatherTrack == null) return 1.0f;
            
            bool isRaining = _weatherTrack.IsRainLikely();
            
            var conditions = _weatherTrack.GetCurrentConditions();
            if (conditions != null && conditions.WeatherStatus != null)
            {
                isRaining |= conditions.WeatherStatus.Contains("Rain", StringComparison.OrdinalIgnoreCase) ||
                            conditions.WeatherStatus.Contains("Wet", StringComparison.OrdinalIgnoreCase);
            }
            
            // Rain multiplier: 0.7 = 30% less tire degradation
            // Cooler track temps + less aggressive inputs = longer tire life
            return isRaining ? 0.7f : 1.0f;
        }

        /// <summary>
        /// Format lap time from seconds to MM:SS.fff format
        /// </summary>
        private static string FormatLapTime(float seconds)
        {
            if (seconds <= 0)
                return "--";

            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return $"{time:m\\:ss\\.fff}";
        }
    }
    
    /// <summary>
    /// Represents calculation results for a pit strategy scenario
    /// </summary>
    internal class ScenarioResult
    {
        public bool IsFeasible { get; set; } = true;
        public float FuelSavingRequired { get; set; }
        public int PitLap1 { get; set; }
        public int PitLap2 { get; set; }
        public int PitLap3 { get; set; }
        public float FuelToAdd1 { get; set; }
        public float TotalPitTime { get; set; }
        public float TimeDelta { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// View model for lap time comparison display (Phase 10.6)
    /// </summary>
    internal class LapTimeViewModel
    {
        public int Position { get; set; }
        public string DriverName { get; set; } = string.Empty;
        public string LastLapTime { get; set; } = "--";
        public string BestLapTime { get; set; } = "--";
    }
}
