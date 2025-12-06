using iRacingOverlay.Core.Models;
using System.Text.Json;
using iRacingOverlay.Core.Services.SetupEngineering;

namespace iRacingOverlay.Core.Services.ML;

/// <summary>
/// ML Model Service for Setup Engineering predictions
/// Loads and manages CatBoost/ONNX models for lap time and setup optimization
/// 
/// Current Models:
/// - formulair04_alltracks_v1.0.cbm: Formula IR-04 lap time predictor
/// - predictor_model.cbm: General lap time predictor
/// 
/// Phase 3.7 Enhancements:
/// - DrivingBehaviorAnalyzer: Automatic handling detection
/// - Neural Network feature extraction (50+ features)
/// - Proactive setup recommendations based on driving behavior
/// 
/// Usage:
/// - Setup Engineering: Predict lap time delta from setup changes
/// - Strategy Scouting: Predict fuel consumption from track/weather
/// - Live Analysis: Analyze driving behavior and suggest improvements
/// </summary>
public class MLModelService
{
    private readonly string _modelsDirectory;
    private readonly Dictionary<string, ModelMetadata> _loadedModels = new();
    private bool _modelsLoaded = false;
    
    // Model cache
    private byte[]? _formulaIR04Model = null;
    private byte[]? _predictorModel = null;
    
    // Behavior analysis
    private readonly DrivingBehaviorAnalyzer _behaviorAnalyzer;
    private readonly List<List<TelemetryData>> _lapBuffer = new();
    
    public MLModelService(string? modelsDirectory = null)
    {
        // Default to %APPDATA%/MRTOverlay/MLModels/
        modelsDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MRTOverlay",
            "MLModels");
        
        _modelsDirectory = modelsDirectory;
        Directory.CreateDirectory(_modelsDirectory);
        
        // Initialize behavior analyzer for Phase 3.7
        _behaviorAnalyzer = new DrivingBehaviorAnalyzer();
        
