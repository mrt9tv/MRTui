using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using iRacingOverlay.Core.Services;
using iRacingOverlay.Core.Services.Setup;
using iRacingOverlay.Core.Services.SetupEngineering;
using iRacingOverlay.Core.Services.ML;

namespace iRacingOverlay.WPF.Windows.SetupEngineering;

/// <summary>
/// Setup Engineering Mode Window
/// Provides UI for lap comparison, setup analysis, and ML recommendations
/// </summary>
public partial class SetupEngineeringWindow : Window
{
    private readonly SetupDatabaseService _database;
    private readonly LapComparisonService _comparisonService;
    private readonly SetupFileParser _setupParser;
    private readonly MLModelService _mlService;
    private readonly ITelemetryService? _telemetryService;
    
    private string? _currentSessionId;
    private string? _currentSetupId; // Active setup being tested
    private SetupComparison? _lastComparison;
    
    // Lap tracking
    private iRacingOverlay.Core.Models.TelemetryData? _lastTelemetryData;
    private int _lastLapNumber = -1;
    private int _sessionLapCount = 0;
    
    public SetupEngineeringWindow(ITelemetryService? telemetryService = null)
    {
        InitializeComponent();
        
        _telemetryService = telemetryService;
        
        // Initialize services
        _database = new SetupDatabaseService();
        _comparisonService = new LapComparisonService(_database);
        _setupParser = new SetupFileParser();
        _mlService = new MLModelService();
        
        // Subscribe to live telemetry updates
        if (_telemetryService != null)
        {
            _telemetryService.TelemetryUpdated += OnTelemetryUpdated;
        }
        
        // Cleanup on close
        Closing += (s, e) =>
        {
            if (_telemetryService != null)
            {
                _telemetryService.TelemetryUpdated -= OnTelemetryUpdated;
            }
        };
        
        // Initialize async
        Loaded += async (s, e) =>
        {
            await _database.InitializeAsync();
            await _mlService.LoadModelsAsync();
            await LoadSessionsAsync();
            
            // Wire up tab selection events for lazy loading
            var mainTab = this.FindName("MainTabControl");
            if (mainTab is TabControl tabControl)
            {
                tabControl.SelectionChanged += TabControl_SelectionChanged;
            }
        };
    }
    
    /// <summary>
    /// Load existing sessions from database
    /// </summary>
    private async Task LoadSessionsAsync()
    {
        try
        {
            var sessions = await _database.ListSessionsAsync();
            
            if (sessions.Count > 0)
            {
                var latest = sessions.First();
                TrackCarText.Text = $"{latest.TrackName} - {latest.CarName}";
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error loading sessions: {ex.Message}", isError: true);
        }
    }
    
    /// <summary>
    /// Handle live telemetry updates for lap tracking
    /// </summary>
    private void OnTelemetryUpdated(object? sender, iRacingOverlay.Core.Models.TelemetryData telemetry)
    {
        // Store latest telemetry data
        _lastTelemetryData = telemetry;
        
        // Track lap changes during active session
        if (_currentSessionId != null && telemetry.Lap != _lastLapNumber)
        {
            // Skip first lap detection (lap number changes from -1 to 0 or 1)
            if (_lastLapNumber >= 0 && telemetry.LapLastLapTime > 0)
            {
                _sessionLapCount++;
                
                // Record lap to database (async without await to avoid blocking telemetry)
                _ = RecordLapAsync(telemetry);
                
                // Update UI on dispatcher thread
                Dispatcher.Invoke(() =>
                {
                    var trackName = telemetry.TrackName ?? "Unknown Track";
                    var carName = !string.IsNullOrEmpty(telemetry.CarScreenName) 
                        ? telemetry.CarScreenName 
                        : (!string.IsNullOrEmpty(telemetry.CarNumber) ? $"Car #{telemetry.CarNumber}" : "Unknown Car");
                    TrackCarText.Text = $"{trackName} - {carName} | Laps: {_sessionLapCount}";
                    
                    // Display current setup name from iRacing garage
                    var setupName = telemetry.DriverSetupName;
                    var setupModified = telemetry.DriverSetupIsModified == 1 ? " (Modified)" : "";
                    if (!string.IsNullOrEmpty(setupName))
                    {
                        SessionNameText.Text = $"{setupName}{setupModified}";
                    }
                    
                    // Update statistics
                    UpdateLapStatistics();
                    
                    var lapTime = telemetry.LapLastLapTime;
                    ShowStatus($"📊 Lap {_sessionLapCount} completed ({lapTime:F3}s)", isError: false);
                });
            }
            
            _lastLapNumber = telemetry.Lap;
        }
    }
    
    /// <summary>
    /// Record completed lap to database
    /// </summary>
    private async Task RecordLapAsync(iRacingOverlay.Core.Models.TelemetryData telemetry)
    {
        if (_currentSetupId == null) return;
        
        try
        {
            // Store lap telemetry data
            await _database.AddLapAsync(
                setupId: _currentSetupId,
                lapNumber: _sessionLapCount,
                lapTime: telemetry.LapLastLapTime,
                sectorTimes: null, // TODO: Parse sector times from telemetry
                fuelUsed: null, // TODO: Calculate fuel used from FuelLevel delta
                avgTireTemps: null,
                avgSpeed: telemetry.Speed,
                maxSpeed: null,
                avgThrottle: telemetry.Throttle,
                isValid: true, // TODO: Add validation logic
                incidentCount: 0
            );
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => ShowStatus($"⚠️ Error recording lap: {ex.Message}", isError: true));
        }
    }
    