        Console.WriteLine($"[MLModelService] Initialized with models directory: {_modelsDirectory}");
        Console.WriteLine($"[MLModelService] ✅ Driving behavior analyzer ready");
    }
    
    /// <summary>
    /// Load all available ML models from disk
    /// Called asynchronously at service startup
    /// </summary>
    public async Task LoadModelsAsync()
    {
        if (_modelsLoaded)
            return;
        
        try
        {
            // Scan for .cbm files (CatBoost models)
            var cbmFiles = Directory.GetFiles(_modelsDirectory, "*.cbm");
            Console.WriteLine($"[MLModelService] Found {cbmFiles.Length} CatBoost models");
            
            foreach (var cbmFile in cbmFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(cbmFile);
                var metadataFile = Path.Combine(_modelsDirectory, $"{fileName}_metadata.json");
                
                // Load model bytes
                var modelBytes = await File.ReadAllBytesAsync(cbmFile);
                
                // Load metadata if exists
                ModelMetadata? metadata = null;
                if (File.Exists(metadataFile))
                {
                    var metadataJson = await File.ReadAllTextAsync(metadataFile);
                    metadata = JsonSerializer.Deserialize<ModelMetadata>(metadataJson);
                }
                
                // Cache model
                if (fileName.Contains("formulair04"))
                {
                    _formulaIR04Model = modelBytes;
                    Console.WriteLine($"[MLModelService] ✅ Loaded Formula IR-04 model: {cbmFile} ({modelBytes.Length / 1024}KB)");
                }
                else if (fileName.Contains("predictor"))
                {
                    _predictorModel = modelBytes;
                    Console.WriteLine($"[MLModelService] ✅ Loaded predictor model: {cbmFile} ({modelBytes.Length / 1024}KB)");
                }
                
                // Store metadata
                if (metadata != null)
                {
                    _loadedModels[fileName] = metadata;
                    Console.WriteLine($"[MLModelService]    Metadata: {metadata.Features?.Count ?? 0} features, trained {metadata.TrainingDate}");
                }
            }
            
            _modelsLoaded = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MLModelService] ⚠️ Failed to load models: {ex.Message}");
            Console.WriteLine($"[MLModelService]    Models directory: {_modelsDirectory}");
        }
    }
    
    /// <summary>
    /// Check if ML models are available and loaded
    /// </summary>
    public bool IsAvailable => _modelsLoaded && (_formulaIR04Model != null || _predictorModel != null);
    
    /// <summary>
    /// Get list of loaded model names and metadata
    /// </summary>
    public Dictionary<string, ModelMetadata> GetLoadedModels() => new(_loadedModels);
    
    /// <summary>
    /// Predict lap time improvement from setup change
    /// Returns predicted delta in seconds (negative = faster)
    /// 
    /// Phase 3.6: Physics-based heuristic prediction until ML models are trained
    /// Uses empirical setup parameter impact coefficients from sim racing research
    /// </summary>
    public SetupPrediction? PredictSetupChange(
        string carName,
        string trackName,
        SetupParameterFeatures baselineSetup,
        SetupParameterFeatures proposedSetup)
    {
        Console.WriteLine($"[MLModelService] 🔮 Predicting setup change: {carName} @ {trackName}");
        
        // Physics-based heuristic prediction (until ML models trained)
        var prediction = PredictViaPhysicsHeuristics(baselineSetup, proposedSetup, trackName);
        
        // If ML models are available, use them for refined prediction
        if (IsAvailable && _formulaIR04Model != null)
        {
            // TODO Phase 3.7: Integrate CatBoost/ONNX inference
            // For now, return physics-based prediction
            Console.WriteLine("[MLModelService] ℹ️ Using physics-based prediction (ML inference not yet implemented)");
        }
        
        return prediction;
    }
    
    /// <summary>
    /// Physics-based heuristic prediction system
    /// Uses empirical setup parameter impact coefficients
    /// </summary>
    private SetupPrediction PredictViaPhysicsHeuristics(
        SetupParameterFeatures baseline,
        SetupParameterFeatures proposed,
        string trackName)
    {
        var featureImportance = new Dictionary<string, float>();
        float totalDelta = 0f;
        
        // Aero: Front Wing (higher = more downforce = slower straights, faster corners)
        if (proposed.FrontWing.HasValue && baseline.FrontWing.HasValue)
        {
            var wingDelta = proposed.FrontWing.Value - baseline.FrontWing.Value;
            var trackType = ClassifyTrack(trackName);
            
            // High-speed tracks: More wing = slower lap times (+0.02s per click)
            // Low-speed tracks: More wing = faster lap times (-0.015s per click)
            float wingImpact = trackType == TrackType.HighSpeed ? 0.02f : -0.015f;
            float wingDeltaSeconds = wingDelta * wingImpact;
            
            totalDelta += wingDeltaSeconds;
            featureImportance["FrontWing"] = Math.Abs(wingDeltaSeconds);
        }
        
        // Aero: Rear Wing (similar to front wing but more impact on stability)
        if (proposed.RearWing.HasValue && baseline.RearWing.HasValue)
        {
            var wingDelta = proposed.RearWing.Value - baseline.RearWing.Value;
            var trackType = ClassifyTrack(trackName);
            
            float wingImpact = trackType == TrackType.HighSpeed ? 0.025f : -0.018f;
            float wingDeltaSeconds = wingDelta * wingImpact;
            
            totalDelta += wingDeltaSeconds;
            featureImportance["RearWing"] = Math.Abs(wingDeltaSeconds);
        }
        
        // Chassis: Front ARB (higher = less body roll = faster turn-in)
        if (proposed.FrontARB.HasValue && baseline.FrontARB.HasValue)
        {
            var arbDelta = proposed.FrontARB.Value - baseline.FrontARB.Value;
            // Optimal ARB depends on track, but generally -0.01s per click for understeer fix
            float arbImpact = -0.01f * arbDelta;
            
            totalDelta += arbImpact;
            featureImportance["FrontARB"] = Math.Abs(arbImpact);
        }
        
        // Tires: Cold Pressure (optimal varies by compound, ±1 kPa = ±0.02s)
        var tireDelta = 0f;
        int tireChanges = 0;
        
        if (proposed.LFTirePressure.HasValue && baseline.LFTirePressure.HasValue)
        {
            var pressureDelta = Math.Abs(proposed.LFTirePressure.Value - baseline.LFTirePressure.Value);
            tireDelta += pressureDelta * 0.02f;  // ±0.02s per kPa deviation from optimal
            tireChanges++;
        }
        
        if (tireChanges > 0)
        {
            totalDelta += tireDelta;
            featureImportance["TirePressure"] = tireDelta;
        }
        
        // Confidence scoring based on number of changed parameters
        int changedParams = featureImportance.Count;
        float confidence = changedParams >= 3 ? 0.75f : (changedParams >= 2 ? 0.60f : 0.40f);
        
        // Generate recommendation
        var recommendation = GenerateHeuristicRecommendation(totalDelta, confidence, featureImportance);
        
        return new SetupPrediction
        {
            LapTimeDelta = totalDelta,
            Confidence = confidence,
            Recommendation = recommendation,
            FeatureImportance = featureImportance
        };
    }
    
    /// <summary>
    /// Classify track type for wing prediction
    /// </summary>
    private TrackType ClassifyTrack(string trackName)
    {
        var highSpeedTracks = new[] { "monza", "spa", "lemans", "daytona", "indianapolis" };
        var lowSpeedTracks = new[] { "monaco", "lime rock", "oulton", "brands" };
        
        var lowerTrack = trackName.ToLowerInvariant();
        
        if (highSpeedTracks.Any(t => lowerTrack.Contains(t)))
            return TrackType.HighSpeed;
        
        if (lowSpeedTracks.Any(t => lowerTrack.Contains(t)))
            return TrackType.LowSpeed;
        
        return TrackType.Medium;
    }
    
    /// <summary>
    /// Generate human-readable recommendation from heuristic prediction
    /// </summary>
    private string GenerateHeuristicRecommendation(float delta, float confidence, Dictionary<string, float> importance)
    {
        var rec = "";
        
        if (Math.Abs(delta) < 0.01f)
        {
            rec = "⚖️ NEUTRAL: Predicted delta < 0.01s (minimal impact)";
        }
        else if (delta < 0)
        {
            rec = $"✅ IMPROVEMENT: Predicted {-delta:F3}s faster ({confidence:P0} confidence)";
        }
        else
        {
            rec = $"⚠️ REGRESSION: Predicted +{delta:F3}s slower ({confidence:P0} confidence)";
        }
        
        // Add key parameter insights
        if (importance.Count > 0)
        {
            var topParam = importance.OrderByDescending(kv => kv.Value).First();
            rec += $"\n💡 Key factor: {topParam.Key} ({topParam.Value:F3}s impact)";
        }
        
        rec += "\n\nℹ️ Physics-based prediction (ML models not yet trained)";
        
        return rec;
    }
    
    /// <summary>
    /// Extract features from telemetry data for ML model input
    /// Combines live telemetry (temps) with setup file data
    /// NOTE: Tire cold pressures are not available in real-time telemetry (only in garage/pit settings)
    /// </summary>
    public SetupParameterFeatures ExtractSetupFeatures(Models.TelemetryData telemetry, string setupName)
    {
        return new SetupParameterFeatures
        {
            SetupName = setupName,
            TrackName = telemetry.TrackName ?? "Unknown",
            AirTemp = telemetry.AirTemp,
            TrackTemp = telemetry.TrackTemp
            
            // NOTE: Tire cold pressures, Aero, and Chassis values must be provided
            // from parsed setup files (.htm HTML exports) - not available in live telemetry
        };
    }
    
    /// <summary>
    /// Merge telemetry features with setup file parameters
    /// Used when comparing two setup files with environmental context
    /// </summary>
    public SetupParameterFeatures MergeSetupWithTelemetry(
        SetupEngineering.SetupData setupData,
        Models.TelemetryData telemetry,
        string setupName)
    {
        return new SetupParameterFeatures
        {
            SetupName = setupName,
            TrackName = telemetry.TrackName ?? "Unknown",
            AirTemp = telemetry.AirTemp,
            TrackTemp = telemetry.TrackTemp,
            
            // From setup file
            FrontWing = setupData.Aero.FrontWing,
            RearWing = setupData.Aero.RearWing,
            FrontARB = setupData.Chassis.FrontARB,
            RearARB = setupData.Chassis.RearARB,
            FrontRideHeight = setupData.Chassis.FrontRideHeight,
            RearRideHeight = setupData.Chassis.RearRideHeight,
            
            // Tire pressures from setup file
            LFTirePressure = setupData.Tires.LeftFrontPressure,
            RFTirePressure = setupData.Tires.RightFrontPressure,
            LRTirePressure = setupData.Tires.LeftRearPressure,
            RRTirePressure = setupData.Tires.RightRearPressure
        };
    }
    
    /// <summary>
    /// Copy models from application bundle to %APPDATA% on first run
    /// Called by application startup
    /// </summary>
    public Task CopyBundledModelsAsync(string bundledModelsPath)
    {
        if (!Directory.Exists(bundledModelsPath))
        {
            Console.WriteLine($"[MLModelService] ⚠️ Bundled models not found: {bundledModelsPath}");
            return Task.CompletedTask;
        }
        
        try
        {
            var cbmFiles = Directory.GetFiles(bundledModelsPath, "*.cbm");
            var jsonFiles = Directory.GetFiles(bundledModelsPath, "*_metadata.json");
            
            foreach (var file in cbmFiles.Concat(jsonFiles))
            {
                var destFile = Path.Combine(_modelsDirectory, Path.GetFileName(file));
                
                // Only copy if doesn't exist (don't overwrite user's custom models)
                if (!File.Exists(destFile))
                {
                    File.Copy(file, destFile, overwrite: false);
                    Console.WriteLine($"[MLModelService] ✅ Copied model: {Path.GetFileName(file)}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MLModelService] ⚠️ Failed to copy bundled models: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Analyze current setup and driving behavior, generate proactive recommendations
    /// Phase 3.7: Neural Network Setup Advisor
    /// </summary>
    /// <param name="laps">5-10 laps of telemetry data for analysis</param>
    /// <param name="currentSetup">Current setup parameters (optional)</param>
    /// <returns>Setup recommendation with automatic handling detection</returns>
    public SetupRecommendation? AnalyzeCurrentSetup(
        List<List<TelemetryData>> laps,
        SetupParameterFeatures? currentSetup = null)
    {
        if (laps.Count < 3)
        {
            Console.WriteLine($"[MLModelService] ⚠️ Need at least 3 laps for analysis (got {laps.Count})");
            return null;
        }
        
        Console.WriteLine($"[MLModelService] 🔍 Analyzing {laps.Count} laps for setup recommendations...");
        
        // Analyze driving behavior (automatic detection, no user input!)
        var behaviorFeatures = _behaviorAnalyzer.Analyze(laps);
        
        // Extract 50+ features for Neural Network input
        var nnFeatures = ExtractNNFeatures(behaviorFeatures, currentSetup, laps);
        
        // Generate recommendations based on detected issues
        var recommendations = GenerateRecommendationsFromBehavior(behaviorFeatures, nnFeatures);
        
        Console.WriteLine($"[MLModelService] ✅ Generated {recommendations.Count} recommendations");
        Console.WriteLine($"[MLModelService]    Primary Issue: {behaviorFeatures.PrimaryIssue}");
        Console.WriteLine($"[MLModelService]    Summary: {behaviorFeatures.Summary}");
        
        return new SetupRecommendation
        {
            BehaviorFeatures = behaviorFeatures,
            Recommendations = recommendations,
            AnalyzedLaps = laps.Count,
            Confidence = CalculateConfidence(behaviorFeatures)
        };
    }
    
    /// <summary>
    /// Extract 50+ features for Neural Network input
    /// Combines setup parameters + driving behavior + track conditions
    /// </summary>
    private float[] ExtractNNFeatures(
        DrivingBehaviorFeatures behaviorFeatures,
        SetupParameterFeatures? setup,
        List<List<TelemetryData>> laps)
    {
        var features = new List<float>();
        
        // Setup parameters (15 features)
        features.Add(setup?.FrontWing ?? 0f);
        features.Add(setup?.RearWing ?? 0f);
        features.Add(setup?.FrontARB ?? 0f);
        features.Add(setup?.RearARB ?? 0f);
        features.Add(setup?.FrontRideHeight ?? 0f);
        features.Add(setup?.RearRideHeight ?? 0f);
        features.Add(setup?.LFTirePressure ?? 0f);
        features.Add(setup?.RFTirePressure ?? 0f);
        features.Add(setup?.LRTirePressure ?? 0f);
        features.Add(setup?.RRTirePressure ?? 0f);
        features.Add(setup?.AirTemp ?? 20f);
        features.Add(setup?.TrackTemp ?? 25f);
        features.Add(0f); // Brake bias (TODO)
        features.Add(0f); // Diff preload (TODO)
        features.Add(0f); // Camber front (TODO)
        
        // Driving behavior (10 features)
        features.Add(behaviorFeatures.OversteerSeverity);
        features.Add(behaviorFeatures.UndersteerSeverity);
        features.Add(behaviorFeatures.BrakeStability);
        features.Add(behaviorFeatures.CornerEntryInstability);
        features.Add(behaviorFeatures.CornerExitTraction);
        features.Add(behaviorFeatures.ThrottleSmoothnessScore);
        features.Add(behaviorFeatures.BrakeModulationScore);
        features.Add(behaviorFeatures.SteeringConsistency);
        features.Add(behaviorFeatures.TotalLockups);
        features.Add(behaviorFeatures.CornerProblems.Count); // Number of problematic corners
        
        // Tires (12 features)
        features.Add(behaviorFeatures.FrontTireDegRate);
        features.Add(behaviorFeatures.RearTireDegRate);
        features.Add(behaviorFeatures.TireImbalance);
        
        // Average tire temps (from last lap)
        if (laps.Count > 0)
        {
            var lastLap = laps[^1];
            var avgData = lastLap.Skip(lastLap.Count / 2).Take(lastLap.Count / 4).ToList(); // Mid-lap data
            
            features.Add(avgData.Average(d => d.LFtempCL)); // LF center
            features.Add(avgData.Average(d => d.LFtempCM)); // LF middle
            features.Add(avgData.Average(d => d.LFtempCR)); // LF right
            features.Add(avgData.Average(d => d.RFtempCL)); // RF left
            features.Add(avgData.Average(d => d.RFtempCM)); // RF middle
            features.Add(avgData.Average(d => d.RFtempCR)); // RF right
            features.Add(avgData.Average(d => d.LRtempCL)); // LR center
            features.Add(avgData.Average(d => d.LRtempCM)); // LR middle
            features.Add(avgData.Average(d => d.LRtempCR)); // LR right
        }
        else
        {
            features.AddRange(Enumerable.Repeat(0f, 9)); // Placeholder tire temps
        }
        
        // Track conditions (5 features)
        if (laps.Count > 0 && laps[0].Count > 0)
        {
            var firstLapData = laps[0][0];
            features.Add(firstLapData.TrackTemp);
            features.Add(firstLapData.AirTemp);
            features.Add(firstLapData.TrackTempCrew > 0 ? firstLapData.TrackTempCrew : firstLapData.TrackTemp); // Crew chief track temp
            features.Add(firstLapData.RelativeHumidity);
            features.Add(firstLapData.WindVel); // Wind speed
        }
        else
        {
            features.AddRange(Enumerable.Repeat(0f, 5));
        }
        
        // Performance metrics (8 features)
        features.Add(behaviorFeatures.AverageLapTime);           // Average lap time
        features.Add(behaviorFeatures.LapTimeConsistency);       // Lap consistency (std dev)
        features.Add(behaviorFeatures.LapsAnalyzed);             // Number of laps
        features.Add(behaviorFeatures.AverageOversteerSeverity); // Average oversteer
        features.Add(behaviorFeatures.AverageUndersteerSeverity);// Average understeer
        features.Add(behaviorFeatures.TotalLockups);             // Total lockups
        features.Add(behaviorFeatures.PrimaryIssueSeverity);     // Primary issue severity
        features.Add(0f); // Placeholder for future metric
        
        Console.WriteLine($"[MLModelService] 📊 Extracted {features.Count} features for NN input");
        return features.ToArray();
    }
    
    /// <summary>
    /// Generate setup recommendations from detected behavior issues
    /// Phase 3.7: Physics-based heuristics until NN model trained
    /// </summary>
    private List<SetupAdjustment> GenerateRecommendationsFromBehavior(
        DrivingBehaviorFeatures behavior,
        float[] nnFeatures)
    {
        var recommendations = new List<SetupAdjustment>();
        
        // Oversteer detection (severity > 5/10)
        if (behavior.OversteerSeverity > 5.0f)
        {
            recommendations.Add(new SetupAdjustment
            {
                Parameter = "Rear ARB",
                Change = "+2 clicks",
                Reason = $"Oversteer detected (severity {behavior.OversteerSeverity:F1}/10)",
                PredictedDelta = -0.08f, // Expected improvement
                Confidence = 0.75f
            });
            
            // Check for corner-specific oversteer
            var worstCorner = behavior.CornerProblems
                .Where(kv => kv.Value.IssueType == "Oversteer")
                .OrderByDescending(kv => kv.Value.Severity)
                .FirstOrDefault();
            
            if (worstCorner.Value != null)
            {
                recommendations.Add(new SetupAdjustment
                {
                    Parameter = "Rear Wing",
                    Change = "+1 click",
                    Reason = $"Severe oversteer in Turn {worstCorner.Key} (severity {worstCorner.Value.Severity:F1}/10)",
                    PredictedDelta = -0.05f,
                    Confidence = 0.70f
                });
            }
        }
        
        // Understeer detection (severity > 5/10)
        if (behavior.UndersteerSeverity > 5.0f)
        {
            recommendations.Add(new SetupAdjustment
            {
                Parameter = "Front ARB",
                Change = "-1 click",
                Reason = $"Understeer detected (severity {behavior.UndersteerSeverity:F1}/10)",
                PredictedDelta = -0.06f,
                Confidence = 0.75f
            });
            
            recommendations.Add(new SetupAdjustment
            {
                Parameter = "Front Wing",
                Change = "+1 click",
                Reason = "Increase front downforce for better turn-in",
                PredictedDelta = -0.04f,
                Confidence = 0.65f
            });
        }
        
        // Brake lockup detection
        if (behavior.TotalLockups > 2)
        {
            recommendations.Add(new SetupAdjustment
            {
                Parameter = "Brake Bias",
                Change = "+0.5% rearward",
                Reason = $"Brake lockups detected ({behavior.TotalLockups} instances)",
                PredictedDelta = -0.03f,
                Confidence = 0.80f
            });
        }
        
        // Tire temperature imbalance
        if (Math.Abs(behavior.TireImbalance) > 5.0f)
        {
            var side = behavior.TireImbalance > 0 ? "left" : "right";
            recommendations.Add(new SetupAdjustment
            {
                Parameter = "Front Tire Pressure",
                Change = $"{(behavior.TireImbalance > 0 ? "-0.5" : "+0.5")} kPa on {side}",
                Reason = $"Tire temperature imbalance ({Math.Abs(behavior.TireImbalance):F1}°C difference)",
                PredictedDelta = -0.02f,
                Confidence = 0.60f
            });
        }
        
        // Sort by predicted improvement (most impactful first)
        return recommendations.OrderByDescending(r => Math.Abs(r.PredictedDelta)).ToList();
    }
    
    /// <summary>
    /// Calculate confidence score for recommendations
    /// Based on lap count, issue severity, and consistency
    /// </summary>
    private float CalculateConfidence(DrivingBehaviorFeatures behavior)
    {
        float confidence = 0.5f; // Base confidence
        
        // More laps = higher confidence (up to +0.3)
        if (behavior.LapsAnalyzed >= 10) confidence += 0.3f;
        else if (behavior.LapsAnalyzed >= 5) confidence += 0.2f;
        else confidence += 0.1f;
        
        // Clear primary issue = higher confidence (+0.2)
        if (!string.IsNullOrEmpty(behavior.PrimaryIssue) && behavior.PrimaryIssue != "Balanced")
        {
            confidence += 0.2f;
        }
        
        return Math.Min(1.0f, confidence);
    }
    
    /// <summary>
    /// Calculate standard deviation for lap consistency
    /// </summary>
    private float CalculateStdDev(List<float> values)
    {
        if (values.Count == 0) return 0f;
        
        var avg = values.Average();
        var sumSquares = values.Sum(v => (v - avg) * (v - avg));
        return (float)Math.Sqrt(sumSquares / values.Count);
    }
}

/// <summary>
/// Model metadata (loaded from {model}_metadata.json)
/// </summary>
public class ModelMetadata
{
    public string? ModelName { get; set; }
    public string? ModelVersion { get; set; }
    public string? TrainingDate { get; set; }
    public List<string>? Features { get; set; }
    public string? TargetVariable { get; set; }
    public Dictionary<string, float>? Metrics { get; set; }
}

/// <summary>
/// Setup parameter features for ML model input
/// Populated from live telemetry + parsed setup files
/// </summary>
public class SetupParameterFeatures
{
    public string SetupName { get; set; } = string.Empty;
    public string TrackName { get; set; } = string.Empty;
    public float AirTemp { get; set; }
    public float TrackTemp { get; set; }
    
    // Aero
    public float? FrontWing { get; set; }
    public float? RearWing { get; set; }
    
    // Chassis/ARB
    public float? FrontARB { get; set; }
    public float? RearARB { get; set; }
    
    // Tires
    public float? LFTirePressure { get; set; }
    public float? RFTirePressure { get; set; }
    public float? LRTirePressure { get; set; }
    public float? RRTirePressure { get; set; }
    
    // Ride Heights
    public float? FrontRideHeight { get; set; }
    public float? RearRideHeight { get; set; }
}

/// <summary>
/// Track classification for setup predictions
/// </summary>
internal enum TrackType
{
    HighSpeed,   // Monza, Spa, Le Mans - long straights, less downforce optimal
    Medium,      // Most tracks
    LowSpeed     // Monaco, Lime Rock - tight corners, more downforce optimal
}

/// <summary>
/// Setup prediction result from ML model
/// </summary>
public class SetupPrediction
{
    /// <summary>
    /// Predicted lap time delta in seconds (negative = faster)
    /// </summary>
    public float LapTimeDelta { get; set; }
    
    /// <summary>
    /// Prediction confidence (0.0 - 1.0)
    /// </summary>
    public float Confidence { get; set; }
    
    /// <summary>
    /// Human-readable recommendation
    /// </summary>
    public string Recommendation { get; set; } = string.Empty;
    
    /// <summary>
    /// Feature importance scores (which setup parameters matter most)
    /// </summary>
    public Dictionary<string, float> FeatureImportance { get; set; } = new();
    
    /// <summary>
    /// Whether this prediction is reliable enough to show to user
    /// </summary>
    public bool IsHighConfidence => Confidence >= 0.8f;
}

/// <summary>
/// Setup recommendation from driving behavior analysis (Phase 3.7)
/// Proactive recommendations based on automatic handling detection
/// </summary>
public class SetupRecommendation
{
    /// <summary>
    /// Detected driving behavior features (auto-detected, no user input!)
    /// </summary>
    public DrivingBehaviorFeatures BehaviorFeatures { get; set; } = new();
    
    /// <summary>
    /// List of recommended setup adjustments (ranked by impact)
    /// </summary>
    public List<SetupAdjustment> Recommendations { get; set; } = new();
    
    /// <summary>
    /// Number of laps analyzed
    /// </summary>
    public int AnalyzedLaps { get; set; }
    
    /// <summary>
    /// Overall confidence in recommendations (0.0 - 1.0)
    /// </summary>
    public float Confidence { get; set; }
    
    /// <summary>
    /// Whether recommendations are reliable (confidence >= 70%)
    /// </summary>
    public bool IsReliable => Confidence >= 0.7f;
}

/// <summary>
/// Individual setup adjustment recommendation
/// </summary>
public class SetupAdjustment
{
    /// <summary>
    /// Setup parameter to adjust (e.g., "Rear ARB", "Front Wing")
    /// </summary>
    public string Parameter { get; set; } = string.Empty;
    
    /// <summary>
    /// Recommended change (e.g., "+2 clicks", "-0.5% forward")
    /// </summary>
    public string Change { get; set; } = string.Empty;
    
    /// <summary>
    /// Reason for recommendation (e.g., "Oversteer detected in Turn 7")
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// Predicted lap time delta in seconds (negative = faster)
    /// </summary>
    public float PredictedDelta { get; set; }
    
    /// <summary>
    /// Confidence in this specific recommendation (0.0 - 1.0)
    /// </summary>
    public float Confidence { get; set; }
}