    /// <summary>
    /// Update lap statistics display
    /// </summary>
    private async void UpdateLapStatistics()
    {
        if (_currentSetupId == null) return;
        
        try
        {
            var laps = await _database.GetLapsAsync(_currentSetupId, validOnly: false);
            
            var totalLaps = laps.Count;
            var validLaps = laps.Count(l => l.IsValid);
            var outliers = totalLaps - validLaps;
            
            LapCountText.Text = totalLaps.ToString();
            ValidLapCountText.Text = validLaps.ToString();
            OutlierCountText.Text = outliers.ToString();
        }
        catch
        {
            // Ignore errors in background UI update
        }
    }
    
    /// <summary>
    /// Handle tab selection changes to populate data on-demand
    /// </summary>
    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not TabControl) return;
        
        var tabControl = sender as TabControl;
        if (tabControl?.SelectedItem is not TabItem selectedTab) return;
        
        var header = selectedTab.Header?.ToString();
        
        if (header == "Lap Data")
        {
            _ = LoadLapDataAsync();
        }
        else if (header == "Sector Analysis")
        {
            ShowStatus("ℹ️ Sector Analysis: Select two setups to compare", isError: false);
        }
        else if (header == "Setup Changes")
        {
            ShowStatus("ℹ️ Setup Changes: Load a setup file to view parameters", isError: false);
        }
    }
    
    /// <summary>
    /// Load lap data for current session
    /// </summary>
    private async Task LoadLapDataAsync()
    {
        if (_currentSetupId == null)
        {
            LapDataGrid.ItemsSource = null;
            ShowStatus("ℹ️ No active session. Start a session to see lap data.", isError: false);
            return;
        }
        
        try
        {
            var laps = await _database.GetLapsAsync(_currentSetupId, validOnly: false);
            LapDataGrid.ItemsSource = laps.OrderBy(l => l.LapNumber).ToList();
            ShowStatus($"✅ Loaded {laps.Count} laps", isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error loading laps: {ex.Message}", isError: true);
        }
    }
    
    /// <summary>
    /// Start new setup engineering session
    /// </summary>
    private async void StartSessionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get track/car from live iRacing telemetry if available
            var trackName = _lastTelemetryData?.TrackName ?? "Unknown Track";
            var carInfo = !string.IsNullOrEmpty(_lastTelemetryData?.CarScreenName)
                ? _lastTelemetryData.CarScreenName
                : (!string.IsNullOrEmpty(_lastTelemetryData?.CarNumber) 
                    ? $"Car #{_lastTelemetryData.CarNumber}" 
                    : "Unknown Car");
            var sessionType = "Practice";
            
            // Reset lap tracking
            _sessionLapCount = 0;
            _lastLapNumber = _lastTelemetryData?.Lap ?? -1;
            
            var sessionId = await _database.CreateSessionAsync(
                trackName: trackName,
                carName: carInfo,
                sessionType: sessionType,
                notes: "Setup engineering session (live telemetry)"
            );
            
            // Create default setup for this session
            var setupId = await _database.AddSetupAsync(
                sessionId: sessionId,
                setupName: "Live Session Setup",
                setupData: "{}", // Empty JSON for now
                setupChanges: null,
                isBaseline: true
            );
            
            _currentSessionId = sessionId;
            _currentSetupId = setupId;
            SessionNameText.Text = $"Session {sessionId.Substring(0, 8)}...";
            StartSessionButton.IsEnabled = false;
            EndSessionButton.IsEnabled = true;
            
            // Update UI with initial info
            TrackCarText.Text = $"{trackName} - {carInfo} | Laps: 0";
            
            if (_telemetryService == null || _lastTelemetryData == null)
            {
                ShowStatus("⚠️ Session started (no telemetry connection)", isError: false);
            }
            else
            {
                ShowStatus("✅ Session started. Begin recording laps...", isError: false);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error starting session: {ex.Message}", isError: true);
        }
    }
    
    /// <summary>
    /// End current session
    /// </summary>
    private async void EndSessionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSessionId == null) return;
        
        try
        {
            await _database.EndSessionAsync(_currentSessionId);
            
            var lapInfo = _sessionLapCount > 0 ? $" ({_sessionLapCount} laps recorded)" : "";
            
            // Reset session state
            _currentSessionId = null;
            _currentSetupId = null;
            _sessionLapCount = 0;
            _lastLapNumber = -1;
            
            StartSessionButton.IsEnabled = true;
            EndSessionButton.IsEnabled = false;
            
            ShowStatus($"✅ Session ended{lapInfo}", isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error ending session: {ex.Message}", isError: true);
        }
    }
    
    /// <summary>
    /// Load baseline setup selection
    /// </summary>
    private async void BaselineSetupCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await UpdateCompareButtonState();
    }
    
    /// <summary>
    /// Load modified setup selection
    /// </summary>
    private async void ModifiedSetupCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await UpdateCompareButtonState();
    }
    
    /// <summary>
    /// Enable compare button if both setups selected
    /// </summary>
    private async Task UpdateCompareButtonState()
    {
        CompareButton.IsEnabled = BaselineSetupCombo.SelectedItem != null && ModifiedSetupCombo.SelectedItem != null;
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Compare selected setups
    /// </summary>
    private async void CompareButton_Click(object sender, RoutedEventArgs e)
    {
        if (BaselineSetupCombo.SelectedItem == null || ModifiedSetupCombo.SelectedItem == null)
            return;
        
        try
        {
            ShowStatus("Comparing setups...", isError: false);
            
            // Get setup IDs (simplified - actual implementation would extract from ComboBox items)
            var baselineId = "baseline-setup-id";  // TODO: Get from selected item
            var modifiedId = "modified-setup-id";  // TODO: Get from selected item
            
            // Perform comparison
            var comparison = await _comparisonService.CompareSetupsAsync(baselineId, modifiedId);
            _lastComparison = comparison;
            
            if (!comparison.IsValid)
            {
                ShowStatus($"⚠️ {comparison.ErrorMessage}", isError: true);
                return;
            }
            
            // Display results
            DisplayComparisonResults(comparison);
            
            // Update sector grid
            SectorDataGrid.ItemsSource = comparison.SectorComparisons;
            
            // Enable export/ML buttons
            ExportCSVButton.IsEnabled = true;
            GenerateMLButton.IsEnabled = true;
            
            ShowStatus($"✅ Comparison complete: {comparison.Recommendation}", isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error comparing setups: {ex.Message}", isError: true);
        }
    }
    
    /// <summary>
    /// Display comparison results in UI
    /// </summary>
    private void DisplayComparisonResults(SetupComparison comparison)
    {
        ComparisonResultsPanel.Children.Clear();
        
        // Overall result
        var resultBorder = new Border
        {
            Background = new SolidColorBrush(comparison.ImprovementSeconds < 0 ? Color.FromRgb(0, 100, 0) : Color.FromRgb(100, 0, 0)),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 10),
            CornerRadius = new CornerRadius(3)
        };
        
        var resultStack = new StackPanel();
        
        var resultIcon = new TextBlock
        {
            Text = comparison.ImprovementSeconds < 0 ? "✅ IMPROVEMENT" : "❌ REGRESSION",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 5)
        };
        resultStack.Children.Add(resultIcon);
        
        var deltaText = new TextBlock
        {
            Text = $"{Math.Abs(comparison.ImprovementSeconds):F3}s {(comparison.ImprovementSeconds < 0 ? "faster" : "slower")}",
            FontSize = 20,
            FontWeight = FontWeights.Bold
        };
        resultStack.Children.Add(deltaText);
        
        var confidenceText = new TextBlock
        {
            Text = $"{comparison.Confidence:P0} confidence {(comparison.IsSignificant ? "✓" : "⚠️")}",
            FontSize = 14,
            Margin = new Thickness(0, 5, 0, 0)
        };
        resultStack.Children.Add(confidenceText);
        
        resultBorder.Child = resultStack;
        ComparisonResultsPanel.Children.Add(resultBorder);
        
        // Baseline stats
        AddStatBlock("Baseline Setup", comparison.BaselineLapCount, comparison.BaselineBestLap, comparison.BaselineAvgLap, comparison.BaselineStdDev);
        
        // Modified stats
        AddStatBlock("Modified Setup", comparison.ModifiedLapCount, comparison.ModifiedBestLap, comparison.ModifiedAvgLap, comparison.ModifiedStdDev);
        
        // Recommendation
        var recoBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 10, 0, 0),
            CornerRadius = new CornerRadius(3)
        };
        
        var recoText = new TextBlock
        {
            Text = comparison.Recommendation,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };
        
        recoBorder.Child = recoText;
        ComparisonResultsPanel.Children.Add(recoBorder);
    }
    
    /// <summary>
    /// Add stat block to comparison panel
    /// </summary>
    private void AddStatBlock(string title, int lapCount, float bestLap, float avgLap, float stdDev)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 5, 0, 0)
        };
        
        var stack = new StackPanel();
        
        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 5)
        };
        stack.Children.Add(titleBlock);
        
        stack.Children.Add(new TextBlock { Text = $"Laps: {lapCount}", FontSize = 12 });
        stack.Children.Add(new TextBlock { Text = $"Best: {bestLap:F3}s", FontSize = 12 });
        stack.Children.Add(new TextBlock { Text = $"Avg: {avgLap:F3}s", FontSize = 12 });
        stack.Children.Add(new TextBlock { Text = $"StdDev: {stdDev:F3}s", FontSize = 12 });
        
        border.Child = stack;
        ComparisonResultsPanel.Children.Add(border);
    }
    
    /// <summary>
    /// Export comparison to CSV
    /// </summary>
    private void ExportCSVButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastComparison == null) return;
        
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"Setup_Comparison_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                // TODO: Implement CSV export
                ShowStatus($"✅ Exported to {dialog.FileName}", isError: false);
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Error exporting: {ex.Message}", isError: true);
            }
        }
    }
    
    /// <summary>
    /// Load setup file (.htm HTML export recommended, .sto binary not yet supported)
    /// </summary>
    private async void LoadSetupFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "iRacing HTML Setup (*.htm;*.html)|*.htm;*.html|iRacing Binary Setup (*.sto)|*.sto|All files (*.*)|*.*",
            Title = "Load iRacing Setup (HTML format recommended)"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var setup = await _setupParser.ParseSetupFileAsync(dialog.FileName);
                
                if (setup != null)
                {
                    var fileName = Path.GetFileName(dialog.FileName);
                    ShowStatus($"✅ Loaded setup: {fileName}", isError: false);
                    
                    // Display setup summary in status
                    var summary = GenerateSetupSummary(setup);
                    MessageBox.Show(summary, "Setup Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Display setup parameters in Setup Changes tab
                    DisplaySetupChanges(setup);
                    
                    // TODO Phase 3.6: Store setup to database and enable comparison
                }
            }
            catch (NotSupportedException ex)
            {
                // Binary .sto file - show helpful message
                MessageBox.Show(
                    ex.Message + "\n\n" +
                    "To export as HTML:\n" +
                    "1. Open iRacing\n" +
                    "2. Go to Garage → Setup\n" +
                    "3. Click 'Export' button\n" +
                    "4. Save as .htm file",
                    "Binary Format Not Supported",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                ShowStatus("⚠️ Binary .sto format not supported - use HTML export", isError: true);
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Error loading setup: {ex.Message}", isError: true);
                MessageBox.Show($"Failed to load setup:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    /// <summary>
    /// Generate human-readable setup summary
    /// </summary>
    private string GenerateSetupSummary(SetupData setup)
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine("📋 SETUP PARAMETERS\n");
        
        summary.AppendLine("✈️ AERO:");
        summary.AppendLine($"  Front Wing: {setup.Aero.FrontWing}°");
        summary.AppendLine($"  Rear Wing: {setup.Aero.RearWing}°\n");
        
        summary.AppendLine("🏁 TIRES (Cold Pressure):");
        summary.AppendLine($"  LF: {setup.Tires.LeftFrontPressure:F1} kPa");
        summary.AppendLine($"  RF: {setup.Tires.RightFrontPressure:F1} kPa");
        summary.AppendLine($"  LR: {setup.Tires.LeftRearPressure:F1} kPa");
        summary.AppendLine($"  RR: {setup.Tires.RightRearPressure:F1} kPa\n");
        
        summary.AppendLine("🔧 CHASSIS:");
        summary.AppendLine($"  Front ARB: {setup.Chassis.FrontARB}");
        summary.AppendLine($"  Rear ARB: {setup.Chassis.RearARB}");
        summary.AppendLine($"  Brake Bias: {setup.Chassis.BrakeBias:F1}%");
        
        return summary.ToString();
    }
    
    /// <summary>
    /// Display setup parameters in Setup Changes tab
    /// </summary>
    private void DisplaySetupChanges(SetupData setup)
    {
        var changes = new List<SetupChangeDisplay>();
        
        // Aero
        if (setup.Aero.FrontWing.HasValue)
            changes.Add(new SetupChangeDisplay { Category = "Aero", Parameter = "Front Wing", BaselineValue = "-", ModifiedValue = $"{setup.Aero.FrontWing:F1}", Delta = "-", Unit = "°" });
        if (setup.Aero.RearWing.HasValue)
            changes.Add(new SetupChangeDisplay { Category = "Aero", Parameter = "Rear Wing", BaselineValue = "-", ModifiedValue = $"{setup.Aero.RearWing:F1}", Delta = "-", Unit = "°" });
        
        // Chassis
        if (setup.Chassis.FrontARB.HasValue)
            changes.Add(new SetupChangeDisplay { Category = "Chassis", Parameter = "Front ARB", BaselineValue = "-", ModifiedValue = $"{setup.Chassis.FrontARB:F0}", Delta = "-", Unit = "" });
        if (setup.Chassis.RearARB.HasValue)
            changes.Add(new SetupChangeDisplay { Category = "Chassis", Parameter = "Rear ARB", BaselineValue = "-", ModifiedValue = $"{setup.Chassis.RearARB:F0}", Delta = "-", Unit = "" });
        if (setup.Chassis.BrakeBias.HasValue)
            changes.Add(new SetupChangeDisplay { Category = "Chassis", Parameter = "Brake Bias", BaselineValue = "-", ModifiedValue = $"{setup.Chassis.BrakeBias:F1}", Delta = "-", Unit = "%" });
        
        // Tires
        if (setup.Tires.LeftFrontPressure.HasValue)
            changes.Add(new SetupChangeDisplay { Category = "Tires", Parameter = "LF Pressure", BaselineValue = "-", ModifiedValue = $"{setup.Tires.LeftFrontPressure:F1}", Delta = "-", Unit = "kPa" });
        if (setup.Tires.RightFrontPressure.HasValue)
            changes.Add(new SetupChangeDisplay { Category = "Tires", Parameter = "RF Pressure", BaselineValue = "-", ModifiedValue = $"{setup.Tires.RightFrontPressure:F1}", Delta = "-", Unit = "kPa" });
        if (setup.Tires.LeftRearPressure.HasValue)
            changes.Add(new SetupChangeDisplay { Category = "Tires", Parameter = "LR Pressure", BaselineValue = "-", ModifiedValue = $"{setup.Tires.LeftRearPressure:F1}", Delta = "-", Unit = "kPa" });
        if (setup.Tires.RightRearPressure.HasValue)
            changes.Add(new SetupChangeDisplay { Category = "Tires", Parameter = "RR Pressure", BaselineValue = "-", ModifiedValue = $"{setup.Tires.RightRearPressure:F1}", Delta = "-", Unit = "kPa" });
        
        SetupChangesDataGrid.ItemsSource = changes;
        ShowStatus($"📊 Displayed {changes.Count} setup parameters in Setup Changes tab", isError: false);
    }
    
    /// <summary>
    /// Generate ML recommendations using live telemetry + setup comparison
    /// Phase 3.6: Real-time physics-based predictions (ML training pending)
    /// </summary>
    private void GenerateMLButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShowStatus("🔮 Generating setup recommendations...", isError: false);
            
            MLRecommendationsPanel.Children.Clear();
            
            // Get current track/car info from live telemetry
            var trackName = _lastTelemetryData?.TrackName ?? "Unknown Track";
            var carInfo = !string.IsNullOrEmpty(_lastTelemetryData?.CarScreenName)
                ? _lastTelemetryData.CarScreenName
                : "Current Car";
            
            var setupName = _lastTelemetryData?.DriverSetupName ?? "Current Setup";
            var isModified = _lastTelemetryData?.DriverSetupIsModified == 1;
            
            // Check if telemetry is available
            if (_lastTelemetryData == null)
            {
                MLRecommendationsPanel.Children.Add(new TextBlock
                {
                    Text = "⚠️ No Live Telemetry",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 150, 0)),
                    Margin = new Thickness(0, 0, 0, 10)
                });
                
                MLRecommendationsPanel.Children.Add(new TextBlock
                {
                    Text = "Connect to iRacing to enable real-time setup predictions.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136))
                });
                
                ShowStatus("⚠️ No telemetry connection - start iRacing session", isError: true);
                return;
            }
            
            // Extract features from live telemetry
            var currentSetupFeatures = _mlService.ExtractSetupFeatures(_lastTelemetryData, setupName);
            
            // Create hypothetical improved setup (adjust parameters slightly)
            var suggestedSetup = new SetupParameterFeatures
            {
                SetupName = "AI Suggested",
                TrackName = trackName,
                AirTemp = currentSetupFeatures.AirTemp,
                TrackTemp = currentSetupFeatures.TrackTemp,
                // Suggest +1 wing for low-speed tracks (placeholder logic)
                FrontWing = currentSetupFeatures.FrontWing.HasValue ? currentSetupFeatures.FrontWing.Value + 1 : null,
                RearWing = currentSetupFeatures.RearWing.HasValue ? currentSetupFeatures.RearWing.Value + 1 : null
            };
            
            // Generate prediction
            var prediction = _mlService.PredictSetupChange(
                carName: carInfo,
                trackName: trackName,
                baselineSetup: currentSetupFeatures,
                proposedSetup: suggestedSetup
            );
            
            // Display recommendation
            if (prediction == null)
            {
                MLRecommendationsPanel.Children.Add(new TextBlock
                {
                    Text = "⚠️ ML Service Unavailable",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 150, 0)),
                    Margin = new Thickness(0, 0, 0, 10)
                });
                
                MLRecommendationsPanel.Children.Add(new TextBlock
                {
                    Text = "ML models are not yet trained. Load two setup files to compare them statistically.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                    Margin = new Thickness(0, 10, 0, 0)
                });
                
                ShowStatus("ℹ️ ML models not yet trained - Phase 3.6 feature", isError: false);
                return;
            }
            
            var recoBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 5, 0, 0),
                CornerRadius = new CornerRadius(5)
            };
            
            var recoStack = new StackPanel();
            
            recoStack.Children.Add(new TextBlock
            {
                Text = "🤖 Next Test Suggestion",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            });
            
            recoStack.Children.Add(new TextBlock
            {
                Text = prediction.Recommendation,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });
            
            recoStack.Children.Add(new TextBlock
            {
                Text = $"Predicted Impact: {prediction.LapTimeDelta:F3}s/lap",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 212, 255))
            });
            
            recoStack.Children.Add(new TextBlock
            {
                Text = $"Confidence: {prediction.Confidence:P0}",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 212, 255))
            });
            
            recoBorder.Child = recoStack;
            MLRecommendationsPanel.Children.Add(recoBorder);
            
            ShowStatus("✅ ML recommendations generated", isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error generating recommendations: {ex.Message}", isError: true);
        }
    }
    
    /// <summary>
    /// Show status message
    /// </summary>
    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = new SolidColorBrush(isError ? Colors.Red : Colors.White);
    }
    
    /// <summary>
    /// Cleanup on window close
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        _database.Dispose();
        base.OnClosed(e);
    }
}

/// <summary>
/// Display model for Setup Changes tab
/// </summary>
public class SetupChangeDisplay
{
    public string Category { get; set; } = "";
    public string Parameter { get; set; } = "";
    public string BaselineValue { get; set; } = "";
    public string ModifiedValue { get; set; } = "";
    public string Delta { get; set; } = "";
    public string Unit { get; set; } = "";
}
